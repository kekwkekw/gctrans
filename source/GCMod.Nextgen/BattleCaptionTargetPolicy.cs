#nullable enable
namespace GCMod.Nextgen;

/// <summary>Finite identity join for the typed RaceTypeView ancestor, not a name/active first match.</summary>
public static class BattleCaptionTargetPolicy
{
    public static long UniqueTypedAncestor(IReadOnlyList<long> candidateGroups, IReadOnlyList<long> typedAncestors)
    {
        if (candidateGroups.Count is < 1 or > 256 || typedAncestors.Count is < 1 or > 16 ||
            candidateGroups.Any(x => x == 0) || typedAncestors.Any(x => x == 0) ||
            candidateGroups.Distinct().Count() != candidateGroups.Count || typedAncestors.Distinct().Count() != typedAncestors.Count) return 0;
        var matches = candidateGroups.Where(typedAncestors.Contains).ToArray();
        return matches.Length == 1 ? matches[0] : 0;
    }
}
