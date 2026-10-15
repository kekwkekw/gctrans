using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using GCMod.Nextgen;
using GCMod.Candidate.Native;

var results = new List<object>(); int failed = 0;
void Check(string name, Action action)
{
    try { action(); results.Add(new { name, status = "PASS", level = "ACTUAL_CANDIDATE_CPU_POLICY_WITH_SYNTHETIC_INPUTS" }); }
    catch (Exception error) { failed++; results.Add(new { name, status = "FAIL", reason = error.Message }); }
}
void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

var click = new AnnotationClickProof(5, 10, 20, 30, Environment.CurrentManagedThreadId, "7", 100, true);
var record = new AnnotationRecordProof(click, 50, 7, "keyword", "body", "ruby", false);
var delivered = new AnnotationDeliveredRecord(50, 7, "keyword", "body", "ruby");

Check("strict_async_missing_click_competing_private_conflict_remain_unproven", () =>
{
    Require(AnnotationDeliveryProofPolicy.Select([], delivered).Reason == "ANNOTATION_CURRENT_CLICK_REQUIRED", "missing_click_promoted");
    Require(AnnotationDeliveryProofPolicy.Select([record with { DeliveredAsynchronously = true }], delivered).Reason == "ANNOTATION_ASYNC_FACTORY_CLICK_UNPROVEN", "async_promoted");
    Require(AnnotationDeliveryProofPolicy.Select([record, record with { Click = click with { Generation = 6 } }], delivered).Reason == "ANNOTATION_COMPETING_CLICK_HELD", "competing_click_promoted");
    Require(AnnotationDeliveryProofPolicy.Select([record], delivered, record with { Description = "changed" }).Reason == "ANNOTATION_PRIVATE_FACTORY_RECORD_CONFLICT_HELD", "private_conflict_promoted");
});
Check("unsafe_missing_and_shared_factory_remain_incompatible", () =>
{
    var signature = new NativeMethodContract("PopupFactory", "Create", false, "System.Void", ImmutableArray.Create(new NativeParameter("Popup")), "win64");
    var observation = new NativeMethodObservation(signature, 1, true, true, 1, false, "PopupFactory", "win64");
    var policy = new CompatibilityService();
    Require(policy.Assess(signature, observation).Available, "valid_cpu_contract_rejected");
    foreach (var bad in new[] { observation with { AliasCount = 0 }, observation with { AliasCount = 2 }, observation with { SharedBody = true }, observation with { CurrentExecutableTarget = false }, observation with { NativeDeclaringClass = "Other" }, observation with { Abi = "unknown" } })
        Require(!policy.Assess(signature, bad).Available, "unsafe_factory_admitted");
});
Check("header_external_writer_is_preserved", () =>
{
    var state = new TextTargetState(new(10, 20, "TMP", 30), "辞典", 30, 30, "heading", "辞典");
    var target = new CpuTarget(state); var own = new UiOwnershipService(); own.OpenEpoch(1);
    Require(own.TryWrite(target, new("Story", "annotation", null, "dictionary-heading", 1, state), "용어 사전").Written, "initial_write");
    target.State = target.State with { Text = "external writer" };
    Require(own.RestoreTargetDetailed(state.Identity) == RestoreOutcome.ExternalChanged && target.State.Text == "external writer" && own.LeaseCount == 0, "external_writer_overwritten");
});
Check("header_restore_failure_retains_lease_and_transition_selection", () =>
{
    var state = new TextTargetState(new(10, 20, "TMP", 30), "辞典", 30, 30, "heading", "辞典");
    var target = new CpuTarget(state); var own = new UiOwnershipService(); own.OpenEpoch(1);
    Require(own.TryWrite(target, new("Story", "annotation", null, "dictionary-heading", 1, state), "용어 사전").Written, "initial_write");
    target.WriteFails = true;
    Require(own.RestoreTargetDetailed(state.Identity) == RestoreOutcome.RetryableWriteFailure && own.LeaseCount == 1, "failed_write_lost_lease");
    Require(!own.Transition(1, 2).Committed && own.LeaseCount == 1 && target.State.Text == "용어 사전", "failed_restore_committed_epoch");
    target.WriteFails = false; target.ReadFails = true;
    Require(own.RestoreTargetDetailed(state.Identity) == RestoreOutcome.RetryableReadFailure && own.LeaseCount == 1, "failed_read_lost_lease");
    target.ReadFails = false;
    Require(own.Transition(1, 2).Committed && own.LeaseCount == 0 && target.State.Text == "辞典", "safe_retry_failed");
    Require(!own.TryWrite(target, new("Story", "annotation", null, "dictionary-heading", 1, state), "용어 사전").Written, "stale_ui_epoch_wrote");
});
Check("header_owner_reuse_cannot_restore_previous_generation", () =>
{
    var state = new TextTargetState(new(10, 20, "TMP", 30), "辞典", 30, 30, "heading", "辞典");
    var target = new CpuTarget(state); var own = new UiOwnershipService(); own.OpenEpoch(1);
    Require(own.TryWrite(target, new("Story", "annotation", null, "dictionary-heading", 1, state), "용어 사전").Written, "initial_write");
    target.State = state with { Identity = state.Identity with { Lifetime = 31 }, OwnerGeneration = 31, Text = "new owner" };
    Require(own.RestoreTargetDetailed(state.Identity) == RestoreOutcome.DestroyedOrRebound && target.State.Text == "new owner", "old_owner_restored_into_reuse");
});
Check("actual_main_thread_lifetime_gate_precedes_target_and_setting_reads", () =>
{
    var gate = new GameThreadGate(); gate.Capture(); int settings = 0;
    Require(SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess, () => { settings++; return true; }), "captured_thread_rejected");
    var target = new CpuTarget(new(new(10, 20, "TMP", 30), "辞典", 30, 30, "heading", "辞典")) { Access = () => gate.CanAccess };
    var own = new UiOwnershipService(); own.OpenEpoch(1);
    var outcome = Task.Run(() =>
    {
        bool eligible = SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess, () => { settings++; return true; });
        var write = own.TryWrite(target, new("Story", "annotation", null, "dictionary-heading", 1, target.State), "용어 사전");
        return (eligible, write);
    }).GetAwaiter().GetResult();
    Require(!outcome.eligible && outcome.write.Reason == "NATIVE_ACCESS_GATE_CLOSED" && target.Reads == 0 && target.Writes == 0 && settings == 1, "offthread_native_read_or_write");
    gate.Close(); Require(!SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess, () => { settings++; return true; }) && settings == 1, "closed_lifetime_reads");
});

FactoryHeadingObservation Observation() => new(
    Owner: new(100, 101, "Gc.NovelKeyWordPopup"), OwnerGeneration: 200, UiGeneration: 300, OwnerActive: true,
    Parts: new(110, 111, "Gc.NovelKeyWordDetailParts"), Close: new(120, 121, "InertButton"), Header: new(130, 131, "TMPro.TextMeshProUGUI"),
    Localizer: new(140, 141, "Gc.Localizer.TextLocalizer"), Title: new(150, 151, "TMPro.TextMeshProUGUI"), Body: new(160, 161, "TMPro.TextMeshProUGUI"), Ruby: new(170, 171, "TMPro.TextMeshProUGUI"),
    PartsOwner: 100, CloseOwner: 100, HeaderOwner: 100, TitleOwner: 100, BodyOwner: 100, RubyOwner: 100,
    PartsPath: FactoryHeadingPolicy.PartsPath, ClosePath: FactoryHeadingPolicy.ClosePath, HeaderPath: FactoryHeadingPolicy.HeaderPath,
    LocalizerTarget: 130, LocalizerType: 0, LocalizerKey: "", HeaderText: "辞典", Keyword: "keyword", Description: "body", RubyText: "ruby");
Check("factory_header_is_independent_of_absent_click_and_async_strict_join", () =>
{
    var observed = Observation();
    Require(FactoryHeadingPolicy.TryCapture(delivered, observed, null, out var header, out _) && header != null, "valid_factory_not_captured");
    Require(AnnotationDeliveryProofPolicy.Select([], delivered).Record == null, "missing_click_became_proven");
    Require(AnnotationDeliveryProofPolicy.Select([record with { DeliveredAsynchronously = true }], delivered).Record == null, "async_became_proven");
    Require(FactoryHeadingPolicy.IsCurrent(header!, observed, null, out _), "strict_unproven_disabled_header");
    Require(typeof(FactoryHeadingEvidence).GetProperties().All(p => p.PropertyType != typeof(AnnotationClickProof) && p.PropertyType != typeof(AnnotationRecordProof)), "factory_evidence_cast_as_click_proof");
});
Check("competing_same_id_and_private_conflict_do_not_poison_independent_heading", () =>
{
    var observed = Observation();
    Require(AnnotationDeliveryProofPolicy.Select([record, record with { Click = click with { Generation = 6 } }], delivered).Record == null, "competing_promoted");
    Require(AnnotationDeliveryProofPolicy.Select([record], delivered, record with { Ruby = "other" }).Record == null, "private_conflict_promoted");
    Require(FactoryHeadingPolicy.TryCapture(delivered, observed, null, out var header, out _) && FactoryHeadingPolicy.IsCurrent(header!, observed, null, out _), "unrelated_strict_join_poisoned_owner_heading");
});
Check("factory_requires_exact_native_classes_unique_targets_and_real_record_identity", () =>
{
    var observed = Observation();
    foreach (var bad in new[] {
        observed with { Owner = observed.Owner with { NativeClass = "Gc.Database.DatabaseTopController" } },
        observed with { Header = observed.Header with { NativeClass = "OtherText" } },
        observed with { Localizer = observed.Localizer with { NativeClass = "OtherLocalizer" } },
        observed with { Header = observed.Title, LocalizerTarget = observed.Title.Pointer },
        observed with { Header = observed.Body, LocalizerTarget = observed.Body.Pointer },
        observed with { Header = observed.Ruby, LocalizerTarget = observed.Ruby.Pointer },
        observed with { Owner = observed.Owner with { Pointer = 0 } },
        observed with { Header = observed.Header with { Instance = 0 } } })
        Require(!FactoryHeadingPolicy.TryCapture(delivered, bad, null, out _, out _), "invalid_class_or_target_admitted");
    Require(!FactoryHeadingPolicy.TryCapture(delivered with { Pointer = 0 }, observed, null, out _, out _), "missing_actual_record_admitted");
});
Check("factory_requires_serialized_path_direct_owner_and_direct_localizer", () =>
{
    var observed = Observation();
    foreach (var bad in new[] {
        observed with { PartsOwner = 999 }, observed with { CloseOwner = 999 }, observed with { HeaderOwner = 999 },
        observed with { TitleOwner = 999 }, observed with { BodyOwner = 999 }, observed with { RubyOwner = 999 },
        observed with { PartsPath = "other" }, observed with { ClosePath = "other" }, observed with { HeaderPath = "other" },
        observed with { LocalizerTarget = observed.Body.Pointer }, observed with { LocalizerType = 1 }, observed with { LocalizerKey = "foreign" } })
        Require(!FactoryHeadingPolicy.TryCapture(delivered, bad, null, out _, out _), "invalid_owner_path_or_localizer_admitted");
});
Check("factory_current_title_body_ruby_are_compared_without_being_rewritten", () =>
{
    var observed = Observation();
    foreach (var bad in new[] { observed with { Keyword = "other" }, observed with { Description = "other" }, observed with { RubyText = "other" } })
        Require(!FactoryHeadingPolicy.TryCapture(delivered, bad, null, out _, out _), "changed_content_admitted");
    var translated = delivered with { Keyword = "현재 제목", Description = "현재 본문", Ruby = "현재 루비" };
    var translatedObserved = observed with { Keyword = translated.Keyword, Description = translated.Description, RubyText = translated.Ruby };
    Require(FactoryHeadingPolicy.TryCapture(translated, translatedObserved, null, out var captured, out _) && captured!.Record == translated && captured.Captured == translatedObserved, "actual_master_translated_values_reprojected");
});
Check("factory_delayed_owner_instance_ui_generation_direct_target_and_content_changes_reject", () =>
{
    var observed = Observation(); Require(FactoryHeadingPolicy.TryCapture(delivered, observed, null, out var captured, out _), "capture");
    foreach (var bad in new[] {
        observed with { Owner = observed.Owner with { Instance = 102 } }, observed with { OwnerGeneration = 201 }, observed with { UiGeneration = 301 },
        observed with { OwnerActive = false }, observed with { Header = observed.Header with { Instance = 132 } },
        observed with { Parts = observed.Parts with { Pointer = 112 } }, observed with { Close = observed.Close with { Instance = 122 } },
        observed with { Localizer = observed.Localizer with { Instance = 142 } }, observed with { Title = observed.Title with { Instance = 152 } },
        observed with { Body = observed.Body with { Instance = 162 } }, observed with { Ruby = observed.Ruby with { Instance = 172 } },
        observed with { Description = "late changed body" } })
        Require(!FactoryHeadingPolicy.IsCurrent(captured!, bad, null, out _), "stale_pending_admitted");
});
Check("factory_accepts_only_exact_JP_or_this_owners_last_written_header", () =>
{
    var observed = Observation(); Require(FactoryHeadingPolicy.TryCapture(delivered, observed, null, out var captured, out _), "capture");
    var ko = observed with { HeaderText = "용어 사전" };
    Require(!FactoryHeadingPolicy.IsCurrent(captured!, ko, null, out _), "arbitrary_KO_treated_as_owned");
    Require(FactoryHeadingPolicy.IsCurrent(captured!, ko, "용어 사전", out _), "own_last_write_rejected");
    Require(!FactoryHeadingPolicy.IsCurrent(captured!, ko with { HeaderText = "external" }, "용어 사전", out _), "external_write_admitted");
});
var donor = new FontDonorHandle(FontService.ApprovedProfile, 1000, 1001, 2);
const string headingKo = "용어 사전";
Check("both_heading_roles_allow_primary_space_with_Medium_Hangul_and_leave_global_Supports_strict", () =>
{
    var glyphs = new CpuGlyphs(); var service = new FontService(); service.PublishMediumDonor(donor, glyphs);
    Require(!service.Supports(headingKo, donor), "global_FontService_Supports_weakened");
    foreach (var role in new[] { HeadingKind.AnnotationFactory, HeadingKind.StandaloneDictionary })
    {
        string? fullString = null;
        var result = HeadingFontPolicy.Check(true, role, headingKo, donor, () => service.Current, scalar => service.Supports(char.ConvertFromUtf32(scalar), donor), value => { fullString = value; return true; });
        Require(result.Allowed && fullString == headingKo, "space_in_donor_required_or_target_coverage_not_full_string");
    }
    Require(headingKo.Contains(' ') && !glyphs.HasGlyph(donor, 0x20), "fixture_must_have_missing_Medium_space");
});
Check("both_heading_roles_block_required_Hangul_or_primary_route_failure", () =>
{
    foreach (var role in new[] { HeadingKind.AnnotationFactory, HeadingKind.StandaloneDictionary })
    {
        int primaryReads = 0;
        var required = HeadingFontPolicy.Check(true, role, headingKo, donor, () => donor, _ => false, _ => { primaryReads++; return true; });
        Require(!required.Allowed && required.Reason == "HEADING_REQUIRED_MEDIUM_GLYPH_MISSING" && required.MissingRequiredScalar.HasValue && primaryReads == 0, "missing_Hangul_bypassed");
        var primary = HeadingFontPolicy.Check(true, role, headingKo, donor, () => donor, ChoiceFontRolePolicy.RequiresMedium, _ => false);
        Require(!primary.Allowed && primary.Reason == "HEADING_PRIMARY_FALLBACK_MISSING", "missing_full_primary_route_bypassed");
    }
});
Check("donor_invalid_stale_and_mid_probe_invalidation_block_font_role", () =>
{
    foreach (var bad in new FontDonorHandle?[] { null, donor with { Profile = "other" }, donor with { NativeHandle = 0 }, donor with { InstanceId = 0 }, donor with { Generation = 0 } })
    {
        int glyphReads = 0;
        var result = HeadingFontPolicy.Check(true, HeadingKind.AnnotationFactory, headingKo, bad, () => bad, _ => { glyphReads++; return true; }, _ => true);
        Require(!result.Allowed && result.Reason == "HEADING_DONOR_STALE" && glyphReads == 0, "invalid_donor_read_glyphs");
    }
    var stale = HeadingFontPolicy.Check(true, HeadingKind.AnnotationFactory, headingKo, donor, () => donor with { Generation = 3 }, _ => true, _ => true);
    Require(!stale.Allowed, "stale_generation_admitted");
    FontDonorHandle? current = donor;
    var changed = HeadingFontPolicy.Check(true, HeadingKind.StandaloneDictionary, headingKo, donor, () => current, ChoiceFontRolePolicy.RequiresMedium, _ => { current = donor with { Generation = 3 }; return true; });
    Require(!changed.Allowed && changed.Reason == "HEADING_DONOR_STALE", "mid_probe_generation_change_admitted");
});
Check("offthread_unsupported_role_and_provider_failure_do_not_escape_font_boundary", () =>
{
    var gate = new GameThreadGate(); gate.Capture(); int reads = 0;
    var offthread = Task.Run(() => HeadingFontPolicy.Check(gate.CanAccess, HeadingKind.AnnotationFactory, headingKo, donor, () => { reads++; return donor; }, _ => { reads++; return true; }, _ => { reads++; return true; })).GetAwaiter().GetResult();
    Require(!offthread.Allowed && offthread.Reason == "HEADING_NATIVE_ACCESS_GATE_CLOSED" && reads == 0, "offthread_font_native_read");
    var unsupported = HeadingFontPolicy.Check(true, (HeadingKind)99, headingKo, donor, () => { reads++; return donor; }, _ => { reads++; return true; }, _ => { reads++; return true; });
    Require(!unsupported.Allowed && reads == 0, "unsupported_role_native_read");
    var error = HeadingFontPolicy.Check(true, HeadingKind.StandaloneDictionary, headingKo, donor, () => donor, _ => throw new IOException("private-path-must-not-escape"), _ => true);
    Require(!error.Allowed && error.Reason == "HEADING_FONT_READ_UNAVAILABLE", "glyph_exception_escaped");
});

string assembly = typeof(AnnotationDeliveryProofPolicy).Assembly.Location;
var report = new { status = failed == 0 ? "PASS" : "FAIL", candidate_dll_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), passed = results.Count - failed, failed, results, native_calls = 0,
    native = "NOT_RUN_IN_CPU_SUITE", game = "NOT_RUN_IN_CPU_SUITE", basis = "Actual candidate helper/gate/ownership library called with synthetic immutable records and mock text/glyph providers. CPU predicates are not native factory, ABI, glyph, hook or game evidence." };
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }); Console.WriteLine(json); if (args.Length > 0) File.WriteAllText(args[0], json);
return failed == 0 ? 0 : 1;

sealed class CpuTarget(TextTargetState initial) : IUiTextTarget, IUiTargetAccessGate
{
    public TextTargetState State = initial; public bool ReadFails, WriteFails; public int Reads, Writes; public Func<bool> Access = () => true;
    public bool CanAccess => Access();
    public TextTargetState ReadState() { Reads++; if (ReadFails) throw new IOException("CPU_READ_FAILURE"); return State; }
    public bool TryWriteText(TextTargetState expected, string value) { if (WriteFails || expected != State) return false; Writes++; State = State with { Text = value }; return true; }
}
sealed class CpuGlyphs : IFontGlyphProvider
{
    public bool HasGlyph(FontDonorHandle donor, int scalar) => ChoiceFontRolePolicy.RequiresMedium(scalar);
}
