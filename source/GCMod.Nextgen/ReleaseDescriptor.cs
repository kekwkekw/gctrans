#nullable enable
using System.Collections.Immutable;
using System.Text.Json;

namespace GCMod.Nextgen;

/// <summary>Release is transport metadata. Logical authority remains the origin inside the UI manifest.</summary>
public sealed record ReleaseDescriptor(int SchemaVersion, PackageFile UiManifest, ImmutableArray<PackageFile> Files)
{
    public static ReleaseDescriptor Parse(byte[] bytes, bool allowLocalV1 = false)
    {
        using var document = ContractRules.Parse(bytes);
        var root = document.RootElement;
        ContractRules.Only(root, "schema_version", "locale", "profile", "revision", "status", "ui_manifest", "files",
            "legacy_index_validation_scope", "legacy_manifest_builder_input");
        var schema = root.GetProperty("schema_version").GetInt32();
        ContractRules.Require(schema == 2 || schema == 1 && allowLocalV1, "RELEASE_SCHEMA_UNSUPPORTED");
        ContractRules.Require(ContractRules.String(root, "locale", 32) == "ko", "RELEASE_LOCALE_UNSUPPORTED");
        PackageFile ui;
        if (schema == 1)
        {
            // Existing local imports have no release-to-UI hash. This adapter is never used by HTTP supply.
            ContractRules.Require(ContractRules.String(root, "ui_manifest", 512) == "ui/manifest.json", "RELEASE_UI_PATH");
            ui = new("ui/manifest.json", "ui_manifest", "", 0);
        }
        else
        {
            var node = root.GetProperty("ui_manifest");
            ContractRules.Only(node, "path", "size", "sha256");
            ui = Descriptor(node, "ui_manifest");
            ContractRules.Require(ui.Path == "ui/manifest.json", "RELEASE_UI_PATH");
        }
        var array = root.GetProperty("files");
        ContractRules.Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() is > 0 and <= 25000, "RELEASE_FILE_COUNT");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = ImmutableArray.CreateBuilder<PackageFile>();
        long total = ui.Size;
        foreach (var node in array.EnumerateArray())
        {
            ContractRules.Only(node, "path", "size", "sha256", "kind");
            var file = Descriptor(node, ContractRules.String(node, "kind", 32));
            ContractRules.Require(paths.Add(file.Path), "RELEASE_DUPLICATE_PATH");
            bool supported = file.Kind switch
            {
                "master" => file.Path == "master.json",
                "names" => file.Path == "names.json",
                "legacy_manifest" => file.Path == "manifest.json",
                "legacy_index" => file.Path == "legacy-components.json",
                "resources" => file.Path == "ui/resources.json",
                "bindings" => file.Path == "ui/bindings.json",
                "layout" => file.Path == "layout/episode-title.json",
                "novel" => file.Path.StartsWith("novels/", StringComparison.Ordinal) && file.Path.EndsWith(".json", StringComparison.Ordinal)
                    && file.Path[7..^5].Length is > 0 and <= 16 && file.Path[7..^5].All(c => c is >= '0' and <= '9'),
                _ => false
            };
            ContractRules.Require(supported, "RELEASE_DATA_PATH_KIND_UNSUPPORTED");
            total += file.Size;
            ContractRules.Require(total <= 256L * 1024 * 1024, "RELEASE_TOTAL_BOUND");
            files.Add(file);
        }
        return new(schema, ui, files.ToImmutable());
    }
    static PackageFile Descriptor(JsonElement node, string kind)
    {
        var path = ContractRules.SafePath(ContractRules.String(node, "path", 512));
        var hash = ContractRules.String(node, "sha256", 64);
        ContractRules.Require(hash.Length == 64 && hash.All(Uri.IsHexDigit) && hash == hash.ToLowerInvariant(), "RELEASE_HASH");
        ContractRules.Require(node.GetProperty("size").TryGetInt64(out var size) && size is >= 1 and <= 32 * 1024 * 1024, "RELEASE_SIZE");
        return new(path, kind, hash, size);
    }
    public void VerifyUi(byte[] bytes)
    {
        if (SchemaVersion == 2)
            ContractRules.Require(bytes.LongLength == UiManifest.Size && PackageLoader.Hash(bytes) == UiManifest.Sha256, "RELEASE_UI_INTEGRITY");
    }
    public void VerifyClosure(IEnumerable<PackageFile> descriptors)
    {
        var closure = descriptors.ToDictionary(x => x.Path, StringComparer.Ordinal);
        ContractRules.Require(closure.Count == Files.Length && Files.All(x => closure.TryGetValue(x.Path, out var member) && member == x), "RELEASE_DESCRIPTOR_CLOSURE");
    }
}
