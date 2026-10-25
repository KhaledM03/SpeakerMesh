using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SpeakerMesh;
public sealed class AudioSession {
 readonly CancellationTokenSource stop=new();
 readonly ManualResetEventSlim go=new(false);
 readonly List<Task> workers=[];
 readonly object shutdownGate=new();Task? shutdown;
 readonly List<OutputConfig> outputs;
 readonly Dictionary<string,LiveGain> gains;
 public LiveGain Master {get;}=new();
 public OutputHealth Health {get;}
 public void SetOutputGain(string id,double gain,bool muted) { if(gains.TryGetValue(id,out var control)) control.Set(gain,muted); }
 readonly string? captureId;
 readonly IAudioSource? fileSource;
 readonly TaskCompletionSource<IAudioSource> sourceReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly Action<string> report;
 readonly IReadOnlyDictionary<string,IAudioSource>? outputSources;
 public long StartTimeQpc=>Volatile.Read(ref epoch);
 public int SampleRate=>sourceReady.Task.IsCompletedSuccessfully?sourceReady.Task.Result.Rate:48000;
 long epoch;
 int failed;
 public SessionDiagnostics Diagnostics {get;}=new();
 public AudioSession(List<OutputConfig> outputs,IAudioSource? source,string? captureId,Action<string> report,IReadOnlyDictionary<string,IAudioSource>? outputSources=null) {
  if(outputs.Count==0) throw new ArgumentException("Select at least one output.");
  if(outputs.Select(x=>x.Id).Distinct().Count()!=outputs.Count) throw new ArgumentException("Duplicate outputs.");
  if(captureId!=null && outputs.Any(x=>x.Id==captureId)) throw new ArgumentException("The system-audio source cannot also be a destination. That would create feedback.");
  if(outputs.Any(x=>!double.IsFinite(x.DelayMs)||x.DelayMs<0||x.DelayMs>2000||!double.IsFinite(x.Gain)||x.Gain<0||x.Gain>1))
   throw new ArgumentException("Delay must be 0-2000 ms; volume must be 0-100%.");
  if(source==null && captureId==null) throw new ArgumentException("Choose an audio source.");
  this.outputs=outputs;
  Health=new(outputs.Select(x=>x.Id));
  gains=outputs.ToDictionary(x=>x.Id,x=> {var gain=new LiveGain();gain.Set(x.Gain,x.Muted);return gain;}); fileSource=source; this.captureId=captureId; this.report=report;this.outputSources=outputSources;
 }
 double Elapsed=>(Stopwatch.GetTimestamp()-Volatile.Read(ref epoch))/(double)Stopwatch.Frequency;
 public async Task StartAsync() {
  try {
  if(captureId!=null) workers.Add(Worker(Capture)); else sourceReady.TrySetResult(fileSource!);
  var source=await sourceReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
  using var ready=new CountdownEvent(outputs.Count);
  foreach(var config in outputs) workers.Add(Worker(()=>Render(config,outputSources!=null&&outputSources.TryGetValue(config.Id,out var perOutput)?perOutput:source,ready),config));
  try {
   await Task.Run(()=> { if(!ready.Wait(TimeSpan.FromSeconds(10),stop.Token)) throw new TimeoutException("Audio devices did not initialize in time."); });
   stop.Token.ThrowIfCancellationRequested();
   Volatile.Write(ref epoch,Stopwatch.GetTimestamp()); go.Set();
   report($"Playing to {outputs.Count} output(s). Volume and mute are live; stop to change routing or delay.");
  } catch { await StopAsync(); throw; }
  } catch { await StopAsync(); throw; }
 }
 Task Worker(Action action,OutputConfig? output=null)=>Task.Factory.StartNew(()=> {
  try { action(); }
  catch(OperationCanceledException) { }
  catch(Exception ex) {
   if(output!=null && go.IsSet && !stop.IsCancellationRequested && outputSources==null) {
    Health.Fail(output.Id,ex.Message);
    report(output.Name+": disconnected or failed. Other speakers continue. Stop and restart to rejoin; recalibrate afterward. "+ex.Message);
    if(Health.Remaining==0) stop.Cancel();
    return;
   }
   sourceReady.TrySetException(ex);
   if(Interlocked.Exchange(ref failed,1)==0) { report("Audio stopped: "+ex.Message); stop.Cancel(); go.Set(); }
  }
 },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
 void Render(OutputConfig config,IAudioSource source,CountdownEvent ready) {
  IAudioClient? client=null; IAudioRenderClient? render=null; IntPtr format=IntPtr.Zero; bool started=false;
  try {
   client=NativeAudio.Open(config.Id); format=NativeAudio.FloatFormat(source.Rate);
   // Shared-mode automatic sample-rate and channel conversion, not exclusive mode.
   NativeAudio.Check(client.Initialize(0,0x88000000,1600000,0,format,IntPtr.Zero));
   NativeAudio.Check(client.GetBufferSize(out uint capacity));
   Guid iid=NativeAudio.RenderId; NativeAudio.Check(client.GetService(ref iid,out object service)); render=(IAudioRenderClient)service;
   Thread.CurrentThread.Priority=ThreadPriority.AboveNormal;
   var buffer=new float[capacity*2]; var scratch=new float[((int)Math.Ceiling(capacity*1.002)+2)*2];
   ready.Signal(); go.Wait(stop.Token); stop.Token.ThrowIfCancellationRequested();
   // Prime the complete endpoint buffer before starting; never start an empty stream.
   var cursor=new PlaybackCursor();var ramp=new GainRamp();
   cursor.Prepare(Mix.Position(Elapsed,0,source.Rate,350,config.DelayMs),source.Rate,source is LiveSource);
   cursor.Fill(source,buffer,scratch,(int)capacity); ramp.Apply(buffer,(int)capacity,source.Rate,gains[config.Id].Effective*Master.Effective,config.Channel);
   NativeAudio.Check(render.GetBuffer(capacity,out IntPtr initial));
   try { Marshal.Copy(buffer,0,initial,(int)capacity*2); }
   finally { NativeAudio.Check(render.ReleaseBuffer(capacity,0)); }
   NativeAudio.Check(client.Start()); started=true;
   while(!stop.IsCancellationRequested) {
    NativeAudio.Check(client.GetCurrentPadding(out uint padding));
    uint count=capacity-padding;
    if(count>0) {
     double desired=Mix.Position(Elapsed,(int)padding,source.Rate,350,config.DelayMs);
     cursor.Prepare(desired,source.Rate,source is LiveSource);
     cursor.Fill(source,buffer,scratch,(int)count);
     ramp.Apply(buffer,(int)count,source.Rate,gains[config.Id].Effective*Master.Effective,config.Channel);
     NativeAudio.Check(render.GetBuffer(count,out IntPtr target));
     try { Marshal.Copy(buffer,0,target,(int)count*2); }
     finally { NativeAudio.Check(render.ReleaseBuffer(count,0)); }
     Diagnostics.Render((int)count,padding==0);
    }
    if(double.IsFinite(source.Duration) && Elapsed>source.Duration+2.5) {
     if(Interlocked.Exchange(ref failed,1)==0) report("Playback complete."); stop.Cancel(); break;
    }
    stop.Token.WaitHandle.WaitOne(3);
   }
  } catch(Exception ex) when(ex is not OperationCanceledException) { throw new InvalidOperationException(config.Name+": "+ex.Message,ex); }
  finally {
   if(started) client?.Stop(); NativeAudio.Release(render); NativeAudio.Release(client);
   if(format!=IntPtr.Zero) Marshal.FreeCoTaskMem(format);
  }
 }
 void Capture() {
  IAudioClient? client=null; IAudioCaptureClient? capture=null; IntPtr formatPtr=IntPtr.Zero; bool started=false;
  try {
   client=NativeAudio.Open(captureId!); NativeAudio.Check(client.GetMixFormat(out formatPtr));
   var format=NativeAudio.ReadFormat(formatPtr);
   if(format.Channels>2) throw new InvalidOperationException("Set the capture endpoint to stereo in Windows Sound settings first.");
   var source=new LiveSource(format.Rate);
   NativeAudio.Check(client.Initialize(0,0x00020000,1000000,0,formatPtr,IntPtr.Zero));
   Guid iid=NativeAudio.CaptureId; NativeAudio.Check(client.GetService(ref iid,out object service)); capture=(IAudioCaptureClient)service;
   sourceReady.TrySetResult(source); go.Wait(stop.Token); stop.Token.ThrowIfCancellationRequested();
   NativeAudio.Check(client.Start()); started=true;
   var timeline=new CaptureTimeline();
   while(!stop.IsCancellationRequested) {
    NativeAudio.Check(capture.GetNextPacketSize(out uint available));
    while(available>0) {
     NativeAudio.Check(capture.GetBuffer(out var ptr,out uint frames,out uint flags,out ulong devicePosition,out ulong qpc));
     try {
      // QPC is in 100 ns units, matching the first captured frame's device timestamp.
      double start=(flags&4)==0 && qpc>0?qpc/10000000.0-Volatile.Read(ref epoch)/(double)Stopwatch.Frequency:Elapsed-frames/(double)format.Rate;
      var data=new byte[checked((int)frames*format.BlockAlign)];
      if((flags&2)==0) Marshal.Copy(ptr,data,0,data.Length);
      if((flags&1)!=0) Diagnostics.Discontinuity();
      var decoded=format.Decode(data); Diagnostics.Capture(decoded);
      source.Write(timeline.Map(devicePosition,start,format.Rate,(flags&1)!=0),decoded);
     } finally { NativeAudio.Check(capture.ReleaseBuffer(frames)); }
     NativeAudio.Check(capture.GetNextPacketSize(out available));
    }
    stop.Token.WaitHandle.WaitOne(3);
   }
  } finally {
   if(started) client?.Stop(); NativeAudio.Release(capture); NativeAudio.Release(client);
   if(formatPtr!=IntPtr.Zero) Marshal.FreeCoTaskMem(formatPtr);
  }
 }
 public bool Finished=>stop.IsCancellationRequested;
 public Task StopAsync() {lock(shutdownGate) return shutdown??=StopWorkers();}
 async Task StopWorkers() {
  stop.Cancel();go.Set();
  try {await Task.WhenAll(workers);}
  finally {go.Dispose();stop.Dispose();}
 }
}





