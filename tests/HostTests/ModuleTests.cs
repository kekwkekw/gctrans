using GCMod.Nextgen;

namespace HostTests;
internal static partial class Program
{
    static readonly UiEvidence Healthy = new(MainThread: true, CurrentOwner: true, UniqueTarget: true, DirectTarget: true,
        NativeControlsUnchanged: true, InputUntouched: true, UniqueValueJoin: true, CallbackActive: true,
        CurrentControllerScene: true, ProfileStateMatches: true, CurrentCellModel: true, MeshReady: true,
        CurrentModelOwnsSource: true, NumericModelUnchanged: true);
    static MemoryTextTarget TargetFor(TranslationPackageSnapshot snapshot, string id, ulong pointer = 123)
    {
        var binding = snapshot.Bindings[id];
        return new(State(pointer, binding.SourceIdentity.ExactJp, identity: binding.SourceIdentity.IdentityJson));
    }
    static UiBindContext ContextFor(TranslationPackageSnapshot snapshot, string id, IUiTextTarget target, UiEvidence? evidence = null, bool hostFixture = true)
    {
        var binding = snapshot.Bindings[id]; var resource = snapshot.Resources[binding.ResourceId]; var selector = binding.Selector;
        var state = target.ReadState();
        return new(id, target, new(binding.SourceIdentity.IdentityJson, resource.Jp, resource.Role, selector.ScopeKey,
            selector.SlotKey, selector.Segments, state.OwnerGeneration, state.SourceGeneration, selector.Domain,
            selector.SourceValue, selector.SourceId, selector.Selected), evidence ?? Healthy, snapshot.Generations.Ui, hostFixture);
    }
    static async Task TestModuleBindings(TranslationPackageSnapshot baseline, TranslationPackageSnapshot corrected,
        TranslationPackageSnapshot added, CountedSource provider, string dllBefore)
    {
        var registry = HostRegistry(); var ownership = new UiOwnershipService();
        var host = new UiModuleHost(baseline, registry, ownership);
        var target = TargetFor(baseline, "binding.download");
        var initial = ContextFor(baseline, "binding.download", target);
        Check("Core_exact_source_direct_current_owner_apply", () => Assert(host.TryApply(initial).Written && target.ReadState().Text == "일괄 다운로드", "Core did not apply exact resource"));
        Check("repeated_bind_no_file_network_IO_or_duplicate_write", () => {
            int reads = provider.Reads;
            for (int i = 0; i < 5000; i++) host.TryApply(ContextFor(baseline, "binding.download", target));
            Assert(provider.Reads == reads && target.Writes == 1, "bind reopened package or wrote unchanged value");
        });
        Check("JSON_KO_reload_restores_old_then_natural_rebind", () => {
            Assert(host.SelectSnapshot(corrected).Restored == 1 && target.ReadState().Text == "一括ダウンロード", "old KO not conditionally restored");
            Assert(!host.TryApply(initial).Written, "old callback survived epoch switch");
            Assert(host.TryApply(ContextFor(corrected, "binding.download", target)).Written && target.ReadState().Text == "다운로드 전체", "new KO not supplied at natural bind");
            Assert(Hash(File.ReadAllBytes(typeof(PackageLoader).Assembly.Location)) == dllBefore, "module rebind changed DLL");
        });
        Check("reject_divergent_snapshot_with_colliding_generation", () => {
            bool rejected = false; try { host.SelectSnapshot(added); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && ReferenceEquals(host.Snapshot, corrected), "same generation different component content replaced active snapshot");
        });
        var authorityRoot = await Fixture("same-ui-divergent-master");
        await File.WriteAllTextAsync(Path.Combine(authorityRoot, "master.json"), "{\"mTextConfig\":{\"ml_value[]\":{\"カーマイン\":\"다른 값\"}}}");
        await RefreshManifest(authorityRoot, "same-ui-divergent-master");
        var authoritySnapshot = await new PackageLoader(provider, registry).LoadAsync(authorityRoot);
        Check("reject_same_UI_independent_authority_with_equal_generation", () => {
            var authorityHost = new UiModuleHost(baseline, registry, new UiOwnershipService());
            Assert(authoritySnapshot.Generations.Master == baseline.Generations.Master && authoritySnapshot.Generations.Ui == baseline.Generations.Ui, "fixture generations not colliding");
            bool rejected = false; try { authorityHost.SelectSnapshot(authoritySnapshot); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && ReferenceEquals(authorityHost.Snapshot, baseline), "same UI changed authority silently selected");
        });
        var newTarget = TargetFor(added, "binding.new");
        var addedHost = new UiModuleHost(added, registry, new UiOwnershipService());
        Check("supported_added_JSON_binding_delivers_new_text", () => Assert(addedHost.TryApply(ContextFor(added, "binding.new", newTarget)).Written && newTarget.ReadState().Text == "새 표시", "JSON-only binding cannot apply"));
        var disabledRoot = await Fixture("disabled-rule", (_, b) => b["bindings"]![0]!["enabled"] = false);
        var disabled = await new PackageLoader(provider, registry).LoadAsync(disabledRoot, previous: corrected);
        Check("disabled_rule_removes_old_owned_KO", () => {
            Assert(host.SelectSnapshot(disabled).Restored == 1 && target.ReadState().Text == "一括ダウンロード", "disabled rule left old KO");
            Assert(!host.TryApply(ContextFor(disabled, "binding.download", target)).Written, "disabled binding writes");
        });
        Check("Consumers_exact_active_Master_ref_and_unique_model_join", () => {
            var elementTarget = TargetFor(baseline, "binding.element", 456);
            var consumer = new UiModuleHost(baseline, registry, new UiOwnershipService());
            Assert(!consumer.TryApply(ContextFor(baseline, "binding.element", elementTarget, Healthy with { UniqueValueJoin = false })).Written, "ambiguous filter join wrote");
            Assert(consumer.TryApply(ContextFor(baseline, "binding.element", elementTarget)).Written && elementTarget.ReadState().Text == "카민", "active exact MasterRef not used");
        });
        Check("missing_Master_ref_only_that_binding_noop", () => {
            var isolated = new UiModuleHost(baseline, registry, new UiOwnershipService(), masterOverride: MasterTranslationView.Empty);
            var element = TargetFor(baseline, "binding.element", 456); var direct = TargetFor(baseline, "binding.download", 789);
            Assert(!isolated.TryApply(ContextFor(baseline, "binding.element", element)).Written && element.ReadState().Text == "カーマイン", "missing MasterRef used hidden fallback");
            Assert(isolated.TryApply(ContextFor(baseline, "binding.download", direct)).Written, "missing MasterRef stopped independent module");
        });
        foreach (var (name, evidence) in new[] {
            ("main_thread", Healthy with { MainThread = false }), ("current_owner", Healthy with { CurrentOwner = false }),
            ("unique_target", Healthy with { UniqueTarget = false }), ("direct_target", Healthy with { DirectTarget = false }),
            ("native_control", Healthy with { NativeControlsUnchanged = false }), ("input_control", Healthy with { InputUntouched = false }) })
        {
            Check("module_reject_missing_" + name, () => {
                var fresh = TargetFor(baseline, "binding.download"); var localHost = new UiModuleHost(baseline, registry, new UiOwnershipService());
                Assert(!localHost.TryApply(ContextFor(baseline, "binding.download", fresh, evidence)).Written && fresh.Writes == 0, "unsafe module evidence wrote");
            });
        }
        Check("host_fixture_capability_cannot_execute_native_context", () => {
            var fresh = TargetFor(baseline, "binding.download");
            Assert(!new UiModuleHost(baseline, registry, new UiOwnershipService()).TryApply(ContextFor(baseline, "binding.download", fresh, hostFixture: false)).Written, "host fixture capability exposed in native context");
        });
        foreach (var (module, adapter, type) in new[] { ("Chrome", "ui.chrome.v1", "caption"), ("Residuals", "ui.residual-caption.v1", "profile"), ("Captions", "ui.caption.v1", "exchange") })
        {
            var localRegistry = HostRegistry();
            localRegistry.RegisterHostAdapter(adapter, SelectorContract.Host(["main-menu"], ["download-label"], [type]), module);
            var moduleRoot = await Fixture("module-" + module, (_, b) => {
                b["bindings"]![0]!["adapter_id"] = adapter; b["bindings"]![0]!["selector"]!["type"] = type;
                if (type == "profile") b["bindings"]![0]!["selector"]!["selected"] = false;
                if (type == "exchange") { b["bindings"]![0]!["selector"]!["lifetime"] = "cell"; b["bindings"]![0]!["selector"]!["overflow_policy"] = "intrinsic-only"; }
            });
            var moduleSnapshot = await new PackageLoader(provider, localRegistry).LoadAsync(moduleRoot);
            Check(module + "_current_scene_required", () => {
                var fresh = TargetFor(moduleSnapshot, "binding.download"); var moduleHost = new UiModuleHost(moduleSnapshot, localRegistry, new UiOwnershipService());
                Assert(!moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh, Healthy with { CurrentControllerScene = false })).Written, "stale scene writes");
            });
            Check(module + "_current_qualified_caption_applies", () => {
                var fresh = TargetFor(moduleSnapshot, "binding.download"); var moduleHost = new UiModuleHost(moduleSnapshot, localRegistry, new UiOwnershipService());
                Assert(moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh, Healthy with { Visibility = ViewportVisibility.Partial })).Written, "qualified caption rejected natural viewport clipping");
            });
            if (type == "profile")
                Check("profile_normal_highlight_not_overstrict_geometry_failure", () => {
                    var fresh = TargetFor(moduleSnapshot, "binding.download"); var moduleHost = new UiModuleHost(moduleSnapshot, localRegistry, new UiOwnershipService());
                    Assert(!moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh, Healthy with { ProfileStateMatches = false })).Written, "wrong profile state wrote");
                    Assert(moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh)).Written, "correct unselected state blocked by normal appearance");
                });
            if (type == "exchange")
            {
                Check("exchange_numeric_current_model_protected", () => {
                    var fresh = TargetFor(moduleSnapshot, "binding.download"); var moduleHost = new UiModuleHost(moduleSnapshot, localRegistry, new UiOwnershipService());
                    Assert(!moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh, Healthy with { NumericModelUnchanged = false })).Written, "changed numeric model wrote");
                    Assert(!moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh, Healthy with { IntrinsicOverflow = true })).Written, "intrinsic overflow treated as viewport clipping");
                });
                Check("exchange_before_cell_reuse_self_only_restore", () => {
                    var fresh = TargetFor(moduleSnapshot, "binding.download"); var moduleHost = new UiModuleHost(moduleSnapshot, localRegistry, new UiOwnershipService());
                    moduleHost.TryApply(ContextFor(moduleSnapshot, "binding.download", fresh));
                    Assert(moduleHost.BeforeCellReuse(fresh.ReadState().Identity) && fresh.ReadState().Text == "一括ダウンロード", "cell cleanup failed");
                    fresh.GameWrite("現在のモデル値 24");
                    Assert(!moduleHost.BeforeCellReuse(fresh.ReadState().Identity) && fresh.ReadState().Text == "現在のモデル値 24", "cleanup overwrote new game value");
                });
            }
        }
        await CheckAsync("historical_paths_receipts_absent_portable_root_load", async () => {
            var portable = await Fixture("portable-independent-root");
            Assert(!Directory.EnumerateFiles(portable, "*", SearchOption.AllDirectories).Any(x => x.Contains("receipt", StringComparison.OrdinalIgnoreCase) || x.Contains("FrozenReviewedMaster", StringComparison.Ordinal)), "portable fixture contains historical inputs");
            var portableSnapshot = await new PackageLoader(provider, registry).LoadAsync(portable);
            var fresh = TargetFor(portableSnapshot, "binding.download");
            Assert(new UiModuleHost(portableSnapshot, registry, new UiOwnershipService()).TryApply(ContextFor(portableSnapshot, "binding.download", fresh)).Written, "portable package required historical path");
        });
        var sortRoot = await Fixture("temporary-sort", (_, b) => {
            b["bindings"]![1]!["selector"]!["source_id"] = 41;
            b["bindings"]![1]!["selector"]!["lifetime"] = "callback";
        });
        var sortSnapshot = await new PackageLoader(provider, registry).LoadAsync(sortRoot);
        Check("temporary_sort_model_only_during_natural_callback", () => {
            var sortTarget = TargetFor(sortSnapshot, "binding.element");
            var sortHost = new UiModuleHost(sortSnapshot, registry, new UiOwnershipService());
            Assert(!sortHost.TryApply(ContextFor(sortSnapshot, "binding.element", sortTarget, Healthy with { CallbackActive = false })).Written, "temporary sort value consumed after callback");
            Assert(sortHost.TryApply(ContextFor(sortSnapshot, "binding.element", sortTarget)).Written, "current callback sort join rejected");
        });
        var dateRegistry = HostRegistry();
        dateRegistry.RegisterHostAdapter("common.date-caption.v1", SelectorContract.Host(["main-menu"], ["download-label"], ["date"]), "Consumers");
        var dateRoot = await Fixture("finite-date", (r, b) => {
            r["resources"]![0]!["jp"] = "最終更新日：----/--/-- --:--";
            r["resources"]![0]!["ko"] = "마지막 업데이트: ----/--/-- --:--";
            b["bindings"]![0]!["source_identity"]!["exact_jp"] = "最終更新日：----/--/-- --:--";
            b["bindings"]![2]!["source_identity"]!["exact_jp"] = "最終更新日：----/--/-- --:--";
            b["bindings"]![0]!["adapter_id"] = "common.date-caption.v1";
            b["bindings"]![0]!["selector"]!["type"] = "date";
            b["bindings"]![0]!["selector"]!["template_id"] = "last-update-empty-v1";
        });
        var dateSnapshot = await new PackageLoader(provider, dateRegistry).LoadAsync(dateRoot);
        Check("finite_date_exact_literal_preserves_empty_placeholder", () => {
            var fresh = TargetFor(dateSnapshot, "binding.download"); var dateHost = new UiModuleHost(dateSnapshot, dateRegistry, new UiOwnershipService());
            Assert(dateHost.TryApply(ContextFor(dateSnapshot, "binding.download", fresh)).Written && fresh.ReadState().Text.EndsWith("----/--/-- --:--", StringComparison.Ordinal), "finite date placeholder changed");
        });
        Check("unsupported_date_variant_rule_only_noop", () => {
            var fresh = TargetFor(dateSnapshot, "binding.download"); var dateHost = new UiModuleHost(dateSnapshot, dateRegistry, new UiOwnershipService());
            fresh.Recycle(fresh.ReadState() with { Text = "最終ダウンロード日時：2026/10/11 12:34", ExactSource = "最終ダウンロード日時：2026/10/11 12:34" });
            Assert(!dateHost.TryApply(ContextFor(dateSnapshot, "binding.download", fresh)).Written && fresh.Writes == 0, "unsupported date parsed or changed instead of no-op");
        });
        var forgedDateRoot = await Fixture("date-matching-source-outside-finite-contract", (r, b) => {
            const string jp = "最終更新日：2026/10/11 12:34";
            r["resources"]![0]!["jp"] = jp; r["resources"]![0]!["ko"] = "마지막 업데이트: 2026/10/11 12:34";
            b["bindings"]![0]!["source_identity"]!["exact_jp"] = jp;
            b["bindings"]![2]!["source_identity"]!["exact_jp"] = jp;
            b["bindings"]![0]!["adapter_id"] = "common.date-caption.v1";
            b["bindings"]![0]!["selector"]!["type"] = "date";
            b["bindings"]![0]!["selector"]!["template_id"] = "last-update-empty-v1";
        });
        var forgedDateSnapshot = await new PackageLoader(provider, dateRegistry).LoadAsync(forgedDateRoot);
        Check("matching_JSON_source_cannot_expand_compiled_finite_date_policy", () => {
            var fresh = TargetFor(forgedDateSnapshot, "binding.download");
            Assert(!new UiModuleHost(forgedDateSnapshot, dateRegistry, new UiOwnershipService()).TryApply(ContextFor(forgedDateSnapshot, "binding.download", fresh)).Written && fresh.Writes == 0,
                "matching JSON source expanded unsupported finite date policy");
        });
    }
}
