using SpeakerMesh;
using System.Text.Json;

int passed=0,failed=0;
void Test(string name,Action check) {
 try {check();passed++;Console.WriteLine("PASS "+name);}
 catch(Exception ex){failed++;Console.Error.WriteLine("FAIL "+name+": "+ex);}
}
void Equal(double actual,double expected,double tolerance=1e-5) {if(Math.Abs(actual-expected)>tolerance || !double.IsFinite(actual))throw new Exception($"Expected {expected}, got {actual}");}
void True(bool condition) {if(!condition)throw new Exception("Assertion failed");}
void Throws<T>(Action action) where T:Exception {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
string temporary=Path.Combine(Path.GetTempPath(),"speakermesh-tests-"+Guid.NewGuid());Directory.CreateDirectory(temporary);
try {
 Test("gain rejects nonfinite and out-of-range values",()=>{var g=new LiveGain();foreach(double v in new[]{double.NaN,double.PositiveInfinity,-.1,1.1})Throws<ArgumentOutOfRangeException>(()=>g.Set(v,false));});
 Test("mute preserves relative gain",()=>{var g=new LiveGain();g.Set(.3,true);Equal(g.Effective,0);g.Set(g.State.Gain,false);Equal(g.Effective,.3);});
 Test("live gain snapshots tolerate concurrent writers",()=>{var g=new LiveGain();Parallel.For(0,10000,i=>{g.Set((i%101)/100.0,i%2==0);var snapshot=g.State;True(snapshot.Gain>=0&&snapshot.Gain<=1);});});
 Test("gain ramp reaches target without a discontinuity",()=>{var ramp=new GainRamp();var data=Enumerable.Repeat(1f,960).ToArray();ramp.Apply(data,480,48000,.5,ChannelMode.Stereo);Equal(data[^1],.5);for(int i=2;i<data.Length;i+=2)True(Math.Abs(data[i]-data[i-2])<=1.0/480+.00001);});
 Test("mute ramps to silence and unmute restores gain",()=>{var r=new GainRamp();var d=Enumerable.Repeat(1f,960).ToArray();r.Apply(d,480,48000,.4,ChannelMode.Stereo);Array.Fill(d,1);r.Apply(d,480,48000,0,ChannelMode.Stereo);Equal(d[^1],0);Array.Fill(d,1);r.Apply(d,480,48000,.4,ChannelMode.Stereo);Equal(d[^1],.4);});
 Test("speaker balance survives master changes",()=>{var a=new LiveGain();var b=new LiveGain();var master=new LiveGain();a.Set(.2,false);b.Set(.8,false);master.Set(.5,false);Equal(a.Effective*master.Effective,.1);Equal(b.Effective*master.Effective,.4);});
 Test("channel selection uses correct source",()=>{var d=new float[]{.2f,.8f};Mix.Apply(d,1,.5,ChannelMode.Right);Equal(d[0],.4);Equal(d[1],.4);});
 Test("one output failure preserves healthy outputs",()=>{var h=new OutputHealth(new[]{"a","b"});h.Fail("a","disconnect");Equal(h.Remaining,1);True(h.Failures.ContainsKey("a"));});
 Test("duplicate failures do not exhaust healthy outputs",()=>{var h=new OutputHealth(new[]{"a","b"});h.Fail("a","one");h.Fail("a","two");Equal(h.Remaining,1);h.Fail("b","three");Equal(h.Remaining,0);});
 Test("unknown output failure rejected",()=>Throws<ArgumentException>(()=>new OutputHealth(new[]{"a"}).Fail("b","bad")));
 Test("absolute ring tags discard wrapped samples",()=>{var source=new LiveSource(8,1);source.Write(0,new float[]{1,1});source.Write(8,new float[]{.5f,.5f});var d=new float[2];source.Read(0,d,1);Equal(d[0],0);source.Read(8,d,1);Equal(d[0],.5);});
 Test("ring interpolation and negative pre-roll",()=>{var source=new LiveSource(8,1);source.Write(0,new float[]{0,0,1,1});var d=new float[2];source.Read(.5,d,1);Equal(d[0],.5);source.Read(-2,d,1);Equal(d[0],0);});
 Test("capture timeline keeps contiguous device frames",()=>{var t=new CaptureTimeline();Equal(t.Map(100,1,48000),48000);Equal(t.Map(580,99,48000),48480);Equal(t.Map(0,2,48000,true),96000);});
 Test("drift correction remains bounded",()=>{var c=new PlaybackCursor();c.Prepare(0,48000,true);c.Prepare(480000,48000,true);Equal(c.Step,1.001);c.Prepare(-480000,48000,true);Equal(c.Step,.999);});
 Test("file playback does not seek on timer jitter",()=>{var c=new PlaybackCursor();c.Prepare(0,48000,false);c.Advance(480);c.Prepare(9000,48000,false);Equal(c.Position,480);Equal(c.Step,1);});
 Test("PCM rejects truncated header",()=>Throws<InvalidDataException>(()=>PcmFormat.Parse(new byte[2])));
 Test("PCM mono duplicates samples and sanitizes NaN",()=>{var f=new PcmFormat(48000,1,32,4,true);var d=f.Decode(BitConverter.GetBytes(float.NaN).Concat(BitConverter.GetBytes(.25f)).ToArray());Equal(d[0],0);Equal(d[2],.25);Equal(d[3],.25);});
 Test("synthetic acoustic detector recovers known delay",()=>{const int rate=12000;var d=new float[rate*4];for(int i=0;i<1080;i++)d[rate+2400+i]=(float)ProbeSource.Value(i/(double)rate,0);var match=AcousticAnalysis.Find(new(d,rate,0),1,0);Equal(match.LagMs,200,.2);True(match.Score>.9);});
 Test("silence is rejected by acoustic acceptance",()=>{var m=AcousticAnalysis.Find(new(new float[48000],12000,0),1,0);Throws<InvalidOperationException>(()=>AcousticAnalysis.Measure("a","speaker",new[]{m,m,m}));});
 Test("unstable acoustic arrivals rejected",()=>Throws<InvalidOperationException>(()=>AcousticAnalysis.Measure("a","speaker",new[]{new ProbeMatch(10,.9,2),new ProbeMatch(30,.9,2),new ProbeMatch(50,.9,2)})));
 Test("relative corrections cancel common microphone delay",()=>{var a=new[]{new SpeakerMeasurement("a","A",100,.9,1),new SpeakerMeasurement("b","B",250,.9,1)};var first=AcousticAnalysis.Corrections(a);var second=AcousticAnalysis.Corrections(a.Select(x=>x with{LagMs=x.LagMs+80}).ToArray());Equal(first["a"],150);Equal(second["a"],150);Equal(first["b"],0);});
 Test("verification refuses excessive residual",()=>Throws<InvalidOperationException>(()=>AcousticAnalysis.Verify(new[]{new SpeakerMeasurement("a","A",0,.9,0),new SpeakerMeasurement("b","B",11,.9,0)})));
 Test("profile round trip includes mute and driver mode",()=>{string path=Path.Combine(temporary,"profile.json");var p=new Profile("virtual","",2,new(){new("a","A",10,.3,Muted:true)},MirrorInputVolume:true);ProfileStore.Save(path,p);var loaded=ProfileStore.Load(path);True(loaded.Outputs[0].Muted&&loaded.MirrorInputVolume);Equal(loaded.Outputs[0].Gain,.3);});
 Test("legacy profile defaults remain loadable",()=>{string path=Path.Combine(temporary,"old.json");File.WriteAllText(path,"{\"SourceId\":\"v\",\"FilePath\":\"\",\"SourceMode\":2,\"Outputs\":[{\"Id\":\"a\",\"Name\":\"A\",\"Gain\":0.3,\"DelayMs\":0,\"Channel\":0}]}");var p=ProfileStore.Load(path);True(!p.Outputs[0].Muted&&!p.MirrorInputVolume);});
 Test("invalid profile cannot replace a saved profile",()=>{string path=Path.Combine(temporary,"safe.json");var p=new Profile("v","",2,new(){new("a","A")});ProfileStore.Save(path,p);Throws<InvalidDataException>(()=>ProfileStore.Save(path,p with{Version=99}));True(ProfileStore.Load(path).Version==1);True(!Directory.GetFiles(temporary,"*.tmp").Any());});
 Test("duplicate profile endpoints rejected",()=>Throws<InvalidDataException>(()=>ProfileStore.Validate(new("v","",2,new(){new("a","A"),new("a","Again")}))));
 Test("route restored after normal stop",()=>{var r=new FakeRouting();var lease=new OutputLease(r,Path.Combine(temporary,"route.json"));lease.Acquire("virtual");True(lease.IsCurrent);lease.Restore();True(r.Read(0)=="physical"&&r.Read(1)=="physical");});
 Test("explicit user routing change preserved",()=>{var r=new FakeRouting();var lease=new OutputLease(r,Path.Combine(temporary,"user-route.json"));lease.Acquire("virtual");r.Write("headphones",0);True(!lease.IsCurrent);lease.Restore();True(r.Read(0)=="headphones"&&r.Read(1)=="physical");});
 Test("crash recovery uses persisted route ownership",()=>{var r=new FakeRouting();string path=Path.Combine(temporary,"crash.json");new OutputLease(r,path).Acquire("virtual");new OutputLease(r,path).Recover();True(r.Read(0)=="physical");True(!File.Exists(path));});
 Test("missing old output falls back to physical device",()=>{var r=new FakeRouting();var lease=new OutputLease(r,Path.Combine(temporary,"fallback.json"));lease.Acquire("virtual");r.Available.Remove("physical");lease.Restore();True(r.Read(0)=="headphones");});
 Test("no fallback keeps recovery journal for retry",()=>{var r=new FakeRouting();string path=Path.Combine(temporary,"retry.json");var lease=new OutputLease(r,path);lease.Acquire("virtual");r.Available.Clear();r.Available.Add("virtual");Throws<InvalidOperationException>(lease.Restore);True(File.Exists(path)&&lease.Owned);r.Available.Add("physical");lease.Restore();True(!File.Exists(path));});
 Test("corrupt recovery journal cannot change system route",()=>{var r=new FakeRouting();string path=Path.Combine(temporary,"corrupt.json");File.WriteAllText(path,"{\"InputId\":\"physical\",\"Previous\":{\"17\":\"virtual\"}}");Throws<InvalidDataException>(()=>new OutputLease(r,path).Recover());True(r.Read(0)=="physical");});
 Test("partial default switch is rolled back",()=>{var r=new FakeRouting{FailSecond=true};var lease=new OutputLease(r,Path.Combine(temporary,"partial.json"));Throws<InvalidOperationException>(()=>lease.Acquire("virtual"));True(r.Read(0)=="physical"&&r.Read(1)=="physical");});
} finally {Directory.Delete(temporary,true);}
Console.WriteLine($"{passed} passed, {failed} failed; native Windows/hardware tests not run.");
return failed==0?0:1;

sealed class FakeRouting:IDefaultRouting {
 readonly Dictionary<int,string> current=new(){{0,"physical"},{1,"physical"}};
 public HashSet<string> Available {get;}=new(){"physical","headphones","virtual"};
 public bool FailSecond {get;init;}
 public string Read(int role)=>current[role];
 public void Write(string id,int role){if(FailSecond&&role==1&&id=="virtual")throw new InvalidOperationException("Simulated routing failure");current[role]=id;}
 public HashSet<string> Active()=>Available;
 public string? Fallback()=>Available.FirstOrDefault(x=>x!="virtual");
}
