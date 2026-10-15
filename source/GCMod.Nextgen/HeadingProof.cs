#nullable enable
namespace GCMod.Nextgen;

public enum HeadingKind { AnnotationFactory, StandaloneDictionary }
public sealed record HeadingObjectIdentity(long Pointer, int Instance, string NativeClass);
/// <summary>Pure values captured from the already validated typed factory and its current owned nodes. Never a click proof.</summary>
public sealed record FactoryHeadingObservation(
    HeadingObjectIdentity Owner, long OwnerGeneration, long UiGeneration, bool OwnerActive,
    HeadingObjectIdentity Parts, HeadingObjectIdentity Close, HeadingObjectIdentity Header,
    HeadingObjectIdentity Localizer, HeadingObjectIdentity Title, HeadingObjectIdentity Body, HeadingObjectIdentity Ruby,
    long PartsOwner, long CloseOwner, long HeaderOwner, long TitleOwner, long BodyOwner, long RubyOwner,
    string PartsPath, string ClosePath, string HeaderPath, long LocalizerTarget, int LocalizerType, string LocalizerKey,
    string HeaderText, string Keyword, string Description, string RubyText);
public sealed record FactoryHeadingEvidence(AnnotationDeliveredRecord Record, FactoryHeadingObservation Captured);

public static class FactoryHeadingPolicy
{
    public const string PartsPath = "Frame_Popup/Frame_Content/Window_Left";
    public const string ClosePath = "Frame_Popup/PopupBase/GC_ButtonSet/BtNormalM_close";
    public const string HeaderPath = "Frame_Popup/PopupBase/TitleWindow/Text_Title";
    public static bool TryCapture(AnnotationDeliveredRecord actual, FactoryHeadingObservation current, string? ownWritten,
        out FactoryHeadingEvidence? evidence, out string reason)
    {
        evidence = null;
        if (!Valid(actual, current, ownWritten, out reason)) return false;
        evidence = new(actual, current); reason = "FACTORY_OWNER_CAPTURED"; return true;
    }
    public static bool IsCurrent(FactoryHeadingEvidence evidence, FactoryHeadingObservation current, string? ownWritten, out string reason)
    {
        var old = evidence.Captured;
        if (old.Owner != current.Owner || old.OwnerGeneration != current.OwnerGeneration || old.UiGeneration != current.UiGeneration)
        { reason = "FACTORY_OWNER_GENERATION_CHANGED"; return false; }
        if (old.Parts != current.Parts || old.Close != current.Close || old.Header != current.Header || old.Localizer != current.Localizer ||
            old.Title != current.Title || old.Body != current.Body || old.Ruby != current.Ruby)
        { reason = "FACTORY_DIRECT_TARGET_CHANGED"; return false; }
        return Valid(evidence.Record, current, ownWritten, out reason);
    }
    static bool Valid(AnnotationDeliveredRecord actual, FactoryHeadingObservation x, string? ownWritten, out string reason)
    {
        bool Reject(string why, out string value) { value = why; return false; }
        if (x.Owner.NativeClass != "Gc.NovelKeyWordPopup" || x.Header.NativeClass != "TMPro.TextMeshProUGUI" ||
            x.Localizer.NativeClass != "Gc.Localizer.TextLocalizer") return Reject("FACTORY_NATIVE_CLASS_MISMATCH", out reason);
        var identities = new[] { x.Owner, x.Parts, x.Close, x.Header, x.Localizer, x.Title, x.Body, x.Ruby };
        if (identities.Any(i => i.Pointer == 0 || i.Instance == 0 || string.IsNullOrEmpty(i.NativeClass)) ||
            identities.Select(i => i.Pointer).Distinct().Count() != identities.Length || actual.Pointer == 0 ||
            x.OwnerGeneration <= 0 || x.UiGeneration <= 0) return Reject("FACTORY_IDENTITY_INVALID", out reason);
        if (!x.OwnerActive) return Reject("FACTORY_OWNER_INACTIVE", out reason);
        if (new[] { x.PartsOwner, x.CloseOwner, x.HeaderOwner, x.TitleOwner, x.BodyOwner, x.RubyOwner }.Any(owner => owner != x.Owner.Pointer))
            return Reject("FACTORY_DIRECT_OWNER_MISMATCH", out reason);
        if (x.PartsPath != PartsPath || x.ClosePath != ClosePath || x.HeaderPath != HeaderPath)
            return Reject("FACTORY_SERIALIZED_PATH_MISMATCH", out reason);
        if (x.LocalizerTarget != x.Header.Pointer || x.LocalizerType != 0 || x.LocalizerKey != "")
            return Reject("FACTORY_LOCALIZER_MISMATCH", out reason);
        if (x.Keyword != actual.Keyword || x.Description != actual.Description || x.RubyText != actual.Ruby)
            return Reject("FACTORY_CURRENT_CONTENT_MISMATCH", out reason);
        if (x.HeaderText != "辞典" && (ownWritten == null || x.HeaderText != ownWritten))
            return Reject("FACTORY_HEADER_SOURCE_CHANGED", out reason);
        reason = "FACTORY_OWNER_CURRENT"; return true;
    }
}

public sealed record HeadingFontResult(bool Allowed, string Reason, int? MissingRequiredScalar = null);
/// <summary>Only the two existing generic headings use this role check. FontService and Choice behavior are unchanged.</summary>
public static class HeadingFontPolicy
{
    public static HeadingFontResult Check(bool canAccess, HeadingKind kind, string text, FontDonorHandle? expected,
        Func<FontDonorHandle?> current, Func<int, bool> mediumHasGlyph, Func<string, bool> targetCoverage)
    {
        if (!canAccess) return new(false, "HEADING_NATIVE_ACCESS_GATE_CLOSED");
        if (kind is not HeadingKind.AnnotationFactory and not HeadingKind.StandaloneDictionary) return new(false, "HEADING_ROLE_UNSUPPORTED");
        try
        {
            if (expected == null || expected.Profile != FontService.ApprovedProfile || expected.NativeHandle == 0 ||
                expected.InstanceId == 0 || expected.Generation <= 0 || current() != expected) return new(false, "HEADING_DONOR_STALE");
            var coverage = ChoiceFontRolePolicy.ProbeDonor(text, mediumHasGlyph);
            if (coverage.FirstMissingRequiredScalar is int missing) return new(false, "HEADING_REQUIRED_MEDIUM_GLYPH_MISSING", missing);
            if (!targetCoverage(text)) return new(false, "HEADING_PRIMARY_FALLBACK_MISSING");
            if (current() != expected) return new(false, "HEADING_DONOR_STALE");
            return new(true, "HEADING_FONT_ROLE_READY");
        }
        catch { return new(false, "HEADING_FONT_READ_UNAVAILABLE"); }
    }
}

