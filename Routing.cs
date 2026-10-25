using System.Text.Json;
namespace SpeakerMesh;
public sealed record DefaultSnapshot(string InputId,Dictionary<int,string> Previous);
public interface IDefaultRouting {
 string Read(int role);void Write(string id,int role);HashSet<string> Active();string? Fallback();
}
public sealed class OutputLease {
 readonly IDefaultRouting routing;readonly string statePath;
 public OutputLease(IDefaultRouting routing,string? statePath=null) {this.routing=routing??throw new ArgumentNullException(nameof(routing));this.statePath=statePath??StatePath;}
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
   AtomicJson.Write(statePath,snapshot);
  }
  try {for(int role=0;role<2;role++) routing.Write(inputId,role);}
  catch {Restore();throw;}
 }
 public bool IsCurrent => snapshot!=null && snapshot.Previous.Keys.All(role=>routing.Read(role)==snapshot.InputId);
 public void Recover() {
  if(snapshot!=null || !File.Exists(statePath)) return;
  var saved=JsonSerializer.Deserialize<DefaultSnapshot>(File.ReadAllText(statePath));
  if(saved==null || string.IsNullOrWhiteSpace(saved.InputId) || saved.Previous==null || saved.Previous.Count!=2 || !saved.Previous.ContainsKey(0) || !saved.Previous.ContainsKey(1) || saved.Previous.Values.Any(string.IsNullOrWhiteSpace))
   throw new InvalidDataException("Invalid saved Windows route; choose your output in Windows Sound settings.");
  snapshot=saved;
  Restore();
 }
 public void Restore() {
  if(snapshot==null) return;
  var active=routing.Active();
  foreach(var role in snapshot.Previous) if(routing.Read(role.Key)==snapshot.InputId) {
   string? target=active.Contains(role.Value) && role.Value!=snapshot.InputId?role.Value:routing.Fallback();
   if(target==null || target==snapshot.InputId) throw new InvalidOperationException("No physical fallback is available. Connect an output and retry restoring Windows audio.");
   routing.Write(target,role.Key);
  }
  snapshot=null;
  if(File.Exists(statePath)) File.Delete(statePath);
 }
}
