using System;
using System.IO;
using GCMod.Patches;
using Utility.Notifications;

namespace GCMod.Services;

public static class DialogueSettings
{
    public static DialogueEditSession Edit { get; private set; }
    public static bool ReadabilityEnabled => Edit?.Readability ?? Config.ModifyText.Value;
    public static bool Committing { get; private set; }
    private static bool refreshPending;

    public static void Initialize()
    {
        Config.NormalAlpha.SettingChanged += (_, _) => refreshPending = true;
        Config.CgModeAlpha.SettingChanged += (_, _) => refreshPending = true;
        Config.ModifyText.SettingChanged += (_, _) => refreshPending = true;
        Config.FrameRate.SettingChanged += (_, _) => refreshPending = true;
        Plugin.ConfigFile.ConfigReloaded += (_, _) => refreshPending = true;
    }
    public static void Begin() => Edit = Snapshot();
    private static DialogueEditSession Snapshot() => new(Config.NormalAlpha.Value, Config.CgModeAlpha.Value, Config.ModifyText.Value, Config.FrameRate.Value);
    public static void End() { Edit = null; Apply(); }
    public static void Reload()
    {
        refreshPending = false;
        Edit?.Reload(Config.NormalAlpha.Value, Config.CgModeAlpha.Value, Config.ModifyText.Value, Config.FrameRate.Value);
        SettingsUiPatch.Refresh();
        Apply();
    }
    public static void Tick()
    {
        SettingsUiPatch.CheckLifetime();
        if (refreshPending && !Committing) Reload();
    }
    public static void Preview(bool cg, float value)
    {
        if (Edit == null) return;
        Edit.Set(cg, value);
        SettingsUiPatch.Refresh();
        Apply();
    }
    public static void ToggleReadability()
    {
        // Ignore late UI callbacks after the options session has ended.
        if (Edit == null) return;
        Edit.SetReadability(!Edit.Readability);
        SettingsUiPatch.Refresh();
        Apply();
    }
    public static void PreviewFrameRate(int value)
    {
        if (Edit == null) return;
        Edit.SetFrameRate(value);
        SettingsUiPatch.Refresh();
        FrameRateSettings.Apply();
    }
    public static void Apply()
    {
        FrameRateSettings.Apply();
        VisualPatch.ApplyBackgrounds(Edit?.Normal ?? Config.NormalAlpha.Value, Edit?.Cg ?? Config.CgModeAlpha.Value);
        TextStylePatch.ApplyTracked();
    }

    // Save only at the explicit UI boundary. Keep the existing config's other entries.
    public static bool Commit() => Commit(Edit);
    private static bool Commit(DialogueEditSession edit)
    {
        if (edit == null || !edit.IsDirty) return true;
        var file = Plugin.ConfigFile;
        float oldNormal = Config.NormalAlpha.Value, oldCg = Config.CgModeAlpha.Value;
        bool oldReadability = Config.ModifyText.Value;
        int oldFrameRate = Config.FrameRate.Value;
        bool autoSave = file.SaveOnConfigSet;
        byte[] before = null;
        bool saveStarted = false;
        try
        {
            before = File.ReadAllBytes(file.ConfigFilePath);
            Committing = true;
            file.SaveOnConfigSet = false;
            Config.NormalAlpha.Value = edit.Normal;
            Config.CgModeAlpha.Value = edit.Cg;
            Config.ModifyText.Value = edit.Readability;
            Config.FrameRate.Value = edit.FrameRate;
            saveStarted = true;
            file.Save();
            edit.Accept();
            Plugin.Log.LogInfo($"[DialogueSettings] Saved normal={edit.Normal:0.####}, cg={edit.Cg:0.####}, readability={edit.Readability}, fps={edit.FrameRate}");
            return true;
        }
        catch (Exception ex)
        {
            Config.NormalAlpha.Value = oldNormal;
            Config.CgModeAlpha.Value = oldCg;
            Config.ModifyText.Value = oldReadability;
            Config.FrameRate.Value = oldFrameRate;
            if (saveStarted && before != null)
            {
                try { ConfigFileSnapshot.RestoreIfChanged(file.ConfigFilePath, before); }
                catch (Exception restore) { Plugin.Log.LogError("[DialogueSettings] Config restore failed: " + restore.Message); }
            }
            Plugin.Log.LogError("[DialogueSettings] Save failed: " + ex.Message);
            Toast.Error("GCMod", "설정 저장에 실패했습니다. 설정 파일을 확인한 뒤 다시 저장해 주세요.");
            return false;
        }
        finally
        {
            file.SaveOnConfigSet = autoSave;
            Committing = false;
            refreshPending = false;
            Apply();
            SettingsUiPatch.Refresh();
        }
    }
}
