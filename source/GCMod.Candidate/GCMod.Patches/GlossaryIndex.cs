using System;
using System.Collections.Generic;
using System.Linq;

namespace GCMod.Patches;

// Display-only glossary ordering. Indices address the returned list, not IDs.
internal static class GlossaryIndex
{
    public static bool HasHangul(string text) =>
        text != null && text.Any(c => c >= '\uac00' && c <= '\ud7a3');

    public static string Label(string text)
    {
        foreach (char c in text ?? "")
        {
            if (c >= '\uac00' && c <= '\ud7a3')
                return "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ"[(c - '\uac00') / 588].ToString();
            if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
        }
        return "#";
    }

    public static IEnumerable<T> Sort<T>(IEnumerable<T> rows, Func<T, string> title) =>
        rows.OrderBy(r => Label(title(r)), StringComparer.Ordinal)
            .ThenBy(title, StringComparer.Ordinal);

    public static IEnumerable<(string Label, int Position)> Sections(IEnumerable<string> titles)
    {
        string previous = null;
        int i = 0;
        foreach (string title in titles)
        {
            string label = Label(title);
            if (label != previous) yield return (label, i);
            previous = label;
            i++;
        }
    }
}
