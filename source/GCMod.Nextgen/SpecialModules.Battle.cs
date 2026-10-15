#nullable enable
namespace GCMod.Nextgen;

public static class BattleCaptionPolicy
{
    /// <summary>Replaces only the exact label of the completed native parameter, preserving every suffix code unit.</summary>
    public static bool TryCostTitle(string completedNativeText, UiResource label, IMasterTranslationView master, out string output)
    {
        output = "";
        if (label.Jp != "必要熱情" || !completedNativeText.StartsWith(label.Jp, StringComparison.Ordinal)) return false;
        var suffix = completedNativeText[label.Jp.Length..];
        if (suffix.Length == 0 || !(char.IsWhiteSpace(suffix[0]) || suffix[0] == '<') || !suffix.Any(char.IsDigit)
            || !new UiResourceResolver(master).TryResolve(label, out var ko)) return false;
        output = ko + suffix; return true;
    }
}
