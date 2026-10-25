using System.Diagnostics;
namespace SpeakerMesh;

// Keeps WAV/test playback continuous regardless of polling jitter. Live playback
// may slew gently toward capture time, but never hard-seeks after a late callback.
public sealed class PlaybackCursor {
 public double Position { get; private set; }
 public bool Initialized { get; private set; }
 public double Step { get; private set; }=1;
 public void Prepare(double desired,int rate,bool live) {
  if(!Initialized) { Position=desired;Initialized=true; }
  Step=live?1+Math.Clamp((desired-Position)/(rate*10),-.001,.001):1;
 }
 public void Advance(int frames)=>Position+=frames*Step;
 public void Fill(IAudioSource source,float[] output,float[] scratch,int frames) {
  if(Step==1) source.Read(Position,output,frames);
  else {
   int needed=(int)Math.Ceiling(frames*Step)+2;source.Read(Position,scratch,needed);
   for(int i=0;i<frames;i++) {
    double p=i*Step;int a=(int)p;float f=(float)(p-a);
    for(int ch=0;ch<2;ch++) output[i*2+ch]=scratch[a*2+ch]+(scratch[(a+1)*2+ch]-scratch[a*2+ch])*f;
   }
  }
  Advance(frames);
 }
}

// WASAPI device positions count frames continuously. Recomputing every packet's
// start from rounded QPC timestamps can leave gaps/overlaps in the capture ring.
public sealed class CaptureTimeline {
 bool initialized;ulong anchorDevice;long anchorFrame;
 public long Map(ulong devicePosition,double timestampSeconds,int rate,bool discontinuity=false) {
  if(!initialized || discontinuity || devicePosition<anchorDevice) {
   anchorDevice=devicePosition;anchorFrame=(long)Math.Round(timestampSeconds*rate);initialized=true;
  }
  return anchorFrame+checked((long)(devicePosition-anchorDevice));
 }
}

public sealed class SessionDiagnostics {
 long capturedFrames,renderedFrames,underruns,discontinuities;
 long lastAudioTicks;
 int peakBits;
 public long CapturedFrames=>Interlocked.Read(ref capturedFrames);
 public long RenderedFrames=>Interlocked.Read(ref renderedFrames);
 public long Underruns=>Interlocked.Read(ref underruns);
 public long Discontinuities=>Interlocked.Read(ref discontinuities);
 public float Peak=>BitConverter.Int32BitsToSingle(Volatile.Read(ref peakBits));
 public bool HasRecentAudio=>Stopwatch.GetTimestamp()-Interlocked.Read(ref lastAudioTicks)<Stopwatch.Frequency*2 && Interlocked.Read(ref lastAudioTicks)>0;
 public void Capture(float[] samples) {
  Interlocked.Add(ref capturedFrames,samples.Length/2);float peak=0;
  foreach(float sample in samples) peak=Math.Max(peak,Math.Abs(sample));
  Volatile.Write(ref peakBits,BitConverter.SingleToInt32Bits(peak));
  if(peak>.00001f) Interlocked.Exchange(ref lastAudioTicks,Stopwatch.GetTimestamp());
 }
 public void Render(int frames,bool underrun) { Interlocked.Add(ref renderedFrames,frames);if(underrun) Interlocked.Increment(ref underruns); }
 public void Discontinuity()=>Interlocked.Increment(ref discontinuities);
}
