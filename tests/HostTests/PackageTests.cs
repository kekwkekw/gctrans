using System.Text.Json;
using System.Text.Json.Nodes;
using GCMod.Nextgen;

namespace HostTests;
internal static partial class Program
{
    sealed class CountedSource : IPackageFileSource
    {
        public int Reads { get; private set; }
        readonly LocalPackageFileSource source = new();
        public async ValueTask<byte[]> ReadAsync(string root, string path, CancellationToken cancellationToken = default)
        { Reads++; return await source.ReadAsync(root, path, cancellationToken); }
    }
    static AdapterRegistry HostRegistry()
    {
        var registry = AdapterRegistry.CreateNg01();
        registry.RegisterHostAdapter("core.static-caption.v1", SelectorContract.Host(["main-menu"], ["download-label", "new-label"], supportsCoMatch: true));
        registry.RegisterHostAdapter("common.model-label.v1", SelectorContract.Host(["element-filter"], ["element-value-1-caption"], ["model"]), "Consumers");
        return registry;
    }
    static JsonObject ResourceDocument() => JsonNode.Parse("""
        {"schema_version":1,"locale":"ko","resources":[
          {"id":"menu.download","role":"menu.caption","kind":"literal","jp":"一括ダウンロード","ko":"일괄 다운로드","disposition":"translate"},
          {"id":"filter.element","role":"filter.element.label","kind":"master_ref","jp":"カーマイン","disposition":"translate","master_ref":{"table":"mTextConfig","field":"ml_value[]","exact_jp":"カーマイン"}}
        ]}
        """)!.AsObject();
    static JsonObject BindingDocument() => JsonNode.Parse("""
        {"schema_version":1,"bindings":[
          {"id":"binding.download","resource_id":"menu.download","adapter_id":"core.static-caption.v1","enabled":true,
           "source_identity":{"kind":"static-asset","identity":"asset-key:download","exact_jp":"一括ダウンロード"},
           "selector":{"scope_key":"main-menu","slot_key":"download-label","type":"static","segments":["Group","Text (TMP)"]}},
          {"id":"binding.element","resource_id":"filter.element","adapter_id":"common.model-label.v1","enabled":true,
           "source_identity":{"kind":"current-model","identity":"element-domain:value-1","exact_jp":"カーマイン"},
           "selector":{"scope_key":"element-filter","slot_key":"element-value-1-caption","type":"model","domain":1,"source_value":1,"lifetime":"owner"}},
          {"id":"binding.optional","resource_id":"menu.download","adapter_id":"future.caption.v99","enabled":true,
           "source_identity":{"kind":"static-asset","identity":"future-independent","exact_jp":"一括ダウンロード"},
           "selector":{"scope_key":"future-menu","slot_key":"future-label","type":"caption"}}
        ]}
        """)!.AsObject();
    static async Task<string> Fixture(string name, Action<JsonObject, JsonObject>? mutate = null, string? rawResources = null)
    {
        var root = Path.Combine(Scratch, name);
        Directory.CreateDirectory(Path.Combine(root, "ui"));
        var resources = ResourceDocument(); var bindings = BindingDocument(); mutate?.Invoke(resources, bindings);
        await File.WriteAllTextAsync(Path.Combine(root, "ui/resources.json"), rawResources ?? resources.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(root, "ui/bindings.json"), bindings.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(root, "master.json"), "{\"mTextConfig\":{\"ml_value[]\":{\"カーマイン\":\"카민\"}}}");
        await File.WriteAllTextAsync(Path.Combine(root, "names.json"), "{\"名前\":\"이름\"}");
        Directory.CreateDirectory(Path.Combine(root, "novels"));
        await File.WriteAllTextAsync(Path.Combine(root, "novels/999.json"), "{\"本文\":\"본문\"}");
        await RefreshManifest(root, name);
        return root;
    }
    static async Task RefreshManifest(string root, string revision)
    {
        var files = new List<object>();
        foreach (var (path, kind) in new[] { ("ui/resources.json", "resources"), ("ui/bindings.json", "bindings"), ("master.json", "master"), ("names.json", "names"), ("novels/999.json", "novel") })
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, path));
            files.Add(new { path, kind, sha256 = Hash(bytes), size = bytes.Length });
        }
        await File.WriteAllTextAsync(Path.Combine(root, "ui/manifest.json"), JsonSerializer.Serialize(new { schema_version = 1, locale = "ko", origin = "local:host-fixture", revision, files }));
    }
    static async Task TestPackageAndModules(string? candidate, string? correctedCandidate)
    {
        var provider = new CountedSource(); var registry = HostRegistry();
        var loader = new PackageLoader(provider, registry);
        var root = await Fixture("baseline");
        var snapshot = await loader.LoadAsync(root);
        Check("snapshot_exact_resource_and_master_ref", () => {
            Assert(snapshot.Resources["menu.download"].Ko == "일괄 다운로드", "KO not loaded");
            Assert(snapshot.Master.TryResolve(snapshot.Resources["filter.element"].MasterRef!, "filter.element.label", out var value) && value.Ko == "카민", "active exact MasterRef failed");
            Assert(!snapshot.Master.TryResolve(new("mTextConfig", "ml_value[]", "カーマイン "), "filter.element.label", out _), "non-exact Master source accepted");
        });
        Check("optional_unknown_adapter_only_unavailable", () => Assert(snapshot.Validation.UnavailableAdapters.SequenceEqual(["future.caption.v99"]), "independent capability isolation failed"));
        var dllPath = typeof(PackageLoader).Assembly.Location;
        var dllBefore = Hash(await File.ReadAllBytesAsync(dllPath));
        var koRoot = await Fixture("one-line-ko", (r, _) => r["resources"]![0]!["ko"] = "다운로드 전체");
        var changed = await loader.LoadAsync(koRoot, previous: snapshot);
        Check("same_DLL_one_line_KO_change_and_component_generations", () => {
            Assert(changed.Resources["menu.download"].Ko == "다운로드 전체", "corrected KO unavailable");
            Assert(changed.Generations.Ui == snapshot.Generations.Ui + 1 && changed.Generations.Master == snapshot.Generations.Master
                && changed.Generations.Names == snapshot.Generations.Names && changed.Generations.Novel == snapshot.Generations.Novel, "UI reload refreshed legacy generations");
            Assert(ReferenceEquals(changed.Master, snapshot.Master), "unchanged Master view refreshed");
            Assert(Hash(File.ReadAllBytes(dllPath)) == dllBefore, "DLL changed during JSON-only correction");
        });
        var addedRoot = await Fixture("supported-binding-add", (r, b) => {
            r["resources"]!.AsArray().Add(new JsonObject { ["id"] = "menu.new", ["role"] = "menu.caption", ["kind"] = "literal", ["jp"] = "新しい表示", ["ko"] = "새 표시", ["disposition"] = "translate" });
            var newBinding = b["bindings"]![0]!.DeepClone();
            newBinding["id"] = "binding.new"; newBinding["resource_id"] = "menu.new";
            newBinding["source_identity"]!["identity"] = "asset-key:new"; newBinding["source_identity"]!["exact_jp"] = "新しい表示";
            newBinding["selector"]!["slot_key"] = "new-label";
            b["bindings"]!.AsArray().Add(newBinding);
        });
        var added = await loader.LoadAsync(addedRoot, previous: snapshot);
        Check("same_DLL_supported_binding_JSON_add", () => {
            Assert(added.Resources["menu.new"].Ko == "새 표시" && registry.CanExecute(added.Bindings["binding.new"], true), "supported JSON binding rejected");
            Assert(Hash(File.ReadAllBytes(dllPath)) == dllBefore, "DLL changed for supported binding");
        });
        var service = new TranslationPackageService(loader);
        Assert((await service.TryActivateAsync(root)).Activated, "fixture activation failed");
        var prior = service.Active;
        await CheckAsync("unsupported_optional_selector_isolated_package_valid", async () => {
            var optionalRoot = await Fixture("optional-selector", (_, b) => b["bindings"]![0]!["selector"]!["slot_key"] = "unregistered-slot");
            var prepared = await service.PrepareAsync(optionalRoot);
            Assert(prepared.Prepared && !registry.CanExecute(prepared.Snapshot!.Bindings["binding.download"], true)
                && ReferenceEquals(service.Active, prior), "optional selector rejected package or changed selection");
        });
        await File.AppendAllTextAsync(Path.Combine(koRoot, "ui/resources.json"), " ");
        await CheckAsync("corrupted_candidate_retains_entire_LKG", async () => {
            Assert(!(await service.PrepareAsync(koRoot)).Prepared && ReferenceEquals(service.Active, prior), "corruption replaced LKG");
        });
        var invalid = new Dictionary<string, Action<JsonObject, JsonObject>> {
            ["duplicate_binding"] = (_, b) => b["bindings"]!.AsArray().Add(b["bindings"]![0]!.DeepClone()),
            ["duplicate_resource"] = (r, _) => r["resources"]!.AsArray().Add(r["resources"]![0]!.DeepClone()),
            ["dangling_resource"] = (_, b) => b["bindings"]![0]!["resource_id"] = "missing.resource",
            ["source_mismatch"] = (_, b) => b["bindings"]![0]!["source_identity"]!["exact_jp"] = "一括ダウンロード ",
            ["native_field_in_selector"] = (_, b) => b["bindings"]![0]!["selector"]!["field"] = "_nativeText",
            ["path_traversal_segment"] = (_, b) => b["bindings"]![0]!["selector"]!["segments"] = new JsonArray("..", "Text"),
            ["wildcard_segment"] = (_, b) => b["bindings"]![0]!["selector"]!["segments"] = new JsonArray("*", "Text"),
            ["damaged_tag"] = (r, _) => r["resources"]![0]!["ko"] = "<b>일괄 다운로드",
            ["damaged_unclosed_tag"] = (r, _) => r["resources"]![0]!["ko"] = "일괄 다운로드<",
            ["damaged_template"] = (r, _) => r["resources"]![0]!["ko"] = "일괄 {value} 다운로드",
            ["damaged_unclosed_template"] = (r, _) => r["resources"]![0]!["ko"] = "일괄 다운로드{",
            ["matching_malformed_source_and_translation_template"] = (r, b) => {
                r["resources"]![0]!["jp"] = "{x"; r["resources"]![0]!["ko"] = "{y";
                b["bindings"]![0]!["source_identity"]!["exact_jp"] = "{x";
                b["bindings"]![2]!["source_identity"]!["exact_jp"] = "{x";
            },
            ["matching_malformed_source_and_translation_tag"] = (r, b) => {
                r["resources"]![0]!["jp"] = "bad <color"; r["resources"]![0]!["ko"] = "나쁜 <color";
                b["bindings"]![0]!["source_identity"]!["exact_jp"] = "bad <color";
                b["bindings"]![2]!["source_identity"]!["exact_jp"] = "bad <color";
            },

            ["enabled_hold"] = (r, _) => r["resources"]![0]!["disposition"] = "hold",
            ["master_ref_with_hidden_KO"] = (r, _) => r["resources"]![1]!["ko"] = "hidden fallback",
            ["master_ref_arbitrary_expression"] = (r, _) => r["resources"]![1]!["master_ref"]!["record_expr"] = "1297 + value",
            ["ambiguous_same_target_source"] = (_, b) => { var duplicate = b["bindings"]![0]!.DeepClone(); duplicate["id"] = "different.id"; b["bindings"]!.AsArray().Add(duplicate); },
        };
        foreach (var (name, mutate) in invalid)
        {
            var invalidRoot = await Fixture("invalid-" + name, mutate);
            await CheckAsync("reject_" + name, async () => {
                Assert(!(await service.PrepareAsync(invalidRoot)).Prepared && ReferenceEquals(service.Active, prior), "invalid candidate accepted or LKG lost");
            });
        }
        foreach (var (name, raw) in new[] { ("malformed_JSON", "{bad-json"), ("duplicate_JSON_property", "{\"schema_version\":1,\"schema_version\":1,\"locale\":\"ko\",\"resources\":[]}") })
        {
            var invalidRoot = await Fixture("invalid-" + name, rawResources: raw);
            await CheckAsync("reject_" + name, async () => Assert(!(await service.PrepareAsync(invalidRoot)).Prepared && ReferenceEquals(service.Active, prior), "malformed candidate activated"));
        }
        foreach (var path in new[] { "../outside.json", "/absolute.json", "C:/absolute.json", "ui/../resources.json", "ui\\resources.json" })
        {
            var pathRoot = await Fixture("path-" + Guid.NewGuid().ToString("N"));
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(pathRoot, "ui/manifest.json")))!;
            manifest["files"]![0]!["path"] = path;
            await File.WriteAllTextAsync(Path.Combine(pathRoot, "ui/manifest.json"), manifest.ToJsonString());
            await CheckAsync("reject_manifest_path_" + path, async () => Assert(!(await service.PrepareAsync(pathRoot)).Prepared, "unsafe path prepared"));
        }
        await CheckAsync("logger_failure_does_not_block_package", async () => {
            var diagnostics = new SafeDiagnostics(4, _ => throw new IOException("fixture sink failure"));
            var failingLoggerService = new TranslationPackageService(new PackageLoader(provider, registry, diagnostics), diagnostics);
            Assert((await failingLoggerService.TryActivateAsync(root)).Activated && diagnostics.SinkFailures > 0, "logger stopped healthy package");
        });
        if (candidate != null)
        {
            await CheckAsync("full_migrated_package_load_native_disarmed", async () => {
                var full = await new PackageLoader(new LocalPackageFileSource(), AdapterRegistry.CreateNg01()).LoadAsync(candidate);
                Assert(full.Resources.Count > 200 && full.Bindings.Count > 200 && full.Validation.Valid, "full migration incomplete");
                Assert(AdapterRegistry.CreateNg01().Descriptors.All(x => !x.Available), "native capability unexpectedly armed");
            });
            await CheckAsync("actual_candidate_relocated_without_historical_custody", async () => {
                var relocated = Path.Combine(Scratch, "full-candidate-relocated");
                foreach (var file in Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(relocated, Path.GetRelativePath(candidate, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
                }
                Assert(!Directory.EnumerateFiles(relocated, "*", SearchOption.AllDirectories).Any(x => x.Contains("FrozenReviewedMaster", StringComparison.Ordinal) || x.Contains("ObserveOnly", StringComparison.Ordinal) || x.EndsWith(".jsonl", StringComparison.Ordinal)), "historical runtime evidence copied");
                var loaded = await new PackageLoader(new LocalPackageFileSource(), AdapterRegistry.CreateNg01()).LoadAsync(relocated);
                Assert(loaded.Resources.Count == JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(relocated, "ui/resources.json")))!["resources"]!.AsArray().Count, "relocated actual candidate failed");
                Assert(Hash(await File.ReadAllBytesAsync(dllPath)) == dllBefore, "relocation changed DLL");
            });
            if (correctedCandidate != null)
                await CheckAsync("full_candidate_KO_correction_after_real_manifest_builder_same_DLL", async () => {
                    var realLoader = new PackageLoader(new LocalPackageFileSource(), AdapterRegistry.CreateNg01());
                    var before = await realLoader.LoadAsync(candidate);
                    var after = await realLoader.LoadAsync(correctedCandidate, previous: before);
                    var changes = before.Resources.Keys.Where(id => before.Resources[id].Ko != after.Resources[id].Ko).ToArray();
                    Assert(changes.Length == 1 && after.Resources[changes[0]].Ko == before.Resources[changes[0]].Ko + " [검증]", "real candidate one-line KO correction not resolved");
                    Assert(after.Bindings.Count == before.Bindings.Count && File.ReadAllBytes(Path.Combine(candidate, "ui/bindings.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(correctedCandidate, "ui/bindings.json"))), "binding or activation changed");
                    Assert(after.Generations.Ui == before.Generations.Ui + 1 && after.Generations.Master == before.Generations.Master && after.Generations.Names == before.Generations.Names && after.Generations.Novel == before.Generations.Novel, "real UI correction advanced legacy generations");
                    Assert(Hash(await File.ReadAllBytesAsync(dllPath)) == dllBefore, "real candidate correction rebuilt DLL");
                });
        }
        await TestModuleBindings(snapshot, changed, added, provider, dllBefore);
    }
}
