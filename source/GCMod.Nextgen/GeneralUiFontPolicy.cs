#nullable enable
using System.Text;
using System.Text.RegularExpressions;
namespace GCMod.Nextgen;

public sealed record GeneralUiFontResult(bool Allowed, string Reason);
public interface IGeneralUiFontTarget
{
    GeneralUiFontResult CheckGeneralUiFont(string text, FontDonorHandle donor, FontService service);
}

/// <summary>General UI only. Medium owns Hangul; actual primary/fallback owns the complete rendered text.
/// Only the formatting tags used by the supported UI corpus are projected. Unknown glyph-producing tags fail closed.</summary>
public static class GeneralUiFontPolicy
{
    static readonly Regex Format = new(@"\G<(?:color=#[0-9A-Fa-f]{6,8}|cspace=-?[0-9]+(?:\.[0-9]+)?(?:em|px|%)?|/color|/cspace)>",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
    public static bool RequiresMedium(int scalar) => scalar is >= 0xAC00 and <= 0xD7A3 or >= 0x1100 and <= 0x11FF
        or >= 0x3130 and <= 0x318F or >= 0xA960 and <= 0xA97F or >= 0xD7B0 and <= 0xD7FF;
    public static bool TryVisibleText(string raw, bool richText, out string visible)
    {
        visible = "";
        if (raw.Length is 0 or > 4096) return false;
        if (!richText) { visible = raw; return true; }
        var output = new StringBuilder(raw.Length);
        var tags = new Stack<string>();
        for (int i = 0; i < raw.Length;)
        {
            if (raw[i] != '<') { output.Append(raw[i++]); continue; }
            var match = Format.Match(raw, i);
            if (!match.Success || match.Index != i) return false;
            string token = match.Value;
            if (token[1] == '/')
            {
                string name = token[2..^1];
                if (tags.Count == 0 || tags.Pop() != name) return false;
            }
            else tags.Push(token[1..token.IndexOf('=')]);
            i += match.Length;
        }
        visible = output.ToString(); return tags.Count == 0 && visible.Length > 0;
    }
    public static GeneralUiFontResult Check(bool canAccess, string text, bool richText, FontDonorHandle? expected,
        Func<FontDonorHandle?> current, Func<int, bool> mediumHasGlyph, Func<string, bool> targetCoverage)
    {
        if (!canAccess) return new(false, "GENERAL_FONT_ACCESS_CLOSED");
        try
        {
            if (expected == null || expected.Profile != FontService.ApprovedProfile || expected.NativeHandle == 0 ||
                expected.InstanceId == 0 || expected.Generation <= 0 || current() != expected) return new(false, "GENERAL_FONT_DONOR_STALE");
            if (!TryVisibleText(text, richText, out var visible)) return new(false, "GENERAL_FONT_MARKUP_UNSUPPORTED");
            foreach (var rune in visible.EnumerateRunes())
                if (RequiresMedium(rune.Value) && !mediumHasGlyph(rune.Value)) return new(false, "GENERAL_FONT_REQUIRED_MEDIUM_MISSING");
            if (!targetCoverage(visible)) return new(false, "GENERAL_FONT_PRIMARY_FALLBACK_MISSING");
            if (current() != expected) return new(false, "GENERAL_FONT_DONOR_STALE");
            return new(true, "GENERAL_FONT_ROLE_READY");
        }
        catch { return new(false, "GENERAL_FONT_READ_UNAVAILABLE"); }
    }
}
