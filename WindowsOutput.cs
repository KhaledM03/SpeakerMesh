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
internal sealed class NativeDefaultRouting:IDefaultRouting {
 public string Read(int role)=>WindowsOutput.DefaultForRole(role);
 public void Write(string id,int role)=>WindowsOutput.SetDefault(id,role);
 public HashSet<string> Active()=>NativeAudio.Devices().Select(x=>x.Id).ToHashSet();
 public string? Fallback()=>NativeAudio.Devices().FirstOrDefault(x=>!DevicePolicy.IsVirtual(x.Name))?.Id;
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


