#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace GCMod.Nextgen;

/// <summary>The existing TranslationHash algorithm, shared by startup authority checks and the legacy facade.</summary>
public static class LegacySemanticHash
{
    static readonly IComparer<string> KeyComparer = Comparer<string>.Create(CompareKeys);
    public static string Compute(string json)
    {
        using var document = JsonDocument.Parse(json);
        var content = new StringBuilder();
        Append(document.RootElement, "", content);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content.ToString()))).ToLowerInvariant();
    }
    static void Append(JsonElement table, string prefix, StringBuilder content)
    {
        foreach (var property in table.EnumerateObject().OrderBy(property => property.Name, KeyComparer))
        {
            string path = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object) Append(property.Value, path + '\x01', content);
            else content.Append(path).Append('\0').Append(property.Value.GetString()).Append('\0');
        }
    }
    // Python uses Unicode code points, while UTF-16 ordinal orders supplementary characters differently.
    static int CompareKeys(string left, string right)
    {
        var first = left.EnumerateRunes().GetEnumerator();
        var second = right.EnumerateRunes().GetEnumerator();
        while (first.MoveNext())
        {
            if (!second.MoveNext()) return 1;
            int order = first.Current.Value.CompareTo(second.Current.Value);
            if (order != 0) return order;
        }
        return second.MoveNext() ? -1 : 0;
    }
}
