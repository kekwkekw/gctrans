#nullable enable
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GCMod.Nextgen;

public interface IPackageFileSource
{
    ValueTask<byte[]> ReadAsync(string root, string relativePath, CancellationToken cancellationToken = default);
}
public sealed class LocalPackageFileSource : IPackageFileSource
{
    public async ValueTask<byte[]> ReadAsync(string root, string relativePath, CancellationToken cancellationToken = default)
    {
        ContractRules.SafePath(relativePath);
        var basePath = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        ContractRules.Require(path.StartsWith(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "PACKAGE_PATH_ESCAPE");
        // Do not follow a relative symlink/junction out of the selected package.
        for (var cursor = path; !string.Equals(cursor, basePath, StringComparison.OrdinalIgnoreCase); cursor = Path.GetDirectoryName(cursor)!)
        {
            ContractRules.Require(!File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint), "PACKAGE_LINK_NOT_ALLOWED");
        }
        ContractRules.Require(new FileInfo(path).Length <= 32 * 1024 * 1024, "PACKAGE_FILE_BOUND");
        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Verifies and parses the same captured bytes, with no historical receipts or compiled data hashes.</summary>
public sealed class PackageLoader(IPackageFileSource source, AdapterRegistry registry, SafeDiagnostics? diagnostics = null)
{
    public PackageLoader(AdapterRegistry registry, SafeDiagnostics? diagnostics = null) : this(new LocalPackageFileSource(), registry, diagnostics) { }
    readonly SafeDiagnostics log = diagnostics ?? new();
    public async Task<TranslationPackageSnapshot> LoadAsync(string root, string manifestPath = "ui/manifest.json",
        TranslationPackageSnapshot? previous = null, CancellationToken cancellationToken = default)
    {
        ContractRules.SafePath(manifestPath);
        ReleaseDescriptor? release = null;
        // HTTP supply always starts at release v2. Existing local-only packages may use the narrow v1 adapter,
        // or omit release metadata when importing their original UI package contract.
        if (source is LocalPackageFileSource && File.Exists(Path.Combine(root, "release-manifest.json")))
            release = ReleaseDescriptor.Parse(await source.ReadAsync(root, "release-manifest.json", cancellationToken).ConfigureAwait(false), allowLocalV1: true);
        // Capture bytes once. Hashing and parsing never reopen the file.
        var manifestBytes = (await source.ReadAsync(root, manifestPath, cancellationToken).ConfigureAwait(false)).ToArray();
        if (release != null)
        {
            ContractRules.Require(release.UiManifest.Path == manifestPath, "RELEASE_UI_PATH");
            release.VerifyUi(manifestBytes);
        }
        using var document = ContractRules.Parse(manifestBytes);
        var manifest = document.RootElement;
        ContractRules.Only(manifest, "schema_version", "locale", "origin", "revision", "files");
        ContractRules.Require(manifest.GetProperty("schema_version").GetInt32() == 1, "UNSUPPORTED_PACKAGE_SCHEMA");
        var locale = ContractRules.String(manifest, "locale", 32);
        ContractRules.Require(locale == "ko", "UNSUPPORTED_PACKAGE_LOCALE");
        var origin = ContractRules.String(manifest, "origin", 2048);
        var revision = ContractRules.String(manifest, "revision", 256);
        ContractRules.Require(origin.Length > 0 && revision.Length > 0, "MISSING_PACKAGE_IDENTITY");
        var fileNodes = manifest.GetProperty("files");
        ContractRules.Require(fileNodes.ValueKind == JsonValueKind.Array && fileNodes.GetArrayLength() is > 0 and <= 25000, "PACKAGE_FILE_COUNT_BOUND");
        var fileBuilder = ImmutableArray.CreateBuilder<PackageFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in fileNodes.EnumerateArray())
        {
            ContractRules.Only(file, "path", "kind", "sha256", "size");
            var path = ContractRules.SafePath(ContractRules.String(file, "path", 512));
            var kind = ContractRules.String(file, "kind", 32);
            var sha = ContractRules.String(file, "sha256", 64);
            ContractRules.Require(paths.Add(path) && path != manifestPath, "DUPLICATE_PACKAGE_PATH");
            ContractRules.Require(kind is "resources" or "bindings" or "layout" or "master" or "names" or "novel" or "legacy_index", "UNKNOWN_COMPONENT_KIND");
            ContractRules.Require(sha.Length == 64 && sha.All(Uri.IsHexDigit) && sha == sha.ToLowerInvariant(), "INVALID_FILE_SHA256");
            ContractRules.Require(file.TryGetProperty("size", out var sizeNode) && sizeNode.TryGetInt64(out var size) && size is >= 1 and <= 32 * 1024 * 1024, "INVALID_FILE_SIZE");
            fileBuilder.Add(new(path, kind, sha, sizeNode.GetInt64()));
        }
        var files = fileBuilder.ToImmutable();
        ContractRules.Require(files.Count(x => x.Kind == "master") <= 1 && files.Count(x => x.Kind == "names") <= 1,
            "AMBIGUOUS_AUTHORITY_FILE");
        ContractRules.Require(files.Count(x => x.Kind == "legacy_index") <= 1
            && (!files.Any(x => x.Kind == "legacy_index") || !files.Any(x => x.Kind is "names" or "novel")), "AMBIGUOUS_LEGACY_AUTHORITY_INDEX");
        ContractRules.Require(files.Any(x => x.Kind == "resources") && files.Any(x => x.Kind == "bindings"), "MISSING_UI_COMPONENT");
        var captured = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long packageBytes = 0;
        foreach (var file in files.Where(x => x.Kind != "novel"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = (await source.ReadAsync(root, file.Path, cancellationToken).ConfigureAwait(false)).ToArray();
            packageBytes += bytes.LongLength;
            ContractRules.Require(packageBytes <= 256L * 1024 * 1024, "PACKAGE_TOTAL_BOUND");
            ContractRules.Require(bytes.LongLength == file.Size && Hash(bytes) == file.Sha256, "PACKAGE_INTEGRITY_FAILED");
            captured.Add(file.Path, bytes);
        }
        var resources = new List<UiResource>();
        var bindings = new List<UiBinding>();
        foreach (var file in files.Where(x => x.Kind is "resources" or "bindings"))
        {
            using var parsed = ContractRules.Parse(captured[file.Path]);
            if (file.Kind == "resources") ParseResources(parsed.RootElement, resources);
            else ParseBindings(parsed.RootElement, bindings);
        }
        ContractRules.Require(resources.Count is > 0 and <= 20000 && bindings.Count is > 0 and <= 40000, "UI_COLLECTION_BOUND");
        ValidateRelations(resources, bindings, registry);
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        var legacyDescriptors = ImmutableArray<PackageFile>.Empty;
        if (files.SingleOrDefault(x => x.Kind == "legacy_index") is { } legacyIndex)
        {
            using var indexDocument = ContractRules.Parse(captured[legacyIndex.Path]);
            legacyDescriptors = ParseLegacyIndex(indexDocument.RootElement);
        }
        release?.VerifyClosure(files.Concat(legacyDescriptors));
        foreach (var file in legacyDescriptors.Where(x => x.Kind != "novel"))
        {
            var bytes = (await source.ReadAsync(root, file.Path, cancellationToken).ConfigureAwait(false)).ToArray();
            ContractRules.Require(bytes.LongLength == file.Size && Hash(bytes) == file.Sha256, "PACKAGE_INTEGRITY_FAILED");
            captured.Add(file.Path, bytes);
        }
        // These startup authorities must satisfy the existing typed contracts before selection.
        foreach (var file in files.Concat(legacyDescriptors).Where(x => x.Kind is "names" or "legacy_manifest"))
            LegacyPayloadContracts.Validate(file, captured[file.Path]);
        LegacyPayloadContracts.ValidateAuthorityClosure(files.Concat(legacyDescriptors),captured);
        foreach (var component in new[] { "ui", "master", "names", "novel" })
        {
            var memberFiles = files.Where(x => component == "ui" ? x.Kind is "resources" or "bindings" or "layout" : x.Kind == component);
            if (component is "names" or "novel") memberFiles = memberFiles.Concat(legacyDescriptors
                .Where(x => x.Kind == component || component == "novel" && x.Kind == "legacy_manifest"));
            signatures[component] = Hash(Encoding.UTF8.GetBytes(string.Join('\n', memberFiles
                .OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => x.Path + ":" + x.Kind + ":" + x.Sha256 + ":" + x.Size))));
        }
        long Generation(string component, long old) => previous != null && previous.Origin == origin && previous.Locale == locale
            && previous.ComponentSignatures.GetValueOrDefault(component) == signatures[component] ? old : old + 1;
        var generations = new ComponentGenerations(Generation("ui", previous?.Generations.Ui ?? 0),
            Generation("master", previous?.Generations.Master ?? 0), Generation("names", previous?.Generations.Names ?? 0),
            Generation("novel", previous?.Generations.Novel ?? 0));
        IMasterTranslationView masterView = MasterTranslationView.Empty;
        if (previous != null && generations.Master == previous.Generations.Master) masterView = previous.Master;
        else if (files.SingleOrDefault(x => x.Kind == "master") is { } masterFile)
        {
            using var masterDocument = ContractRules.Parse(captured[masterFile.Path]);
            masterView = ParseMaster(masterDocument.RootElement, generations.Master, origin + ":" + masterFile.Sha256);
        }
        // Layout is preserved and structurally validated; Fit3 native layout execution belongs to NG02.
        var layouts = new List<EpisodeTitleLayoutPolicy>();
        foreach (var layout in files.Where(x => x.Kind == "layout"))
        {
            using var parsedLayout = ContractRules.Parse(captured[layout.Path]);
            layouts.Add(ValidateLayout(parsedLayout.RootElement));
        }
        var unavailable = bindings.Where(x => x.Enabled && !registry.CanExecute(x, true)).Select(x => x.AdapterId)
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray();
        foreach (var _ in unavailable) log.Record("OPTIONAL_ADAPTER_UNAVAILABLE");
        return new(origin, revision, locale, Hash(manifestBytes), generations, resources, bindings, files,
            signatures.ToImmutableDictionary(StringComparer.Ordinal), masterView, new(true, unavailable,
                legacyDescriptors.Length > 0 ? "startup authority verified; novel lazy verified on request" : "direct-indexed-files"), previous?.Identity,
                layouts, new VerifiedPackageFiles(root, source, files.Concat(legacyDescriptors), captured));
    }
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static void ParseResources(JsonElement root, List<UiResource> output)
    {
        ContractRules.Only(root, "schema_version", "locale", "resources");
        ContractRules.Require(root.GetProperty("schema_version").GetInt32() == 1 && ContractRules.String(root, "locale", 32) == "ko", "RESOURCE_SCHEMA");
        var rows = root.GetProperty("resources");
        ContractRules.Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() is > 0 and <= 20000, "RESOURCE_COUNT_BOUND");
        foreach (var node in rows.EnumerateArray())
        {
            ContractRules.Only(node, "id", "role", "kind", "jp", "ko", "disposition", "master_ref", "markup_policy");
            var id = ContractRules.String(node, "id", 160); var role = ContractRules.String(node, "role", 160);
            var kind = ContractRules.String(node, "kind", 32); var jp = ContractRules.String(node, "jp");
            var ko = ContractRules.OptionalString(node, "ko"); var disposition = ContractRules.String(node, "disposition", 32);
            var markupPolicy = ContractRules.OptionalString(node, "markup_policy", 32) ?? "exact-tags";
            ContractRules.Require(markupPolicy is "exact-tags" or "omit-cspace-v1", "UNSUPPORTED_MARKUP_POLICY");
            ContractRules.Require(ContractRules.Id(id) && ContractRules.Id(role) && jp.Length > 0, "RESOURCE_ID_OR_SOURCE");
            ContractRules.Require(kind is "literal" or "template_label" or "master_ref", "RESOURCE_KIND");
            ContractRules.Require(disposition is "translate" or "keep_source" or "hold", "RESOURCE_DISPOSITION");
            MasterRef? reference = null;
            if (kind == "master_ref")
            {
                ContractRules.Require(markupPolicy == "exact-tags", "MASTER_REF_MARKUP_POLICY");
                ContractRules.Require(ko == null && node.TryGetProperty("master_ref", out _), "MASTER_REF_KO_FORBIDDEN");
                var r = node.GetProperty("master_ref"); ContractRules.Only(r, "table", "field", "exact_jp");
                var table = ContractRules.String(r, "table", 128); var field = ContractRules.String(r, "field", 128);
                var exactJp = ContractRules.String(r, "exact_jp");
                ContractRules.Require(ContractRules.Table(table) && ContractRules.Field(field) && exactJp == jp, "MASTER_REF_CONTRACT");
                reference = new(table, field, exactJp);
            }
            else
            {
                ContractRules.Require(ko != null && !node.TryGetProperty("master_ref", out _), "LITERAL_KO_REQUIRED");
                ContractRules.Require(disposition != "translate" || ko!.Length > 0, "EMPTY_TRANSLATION");
                ContractRules.Require(disposition != "keep_source" || jp == ko, "KEEP_SOURCE_CHANGED");
                ContractRules.Require(ContractRules.MarkupEquivalent(jp, ko!, markupPolicy), "TAG_OR_TEMPLATE_DAMAGE");
            }
            output.Add(new(id, role, kind, jp, ko, disposition, reference, markupPolicy));
        }
    }
    static void ParseBindings(JsonElement root, List<UiBinding> output)
    {
        ContractRules.Only(root, "schema_version", "bindings");
        ContractRules.Require(root.GetProperty("schema_version").GetInt32() == 1, "BINDING_SCHEMA");
        var rows = root.GetProperty("bindings");
        ContractRules.Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() is > 0 and <= 40000, "BINDING_COUNT_BOUND");
        foreach (var node in rows.EnumerateArray())
        {
            ContractRules.Only(node, "id", "resource_id", "adapter_id", "enabled", "source_identity", "selector", "action_group", "producer");
            var id = ContractRules.String(node, "id", 160); var resourceId = ContractRules.String(node, "resource_id", 160);
            var adapter = ContractRules.String(node, "adapter_id", 128); var enabled = ContractRules.Bool(node, "enabled");
            var group = ContractRules.OptionalString(node, "action_group", 160);
            ContractRules.Require(ContractRules.Id(id) && ContractRules.Id(resourceId) && ContractRules.AdapterId(adapter) && (group == null || ContractRules.Id(group)), "BINDING_IDENTIFIER");
            var source = node.GetProperty("source_identity"); ContractRules.Only(source, "kind", "identity", "exact_jp");
            var kind = ContractRules.String(source, "kind", 32); var jp = ContractRules.String(source, "exact_jp");
            ContractRules.Require(kind is "static-asset" or "current-model" or "choice-group" or "native-template", "SOURCE_KIND");
            var identity = source.GetProperty("identity"); ValidateIdentity(identity);
            var selector = ParseSelector(node.GetProperty("selector"));
            ContractRules.Require(jp.Length > 0, "EMPTY_SOURCE");
            ContractRules.Require(kind == "choice-group" ? selector.GroupId != null && selector.ScriptId != null && selector.BranchLabel != null
                : selector.GroupId == null && selector.ScriptId == null && selector.BranchLabel == null, "CHOICE_SELECTOR_CONTRACT");
            ContractRules.Require(selector.SourceId == null || selector.Lifetime == "callback", "SORT_SOURCE_REQUIRES_CALLBACK");
            if (group != null) ContractRules.Require(registrySupportsCoMatch(adapter), "COMATCH_ADAPTER_CONTRACT");
            var producer = node.TryGetProperty("producer", out var producerNode)
                ? CoreProducerPolicy.Parse(producerNode, adapter, kind, selector.ScopeKey) : null;
            output.Add(new(id, resourceId, adapter, enabled, new(kind, CanonicalIdentity(identity), jp), selector, group, producer));
        }
        static bool registrySupportsCoMatch(string adapter) => adapter is "ui4.static-caption.v1" or "core.static-caption.v1";
    }
    static void ValidateIdentity(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String) { ContractRules.Require(node.GetString()!.Length is > 0 and <= 2048 && !node.GetString()!.Contains('\0'), "SOURCE_IDENTITY_STRING"); return; }
        ContractRules.Require(node.ValueKind == JsonValueKind.Array && node.GetArrayLength() is > 0 and <= 16, "SOURCE_IDENTITY_ARRAY");
        foreach (var x in node.EnumerateArray()) ContractRules.Require(x.ValueKind == JsonValueKind.Null
            || x.ValueKind == JsonValueKind.String && x.GetString()!.Length <= 2048 && !x.GetString()!.Contains('\0')
            || x.ValueKind == JsonValueKind.Number && x.TryGetInt64(out var n) && n is >= -9007199254740991 and <= 9007199254740991, "SOURCE_IDENTITY_SCALAR");
    }
    public static string CanonicalIdentity(JsonElement element) => JsonSerializer.Serialize(element);
    static UiSelector ParseSelector(JsonElement node)
    {
        ContractRules.Only(node, "scope_key", "slot_key", "segments", "group_id", "script_id", "branch_label", "type", "domain", "source_value", "source_id", "selected", "template_id", "lifetime", "overflow_policy");
        var scope = ContractRules.String(node, "scope_key", 128); var slot = ContractRules.String(node, "slot_key", 128);
        ContractRules.Require(ContractRules.Scope(scope) && ContractRules.Scope(slot), "SELECTOR_SCOPE_SLOT");
        var segments = ImmutableArray<string>.Empty;
        if (node.TryGetProperty("segments", out var array))
        {
            ContractRules.Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() is > 0 and <= 32, "SELECTOR_SEGMENT_BOUND");
            segments = array.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : "").ToImmutableArray();
            ContractRules.Require(segments.All(x => x.Length is > 0 and <= 128 && x is not "." and not ".." && x.IndexOfAny(['/', '\\', '*', '?', '\0']) < 0 && !x.Any(char.IsControl)), "UNSAFE_SELECTOR_SEGMENT");
        }
        var group = ContractRules.OptionalString(node, "group_id", 160); var script = ContractRules.OptionalString(node, "script_id", 64);
        var branch = ContractRules.OptionalString(node, "branch_label", 128); var type = ContractRules.OptionalString(node, "type", 32);
        var template = ContractRules.OptionalString(node, "template_id", 128); var lifetime = ContractRules.OptionalString(node, "lifetime", 32);
        var overflow = ContractRules.OptionalString(node, "overflow_policy", 32);
        ContractRules.Require((group == null || ContractRules.Id(group)) && (script == null || ContractRules.ChoicePart(script)) && (branch == null || ContractRules.ChoicePart(branch)) && (template == null || ContractRules.Scope(template)), "SELECTOR_ID");
        ContractRules.Require(type == null || type is "static" or "model" or "control" or "date" or "profile" or "exchange" or "caption", "SELECTOR_TYPE");
        ContractRules.Require(lifetime == null || lifetime is "callback" or "owner" or "cell", "SELECTOR_LIFETIME");
        ContractRules.Require(overflow == null || overflow == "intrinsic-only", "SELECTOR_OVERFLOW_POLICY");
        var selected = node.TryGetProperty("selected", out _) ? ContractRules.Bool(node, "selected") : (bool?)null;
        return new(scope, slot, segments, group, script, branch, type, ContractRules.OptionalInt(node, "domain"),
            ContractRules.OptionalInt(node, "source_value"), ContractRules.OptionalInt(node, "source_id"), selected, template, lifetime, overflow);
    }
    static void ValidateRelations(List<UiResource> resources, List<UiBinding> bindings, AdapterRegistry registry)
    {
        ContractRules.Require(resources.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == resources.Count, "DUPLICATE_RESOURCE_ID");
        ContractRules.Require(bindings.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == bindings.Count, "DUPLICATE_BINDING_ID");
        var resourceMap = resources.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            ContractRules.Require(resourceMap.TryGetValue(binding.ResourceId, out var resource), "DANGLING_RESOURCE");
            ContractRules.Require(binding.SourceIdentity.ExactJp == resource!.Jp, "EXACT_SOURCE_MISMATCH");
            ContractRules.Require(!binding.Enabled || resource.Disposition == "translate", "ENABLED_HOLD_OR_KEEP_SOURCE");
            ContractRules.Require((binding.SourceIdentity.Kind == "native-template") == (resource.Kind == "template_label"), "TEMPLATE_KIND_MISMATCH");
            if (resource.MarkupPolicy == "omit-cspace-v1") ContractRules.Require(binding.AdapterId == "ui.caption.v1"
                && binding.Selector.Type == "caption" && resource.Role is "character_palette" or "character_imaju_label" or "character_memory", "MARKUP_POLICY_ROLE_NOT_SUPPORTED");
            // Structural syntax is already checked. Optional capability mismatch isolates the binding.
        }
        foreach (var group in bindings.Where(x => x.ActionGroup != null).GroupBy(x => x.ActionGroup, StringComparer.Ordinal))
        {
            ContractRules.Require(group.Count() is >= 2 and <= 16 && group.All(x => x.SourceIdentity.Kind == "static-asset")
                && group.Select(x => x.SourceIdentity.IdentityJson).Distinct(StringComparer.Ordinal).Count() == group.Count(), "COMATCH_MEMBER_CONTRACT");
            var a = group.First(); var resource = resourceMap[a.ResourceId];
            foreach (var b in group)
            {
                var r = resourceMap[b.ResourceId];
                ContractRules.Require(a.AdapterId == b.AdapterId && a.Enabled == b.Enabled && SelectorEquals(a.Selector, b.Selector)
                    && CoreProducerPolicy.SameContract(a.Producer, b.Producer)
                    && resource.Role == r.Role && resource.Jp == r.Jp && resource.Ko == r.Ko && resource.Kind == r.Kind
                    && resource.Disposition == r.Disposition && resource.MasterRef == r.MasterRef && resource.MarkupPolicy == r.MarkupPolicy, "CONFLICTING_COMATCH");
            }
        }
        // Exact same selector/source without explicit co-match is ambiguous even if the text happens to match.
        foreach (var group in bindings.Where(x => x.Enabled).GroupBy(x => x.AdapterId + "|" + JsonSerializer.Serialize(x.Selector) + "|" + x.SourceIdentity.ExactJp))
            ContractRules.Require(group.Count() == 1 || group.All(x => x.ActionGroup != null && x.ActionGroup == group.First().ActionGroup), "AMBIGUOUS_DUPLICATE_BINDING");
    }
    public static bool SelectorEquals(UiSelector a, UiSelector b) => a with { Segments = ImmutableArray<string>.Empty } == b with { Segments = ImmutableArray<string>.Empty } && a.Segments.SequenceEqual(b.Segments, StringComparer.Ordinal);
    static IMasterTranslationView ParseMaster(JsonElement root, long generation, string provenance)
    {
        ContractRules.Require(root.ValueKind == JsonValueKind.Object, "MASTER_OBJECT");
        var entries = new Dictionary<(string, string, string), string>();
        foreach (var table in root.EnumerateObject())
        {
            ContractRules.Require(ContractRules.Table(table.Name) && table.Value.ValueKind == JsonValueKind.Object, "MASTER_TABLE");
            foreach (var field in table.Value.EnumerateObject())
            {
                ContractRules.Require(ContractRules.LegacyField(field.Name) && field.Value.ValueKind == JsonValueKind.Object, "MASTER_FIELD");
                foreach (var mapping in field.Value.EnumerateObject())
                {
                    ContractRules.Require(mapping.Value.ValueKind == JsonValueKind.String, "MASTER_VALUE");
                    entries.Add((table.Name, field.Name, mapping.Name), mapping.Value.GetString()!);
                }
            }
        }
        return new MasterTranslationView(entries, generation, provenance);
    }
    static ImmutableArray<PackageFile> ParseLegacyIndex(JsonElement root)
    {
        ContractRules.Only(root, "schema_version", "files");
        ContractRules.Require(root.GetProperty("schema_version").GetInt32() == 1, "LEGACY_INDEX_SCHEMA");
        var files = root.GetProperty("files");
        ContractRules.Require(files.ValueKind == JsonValueKind.Array && files.GetArrayLength() is >= 2 and <= 25000, "LEGACY_INDEX_BOUND");
        var result = ImmutableArray.CreateBuilder<PackageFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.EnumerateArray())
        {
            ContractRules.Only(file, "path", "kind", "sha256", "size");
            var path = ContractRules.SafePath(ContractRules.String(file, "path", 512));
            var kind = ContractRules.String(file, "kind", 32);
            bool valid = kind == "names" ? path == "names.json" : kind == "legacy_manifest" ? path == "manifest.json"
                : kind == "novel" && path.StartsWith("novels/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal)
                    && path[7..^5].Length is >= 1 and <= 16 && path[7..^5].All(c => c is >= '0' and <= '9');
            ContractRules.Require(valid && paths.Add(path), "LEGACY_INDEX_PATH_KIND");
            var hash = ContractRules.String(file, "sha256", 64);
            ContractRules.Require(hash.Length == 64 && hash.All(Uri.IsHexDigit) && hash == hash.ToLowerInvariant(), "LEGACY_INDEX_HASH");
            ContractRules.Require(file.GetProperty("size").TryGetInt64(out var size) && size is >= 1 and <= 32 * 1024 * 1024, "LEGACY_INDEX_SIZE");
            result.Add(new(path, kind, hash, size));
        }
        ContractRules.Require(result.Count(x => x.Kind == "names") == 1 && result.Count(x => x.Kind == "legacy_manifest") == 1, "LEGACY_INDEX_AUTHORITY_MEMBERS");
        return result.ToImmutable();
    }
    static EpisodeTitleLayoutPolicy ValidateLayout(JsonElement root)
    {
        // NG02 metadata policy is bounded here; no executor or arbitrary nested policy language is supplied.
        ContractRules.Only(root, "schema_version", "policy_id", "font_profile", "base_size", "descending_sizes", "restore", "overflow_failure", "scope_key");
        ContractRules.Require(root.GetProperty("schema_version").GetInt32() == 1
            && ContractRules.String(root, "policy_id", 160) == "episode-title.fit.v1"
            && ContractRules.String(root, "font_profile", 128) == FontService.ApprovedProfile
            && root.GetProperty("base_size").GetInt32() == 35
            && ContractRules.String(root, "restore", 32) == "owned-only"
            && ContractRules.String(root, "overflow_failure", 32) == "restore-base"
            && ContractRules.String(root, "scope_key", 128) == "episode-list-title", "UNSUPPORTED_LAYOUT_POLICY");
        var sizes = root.GetProperty("descending_sizes");
        ContractRules.Require(sizes.ValueKind == JsonValueKind.Array && sizes.GetArrayLength() == 4
            && sizes.EnumerateArray().Select(x => x.GetInt32()).SequenceEqual([34, 33, 32, 31]), "UNSUPPORTED_LAYOUT_GRID");
        return new("episode-title.fit.v1", FontService.ApprovedProfile, 35,
            sizes.EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray(), "owned-only", "restore-base", "episode-list-title");
    }
}

public sealed record ActivationResult(bool Activated, string Reason, TranslationPackageSnapshot? Snapshot);
public sealed record PackagePreparationResult(bool Prepared, string Reason, TranslationPackageSnapshot? Snapshot);
public sealed class TranslationPackageService(PackageLoader loader, SafeDiagnostics? diagnostics = null)
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly SafeDiagnostics log = diagnostics ?? new();
    TranslationPackageSnapshot? active;
    public TranslationPackageSnapshot? Active => Volatile.Read(ref active);
    public async Task<PackagePreparationResult> PrepareAsync(string root, string manifestPath = "ui/manifest.json",
        CancellationToken cancellationToken = default)
    {
        try { return new(true, "VALIDATED_CANDIDATE_NOT_SELECTED", await loader.LoadAsync(root, manifestPath, Active, cancellationToken).ConfigureAwait(false)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is PackageValidationException or JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or OverflowException)
        { log.Record("CANDIDATE_REJECTED_LKG_RETAINED"); return new(false, "CANDIDATE_VALIDATION_FAILED", null); }
    }
    public async Task<ActivationResult> TryActivateAsync(string root, string manifestPath = "ui/manifest.json",
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Compatibility API for pre-writer startup only. Live selection belongs to RuntimeCoordinator.CommitUi.
            if (Active != null) return new(false, "LIVE_ACTIVATION_REQUIRES_RUNTIME_COMMIT", Active);
            TranslationPackageSnapshot candidate;
            try { candidate = await loader.LoadAsync(root, manifestPath, Active, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is PackageValidationException or JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or OverflowException)
            { log.Record("CANDIDATE_REJECTED_LKG_RETAINED"); return new(false, "CANDIDATE_VALIDATION_FAILED", Active); }
            Volatile.Write(ref active, candidate);
            log.Record("PACKAGE_ACTIVATED");
            return new(true, "VALIDATED_IMMUTABLE_SNAPSHOT", candidate);
        }
        finally { gate.Release(); }
    }
}
