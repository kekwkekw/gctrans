#nullable enable
namespace GCMod.Nextgen;

public sealed record FloatTargetState(PhysicalTargetIdentity Identity, string Property, float Value,
    long OwnerGeneration, long SourceGeneration, string ProtectedIdentity, bool Active = true, bool DestroyedOrRebound = false);
public interface IUiFloatTarget
{
    FloatTargetState ReadState();
    bool TryWriteFloat(FloatTargetState expected, float value);
}
/// <summary>Scalar properties use the same physical identity discipline as text, in distinct slots.</summary>
public sealed class UiFloatOwnershipService
{
    sealed record Lease(IUiFloatTarget Target, string Writer, FloatTargetState Original, float Last);
    readonly Dictionary<(ulong, string), Lease> leases = new();
    readonly object gate = new();
    public int LeaseCount { get { lock (gate) return leases.Count; } }
    static bool SameSource(FloatTargetState a, FloatTargetState b) => a.Identity == b.Identity && a.Property == b.Property
        && a.OwnerGeneration == b.OwnerGeneration && a.SourceGeneration == b.SourceGeneration && a.ProtectedIdentity == b.ProtectedIdentity;
    public OwnershipResult TryWrite(IUiFloatTarget target, string writer, FloatTargetState expected, float value)
    {
        lock (gate)
        {
            if (!float.IsFinite(value) || target is IUiTargetAccessGate access && !access.CanAccess) return new(false, "PROPERTY_ACCESS_UNAVAILABLE");
            FloatTargetState current;
            try { current = target.ReadState(); } catch { return new(false, "PROPERTY_READ_UNAVAILABLE"); }
            if (current != expected || !current.Active || current.Identity.NativeObject == 0) return new(false, "PROPERTY_SOURCE_MISMATCH");
            var key = (current.Identity.NativeObject, current.Property);
            leases.TryGetValue(key, out var lease);
            if (lease != null)
            {
                if (!SameSource(lease.Original, current)) { leases.Remove(key); lease = null; }
                else if (lease.Writer != writer) return new(false, "PHYSICAL_PROPERTY_ALREADY_OWNED");
                else if (current.Value != lease.Last) { leases.Remove(key); return new(false, "EXTERNAL_PROPERTY_CHANGE_YIELDED"); }
            }
            if (current.Value == value) return new(false, "PROPERTY_UNCHANGED");
            if (leases.Count >= 4096 && lease == null) return new(false, "PROPERTY_LEASE_BOUND");
            try { if (!target.TryWriteFloat(current, value)) return new(false, "PROPERTY_WRITE_BOUNDARY_CHANGED"); }
            catch { return new(false, "PROPERTY_WRITE_RETRYABLE"); }
            leases[key] = lease == null ? new(target, writer, current, value) : lease with { Last = value };
            return new(true, "OWNED_PROPERTY_WRITE");
        }
    }
    public RestoreOutcome Restore(PhysicalTargetIdentity identity, string property)
    {
        lock (gate)
        {
            var key = (identity.NativeObject, property);
            if (!leases.TryGetValue(key, out var lease) || lease.Original.Identity != identity) return RestoreOutcome.NotOwned;
            if (lease.Target is IUiTargetAccessGate access && !access.CanAccess) return RestoreOutcome.RetryableReadFailure;
            FloatTargetState now;
            try { now = lease.Target.ReadState(); } catch { return RestoreOutcome.RetryableReadFailure; }
            RestoreOutcome outcome;
            if (now.DestroyedOrRebound || !SameSource(lease.Original, now)) outcome = RestoreOutcome.DestroyedOrRebound;
            else if (!now.Active) return RestoreOutcome.RetryableReadFailure;
            else if (now.Value != lease.Last) outcome = RestoreOutcome.ExternalChanged;
            else
            {
                try
                {
                    if (!lease.Target.TryWriteFloat(now, lease.Original.Value)) return RestoreOutcome.RetryableWriteFailure;
                }
                catch { return RestoreOutcome.RetryableWriteFailure; }
                outcome = RestoreOutcome.Restored;
            }
            leases.Remove(key); return outcome;
        }
    }
    public bool RestoreAll()
    {
        lock (gate)
        {
            bool complete = true;
            foreach (var lease in leases.Values.ToArray())
                complete &= Restore(lease.Original.Identity, lease.Original.Property) is not (RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure);
            return complete;
        }
    }
    /// <summary>Only a witnessed natural rebind may carry a style lease to the same physical cell.</summary>
    public RestoreOutcome RestoreAtRebind(PhysicalTargetIdentity oldIdentity, string property, IUiFloatTarget currentTarget, FloatTargetState expected)
    {
        lock (gate)
        {
            var key = (oldIdentity.NativeObject, property);
            if (!leases.TryGetValue(key, out var lease) || lease.Original.Identity != oldIdentity) return RestoreOutcome.NotOwned;
            if (currentTarget is IUiTargetAccessGate access && !access.CanAccess) return RestoreOutcome.RetryableReadFailure;
            FloatTargetState now;
            try { now = currentTarget.ReadState(); } catch { return RestoreOutcome.RetryableReadFailure; }
            if (now != expected || now.Identity != oldIdentity || now.Property != property || !now.Active)
                return RestoreOutcome.RetryableReadFailure;
            if (now.ProtectedIdentity != lease.Original.ProtectedIdentity || now.Value != lease.Last)
            { leases.Remove(key); return RestoreOutcome.ExternalChanged; }
            // The native binding callback proves source turnover; property/source protected style is unchanged.
            leases[key] = lease with { Target = currentTarget, Original = now with { Value = lease.Original.Value } };
            return Restore(oldIdentity, property);
        }
    }
}
