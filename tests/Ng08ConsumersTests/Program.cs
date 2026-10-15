using System.Text.Json;
using GCMod.Nextgen;

var results = new List<object>();
int failed = 0;
void Check(string name, Action test)
{
    try { test(); results.Add(new { name, status = "PASS", level = "ACTUAL_PRODUCT_CPU_POLICY_OR_OWNERSHIP_MOCK" }); }
    catch (Exception e) { failed++; results.Add(new { name, status = "FAIL", reason = e.Message }); }
}
void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
Check("source_owner_control_faults_preempt_mesh_wait", () =>
{
    foreach (var reason in new[] { UiVisualFailure.OwnerRebound, UiVisualFailure.SourceChanged, UiVisualFailure.ControlChanged, UiVisualFailure.TargetInactive })
    {
        var r = UiVisualSettlement.Assess(1, reason | UiVisualFailure.MeshStale | UiVisualFailure.Dirty, false);
        Require(r.Action == UiVisualAction.Restore && (r.Failure & reason) != 0, "fatal_source_control_lost");
    }
});
Check("mesh_and_layout_have_distinct_finite_waits", () =>
{
    Require(UiVisualSettlement.Assess(4, UiVisualFailure.MeshStale, false).Phase == UiVisualPhase.WaitMesh, "mesh_phase");
    Require(UiVisualSettlement.Assess(4, UiVisualFailure.None, false).Phase == UiVisualPhase.WaitLayout, "layout_phase");
    Require(UiVisualSettlement.Assess(61, UiVisualFailure.Dirty, false).Action == UiVisualAction.Restore, "unbounded_wait");
    Require(UiVisualSettlement.Assess(61, UiVisualFailure.None, true).Action == UiVisualAction.Restore, "late_accept");
});
Check("current_layout_can_settle_after_popup_motion", () =>
{
    Require(UiVisualSettlement.Assess(1, UiVisualFailure.None, false).Action == UiVisualAction.Wait, "moving_layout_accepted");
    Require(UiVisualSettlement.Assess(8, UiVisualFailure.None, true).Action == UiVisualAction.Accept, "settled_layout_rejected");
});
Check("stable_overflow_mask_or_channel_failure_never_accepts", () =>
{
    foreach (var failure in new[] { UiVisualFailure.IntrinsicOverflow, UiVisualFailure.MaskClipped, UiVisualFailure.RenderChannelHidden })
        Require(UiVisualSettlement.Assess(2, failure, true).Action == UiVisualAction.Restore, "unsafe_visible_geometry");
});
Check("display_font_mutation_is_fatal_before_layout_wait", () => Require(
    UiVisualSettlement.Assess(1, UiVisualFailure.DisplayChanged, false).Action == UiVisualAction.Restore, "font_change_waited"));
Check("fallback_uses_actual_channel_not_empty_primary", () =>
{
    Require(!UiGlyphChannelPolicy.Visible(0, true, true, true, true, 1), "culled_primary_visible");
    Require(UiGlyphChannelPolicy.Visible(1, true, true, true, false, 1), "visible_fallback_rejected");
    Require(!UiGlyphChannelPolicy.Visible(1, true, false, true, false, 1), "foreign_submesh_admitted");
    Require(!UiGlyphChannelPolicy.Visible(1, false, true, true, false, 1), "font_supplier_mismatch");
    Require(!UiGlyphChannelPolicy.Visible(1, true, true, true, false, float.NaN), "invalid_alpha");
    Require(!UiGlyphChannelPolicy.Visible(9, true, true, true, false, 1), "unbounded_channel");
});
Check("sort_ticket_rejects_reused_owner_list_toggle_source_and_text", () =>
{
    var ticket = new SortLabelSourceTicket(10, 2, 20, 30, 4, 40, "JP");
    Require(ticket.Matches(10, 2, 20, 30, 4, 40), "captured_identity");
    Require(!ticket.Matches(11, 2, 20, 30, 4, 40) && !ticket.Matches(10, 3, 20, 30, 4, 40) &&
        !ticket.Matches(10, 2, 21, 30, 4, 40) && !ticket.Matches(10, 2, 20, 31, 4, 40) &&
        !ticket.Matches(10, 2, 20, 30, 5, 40) && !ticket.Matches(10, 2, 20, 30, 4, 41), "stale_ticket_admitted");
    Require(typeof(SortLabelSourceTicket).GetProperties().All(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(long) || p.PropertyType == typeof(string)), "native_temp_retained");
});
Check("settlement_failure_restores_own_value_only", () =>
{
    var identity = new PhysicalTargetIdentity(1, 2, "TMP", 3);
    var initial = new TextTargetState(identity, "JP", 3, 3, "source", "JP");
    var target = new MemoryTextTarget(initial); var service = new UiOwnershipService(); service.OpenEpoch(1);
    Require(service.TryWrite(target, new("Consumers", "b", null, "slot", 1, initial), "KO").Written, "write_failed");
    Require(UiVisualSettlement.Assess(5, UiVisualFailure.MaskClipped, true).Action == UiVisualAction.Restore, "mask_accepted");
    Require(service.RestoreTargetDetailed(identity) == RestoreOutcome.Restored && target.ReadState().Text == "JP", "owned_restore");
    Require(service.TryWrite(target, new("Consumers", "b", null, "slot", 1, initial), "KO").Written, "second_write");
    target.GameWrite("NEW_GAME_VALUE");
    Require(service.RestoreTargetDetailed(identity) == RestoreOutcome.ExternalChanged && target.ReadState().Text == "NEW_GAME_VALUE", "external_value_overwritten");
});
Check("restore_read_failure_preserves_live_lease", () =>
{
    var initial = new TextTargetState(new(1, 2, "TMP", 3), "JP", 3, 3, "source", "JP");
    var target = new FailingTarget(initial); var service = new UiOwnershipService(); service.OpenEpoch(1);
    Require(service.TryWrite(target, new("Consumers", "b", null, "slot", 1, initial), "KO").Written, "write_failed");
    target.ReadFails = true;
    Require(service.RestoreTargetDetailed(initial.Identity) == RestoreOutcome.RetryableReadFailure && service.LeaseCount == 1, "lease_lost");
    target.ReadFails = false;
    Require(service.RestoreTargetDetailed(initial.Identity) == RestoreOutcome.Restored && target.State.Text == "JP", "retry_failed");
});
Check("sort_prefix_restore_allows_first_reused_setup_write", () =>
{
    var identity = new PhysicalTargetIdentity(1, 2, "TMP", 3);
    var initial = new TextTargetState(identity, "JP", 3, 3, "source", "JP");
    var target = new MemoryTextTarget(initial); var service = new UiOwnershipService(); service.OpenEpoch(1);
    Require(service.TryWrite(target, new("Consumers", "b", null, "slot", 1, initial), "KO").Written, "first_setup");
    // The actual native provider subscribes this cleanup before the original Sort.Setup.
    Require(service.RestoreTargetDetailed(identity) == RestoreOutcome.Restored, "prefix_restore");
    var next = initial with { Identity = identity with { Lifetime = 4 }, OwnerGeneration = 4, SourceGeneration = 4 };
    target.Recycle(next);
    Require(service.TryWrite(target, new("Consumers", "b", null, "slot", 1, next), "KO").Written, "first_reused_setup_dropped");
});
Check("diagnostic_sink_failure_cannot_abort_policy", () =>
{
    var log = new SafeDiagnostics(128, _ => throw new InvalidOperationException("SINK"));
    log.Record("GEO_AABBCCDDEEFF_O10_WAIT_LAYOUT_M20_F40_A8");
    Require(log.SinkFailures == 1 && !log.Counters.ContainsKey("INVALID_DIAGNOSTIC_CODE") &&
        UiVisualSettlement.Assess(8, UiVisualFailure.None, true).Action == UiVisualAction.Accept, "sink_corrupted_policy");
});
Check("native_preferred_driver_accepts_only_its_current_axis_size", () =>
{
    Require(NativeContentFitPolicy.Matches(true, true, 90, 32, 73.44f, 27.01f, 73.44f, 27.01f), "natural_preferred_size_rejected");
    Require(!NativeContentFitPolicy.Matches(false, false, 90, 32, 73.44f, 27.01f, 73.44f, 27.01f), "absent_driver_admitted");
    Require(!NativeContentFitPolicy.Matches(true, false, 90, 32, 73.44f, 27.01f, 73.44f, 27.01f), "undriven_height_changed");
    Require(!NativeContentFitPolicy.Matches(true, true, 90, 32, 90, 32, 73.44f, 27.01f), "wrong_native_layout_admitted");
    Require(!NativeContentFitPolicy.Matches(true, true, 90, 32, float.NaN, 27.01f, 73.44f, 27.01f), "invalid_layout_admitted");
});
Check("geometry_failure_hold_ignores_queue_clock_reset", () =>
{
    var held = new HashSet<UiGeometryHoldKey> { new("b", 12, 3, 1) };
    // Queue.Started changes are scheduling, not native source/lifetime proof.
    Require(held.Contains(new("b", 12, 3, 1)), "schedule_reset_reopened_writer");
    Require(!held.Contains(new("b", 12, 4, 1)) && !held.Contains(new("b", 12, 3, 2)), "actual_epoch_cannot_requalify");
    Require(typeof(UiGeometryHoldKey).GetProperty("Producer") == null, "queue_clock_is_authority");
});
Check("diagnostic_budget_is_shared_across_fresh_attempts", () =>
{
    var budget = new UiGeometryDiagnosticBudget(); var key = new UiGeometryHoldKey("b", 12, 3, 1);
    Require(budget.TryRecord(key, UiVisualPhase.SourceJoin, UiVisualFailure.None, UiVisualField.None), "first_source_event");
    for (int i = 0; i < 1000; i++) Require(!budget.TryRecord(new("b", 12, 3, 1), UiVisualPhase.SourceJoin, UiVisualFailure.None, UiVisualField.None), "fresh_attempt_restarted_log");
    Require(budget.TryRecord(key, UiVisualPhase.Failed, UiVisualFailure.LayoutChanged, UiVisualField.LayoutDriver), "final_failure_lost");
    Require(budget.EntryCount == 1, "attempt_memory_growth");
    budget.ForgetOwner(3); Require(budget.EntryCount == 0, "owner_cleanup");
});
Check("layout_diagnostics_separate_rect_driver_parent_sibling_transform", () =>
{
    const string baseline = "{\"own\":{\"width\":90,\"height\":32,\"x\":0},\"parent\":1,\"siblings\":[1],\"fitter\":1,\"ancestors\":[1]}";
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"width\":90", "\"width\":73")) == UiVisualField.OwnRect, "own_width_reason");
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"x\":0", "\"x\":1")) == UiVisualField.Transform, "own_transform_reason");
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"parent\":1", "\"parent\":2")) == UiVisualField.Parent, "parent_reason");
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"siblings\":[1]", "\"siblings\":[2]")) == UiVisualField.Siblings, "sibling_reason");
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"fitter\":1", "\"fitter\":2")) == UiVisualField.LayoutDriver, "driver_reason");
    Require(UiLayoutDiagnosticFields.Compare(baseline, baseline.Replace("\"ancestors\":[1]", "\"ancestors\":[2]")) == UiVisualField.AncestorMotion, "ancestor_reason");
    Require(UiLayoutDiagnosticFields.Compare("[1]", "[2]") == UiVisualField.Transform, "bounded_array_layout_reject");
});
Check("profile_prepare_detail_remains_bounded_without_weakening_proof", () =>
{
    var budget = new UiGeometryDiagnosticBudget(); var key = new UiGeometryHoldKey("profile", 12, 3, 4);
    Require(budget.TryRecord(key, UiVisualPhase.ClusterBaseline, UiVisualFailure.LayoutChanged, UiVisualField.LayoutDriver, 1), "missing_driver_detail_lost");
    Require(!budget.TryRecord(key, UiVisualPhase.ClusterBaseline, UiVisualFailure.LayoutChanged, UiVisualField.LayoutDriver, 1), "same_detail_spam");
    Require(budget.TryRecord(key, UiVisualPhase.ClusterBaseline, UiVisualFailure.LayoutChanged, UiVisualField.LayoutDriver, 2), "different_structure_detail_lost");
    Require(UiVisualSettlement.Assess(61, UiVisualFailure.LayoutChanged, false).Action == UiVisualAction.Restore, "diagnostics_weakened_layout_guard");
});
Check("original_jp_composite_contract_does_not_bypass_real_clip_or_ko_leaf_fit", () =>
{
    Require(DecorativeJpGeometryPolicy.Eligible(true, true, true, true, true, true), "supported_original_composite_rejected");
    Require(!DecorativeJpGeometryPolicy.Eligible(true, false, true, true, true, true), "unsupported_cluster_admitted");
    Require(!DecorativeJpGeometryPolicy.Eligible(true, true, false, true, true, true), "invisible_original_glyph_admitted");
    Require(!DecorativeJpGeometryPolicy.Eligible(true, true, true, false, true, true), "outside_composite_admitted");
    Require(!DecorativeJpGeometryPolicy.Eligible(true, true, true, true, false, true), "mask_clip_admitted");
    Require(!DecorativeJpGeometryPolicy.Eligible(true, true, true, true, true, false), "hidden_channel_admitted");
    Require(!DecorativeJpGeometryPolicy.Eligible(false, true, true, true, true, true), "partial_source_admitted");
    Require(UiVisualSettlement.Assess(3, UiVisualFailure.IntrinsicOverflow, true).Action == UiVisualAction.Restore, "post_ko_leaf_guard_removed");
});
Check("profile_info_rebind_requires_real_selected_current_getter_scope", () =>
{
    Require(ProfileInfoRebindPolicy.Eligible(true, true, true, true, true), "existing_profile_rebind_rejected");
    Require(!ProfileInfoRebindPolicy.Eligible(false, true, true, true, true), "deferred_queue_replayed_native_callback");
    Require(!ProfileInfoRebindPolicy.Eligible(true, false, true, true, true), "off_tab_queued_profile_content");
    Require(!ProfileInfoRebindPolicy.Eligible(true, true, false, true, true), "foreign_title_or_disabled_binding_admitted");
    Require(!ProfileInfoRebindPolicy.Eligible(true, true, true, false, true), "ambiguous_current_tab_admitted");
    Require(!ProfileInfoRebindPolicy.Eligible(true, true, true, true, false), "stale_info_getter_admitted");
});
Check("decorative_ko_preserves_recovered_single_line_guard", () =>
{
    Require(DecorativeKoGeometryPolicy.SingleLine(1), "valid_single_line_rejected");
    foreach(int lines in new[]{-1,0,2,4})
    {
        Require(!DecorativeKoGeometryPolicy.SingleLine(lines), "multiline_ko_admitted");
        Require(UiVisualSettlement.Assess(2, UiVisualFailure.IntrinsicOverflow, true).Action == UiVisualAction.Restore, "multiline_ko_lease_not_reclaimed");
    }
});
Check("profile_info_callback_ticket_survives_inactivity_without_reading_body", () =>
{
    int bodyReads = 0, writerQueues = 0, activeFrames = 0;
    foreach(var (age,active) in new[]{(1,false),(2,false),(3,false),(4,true),(5,true)})
    {
        activeFrames = active ? activeFrames + 1 : 0;
        var action = ProfileInfoActivationPolicy.Assess(age,true,active,activeFrames);
        if(action == ProfileInfoActivationAction.Activate) { bodyReads++;writerQueues++; }
        else Require(action is ProfileInfoActivationAction.WaitInactive or ProfileInfoActivationAction.WaitActive,"callback_ticket_lost_before_activation");
        if(!active) Require(bodyReads==0&&writerQueues==0,"inactive_body_read_or_owner_queued");
    }
    Require(bodyReads==1&&writerQueues==1,"fresh_active_writer_window_missing");
    Require(ProfileInfoActivationPolicy.Assess(6,false,true,3)==ProfileInfoActivationAction.Invalid,"stale_getter_model_selected_tab_admitted");
    Require(ProfileInfoActivationPolicy.Assess(91,true,false,0)==ProfileInfoActivationAction.Expired,"inactive_wait_unbounded");
    Require(ProfileInfoActivationPolicy.Assess(91,true,true,9)==ProfileInfoActivationAction.Expired,"late_active_writer_admitted");
    Require(ProfileInfoActivationPolicy.Assess(5,true,false,0)==ProfileInfoActivationAction.WaitInactive,"inactive_state_not_reset");
});
Console.WriteLine(JsonSerializer.Serialize(new { status = failed == 0 ? "PASS" : "FAIL", tests = results.Count,
    failures = failed, native_calls = 0, product_assembly = typeof(UiVisualSettlement).Assembly.GetName().Name,
    product_assembly_sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(UiVisualSettlement).Assembly.Location))).ToLowerInvariant(),
    game_validation = "NOT_RUN_IN_CPU_SUITE", results }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;

sealed class FailingTarget(TextTargetState initial) : IUiTextTarget
{
    public TextTargetState State = initial; public bool ReadFails;
    public TextTargetState ReadState() => ReadFails ? throw new IOException("READ") : State;
    public bool TryWriteText(TextTargetState expected, string value) { if (ReadState() != expected) return false; State = State with { Text = value }; return true; }
}
