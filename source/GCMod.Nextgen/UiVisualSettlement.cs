#nullable enable
using System.Text.Json;
namespace GCMod.Nextgen;

/// <summary>Finite post-write renderer observations. These flags contain no native handles or display text.</summary>
[Flags]
public enum UiVisualFailure
{
    None = 0, OwnerRebound = 1, SourceChanged = 2, TargetInactive = 4,
    MeshStale = 8, Dirty = 16, LayoutChanged = 32, DisplayChanged = 64,
    IntrinsicOverflow = 128, MaskClipped = 256, RenderChannelHidden = 512,
    ControlChanged = 1024, ReadFailed = 2048
}
[Flags]
public enum UiVisualField
{
    None = 0, Target = 1, Source = 2, Controls = 4, MeshSequence = 8,
    Dirty = 16, OwnRect = 32, AncestorMotion = 64, Font = 128,
    Material = 256, FontSize = 512, Color = 1024, Display = 2048,
    GlyphRect = 4096, Mask = 8192, Channel = 16384, LayoutDriver = 32768,
    Parent = 65536, Siblings = 131072, Transform = 262144
}
public enum UiVisualPhase { SourceJoin, Prepare, Written, WaitMesh, WaitLayout, Settled, Restored, Failed, JpBaseline, ClusterBaseline }
public enum UiVisualAction { Wait, Accept, Restore }
public sealed record UiVisualAssessment(UiVisualAction Action, UiVisualPhase Phase, UiVisualFailure Failure);

/// <summary>The actual adapter uses this policy; fixtures cannot override ownership/source/control failure.</summary>
public static class UiVisualSettlement
{
    public const int MaximumFrames = 60;
    public static UiVisualAssessment Assess(int age, UiVisualFailure failures, bool layoutStable)
    {
        const UiVisualFailure fatal = UiVisualFailure.OwnerRebound | UiVisualFailure.SourceChanged |
            UiVisualFailure.TargetInactive | UiVisualFailure.ControlChanged | UiVisualFailure.DisplayChanged | UiVisualFailure.ReadFailed;
        if ((failures & fatal) != 0) return new(UiVisualAction.Restore, UiVisualPhase.Failed, failures);
        if (age < 0 || age > MaximumFrames) return new(UiVisualAction.Restore, UiVisualPhase.Failed, failures);
        if ((failures & (UiVisualFailure.MeshStale | UiVisualFailure.Dirty)) != 0)
            return new(UiVisualAction.Wait, UiVisualPhase.WaitMesh, failures);
        if (!layoutStable || (failures & UiVisualFailure.LayoutChanged) != 0)
            return new(UiVisualAction.Wait, UiVisualPhase.WaitLayout, failures | UiVisualFailure.LayoutChanged);
        // Only an actually current, stable mesh/layout is allowed to supply this proof.
        if ((failures & (UiVisualFailure.IntrinsicOverflow | UiVisualFailure.MaskClipped | UiVisualFailure.RenderChannelHidden)) != 0)
            return new(UiVisualAction.Restore, UiVisualPhase.Failed, failures);
        return new(UiVisualAction.Accept, UiVisualPhase.Settled, UiVisualFailure.None);
    }
}

/// <summary>Visibility belongs to the glyph's material channel, not always the primary renderer.</summary>
public static class UiGlyphChannelPolicy
{
    public static bool Visible(int materialIndex, bool currentFont, bool ownChannel, bool activeRenderer, bool culled, float alpha)
        => materialIndex is >= 0 and <= 8 && currentFont && ownChannel && activeRenderer && !culled && float.IsFinite(alpha) && alpha > 0;
}

/// <summary>Geometry failure stays held for an actual owner/UI epoch; scheduling clocks are not source authority.</summary>
public sealed record UiGeometryHoldKey(string Binding, long Target, long OwnerGeneration, long UiGeneration);

/// <summary>Finite events shared by all attempts of one actual binding/target/owner/UI epoch.</summary>
public sealed class UiGeometryDiagnosticBudget(int maximumEntries = 256, int maximumEvents = 16)
{
    readonly Dictionary<UiGeometryHoldKey, HashSet<string>> events = new();
    public int EntryCount => events.Count;
    public bool TryRecord(UiGeometryHoldKey key, UiVisualPhase phase, UiVisualFailure failure, UiVisualField fields, uint detail = 0)
    {
        if (!events.TryGetValue(key, out var transitions))
        {
            if (events.Count >= Math.Clamp(maximumEntries, 1, 512)) return false;
            events[key] = transitions = new(StringComparer.Ordinal);
        }
        return transitions.Count < Math.Clamp(maximumEvents, 1, 16) && transitions.Add($"{phase}_{(int)failure:X}_{(int)fields:X}_{detail:X}");
    }
    public void ForgetOwner(long owner) { foreach (var key in events.Keys.Where(k => k.OwnerGeneration == owner).ToArray()) events.Remove(key); }
    public void Clear() => events.Clear();
}

/// <summary>Only an actually observed native preferred-size driver may change its driven axis.</summary>
public static class NativeContentFitPolicy
{
    public static bool Matches(bool horizontalPreferred, bool verticalPreferred, float originalWidth, float originalHeight,
        float width, float height, float preferredWidth, float preferredHeight)
    {
        var numbers = new[] { originalWidth, originalHeight, width, height, preferredWidth, preferredHeight };
        if (numbers.Any(n => !float.IsFinite(n) || n <= 0 || n > 9999) || !(horizontalPreferred || verticalPreferred)) return false;
        return Math.Abs(width - (horizontalPreferred ? preferredWidth : originalWidth)) <= .5f &&
            Math.Abs(height - (verticalPreferred ? preferredHeight : originalHeight)) <= .5f;
    }
}

/// <summary>Field-only diagnostics over a bounded native capture; never exposes the capture to a log sink.</summary>
public static class UiLayoutDiagnosticFields
{
    public static UiVisualField Compare(string before, string after)
    {
        using var a = JsonDocument.Parse(before); using var b = JsonDocument.Parse(after);
        if (a.RootElement.ValueKind != JsonValueKind.Object || b.RootElement.ValueKind != JsonValueKind.Object)
            return before == after ? UiVisualField.None : UiVisualField.Transform;
        UiVisualField result = UiVisualField.None;
        foreach (var property in a.RootElement.EnumerateObject())
        {
            if (b.RootElement.TryGetProperty(property.Name, out var value) && property.Value.GetRawText() == value.GetRawText()) continue;
            if (property.Name == "own" && property.Value.ValueKind == JsonValueKind.Object && value.ValueKind == JsonValueKind.Object)
            {
                foreach (var own in property.Value.EnumerateObject())
                    if (!value.TryGetProperty(own.Name, out var current) || own.Value.GetRawText() != current.GetRawText())
                        result |= own.Name switch { "width" or "height" => UiVisualField.OwnRect, "instance" => UiVisualField.Target, _ => UiVisualField.Transform };
            }
            else result |= property.Name switch
            { "own" => UiVisualField.OwnRect, "position" or "ownPosition" => UiVisualField.Transform, "parent" or "container" => UiVisualField.Parent,
                "siblings" => UiVisualField.Siblings, "ancestors" => UiVisualField.AncestorMotion,
                "fitter" or "group" or "contentDriver" => UiVisualField.LayoutDriver, _ => UiVisualField.Transform };
        }
        return result;
    }
}

/// <summary>The recovered selected-profile renderer validates its original JP against its supported
/// composite before writing; the shorter KO still requires strict leaf fit after natural layout.</summary>
public static class DecorativeJpGeometryPolicy
{
    public static bool Eligible(bool fullCurrentSource, bool supportedCluster, bool allSourceGlyphsVisible,
        bool compositeContained, bool masksContained, bool currentGlyphChannels)
        => fullCurrentSource && supportedCluster && allSourceGlyphsVisible && compositeContained && masksContained && currentGlyphChannels;
}

/// <summary>The recovered selected-profile post-KO contract requires exactly one rendered line.</summary>
public static class DecorativeKoGeometryPolicy
{
    public static bool SingleLine(int lineCount) => lineCount == 1;
}

/// <summary>Only a real existing selected-profile tab event may schedule its current direct info view.</summary>
public static class ProfileInfoRebindPolicy
{
    public static bool Eligible(bool realCallback, bool selected, bool acceptedProfileSource,
        bool uniqueCurrentTab, bool currentInfoGetterRoute)
        => realCallback && selected && acceptedProfileSource && uniqueCurrentTab && currentInfoGetterRoute;
}

public enum ProfileInfoActivationAction { WaitInactive, WaitActive, Activate, Invalid, Expired }
/// <summary>A real callback's current info getter may await activation for a fixed deadline,
/// separately from existing displayed ownership/restoration. Scheduling cannot renew it.</summary>
public static class ProfileInfoActivationPolicy
{
    public const int MaximumFrames = 90;
    public static ProfileInfoActivationAction Assess(int age, bool currentRoute, bool active, int activeFrames)
    {
        if (!currentRoute) return ProfileInfoActivationAction.Invalid;
        if (age < 0 || age > MaximumFrames) return ProfileInfoActivationAction.Expired;
        if (!active) return ProfileInfoActivationAction.WaitInactive;
        return activeFrames >= 2 ? ProfileInfoActivationAction.Activate : ProfileInfoActivationAction.WaitActive;
    }
}

/// <summary>Pure values captured inside Sort.Setup. No temporary sorter/work/native callback is retained.</summary>
public sealed record SortLabelSourceTicket(int OwnerInstance, long OwnerGeneration, long ListIdentity,
    int ToggleInstance, int SourceId, long TextIdentity, string ExactSource)
{
    public bool Matches(int ownerInstance, long ownerGeneration, long listIdentity, int toggleInstance,
        int sourceId, long textIdentity) => OwnerInstance == ownerInstance && OwnerGeneration == ownerGeneration &&
        ListIdentity == listIdentity && ToggleInstance == toggleInstance && SourceId == sourceId && TextIdentity == textIdentity;
}
