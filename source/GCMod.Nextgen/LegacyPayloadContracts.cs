#nullable enable
using System.Text;
using System.Text.Json;
namespace GCMod.Nextgen;

/// <summary>Checks the existing legacy payload shapes without replacing their semantic hash or parser contracts.</summary>
public static class LegacyPayloadContracts
{
    public static void Validate(PackageFile descriptor, ReadOnlyMemory<byte> bytes)
    {
        using var document = ContractRules.Parse(bytes);
        var root = document.RootElement;
        if (descriptor.Kind is "names" or "novel")
        {
            ContractRules.Require(root.ValueKind == JsonValueKind.Object, "LEGACY_TRANSLATION_DICTIONARY_REQUIRED");
            foreach (var mapping in root.EnumerateObject())
            {
                CheckString(mapping.Name);
                ContractRules.Require(mapping.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null, "LEGACY_TRANSLATION_STRING_OR_NULL_REQUIRED");
                if (mapping.Value.ValueKind == JsonValueKind.String) CheckString(mapping.Value.GetString()!);
            }
        }
        else if (descriptor.Kind == "legacy_manifest")
        {
            ContractRules.Only(root, "hash", "master", "names", "novels");
            foreach (var field in new[] { "hash", "master", "names" })
                ContractRules.Require(IsHash(ContractRules.String(root, field, 32)), "LEGACY_MANIFEST_HASH_REQUIRED");
            ContractRules.Require(root.TryGetProperty("novels", out var novels) && novels.ValueKind == JsonValueKind.Object, "LEGACY_MANIFEST_NOVELS_OBJECT_REQUIRED");
            foreach (var row in novels.EnumerateObject())
            {
                ContractRules.Require(row.Name.Length is >= 1 and <= 16 && row.Name.All(c => c is >= '0' and <= '9'), "LEGACY_MANIFEST_NOVEL_ID");
                ContractRules.Require(row.Value.ValueKind == JsonValueKind.String && IsHash(row.Value.GetString()!), "LEGACY_MANIFEST_NOVEL_HASH_STRING_REQUIRED");
                CheckString(row.Value.GetString()!);
            }
        }
    }
    public static void ValidateAuthorityClosure(IEnumerable<PackageFile> descriptors, IReadOnlyDictionary<string, byte[]> captured)
    {
        var files=descriptors.ToArray();
        if (files.SingleOrDefault(x=>x.Kind=="legacy_manifest") is not { } manifest) return;
        ContractRules.Require(files.SingleOrDefault(x=>x.Kind=="master") is { } && files.SingleOrDefault(x=>x.Kind=="names") is { }, "LEGACY_AUTHORITY_PAYLOAD_REQUIRED");
        var master=files.Single(x=>x.Kind=="master");var names=files.Single(x=>x.Kind=="names");
        using var document=ContractRules.Parse(captured[manifest.Path]);var root=document.RootElement;
        var body=root.EnumerateObject().Where(x=>x.Name!="hash").ToDictionary(x=>x.Name,x=>x.Value.Clone(),StringComparer.Ordinal);
        ContractRules.Require(LegacySemanticHash.Compute(JsonSerializer.Serialize(body))==root.GetProperty("hash").GetString(), "LEGACY_MANIFEST_ROOT_SEMANTIC_MISMATCH");
        ContractRules.Require(LegacySemanticHash.Compute(Encoding.UTF8.GetString(captured[master.Path]))==root.GetProperty("master").GetString(), "LEGACY_MASTER_SEMANTIC_MISMATCH");
        ContractRules.Require(LegacySemanticHash.Compute(Encoding.UTF8.GetString(captured[names.Path]))==root.GetProperty("names").GetString(), "LEGACY_NAMES_SEMANTIC_MISMATCH");
        var available=files.Where(x=>x.Kind=="novel").Select(x=>x.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var row in root.GetProperty("novels").EnumerateObject())
            ContractRules.Require(available.Contains("novels/"+row.Name+".json"), "LEGACY_REGISTERED_NOVEL_DESCRIPTOR_MISSING");
    }
    static bool IsHash(string value) => value.Length==32 && value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static void CheckString(string value) => ContractRules.Require(!value.Contains('\0') && Encoding.UTF8.GetByteCount(value) <= 65536, "LEGACY_TRANSLATION_STRING_BOUND");
}
