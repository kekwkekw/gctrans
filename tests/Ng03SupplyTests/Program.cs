using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GCMod.Nextgen;
using GCMod.Services;

if (args.Length > 0 && args[0] == "--actual-branch")
{
    if (args.Length != 4) throw new ArgumentException("--actual-branch ISOLATED_STORE EXPECTED_COMMIT REPORT");
    await ActualBranchProbe.RunAsync(args[1], args[2], args[3]);
    return;
}
var run = new SupplyTests(args[0], args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null);
await run.RunAsync();

sealed class SupplyTests(string report, string? exportRoot, string? changedRoot)
{
    readonly string scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "scratch-supply-" + Guid.NewGuid().ToString("N"));
    readonly List<object> results = [];
    readonly Dictionary<string,UpdateMetrics> costs = new(StringComparer.Ordinal);
    int failures;
    static readonly string Commit = new('c', 40);
    static readonly UpdateSource Source = new("owner/repo", "main", ApiBase: new Uri("https://api.test/"), ContentBase: new Uri("https://raw.test/"));
    static void Assert(bool yes, string reason) { if (!yes) throw new Exception(reason); }
    static AdapterRegistry Registry()
    {
        var registry = AdapterRegistry.CreateNg01();
        registry.RegisterHostAdapter("core.static-caption.v1", SelectorContract.Host(["main-menu"], ["download-label", "new-label"]));
        return registry;
    }
    async Task Check(string name, Func<Task> callback)
    {
        try { await callback(); results.Add(new { name, status = "PASS" }); }
        catch (Exception ex) { failures++; results.Add(new { name, status = "FAIL", error = ex.ToString() }); Console.WriteLine(name + ": " + ex.Message); }
    }
    Dictionary<string, byte[]> Package(string ko = "다운로드", bool extraBinding = false, string adapter = "core.static-caption.v1")
    {
        var files = new Dictionary<string, (string Kind, byte[] Bytes)>();
        byte[] Json(object node) => JsonSerializer.SerializeToUtf8Bytes(node);
        object resource(string id, string jp, string value) => new { id, role = "download", kind = "literal", jp, ko = value, disposition = "translate" };
        object binding(string id, string resourceId, string jp, string slot) => new { id, resource_id = resourceId, adapter_id = adapter, enabled = true,
            source_identity = new { kind = "static-asset", identity = "asset-key:" + slot, exact_jp = jp },
            selector = new { scope_key = "main-menu", slot_key = slot, type = "static" } };
        files["ui/resources.json"] = ("resources", Json(new { schema_version = 1, locale = "ko", resources = extraBinding
            ? new[] { resource("fixture.resource", "一括ダウンロード", ko), resource("fixture.new", "新規", "신규") }
            : new[] { resource("fixture.resource", "一括ダウンロード", ko) } }));
        files["ui/bindings.json"] = ("bindings", Json(new { schema_version = 1, bindings = extraBinding
            ? new[] { binding("fixture.binding", "fixture.resource", "一括ダウンロード", "download-label"), binding("fixture.new.binding", "fixture.new", "新規", "new-label") }
            : new[] { binding("fixture.binding", "fixture.resource", "一括ダウンロード", "download-label") } }));
        files["master.json"] = ("master", Encoding.UTF8.GetBytes("{\"mEpisode\":{\"title\":{\"題名\":\"제목\"}}}"));
        files["names.json"] = ("names", Encoding.UTF8.GetBytes("{\"人物\":\"인물\"}"));
        files["novels/43.json"] = ("novel", Encoding.UTF8.GetBytes("{\"セリフ\":\"대사\"}"));
        string Semantic(string path) => TranslationHash.Compute(Encoding.UTF8.GetString(files[path].Bytes));
        var manifest = new Dictionary<string,object> { ["master"] = Semantic("master.json"), ["names"] = Semantic("names.json"),
            ["novels"] = new Dictionary<string,string> { ["43"] = Semantic("novels/43.json") } };
        manifest["hash"] = TranslationHash.Compute(JsonSerializer.Serialize(manifest));
        files["manifest.json"] = ("legacy_manifest", Json(manifest));
        object Descriptor(KeyValuePair<string, (string Kind, byte[] Bytes)> row) => new { path = row.Key, kind = row.Value.Kind, sha256 = PackageLoader.Hash(row.Value.Bytes), size = row.Value.Bytes.Length };
        var legacy = files.Where(x => x.Value.Kind is "names" or "novel" or "legacy_manifest").Select(Descriptor).ToArray();
        files["legacy-components.json"] = ("legacy_index", Json(new { schema_version = 1, files = legacy }));
        var ui = Json(new { schema_version = 1, locale = "ko", origin = "logical-authority-kept", revision = ko + extraBinding + adapter,
            files = files.Where(x => x.Value.Kind is "master" or "resources" or "bindings" or "legacy_index").Select(Descriptor).ToArray() });
        var release = Json(new { schema_version = 2, locale = "ko", profile = "NextgenCandidate", revision = "transport-independent",
            status = "OFFLINE_CANDIDATE", ui_manifest = new { path = "ui/manifest.json", size = ui.Length, sha256 = PackageLoader.Hash(ui) }, files = files.Select(Descriptor).ToArray() });
        var package = files.ToDictionary(x => x.Key, x => x.Value.Bytes);
        package["ui/manifest.json"] = ui; package["release-manifest.json"] = release;
        return package;
    }
    static void RebindDescriptors(Dictionary<string,byte[]> package)
    {
        void Sync(JsonArray rows)
        {
            foreach (var row in rows)
            {
                var bytes=package[(string)row!["path"]!];
                row["sha256"]=PackageLoader.Hash(bytes);row["size"]=bytes.Length;
            }
        }
        byte[] Bytes(JsonNode value)=>Encoding.UTF8.GetBytes(value.ToJsonString());
        var legacy=JsonNode.Parse(package["legacy-components.json"])!;Sync(legacy["files"]!.AsArray());
        package["legacy-components.json"]=Bytes(legacy);
        var ui=JsonNode.Parse(package["ui/manifest.json"])!;Sync(ui["files"]!.AsArray());package["ui/manifest.json"]=Bytes(ui);
        var release=JsonNode.Parse(package["release-manifest.json"])!;Sync(release["files"]!.AsArray());
        release["ui_manifest"]!["size"]=package["ui/manifest.json"].Length;
        release["ui_manifest"]!["sha256"]=PackageLoader.Hash(package["ui/manifest.json"]);
        package["release-manifest.json"]=Bytes(release);
    }
    async Task<string> Write(Dictionary<string, byte[]> package, string name)
    {
        var root = Path.Combine(scratch, name);
        foreach (var file in package) { var path = Path.Combine(root, file.Key.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllBytesAsync(path, file.Value); }
        return root;
    }
    async Task<(RuntimeCoordinator Coordinator, Features Feature, UiOwnershipService Ownership)> Runtime(string name, Dictionary<string, byte[]>? package = null)
    {
        var registry = Registry(); var loader = new PackageLoader(registry); var owner = new UiOwnershipService(); var features = new Features();
        var store = new LocalImmutablePackageStore(Path.Combine(scratch, name + "-store"), loader);
        var runtime = new RuntimeCoordinator(loader, registry, owner, new Scheduler(), features, store);
        if (package != null) Assert((await runtime.StartAsync(await Write(package, name))).Committed, "fixture start failed");
        return (runtime, features, owner);
    }
    async Task<UpdatePreparation> Supply(RuntimeCoordinator runtime, Dictionary<string, byte[]> package, string name, Action<Mock>? configure = null, CancellationToken ct = default, UpdateSource? source = null)
    {
        var mock = new Mock(package); configure?.Invoke(mock);
        using var client = new HttpClient(mock);
        var updater = new FixedRefDataUpdater(client, Path.Combine(scratch, name + "-downloads"));
        var result = await updater.PrepareAsync(source ?? Source, runtime, ct);
        Assert(!Directory.Exists(Path.Combine(scratch, name + "-downloads")) || !Directory.EnumerateFileSystemEntries(Path.Combine(scratch, name + "-downloads")).Any(), "owned temporary left after success/failure/cancel");
        return result;
    }
    public async Task RunAsync()
    {
        Directory.CreateDirectory(scratch);
        string dllPath = typeof(FixedRefDataUpdater).Assembly.Location;
        string dllBefore = PackageLoader.Hash(await File.ReadAllBytesAsync(dllPath));
        await Check("NG12_full_SHA_uses_small_Git_commit_not_changed_file_metadata", async () => {
            var runtime = await Runtime("git-sha"); var mock = new Mock(Package()); using var client = new HttpClient(mock);
            var result = await new FixedRefDataUpdater(client, Path.Combine(scratch,"git-sha-downloads"))
                .PrepareAsync(Source with { Channel = Commit.ToUpperInvariant() },runtime.Coordinator);
            Assert(result.Prepared != null && result.TransportCommit == Commit,"full SHA not prepared");
            Assert(mock.Resolves == 1 && mock.Requests[0].AbsolutePath == "/repos/owner/repo/git/commits/"+Commit,"large REST commit endpoint used");
            Assert(mock.Requests.Where(u=>u.Host=="raw.test").All(u=>u.AbsolutePath.Contains("/"+Commit+"/")),"payload ref not fixed");
            runtime.Coordinator.Dispose();
        });
        await Check("NG12_branch_namespaced_ref_and_supported_tag_resolution", async () => {
            foreach (var (channel,tag,missing,apiCount) in new[] {
                ("feature/ui","",false,1),("refs/heads/feature/ui","",false,1),
                ("refs/tags/v1","",false,1),("v1","",true,2),
                ("refs/tags/v1","annotated",false,2),("v1","annotated",true,3) })
            {
                var runtime = await Runtime("ref-"+Guid.NewGuid().ToString("N"));
                var mock = new Mock(Package()){TagMode=tag,BranchMissing=missing};using var client = new HttpClient(mock);
                var result = await new FixedRefDataUpdater(client,Path.Combine(scratch,"refs-"+Guid.NewGuid().ToString("N")))
                    .PrepareAsync(Source with { Channel=channel },runtime.Coordinator);
                Assert(result.Prepared!=null && result.TransportCommit==Commit && mock.Resolves==apiCount,"branch/tag contract lost: "+channel+tag);
                Assert(mock.Requests.Where(u=>u.Host=="raw.test").All(u=>u.AbsolutePath.Contains("/"+Commit+"/")),"tag/branch moving payload");
                runtime.Coordinator.Dispose();
            }
        });
        await Check("NG12_invalid_resolver_metadata_keeps_current_previous_pending_and_restart_LKG", async () => {
            var runtime = await Runtime("resolver-lkg",Package("이전"));
            var current = await Supply(runtime.Coordinator,Package("현재"),"resolver-current");
            Assert(current.Prepared!=null && runtime.Coordinator.CommitUi(current.Prepared).Committed,"fixture current commit failed");
            var pending = await runtime.Coordinator.PrepareAsync(await Write(Package("재시작 대기"),"resolver-pending"));
            Assert(pending!=null && runtime.Coordinator.SavePendingStartup(pending),"fixture pending failed");
            string storeRoot=Path.Combine(scratch,"resolver-lkg-store");
            var records = new[]{"current.json","previous.json","pending_restart.json"}.ToDictionary(p=>p,p=>File.ReadAllBytes(Path.Combine(storeRoot,p)));
            var selected=runtime.Coordinator.Selected!;
            foreach(var (channel,fault,tag,expectedApiCalls) in new[] {
                (Commit,"sha","",1),(Commit,"tree","",1),(new string('0',40),"notfound","",1),
                ("main","bad-sha","",1),("main","ref","",1),("main","object","",1),
                ("main","json","",1),("main","duplicate","",1),("main","oversize","",1),
                ("main","chunked-large","",1),("main","forbidden","",1),("main","redirect-meta","",1),
                ("main","final-meta-host","",1),("refs/tags/v1","","cycle",2),
                ("refs/tags/v1","","deep",5),("refs/tags/v1","tag-id","annotated",2) })
            {
                var mock=new Mock(Package("잘못된 신규")){MetadataFault=fault,TagMode=tag};using var client=new HttpClient(mock);
                var result=await new FixedRefDataUpdater(client,Path.Combine(scratch,"metadata-"+Guid.NewGuid().ToString("N")))
                    .PrepareAsync(Source with { Channel=channel },runtime.Coordinator);
                Assert(result.Prepared==null && result.Metrics.PayloadDownloads==0 && mock.Resolves==expectedApiCalls,"bad metadata accepted/fell through: "+fault+tag);
                Assert(ReferenceEquals(selected,runtime.Coordinator.Selected),"metadata changed active selection");
                Assert(records.All(row=>File.ReadAllBytes(Path.Combine(storeRoot,row.Key)).SequenceEqual(row.Value)),"metadata changed selection records");
            }
            foreach(string invalid in new[]{"../main","main/../other","/main","main/","main//x"}) {
                var mock=new Mock(Package());using var client=new HttpClient(mock);
                var result=await new FixedRefDataUpdater(client,Path.Combine(scratch,"channel-paths"))
                    .PrepareAsync(Source with { Channel=invalid },runtime.Coordinator);
                Assert(result.Prepared==null && mock.Requests.Count==0,"unsafe channel path reached transport");
            }
            var restarted=await new LocalImmutablePackageStore(storeRoot,new PackageLoader(Registry())).RecoverAsync();
            Assert(restarted?.ContentId==selected.Identity,"current LKG failed after resolver rejection");
            Assert(records.All(row=>File.ReadAllBytes(Path.Combine(storeRoot,row.Key)).SequenceEqual(row.Value)),"restart read changed selection");
            runtime.Coordinator.Dispose();
        });
        if (exportRoot != null && changedRoot != null)
        await Check("NG10_actual_Git_export_complete_then_UI_changed_only_fixed_ref", async () => {
            // Read the actual chosen exported package once to construct the mock server.
            // Server fixture reads are distinct from product updater/cache counters.
            var package = Directory.EnumerateFiles(exportRoot, "*.json", SearchOption.AllDirectories)
                .Where(p => !Path.GetFileName(p).StartsWith('.'))
                .ToDictionary(p => Path.GetRelativePath(exportRoot,p).Replace('\\','/'), File.ReadAllBytes);
            var runtime = await Runtime("actual-export"); var coldMock = new Mock(package);
            using (var client = new HttpClient(coldMock)) {
                var cold = await new FixedRefDataUpdater(client,Path.Combine(scratch,"actual-export-cold")).PrepareAsync(Source,runtime.Coordinator);
                Assert(cold.Prepared != null,"actual export rejected: "+cold.Reason);
                Assert(coldMock.Resolves==1 && coldMock.Requests.Where(u=>u.Host=="raw.test").All(u=>u.AbsolutePath.Contains("/"+Commit+"/")),"actual export moving ref");
                Assert(runtime.Coordinator.Selected==null,"supplier changed current");
                Assert(runtime.Coordinator.CommitUi(cold.Prepared!).Committed,"actual startup commit failed");
                costs["actual_Git_export_first_import"] = cold.Metrics;
            }
            var updated = new Dictionary<string,byte[]>(package);
            // Novel files were not modified by the reviewed UI edit; reuse fixture
            // bytes without reading any changed-root Novel bodies.
            foreach(var path in package.Keys.Where(p=>!p.StartsWith("novels/",StringComparison.Ordinal)))
                updated[path]=File.ReadAllBytes(Path.Combine(changedRoot,path.Replace('/',Path.DirectorySeparatorChar)));
            var warmMock = new Mock(updated); using var warmClient = new HttpClient(warmMock);
            var warm = await new FixedRefDataUpdater(warmClient,Path.Combine(scratch,"actual-export-warm")).PrepareAsync(Source,runtime.Coordinator);
            Assert(warm.Prepared!=null && warmMock.Resolves==1,"actual changed export failed");
            Assert(!warmMock.Requests.Any(u=>u.AbsolutePath.Contains("/novels/",StringComparison.Ordinal)),"unchanged Novel downloaded");
            Assert(warmMock.Requests.Where(u=>u.Host=="raw.test").All(u=>u.AbsolutePath.Contains("/"+Commit+"/")),"warm moving ref mixed");
            Assert(runtime.Coordinator.CommitUi(warm.Prepared!).Committed && runtime.Feature.Initializations==1,"UI commit refreshed legacy initialization");
            costs["actual_Git_export_UI_changed_only"] = warm.Metrics;
        });
        await Check("U01_fixed_ref_branch_moves_resolve_once_no_moving_main_payload", async () => {
            var runtime = await Runtime("fixed"); var mock = new Mock(Package()); using var client = new HttpClient(mock);
            var result = await new FixedRefDataUpdater(client, Path.Combine(scratch, "fixed-downloads")).PrepareAsync(Source, runtime.Coordinator);
            Assert(result.Prepared != null && result.TransportCommit == Commit, "fixed commit not materialized");
            costs["cold_complete_fixture_import"] = result.Metrics;
            Assert(mock.Resolves == 1 && mock.Requests.Where(x => x.Host == "raw.test").All(x => x.AbsolutePath.Contains("/" + Commit + "/")), "moving ref mixed into payload");
            Assert(runtime.Coordinator.Selected == null && !File.Exists(Path.Combine(scratch, "fixed-store", "current.json")), "downloader independently selected package");
        });
        await Check("U02_UI_manifest_release_size_sha_bound_before_payload_fetch", async () => {
            var runtime = await Runtime("tamper", Package()); var active = runtime.Coordinator.Selected;
            foreach (string kind in new[] { "hash", "size" }) {
                var package = Package("교정"); var release = JsonNode.Parse(package["release-manifest.json"])!;
                if (kind == "hash") release["ui_manifest"]!["sha256"] = new string('a',64); else release["ui_manifest"]!["size"] = 1;
                package["release-manifest.json"] = Encoding.UTF8.GetBytes(release.ToJsonString());
                var result = await Supply(runtime.Coordinator, package, "tamper-" + kind);
                Assert(result.Prepared == null && result.Metrics.PayloadDownloads == 0 && ReferenceEquals(runtime.Coordinator.Selected, active), "tampered UI root accepted");
            }
        });
        await Check("U03_same_actual_candidate_DLL_KO_and_supported_binding_UI_reload_session_unchanged", async () => {
            var runtime = await Runtime("ui", Package()); var authority = runtime.Feature.MasterView; var old = runtime.Coordinator.Selected!;
            var result = await Supply(runtime.Coordinator, Package("수정 다운로드", true), "ui-update");
            Assert(result.Prepared != null, "UI supply failed");
            var commit = runtime.Coordinator.CommitUi(result.Prepared!);
            Assert(commit.Committed && commit.Persisted && runtime.Coordinator.Selected!.Resources["fixture.resource"].Ko == "수정 다운로드"
                && runtime.Coordinator.Selected.Bindings.ContainsKey("fixture.new.binding"), "same DLL UI correction/binding unavailable");
            Assert(ReferenceEquals(runtime.Feature.MasterView, authority) && runtime.Feature.Initializations == 1 && runtime.Coordinator.Selected?.Generations.Master == old.Generations.Master, "UI-only refreshed authority session");
            Assert(PackageLoader.Hash(await File.ReadAllBytesAsync(dllPath)) == dllBefore, "updater changed candidate DLL");
        });
        await Check("U04_no_change_payload_zero_and_UI_only_unchanged_Novel_network_zero", async () => {
            var runtime = await Runtime("delta", Package());
            var same = await Supply(runtime.Coordinator, Package(), "same");
            costs["validated_fixture_no_change"] = same.Metrics;
            Assert(same.Reason == "UPDATE_NO_CHANGE" && same.Metrics.PayloadDownloads == 0 && same.Metrics.AppPayloadHashes == 0 && !same.Metrics.ColdImport, "no-change fetched/hashed payload");
            var next = await Supply(runtime.Coordinator, Package("다음 다운로드"), "delta-update");
            costs["validated_fixture_UI_correction"] = next.Metrics;
            Assert(next.Prepared != null && next.Metrics.PayloadDownloads == 1 && next.Metrics.ReusedFiles == 6 && next.Metrics.AppPayloadHashes == 1, "changed-only transfer count wrong");
            Assert(next.Metrics.OsLinks + next.Metrics.OsCopies == 6, "OS reuse costs not separated");
        });
        await Check("offline_CLI_recovered_baseline_reuse_pending_only_no_native_or_current_selection", async () => {
            var running=await Runtime("offline",Package());var currentBytes=await File.ReadAllBytesAsync(Path.Combine(scratch,"offline-store","current.json"));
            var registry=Registry();var store=new LocalImmutablePackageStore(Path.Combine(scratch,"offline-store"),new PackageLoader(registry));
            var coordinator=new RuntimeCoordinator(new PackageLoader(registry),registry,new UiOwnershipService(),new PreparationScheduler(),new PreparationFeatures(),store);
            var same=await Supply(coordinator,Package(),"offline-same");
            Assert(same.Reason=="UPDATE_NO_CHANGE"&&same.Metrics.PayloadDownloads==0&&same.BasedOnIdentity==running.Coordinator.Selected!.Identity,"cold process ignored managed verified baseline");
            var next=await Supply(coordinator,Package("대기 교정"),"offline-next");
            Assert(next.Prepared!=null&&coordinator.SavePendingStartup(next.Prepared,next.BasedOnIdentity),"offline pending record missing");
            Assert(coordinator.Host==null&&coordinator.Selected==null&&store.ReadPendingRestart()?.ContentId==next.Prepared!.Snapshot.Identity
                &&(await File.ReadAllBytesAsync(Path.Combine(scratch,"offline-store","current.json"))).SequenceEqual(currentBytes),"CLI prepared native/current selection");
        });
        await Check("U04_missing_JSON_downloaded_complete_new_generation_and_protected_same_ID_repair_deferred", async () => {
            var runtime=await Runtime("missing",Package());var old=runtime.Coordinator.Selected!;
            File.Delete(Path.Combine(old.Payloads.Root,"novels","43.json"));
            var same=await Supply(runtime.Coordinator,Package(),"missing-same");
            Assert(same.Prepared==null&&same.Metrics.PayloadDownloads==1&&ReferenceEquals(old,runtime.Coordinator.Selected),"missing protected same-ID package silently accepted or overwritten");
            // A rejected corrupt package loses its validated-custody token. Measure the independent changed+missing
            // case from a fresh valid baseline, rather than silently trusting that rejected cache again.
            var separate=await Runtime("missing-ui",Package());var separateOld=separate.Coordinator.Selected!;
            File.Delete(Path.Combine(separateOld.Payloads.Root,"novels","43.json"));
            var next=await Supply(separate.Coordinator,Package("교정"),"missing-next");
            costs["validated_fixture_UI_correction_missing_Novel"] = next.Metrics;
            Assert(next.Prepared!=null&&next.Metrics.PayloadDownloads==2,"new UI + missing Novel transfer incorrect");
            var restored=await next.Prepared!.Snapshot.Payloads.ReadAsync("novels/43.json");
            Assert(restored!=null&&PackageLoader.Hash(restored)==PackageLoader.Hash(Package()["novels/43.json"])
                &&!File.Exists(Path.Combine(separateOld.Payloads.Root,"novels","43.json")),"new generation incomplete or old root overwritten");
        });
        await Check("R03_same_ID_inactive_missing_cache_rebuilt_from_valid_supply", async () => {
            var registry=Registry();var loader=new PackageLoader(registry);var store=new LocalImmutablePackageStore(Path.Combine(scratch,"missing-inactive-store"),loader);
            var package=Package();var old=await store.StageAsync(await Write(package,"missing-inactive"));
            File.Delete(Path.Combine(old.Root,"novels","43.json"));
            var coordinator=new RuntimeCoordinator(loader,registry,new UiOwnershipService(),new PreparationScheduler(),new PreparationFeatures(),store);
            var next=await Supply(coordinator,package,"missing-inactive-update");
            Assert(next.Prepared!=null&&next.Prepared.Snapshot.Identity==old.ContentId&&File.Exists(Path.Combine(next.Prepared.Snapshot.Payloads.Root,"novels","43.json"))
                &&coordinator.Selected==null,"inactive same-ID recovery failed or selected package");
        });
        await Check("U05_hash_size_malformed_JSON_failures_keep_current_LKG", async () => {
            var runtime = await Runtime("integrity", Package()); var old = runtime.Coordinator.Selected;
            string root=Path.Combine(scratch,"integrity-store");
            byte[]? Record(string name)=>File.Exists(Path.Combine(root,name))?File.ReadAllBytes(Path.Combine(root,name)):null;
            var records=new[]{"current.json","previous.json","pending_restart.json"}.ToDictionary(n=>n,Record);
            foreach (string fault in new[] { "hash", "size", "json" }) {
                var package = Package("교정");
                if (fault == "json") {
                    package["ui/resources.json"] = Encoding.UTF8.GetBytes("{");
                    var ui = JsonNode.Parse(package["ui/manifest.json"])!; var release = JsonNode.Parse(package["release-manifest.json"])!;
                    foreach (var row in ui["files"]!.AsArray().Concat(release["files"]!.AsArray()).Where(x => (string?)x!["path"] == "ui/resources.json")) { row!["sha256"] = PackageLoader.Hash(package["ui/resources.json"]); row["size"] = 1; }
                    package["ui/manifest.json"] = Encoding.UTF8.GetBytes(ui.ToJsonString()); release["ui_manifest"]!["size"] = package["ui/manifest.json"].Length; release["ui_manifest"]!["sha256"] = PackageLoader.Hash(package["ui/manifest.json"]);
                    package["release-manifest.json"] = Encoding.UTF8.GetBytes(release.ToJsonString());
                }
                var result = await Supply(runtime.Coordinator, package, "integrity-" + fault, mock => { if (fault != "json") mock.Fault = fault; });
                Assert(result.Prepared == null && ReferenceEquals(old, runtime.Coordinator.Selected), "bad payload changed current");
                Assert(records.All(row=>(row.Value==null && Record(row.Key)==null)||row.Value!=null && Record(row.Key) is {} now && row.Value.SequenceEqual(now)),"bad payload changed persistent selection records");
            }
        });
        await Check("U05_parseable_legacy_shapes_and_registered_closure_reject_candidate_keep_LKG", async () => {
            var runtime=await Runtime("typed-legacy",Package());var old=runtime.Coordinator.Selected;
            var pointer=Path.Combine(scratch,"typed-legacy-store","current.json");var pointerBefore=await File.ReadAllBytesAsync(pointer);
            foreach(var fault in new[] {"names-array","names-number","manifest-empty","novel-nested","novel-array","registered-missing","root-semantic-mismatch","master-semantic-mismatch","names-semantic-mismatch"})
            {
                var package=Package("검증 교정");
                if(fault=="names-array")package["names.json"]=Encoding.UTF8.GetBytes("[]");
                else if(fault=="names-number")package["names.json"]=Encoding.UTF8.GetBytes("{\"人物\":3}");
                else if(fault=="manifest-empty")package["manifest.json"]=Encoding.UTF8.GetBytes("{}");
                else if(fault=="novel-nested")package["novels/43.json"]=Encoding.UTF8.GetBytes("{\"セリフ\":{\"ko\":\"대사\"}}");
                else if(fault=="novel-array")package["novels/43.json"]=Encoding.UTF8.GetBytes("[]");
                else
                {
                    var manifest=JsonNode.Parse(package["manifest.json"])!;
                    manifest.AsObject().Remove("hash");
                    if(fault=="registered-missing")manifest["novels"]!["44"]=new string('a',32);
                    else if(fault=="master-semantic-mismatch")manifest["master"]=new string('a',32);
                    else if(fault=="names-semantic-mismatch")manifest["names"]=new string('a',32);
                    manifest["hash"]=fault=="root-semantic-mismatch"?new string('a',32):TranslationHash.Compute(manifest.ToJsonString());
                    package["manifest.json"]=Encoding.UTF8.GetBytes(manifest.ToJsonString());
                }
                RebindDescriptors(package);
                var result=await Supply(runtime.Coordinator,package,"typed-legacy-"+fault);
                Assert(result.Prepared==null&&ReferenceEquals(old,runtime.Coordinator.Selected)
                    &&(await File.ReadAllBytesAsync(pointer)).SequenceEqual(pointerBefore),"parseable legacy fault changed selected LKG: "+fault);
            }
            var valid=await Supply(runtime.Coordinator,Package("유효 교정"),"typed-legacy-valid");
            Assert(valid.Prepared!=null&&runtime.Coordinator.CommitUi(valid.Prepared).Committed&&runtime.Feature.Initializations==1,"valid retry after typed rejection failed or reset session");
        });
        await Check("U05_429_5xx_timeout_bounded_retry_recovery_and_cancel_cleanup", async () => {
            foreach (string transient in new[] { "429", "503", "timeout" }) {
                var runtime = await Runtime("retry" + transient, Package());
                var result = await Supply(runtime.Coordinator, Package("교정"), "retry-" + transient, mock => mock.Fault = transient);
                Assert(result.Prepared != null && result.Metrics.Retries == 2, "transient retry bound/recovery incorrect");
                result = await Supply(runtime.Coordinator, Package("교정"), "exhaust-" + transient, mock => { mock.Fault = transient; mock.Exhaust = true; });
                Assert(result.Prepared == null && result.Metrics.Retries == 2, "retry exhaustion incorrect");
            }
            var cancelled = await Runtime("cancel", Package()); using var cancel = new CancellationTokenSource();
            var cancellation = await Supply(cancelled.Coordinator, Package("교정"), "cancel-update", mock => mock.Cancel = cancel, cancel.Token);
            Assert(cancellation.Reason == "UPDATE_CANCELLED" && cancelled.Coordinator.Selected != null, "cancellation mixed package");
        });
        await Check("U06_path_host_redirect_credential_logging_rejected", async () => {
            var runtime = await Runtime("security", Package()); var old = runtime.Coordinator.Selected;
            var hostile = Package("교정"); var release = JsonNode.Parse(hostile["release-manifest.json"])!;
            release["files"]![0]!["path"] = "../escape.json"; hostile["release-manifest.json"] = Encoding.UTF8.GetBytes(release.ToJsonString());
            Assert((await Supply(runtime.Coordinator, hostile, "traversal")).Prepared == null, "path escape allowed");
            foreach (string redirect in new[] { "redirect", "final-host" }) Assert((await Supply(runtime.Coordinator, Package("교정"), redirect, mock => mock.Fault = redirect)).Prepared == null, "redirect allowed");
            var messages = new List<string>(); var mock2 = new Mock(Package()); using var client = new HttpClient(mock2); client.DefaultRequestHeaders.Authorization = new("Bearer", "DO_NOT_LOG_TOKEN");
            var rejected = await new FixedRefDataUpdater(client, Path.Combine(scratch,"auth-downloads"), new SafeDiagnostics(sink: messages.Add)).PrepareAsync(Source, runtime.Coordinator);
            Assert(rejected.Prepared == null && mock2.Requests.Count == 0 && !string.Join("", messages).Contains("DO_NOT_LOG_TOKEN") && ReferenceEquals(runtime.Coordinator.Selected,old), "secret forwarded/logged");
        });
        await Check("U07_Choice_natural_boundary_and_failed_owned_restore_preserve_snapshot_lease", async () => {
            var runtime = await Runtime("boundary", Package()); var snapshot = runtime.Coordinator.Selected!; var target = new Target();
            var state = target.ReadState(); Assert(runtime.Ownership.TryWrite(target, new("General", "fixture.binding", null, "download-label",snapshot.Generations.Ui,state), "다운로드").Written, "lease fixture failed");
            var result = await Supply(runtime.Coordinator, Package("새 다운로드"), "boundary-update"); Assert(result.Prepared != null, "supply failed");
            runtime.Feature.Busy=true; Assert(runtime.Coordinator.CommitUi(result.Prepared!).Reason == "CHOICE_SAFE_BOUNDARY_DEFERRED", "busy Choice committed");
            runtime.Feature.Busy=false; target.FailWrite=true; var failure=runtime.Coordinator.CommitUi(result.Prepared!);
            Assert(!failure.Committed && ReferenceEquals(snapshot,runtime.Coordinator.Selected) && runtime.Ownership.LeaseCount==1 && target.ReadState().Text=="다운로드", "failed restore lost lease/current");
            target.FailWrite=false; Assert(runtime.Coordinator.CommitUi(result.Prepared!).Committed && runtime.Ownership.LeaseCount==0, "natural-end restore retry failed");
        });
        await Check("U11_optional_unknown_adapter_noop_and_invalid_required_contract_rejected", async () => {
            var runtime = await Runtime("optional", Package());
            var candidate=await Supply(runtime.Coordinator, Package(adapter:"unknown.optional.v1"), "optional-update");
            Assert(candidate.Prepared != null && candidate.Prepared.Snapshot.Validation.UnavailableAdapters.Contains("unknown.optional.v1"), "unknown optional rejected entire package");
            var broken=Package(); var ui=JsonNode.Parse(broken["ui/manifest.json"])!;ui["schema_version"]=99;
            broken["ui/manifest.json"]=Encoding.UTF8.GetBytes(ui.ToJsonString());var release=JsonNode.Parse(broken["release-manifest.json"])!;
            release["ui_manifest"]!["size"]=broken["ui/manifest.json"].Length;release["ui_manifest"]!["sha256"]=PackageLoader.Hash(broken["ui/manifest.json"]);broken["release-manifest.json"]=Encoding.UTF8.GetBytes(release.ToJsonString());
            Assert((await Supply(runtime.Coordinator,broken,"schema-update")).Prepared==null,"required schema silently accepted");
        });
        await Check("U12_DLL_font_and_remote_v1_never_installed", async () => {
            var runtime = await Runtime("installer", Package());
            foreach(string bad in new[]{"GCMod.dll","fonts/new.ttf","payload.exe"}) {
                var package=Package();var release=JsonNode.Parse(package["release-manifest.json"])!;
                release["files"]!.AsArray().Add(new JsonObject{["path"]=bad,["kind"]="resources",["size"]=2,["sha256"]=PackageLoader.Hash(Encoding.UTF8.GetBytes("{}"))});
                package["release-manifest.json"]=Encoding.UTF8.GetBytes(release.ToJsonString());
                Assert((await Supply(runtime.Coordinator,package,"bad-"+Path.GetFileName(bad))).Prepared==null,"non-JSON installed");
            }
            var v1=Package();var root=JsonNode.Parse(v1["release-manifest.json"])!;root["schema_version"]=1;root["ui_manifest"]="ui/manifest.json";
            v1["release-manifest.json"]=Encoding.UTF8.GetBytes(root.ToJsonString());
            Assert((await Supply(runtime.Coordinator,v1,"remote-v1")).Prepared==null,"remote v1 hashless entry allowed");
            Assert((await new PackageLoader(Registry()).LoadAsync(await Write(v1,"local-v1"))).Resources.Count==1,"narrow local v1 import lost");
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { status=failures==0?"PASS":"FAIL", failures, implementation="actual candidate GCMod.dll", dll_sha256=dllBefore, scope="CPU + mock HttpMessageHandler; native/game/real HTTP NOT_RUN", results,
            exported_package=exportRoot, changed_package=changedRoot,
            transport_ref="synthetic immutable mock commit; actual selected Git export bytes when supplied; no real HTTP/publication",
            cost_sample_scope="actual updater counters; server fixture preload/copy and downstream loader/first-use costs are separate", cost_samples=costs },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Supply CPU: {results.Count-failures}/{results.Count} PASS; actual candidate; native/game NOT_RUN");
        Environment.ExitCode=failures==0?0:1;
    }
    sealed class Scheduler : IRuntimeMainThreadScheduler { public bool IsMainThread=>true; public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken ct=default){ct.ThrowIfCancellationRequested();return Task.FromResult(callback());} }
    sealed class PreparationScheduler : IRuntimeMainThreadScheduler { public bool IsMainThread=>false; public Task<T> InvokeAsync<T>(Func<T> callback,CancellationToken ct=default)=>throw new InvalidOperationException("NO_NATIVE_CLI_SCHEDULER"); }
    sealed class PreparationFeatures : IRuntimeFeatureProvider
    {
        public IMasterTranslationView MasterView=>MasterTranslationView.Empty;public bool IsChoiceBusy=>false;
        public void Initialize(TranslationPackageSnapshot snapshot,UiModuleHost host)=>throw new InvalidOperationException("NO_NATIVE_CLI_INITIALIZE");
        public void Selected(TranslationPackageSnapshot snapshot,UiModuleHost host)=>throw new InvalidOperationException("NO_NATIVE_CLI_SELECT");public bool TryUninstall()=>true;public void Dispose(){}
    }
    sealed class Features : IRuntimeFeatureProvider
    {
        public IMasterTranslationView MasterView{get;}=MasterTranslationView.Empty;public bool Busy;public bool IsChoiceBusy=>Busy;public int Initializations;
        public void Initialize(TranslationPackageSnapshot snapshot,UiModuleHost host)=>Initializations++;
        public void Selected(TranslationPackageSnapshot snapshot,UiModuleHost host){} public bool TryUninstall()=>!Busy; public void Dispose(){}
    }
    sealed class Target : IUiTextTarget
    {
        TextTargetState state=new(new(11,22,"TMP",33),"一括ダウンロード",44,55,"\"asset-key:download-label\"","一括ダウンロード"); public bool FailWrite;
        public TextTargetState ReadState()=>state;public bool TryWriteText(TextTargetState expected,string text){if(FailWrite)throw new IOException("restore fault");if(state!=expected)return false;state=state with{Text=text};return true;}
    }
    sealed class Mock(Dictionary<string,byte[]> package) : HttpMessageHandler
    {
        public List<Uri> Requests=[]; public int Resolves;public string? Fault;public bool Exhaust;public CancellationTokenSource? Cancel;int faultRequests;
        public string? MetadataFault;public string TagMode="";public bool BranchMissing;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();var uri=request.RequestUri!;Requests.Add(uri);
            if(uri.Host=="api.test") {
                Resolves++;
                HttpResponseMessage Response(HttpStatusCode status,string json) => new(status){RequestMessage=request,Content=new StringContent(json)};
                string apiPath=Uri.UnescapeDataString(uri.AbsolutePath),json;
                if(MetadataFault=="notfound" || BranchMissing && apiPath.Contains("/git/ref/heads/"))return Task.FromResult(Response(HttpStatusCode.NotFound,"{}"));
                if(MetadataFault=="forbidden")return Task.FromResult(Response(HttpStatusCode.Forbidden,"{}"));
                if(MetadataFault=="redirect-meta")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found){RequestMessage=request,Headers={Location=new Uri("https://evil.test/meta")},Content=new StringContent("{}")});
                if(apiPath.Contains("/git/commits/"))json=JsonSerializer.Serialize(new{sha=MetadataFault=="sha"?new string('d',40):Commit,tree=new{sha=MetadataFault=="tree"?"bad":new string('e',40)}});
                else if(apiPath.Contains("/git/ref/")) {
                    string reference="refs/"+apiPath[(apiPath.IndexOf("/git/ref/",StringComparison.Ordinal)+9)..];
                    bool tag=reference.StartsWith("refs/tags/",StringComparison.Ordinal) && TagMode!="";
                    json=JsonSerializer.Serialize(new{@ref=MetadataFault=="ref"?"refs/heads/other":reference,@object=new{type=MetadataFault=="object"?"tree":tag?"tag":"commit",sha=MetadataFault=="bad-sha"?new string('x',40):tag?new string('a',40):Resolves==1||reference.StartsWith("refs/tags/")?Commit:new string('d',40)}});
                }
                else if(apiPath.Contains("/git/tags/")) {
                    string tagSha=apiPath.Split('/')[^1];
                    string next=TagMode=="deep"?new string((char)(tagSha[0]+1),40):TagMode=="cycle"?tagSha:Commit;
                    json=JsonSerializer.Serialize(new{sha=MetadataFault=="tag-id"?new string('f',40):tagSha,@object=new{type=TagMode is "cycle" or "deep"?"tag":"commit",sha=next}});
                }
                else return Task.FromResult(Response(HttpStatusCode.NotFound,"{}")); // No broad REST commits fallback.
                if(MetadataFault=="json")json="{";
                if(MetadataFault=="duplicate")json="{\"ref\":\"one\",\"ref\":\"two\"}";
                if(MetadataFault is "oversize" or "chunked-large")json=new string(' ',1024*1024)+json;
                var response=Response(HttpStatusCode.OK,json);
                if(MetadataFault=="chunked-large")response.Content=new UnknownLengthContent(Encoding.UTF8.GetBytes(json));
                if(MetadataFault=="final-meta-host")response.RequestMessage=new HttpRequestMessage(HttpMethod.Get,"https://evil.test/meta");
                return Task.FromResult(response);
            }
            string path=uri.AbsolutePath[(uri.AbsolutePath.IndexOf("/translations/ko/",StringComparison.Ordinal)+17)..];
            if(path=="ui/resources.json") {
                if(Cancel!=null){Cancel.Cancel();throw new OperationCanceledException(cancellationToken);}
                if(Fault is "429" or "503" or "timeout" && (Exhaust||faultRequests++<2)) {
                    if(Fault=="timeout")throw new TaskCanceledException("synthetic bounded timeout");
                    return Task.FromResult(new HttpResponseMessage((HttpStatusCode)int.Parse(Fault)){RequestMessage=request,Content=new StringContent("")});
                }
                if(Fault=="redirect")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found){RequestMessage=request,Headers={Location=new Uri("https://evil.test/payload")},Content=new StringContent("")});
                if(Fault=="final-host")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=new HttpRequestMessage(HttpMethod.Get,"https://evil.test/payload"),Content=new ByteArrayContent(package[path])});
            }
            if(!package.TryGetValue(path,out var bytes))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound){RequestMessage=request,Content=new StringContent("")});
            if(path=="ui/resources.json"&&Fault=="hash"){bytes=bytes.ToArray();bytes[^1]=(byte)' ';}
            if(path=="ui/resources.json"&&Fault=="size")bytes=bytes.Concat(new byte[]{32}).ToArray();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(bytes)});
        }
    }
    sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length){length=0;return false;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(bytes).AsTask();
    }
}
