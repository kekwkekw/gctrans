#nullable enable
namespace GCMod.Nextgen;

/// <summary>Battle caption and completed cost-label coverage. The callbacks are read-only
/// observations of the actual target; CPU callers supply explicit mocks, never native proof.</summary>
public static class BattleFontPolicy
{
    public static GeneralUiFontResult Check(bool canAccess, string output, FontDonorHandle? expected,
        Func<FontDonorHandle?> current, Func<bool> currentTarget, Func<bool> richText,
        Func<int, bool> mediumHasGlyph, Func<string, bool> primaryFallbackCoverage)
    {
        // This guard precedes even native target identity, richText and glyph reads.
        if (!canAccess) return new(false, "BATTLE_FONT_ACCESS_CLOSED");
        try
        {
            if (expected == null || expected.Profile != FontService.ApprovedProfile || expected.NativeHandle == 0 ||
                expected.InstanceId == 0 || expected.Generation <= 0 || current() != expected)
                return new(false, "BATTLE_FONT_DONOR_STALE");
            if (!currentTarget()) return new(false, "BATTLE_FONT_TARGET_NOT_CURRENT");
            var result = GeneralUiFontPolicy.Check(true, output, richText(), expected, current,
                mediumHasGlyph, primaryFallbackCoverage);
            if (!result.Allowed) return new(false, result.Reason.Replace("GENERAL_FONT_", "BATTLE_FONT_", StringComparison.Ordinal));
            if (!currentTarget()) return new(false, "BATTLE_FONT_TARGET_NOT_CURRENT");
            if (current() != expected) return new(false, "BATTLE_FONT_DONOR_STALE");
            return new(true, "BATTLE_FONT_ROLE_READY");
        }
        catch { return new(false, "BATTLE_FONT_READ_UNAVAILABLE"); }
    }
}
