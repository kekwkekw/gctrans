#nullable enable
namespace GCMod.Nextgen;

public sealed record HeaderEntryIdentity(long Owner, int OwnerInstance, long Lifetime, int Scene,
    long Target, int TargetInstance, long UiGeneration);
public sealed record HeaderEntryReadiness(HeaderEntryIdentity Identity, bool MainThread, bool OwnerAlive,
    bool TargetAlive, bool OwnerActive, bool TargetEnabled, bool TargetActive, bool SourceCurrent,
    bool DonorReady, long DonorGeneration);
public enum HeaderEntryReadinessResult { OffThread, Invalidated, Waiting, Unchanged, Rearm }

/// Readiness edges for one already validated HeaderPower producer/target. Never writes native state.
public sealed class HeaderEntryReadinessState(HeaderEntryIdentity identity)
{
    bool invalidated, wasReady;
    long donorGeneration;
    public HeaderEntryReadinessResult Observe(HeaderEntryReadiness observation)
    {
        if (!observation.MainThread) return HeaderEntryReadinessResult.OffThread;
        if (invalidated || observation.Identity != identity || !observation.OwnerAlive || !observation.TargetAlive || !observation.SourceCurrent)
        {
            invalidated = true;
            return HeaderEntryReadinessResult.Invalidated;
        }
        bool ready = observation.OwnerActive && observation.TargetEnabled && observation.TargetActive
            && observation.DonorReady && observation.DonorGeneration > 0;
        if (!ready) { wasReady = false; return HeaderEntryReadinessResult.Waiting; }
        if (!wasReady || donorGeneration != observation.DonorGeneration)
        {
            wasReady = true; donorGeneration = observation.DonorGeneration;
            return HeaderEntryReadinessResult.Rearm;
        }
        return HeaderEntryReadinessResult.Unchanged;
    }
}
