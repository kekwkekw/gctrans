#nullable enable
using GCMod.Nextgen;
using TMPro;

namespace GCMod.Candidate.Native;

internal sealed partial class BattleNativeProvider
{
    readonly HashSet<string> battleOutcomes = new(StringComparer.Ordinal);

    bool TryBattleFont(UiBinding binding, long ownerGeneration, long uiGeneration, TextMeshProUGUI text,
        string output, Func<bool> currentTarget)
    {
        var donor = fonts.Current; // Managed publication only; no Unity reads before the policy gate.
        var result = BattleFontPolicy.Check(gate.CanAccess, output, donor, () => fonts.Current,
            () => Enabled && currentTarget() && Alive(text) && text.enabled && text.gameObject.activeInHierarchy,
            () => text.richText,
            scalar => donor != null && fonts.Service.Supports(new System.Text.Rune(scalar).ToString(), donor),
            visible => Alive(text.font) && text.font.name == "GameFont" && fonts.Glyphs(text.font, visible));
        BattleOutcome(binding, ownerGeneration, uiGeneration, result.Reason);
        return result.Allowed;
    }

    void BattleOutcome(UiBinding binding, long ownerGeneration, long uiGeneration, string reason)
    {
        // Finite per-binding/owner/UI/phase codes, never text, full trees, raw addresses or parameters.
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string code = $"BATTLE_{key}_O{ownerGeneration}_U{uiGeneration}_{reason}";
        if (battleOutcomes.Count < 1024 && battleOutcomes.Add(code)) log.Record(code);
    }

    void BattleReadback(UiBinding binding, long ownerGeneration, long uiGeneration, UnityTextTarget target, string expected)
    {
        // Same-callback text readback is not mesh/layout settlement or native display acceptance.
        if (!gate.CanAccess) return;
        try
        {
            var state = target.ReadState();
            BattleOutcome(binding, ownerGeneration, uiGeneration,
                state.Active && !state.DestroyedOrRebound && state.Text == expected ? "TEXT_READBACK" : "TEXT_READBACK_CHANGED");
        }
        catch { BattleOutcome(binding, ownerGeneration, uiGeneration, "TEXT_READBACK_UNAVAILABLE"); }
        // Ownership remains intact when a post-write observation fails; shared conditional restore owns it.
    }
}
