using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SpeakerMesh;

// Microphone data exists only in this bounded memory buffer, never in a WAV file.
internal sealed class MicrophoneRecorder(string deviceId):IAsyncDisposable {
 readonly CancellationTokenSource stop=new();readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);readonly object gate=new();
 Task? worker;float[] samples=[];int used,rate;ulong firstPosition;double origin;bool initialized;int gaps;
 public Exception? Error {get;private set;}
 public int Gaps=>Volatile.Read(ref gaps);
 public async Task StartAsync(CancellationToken cancellation) {
  worker=Task.Factory.StartNew(Capture,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
  await ready.Task.WaitAsync(TimeSpan.FromSeconds(10),cancellation);
 }
 public AcousticRecording Snapshot() {
  lock(gate){if(!initialized || used==0)throw new InvalidOperationException("No microphone samples arrived. Check the selected microphone and desktop-app microphone access.");return new(samples.AsSpan(0,used).ToArray(),rate,origin);}
 }
 void Capture() {
  IAudioClient? client=null;IAudioCaptureClient? capture=null;IntPtr formatPointer=IntPtr.Zero;bool started=false;
  try {
   client=NativeAudio.Open(deviceId);NativeAudio.Check(client.GetMixFormat(out formatPointer));var format=NativeAudio.ReadFormat(formatPointer);rate=format.Rate;
   NativeAudio.Check(client.Initialize(0,0,1000000,0,formatPointer,IntPtr.Zero));Guid iid=NativeAudio.CaptureId;NativeAudio.Check(client.GetService(ref iid,out object service));capture=(IAudioCaptureClient)service;
   NativeAudio.Check(client.Start());started=true;ready.TrySetResult();
   while(!stop.IsCancellationRequested) {
    NativeAudio.Check(capture.GetNextPacketSize(out uint available));
    while(available>0 && !stop.IsCancellationRequested) {
     NativeAudio.Check(capture.GetBuffer(out var pointer,out uint frames,out uint flags,out ulong position,out ulong qpc));
     try {
      if(frames>0) {
       var bytes=new byte[checked((int)frames*format.BlockAlign)];if((flags&2)==0)Marshal.Copy(pointer,bytes,0,bytes.Length);var stereo=format.Decode(bytes);
       lock(gate) {
        if(!initialized){initialized=true;firstPosition=position;origin=(flags&4)==0&&qpc>0?qpc/10000000.0:Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency-frames/(double)rate;samples=new float[rate*2];}
        else if((flags&1)!=0)Interlocked.Increment(ref gaps);
        if(position<firstPosition)throw new InvalidOperationException("The microphone clock reset. Retry calibration.");
        long offset=(long)(position-firstPosition),end=offset+frames;
        if(end>rate*110L)throw new InvalidOperationException("Calibration exceeded the microphone time limit.");
        if(end>samples.Length)Array.Resize(ref samples,(int)Math.Min(rate*110L,Math.Max(end,samples.Length*2L)));
        for(int i=0;i<frames;i++)samples[offset+i]=(stereo[i*2]+stereo[i*2+1])*.5f;used=Math.Max(used,(int)end);
       }
      }
     }finally{NativeAudio.Check(capture.ReleaseBuffer(frames));}
     NativeAudio.Check(capture.GetNextPacketSize(out available));
    }
    stop.Token.WaitHandle.WaitOne(3);
   }
  }catch(Exception ex){Error=ex;ready.TrySetException(ex);}
  finally{if(started)client?.Stop();NativeAudio.Release(capture);NativeAudio.Release(client);if(formatPointer!=IntPtr.Zero)Marshal.FreeCoTaskMem(formatPointer);}
 }
 public async ValueTask DisposeAsync() {
  stop.Cancel();if(worker!=null)await worker;lock(gate){Array.Clear(samples);samples=[];used=0;}stop.Dispose();
 }
}
internal static class AcousticCalibrator {
 public static async Task<CalibrationResult> RunAsync(List<OutputConfig> outputs,string microphone,IProgress<string> progress,CancellationToken cancellation,int playbackRate=48000) {
  if(outputs.Count<2 || outputs.Count>6)throw new InvalidOperationException("Select between two and six connected speakers for Auto Sync.");
  if(outputs.Any(x=>x.Gain<=0))throw new InvalidOperationException("Turn up every selected speaker above zero before calibration.");
  await using var recorder=new MicrophoneRecorder(microphone);progress.Report("Starting microphone. Audio stays on this computer.");await recorder.StartAsync(cancellation);
  var first=await Round(outputs.Select(x=>x with{DelayMs=0,Gain=Math.Min(x.Gain,.35)}).ToList(),recorder,progress,cancellation,"Measuring",playbackRate);
  var delays=AcousticAnalysis.Corrections(first);
  var adjusted=outputs.Select(x=>x with{DelayMs=delays[x.Id],Gain=Math.Min(x.Gain,.35)}).ToList();
  var verified=await Round(adjusted,recorder,progress,cancellation,"Checking",playbackRate);
  double residual=AcousticAnalysis.Verify(verified);cancellation.ThrowIfCancellationRequested();return new(delays,first,residual,DateTime.UtcNow);
 }
 static async Task<List<SpeakerMeasurement>> Round(List<OutputConfig> outputs,MicrophoneRecorder recorder,IProgress<string> progress,CancellationToken cancellation,string label,int playbackRate) {
  var sources=outputs.Select((x,i)=>(x.Id,Source:(IAudioSource)new ProbeSource(i,outputs.Count,playbackRate))).ToDictionary(x=>x.Id,x=>x.Source);
  string? error=null;var clock=sources[outputs[0].Id];var session=new AudioSession(outputs,clock,null,message=>{if(message.StartsWith("Audio stopped:"))error=message;},sources);
  try {
   await session.StartAsync();double origin=session.StartTimeQpc/(double)Stopwatch.Frequency;
   var elapsed=Stopwatch.StartNew();double duration=clock.Duration+.35;
   while(elapsed.Elapsed.TotalSeconds<duration) {
    cancellation.ThrowIfCancellationRequested();if(recorder.Error!=null)throw recorder.Error;if(session.Finished)throw new InvalidOperationException(error??"A speaker disconnected during calibration.");
    int index=Math.Clamp((int)((elapsed.Elapsed.TotalSeconds-2)/4),0,outputs.Count-1);progress.Report($"{label} {outputs[index].Name}  ({index+1}/{outputs.Count}). Keep the room quiet.");await Task.Delay(180,cancellation);
   }
   await session.StopAsync();
   if(recorder.Gaps>0)throw new InvalidOperationException("The microphone dropped audio during calibration. Previous delays are unchanged; retry with a PC/USB microphone.");
   var recording=recorder.Snapshot();
   try {
    return await Task.Run(()=>outputs.Select((x,index)=> {
     cancellation.ThrowIfCancellationRequested();progress.Report($"Analyzing {x.Name}...");
     var matches=Enumerable.Range(0,3).Select(r=>AcousticAnalysis.Find(recording,origin+.35+ProbeSource.At(index,r),r)).ToList();return AcousticAnalysis.Measure(x.Id,x.Name,matches);
    }).ToList(),cancellation);
   }finally{Array.Clear(recording.Samples);}
  }finally{await session.StopAsync();}
 }
}


