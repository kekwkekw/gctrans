using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GCMod.Nextgen;

var output = new List<object>(); int failed = 0;
void Test(string name, Action body) { try { body(); output.Add(new { name, status = "PASS" }); } catch (Exception e) { failed++; output.Add(new { name, status = "FAIL", reason = e.Message }); } }
void Need(bool ok, string reason) { if (!ok) throw new Exception(reason); }
var contract = new CoreProducerContract(0, "OptionPopup_6");
var id = new CoreProducerIdentity(1, 2, 3, 4, 5, 6, 0, "OptionPopup_6", 0, 0);
var actual = new CoreProducerObservation(id, true, true, true, true);
bool Check(CoreProducerObservation current, CoreProducerIdentity? captured = null, CoreProducerContract? expected = null, bool absent = false)
    => CoreProducerPolicy.Matches(true, expected ?? contract, absent, captured, () => current);
Test("nonempty_type0_metadata_is_exact_not_lookup_authority", () => Need(Check(actual), "accepted_type0_key_rejected"));
Test("different_nonempty_key_and_nonliteral_type_fail", () => {
    Need(!Check(actual with { Identity = id with { MetadataKey = "OptionPopup_3" } }), "wrong_key_admitted");
    Need(!Check(actual with { Identity = id with { LocalizerType = 1 } }), "nonliteral_admitted");
});
Test("retained_target_owner_localizer_generation_all_match", () => {
    Need(Check(actual, id), "retained_identity_rejected");
    foreach (var changed in new[] { id with { Owner = 90 }, id with { OwnerInstance = 90 }, id with { Lifetime = 90 },
        id with { Target = 90 }, id with { TargetInstance = 90 }, id with { Localizer = 90 } })
        Need(!Check(actual with { Identity = changed }, id), "reused_identity_admitted");
});
Test("source_external_writer_ambiguous_target_and_owner_fail", () => {
    foreach (var changed in new[] { actual with { ExactSourceOrOwnedDisplay = false }, actual with { UniqueTarget = false },
        actual with { OwnerOwnsTarget = false }, actual with { AreaPathMatches = false } }) Need(!Check(changed), "guard_removed");
});
Test("off_thread_discovery_and_late_guard_read_nothing", () => {
    int reads = 0;
    Need(!CoreProducerPolicy.Matches(false, contract, false, id, () => { reads++; return actual; }) && reads == 0, "native_read_before_gate");
});
Test("failed_visual_is_not_new_apply_authority_but_conditional_restore_keeps_source", () => {
    var target = new ConditionalRestoreTarget(); var service = new UiOwnershipService(); service.OpenEpoch(1);
    Need(service.TryWrite(target, new("Core", "case", null, "caption", 1, target.ReadState()), "KO").Written, "fixture_write");
    target.VisualFailed = true;
    Need(!CoreProducerPolicy.OwnedDisplayMayConfirmSource(true, false) && !target.ReadState().Active, "failed_display_admitted_for_apply");
    target.ConditionalRestore = true;
    try { Need(service.RestoreTargetDetailed(target.Identity) == RestoreOutcome.Restored && target.Text == "JP" && service.LeaseCount == 0, "visual_failure_orphaned_lease"); }
    finally { target.ConditionalRestore = false; }
});
Test("failed_visual_restore_write_failure_preserves_original_lease_for_retry", () => {
    var target = new ConditionalRestoreTarget(); var service = new UiOwnershipService(); service.OpenEpoch(1);
    Need(service.TryWrite(target, new("Core", "case", null, "caption", 1, target.ReadState()), "KO").Written, "fixture_write");
    target.VisualFailed = true; target.ConditionalRestore = true; target.WriteFails = true;
    try { Need(service.RestoreTargetDetailed(target.Identity) == RestoreOutcome.RetryableWriteFailure && target.Text == "KO" && service.LeaseCount == 1, "restore_failure_lost_lease"); }
    finally { target.ConditionalRestore = false; }
    target.WriteFails = false; target.ConditionalRestore = true;
    try { Need(service.RestoreTargetDetailed(target.Identity) == RestoreOutcome.Restored && target.Text == "JP", "retry_lost_original"); }
    finally { target.ConditionalRestore = false; }
});
Test("conditional_restore_does_not_bypass_changed_owner_or_external_text", () => {
    foreach (bool rebound in new[] { true, false })
    {
        var target = new ConditionalRestoreTarget(); var service = new UiOwnershipService(); service.OpenEpoch(1);
        Need(service.TryWrite(target, new("Core", "case", null, "caption", 1, target.ReadState()), "KO").Written, "fixture_write");
        target.VisualFailed = true; target.ConditionalRestore = true;
        if (rebound) target.OwnerValid = false; else target.Text = "EXTERNAL";
        string before = target.Text; var result = service.RestoreTargetDetailed(target.Identity);
        Need(result is RestoreOutcome.DestroyedOrRebound or RestoreOutcome.ExternalChanged && target.Text == before, "restore_overwrote_changed_target");
    }
});
Test("legacy_empty_key_and_scoped_absent_paths_preserved", () => {
    var empty = actual with { Identity = id with { MetadataKey = "" } };
    Need(CoreProducerPolicy.Matches(true, null, false, null, () => empty), "legacy_empty_rejected");
    var absent = empty with { Identity = empty.Identity with { Localizer = 0, LocalizerType = -1 } };
    Need(CoreProducerPolicy.Matches(true, null, true, null, () => absent), "scoped_absent_rejected");
    Need(!CoreProducerPolicy.Matches(true, null, false, null, () => absent), "unscoped_absent_admitted");
    Need(!Check(absent, absent: true), "nonempty_metadata_degraded_to_absent");
});
var areaPath = new OptionAreaContract("soundAndTextOptionArea", ImmutableArray.Create("Inner_Frame", "Text_Text"), ImmutableArray.Create(0, 1));
var a = new Node("OptionArea", new Node("Inner_Frame", new Node("Text_Text"), new Node("Text_Text")));
Node? Resolve(Node root, OptionAreaContract path, bool access = true) => CoreProducerPolicy.ResolveAreaTarget(access, root, path,
    n => n.Children.Length, (n, i) => n.Children[i], n => n.Name);
Test("typed_area_anchor_survives_outer_tab_clone_change", () => Need(ReferenceEquals(Resolve(a, areaPath), a.Children[0].Children[1]), "area_path_used_root_wrapper"));
Test("indexed_same_name_target_is_not_first_match", () => Need(!ReferenceEquals(Resolve(a, areaPath), a.Children[0].Children[0]), "first_match_returned"));
Test("area_index_name_and_bounds_must_all_match", () => {
    Need(Resolve(a, areaPath with { Indices = ImmutableArray.Create(0, 2) }) == null, "out_of_range_admitted");
    Need(Resolve(a, areaPath with { Segments = ImmutableArray.Create("Inner_Frame", "Other") }) == null, "wrong_name_admitted");
    Need(Resolve(a, areaPath with { Indices = ImmutableArray.Create(0) }) == null, "arity_mismatch_admitted");
    int reads = 0;
    Need(CoreProducerPolicy.ResolveAreaTarget(false, a, areaPath, n => { reads++; return n.Children.Length; }, (n, i) => n.Children[i], n => n.Name) == null && reads == 0, "off_thread_area_read");
});
Test("area_reuse_and_replacement_reject_retained_ticket", () => {
    var expected = contract with { OptionArea = areaPath };
    var current = actual with { Identity = id with { Area = 8, AreaInstance = 9 } };
    Need(Check(current, current.Identity, expected), "current_area_rejected");
    Need(!Check(current with { Identity = current.Identity with { Area = 10 } }, current.Identity, expected), "new_area_admitted");
    Need(!Check(current with { Identity = current.Identity with { AreaInstance = 10 } }, current.Identity, expected), "reused_area_admitted");
    Need(!Check(actual, expected: expected), "missing_area_admitted");
});
Test("comatch_contract_comparison_includes_path_and_metadata", () => {
    Need(CoreProducerPolicy.SameContract(contract, new(0, "OptionPopup_6")), "equal_contract_rejected");
    Need(!CoreProducerPolicy.SameContract(contract, new(0, "OptionPopup_3")), "key_conflict_ignored");
    var p = contract with { OptionArea = areaPath };
    Need(CoreProducerPolicy.SameContract(p, p with { OptionArea = areaPath with { Indices = ImmutableArray.Create(0, 1) } }), "equal_array_values_rejected");
    Need(!CoreProducerPolicy.SameContract(p, p with { OptionArea = areaPath with { Indices = ImmutableArray.Create(0, 0) } }), "path_conflict_ignored");
});
string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(".");
using var cases = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tests/ComprehensiveCoreTests/producer_cases.json")));
Test("header_power_local_geometry_role_is_not_other_header_or_footer", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true,
        new("static-asset", "case", "source"), new("gc.headertopcontroller", "ui4-ui4-s027-1-06002e63",
            ImmutableArray.Create("Frame_UI", "Group_Left", "Frame_Toplayer", "CombatPower", "Text_PlayerCombatPower", "Text_CombatPowerHeader"), Type: "static", Lifetime: "owner"), null,
        new(0, "Unit_deck_ui_13"));
    Need(HeaderPowerGeometryPolicy.Supports(b), "accepted_header_power_role_rejected");
    foreach (var changed in new[] { b with { Enabled = false }, b with { AdapterId = "ui.caption.v1" },
        b with { Selector = b.Selector with { ScopeKey = "gc.globalmenu.globalmenufooterbuttons" } },
        b with { Producer = b.Producer! with { TextConfigKey = "Other" } }, b with { Producer = null },
        b with { Selector = b.Selector with { Segments = ImmutableArray.Create("Frame_UI", "Other") } } })
        Need(!HeaderPowerGeometryPolicy.Supports(changed), "unrelated_renderer_role_admitted");
});
Test("option_checkbox_composite_is_exact_typed_caption_role_only", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.option.optionpopup", "case", ImmutableArray<string>.Empty, Type: "static", Lifetime: "owner"), null,
        new(0, "OptionPopup_3", new("soundAndTextOptionArea", ImmutableArray.Create("Inner_Frame", "Group_DisplaySpeed", "CheckBox", "Text_Color"), ImmutableArray.Create(1, 2, 1, 1))));
    Need(OptionCheckboxGeometryPolicy.Supports(b), "accepted_option_caption_rejected");
    foreach (var changed in new[] { b with { Enabled = false }, b with { AdapterId = "ui.caption.v1" },
        b with { Selector = b.Selector with { ScopeKey = "gc.globalmenu.globalmenufooterbuttons" } },
        b with { Producer = b.Producer! with { TextConfigKey = "OptionPopup_4" } }, b with { Producer = null },
        b with { Producer = b.Producer! with { OptionArea = b.Producer.OptionArea! with { Indices = ImmutableArray.Create(1, 2, 1, 0) } } } })
        Need(!OptionCheckboxGeometryPolicy.Supports(changed), "other_option_or_renderer_admitted");
});
Test("option_checkbox_requires_one_current_bounded_graphic_member", () => {
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(new long[] { 8, 9, 10 }, 9) == 1, "current_member_rejected");
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(new long[] { 8, 10 }, 9) == -1, "missing_member_admitted");
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(new long[] { 9, 8, 9 }, 9) == -1, "ambiguous_member_admitted");
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(Enumerable.Repeat(9L, 33).ToArray(), 9) == -1, "unbounded_graphics_admitted");
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(new long[] { 0, 8 }, 0) == -1, "null_target_admitted");
});
Test("option_checkbox_empty_graphics_uses_declared_caption_without_invented_member", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.option.optionpopup", "case", ImmutableArray<string>.Empty, Type: "static", Lifetime: "owner"), null,
        new(0, "OptionPopup_3", new("soundAndTextOptionArea", ImmutableArray.Create("Inner_Frame", "Group_DisplaySpeed", "CheckBox", "Text_Color"), ImmutableArray.Create(1, 2, 1, 1))));
    var current = new OptionCheckboxCaptionObservation(10, 20, 20, 10, "Text_Color", 1, 1);
    Need(OptionCheckboxGeometryPolicy.SelectRole(b, Array.Empty<long>(), 30, current) == OptionCheckboxCaptionRole.DeclaredDirectCaption, "declared_direct_caption_rejected");
    Need(OptionCheckboxGeometryPolicy.UniqueGraphicMember(Array.Empty<long>(), 30) == -1, "empty_list_fabricated_member");
    Need(OptionCheckboxGeometryPolicy.SelectRole(b, new long[] { 40, 30 }, 30, current) == OptionCheckboxCaptionRole.GraphicMember, "actual_member_rejected");
});
Test("option_checkbox_declared_caption_refuses_wrong_path_ambiguity_or_nonempty_nonmember", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.option.optionpopup", "case", ImmutableArray<string>.Empty, Type: "static", Lifetime: "owner"), null,
        new(0, "OptionPopup_3", new("soundAndTextOptionArea", ImmutableArray.Create("Inner_Frame", "Group_DisplaySpeed", "CheckBox", "Text_Color"), ImmutableArray.Create(1, 2, 1, 1))));
    var current = new OptionCheckboxCaptionObservation(10, 20, 20, 10, "Text_Color", 1, 1);
    foreach (var changed in new[] { current with { IndexedTargetTransform = 11 }, current with { ParentTransform = 21 },
        current with { SameNamedChildren = 2 }, current with { TargetTmpCount = 2 }, current with { TargetName = "Other" } })
        Need(OptionCheckboxGeometryPolicy.SelectRole(b, Array.Empty<long>(), 30, changed) == OptionCheckboxCaptionRole.Unavailable, "unproven_direct_caption_admitted");
    Need(OptionCheckboxGeometryPolicy.SelectRole(b, new long[] { 40 }, 30, current) == OptionCheckboxCaptionRole.Unavailable, "nonempty_nonmember_treated_as_empty");
    Need(OptionCheckboxGeometryPolicy.SelectRole(b, new long[] { 30, 30 }, 30, current) == OptionCheckboxCaptionRole.Unavailable, "duplicate_actual_member_admitted");
    Need(OptionCheckboxGeometryPolicy.SelectRole(b, Array.Empty<long>(), 0, current) == OptionCheckboxCaptionRole.Unavailable, "null_target_admitted");
    Need(OptionCheckboxGeometryPolicy.SelectRole(b with { Producer = b.Producer! with { OptionArea = b.Producer.OptionArea! with { Indices = ImmutableArray.Create(1, 2, 1, 0) } } }, Array.Empty<long>(), 30, current) == OptionCheckboxCaptionRole.Unavailable, "wrong_declared_path_admitted");
});
Test("option_checkbox_plain_material_does_not_inherit_footer_effect_policy", () => {
    Need(OptionCheckboxGeometryPolicy.PlainSdfMaterial("TextMeshPro/Mobile/Distance Field", Array.Empty<string>()), "native_plain_material_rejected");
    Need(OptionCheckboxGeometryPolicy.PlainSdfMaterial("TextMeshPro/Mobile/Distance Field", new[] { "UNITY_UI_CLIP_RECT" }), "actual_ui_mask_keyword_rejected");
    foreach (string keyword in new[] { "GLOW_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "OUTLINE_ON", "UNKNOWN" })
        Need(!OptionCheckboxGeometryPolicy.PlainSdfMaterial("TextMeshPro/Mobile/Distance Field", new[] { keyword }), "unsupported_effect_admitted");
    Need(!OptionCheckboxGeometryPolicy.PlainSdfMaterial("Custom/Distance Field", Array.Empty<string>()), "unknown_shader_admitted");
});
Test("filter_reset_preferred_size_role_is_not_other_filter_button", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.filterpopup", "ui4-ui4-s089-62-06007a91", ImmutableArray.Create("Frame_Popup", "PopupWindowM", "PopupBase", "ContentsArea", "Frame_Content", "Group_Bt", "BtDefaultAll", "image_Textbtn01_s", "Group_Text", "Text_Textbtn01"), Type: "static", Lifetime: "owner"), null);
    Need(FilterResetGeometryPolicy.Supports(b), "accepted_reset_role_rejected");
    foreach (var changed in new[] { b with { Enabled = false }, b with { AdapterId = "common.model-label.v1" },
        b with { Selector = b.Selector with { ScopeKey = "gc.option.optionpopup" } },
        b with { Selector = b.Selector with { SlotKey = "ui4-ui4-s085-82-06007a91" } },
        b with { Selector = b.Selector with { Segments = b.Selector.Segments.SetItem(6, "BtSelectAll") } } })
        Need(!FilterResetGeometryPolicy.Supports(changed), "unrelated_filter_caption_admitted");
});
Test("filter_deselect_preferred_size_role_uses_its_exact_typed_field_contract", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.filterpopup", "ui4-ui4-s064-62-06007a91", ImmutableArray.Create("Frame_Popup", "PopupWindowM", "PopupBase", "ContentsArea", "Frame_Content", "Group_Bt", "BtDeselectAll", "image_Textbtn01_s", "Group_Text", "Text_Textbtn01"), Type: "static", Lifetime: "owner"), null);
    Need(FilterResetGeometryPolicy.Role(b) == FilterPreferredCaptionRole.Deselect, "accepted_deselect_role_rejected");
    Need(FilterResetGeometryPolicy.Role(b with { Selector = b.Selector with { SlotKey = "ui4-ui4-s089-62-06007a91" } }) == FilterPreferredCaptionRole.Unavailable, "reset_slot_deselect_path_mixed");
    Need(FilterResetGeometryPolicy.Role(b with { Selector = b.Selector with { Segments = b.Selector.Segments.SetItem(6, "BtDefaultAll") } }) == FilterPreferredCaptionRole.Unavailable, "deselect_slot_reset_path_mixed");
    Need(FilterResetGeometryPolicy.Role(b with { Selector = b.Selector with { Segments = b.Selector.Segments.SetItem(6, "BtSelectAll") } }) == FilterPreferredCaptionRole.Unavailable, "unapproved_select_button_added");
    Need(NativeContentFitPolicy.Matches(true, false, 159.30f, 27.01f, 160.64f, 27.01f, 160.64f, 20), "current_preferred_growth_rejected");
    Need(!NativeContentFitPolicy.Matches(true, false, 159.30f, 27.01f, 160.64f, 27.01f, 159.30f, 20), "arbitrary_growth_admitted");
});
Test("native_preferred_size_shrink_preserves_other_dimensions_and_requires_actual_preference", () => {
    Need(NativeContentFitPolicy.Matches(true, false, 79.93f, 27.01f, 73.44f, 27.01f, 73.44f, 20), "native_preferred_width_rejected");
    Need(!NativeContentFitPolicy.Matches(false, false, 79.93f, 27.01f, 73.44f, 27.01f, 73.44f, 20), "absent_driver_admitted");
    Need(!NativeContentFitPolicy.Matches(true, false, 79.93f, 27.01f, 73.44f, 30, 73.44f, 20), "external_height_change_admitted");
    Need(!NativeContentFitPolicy.Matches(true, false, 79.93f, 27.01f, 73.44f, 27.01f, 80, 20), "nonpreferred_width_admitted");
});
Test("campaign_caption_current_visibility_gate_is_not_header_power_or_footer_policy", () => {
    var b = new UiBinding("case", "case", "ui4.static-caption.v1", true, new("static-asset", "case", "source"),
        new("gc.headertopcontroller", "ui4-ui4-s038-1-06002e55", ImmutableArray.Create("Frame_UI", "Group_Left", "Frame_Underlayer", "GroupCanpaign", "FrameCanpaign", "Text_Campaign"), Type: "static", Lifetime: "owner"), null);
    Need(CampaignCaptionPolicy.Supports(b), "accepted_campaign_role_rejected");
    foreach (var changed in new[] { b with { Enabled = false }, b with { AdapterId = "ui.caption.v1" },
        b with { Selector = b.Selector with { ScopeKey = "gc.globalmenu.globalmenufooterbuttons" } },
        b with { Selector = b.Selector with { SlotKey = "ui4-ui4-s027-1-06002e63" } },
        b with { Producer = new(0, "Unit_deck_ui_13") },
        b with { Selector = b.Selector with { Segments = ImmutableArray.Create("Frame_UI", "Other") } } })
        Need(!CampaignCaptionPolicy.Supports(changed), "unrelated_header_or_footer_role_admitted");
});
Test("filter_option_native_autosize_only_uses_existing_category_owner_role", () => {
    var b = new UiBinding("case", "case", "common.model-label.v1", true, new("current-model", "case", "source"),
        new("current-filter", "filter_option", ImmutableArray<string>.Empty, Type: "model", Domain: 6, SourceValue: 116, Lifetime: "owner"), null);
    Need(FilterOptionAutoSizePolicy.Supports(b), "accepted_category_option_rejected");
    foreach(var changed in new[]{b with{Enabled=false},b with{Selector=b.Selector with{ScopeKey="element-filter"}},
        b with{Selector=b.Selector with{SlotKey="filter_heading"}},b with{Selector=b.Selector with{Domain=7}},
        b with{Selector=b.Selector with{SourceValue=null}},b with{Selector=b.Selector with{Lifetime="callback"}}})
        Need(!FilterOptionAutoSizePolicy.Supports(changed), "unrelated_renderer_autosize_admitted");
});
Test("native_autosize_changes_only_current_size_with_same_authoring_configuration", () => {
    var initial = new NativeAutoSizeProfile(true, 18, 32, 32); var expected = FilterOptionAutoSizePolicy.Capture(initial);
    Need(expected!=null && FilterOptionAutoSizePolicy.Matches(expected,initial with{Size=24}),"native_shrink_rejected");
    Need(FilterOptionAutoSizePolicy.Matches(expected!,initial with{Size=30}),"native_growth_in_bounds_rejected");
    foreach(var changed in new[]{initial with{Enabled=false},initial with{Minimum=17},initial with{Maximum=33},
        initial with{Size=17},initial with{Size=33},initial with{Size=float.NaN}})
        Need(!FilterOptionAutoSizePolicy.Matches(expected!,changed),"changed_configuration_or_unfit_size_admitted");
});
Test("manual_or_invalid_native_autosize_profiles_remain_strict", () => {
    foreach(var profile in new[]{new NativeAutoSizeProfile(false,18,32,24),new(true,0,32,24),new(true,33,32,32),
        new(true,float.NaN,32,24),new(true,18,float.PositiveInfinity,24),new(true,18,600,24)})
        Need(FilterOptionAutoSizePolicy.Capture(profile)==null,"unproven_autosize_driver_admitted");
});
foreach (var example in cases.RootElement.EnumerateArray())
{
    string name = example.GetProperty("name").GetString()!;
    Test("shared_parser_corpus_" + name, () => {
        bool accepted;
        try { CoreProducerPolicy.Parse(example.GetProperty("producer"), example.GetProperty("adapter").GetString()!,
            example.GetProperty("source_kind").GetString()!, example.GetProperty("scope").GetString()!); accepted = true; }
        catch (PackageValidationException) { accepted = false; }
        Need(accepted == example.GetProperty("valid").GetBoolean(), "parser_parity_" + name);
    });
}
Test("real_binding_metadata_has_no_disabled_activation_or_translation_copy", () => {
    using var bindings = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "translations/ko/ui/bindings.json")));
    int nonempty = 0, areas = 0, disabledScreens = 0;
    foreach (var binding in bindings.RootElement.GetProperty("bindings").EnumerateArray())
    {
        string bindingId = binding.GetProperty("id").GetString()!;
        if (new[] { "binding.core.UI4-S004.", "binding.core.UI4-S010.", "binding.core.UI4-S026.", "binding.core.UI4-S036." }.Any(bindingId.StartsWith))
        { Need(!binding.GetProperty("enabled").GetBoolean() && !binding.TryGetProperty("producer", out _), "inactive_screen_changed"); disabledScreens++; }
        if (!binding.TryGetProperty("producer", out var node)) continue;
        var p = CoreProducerPolicy.Parse(node, binding.GetProperty("adapter_id").GetString()!, binding.GetProperty("source_identity").GetProperty("kind").GetString()!, binding.GetProperty("selector").GetProperty("scope_key").GetString()!);
        Need(!node.TryGetProperty("ko", out _) && binding.GetProperty("enabled").GetBoolean(), "producer_copied_translation_or_activated_archive");
        if (p!.TextConfigKey.Length > 0) nonempty++;
        if (p.OptionArea != null) areas++;
    }
    // Input accounting assertions for this restoration dataset, never runtime count gates.
    Need(nonempty == 13 && areas == 15 && disabledScreens == 4, "restoration_input_accounting");
});
var assembly = typeof(CoreProducerPolicy).Assembly.Location;
Console.WriteLine(JsonSerializer.Serialize(new { fixture = "managed_policy_and_indexed_tree_only_not_native_proof", assembly_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), count = output.Count, failed, tests = output }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;

sealed class Node(string name, params Node[] children) { public string Name { get; } = name; public Node[] Children { get; } = children; }
sealed class ConditionalRestoreTarget : IUiTextTarget
{
    public PhysicalTargetIdentity Identity { get; } = new(500, 1, "managed-policy-fixture", 1);
    public string Text = "JP";
    public bool VisualFailed, ConditionalRestore, WriteFails, OwnerValid = true;
    public TextTargetState ReadState()
    {
        bool source = OwnerValid && (Text == "JP" || Text == "KO" && CoreProducerPolicy.OwnedDisplayMayConfirmSource(VisualFailed, ConditionalRestore));
        return new(Identity, Text, 1, 1, "fixture", "JP", source, !source);
    }
    public bool TryWriteText(TextTargetState expected, string value)
    {
        if (WriteFails || ReadState() != expected || !expected.Active) return false;
        Text = value; return true;
    }
}
