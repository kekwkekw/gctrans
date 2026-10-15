#nullable enable
namespace GCMod.Nextgen;

public sealed record ManagedSettingChange(string Section, string Key, string DisplayValue);
/// Events enqueue managed values only. The active main-thread lifetime drains native work.
public sealed class ManagedSettingsMailbox
{
    readonly object gate = new();
    readonly Queue<ManagedSettingChange> pending = new();
    readonly int capacity;
    bool alive = true;
    public ManagedSettingsMailbox(int capacity = 64) { this.capacity = Math.Clamp(capacity, 1, 256); }
    public bool Enqueue(string section, string key, object? value)
    {
        lock (gate)
        {
            if (!alive) return false;
            if (pending.Count >= capacity) pending.Dequeue();
            pending.Enqueue(new(section, key, DisplayValue(section, key, value)));
            return true;
        }
    }
    public int Drain(bool isMainThread, Action<ManagedSettingChange> apply)
    {
        if (!isMainThread) return 0;
        int count = 0;
        lock (gate)
        {
            while (alive && pending.Count > 0)
            {
                var change = pending.Dequeue();
                // Close and event production synchronize with drain; callbacks cannot outlive Close.
                try { apply(change); } catch { }
                count++;
            }
        }
        return count;
    }
    public void Close() { lock (gate) { alive = false; pending.Clear(); } }
    public static string DisplayValue(string section, string key, object? value)
    {
        var name = (section + "." + key).ToLowerInvariant();
        if (new[] { "token", "secret", "password", "credential", "authorization", "cdn", "url", "uri", "path", "root", "repository" }.Any(name.Contains)) return "[MASKED]";
        // Arbitrary strings may contain an endpoint/token: show only safe typed values.
        return value is bool b ? b.ToString() : value is int i ? i.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value is float f ? f.ToString(System.Globalization.CultureInfo.InvariantCulture) : "[UPDATED]";
    }
}
/// Initial attempt is automatic once; failure permits explicit retry, never frame-driven retries.
public sealed class RuntimeStartupAttemptGate
{
    bool attempted, running;
    public bool TryBegin(bool enabled, bool explicitRetry)
    {
        if (!enabled || running || (attempted && !explicitRetry)) return false;
        attempted = running = true; return true;
    }
    public void Complete() => running = false;
    public bool Running => running;
}

public static class CandidateLocalePolicy
{
    public const string DefaultLocale = "ko";
    public static bool IsSupported(string locale) => locale is "ko" or "ko-ui3-common-poc";
}
