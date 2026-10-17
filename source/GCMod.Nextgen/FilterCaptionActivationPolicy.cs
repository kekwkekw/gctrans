#nullable enable
namespace GCMod.Nextgen;
public sealed record FilterCaptionActivationIdentity(long Owner, int OwnerInstance, long Lifetime, int Scene,
    long Button, int ButtonInstance, long Target, int TargetInstance, long UiGeneration, FilterPreferredCaptionRole Role);
public sealed record FilterCaptionActivationObservation(FilterCaptionActivationIdentity Identity, bool MainThread,
    bool Current, bool DirectTarget, bool SourceCurrent, bool Active, bool DonorReady, long DonorGeneration);
public enum FilterCaptionActivationResult { OffThread, Invalidated, Waiting, Unchanged, Rearm }
/// The two independently owned SelectAll/Deselect controls, never an arbitrary Filter option or source.
public sealed class FilterCaptionActivationState(FilterCaptionActivationIdentity identity)
{
    bool invalidated, ready;
    long donor;
    public FilterCaptionActivationResult Observe(FilterCaptionActivationObservation observation)
    {
        if (!observation.MainThread) return FilterCaptionActivationResult.OffThread;
        if (invalidated || observation.Identity != identity || !observation.Current || !observation.DirectTarget || !observation.SourceCurrent)
        { invalidated = true; return FilterCaptionActivationResult.Invalidated; }
        if (!observation.Active || !observation.DonorReady || observation.DonorGeneration <= 0)
        { ready = false; return FilterCaptionActivationResult.Waiting; }
        if (!ready || donor != observation.DonorGeneration)
        { ready = true; donor = observation.DonorGeneration; return FilterCaptionActivationResult.Rearm; }
        return FilterCaptionActivationResult.Unchanged;
    }
}
