#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GCMod.Nextgen;

/// <summary>Closed diagnostic vocabulary; neither exception messages nor native identities leave this boundary.</summary>
public enum ChoiceDisplayPhase { ResolveSource, Plan, BaselineControls, BeforeRaycast, FontPrimary, FontDonor, Glyph, CreateOwnVisual, Fit, PrecommitControls, PrecommitRaycast, Show, Postcommit, Retained, Cleanup }
public enum ChoiceDisplayReason { None, InvalidOptionCount, OptionNotPrepared, SourceChanged, PlanUnavailable, NativeControlsChanged, NativeRaycastChanged, CurrentEventSystemRequired, CurrentButtonCanvasRequired, RaycastBudget, ButtonRaycastRequired, ComponentBudget, SubtreeBudget, GlyphBudget, MaskContainsInput, ExistingNativeMask, PrimaryUnavailable, PrimaryRouteMismatch, DonorUnavailable, DonorGlyphMissing, FallbackGlyphMissing, ButtonRectRequired, ButtonLayoutUnsettled, NativeFontSizeBound, OwnedVisualDidNotFit, OwnGraphicScopeChanged, OwnInputComponent, OwnObjectLost, CleanupPending, IsolatedException }
public enum ChoiceExceptionKind { None, InvalidOperation, Argument, NullReference, IndexOutOfRange, InvalidData, Other }
public enum ChoiceControlRole { None, Source, Branch, Button, Navigation, NativeGeometry, NativeText, NativeFont, NativeComponent, NativeSubtree, OwnerLifetime, NativeControl, MalformedProof }
public sealed class ChoiceDisplayException(ChoiceDisplayReason reason) : InvalidOperationException(reason.ToString()) { public ChoiceDisplayReason Reason { get; } = reason; }
public sealed record ChoiceCommitFailure(ChoiceDisplayPhase Phase, ChoiceDisplayReason Reason, int OptionOrdinal, ChoiceExceptionKind Exception, ChoiceExceptionKind CleanupException = ChoiceExceptionKind.None);
public sealed record ChoiceCommitResult(bool Committed, ChoiceCommitFailure? Failure);

public static class ChoiceDisplayDiagnostics
{
    public static string Key(string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id ?? "")))[..12];
    public static string Code(ChoiceCommitFailure failure, string groupId) =>
        $"CH_FAIL_G_{Key(groupId)}_O_{Math.Clamp(failure.OptionOrdinal, -1, 31)}_{failure.Phase.ToString().ToUpperInvariant()}_{failure.Reason.ToString().ToUpperInvariant()}";
    public static ChoiceExceptionKind ExceptionKind(Exception exception) => exception switch
    {
        ArgumentException => ChoiceExceptionKind.Argument,
        NullReferenceException => ChoiceExceptionKind.NullReference,
        IndexOutOfRangeException => ChoiceExceptionKind.IndexOutOfRange,
        InvalidDataException => ChoiceExceptionKind.InvalidData,
        InvalidOperationException => ChoiceExceptionKind.InvalidOperation,
        _ => ChoiceExceptionKind.Other
    };
    public static ChoiceDisplayReason Reason(Exception exception) => exception is ChoiceDisplayException known ? known.Reason : exception.Message switch
    {
        "CURRENT_CHOICE_SOURCE_CHANGED" => ChoiceDisplayReason.SourceChanged,
        "CHOICE_COMPONENT_BUDGET" => ChoiceDisplayReason.ComponentBudget,
        "CHOICE_SUBTREE_BUDGET" => ChoiceDisplayReason.SubtreeBudget,
        "CHOICE_GLYPH_BUDGET" => ChoiceDisplayReason.GlyphBudget,
        "CURRENT_EVENT_SYSTEM_REQUIRED" => ChoiceDisplayReason.CurrentEventSystemRequired,
        "CURRENT_BUTTON_CANVAS_REQUIRED" => ChoiceDisplayReason.CurrentButtonCanvasRequired,
        "CURRENT_RAYCAST_BUDGET" => ChoiceDisplayReason.RaycastBudget,
        "CURRENT_BUTTON_RAYCAST_REQUIRED" => ChoiceDisplayReason.ButtonRaycastRequired,
        _ => ChoiceDisplayReason.IsolatedException
    };
    public static ChoiceControlRole ControlsChanged(string baseline, string current)
    {
        try
        {
            using var left = JsonDocument.Parse(baseline); using var right = JsonDocument.Parse(current);
            return Difference(left.RootElement, right.RootElement, "");
        }
        catch (JsonException) { return ChoiceControlRole.MalformedProof; }
    }
    static ChoiceControlRole Role(string path) => path.Contains(".source", StringComparison.Ordinal) ? ChoiceControlRole.Source
        : path.Contains(".branch", StringComparison.Ordinal) || path.Contains(".clickTarget", StringComparison.Ordinal) ? ChoiceControlRole.Branch
        : path.Contains(".navigation", StringComparison.Ordinal) || path.Contains(".up", StringComparison.Ordinal) || path.Contains(".down", StringComparison.Ordinal) || path.Contains(".left", StringComparison.Ordinal) || path.Contains(".right", StringComparison.Ordinal) ? ChoiceControlRole.Navigation
        : path.Contains("Rect", StringComparison.Ordinal) ? ChoiceControlRole.NativeGeometry
        : path.Contains(".font", StringComparison.Ordinal) || path.Contains(".material", StringComparison.Ordinal) || path.Contains(".size", StringComparison.Ordinal) ? ChoiceControlRole.NativeFont
        : path.Contains(".texts", StringComparison.Ordinal) ? ChoiceControlRole.NativeText
        : path.Contains(".components", StringComparison.Ordinal) ? ChoiceControlRole.NativeComponent
        : path.Contains(".inventory", StringComparison.Ordinal) ? ChoiceControlRole.NativeSubtree
        : path.Contains(".button", StringComparison.Ordinal) || path.Contains(".interactable", StringComparison.Ordinal) ? ChoiceControlRole.Button
        : path is ".owner" or ".instance" or ".root" or ".letters" or ".eventText" or ".action" ? ChoiceControlRole.OwnerLifetime
        : ChoiceControlRole.NativeControl;
    static ChoiceControlRole Difference(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind) return Role(path);
        if (left.ValueKind == JsonValueKind.Object)
        {
            var lp = left.EnumerateObject().ToArray(); var rp = right.EnumerateObject().ToArray();
            if (lp.Length != rp.Length) return Role(path);
            for (int i = 0; i < lp.Length; i++)
            {
                if (lp[i].Name != rp[i].Name) return Role(path);
                var difference = Difference(lp[i].Value, rp[i].Value, path + "." + lp[i].Name);
                if (difference != ChoiceControlRole.None) return difference;
            }
            return ChoiceControlRole.None;
        }
        if (left.ValueKind == JsonValueKind.Array)
        {
            var la = left.EnumerateArray().ToArray(); var ra = right.EnumerateArray().ToArray();
            if (la.Length != ra.Length) return Role(path);
            for (int i = 0; i < la.Length; i++) { var difference = Difference(la[i], ra[i], path); if (difference != ChoiceControlRole.None) return difference; }
            return ChoiceControlRole.None;
        }
        return left.GetRawText() == right.GetRawText() ? ChoiceControlRole.None : Role(path);
    }
}

/// <summary>Deferred Unity destruction is not completed cleanup. Caller checks only its captured owned objects.</summary>
public sealed class ChoiceCleanupBarrier
{
    public bool Pending { get; private set; }
    int retiredFrame;
    public void Retire(int unityFrame) { if (Pending) throw new InvalidOperationException("OWNED_CLEANUP_ALREADY_PENDING"); Pending = true; retiredFrame = unityFrame; }
    public bool Confirm(int unityFrame, Func<bool> allOwnedDestroyed)
    {
        if (!Pending) return true;
        if (unityFrame <= retiredFrame || !allOwnedDestroyed()) return false;
        Pending = false; return true;
    }
}

public enum ChoiceGlyphKind { Hangul, Whitespace, Ascii, Other }
public sealed record ChoiceDonorCoverage(int? FirstMissingScalar, int? FirstMissingRequiredScalar);
/// <summary>The approved Medium is the Hangul donor; primary/fallback still proves the complete string separately.</summary>
public static class ChoiceFontRolePolicy
{
    public static bool RequiresMedium(int scalar) => scalar is >= 0xAC00 and <= 0xD7A3;
    public static ChoiceGlyphKind Kind(int scalar) => RequiresMedium(scalar) ? ChoiceGlyphKind.Hangul
        : Rune.IsWhiteSpace(new Rune(scalar)) ? ChoiceGlyphKind.Whitespace
        : scalar is >= 0 and <= 0x7F ? ChoiceGlyphKind.Ascii : ChoiceGlyphKind.Other;
    public static ChoiceDonorCoverage ProbeDonor(string text, Func<int,bool> hasGlyph)
    {
        int? any = null, required = null;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || hasGlyph(rune.Value)) continue;
            any ??= rune.Value;
            if (RequiresMedium(rune.Value)) required ??= rune.Value;
        }
        return new(any, required);
    }
    public static int? FirstMissingScalar(string text, Func<int,bool> hasGlyph)
    {
        foreach (var rune in text.EnumerateRunes()) if (!Rune.IsControl(rune) && !hasGlyph(rune.Value)) return rune.Value;
        return null;
    }
}

public enum ChoiceCleanupRole { None, Visibility, Mask, Destroy }
public sealed record ChoiceCleanupResult(bool Completed, int OptionOrdinal, ChoiceCleanupRole Role, ChoiceExceptionKind Exception);
/// <summary>Attempt all owned recovery roles. One failed setter cannot strand the later group's original labels.</summary>
public static class ChoiceOwnedCleanup
{
    public static ChoiceCleanupResult Run(int options, Action<int> hideOwn, Action<int> restoreOwnMask, Action<int> destroyOwn)
    {
        if (options is <1 or >32) return new(false, -1, ChoiceCleanupRole.None, ChoiceExceptionKind.Argument);
        var result = new ChoiceCleanupResult(true, -1, ChoiceCleanupRole.None, ChoiceExceptionKind.None);
        void Attempt(int ordinal, ChoiceCleanupRole role, Action<int> action)
        {
            try { action(ordinal); }
            catch (Exception failure) { if (result.Completed) result = new(false, ordinal, role, ChoiceDisplayDiagnostics.ExceptionKind(failure)); }
        }
        for (int i = 0; i < options; i++)
        {
            Attempt(i, ChoiceCleanupRole.Visibility, hideOwn);
            Attempt(i, ChoiceCleanupRole.Mask, restoreOwnMask);
            Attempt(i, ChoiceCleanupRole.Destroy, destroyOwn);
        }
        return result;
    }
}
