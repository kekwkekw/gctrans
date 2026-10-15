#nullable enable
using System.Collections.Immutable;

namespace GCMod.Nextgen;

/// <summary>Bounded memory diagnostics. A failing optional sink cannot stop a writer.</summary>
public sealed class SafeDiagnostics(int capacity = 128, Action<string>? sink = null)
{
    readonly object gate = new();
    readonly Queue<string> recent = new();
    readonly Dictionary<string, long> counters = new(StringComparer.Ordinal);
    public long SinkFailures { get; private set; }
    public long Dropped { get; private set; }
    public void Record(string code)
    {
        // Codes only: no raw display strings, paths, native addresses, or tokens.
        if (code.Length is < 1 or > 96 || code.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9') && c != '_' && c != '-')) code = "INVALID_DIAGNOSTIC_CODE";
        lock (gate)
        {
            if (counters.ContainsKey(code)) counters[code]++;
            else if (counters.Count < Math.Clamp(capacity, 1, 1024)) counters.Add(code, 1);
            else Dropped++;
            if (recent.Count >= Math.Clamp(capacity, 1, 1024)) { recent.Dequeue(); Dropped++; }
            recent.Enqueue(code);
        }
        try { sink?.Invoke(code); } catch { lock (gate) SinkFailures++; }
    }
    public ImmutableDictionary<string, long> Counters { get { lock (gate) return counters.ToImmutableDictionary(StringComparer.Ordinal); } }
    public ImmutableArray<string> Recent { get { lock (gate) return recent.ToImmutableArray(); } }
}
