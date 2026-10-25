using System.Numerics;
namespace SpeakerMesh;

public sealed class ProbeSource(int speakerIndex,int count,int rate=48000):IAudioSource {
 public int Rate=>rate;
 public double Duration=>2+count*4;
 public static double At(int speaker,int repetition)=>2+speaker*4+repetition*.45;
 public static double Value(double time,int repetition) {
  const double duration=.09;if(time<0||time>=duration)return 0;
  double f0=repetition switch{0=>650,1=>4400,_=>1100},f1=repetition switch{0=>3900,1=>900,_=>4700};
  double phase=2*Math.PI*(f0*time+(f1-f0)*time*time/(2*duration))+repetition*.7;
  return .25*Math.Sin(phase)*Math.Pow(Math.Sin(Math.PI*time/duration),2);
 }
 public static float[] Template(int repetition,int rate=12000)=>Enumerable.Range(0,(int)(.09*rate)).Select(i=>(float)Value(i/(double)rate,repetition)).ToArray();
 public void Read(double start,float[] output,int frames) {
  for(int i=0;i<frames;i++) {
   double time=(start+i)/Rate,v=0;
   // Quiet pre-roll keeps the active render transports awake before measurement.
   if(time>=.45 && time<.52)v=.015*Math.Sin(2*Math.PI*1200*time)*Math.Pow(Math.Sin(Math.PI*(time-.45)/.07),2);
   for(int r=0;r<3;r++)v+=Value(time-At(speakerIndex,r),r);
   output[i*2]=output[i*2+1]=(float)v;
  }
 }
}
public record AcousticRecording(float[] Samples,int Rate,double StartSeconds);
public record ProbeMatch(double LagMs,double Score,double Dominance);
public record SpeakerMeasurement(string Id,string Name,double LagMs,double MatchScore,double JitterMs);
public record CalibrationResult(Dictionary<string,double> Delays,List<SpeakerMeasurement> Measured,double ResidualMs,DateTime MeasuredAt);

public static class AcousticAnalysis {
 public const int AnalysisRate=12000;
 public static ProbeMatch Find(AcousticRecording recording,double expectedSeconds,int repetition) {
  double relative=expectedSeconds-recording.StartSeconds;
  double begin=relative-.08;int length=(int)(2.16*AnalysisRate);
  var data=new float[length];
  for(int i=0;i<length;i++) {
   double p=(begin+i/(double)AnalysisRate)*recording.Rate;long a=(long)Math.Floor(p);double f=p-a;
   if(a>=0 && a+1<recording.Samples.Length)data[i]=(float)(recording.Samples[a]*(1-f)+recording.Samples[a+1]*f);
  }
  var kernel=ProbeSource.Template(repetition);double mean=kernel.Average(x=>(double)x);for(int i=0;i<kernel.Length;i++)kernel[i]-=(float)mean;
  double energy=kernel.Sum(x=>(double)x*x);
  var correlation=Convolve(data,kernel.Reverse().ToArray());
  var power=new double[length+1];var sums=new double[length+1];for(int i=0;i<length;i++){power[i+1]=power[i]+data[i]*data[i];sums[i+1]=sums[i]+data[i];}
  int candidates=length-kernel.Length+1,best=0;var scores=new double[candidates];double peak=0;
  for(int i=0;i<candidates;i++) {
   double sum=sums[i+kernel.Length]-sums[i];double localEnergy=power[i+kernel.Length]-power[i]-sum*sum/kernel.Length;
   double score=localEnergy>1e-12?Math.Abs(correlation[i+kernel.Length-1])/Math.Sqrt(energy*localEnergy):0;
   scores[i]=Math.Min(1,score);if(scores[i]>peak){peak=scores[i];best=i;}
  }
  double other=0;for(int i=0;i<candidates;i++)if(Math.Abs(i-best)>AnalysisRate*.025)other=Math.Max(other,scores[i]);
  double fractional=0;
  if(best>0 && best+1<candidates){double denominator=scores[best-1]-2*peak+scores[best+1];if(Math.Abs(denominator)>1e-10)fractional=Math.Clamp(.5*(scores[best-1]-scores[best+1])/denominator,-.5,.5);}
  return new((begin+(best+fractional)/AnalysisRate-relative)*1000,peak,other>0?peak/other:100);
 }
 public static SpeakerMeasurement Measure(string id,string name,IReadOnlyList<ProbeMatch> matches) {
  var usable=matches.Where(x=>double.IsFinite(x.LagMs)&&x.Score>=.30&&x.Dominance>=1.15&&x.LagMs>=-60&&x.LagMs<=1950).OrderBy(x=>x.LagMs).ToList();
  var cluster=usable.Select(p=>usable.Where(x=>x.LagMs>=p.LagMs&&x.LagMs<=p.LagMs+5).ToList()).OrderByDescending(x=>x.Count).ThenBy(x=>x.Count>0?x.Max(y=>y.LagMs)-x.Min(y=>y.LagMs):double.MaxValue).FirstOrDefault();
  if(cluster==null||cluster.Count<2)throw new InvalidOperationException(name+": test tones were unclear or timing was unstable. Keep the room quiet, move the microphone closer, and retry. Previous delays are unchanged.");
  var values=cluster.Select(x=>x.LagMs).OrderBy(x=>x).ToArray();double median=values.Length%2==1?values[values.Length/2]:(values[values.Length/2-1]+values[values.Length/2])/2;
  return new(id,name,median,cluster.Min(x=>x.Score),values.Max()-values.Min());
 }
 public static Dictionary<string,double> Corrections(IReadOnlyList<SpeakerMeasurement> measured) {
  if(measured.Count<2 || measured.Any(x=>!double.IsFinite(x.LagMs)))throw new InvalidOperationException("At least two valid speaker measurements are needed.");
  double slowest=measured.Max(x=>x.LagMs);var result=measured.ToDictionary(x=>x.Id,x=>Math.Round(slowest-x.LagMs,1));
  if(result.Values.Any(x=>x<0||x>2000))throw new InvalidOperationException("Required adjustment exceeds the supported delay range.");return result;
 }
 public static double Verify(IReadOnlyList<SpeakerMeasurement> measured) {
  if(measured.Count<2)throw new InvalidOperationException("Verification needs every selected speaker.");
  double residual=measured.Max(x=>x.LagMs)-measured.Min(x=>x.LagMs);
  if(!double.IsFinite(residual)||residual>10)throw new InvalidOperationException($"The check pass still found {residual:0.0} ms between speakers. Bluetooth timing may have changed. Previous delays are unchanged; retry or keep manual timing.");
  return residual;
 }
 static double[] Convolve(float[] a,float[] b) {
  int size=1;while(size<a.Length+b.Length-1)size<<=1;var x=new Complex[size];var y=new Complex[size];
  for(int i=0;i<a.Length;i++)x[i]=a[i];for(int i=0;i<b.Length;i++)y[i]=b[i];Fft(x,false);Fft(y,false);
  for(int i=0;i<size;i++)x[i]*=y[i];Fft(x,true);return x.Select(z=>z.Real).ToArray();
 }
 static void Fft(Complex[] data,bool inverse) {
  int n=data.Length;
  for(int i=1,j=0;i<n;i++){int bit=n>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j)(data[i],data[j])=(data[j],data[i]);}
  for(int len=2;len<=n;len<<=1){var step=Complex.FromPolarCoordinates(1,(inverse?2:-2)*Math.PI/len);for(int i=0;i<n;i+=len){Complex w=1;for(int j=0;j<len/2;j++){var u=data[i+j];var v=data[i+j+len/2]*w;data[i+j]=u+v;data[i+j+len/2]=u-v;w*=step;}}}
  if(inverse)for(int i=0;i<n;i++)data[i]/=n;
 }
}


