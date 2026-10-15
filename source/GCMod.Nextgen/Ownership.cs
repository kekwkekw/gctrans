#nullable enable
using System.Collections.Immutable;

namespace GCMod.Nextgen;

/// <summary>Physical native identity, independent of managed wrapper names and binding IDs.</summary>
public sealed record PhysicalTargetIdentity(ulong NativeObject, long InstanceId, string NativeClass, long Lifetime);
public sealed record TextTargetState(PhysicalTargetIdentity Identity, string Text, long OwnerGeneration,
    long SourceGeneration, string SourceIdentityJson, string ExactSource, bool Active = true, bool DestroyedOrRebound = false);
public interface IUiTextTarget
{
    TextTargetState ReadState();
    // Implementation must compare the complete expected state at the write boundary.
    bool TryWriteText(TextTargetState expected, string value);
}
/// <summary>This check uses managed callback/lifetime tokens only, before any Unity/native access.</summary>
public interface IUiTargetAccessGate { bool CanAccess { get; } }
public sealed record OwnershipRequest(string Module, string BindingId, string? ActionGroup, string Slot,
    long ComponentGeneration, TextTargetState Expected);
public sealed record OwnershipResult(bool Written, string Reason);
public enum RestoreOutcome { Restored, ExternalChanged, DestroyedOrRebound, RetryableReadFailure, RetryableWriteFailure, NotOwned }
public sealed record TransitionSummary(int Restored, int Released, int ExternalChangesPreserved,
    int RetryableFailures = 0, bool Committed = true, int DestroyedOrRebound = 0);

/// <summary>One lease per physical target and property; restore only our own still-current value.</summary>
public sealed class UiOwnershipService(SafeDiagnostics? diagnostics = null, int maximumLeases = 4096)
{
    sealed record Lease(IUiTextTarget Target, OwnershipRequest Request, string Original, string LastWritten);
    readonly object gate = new();
    readonly Dictionary<(ulong Physical, string Property), Lease> leases = new();
    readonly HashSet<long> blockedEpochs = new();
    readonly SafeDiagnostics log = diagnostics ?? new();
    long currentGeneration;
    public int LeaseCount { get { lock (gate) return leases.Count; } }
    public void OpenEpoch(long generation)
    {
        lock (gate)
        {
            if (generation <= 0 || blockedEpochs.Contains(generation) || currentGeneration > 0 && generation != currentGeneration)
                throw new InvalidOperationException("EPOCH_CANNOT_REOPEN");
            currentGeneration = generation;
        }
    }
    public OwnershipResult TryWrite(IUiTextTarget target, OwnershipRequest request, string translation)
    {
        lock (gate)
        {
            if (request.ComponentGeneration != currentGeneration || blockedEpochs.Contains(request.ComponentGeneration)) return new(false, "STALE_COMPONENT_GENERATION");
            if (target is IUiTargetAccessGate access && !access.CanAccess) return new(false, "NATIVE_ACCESS_GATE_CLOSED");
            TextTargetState now;
            try { now = target.ReadState(); }
            catch { log.Record("TARGET_READ_FAILURE_ISOLATED"); return new(false, "TARGET_READ_UNAVAILABLE"); }
            if (now != request.Expected || !now.Active || now.Identity.NativeObject == 0 || string.IsNullOrEmpty(now.Identity.NativeClass)) return new(false, "CURRENT_TARGET_SOURCE_MISMATCH");
            var key = (now.Identity.NativeObject, "text");
            if (leases.TryGetValue(key, out var lease))
            {
                // A recycled cell may retain the same physical text object under a new owner/model epoch.
                // Release obsolete ownership before considering the new writer; never restore into that new source.
                if (!SameLifetime(lease.Request.Expected, now))
                {
                    leases.Remove(key);
                    return TryWrite(target, request, translation);
                }
                var sameWriter = lease.Request.Module == request.Module && lease.Request.BindingId == request.BindingId;
                var coMatch = request.ActionGroup != null && lease.Request.ActionGroup == request.ActionGroup
                    && lease.Request.Module == request.Module && lease.Request.Slot == request.Slot;
                if (!sameWriter && !coMatch) return new(false, "PHYSICAL_PROPERTY_ALREADY_OWNED");
                if (now.Text != lease.LastWritten)
                { leases.Remove(key); return new(false, "EXTERNAL_CHANGE_YIELDED"); }
                if (now.Text == translation)
                {
                    // Natural rebind refreshes the safe provider closure while preserving the original value.
                    leases[key] = lease with { Target = target, Request = request };
                    return new(false, coMatch ? "EXPLICIT_COMATCH_SINGLE_WRITE" : "OWNED_VALUE_UNCHANGED");
                }
                if (!TryWriteSafely(target, now, translation)) return new(false, "WRITE_BOUNDARY_CHANGED");
                leases[key] = lease with { Target = target, LastWritten = translation, Request = request };
                return new(true, "OWNED_TRANSLATION_UPDATED");
            }
            // A KO equal to our result is not proof that we wrote it.
            if (now.Text != now.ExactSource) return new(false, "SOURCE_NOT_NATIVE_EXACT");
            if (translation == now.Text) return new(false, "UNCHANGED_DISPLAY");
            if (leases.Count >= Math.Clamp(maximumLeases, 1, 65536)) return new(false, "LEASE_BOUND");
            if (!TryWriteSafely(target, now, translation)) return new(false, "WRITE_BOUNDARY_CHANGED");
            leases.Add(key, new(target, request, now.Text, translation));
            log.Record("OWNED_TEXT_WRITE");
            return new(true, "OWNED_TEXT_WRITE");
        }
    }
    static bool SameLifetime(TextTargetState old, TextTargetState current) => old.Identity == current.Identity
        && old.OwnerGeneration == current.OwnerGeneration && old.SourceGeneration == current.SourceGeneration
        && old.SourceIdentityJson == current.SourceIdentityJson && old.ExactSource == current.ExactSource;
    public bool RestoreTarget(PhysicalTargetIdentity identity) => RestoreTargetDetailed(identity) == RestoreOutcome.Restored;
    public RestoreOutcome RestoreTargetDetailed(PhysicalTargetIdentity identity)
    {
        lock (gate)
        {
            if (!leases.TryGetValue((identity.NativeObject, "text"), out var lease) || lease.Request.Expected.Identity != identity) return RestoreOutcome.NotOwned;
            var result = Restore(lease);
            if (!Retryable(result)) leases.Remove((identity.NativeObject, "text"));
            return result;
        }
    }
    static bool Retryable(RestoreOutcome result) => result is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure;
    RestoreOutcome Restore(Lease lease)
    {
        if (lease.Target is IUiTargetAccessGate access && !access.CanAccess) return RestoreOutcome.RetryableReadFailure;
        TextTargetState current;
        try
        {
            current = lease.Target.ReadState();
        }
        catch { log.Record("TARGET_RESTORE_READ_RETRYABLE"); return RestoreOutcome.RetryableReadFailure; }
        if (current.DestroyedOrRebound || !SameLifetime(lease.Request.Expected, current)) return RestoreOutcome.DestroyedOrRebound;
        if (!current.Active) return RestoreOutcome.RetryableReadFailure;
        if (current.Text != lease.LastWritten) return RestoreOutcome.ExternalChanged;
        if (TryWriteSafely(lease.Target, current, lease.Original)) return RestoreOutcome.Restored;
        // Setter failure is not evidence of an external change. Classify a boundary race only by rereading.
        try
        {
            var after = lease.Target.ReadState();
            if (after.DestroyedOrRebound || !SameLifetime(lease.Request.Expected, after)) return RestoreOutcome.DestroyedOrRebound;
            if (!after.Active) return RestoreOutcome.RetryableReadFailure;
            if (after.Text == lease.Original) return RestoreOutcome.Restored;
            if (after.Text != lease.LastWritten) return RestoreOutcome.ExternalChanged;
        }
        catch { return RestoreOutcome.RetryableReadFailure; }
        log.Record("TARGET_RESTORE_WRITE_RETRYABLE"); return RestoreOutcome.RetryableWriteFailure;
    }
    bool TryWriteSafely(IUiTextTarget target, TextTargetState current, string text)
    {
        try { return target.TryWriteText(current, text); }
        catch { log.Record("TARGET_WRITE_FAILURE_ISOLATED"); return false; }
    }
    public TransitionSummary Transition(long oldGeneration, long newGeneration)
    {
        lock (gate)
        {
            if (oldGeneration != currentGeneration || newGeneration <= oldGeneration)
                throw new InvalidOperationException("INVALID_EPOCH_TRANSITION");
            // Block old work before touching leases. Old source metadata survives through conditional restoration.
            blockedEpochs.Add(oldGeneration);
            int restored = 0, released = 0, changed = 0, retryable = 0, destroyed = 0;
            foreach (var row in leases.Where(x => x.Value.Request.ComponentGeneration == oldGeneration).ToArray())
            {
                var outcome = Restore(row.Value);
                if (Retryable(outcome)) { retryable++; continue; }
                if (outcome == RestoreOutcome.Restored) restored++;
                else { changed++; if (outcome == RestoreOutcome.DestroyedOrRebound) destroyed++; }
                leases.Remove(row.Key); released++;
            }
            if (retryable > 0)
            {
                // Keep the old epoch and remaining leases; natural old bind may continue after this callback.
                blockedEpochs.Remove(oldGeneration);
                return new(restored, released, changed, retryable, false, destroyed);
            }
            currentGeneration = newGeneration;
            blockedEpochs.Remove(newGeneration);
            // No pending native pointers are kept. Bound memory of historical epochs, while generation equality remains authoritative.
            if (blockedEpochs.Count > 64) foreach (var epoch in blockedEpochs.OrderBy(x => x).Take(blockedEpochs.Count - 64).ToArray()) blockedEpochs.Remove(epoch);
            log.Record("UI_EPOCH_TRANSITION");
            return new(restored, released, changed, 0, true, destroyed);
        }
    }
    public TransitionSummary RestoreAllCurrent() => RestoreBindings(_ => true);
    /// <summary>Restore selected current leases without changing the UI epoch or touching unrelated targets.</summary>
    public TransitionSummary RestoreBindings(Func<OwnershipRequest, bool> shouldRestore)
    {
        lock (gate)
        {
            blockedEpochs.Add(currentGeneration);
            int restored = 0, released = 0, changed = 0, retryable = 0, destroyed = 0;
            foreach (var row in leases.Where(x => shouldRestore(x.Value.Request)).ToArray())
            {
                var outcome = Restore(row.Value);
                if (Retryable(outcome)) { retryable++; continue; }
                if (outcome == RestoreOutcome.Restored) restored++;
                else { changed++; if (outcome == RestoreOutcome.DestroyedOrRebound) destroyed++; }
                leases.Remove(row.Key); released++;
            }
            blockedEpochs.Remove(currentGeneration);
            return new(restored, released, changed, retryable, retryable == 0, destroyed);
        }
    }
}

/// <summary>Host fixture implementation. It is never a Unity/native adapter.</summary>
public sealed class MemoryTextTarget(TextTargetState initial) : IUiTextTarget
{
    readonly object gate = new();
    TextTargetState state = initial;
    public int Writes { get; private set; }
    public TextTargetState ReadState() { lock (gate) return state; }
    public bool TryWriteText(TextTargetState expected, string value)
    {
        lock (gate)
        {
            if (state != expected) return false;
            state = state with { Text = value }; Writes++; return true;
        }
    }
    public void GameWrite(string text) { lock (gate) state = state with { Text = text }; }
    public void Recycle(TextTargetState next) { lock (gate) state = next; }
}
