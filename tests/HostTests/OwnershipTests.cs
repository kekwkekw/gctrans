using GCMod.Nextgen;

namespace HostTests;
internal static partial class Program
{
    static TextTargetState State(ulong pointer = 123, string source = "一括ダウンロード", long owner = 3, long sourceGen = 4,
        string identity = "\"asset-key:download\"", long lifetime = 1) => new(new(pointer, 42, "TMPro.TextMeshProUGUI", lifetime), source, owner, sourceGen, identity, source);
    static OwnershipRequest Request(TextTargetState state, string binding = "binding.download", long generation = 1,
        string module = "Core", string slot = "download-label", string? coMatch = null) => new(module, binding, coMatch, slot, generation, state);
    static void TestOwnership()
    {
        Check("owned_self_restore_and_epoch_cleanup", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            Assert(ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드").Written, "write failed");
            Assert(ownership.LeaseCount == 1, "lease missing");
            var transition = ownership.Transition(1, 2);
            Assert(transition.Restored == 1 && transition.Released == 1 && ownership.LeaseCount == 0 && target.ReadState().Text == "一括ダウンロード", "self change not restored before epoch cleanup");
        });
        Check("physical_property_exclusive_across_modules_bindings_slots", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드");
            Assert(!ownership.TryWrite(target, Request(target.ReadState(), "binding.competing", module: "Chrome", slot: "other-slot"), "경쟁 값").Written, "metadata namespace bypassed physical exclusivity");
            Assert(target.Writes == 1 && target.ReadState().Text == "일괄 다운로드", "other writer changed owned text");
        });
        Check("explicit_comatch_exact_output_single_write", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState(), coMatch: "co.footer"), "일괄 다운로드");
            Assert(!ownership.TryWrite(target, Request(target.ReadState(), "binding.comatch", coMatch: "co.footer"), "일괄 다운로드").Written, "co-match wrote twice");
            Assert(target.Writes == 1 && ownership.LeaseCount == 1, "co-match duplicate lease");
        });
        Check("restore_preserves_external_writer_current_value", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드");
            target.GameWrite("新しいゲーム表示 17");
            Assert(!ownership.RestoreTarget(target.ReadState().Identity) && target.ReadState().Text == "新しいゲーム表示 17", "old JP overwrote current game value");
        });
        Check("reused_cell_source_owner_no_old_restore", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드");
            target.Recycle(State(source: "新しい表示", owner: 9, sourceGen: 10, identity: "\"new-model\""));
            var transition = ownership.Transition(1, 2);
            Assert(transition.Restored == 0 && transition.ExternalChangesPreserved == 1 && target.ReadState().Text == "新しい表示", "recycled cell restored obsolete source");
        });
        Check("reused_cell_new_binding_can_acquire_same_epoch", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드");
            target.Recycle(State(source: "新しい表示", owner: 9, sourceGen: 10, identity: "\"new-model\""));
            Assert(ownership.TryWrite(target, Request(target.ReadState(), "binding.new-model"), "새 표시").Written, "obsolete lease blocked recycled current binding");
            Assert(target.ReadState().Text == "새 표시", "new model value not supplied");
        });
        Check("late_result_dropped_after_generation_transition", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            var oldRequest = Request(target.ReadState()); ownership.Transition(1, 2);
            Assert(!ownership.TryWrite(target, oldRequest, "늦은 값").Written && target.Writes == 0, "late result wrote in new epoch");
        });
        Check("stale_source_owner_context_cannot_write", () => {
            var target = new MemoryTextTarget(State()); var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            var request = Request(target.ReadState());
            target.Recycle(State(owner: 5));
            Assert(!ownership.TryWrite(target, request, "오래된 값").Written, "stale owner context wrote");
        });
        Check("same_native_object_different_wrapper_conflict", () => {
            var target = new MemoryTextTarget(State()); var wrapper = new TargetWrapper(target);
            var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            ownership.TryWrite(target, Request(target.ReadState()), "일괄 다운로드");
            Assert(!ownership.TryWrite(wrapper, Request(wrapper.ReadState(), "other.wrapper", module: "Residuals"), "다른 wrapper 값").Written, "wrapper managed identity bypassed ownership");
            Assert(ownership.RestoreTarget(wrapper.ReadState().Identity), "physical identity could not restore own change");
        });
        Check("changed_at_write_boundary_does_not_acquire_lease", () => {
            var inner = new MemoryTextTarget(State()); var target = new RaceTarget(inner);
            var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            Assert(!ownership.TryWrite(target, Request(inner.ReadState()), "일괄 다운로드").Written && ownership.LeaseCount == 0, "failed conditional write acquired lease");
            Assert(inner.ReadState().Text == "原ゲーム更新", "write race overwrote game update");
        });
        Check("ownership_reject_epoch_rewind", () => {
            var ownership = new UiOwnershipService(); ownership.OpenEpoch(2);
            bool rejected = false; try { ownership.Transition(2, 1); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "epoch rewind accepted");
        });
        Check("ownership_reject_wrong_old_epoch", () => {
            var ownership = new UiOwnershipService(); ownership.OpenEpoch(2);
            bool rejected = false; try { ownership.Transition(1, 3); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "stale transition accepted");
        });
        Check("inactive_target_and_null_native_identity_noop", () => {
            var ownership = new UiOwnershipService(); ownership.OpenEpoch(1);
            var inactive = new MemoryTextTarget(State() with { Active = false });
            var invalid = new MemoryTextTarget(State(pointer: 0));
            Assert(!ownership.TryWrite(inactive, Request(inactive.ReadState()), "값").Written && !ownership.TryWrite(invalid, Request(invalid.ReadState()), "값").Written, "invalid target wrote");
        });
        Check("lease_collection_bounded", () => {
            var ownership = new UiOwnershipService(maximumLeases: 1); ownership.OpenEpoch(1);
            var first = new MemoryTextTarget(State()); var second = new MemoryTextTarget(State(456));
            Assert(ownership.TryWrite(first, Request(first.ReadState()), "값").Written && !ownership.TryWrite(second, Request(second.ReadState()), "값").Written && ownership.LeaseCount == 1, "lease bound ignored");
        });
        TestFont();
    }
    sealed class TargetWrapper(IUiTextTarget target) : IUiTextTarget
    {
        public TextTargetState ReadState() => target.ReadState();
        public bool TryWriteText(TextTargetState expected, string value) => target.TryWriteText(expected, value);
    }
    sealed class RaceTarget(MemoryTextTarget target) : IUiTextTarget
    {
        public TextTargetState ReadState() => target.ReadState();
        public bool TryWriteText(TextTargetState expected, string value) { target.GameWrite("原ゲーム更新"); return target.TryWriteText(expected, value); }
    }
    sealed class Glyphs(bool available = true) : IFontGlyphProvider
    { public bool HasGlyph(FontDonorHandle donor, int scalar) => available; }
    static void TestFont()
    {
        Check("approved_Medium_explicit_donor_generation", () => {
            var font = new FontService(); var medium = new FontDonorHandle(FontService.ApprovedProfile, 1, 2, 1);
            font.PublishMediumDonor(medium, new Glyphs());
            Assert(font.Supports("일괄 다운로드", medium), "approved donor cannot provide glyphs");
            var next = medium with { Generation = 2 }; font.PublishMediumDonor(next, new Glyphs());
            font.Invalidate(1);
            Assert(font.Current == next && !font.Supports("값", medium), "stale font invalidation changed new donor");
            font.Invalidate(2); Assert(font.Current == null, "current donor not invalidated");
        });
        Check("font_missing_supplementary_glyph_not_low16bit_control", () => {
            var font = new FontService(); var medium = new FontDonorHandle(FontService.ApprovedProfile, 1, 2, 1);
            font.PublishMediumDonor(medium, new Glyphs(false));
            Assert(!font.Supports(char.ConvertFromUtf32(0x1000A), medium), "supplementary missing glyph bypassed check via control-char cast");
        });
        Check("invalidated_donor_rejects_late_same_generation_republish", () => {
            var font = new FontService(); var medium = new FontDonorHandle(FontService.ApprovedProfile, 1, 2, 1);
            font.PublishMediumDonor(medium, new Glyphs()); font.Invalidate(1);
            bool rejected = false; try { font.PublishMediumDonor(medium, new Glyphs()); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && font.Current == null, "disposed donor republished by late callback");
            font.PublishMediumDonor(medium with { Generation = 2 }, new Glyphs());
            Assert(font.Current?.Generation == 2, "fresh font lifetime rejected");
        });
        Check("font_invalidation_before_publication_fences_cancelled_lifetime", () => {
            var font = new FontService(); font.Invalidate(2);
            bool rejected = false;
            try { font.PublishMediumDonor(new(FontService.ApprovedProfile, 1, 2, 2), new Glyphs()); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && font.Current == null, "cancelled unpublished lifetime revived");
            font.PublishMediumDonor(new(FontService.ApprovedProfile, 1, 2, 3), new Glyphs());
            font.Invalidate(2); Assert(font.Current?.Generation == 3, "stale invalidation removed newer donor");
            font.Invalidate(4); Assert(font.Current?.Generation == 3, "invalidation of an unpublished future lifetime removed current donor");
            rejected = false;
            try { font.PublishMediumDonor(new(FontService.ApprovedProfile, 1, 2, 4), new Glyphs()); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "cancelled future lifetime bypassed publication fence");
        });
        Check("unapproved_font_profile_rejected", () => {
            bool rejected = false; try { new FontService().PublishMediumDonor(new("unapproved", 1, 2, 1), new Glyphs()); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "unapproved font published");
        });
    }
}
