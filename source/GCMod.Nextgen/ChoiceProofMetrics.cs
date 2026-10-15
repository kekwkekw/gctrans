#nullable enable
namespace GCMod.Nextgen;

/// <summary>Operation counts only, never an FPS claim. Conservative raycast probes remain where no global native change token exists.</summary>
public sealed class ChoiceProofMetrics
{
    public long SourceDiscoveryScans { get; internal set; }
    public long FullTreeScans { get; internal set; }
    public long FullSerializations { get; internal set; }
    public long BoundaryRaycasts { get; internal set; }
    public long StableRaycasts { get; internal set; }
    public long StableGuards { get; internal set; }
    public long CachedNodeChecks { get; internal set; }
    public long CachedSourceRowChecks { get; internal set; }
    public long GuardCaptures { get; internal set; }
}
