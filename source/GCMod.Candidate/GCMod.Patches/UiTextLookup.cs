using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GCMod.Patches;

// Exact lookup remains first. These complete labels were observed in Native UI;
// no substring translation, hierarchy selectors, or general template parser.
internal static class UiTextLookup
{
    private static readonly Regex Wrapper = new Regex(@"^(<[^>]+>)(.+?)(</[^>]+>)$", RegexOptions.Singleline);
    private static readonly (Regex Pattern, string Key)[] Counters = {
        (new Regex(@"^第([0-9]{1,3})章$"), "第{0}章"),
        (new Regex(@"^シーズン([0-9]{1,3})$"), "シーズン{0}"),
        (new Regex(@"^現在[：:]\s*([0-9]{1,3})周目フロア([0-9]{1,3})$"), "現在：{0}周目フロア{1}"),
        (new Regex(@"^([0-9]{1,3})箱目$"), "{0}箱目"),
        (new Regex(@"^ボックス[：:]\s*([0-9]{1,3})箱目$"), "ボックス：{0}箱目"),
        (new Regex(@"^([0-9]{1,3})回[ 　]*創作完了$"), "{0}回 創作完了"),
        (new Regex(@"^([0-9]{1,3})回開封$"), "{0}回開封"),
        (new Regex(@"^あと([0-9]{1,3})日$"), "あと{0}日"),
        (new Regex(@"^あと([0-9]{1,9})$"), "あと{0}"),
        (new Regex(@"^あと([0-9]{1,3}:[0-9]{2}:[0-9]{2})$"), "あと{0}"),
        (new Regex(@"^([0-9]{1,3})ターン$"), "{0}ターン"),
        (new Regex(@"^([0-9]{1,4})日目$"), "{0}日目"),
        (new Regex(@"^([0-9]{1,6})/月$"), "{0}/月"),
    };
    private static readonly Regex ItemUnlock = new Regex(@"^([^<>\r\n]{1,80}) ([0-9]{1,5})個で解放$");
    private static readonly Regex EpisodeUnlock = new Regex(@"^([^<>\r\n]{1,120}) の閲覧が必要$");
    // The deck-copy producer inserts a user-owned team name in one color tag.
    // Preserve that entire span verbatim; never look up or translate the name.
    private static readonly Regex DeckCopy = new Regex(@"\A編成の内容を(<color=#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?>[^<>\r\n]{1,120}</color>)に上書きします\z");

    // Native supplies an already translated item/title followed by a static JP
    // suffix. Only these two complete labels are supported; leave JP names alone.
    private static bool IsKoreanLabel(string text)
    {
        bool hangul = false;
        foreach (char c in text)
        {
            if ((c >= '\u3040' && c <= '\u30ff') || (c >= '\u3400' && c <= '\u9fff')) return false;
            hangul |= c >= '\uac00' && c <= '\ud7a3';
        }
        return hangul;
    }
    // Upstream ui.json contains this date format. Deliberately does not match
    // 更新まで or 開封期限 prefixes absent from the upstream coverage baseline.
    private static readonly Regex DateUntil = new Regex(@"^([0-9]{4}/[0-9]{1,2}/[0-9]{1,2} [0-9]{2}:[0-9]{2})[ 　]*まで$");
    // Two observed Research UI producers; preserve the date verbatim. These
    // formats do not authorize translating arbitrary date prefixes or prose.
    private static readonly (Regex Pattern, string Key, string Token)[] ResetDates = {
        (new Regex(@"^リセット[：:][ 　]*([0-9]{2}/[0-9]{2} [0-9]{2}:[0-9]{2})$"),
            "リセット：{0:MM/dd HH:mm}", "{0:MM/dd HH:mm}"),
        (new Regex(@"^次回リセット[：:][ 　]*([0-9]{4}/[0-9]{2}/[0-9]{2} [0-9]{2}:[0-9]{2})$"),
            "次回リセット：{0:yyyy/MM/dd HH:mm}", "{0:yyyy/MM/dd HH:mm}"),
    };

    public static bool TryTranslate(Dictionary<string, string> dictionary, string original, out string translated, int depth = 0)
    {
        translated = null;
        if (string.IsNullOrEmpty(original)) return false;
        if (dictionary.TryGetValue(original, out translated)) return true;
        if (original.Length > 200 || depth >= 8) return false;

        // Restore upstream whitespace/outer rich-text handling, preserving both.
        string trimmed = original.Trim();
        if (trimmed.Length > 0 && trimmed.Length != original.Length && TryTranslate(dictionary, trimmed, out string inner, depth + 1))
        {
            int start = original.IndexOf(trimmed, StringComparison.Ordinal);
            translated = original.Substring(0, start) + inner + original.Substring(start + trimmed.Length);
            return true;
        }
        Match wrapper = Wrapper.Match(original);
        if (wrapper.Success && TryTranslate(dictionary, wrapper.Groups[2].Value, out string wrapped, depth + 1))
        {
            translated = wrapper.Groups[1].Value + wrapped + wrapper.Groups[3].Value;
            return true;
        }
        foreach (var rule in Counters)
        {
            Match match = rule.Pattern.Match(original);
            if (!match.Success || !dictionary.TryGetValue(rule.Key, out string format)) continue;
            translated = format;
            for (int i = 1; i < match.Groups.Count; i++) translated = translated.Replace("{" + (i - 1) + "}", match.Groups[i].Value);
            return true;
        }
        Match deck = DeckCopy.Match(original);
        if (deck.Success && dictionary.TryGetValue("編成の内容を{0}に上書きします", out string deckFormat))
        {
            translated = deckFormat.Replace("{0}", deck.Groups[1].Value);
            return true;
        }
        Match item = ItemUnlock.Match(original);
        if (item.Success && IsKoreanLabel(item.Groups[1].Value) && dictionary.TryGetValue("{0} {1}個で解放", out string itemFormat))
        {
            translated = itemFormat.Replace("{0}", item.Groups[1].Value).Replace("{1}", item.Groups[2].Value);
            return true;
        }
        Match episode = EpisodeUnlock.Match(original);
        if (episode.Success && IsKoreanLabel(episode.Groups[1].Value) && dictionary.TryGetValue("{0} の閲覧が必要", out string episodeFormat))
        {
            translated = episodeFormat.Replace("{0}", episode.Groups[1].Value);
            return true;
        }
        Match date = DateUntil.Match(original);
        if (date.Success && dictionary.TryGetValue("{0:yyyy/MM/dd HH:mm} まで", out string dateFormat))
        {
            translated = dateFormat.Replace("{0:yyyy/MM/dd HH:mm}", date.Groups[1].Value);
            return true;
        }
        foreach (var rule in ResetDates)
        {
            Match reset = rule.Pattern.Match(original);
            if (!reset.Success || !dictionary.TryGetValue(rule.Key, out string resetFormat)) continue;
            translated = resetFormat.Replace(rule.Token, reset.Groups[1].Value);
            return true;
        }
        return false;
    }
}
