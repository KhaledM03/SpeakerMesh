using System.Runtime.InteropServices;
namespace SpeakerMesh;
public record Endpoint(string Id,string Name) { public override string ToString()=>Name; }
internal static class NativeAudio {
 internal static readonly Guid ClientId=new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"),RenderId=new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"),CaptureId=new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
 [DllImport("ole32.dll")] internal static extern int PropVariantClear(ref PropVariant value);
 internal static void Check(int hr) { if(hr<0) Marshal.ThrowExceptionForHR(hr); }
 internal static void Release(object? value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
 internal static IMMDeviceEnumerator Enumerator()=>(IMMDeviceEnumerator)new DeviceEnumerator();
 public static string? DefaultOutputId() {
  var enumerator=Enumerator();IMMDevice? device=null;
  try { Check(enumerator.GetDefaultAudioEndpoint(0,0,out device));Check(device.GetId(out string id));return id; }
  finally { Release(device);Release(enumerator); }
 }
 public static List<Endpoint> Devices(int flow=0) {
  var enumerator=Enumerator(); IMMDeviceCollection? devices=null; var result=new List<Endpoint>();
  try {
   Check(enumerator.EnumAudioEndpoints(flow,1,out devices)); Check(devices.GetCount(out uint count));
   for(uint i=0;i<count;i++) {
    Check(devices.Item(i,out var device)); IPropertyStore? store=null;
    try {
     Check(device.GetId(out string id)); Check(device.OpenPropertyStore(0,out store));
     var key=new PropertyKey { Format=new("a45c254e-df1c-4efd-8020-67d146a850e0"),Id=14 };
     Check(store.GetValue(ref key,out var name));
     try { result.Add(new(id,name.Type==31?Marshal.PtrToStringUni(name.Pointer)??id:id)); }
     finally { PropVariantClear(ref name); }
    } finally { Release(store); Release(device); }
   }
   return result;
  } finally { Release(devices); Release(enumerator); }
 }
 internal static IAudioClient Open(string id) {
  var enumerator=Enumerator(); IMMDevice? device=null;
  try {
   Check(enumerator.GetDevice(id,out device)); Guid iid=ClientId;
   Check(device.Activate(ref iid,23,IntPtr.Zero,out var client)); return (IAudioClient)client;
  } finally { Release(device); Release(enumerator); }
 }
 internal static IntPtr FloatFormat(int rate) {
  var b=new byte[18]; BitConverter.GetBytes((ushort)3).CopyTo(b,0); BitConverter.GetBytes((ushort)2).CopyTo(b,2);
  BitConverter.GetBytes(rate).CopyTo(b,4); BitConverter.GetBytes(rate*8).CopyTo(b,8);
  BitConverter.GetBytes((ushort)8).CopyTo(b,12); BitConverter.GetBytes((ushort)32).CopyTo(b,14);
  IntPtr ptr=Marshal.AllocCoTaskMem(18); Marshal.Copy(b,0,ptr,18); return ptr;
 }
 internal static PcmFormat ReadFormat(IntPtr ptr) {
  int size=18+(ushort)Marshal.ReadInt16(ptr,16); if(size>4096) throw new InvalidDataException("Invalid device format.");
  var bytes=new byte[size]; Marshal.Copy(ptr,bytes,0,size); return PcmFormat.Parse(bytes);
 }
}
[ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class DeviceEnumerator { }
[ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator {
 [PreserveSig] int EnumAudioEndpoints(int flow,uint state,out IMMDeviceCollection devices);
 [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
 [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id,out IMMDevice device);
 [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
 [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
}
[ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection {
 [PreserveSig] int GetCount(out uint count);
 [PreserveSig] int Item(uint index,out IMMDevice device);
}
[ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice {
 [PreserveSig] int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object instance);
 [PreserveSig] int OpenPropertyStore(uint mode,out IPropertyStore store);
 [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
 [PreserveSig] int GetState(out uint state);
}
[StructLayout(LayoutKind.Sequential)] internal struct PropertyKey { public Guid Format; public uint Id; }
[StructLayout(LayoutKind.Explicit,Size=24)] internal struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
[ComImport,Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore {
 [PreserveSig] int GetCount(out uint count); [PreserveSig] int GetAt(uint index,out PropertyKey key);
 [PreserveSig] int GetValue(ref PropertyKey key,out PropVariant value);
 [PreserveSig] int SetValue(ref PropertyKey key,ref PropVariant value); [PreserveSig] int Commit();
}
[ComImport,Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient {
 [PreserveSig] int Initialize(int mode,uint flags,long duration,long periodicity,IntPtr format,IntPtr session);
 [PreserveSig] int GetBufferSize(out uint size); [PreserveSig] int GetStreamLatency(out long latency);
 [PreserveSig] int GetCurrentPadding(out uint padding);
 [PreserveSig] int IsFormatSupported(int mode,IntPtr format,out IntPtr closest);
 [PreserveSig] int GetMixFormat(out IntPtr format);
 [PreserveSig] int GetDevicePeriod(out long normal,out long minimum);
 [PreserveSig] int Start(); [PreserveSig] int Stop(); [PreserveSig] int Reset();
 [PreserveSig] int SetEventHandle(IntPtr handle);
 [PreserveSig] int GetService(ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)] out object service);
}
[ComImport,Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioRenderClient {
 [PreserveSig] int GetBuffer(uint frames,out IntPtr data); [PreserveSig] int ReleaseBuffer(uint frames,uint flags);
}
[ComImport,Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient {
 [PreserveSig] int GetBuffer(out IntPtr data,out uint frames,out uint flags,out ulong devicePosition,out ulong qpcPosition);
 [PreserveSig] int ReleaseBuffer(uint frames); [PreserveSig] int GetNextPacketSize(out uint frames);
}



