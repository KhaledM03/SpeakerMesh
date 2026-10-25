using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace SpeakerMesh;
public static class DevicePolicy {
 public static bool IsVirtual(string name)=>name.Contains("Virtual Audio",StringComparison.OrdinalIgnoreCase)||name.Contains("VB-Audio",StringComparison.OrdinalIgnoreCase)||name.Contains("CABLE Output",StringComparison.OrdinalIgnoreCase)||name.Contains("CABLE Input",StringComparison.OrdinalIgnoreCase)||name.StartsWith("SpeakerMesh",StringComparison.OrdinalIgnoreCase);
 public static bool IsDedicated(string name)=>name.Contains("CABLE Input",StringComparison.OrdinalIgnoreCase)||name.StartsWith("SpeakerMesh",StringComparison.OrdinalIgnoreCase);
 public static List<OutputConfig> Destinations(IEnumerable<OutputConfig> outputs,IEnumerable<Endpoint> known,string? sourceId) {
  var devices=known.ToDictionary(x=>x.Id);
  return outputs.Where(x=>x.Id!=sourceId && devices.TryGetValue(x.Id,out var endpoint) && !IsVirtual(endpoint.Name)).ToList();
 }
}
public sealed class SpeakerView : INotifyPropertyChanged {
 public string Id {get;}
 string name;bool connected,selected;double volume=25,delay;ChannelMode channel;
 public SpeakerView(Endpoint endpoint) {Id=endpoint.Id;name=endpoint.Name;connected=true;}
 public string Name {get=>name;set {if(name==value)return;name=value;Changed();Changed(nameof(Title));}}
 public string Title=>Name.StartsWith("Headphones (")||Name.StartsWith("Speakers (")?Name.Replace("Headphones (","").Replace("Speakers (","").TrimEnd(')'):Name.Split(" (")[0];
 public bool Connected {get=>connected;set{if(connected==value)return;connected=value;Changed();Changed(nameof(ConnectionText));}}
 public string ConnectionText=>Connected?"Connected to Windows":"Disconnected";
 public bool Selected {get=>selected;set{if(selected==value)return;selected=value;Changed();}}
 public double Volume {get=>volume;set{if(volume==value)return;volume=value;Changed();Changed(nameof(VolumeText));}}
 public string VolumeText=>$"{Volume:0}%";
 public double Delay {get=>delay;set{if(delay==value)return;delay=value;Changed();Changed(nameof(DelayText));}}
 public string DelayText=>$"{Delay:0} ms";
 public ChannelMode Channel {get=>channel;set{if(channel==value)return;channel=value;Changed();}}
 public OutputConfig Config()=>new(Id,Name,Delay,Volume/100,Channel);
 public event PropertyChangedEventHandler? PropertyChanged;
 void Changed([CallerMemberName]string? property=null)=>PropertyChanged?.Invoke(this,new(property));
}


