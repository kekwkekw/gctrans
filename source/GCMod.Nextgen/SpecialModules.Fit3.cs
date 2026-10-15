#nullable enable
using System.Collections.Immutable;
namespace GCMod.Nextgen;

public sealed record TitleFitMeasurement(string PhysicalContext, string BindingContext, string ProtectedStyle,
    string Text, string ModelText, float Size, bool Current, bool Supplier, bool Fresh, bool VisibleInk,
    bool FullSequence, bool Truncated, int Overflow, bool AutoSize);
public sealed record TitleFitDecision(string Action, float Size, string Reason);
public static class EpisodeTitleFitPolicy
{
    public static bool Eligible(EpisodeTitleLayoutPolicy policy, TitleFitMeasurement value) =>
        policy.ScopeKey == "episode-list-title" && policy.BaseSize == 35 && policy.DescendingSizes.SequenceEqual(new[] { 34, 33, 32, 31 })
        && policy.Restore == "owned-only" && policy.OverflowFailure == "restore-base"
        && value.Current && value.Supplier && !value.AutoSize && value.PhysicalContext.Length > 0 && value.BindingContext.Length > 0
        && value.ProtectedStyle.Length > 0 && value.Text == value.ModelText && value.Text.Any(c => c is >= '\uAC00' and <= '\uD7A3' or >= '\u3130' and <= '\u318F')
        && !value.Text.Any(c => "<>\r\n".Contains(c)) && float.IsFinite(value.Size);
    public static TitleFitDecision Decide(EpisodeTitleLayoutPolicy policy, TitleFitMeasurement original, TitleFitMeasurement current, ImmutableArray<int> tested)
    {
        if (!Eligible(policy, original) || !Eligible(policy, current) || original.Size != policy.BaseSize
            || original.PhysicalContext != current.PhysicalContext || original.BindingContext != current.BindingContext || original.ProtectedStyle != current.ProtectedStyle)
            return new("YIELD", current.Size, "CURRENT_CONTEXT_OR_STYLE_CHANGED");
        var grid = ImmutableArray.Create(policy.BaseSize).AddRange(policy.DescendingSizes);
        if (tested.IsDefaultOrEmpty || tested.Length > grid.Length || !tested.SequenceEqual(grid.Take(tested.Length)) || current.Size != tested[^1])
            return new("YIELD", current.Size, "EXTERNAL_SIZE_OR_INVALID_TRIAL_SEQUENCE");
        if (!current.Fresh || !current.VisibleInk) return new("WAIT", current.Size, "CURRENT_VISIBLE_LAYOUT_UNAVAILABLE");
        if (current.FullSequence && !current.Truncated && current.Overflow == -1)
            return new("KEEP", current.Size, "LARGEST_FITTING_TESTED_SIZE");
        return tested.Length == grid.Length ? new("RESTORE", policy.BaseSize, "UNRESOLVED_AT_MINIMUM")
            : new("TRY", grid[tested.Length], "NEXT_FINITE_SIZE");
    }
}
