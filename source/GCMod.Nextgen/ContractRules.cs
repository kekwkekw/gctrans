#nullable enable
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GCMod.Nextgen;

internal static partial class ContractRules
{
    static readonly Regex idRegex = new("^[A-Za-z0-9][A-Za-z0-9._:/-]{0,159}$", RegexOptions.CultureInvariant);
    static readonly Regex adapterRegex = new("^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
    static readonly Regex scopeRegex = new("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant);
    static readonly Regex choiceRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);
    static readonly Regex tableRegex = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant);
    static readonly Regex fieldRegex = new("^[A-Za-z_][A-Za-z0-9_]*(?:\\[\\])?$", RegexOptions.CultureInvariant);
    static readonly Regex legacyFieldRegex = new("^[A-Za-z_][A-Za-z0-9_]*(?:\\[\\]|\\|)?$", RegexOptions.CultureInvariant);
    static readonly Regex markupRegex = new("<[^<>]*>|\\{[^{}]*\\}", RegexOptions.CultureInvariant);
    static readonly Regex cspaceRegex = new("<cspace=[^<>]+>", RegexOptions.CultureInvariant);
    static readonly Regex tagRegex = new("<(/?)([A-Za-z][A-Za-z0-9]*)([^<>]*)>", RegexOptions.CultureInvariant);
    static readonly Regex newlineRegex = new("\\r\\n|\\r|\\n", RegexOptions.CultureInvariant);
    static Regex IdPattern() => idRegex;
    static Regex AdapterPattern() => adapterRegex;
    static Regex ScopePattern() => scopeRegex;
    static Regex ChoicePartPattern() => choiceRegex;
    static Regex TablePattern() => tableRegex;
    static Regex FieldPattern() => fieldRegex;
    static Regex LegacyFieldPattern() => legacyFieldRegex;
    static Regex MarkupPattern() => markupRegex;
    static Regex CspacePattern() => cspaceRegex;
    static Regex TagPattern() => tagRegex;
    static Regex NewlinePattern() => newlineRegex;
    public static bool Id(string value) => IdPattern().IsMatch(value);
    public static bool AdapterId(string value) => value.Length <= 128 && AdapterPattern().IsMatch(value);
    public static bool Scope(string value) => ScopePattern().IsMatch(value);
    public static bool ChoicePart(string value) => ChoicePartPattern().IsMatch(value);
    public static bool Table(string value) => TablePattern().IsMatch(value);
    public static bool Field(string value) => value.Length <= 128 && FieldPattern().IsMatch(value);
    public static bool LegacyField(string value) => value.Length <= 128 && LegacyFieldPattern().IsMatch(value);
    public static void Require(bool condition, string reason) { if (!condition) throw new PackageValidationException(reason); }
    public static string String(JsonElement node, string name, int max = 16384)
    {
        Require(node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String, "MISSING_STRING_" + name);
        var result = value.GetString()!;
        Require(result.Length <= max && !result.Contains('\0') && Encoding.UTF8.GetByteCount(result) <= 65536, "STRING_BOUND_" + name);
        return result;
    }
    public static string? OptionalString(JsonElement node, string name, int max = 16384) => node.TryGetProperty(name, out _) ? String(node, name, max) : null;
    public static int? OptionalInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return null;
        Require(value.TryGetInt32(out var result) && result is >= -1 and <= 65535, "INTEGER_BOUND_" + name);
        return result;
    }
    public static bool Bool(JsonElement node, string name)
    {
        Require(node.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False, "MISSING_BOOL_" + name);
        return value.GetBoolean();
    }
    public static void Only(JsonElement value, params string[] names)
    {
        Require(value.ValueKind == JsonValueKind.Object, "OBJECT_REQUIRED");
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject()) Require(allowed.Contains(field.Name), "UNKNOWN_PROPERTY_" + field.Name);
    }
    public static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        Require(bytes.Length <= 8 * 1024 * 1024, "JSON_FILE_BOUND");
        JsonDocument document;
        try { document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }); }
        catch (JsonException) { throw new PackageValidationException("MALFORMED_JSON"); }
        try { CheckDuplicates(document.RootElement); } catch { document.Dispose(); throw; }
        return document;
    }
    static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) { Require(names.Add(property.Name), "DUPLICATE_JSON_PROPERTY"); CheckDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) CheckDuplicates(child);
    }
    public static bool MarkupEquivalent(string jp, string ko, string policy = "exact-tags")
    {
        static bool Balanced(string text)
        {
            var stack = new Stack<string>();
            foreach (Match token in TagPattern().Matches(text))
            {
                string name = token.Groups[2].Value;
                bool closing = token.Groups[1].Value == "/";
                if (name.ToLowerInvariant() is "br" or "sprite" or "space" or "page" or "pos" or "voffset" or "alpha")
                { if (closing) return false; continue; }
                if (closing) { if (stack.Count == 0 || stack.Pop() != name) return false; }
                else if (!token.Groups[3].Value.EndsWith("/", StringComparison.Ordinal)) stack.Push(name);
            }
            return stack.Count == 0;
        }
        if (!Balanced(jp) || !Balanced(ko)) return false;
        var jpPlain = TagPattern().Replace(jp, "");
        var koPlain = TagPattern().Replace(ko, "");
        if (!NewlinePattern().Matches(jpPlain).Select(x => x.Value).SequenceEqual(NewlinePattern().Matches(koPlain).Select(x => x.Value), StringComparer.Ordinal)) return false;
        if (policy == "omit-cspace-v1")
        {
            int depth = 0;
            foreach (Match token in MarkupPattern().Matches(jp))
            {
                if (CspacePattern().IsMatch(token.Value)) depth++;
                else if (token.Value == "</cspace>" && --depth < 0) return false;
            }
            if (depth != 0 || !CspacePattern().IsMatch(jp) || CspacePattern().IsMatch(ko) || ko.Contains("</cspace>", StringComparison.Ordinal)) return false;
            jp = CspacePattern().Replace(jp, "").Replace("</cspace>", "", StringComparison.Ordinal);
        }
        var sourceTokens = MarkupPattern().Matches(jp);
        var translatedTokens = MarkupPattern().Matches(ko);
        if (!sourceTokens.Select(x => x.Value).SequenceEqual(translatedTokens.Select(x => x.Value), StringComparer.Ordinal)) return false;
        // No accepted NG01 source requires a literal unmatched delimiter. Incomplete markup/templates are rejected,
        // even when source and translation repeat the same damage; future literal delimiter roles need an explicit contract.
        static string Remainder(string text) => new(MarkupPattern().Replace(text, "").Where(c => c is '<' or '>' or '{' or '}').ToArray());
        return Remainder(jp).Length == 0 && Remainder(ko).Length == 0;
    }
    public static string SafePath(string path)
    {
        Require(path.Length is > 0 and <= 512 && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Contains(':')
            && path.Split('/').All(x => x.Length > 0 && x is not "." and not ".." && x.IndexOfAny(['*', '?', '\0']) < 0 && !x.Any(char.IsControl)), "UNSAFE_PACKAGE_PATH");
        return path;
    }
}
