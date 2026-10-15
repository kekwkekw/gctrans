using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GCMod.Nextgen;

var results = new List<object>(); int failed = 0;
void Check(string name, Action body)
{
    try { body(); results.Add(new { name, status = "PASS" }); }
    catch (Exception failure) { failed++; results.Add(new { name, status = "FAIL", reason = failure.Message }); }
}
void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

Check("atomic_group_requires_every_prepared_option", () =>
{
    int shown = 0, rolledBack = 0;
    var result = ChoiceGroupTransaction.TryCommitDetailed(4, ordinal => ordinal != 2, () => true, _ => shown++, () => rolledBack++);
    Require(!result.Committed && result.Failure?.Reason == ChoiceDisplayReason.OptionNotPrepared && result.Failure.OptionOrdinal == 2 && shown == 0 && rolledBack == 1, "partial preparation must not show any option");
});
Check("precommit_control_failure_keeps_role_reason", () =>
{
    int shown = 0, rollback = 0;
    var result = ChoiceGroupTransaction.TryCommitDetailed(4, _ => true, () => throw new ChoiceDisplayException(ChoiceDisplayReason.NativeControlsChanged), _ => shown++, () => rollback++);
    Require(result.Failure?.Phase == ChoiceDisplayPhase.PrecommitControls && result.Failure.Reason == ChoiceDisplayReason.NativeControlsChanged && shown == 0 && rollback == 1, "precommit reason missing");
});
Check("raycast_failure_is_distinct_from_control_failure", () =>
{
    var result = ChoiceGroupTransaction.TryCommitDetailed(3, _ => true, () => throw new ChoiceDisplayException(ChoiceDisplayReason.NativeRaycastChanged), _ => throw new Exception("must not show"), () => { });
    Require(result.Failure?.Reason == ChoiceDisplayReason.NativeRaycastChanged, "raycast reason missing");
});
Check("partial_show_exception_recovers_all_owned_options", () =>
{
    bool[] visuals = new bool[4]; string[] original = ["jp1", "jp2", "jp3", "jp4"]; var baseline = original.ToArray();
    var result = ChoiceGroupTransaction.TryCommitDetailed(4, _ => true, () => original.SequenceEqual(baseline), ordinal => { visuals[ordinal] = true; if (ordinal == 1) throw new InvalidOperationException("private text/path must not escape"); }, () => Array.Clear(visuals));
    Require(result.Failure?.Phase == ChoiceDisplayPhase.Show && result.Failure.OptionOrdinal == 1 && result.Failure.Exception == ChoiceExceptionKind.InvalidOperation && visuals.All(value => !value) && original.SequenceEqual(baseline), "atomic rollback/control preservation failed");
});
Check("postcommit_changes_rollback_whole_group", () =>
{
    int boundaryReads = 0; bool[] visible = new bool[4];
    var result = ChoiceGroupTransaction.TryCommitDetailed(4, _ => true, () => ++boundaryReads == 1, ordinal => visible[ordinal] = true, () => Array.Clear(visible));
    Require(result.Failure?.Phase == ChoiceDisplayPhase.Postcommit && !result.Committed && visible.All(value => !value), "postcommit rollback failed");
});
Check("first_failure_survives_cleanup_failure_and_rollback_runs_once", () =>
{
    int rollback = 0;
    var result = ChoiceGroupTransaction.TryCommitDetailed(4, _ => true, () => throw new ChoiceDisplayException(ChoiceDisplayReason.NativeRaycastChanged), _ => { }, () => { rollback++; throw new IOException("private cleanup path"); });
    Require(result.Failure?.Reason == ChoiceDisplayReason.NativeRaycastChanged && result.Failure.CleanupException == ChoiceExceptionKind.Other && rollback == 1, "cleanup erased first failure or repeated rollback");
});
Check("variable_group_sizes_preserve_legacy_boolean_api", () =>
{
    foreach (int count in new[] { 1, 3, 4, 5, 32 })
    {
        int show = 0, rollback = 0;
        Require(ChoiceGroupTransaction.TryCommit(count, _ => true, () => true, _ => show++, () => rollback++), "valid supported group rejected");
        Require(show == count && rollback == 0, "group count must not be permanently four");
    }
    foreach (int count in new[] { 0, 33 }) Require(!ChoiceGroupTransaction.TryCommit(count, _ => true, () => true, _ => { }, () => { }), "unsafe option budget accepted");
});
Check("verified_add_release_lifecycle_does_not_require_shared_open", () =>
{
    Require(StoryChoicePolicy.RequiredLifecycleAvailable(true,true), "verified producer/release falsely require Open");
    Require(!StoryChoicePolicy.RequiredLifecycleAvailable(false,true) && !StoryChoicePolicy.RequiredLifecycleAvailable(true,false) && !StoryChoicePolicy.RequiredLifecycleAvailable(false,false), "missing required lifecycle falsely admitted");
});
Check("Medium_Hangul_role_does_not_require_primary_whitespace_or_ASCII", () =>
{
    var reads=new List<int>();var result=ChoiceFontRolePolicy.ProbeDonor("가 A",scalar=>{reads.Add(scalar);return scalar==0xAC00;});
    Require(result.FirstMissingScalar==0x20&&result.FirstMissingRequiredScalar==null&&reads.SequenceEqual(new[]{0xAC00,0x20,0x41}),"primary-owned whitespace/ASCII incorrectly requires donor coverage");
    Require(ChoiceFontRolePolicy.Kind(0x20)==ChoiceGlyphKind.Whitespace&&ChoiceFontRolePolicy.Kind(0xAC00)==ChoiceGlyphKind.Hangul,"missing scalar role misclassified");
});
Check("missing_Hangul_donor_and_missing_primary_route_still_reject", () =>
{
    var result=ChoiceFontRolePolicy.ProbeDonor("가 A",scalar=>scalar!=0xAC00);
    Require(result.FirstMissingRequiredScalar==0xAC00,"missing Hangul donor bypassed");
    Require(ChoiceFontRolePolicy.FirstMissingScalar("가 A",scalar=>scalar!=0x41)==0x41,"full primary/fallback route lost ASCII requirement");
    Require(ChoiceFontRolePolicy.FirstMissingScalar("가 A",_=>true)==null,"complete route falsely failed");
});
Check("every_phase_uses_closed_bounded_diagnostic_vocabulary", () =>
{
    foreach (var phase in Enum.GetValues<ChoiceDisplayPhase>())
    foreach (var reason in Enum.GetValues<ChoiceDisplayReason>())
    {
        var failure = new ChoiceCommitFailure(phase, reason, 31, ChoiceExceptionKind.Other);
        string code = ChoiceDisplayDiagnostics.Code(failure, "private group/raw text/path");
        Require(code.Length <= 96 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') && !code.Contains("private"), "diagnostic escaped closed code boundary");
    }
    Require(ChoiceDisplayDiagnostics.Reason(new Exception("arbitrary source text/token")) == ChoiceDisplayReason.IsolatedException, "exception message leaked into reason");
});
Check("native_control_proof_reports_first_changed_role_without_relaxing_equality", () =>
{
    var cases = new[] {
        ("{\"rows\":[{\"source\":\"old\"}]}", "{\"rows\":[{\"source\":\"new\"}]}", ChoiceControlRole.Source),
        ("{\"rows\":[{\"branch\":1}]}", "{\"rows\":[{\"branch\":2}]}", ChoiceControlRole.Branch),
        ("{\"rows\":[{\"inventory\":[{\"components\":[1]}]}]}", "{\"rows\":[{\"inventory\":[{\"components\":[2]}]}]}", ChoiceControlRole.NativeComponent),
        ("{\"rows\":[{\"textRect\":{\"position\":[1,2]}}]}", "{\"rows\":[{\"textRect\":{\"position\":[2,2]}}]}", ChoiceControlRole.NativeGeometry),
        ("{\"rows\":[{\"navigation\":1}]}", "{\"rows\":[{\"navigation\":2}]}", ChoiceControlRole.Navigation)
    };
    foreach (var (before, after, role) in cases) Require(ChoiceDisplayDiagnostics.ControlsChanged(before, after) == role, "changed native role missed");
    Require(ChoiceDisplayDiagnostics.ControlsChanged(cases[0].Item1, cases[0].Item1) == ChoiceControlRole.None, "unchanged proof changed");
    Require(ChoiceDisplayDiagnostics.ControlsChanged("{", "{}") == ChoiceControlRole.MalformedProof, "malformed proof silently accepted");
});
Check("failed_owned_cleanup_attempts_later_options_and_preserves_retry", () =>
{
    bool[] hidden = new bool[4], restored = new bool[4], scheduled = new bool[4]; bool first = true;
    void Mask(int ordinal) { if (ordinal == 1 && first) { first = false; throw new InvalidOperationException("injected owned setter failure"); } restored[ordinal] = true; }
    var outcome = ChoiceOwnedCleanup.Run(4, i => hidden[i] = true, Mask, i => scheduled[i] = true);
    Require(!outcome.Completed && outcome.OptionOrdinal == 1 && outcome.Role == ChoiceCleanupRole.Mask && hidden.All(x => x) && scheduled.All(x => x) && restored[3], "one failed setter stranded later labels");
    var retry = ChoiceOwnedCleanup.Run(4, i => hidden[i] = true, Mask, i => scheduled[i] = true);
    Require(retry.Completed && restored.All(x => x), "retained ownership could not finish retry");
});
Check("deferred_destroy_blocks_retry_until_later_frame_and_all_owned_objects_gone", () =>
{
    var barrier = new ChoiceCleanupBarrier(); barrier.Retire(20); int reads = 0;
    Require(!barrier.Confirm(20, () => { reads++; return true; }) && reads == 0, "same-frame deferred cleanup claimed complete");
    Require(!barrier.Confirm(21, () => false) && barrier.Pending, "alive owned component accepted as cleaned");
    bool rejected = false; try { barrier.Retire(21); } catch (InvalidOperationException) { rejected = true; }
    Require(rejected && barrier.Pending, "second retirement overwrote live old generation");
    Require(barrier.Confirm(22, () => true) && !barrier.Pending, "later verified cleanup did not release barrier");
    barrier.Retire(23); Require(!barrier.Confirm(23, () => true), "owner-reused group bypassed new destruction barrier");
});
Check("cleanup_probe_failure_retains_old_generation", () =>
{
    var barrier = new ChoiceCleanupBarrier(); barrier.Retire(1);
    try { barrier.Confirm(2, () => throw new InvalidOperationException("read failed")); } catch (InvalidOperationException) { }
    Require(barrier.Pending && barrier.Confirm(3, () => true), "failed destruction read lost pending generation");
});

var assembly = typeof(ChoiceGroupTransaction).Assembly.Location;
var report = new { status = failed == 0 ? "PASS" : "FAIL", candidate_dll_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), passed = results.Count - failed, failed, cases = results, native_calls = 0, native = "NOT_RUN_IN_CPU_SUITE", scope = "Actual candidate library transaction/diagnostic/owned-cleanup policies; no Unity/native renderer/font/raycast behavior simulated as native PASS." };
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json); if (args.Length > 0) File.WriteAllText(args[0], json); return failed == 0 ? 0 : 1;
