using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace SpeakerMesh;

// Default switching uses the widely used private Windows audio policy interface.
// Failures are surfaced and can be completed in Windows Sound settings instead.
internal static class WindowsOutput {
 public static string DefaultForRole(int role) {
  var enumerator=NativeAudio.Enumerator();IMMDevice? device=null;
  try {NativeAudio.Check(enumerator.GetDefaultAudioEndpoint(0,role,out device));NativeAudio.Check(device.GetId(out string id));return id;}
  finally {NativeAudio.Release(device);NativeAudio.Release(enumerator);}
 }
 public static void SetDefault(string id,int role) {
  var policy=(IPolicyConfig)new PolicyConfigClient();
  try {NativeAudio.Check(policy.SetDefaultEndpoint(id,role));if(DefaultForRole(role)!=id) throw new InvalidOperationException("Windows did not retain the requested output.");}
  finally {NativeAudio.Release(policy);}
 }
 public static bool RenameDedicated(Endpoint endpoint) {
  if(!DevicePolicy.IsDedicated(endpoint.Name)) throw new ArgumentException("Only a dedicated cable input can be renamed by this setup.");
  var enumerator=NativeAudio.Enumerator();IMMDevice? device=null;IPropertyStore? store=null;
  var value=new PropVariant {Type=31,Pointer=Marshal.StringToCoTaskMemUni("SpeakerMesh")};
  try {
   NativeAudio.Check(enumerator.GetDevice(endpoint.Id,out device));NativeAudio.Check(device.OpenPropertyStore(2,out store));
   var key=new PropertyKey {Format=new("a45c254e-df1c-4efd-8020-67d146a850e0"),Id=14};
   NativeAudio.Check(store.SetValue(ref key,ref value));NativeAudio.Check(store.Commit());
   return NativeAudio.Devices().Any(x=>x.Id==endpoint.Id && x.Name=="SpeakerMesh");
  } finally {NativeAudio.PropVariantClear(ref value);NativeAudio.Release(store);NativeAudio.Release(device);NativeAudio.Release(enumerator);}
 }
}
internal sealed record DefaultSnapshot(string InputId,Dictionary<int,string> Previous);
internal interface IDefaultRouting {
 string Read(int role);void Write(string id,int role);HashSet<string> Active();string? Fallback();
}
internal sealed class NativeDefaultRouting:IDefaultRouting {
 public string Read(int role)=>WindowsOutput.DefaultForRole(role);
 public void Write(string id,int role)=>WindowsOutput.SetDefault(id,role);
 public HashSet<string> Active()=>NativeAudio.Devices().Select(x=>x.Id).ToHashSet();
 public string? Fallback()=>NativeAudio.Devices().FirstOrDefault(x=>!DevicePolicy.IsVirtual(x.Name))?.Id;
}
internal sealed class OutputLease {
 readonly IDefaultRouting routing;readonly string statePath;
 public OutputLease(IDefaultRouting? routing=null,string? statePath=null) {this.routing=routing??new NativeDefaultRouting();this.statePath=statePath??StatePath;}
 static string StatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SpeakerMesh","default-restore.json");
 DefaultSnapshot? snapshot;
 public bool Owned=>snapshot!=null;
 public string? InputId=>snapshot?.InputId;
 public void Acquire(string inputId) {
  if(snapshot!=null && snapshot.InputId!=inputId) Restore();
  if(snapshot==null) {
   var previous=new Dictionary<int,string>();for(int role=0;role<2;role++) previous[role]=routing.Read(role);
   // Recover a prior snapshot if a previous app instance closed unexpectedly.
   if(previous.Values.All(x=>x==inputId) && File.Exists(statePath)) {
    var old=JsonSerializer.Deserialize<DefaultSnapshot>(File.ReadAllText(statePath));if(old?.InputId==inputId) snapshot=old;
   }
   snapshot??=new(inputId,previous);
   Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);File.WriteAllText(statePath,JsonSerializer.Serialize(snapshot));
  }
  try {for(int role=0;role<2;role++) routing.Write(inputId,role);}
  catch {Restore();throw;}
 }
 public void Maintain() {if(snapshot!=null) for(int role=0;role<2;role++) if(routing.Read(role)!=snapshot.InputId) routing.Write(snapshot.InputId,role);}
 public void Restore() {
  if(snapshot==null) return;
  var active=routing.Active();
  foreach(var role in snapshot.Previous) if(routing.Read(role.Key)==snapshot.InputId) {
   string? target=active.Contains(role.Value) && role.Value!=snapshot.InputId?role.Value:routing.Fallback();
   if(target!=null && target!=snapshot.InputId) routing.Write(target,role.Key);
  }
  snapshot=null;
 }
}
[ComImport,Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] internal class PolicyConfigClient {}
[ComImport,Guid("f8679f50-850a-41cf-9c72-430f290290c8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig {
 [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id,out IntPtr format);
 [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id,int defaultFormat,out IntPtr format);
 [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
 [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr endpoint,IntPtr mix);
 [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id,int defaultPeriod,IntPtr period,IntPtr minimum);
 [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr period);
 [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr mode);
 [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr mode);
 [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr key,IntPtr value);
 [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,IntPtr key,IntPtr value);
 [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id,int role);
 [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id,int visible);
}


