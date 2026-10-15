using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using GCMod.Nextgen;
using GCMod.Candidate;
using GCMod.Services;

var runner = new RuntimeTests(args);
await runner.Run();

sealed class RuntimeTests(string[] args)
{
    readonly List<object> results = [];
    int failures;
    int? coldStagingNovelReads;
    readonly string scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "scratch-runtime-" + Guid.NewGuid().ToString("N"));
    static readonly JsonSerializerOptions options = new() { WriteIndented = true };
    async Task Check(string name, Func<Task> test)
    {
        try { await test(); results.Add(new { name, status = "PASS" }); }
        catch (Exception ex) { failures++; results.Add(new { name, status = "FAIL", error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static AdapterRegistry Registry()
    {
        var registry = AdapterRegistry.CreateNg01();
        registry.RegisterHostAdapter("core.static-caption.v1", SelectorContract.Host(["main-menu"], ["download-label", "new-label"], supportsCoMatch: true));
        return registry;
    }
    async Task<string> Fixture(string name, string ko = "일괄 다운로드", string masterKo = "제목", JsonElement? resourceDoc = null, JsonElement? bindingDoc = null)
    {
        string root = Path.Combine(scratch, name); Directory.CreateDirectory(Path.Combine(root, "ui")); Directory.CreateDirectory(Path.Combine(root, "layout")); Directory.CreateDirectory(Path.Combine(root, "novels"));
        var resources = resourceDoc?.GetRawText() ?? "{\"schema_version\":1,\"locale\":\"ko\",\"resources\":[{\"id\":\"fixture.resource\",\"role\":\"download\",\"kind\":\"literal\",\"jp\":\"一括ダウンロード\",\"ko\":" + JsonSerializer.Serialize(ko) + ",\"disposition\":\"translate\"}]}";
        var bindings = bindingDoc?.GetRawText() ?? "{\"schema_version\":1,\"bindings\":[{\"id\":\"fixture.binding\",\"resource_id\":\"fixture.resource\",\"adapter_id\":\"core.static-caption.v1\",\"enabled\":true,\"source_identity\":{\"kind\":\"static-asset\",\"identity\":\"asset-key:download\",\"exact_jp\":\"一括ダウンロード\"},\"selector\":{\"scope_key\":\"main-menu\",\"slot_key\":\"download-label\",\"type\":\"static\"}}]}";
        var master = "{\"mEpisode\":{\"title\":{\"題名\":" + JsonSerializer.Serialize(masterKo) + "}}}";
        var names = "{\"人物\":\"인물\"}";
        var novel = "{\"セリフ\":\"대사\"}";
        var legacyBody = new { names = TranslationHash.Compute(names), master = TranslationHash.Compute(master), novels = new Dictionary<string, string>() };
        var legacyManifest = JsonSerializer.Serialize(new { hash = TranslationHash.Compute(JsonSerializer.Serialize(legacyBody)), legacyBody.names, legacyBody.master, legacyBody.novels });
        var payloads = new Dictionary<string, (string Kind, string Json)> {
            ["ui/resources.json"] = ("resources", resources), ["ui/bindings.json"] = ("bindings", bindings), ["master.json"] = ("master", master),
            ["layout/episode-title.json"] = ("layout", "{\"schema_version\":1,\"policy_id\":\"episode-title.fit.v1\",\"font_profile\":\"RHR_Medium_GFSAFE_ADH\",\"base_size\":35,\"descending_sizes\":[34,33,32,31],\"restore\":\"owned-only\",\"overflow_failure\":\"restore-base\",\"scope_key\":\"episode-list-title\"}") };
        var legacy = new Dictionary<string, (string Kind, string Json)> { ["names.json"] = ("names", names), ["manifest.json"] = ("legacy_manifest", legacyManifest), ["novels/43.json"] = ("novel", novel) };
        foreach (var file in payloads.Concat(legacy)) await File.WriteAllTextAsync(Path.Combine(root, file.Key.Replace('/', Path.DirectorySeparatorChar)), file.Value.Json, new UTF8Encoding(false));
        object Descriptor(string path, (string Kind, string Json) row) { var bytes = Encoding.UTF8.GetBytes(row.Json); return new { path, kind = row.Kind, sha256 = PackageLoader.Hash(bytes), size = bytes.Length }; }
        var index = JsonSerializer.Serialize(new { schema_version = 1, files = legacy.Select(x => Descriptor(x.Key, x.Value)).ToArray() });
        await File.WriteAllTextAsync(Path.Combine(root, "ui/legacy-components.json"), index, new UTF8Encoding(false));
        payloads["ui/legacy-components.json"] = ("legacy_index", index);
        await File.WriteAllTextAsync(Path.Combine(root, "ui/manifest.json"), JsonSerializer.Serialize(new { schema_version = 1, locale = "ko", origin = "fixture-local-origin", revision = ko + masterKo, files = payloads.Select(x => Descriptor(x.Key, x.Value)).ToArray() }), new UTF8Encoding(false));
        return root;
    }
    static TextTargetState State() => new(new(123, 42, "TMPro.TextMeshProUGUI", 1), "一括ダウンロード", 3, 4, "\"asset-key:download\"", "一括ダウンロード");
    static UiBindContext Context(FailingTarget target, long generation = 1, bool mainThread = true) => new("fixture.binding", target,
        new("\"asset-key:download\"", "一括ダウンロード", "download", "main-menu", "download-label", [], 3, 4),
        new(MainThread: mainThread, CurrentOwner: true, UniqueTarget: true, DirectTarget: true, NativeControlsUnchanged: true, InputUntouched: true), generation, true);
    async Task<(UiModuleHost Host, UiOwnershipService Owner, MutableMaster Master, FailingTarget Target, FailingTarget Literal, CountingGlyphs Glyphs)> ReconciliationFixture(string name)
    {
        using var resources = JsonDocument.Parse("""
        {"schema_version":1,"locale":"ko","resources":[
          {"id":"fixture.resource","role":"download","kind":"master_ref","jp":"一括ダウンロード","disposition":"translate","master_ref":{"table":"mTextConfig","field":"ml_value[]","exact_jp":"一括ダウンロード"}},
          {"id":"fixture.literal.resource","role":"download","kind":"literal","jp":"一括ダウンロード","ko":"독립 캡션","disposition":"translate"}]}
        """);
        using var bindings = JsonDocument.Parse("""
        {"schema_version":1,"bindings":[
          {"id":"fixture.binding","resource_id":"fixture.resource","adapter_id":"core.static-caption.v1","enabled":true,"source_identity":{"kind":"static-asset","identity":"asset-key:download","exact_jp":"一括ダウンロード"},"selector":{"scope_key":"main-menu","slot_key":"download-label","type":"static"}},
          {"id":"fixture.literal","resource_id":"fixture.literal.resource","adapter_id":"core.static-caption.v1","enabled":true,"source_identity":{"kind":"static-asset","identity":"asset-key:download","exact_jp":"一括ダウンロード"},"selector":{"scope_key":"main-menu","slot_key":"new-label","type":"static"}}]}
        """);
        var root = await Fixture(name, resourceDoc: resources.RootElement, bindingDoc: bindings.RootElement);
        var snapshot = await new PackageLoader(Registry()).LoadAsync(root); var owner = new UiOwnershipService(); var master = new MutableMaster();
        var font = new FontService(); var glyphs = new CountingGlyphs(); var donor = new FontDonorHandle(FontService.ApprovedProfile, 77, 88, 1); font.PublishMediumDonor(donor, glyphs);
        var host = new UiModuleHost(snapshot, Registry(), owner, masterOverride: master, fonts: font); var target = new FailingTarget();
        var literal = new FailingTarget(State() with { Identity = State().Identity with { NativeObject = 124, InstanceId = 43 } });
        Assert(host.TryApply(Context(target) with { FontDonor = donor }).Written, "active Master write failed"); var literalContext = Context(literal) with { FontDonor = donor };
        Assert(host.TryApply(literalContext with { BindingId = "fixture.literal", Facts = literalContext.Facts with { SlotKey = "new-label" } }).Written, "independent literal write failed");
        return (host, owner, master, target, literal, glyphs);
    }
    public async Task Run()
    {
        Directory.CreateDirectory(scratch);
        string initial = await Fixture("initial"), changed = await Fixture("changed", "새 다운로드"), authority = await Fixture("authority", masterKo: "바뀐 제목");
        await Check("managed_IL_instruction_boundaries_ignore_opcode_bytes_inside_operands", async () => {
            await Task.CompletedTask;
            var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName("IlBoundaryFixture"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Fixture").DefineType("Wrapper", System.Reflection.TypeAttributes.Public);
            var decoy = type.DefineField("NativeMethodInfoPtr_Callback_Decoy", typeof(IntPtr), System.Reflection.FieldAttributes.Public | System.Reflection.FieldAttributes.Static);
            var actual = type.DefineField("NativeMethodInfoPtr_Callback_Actual", typeof(IntPtr), System.Reflection.FieldAttributes.Public | System.Reflection.FieldAttributes.Static);
            var method = type.DefineMethod("Callback", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static, typeof(IntPtr), Type.EmptyTypes);
            var il = method.GetILGenerator();
            // This InlineI8 operand contains a valid ldsfld byte followed by the decoy metadata token.
            il.Emit(System.Reflection.Emit.OpCodes.Ldc_I8, unchecked((long)((ulong)(uint)decoy.MetadataToken << 8 | 0x7e)));
            il.Emit(System.Reflection.Emit.OpCodes.Pop); il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, actual); il.Emit(System.Reflection.Emit.OpCodes.Ret);
            var created = type.CreateType(); var fields = ManagedIlReader.StaticHandleFields(created.GetMethod("Callback"), "NativeMethodInfoPtr_Callback_");
            Assert(fields.Length == 1 && fields[0].Name == actual.Name, "operand byte guessed as ldsfld / real handle unavailable");
        });
        await Check("resource_reconciliation_offthread_zero_target_and_glyph_reads", async () => {
            var fixture = await ReconciliationFixture("reconcile-offthread"); fixture.Master.EnabledTables = [];
            int targetReads = fixture.Target.Reads, literalReads = fixture.Literal.Reads, glyphReads = fixture.Glyphs.Reads;
            var result = fixture.Host.ReconcileUnavailableResources(true, false);
            Assert(!result.Committed && fixture.Target.Reads == targetReads && fixture.Literal.Reads == literalReads && fixture.Glyphs.Reads == glyphReads && fixture.Owner.LeaseCount == 2, "offthread reconcile touched target/glyph or dropped lease");
        });
        await Check("disabled_actual_Master_table_restores_only_dependent_binding_and_OFF_restores_all", async () => {
            var fixture = await ReconciliationFixture("reconcile-selective"); var snapshot = fixture.Host.Snapshot; fixture.Master.EnabledTables = [];
            var result = fixture.Host.ReconcileUnavailableResources(true, true);
            Assert(result.Committed && result.Restored == 1 && fixture.Target.ReadState().Text == State().ExactSource && fixture.Literal.ReadState().Text == "독립 캡션" && fixture.Owner.LeaseCount == 1, "Master disabled did not selectively preserve independent literal");
            fixture.Master.EnabledTables = ["mTextConfig"]; Assert(fixture.Host.TryApply(Context(fixture.Target) with { FontDonor = new(FontService.ApprovedProfile, 77, 88, 1) }).Written, "same epoch cannot naturally rebind after reconciliation");
            result = fixture.Host.ReconcileUnavailableResources(false, true);
            Assert(result.Committed && fixture.Owner.LeaseCount == 0 && fixture.Target.ReadState().Text == State().ExactSource && fixture.Literal.ReadState().Text == State().ExactSource && ReferenceEquals(snapshot, fixture.Host.Snapshot), "Translation OFF left owned KO or changed selection");
        });
        await Check("unavailable_Master_reconcile_retry_keeps_old_lease_and_snapshot", async () => {
            var fixture = await ReconciliationFixture("reconcile-retry"); var snapshot = fixture.Host.Snapshot; fixture.Master.EnabledTables = []; fixture.Target.FailWrite = true;
            var result = fixture.Host.ReconcileUnavailableResources(true, true);
            Assert(!result.Committed && result.RetryableFailures == 1 && fixture.Owner.LeaseCount == 2 && ReferenceEquals(snapshot, fixture.Host.Snapshot) && fixture.Target.ReadState().Text == "활성 마스터", "reconcile failure discarded old ownership/selection");
            fixture.Target.FailWrite = false; result = fixture.Host.ReconcileUnavailableResources(true, true);
            Assert(result.Committed && fixture.Owner.LeaseCount == 1 && fixture.Target.ReadState().Text == State().ExactSource && fixture.Literal.ReadState().Text == "독립 캡션", "reconcile retry failed or restored unrelated caption");
        });
        await Check("prepare_is_pure_and_optional_capability_independent", async () => {
            var loader = new PackageLoader(new LocalPackageFileSource(), Registry()); var service = new TranslationPackageService(loader);
            var prepared = await service.PrepareAsync(initial); Assert(prepared.Prepared && service.Active == null, "validation published Active");
        });
        await Check("actual_legacy_session_stored_unregistered_novel_and_success_task_reuse", async () => {
            var source = new CountingSource(); var snapshot = await new PackageLoader(source, Registry()).LoadAsync(initial);
            Assert(source.NovelReads == 0, "startup opened Novel");
            using var session = new TranslationSession(new LocalPackageTranslationStore(snapshot, "ko-ui3-common-poc"));
            var first = session.GetNovelAsync(43); var second = session.GetNovelAsync(43);
            Assert(ReferenceEquals(first, second), "same load task not shared"); var rows = await first;
            Assert(rows["セリフ"] == "대사" && session.TryGetNovel(43, out var ready) && ReferenceEquals(ready, rows), "unregistered stored novel denied or parser changed");
            Assert(source.NovelReads == 1, "lazy verified read not shared");
            Assert(await session.GetNovelAsync(44) == null, "missing payload became implicit deny/ fabricated data");
        });
        await Check("actual_master_catalog_readiness_and_enabled_tables", async () => {
            var snapshot = await new PackageLoader(new LocalPackageFileSource(), Registry()).LoadAsync(initial);
            using var session = new TranslationSession(new LocalPackageTranslationStore(snapshot));
            Assert(!session.TryGetMaster(out _), "catalog ready before materialization");
            var catalog = await session.GetMasterAsync(); Assert(session.TryGetMaster(out var active) && ReferenceEquals(active, catalog), "actual ready catalog mismatch");
            Assert(!active.TryResolve("mEpisode", "title", "題名", [], out _), "disabled table exposed hidden KO");
            Assert(active.TryResolve("mEpisode", "title", "題名", ["mEpisode"], out var text) && text == "제목", "enabled table cannot resolve actual catalog");
            Assert(active.Translate("mEpisode", "[{\"title\":\"題名\"}]", ["mEpisode"]).Contains("제목"), "same actual catalog not used by Master rewrite");
        });
        await Check("pre_read_main_thread_lifetime_zero_native_and_glyph_access", async () => {
            var snapshot = await new PackageLoader(new LocalPackageFileSource(), Registry()).LoadAsync(initial);
            var font = new FontService(); var glyphs = new CountingGlyphs(); var donor = new FontDonorHandle(FontService.ApprovedProfile, 77, 88, 1); font.PublishMediumDonor(donor, glyphs);
            var host = new UiModuleHost(snapshot, Registry(), new UiOwnershipService(), fonts: font); var target = new FailingTarget();
            Assert(!host.TryApply(Context(target, mainThread: false)).Written && target.Reads == 0, "offthread target read happened");
            target.CanAccess = false; Assert(!host.TryApply(Context(target)).Written && target.Reads == 0, "closed lifetime target read happened");
            Assert(glyphs.Reads == 0, "offthread/lifetime rejection queried glyphs");
        });
        await Check("restore_read_failure_preserves_lease_and_write_failure_classification", async () => {
            await Task.CompletedTask; var target = new FailingTarget(); var owner = new UiOwnershipService(); owner.OpenEpoch(1);
            Assert(owner.TryWrite(target, new("Core", "fixture.binding", null, "download-label", 1, target.ReadState()), "KO").Written, "setup failed");
            target.FailRead = true; Assert(owner.RestoreTargetDetailed(State().Identity) == RestoreOutcome.RetryableReadFailure && owner.LeaseCount == 1, "read failure dropped lease");
            target.FailRead = false; target.FailWrite = true; Assert(owner.RestoreTargetDetailed(State().Identity) == RestoreOutcome.RetryableWriteFailure && owner.LeaseCount == 1, "setter failure classified external");
            target.FailWrite = false; Assert(owner.RestoreTargetDetailed(State().Identity) == RestoreOutcome.Restored && owner.LeaseCount == 0, "bounded retry failed");
        });
        await Check("single_commit_choice_deferred_and_restore_failure_keeps_old_snapshot", async () => {
            var features = new Features(); var owner = new UiOwnershipService(); var scheduler = new Scheduler(); var coordinator = new RuntimeCoordinator(new PackageLoader(new LocalPackageFileSource(), Registry()), Registry(), owner, scheduler, features);
            Assert((await coordinator.StartAsync(initial)).Committed, "start failed"); var before = coordinator.Selected;
            var target = new FailingTarget(); Assert(coordinator.Host.TryApply(Context(target)).Written, "natural bind failed");
            var prepared = await coordinator.PrepareAsync(changed); Assert(ReferenceEquals(coordinator.Selected, before), "prepare changed selection");
            features.IsChoiceBusy = true; Assert(coordinator.CommitUi(prepared).Reason == "CHOICE_SAFE_BOUNDARY_DEFERRED" && coordinator.Selected == before, "busy Choice committed");
            features.IsChoiceBusy = false; target.FailWrite = true;
            var failed = coordinator.CommitUi(prepared); Assert(!failed.Committed && failed.Ownership.RetryableFailures == 1 && owner.LeaseCount == 1 && coordinator.Selected == before, "restore failure lost old lease/snapshot");
            target.FailWrite = false; Assert(coordinator.CommitUi(prepared).Committed && coordinator.Host.Snapshot == coordinator.Selected && features.SelectedCount == 1 && target.ReadState().Text == "一括ダウンロード", "single selection did not commit after retry");
        });
        await Check("authority_change_restart_required_preserves_session_identity", async () => {
            var features = new Features(); var coordinator = new RuntimeCoordinator(new PackageLoader(new LocalPackageFileSource(), Registry()), Registry(), new UiOwnershipService(), new Scheduler(), features);
            await coordinator.StartAsync(initial); var before = coordinator.Selected; var identity = features.SessionIdentity;
            var prepared = await coordinator.PrepareAsync(authority); var result = coordinator.CommitUi(prepared);
            Assert(!result.Committed && result.Reason == "RESTART_REQUIRED" && coordinator.Selected == before && ReferenceEquals(features.SessionIdentity, identity) && features.SelectedCount == 0, "authority change split legacy/UI state");
        });
        await Check("native_display_cleanup_failure_precedes_all_selection_and_text_restore", async () => {
            var features = new Features(); var owner = new UiOwnershipService(); var coordinator = new RuntimeCoordinator(new PackageLoader(new LocalPackageFileSource(), Registry()), Registry(), owner, new Scheduler(), features);
            await coordinator.StartAsync(initial); var before = coordinator.Selected; var target = new FailingTarget(); coordinator.Host.TryApply(Context(target));
            var prepared = await coordinator.PrepareAsync(changed); features.CanRestoreDisplay = false;
            Assert(coordinator.CommitUi(prepared).Reason == "NATIVE_DISPLAY_RESTORE_DEFERRED" && coordinator.Selected == before
                && owner.LeaseCount == 1 && target.ReadState().Text == "일괄 다운로드", "text/snapshot changed before native display restore success");
            features.CanRestoreDisplay = true; Assert(coordinator.CommitUi(prepared).Committed, "safe display boundary did not retry");
        });
        await Check("uninstall_becomes_busy_after_preflight_keeps_old_epoch_operational", async () => {
            var features = new Features(); var coordinator = new RuntimeCoordinator(new PackageLoader(new LocalPackageFileSource(), Registry()), Registry(), new UiOwnershipService(), new Scheduler(), features);
            await coordinator.StartAsync(initial); var target = new FailingTarget(); coordinator.Host.TryApply(Context(target));
            features.CanUninstall = false; Assert(!coordinator.TryUnload() && !features.Disposed && target.ReadState().Text == "一括ダウンロード", "busy uninstallation disposed or failed restoration");
            Assert(coordinator.Host.TryApply(Context(target)).Written, "failed unload advanced epoch / old writer cannot natural rebind");
        });
        await Check("retryable_hook_uninstall_ack_preserves_selection_and_lifetime_until_success", async () => {
            var features = new Features(); var owner = new UiOwnershipService(); var coordinator = new RuntimeCoordinator(new PackageLoader(Registry()), Registry(), owner, new Scheduler(), features);
            await coordinator.StartAsync(initial); var selected = coordinator.Selected; var target = new FailingTarget(); coordinator.Host.TryApply(Context(target));
            features.CanUninstall = false; Assert(!coordinator.TryUnload() && !features.Disposed && ReferenceEquals(selected, coordinator.Selected), "negative hook acknowledgement disposed active runtime");
            Assert(await coordinator.PrepareAsync(changed) != null && coordinator.Host.TryApply(Context(target)).Written && owner.LeaseCount == 1, "negative uninstall acknowledgement cancelled lifetime or closed old writer");
            features.CanUninstall = true; Assert(coordinator.TryUnload() && features.Disposed && coordinator.Selected == null && owner.LeaseCount == 0 && target.ReadState().Text == State().ExactSource, "uninstall retry failed to restore/dispose exactly after acknowledgement");
        });
        await Check("inflight_worker_cancelled_at_unload_cannot_complete_native_selection", async () => {
            var source = new BlockingSource { BlockRoot = changed }; var features = new Features(); var coordinator = new RuntimeCoordinator(new PackageLoader(source, Registry()), Registry(), new UiOwnershipService(), new Scheduler(), features);
            await coordinator.StartAsync(initial); var work = coordinator.PrepareAsync(changed); await source.Entered.Task;
            Assert(coordinator.TryUnload(), "safe unload failed"); Assert(await work == null && features.SelectedCount == 0, "late worker selected or wrote after lifetime cancelled");
        });
        await Check("latest_request_and_unload_fence_late_prepare", async () => {
            var features = new Features(); var coordinator = new RuntimeCoordinator(new PackageLoader(new LocalPackageFileSource(), Registry()), Registry(), new UiOwnershipService(), new Scheduler(), features);
            await coordinator.StartAsync(initial); var stale = await coordinator.PrepareAsync(changed); var fresh = await coordinator.PrepareAsync(changed);
            Assert(coordinator.CommitUi(stale).Reason == "STALE_OR_CANCELLED_PREPARATION", "stale request selected");
            features.CanUninstall = false; Assert(!coordinator.TryUnload() && !features.Disposed && coordinator.Host != null, "busy master disposed translation/font");
            features.CanUninstall = true; Assert(coordinator.TryUnload() && features.Disposed, "safe unload failed");
            Assert(!coordinator.CommitUi(fresh).Committed, "unloaded lifetime selected late result");
        });
        await Check("persistent_current_previous_corruption_and_incomplete_fallback_no_novel_scan", async () => {
            var source = new CountingSource(); var loader = new PackageLoader(source, Registry()); var store = new LocalImmutablePackageStore(Path.Combine(scratch, "store"), loader);
            var first = await store.StageAsync(initial); store.Select(first.Snapshot); var second = await store.StageAsync(changed, first.Snapshot); store.Select(second.Snapshot);
            Assert(File.Exists(Path.Combine(store.Root, "previous.json")), "previous pointer absent");
            source.NovelReads = 0; var recovered = await store.RecoverAsync(); Assert(recovered.ContentId == second.ContentId && source.NovelReads == 0, "startup rescanned Novel");
            await File.WriteAllTextAsync(Path.Combine(store.Root, "current.json"), "{invalid");
            Directory.CreateDirectory(Path.Combine(store.Root, "packages", ".incomplete-abandoned"));
            recovered = await store.RecoverAsync(); Assert(recovered.ContentId == first.ContentId && source.NovelReads == 0, "corrupt pointer fallback failed or scanned Novel");
            store.Select(second.Snapshot);
            var firstMaster = Path.Combine(first.Root, "master.json"); var firstMasterBytes = await File.ReadAllBytesAsync(firstMaster);
            var secondMaster = Path.Combine(second.Root, "master.json"); var corruptMaster = secondMaster + ".corrupt-fixture-" + Guid.NewGuid().ToString("N");
            // Unchanged immutable payloads may be hardlinked across generations.
            // Replace this generation's file, rather than corrupting the shared inode.
            await File.WriteAllTextAsync(corruptMaster, "corrupt authority"); File.Move(corruptMaster, secondMaster, true);
            Assert(firstMasterBytes.SequenceEqual(await File.ReadAllBytesAsync(firstMaster)), "current corruption fixture changed previous Master bytes");
            recovered = await store.RecoverAsync(); Assert(recovered != null && recovered.ContentId == first.ContentId, "corrupt current authority did not fall back");
            store.Select(recovered.Snapshot);
            var previousPointer = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(store.Root, "previous.json")));
            Assert(previousPointer.RootElement.GetProperty("ContentId").GetString() == first.ContentId, "recover/select demoted valid previous into corrupt current pointer");
        });
        await Check("cold_UI_staging_reuses_validated_current_without_Novel_hash_preserving_generations", async () => {
            var source = new CountingSource(); var loader = new PackageLoader(source, Registry()); var root = Path.Combine(scratch, "cold-stage-store");
            var firstStore = new LocalImmutablePackageStore(root, loader); var first = await firstStore.StageAsync(initial); firstStore.Select(first.Snapshot);
            var nextRoot = await Fixture("cold-new-ui", "재시작 UI 수정"); source.NovelReads = 0;
            var coldStore = new LocalImmutablePackageStore(root, loader); var staged = await coldStore.StageAsync(nextRoot);
            coldStagingNovelReads = source.NovelReads;
            // Restricted Windows sandboxes may deny CreateHardLink; immutable File.Copy is still OS byte I/O.
            Assert(staged.ContentId != first.ContentId && source.NovelReads == 0, "cold UI stage rehashed unchanged Novel or selected old UI identity");
            Assert(staged.Snapshot.Generations == new ComponentGenerations(1, 1, 1, 1) && staged.Snapshot.PreviousIdentity == null, "payload reuse changed cold generation/selection authority");
            var bytes = await staged.Snapshot.Payloads.ReadAsync("novels/43.json"); Assert(Encoding.UTF8.GetString(bytes).Contains("대사"), "hardlink/copy fallback supplied invalid Novel");
        });
        await Check("immutable_reuse_never_trusts_mutable_authoring_or_a_fake_complete_marker", async () => {
            var source = new CountingSource(); var loader = new PackageLoader(source, Registry()); var authoring = await loader.LoadAsync(initial);
            string storeRoot = Path.Combine(scratch, "untrusted-reuse-store"), fake = Path.Combine(storeRoot, "packages", authoring.Identity);
            foreach (var relative in authoring.Payloads.Descriptors.Keys.Append("ui/manifest.json"))
            {
                var destination = Path.Combine(fake, relative.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Path.Combine(initial, relative.Replace('/', Path.DirectorySeparatorChar)), destination);
            }
            await File.WriteAllTextAsync(Path.Combine(fake, "complete.json"), "{}"); var mutablePrevious = await loader.LoadAsync(fake);
            await File.WriteAllTextAsync(Path.Combine(fake, "novels/43.json"), "{\"セリフ\":\"변조\"}");
            var nextRoot = await Fixture("trusted-new-ui", "새 UI"); source.NovelReads = 0;
            var staged = await new LocalImmutablePackageStore(storeRoot, loader).StageAsync(nextRoot, mutablePrevious);
            Assert(source.NovelReads == 1, "fake immutable directory reused unvalidated authoring Novel");
            var bytes = await staged.Snapshot.Payloads.ReadAsync("novels/43.json"); Assert(Encoding.UTF8.GetString(bytes).Contains("대사"), "mutable authoring corruption became selected package bytes");
        });
        await Check("captured_verified_bytes_do_not_reopen_after_external_same_length_mutation", async () => {
            var root = await Fixture("captured"); var source = new CountingSource(); var snapshot = await new PackageLoader(source, Registry()).LoadAsync(root);
            var before = await snapshot.Payloads.ReadAsync("novels/43.json");
            await File.WriteAllTextAsync(Path.Combine(root, "novels/43.json"), "{\"セリフ\":\"변조\"}");
            var after = await snapshot.Payloads.ReadAsync("novels/43.json"); Assert(before.SequenceEqual(after) && source.NovelReads == 1, "verified captured bytes reopened");
        });
        await Check("first_novel_integrity_failure_does_not_publish_session_cache", async () => {
            var root = await Fixture("corrupt-novel"); var snapshot = await new PackageLoader(new LocalPackageFileSource(), Registry()).LoadAsync(root);
            await File.WriteAllTextAsync(Path.Combine(root, "novels/43.json"), "{\"セリフ\":\"변조\"}");
            using var session = new TranslationSession(new LocalPackageTranslationStore(snapshot)); Assert(await session.GetNovelAsync(43) == null && !session.TryGetNovel(43, out _), "corrupt lazy bytes parsed/cache published");
        });
        await Check("typed_fit3_layout_preserved_in_snapshot", async () => {
            var snapshot = await new PackageLoader(new LocalPackageFileSource(), Registry()).LoadAsync(initial); var policy = snapshot.LayoutPolicies["episode-title.fit.v1"];
            Assert(policy.BaseSize == 35 && policy.DescendingSizes.SequenceEqual([34,33,32,31]) && policy.Restore == "owned-only", "layout metadata only validated but not stored");
        });
        await Check("property_slots_restore_retry_and_external_yield", async () => {
            await Task.CompletedTask; var target = new FloatTarget(); var owner = new UiFloatOwnershipService();
            Assert(owner.TryWrite(target, "Fit3", target.ReadState(), 31).Written, "float write failed"); target.FailWrite = true;
            Assert(owner.Restore(State().Identity, "fontSize") == RestoreOutcome.RetryableWriteFailure && owner.LeaseCount == 1, "float setter failure lost lease");
            target.FailWrite = false; target.Value = 29;
            Assert(owner.Restore(State().Identity, "fontSize") == RestoreOutcome.ExternalChanged && target.Value == 29 && owner.LeaseCount == 0, "external float changed overwritten");
        });
        await Check("inactive_alive_target_keeps_lease_and_proven_destroyed_target_releases", async () => {
            await Task.CompletedTask; var target = new FailingTarget(); var owner = new UiOwnershipService(); owner.OpenEpoch(1); owner.TryWrite(target, new("Core", "fixture.binding", null, "download-label", 1, target.ReadState()), "KO");
            target.Active = false; Assert(owner.RestoreTargetDetailed(State().Identity) == RestoreOutcome.RetryableReadFailure && owner.LeaseCount == 1, "inactive alive target called destroyed");
            target.Destroyed = true; Assert(owner.RestoreTargetDetailed(State().Identity) == RestoreOutcome.DestroyedOrRebound && owner.LeaseCount == 0, "proven destroyed target lease leaked");
        });
        await Check("witnessed_same_cell_float_rebind_restores_35_and_retains_retry", async () => {
            await Task.CompletedTask; var target = new FloatTarget(); var owner = new UiFloatOwnershipService(); owner.TryWrite(target, "Fit3", target.ReadState(), 31);
            target.Source = 5; target.FailWrite = true;
            Assert(owner.RestoreAtRebind(State().Identity, "fontSize", target, target.ReadState()) == RestoreOutcome.RetryableWriteFailure && owner.LeaseCount == 1, "rebind failure orphaned style lease");
            target.FailWrite = false; Assert(owner.RestoreAtRebind(State().Identity, "fontSize", target, target.ReadState()) == RestoreOutcome.Restored && target.Value == 35 && owner.LeaseCount == 0, "new source inherited stale31");
        });
        bool candidateAssembly = typeof(RuntimeCoordinator).Assembly.GetName().Name == "GCMod";
        if (candidateAssembly && args.Length >= 5)
        {
            await Check("actual_candidate_compiled_general_selector_paths_and_shape_combinations", async () => {
                var snapshot = await new PackageLoader(Registry()).LoadAsync(args[2]);
                var policyType = typeof(RuntimeCoordinator).Assembly.GetType("GCMod.Candidate.Native.CompiledSelectorPolicy", true);
                var method = policyType.GetMethod("For", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var general = snapshot.Bindings.Values.Where(x => x.Enabled && x.AdapterId is not ("battle.caption.v1" or "battle.command-cost-title.v1" or "story.choice.visual.v1" or "story.annotation-heading.v1" or "story.dictionary-heading.v1")).ToArray();
                foreach (var binding in general)
                {
                    var policy = (SelectorContract)method.Invoke(null, [binding.AdapterId]);
                    Assert(policy != null && policy.Supports(binding.Selector), "code-owned selector missing actual ordered shape: " + binding.Id);
                    Assert(policy.Shapes != null, "finite shape combinations missing: " + binding.AdapterId);
                    var altered = binding.Selector with { SlotKey = "invented-unsupported-slot" };
                    Assert(!policy.Supports(altered), "invented selector combination executable");
                }
                Assert(general.Length > 0, "full candidate general selectors absent");
            });
            await Check("actual_candidate_full_JSON_KO_and_supported_binding_reload_same_DLL_legacy_session", async () => {
                var registry = Registry(); var policyType = typeof(RuntimeCoordinator).Assembly.GetType("GCMod.Candidate.Native.CompiledSelectorPolicy", true);
                var method = policyType.GetMethod("For", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var (id, module) in new[] {("core.static-caption.v1","Core"),("ui4.static-caption.v1","Core"),("core.current-model.v1","Core"),("core.dynamic-caption.v1","Core"),("common.model-label.v1","Consumers"),("common.date-caption.v1","Consumers"),("ui.chrome.v1","Chrome"),("ui.residual-caption.v1","Residuals"),("ui.caption.v1","Captions")})
                    registry.RegisterHostAdapter(id, (SelectorContract)method.Invoke(null, [id]), module);
                var source = new CountingSource(); var features = new SessionFeatures(); var coordinator = new RuntimeCoordinator(new PackageLoader(source, registry), registry, new UiOwnershipService(), new Scheduler(), features);
                var dllBefore = PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(RuntimeCoordinator).Assembly.Location));
                Assert((await coordinator.StartAsync(args[2])).Committed, "full candidate startup failed"); var original = coordinator.Selected;
                var names = await features.Session.GetNamesAsync(); var session = features.Session; var namesTask = features.Session.GetNamesAsync(); source.NovelReads = 0;
                var legacyManifestBytes = await original.Payloads.ReadAsync("manifest.json");
                var legacyManifest = JsonSerializer.Deserialize<GCMod.Manifest>(legacyManifestBytes);
                var storedUnregistered = original.Payloads.Descriptors.Values.Where(x => x.Kind == "novel")
                    .Select(x => int.Parse(x.Path[7..^5])).First(id => !legacyManifest.Novels.ContainsKey(id.ToString()));
                var storedTask = features.Session.GetNovelAsync(storedUnregistered); var storedRows = await storedTask;
                Assert(storedRows != null && ReferenceEquals(storedTask, features.Session.GetNovelAsync(storedUnregistered)) && source.NovelReads == 1, "actual stored unregistered Novel not loaded/reused");
                source.NovelReads = 0;
                var corrected = await coordinator.PrepareAsync(args[3]); Assert(corrected != null && coordinator.Selected == original, "full validation selected prematurely");
                var changedIds = original.Resources.Keys.Where(id => original.Resources[id].Ko != corrected.Snapshot.Resources[id].Ko).ToArray(); Assert(changedIds.Length == 1, "full fixture must change one real KO");
                Assert(coordinator.CommitUi(corrected).Committed && ReferenceEquals(features.Session, session) && ReferenceEquals(namesTask, features.Session.GetNamesAsync()), "UI-only commit reset actual legacy session/cache");
                Assert(ReferenceEquals(storedTask, features.Session.GetNovelAsync(storedUnregistered)), "actual Novel success task reset by UI reload");
                var added = await coordinator.PrepareAsync(args[4]); Assert(added != null, "full supported addition invalid");
                var newIds = added.Snapshot.Bindings.Keys.Except(original.Bindings.Keys).ToArray(); Assert(newIds.Length == 1, "full fixture needs one supported binding addition");
                var binding = added.Snapshot.Bindings[newIds[0]]; Assert(binding.Enabled && registry.CanExecute(binding, true), "actual compiled renderer cannot support new binding");
                Assert(coordinator.CommitUi(added).Committed && ReferenceEquals(features.Session, session) && source.NovelReads == 0, "full UI addition read Novel/reset session");
                var value = added.Snapshot.Resources[binding.ResourceId]; var targetState = new TextTargetState(new(555, 556, "TMPro.TextMeshProUGUI", 1), value.Jp, 3, 4, binding.SourceIdentity.IdentityJson, value.Jp);
                var target = new MemoryTextTarget(targetState); var selector = binding.Selector;
                var facts = new BindingFacts(binding.SourceIdentity.IdentityJson, value.Jp, value.Role, selector.ScopeKey, selector.SlotKey, selector.Segments, 3, 4, selector.Domain, selector.SourceValue, selector.SourceId, selector.Selected,
                    added.Snapshot.Bindings.Values.Where(x => x.Enabled && x.ActionGroup != null && x.ActionGroup == binding.ActionGroup).Select(x => x.SourceIdentity.IdentityJson).ToImmutableArray());
                var evidence = new UiEvidence(MainThread: true, CurrentOwner: true, UniqueTarget: true, DirectTarget: true, NativeControlsUnchanged: true, InputUntouched: true, UniqueValueJoin: true, CallbackActive: true, CurrentControllerScene: true, ProfileStateMatches: true, CurrentCellModel: true, MeshReady: true, CurrentModelOwnsSource: true, NumericModelUnchanged: true);
                Assert(coordinator.Host.TryApply(new(binding.Id, target, facts, evidence, added.Snapshot.Generations.Ui, true)).Written && target.ReadState().Text == value.Ko, "supported full JSON addition cannot natural bind");
                Assert(PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(RuntimeCoordinator).Assembly.Location)) == dllBefore, "actual candidate DLL rebuilt for JSON changes");
                Assert(coordinator.TryUnload(), "full portable candidate unload failed");
            });
        }
        using var corpus = JsonDocument.Parse(await File.ReadAllBytesAsync(args[1]));
        foreach (var row in corpus.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString(); var item = row.Clone();
            await Check("shared_validator_" + id, async () => {
                string root = await Fixture("case-" + id, resourceDoc: item.GetProperty("resources_document"), bindingDoc: item.GetProperty("bindings_document"));
                bool accepted = false; TranslationPackageSnapshot snapshot = null;
                try { snapshot = await new PackageLoader(new LocalPackageFileSource(), Registry()).LoadAsync(root); accepted = true; }
                catch (Exception ex) when (ex is PackageValidationException or JsonException or KeyNotFoundException or InvalidOperationException) { }
                bool expected = item.GetProperty("expected").GetProperty("accept").GetBoolean(); Assert(accepted == expected, "Python/C# accept classification differs");
                if (accepted)
                {
                    var registry = Registry(); bool optional = snapshot.Bindings.Values.Any(x => !registry.CanExecute(x, true));
                    string capability = item.GetProperty("expected").GetProperty("capability").GetString(); Assert(optional == (capability == "optional_unavailable"), "optional capability classification differs");
                }
            });
        }
        var report = new { schema = "gc-ng02-runtime-host-v1", status = failures == 0 ? "PASS" : "FAIL", count = results.Count, failures, tests = results,
            game_runtime = "NOT_RUN", native_game_hooks = "NOT_RUN", actual_legacy_session_code = true,
            execution_mode = candidateAssembly ? "actual_candidate_cpu" : "nextgen_managed_cpu", tested_assembly = typeof(RuntimeCoordinator).Assembly.Location,
            game_interop_assembly_loaded = AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == "Assembly-CSharp"),
            cold_UI_staging_novel_reads = coldStagingNovelReads,
            cold_UI_staging_reuse = coldStagingNovelReads == 0 ? "immutable_link_or_OS_copy_no_application_hash_parse" : "verified_authoring_fallback",
            assembly_sha256 = PackageLoader.Hash(await File.ReadAllBytesAsync(typeof(RuntimeCoordinator).Assembly.Location)) };
        await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(report, options) + "\n"); Console.WriteLine($"RUNTIME {(failures == 0 ? "PASS" : "FAIL")} {results.Count - failures}/{results.Count}"); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    sealed class CountingSource : IPackageFileSource
    {
        readonly LocalPackageFileSource source = new(); public int NovelReads;
        public async ValueTask<byte[]> ReadAsync(string root, string path, CancellationToken cancellationToken = default)
        { if (path.StartsWith("novels/")) NovelReads++; return await source.ReadAsync(root, path, cancellationToken); }
    }
    sealed class Scheduler : IRuntimeMainThreadScheduler
    {
        public bool IsMainThread => true;
        public Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(callback()); }
    }
    sealed class Features : IRuntimeFeatureProvider, IRuntimeDisplayBoundary, IRuntimeUnloadBoundary
    {
        public IMasterTranslationView MasterView => MasterTranslationView.Empty; public bool IsChoiceBusy { get; set; }
        public bool CanUninstall = true; public bool Disposed; public int SelectedCount; public readonly object SessionIdentity = new();
        public bool CanRestoreDisplay = true; public bool CanUnload => true;
        public void Initialize(TranslationPackageSnapshot snapshot, UiModuleHost host) { }
        public void Selected(TranslationPackageSnapshot snapshot, UiModuleHost host) { SelectedCount++; }
        public bool TryUninstall() => CanUninstall;
        public bool TryRestoreDisplay() => CanRestoreDisplay;
        public void Dispose() { Disposed = true; }
    }
    sealed class FailingTarget(TextTargetState initial = null) : IUiTextTarget, IUiTargetAccessGate
    {
        TextTargetState state = initial ?? State(); public bool FailRead, FailWrite, Destroyed; public bool Active = true; public int Reads; public bool CanAccess { get; set; } = true;
        public TextTargetState ReadState() { Reads++; if (FailRead) throw new IOException("fixture read"); return state with { Active = Active, DestroyedOrRebound = Destroyed }; }
        public bool TryWriteText(TextTargetState expected, string value) { if (FailWrite) throw new IOException("fixture setter"); if (state != expected) return false; state = state with { Text = value }; return true; }
    }
    sealed class FloatTarget : IUiFloatTarget, IUiTargetAccessGate
    {
        public float Value = 35; public long Source = 4; public bool FailWrite; public bool CanAccess => true;
        public FloatTargetState ReadState() => new(State().Identity, "fontSize", Value, 3, Source, "protected-native-style");
        public bool TryWriteFloat(FloatTargetState expected, float value) { if (FailWrite) throw new IOException("fixture setter"); if (expected != ReadState()) return false; Value = value; return true; }
    }
    sealed class CountingGlyphs : IFontGlyphProvider
    { public int Reads; public bool HasGlyph(FontDonorHandle donor, int scalar) { Reads++; return true; } }
    sealed class MutableMaster : IMasterTranslationView
    {
        public string[] EnabledTables = ["mTextConfig"];
        readonly MasterDataTranslator actual = new(new Dictionary<string, Dictionary<string, Dictionary<string, string>>> {
            ["mTextConfig"] = new() { ["ml_value[]"] = new() { ["一括ダウンロード"] = "활성 마스터" } } });
        public bool TryResolve(MasterRef reference, string role, out MasterValue value)
        {
            value = new("", "actual-catalog", 1);
            if (!actual.TryResolve(reference.Table, reference.Field, reference.ExactJp, EnabledTables, out var ko)) return false;
            value = new(ko, "actual-catalog", 1); return true;
        }
    }
    sealed class BlockingSource : IPackageFileSource
    {
        public string BlockRoot; public TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<byte[]> ReadAsync(string root, string path, CancellationToken cancellationToken = default)
        {
            if (root == BlockRoot) { Entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, cancellationToken); }
            return await new LocalPackageFileSource().ReadAsync(root, path, cancellationToken);
        }
    }
    sealed class SessionFeatures : IRuntimeFeatureProvider
    {
        public TranslationSession Session;
        public IMasterTranslationView MasterView => MasterTranslationView.Empty;
        public bool IsChoiceBusy => false;
        public void Initialize(TranslationPackageSnapshot snapshot, UiModuleHost host) { Session = new(new LocalPackageTranslationStore(snapshot)); }
        public void Selected(TranslationPackageSnapshot snapshot, UiModuleHost host) { }
        public bool TryUninstall() => true;
        public void Dispose() { Session.Dispose(); }
    }
}
