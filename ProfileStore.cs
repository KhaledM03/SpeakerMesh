using System.Text.Json;
namespace SpeakerMesh;
public static class ProfileStore {
 public static Profile Load(string path) {
  var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid setup.");
  Validate(profile); return profile;
 }
 public static void Validate(Profile p) {
  if (p.Version != 1 || p.SourceMode is < 0 or > 2 || p.Outputs == null || p.Outputs.Count > 32 ||
      p.Outputs.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id) || !double.IsFinite(x.Gain) || x.Gain < 0 || x.Gain > 1 ||
        !double.IsFinite(x.DelayMs) || x.DelayMs < 0 || x.DelayMs > 2000 || !Enum.IsDefined(x.Channel)) ||
      p.Outputs.Select(x=>x.Id).Distinct().Count() != p.Outputs.Count)
   throw new InvalidDataException("Unsupported or invalid speaker setup.");
 }
 public static void Save(string path, Profile profile) { Validate(profile); AtomicJson.Write(path, profile); }
}
public static class AtomicJson {
 public static void Write<T>(string path, T value) {
  path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try {
   using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
    JsonSerializer.Serialize(stream, value); stream.Flush(true);
   }
   File.Move(temporary, path, true);
  } finally { if(File.Exists(temporary)) File.Delete(temporary); }
 }
}
