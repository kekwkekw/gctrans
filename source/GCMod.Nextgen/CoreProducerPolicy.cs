#nullable enable
using System.Collections.Immutable;
using System.Text.Json;

namespace GCMod.Nextgen;

// These are literal identities and a finite typed owner field, never reflection instructions.
public sealed record OptionAreaContract(string Field, ImmutableArray<string> Segments, ImmutableArray<int> Indices);
public sealed record CoreProducerContract(int LocalizerType, string TextConfigKey, OptionAreaContract? OptionArea = null);
public sealed record CoreProducerIdentity(long Owner, int OwnerInstance, long Lifetime, long Target, int TargetInstance,
    long Localizer, int LocalizerType, string MetadataKey, long Area, int AreaInstance);
public sealed record CoreProducerObservation(CoreProducerIdentity Identity, bool OwnerOwnsTarget, bool UniqueTarget,
    bool ExactSourceOrOwnedDisplay, bool AreaPathMatches);

/// Pure identity/path policy shared by native discovery and its retained guard. No native reads happen before access checks.
public static class CoreProducerPolicy
{
    // A visual failure is not an identity loss. The exact owned display may confirm
    // source only inside its conditional restore, never as a new failed apply/rebind.
    public static bool OwnedDisplayMayConfirmSource(bool visualFailed, bool conditionalRestore) => !visualFailed || conditionalRestore;
    public static CoreProducerContract? Parse(JsonElement node, string adapter, string sourceKind, string scope)
    {
        ContractRules.Only(node, "family", "localizer_type", "text_config_key", "option_area");
        ContractRules.Require(ContractRules.String(node, "family", 64) == "core-literal-v1"
            && (adapter is "core.static-caption.v1" or "ui4.static-caption.v1")
            && sourceKind == "static-asset", "CORE_PRODUCER_ADAPTER");
        ContractRules.Require(node.TryGetProperty("localizer_type", out var type) && type.ValueKind == JsonValueKind.Number && type.TryGetInt32(out int localizerType)
            && localizerType == 0, "CORE_LITERAL_TYPE");
        ContractRules.Require(node.TryGetProperty("text_config_key", out var keyNode) && keyNode.ValueKind == JsonValueKind.String,
            "CORE_LITERAL_METADATA_KEY");
        string key = keyNode.GetString()!;
        ContractRules.Require(key.Length <= 128 && !key.Any(char.IsControl), "CORE_LITERAL_METADATA_KEY");
        OptionAreaContract? area = null;
        if (node.TryGetProperty("option_area", out var a))
        {
            ContractRules.Only(a, "field", "segments", "indices");
            var field = ContractRules.String(a, "field", 64);
            ContractRules.Require(scope == "gc.option.optionpopup" && (field is "soundAndTextOptionArea" or "screenSizeOptionArea" or "downloadOptionArea"),
                "OPTION_AREA_TYPED_FIELD");
            ContractRules.Require(a.TryGetProperty("segments", out var names) && a.TryGetProperty("indices", out _), "OPTION_AREA_PATH_BOUND");
            var indices = a.GetProperty("indices");
            ContractRules.Require(names.ValueKind == JsonValueKind.Array && names.GetArrayLength() is > 0 and <= 16
                && indices.ValueKind == JsonValueKind.Array && indices.GetArrayLength() == names.GetArrayLength(), "OPTION_AREA_PATH_BOUND");
            var segments = names.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : "").ToImmutableArray();
            ContractRules.Require(segments.All(SafeSegment), "OPTION_AREA_PATH_SEGMENT");
            ContractRules.Require(indices.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out int n) && n is >= 0 and < 512), "OPTION_AREA_PATH_INDEX");
            area = new(field, segments, indices.EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray());
        }
        return new(0, key, area);
    }
    static bool SafeSegment(string s) => s.Length is > 0 and <= 128 && s is not "." and not ".."
        && s.IndexOfAny(new[] {'/', '\\', '*', '?', '\0'}) < 0 && !s.Any(char.IsControl);
    public static bool SameContract(CoreProducerContract? a, CoreProducerContract? b) => a is null ? b is null : b is not null
        && a.LocalizerType == b.LocalizerType && a.TextConfigKey == b.TextConfigKey
        && (a.OptionArea is null ? b.OptionArea is null : b.OptionArea is not null && a.OptionArea.Field == b.OptionArea.Field
            && a.OptionArea.Segments.SequenceEqual(b.OptionArea.Segments) && a.OptionArea.Indices.SequenceEqual(b.OptionArea.Indices));
    public static bool Matches(bool canAccess, CoreProducerContract? expected, bool allowAbsent,
        CoreProducerIdentity? captured, Func<CoreProducerObservation> read)
    {
        if (!canAccess) return false;
        var now = read(); var i = now.Identity;
        if (i.Owner == 0 || i.Target == 0 || i.Lifetime <= 0 || !now.OwnerOwnsTarget || !now.UniqueTarget
            || !now.ExactSourceOrOwnedDisplay || !now.AreaPathMatches || captured is not null && captured != i) return false;
        if (expected?.OptionArea != null && i.Area == 0) return false;
        if (i.Localizer == 0) return allowAbsent && (expected == null || expected.TextConfigKey.Length == 0);
        return i.LocalizerType == (expected?.LocalizerType ?? 0) && i.MetadataKey == (expected?.TextConfigKey ?? "");
    }
    public static T? ResolveAreaTarget<T>(bool canAccess, T root, OptionAreaContract path,
        Func<T, int> childCount, Func<T, int, T> child, Func<T, string> name) where T : class
    {
        if (!canAccess || path.Segments.Length is <= 0 or > 16 || path.Indices.Length != path.Segments.Length) return null;
        var current = root;
        for (int n = 0; n < path.Segments.Length; n++)
        {
            int count = childCount(current), index = path.Indices[n];
            if (count < 0 || count > 512 || index < 0 || index >= count || !SafeSegment(path.Segments[n])) return null;
            var next = child(current, index);
            if (name(next) != path.Segments[n]) return null;
            current = next;
        }
        return current;
    }
}
