using System.Collections.Concurrent;
namespace SpeakerMesh;

// Immutable snapshots keep UI changes off the render thread's locks.
public sealed record GainState(double Gain, bool Muted);
public sealed class LiveGain {
 GainState state = new(1, false);
 public GainState State => Volatile.Read(ref state);
 public void Set(double gain, bool muted) {
  if (!double.IsFinite(gain) || gain < 0 || gain > 1) throw new ArgumentOutOfRangeException(nameof(gain));
  Volatile.Write(ref state, new(gain, muted));
 }
 public double Effective => State is var s && !s.Muted ? s.Gain : 0;
}

// One instance per render worker. Ramp over 10 ms, including initial startup.
public sealed class GainRamp {
 double current;
 public void Apply(float[] samples, int frames, int rate, double target, ChannelMode channel) {
  double step = 1.0 / Math.Max(1, rate * .01);
  for (int i = 0; i < frames; i++) {
   current += Math.Clamp(target - current, -step, step);
   float l = samples[2*i], r = samples[2*i+1];
   (l,r) = channel switch { ChannelMode.Left => (l,l), ChannelMode.Right => (r,r), ChannelMode.Mono => ((l+r)*.5f,(l+r)*.5f), _ => (l,r) };
   samples[2*i] = (float)Math.Clamp(l * current, -1, 1);
   samples[2*i+1] = (float)Math.Clamp(r * current, -1, 1);
  }
 }
}

public sealed class OutputHealth(IEnumerable<string> ids) {
 readonly ConcurrentDictionary<string,string> failures = new();
 readonly HashSet<string> selected = ids.ToHashSet();
 public IReadOnlyDictionary<string,string> Failures => failures;
 public int Remaining => selected.Count - failures.Count;
 public void Fail(string id, string reason) {
  if (!selected.Contains(id)) throw new ArgumentException("Unknown output", nameof(id));
  failures.TryAdd(id, reason);
 }
}
