using Gc;
using UnityEngine;

namespace GCMod.Services;

public static class FrameRateSettings
{
    private static int? nativeTarget, appliedTarget;

    // Called after the game has applied its own power-saving policy, before our override.
    public static void NativeSettingsApplied()
    {
        nativeTarget = Application.targetFrameRate;
        appliedTarget = null;
        Apply();
    }

    public static void Apply()
    {
        int current = Application.targetFrameRate;
        // Observe a native change instead of restoring a stale hard-coded 30 FPS later.
        if (!nativeTarget.HasValue || !appliedTarget.HasValue || current != appliedTarget.Value)
            nativeTarget = current;
        int requested = DialogueSettings.Edit?.FrameRate ?? Config.FrameRate.Value;
        int target = requested > 0 ? requested : nativeTarget.Value;
        if (current != target) GcOptionData.ChangeApplicationTargetFrameRate(target);
        appliedTarget = requested > 0 ? target : null;
    }

    public static void RestoreNative()
    {
        if (nativeTarget.HasValue && appliedTarget.HasValue && Application.targetFrameRate == appliedTarget.Value)
            GcOptionData.ChangeApplicationTargetFrameRate(nativeTarget.Value);
        nativeTarget = appliedTarget = null;
    }
}
