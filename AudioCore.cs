using System.Text;
namespace SpeakerMesh;
public enum ChannelMode { Stereo, Left, Right, Mono }
public record OutputConfig(string Id, string Name, double DelayMs=0, double Gain=.5, ChannelMode Channel=ChannelMode.Stereo, bool Muted=false);
public record Profile(string SourceId, string FilePath, int SourceMode, List<OutputConfig> Outputs, int Version=1, bool MirrorInputVolume=false);
public interface IAudioSource { int Rate { get; } double Duration { get; } void Read(double start, float[] destination, int frames); }
public static class Mix {
 public static double Position(double elapsed,int padding,int rate,double baseMs,double delayMs)=>(elapsed-(baseMs+delayMs)/1000)*rate+padding;
 public static void Apply(float[] data,int frames,double gain,ChannelMode mode) {
  for(int i=0;i<frames*2;i+=2) {
   float l=data[i],r=data[i+1];
   (l,r)=mode switch { ChannelMode.Left=>(l,l),ChannelMode.Right=>(r,r),ChannelMode.Mono=>((l+r)*.5f,(l+r)*.5f),_=>(l,r) };
   data[i]=Math.Clamp((float)(l*gain),-1,1); data[i+1]=Math.Clamp((float)(r*gain),-1,1);
  }
 }
}
public sealed class PulseSource:IAudioSource {
 public int Rate=>48000; public double Duration=>double.PositiveInfinity;
 public void Read(double start,float[] data,int frames) {
  for(int i=0;i<frames;i++) {
   double t=(start+i)/Rate,p=t-Math.Floor(t);
   float v=t>=0 && p<.035?(float)(.18*Math.Sin(2*Math.PI*880*t)*Math.Pow(Math.Sin(Math.PI*p/.035),2)):0;
   data[i*2]=data[i*2+1]=v;
  }
 }
}
public record PcmFormat(int Rate,int Channels,int Bits,int BlockAlign,bool IsFloat) {
 public static PcmFormat Parse(byte[] bytes) {
  if(bytes.Length<16) throw new InvalidDataException("Incomplete audio format.");
  int tag=BitConverter.ToUInt16(bytes,0),ch=BitConverter.ToUInt16(bytes,2),rate=BitConverter.ToInt32(bytes,4);
  int align=BitConverter.ToUInt16(bytes,12),bits=BitConverter.ToUInt16(bytes,14);
  if(tag==65534) {
   if(bytes.Length<40) throw new InvalidDataException("Incomplete extensible format.");
   var guid=new Guid(bytes.AsSpan(24,16));
   if(guid==new Guid("00000001-0000-0010-8000-00aa00389b71")) tag=1;
   else if(guid==new Guid("00000003-0000-0010-8000-00aa00389b71")) tag=3;
   else throw new InvalidDataException("Unsupported audio subtype.");
  }
  if(rate<8000 || rate>192000 || ch<1 || ch>32 || align!=ch*bits/8 || !((tag==3 && bits==32)||(tag==1 && bits is 16 or 24 or 32)))
   throw new InvalidDataException("Use PCM 16/24/32-bit or float32 audio, 8-192 kHz.");
  return new(rate,ch,bits,align,tag==3);
 }
 public float[] Decode(byte[] bytes) {
  int frames=bytes.Length/BlockAlign,width=Bits/8; var output=new float[frames*2];
  for(int i=0;i<frames;i++) for(int ch=0;ch<2;ch++) {
   int p=i*BlockAlign+Math.Min(ch,Channels-1)*width;
   float v=IsFloat?BitConverter.ToSingle(bytes,p):Bits switch {
    16=>BitConverter.ToInt16(bytes,p)/32768f,
    24=>((bytes[p]|bytes[p+1]<<8|bytes[p+2]<<16)<<8>>8)/8388608f,
    _=>(float)(BitConverter.ToInt32(bytes,p)/2147483648.0)
   };
   output[i*2+ch]=float.IsFinite(v)?Math.Clamp(v,-1,1):0;
  }
  return output;
 }
}
public sealed class WavSource:IAudioSource {
 readonly float[] samples;
 public int Rate {get;} public double Duration=>samples.Length/(2.0*Rate);
 public WavSource(string path) {
  using var r=new BinaryReader(File.OpenRead(path));
  if(new string(r.ReadChars(4))!="RIFF") throw new InvalidDataException("Choose an uncompressed RIFF WAV.");
  r.ReadUInt32(); if(new string(r.ReadChars(4))!="WAVE") throw new InvalidDataException("Not a WAV file.");
  byte[]? format=null,data=null;
  while(r.BaseStream.Position+8<=r.BaseStream.Length) {
   string tag=Encoding.ASCII.GetString(r.ReadBytes(4)); uint size=r.ReadUInt32();
   if(size>r.BaseStream.Length-r.BaseStream.Position) throw new InvalidDataException("Truncated WAV.");
   if(tag=="fmt ") {
    if(size>4096) throw new InvalidDataException("Unsupported WAV header."); format=r.ReadBytes((int)size);
   } else if(tag=="data") {
    if(size>100*1024*1024) throw new InvalidDataException("WAV exceeds 100 MB. Use system audio for longer playback."); data=r.ReadBytes((int)size);
   } else r.BaseStream.Seek(size,SeekOrigin.Current);
   if((size&1)!=0) r.BaseStream.Seek(1,SeekOrigin.Current);
  }
  if(format==null || data==null) throw new InvalidDataException("Missing WAV format or data.");
  var f=PcmFormat.Parse(format);
  if(f.Channels>2) throw new InvalidDataException("Use a mono or stereo WAV. Surround decoding is not included.");
  if(data.Length%f.BlockAlign!=0) throw new InvalidDataException("Incomplete WAV audio frame.");
  Rate=f.Rate; samples=f.Decode(data); if(samples.Length==0) throw new InvalidDataException("WAV is empty.");
 }
 public void Read(double start,float[] output,int frames) {
  for(int i=0;i<frames;i++) {
   double p=start+i; long a=(long)Math.Floor(p); float frac=(float)(p-a);
   for(int ch=0;ch<2;ch++) {
    float l=a>=0 && a<samples.Length/2?samples[a*2+ch]:0;
    float r=a+1>=0 && a+1<samples.Length/2?samples[(a+1)*2+ch]:0;
    output[i*2+ch]=l+(r-l)*frac;
   }
  }
 }
}
// Absolute frame tags prevent stale playback after wrap and silence.
public sealed class LiveSource(int rate,int seconds=8):IAudioSource {
 readonly object gate=new(); readonly float[] samples=new float[rate*seconds*2];
 readonly long[] tags=Enumerable.Repeat(long.MinValue,rate*seconds).ToArray();
 public int Rate=>rate; public double Duration=>double.PositiveInfinity;
 public void Write(long start,float[] data) {
  lock(gate) for(int i=0;i<data.Length/2;i++) {
   long f=start+i; if(f<0) continue; int slot=(int)(f%tags.Length);
   tags[slot]=f; samples[slot*2]=data[i*2]; samples[slot*2+1]=data[i*2+1];
  }
 }
 float At(long f,int ch)=>f>=0 && tags[f%tags.Length]==f?samples[f%tags.Length*2+ch]:0;
 public void Read(double start,float[] output,int frames) {
  lock(gate) for(int i=0;i<frames;i++) {
   double p=start+i; long f=(long)Math.Floor(p); float frac=(float)(p-f);
   for(int ch=0;ch<2;ch++) { float a=At(f,ch),b=At(f+1,ch); output[i*2+ch]=a+(b-a)*frac; }
  }
 }
}
