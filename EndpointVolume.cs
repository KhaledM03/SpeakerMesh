using System.Runtime.InteropServices;
namespace SpeakerMesh;

// Bind to the virtual SOURCE, never overwrite physical speaker endpoint volumes.
// Read on the UI timer so changes from keyboard knobs and Windows settings appear
// without a global keyboard hook or touching another app's media-key handling.
internal sealed class EndpointVolume : IDisposable {
 IAudioEndpointVolume? volume;
 readonly Guid context = Guid.NewGuid();
 public EndpointVolume(string id) {
  var enumerator=NativeAudio.Enumerator(); IMMDevice? device=null;
  try {
   NativeAudio.Check(enumerator.GetDevice(id,out device));
   Guid iid=typeof(IAudioEndpointVolume).GUID;
   NativeAudio.Check(device.Activate(ref iid,23,IntPtr.Zero,out var instance));
   volume=(IAudioEndpointVolume)instance;
  } finally { NativeAudio.Release(device); NativeAudio.Release(enumerator); }
 }
 public GainState Read() {
  ObjectDisposedException.ThrowIf(volume==null,this);
  NativeAudio.Check(volume.GetMasterVolumeLevelScalar(out float gain));
  NativeAudio.Check(volume.GetMute(out bool muted)); return new(gain,muted);
 }
 public void SetGain(double gain) {
  ObjectDisposedException.ThrowIf(volume==null,this);
  var token=context; NativeAudio.Check(volume.SetMasterVolumeLevelScalar((float)Math.Clamp(gain,0,1),ref token));
 }
 public void SetMute(bool muted) {
  ObjectDisposedException.ThrowIf(volume==null,this);
  var token=context; NativeAudio.Check(volume.SetMute(muted,ref token));
 }
 public void Dispose() { NativeAudio.Release(volume); volume=null; }
}
[ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume {
 [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
 [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
 [PreserveSig] int GetChannelCount(out uint count);
 [PreserveSig] int SetMasterVolumeLevel(float level,ref Guid context);
 [PreserveSig] int SetMasterVolumeLevelScalar(float level,ref Guid context);
 [PreserveSig] int GetMasterVolumeLevel(out float level);
 [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
 [PreserveSig] int SetChannelVolumeLevel(uint channel,float level,ref Guid context);
 [PreserveSig] int SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context);
 [PreserveSig] int GetChannelVolumeLevel(uint channel,out float level);
 [PreserveSig] int GetChannelVolumeLevelScalar(uint channel,out float level);
 [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context);
 [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
