using BepInEx.Configuration;
using GCMod;
using GCMod.Patches;
using GCMod.Services;
using Utility.Notifications;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
// Invoke the production Unity callback with a lightweight MonoBehaviour adapter.
// Native component registration and actual popup destruction still require game testing.
var updater = new DialogueSettingsUpdater();
var update = (Action)typeof(DialogueSettingsUpdater).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
    .CreateDelegate(typeof(Action), updater);
var directory = Path.Combine(Path.GetTempPath(), "gcmod-dialogue-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "GCMod.cfg");
try
{
    Plugin.ConfigFile = new ConfigFile(path, false);
    Config.Bind();
    var unrelated = Plugin.ConfigFile.Bind("Other", "Keep", "사용자 설정");
    Plugin.ConfigFile.Save();
    DialogueSettings.Initialize();
    byte[] original = File.ReadAllBytes(path);
    DialogueSettings.Begin();
    DialogueSettings.Preview(false, 0.31f); DialogueSettings.Preview(true, 0.84f);
    DialogueSettings.ToggleReadability(); update();
    Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(original), "Combined preview never writes cfg");
    Check(Config.ModifyText.Value && Config.NormalAlpha.Value == 0.7f && !TextStylePatch.Enabled && VisualPatch.Normal == 0.31f,
        "Live preview changes renderer without changing persisted values");
    DialogueSettings.End();
    Check(TextStylePatch.Enabled && VisualPatch.Normal == 0.7f && VisualPatch.Cg == 0f, "Close restores all previewed settings");

    DialogueSettings.Begin(); DialogueSettings.ToggleReadability(); DialogueSettings.Preview(false, 0.6f);
    Check(DialogueSettings.Commit(), "Combined readability/opacity save succeeds");
    Check(!Config.ModifyText.Value && Config.NormalAlpha.Value == 0.6f && !DialogueSettings.Edit.IsDirty, "Commit accepts all three values");
    Check(Plugin.ConfigFile.SaveOnConfigSet && unrelated.Value == "사용자 설정", "Commit preserves auto-save mode and unrelated value");
    DialogueSettings.ToggleReadability(); DialogueSettings.End();
    Check(!TextStylePatch.Enabled, "Discard after save returns to the new saved baseline");

    DialogueSettings.Begin(); DialogueSettings.ToggleReadability(); DialogueSettings.Preview(true, 0.5f);
    byte[] saved = File.ReadAllBytes(path);
    using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        Check(!DialogueSettings.Commit(), "Real BepInEx Save rejects a locked cfg");
        Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(saved), "Failed combined save preserves every byte");
        Check(!Config.ModifyText.Value && Config.CgModeAlpha.Value == 0f, "Failed save rolls back both boolean and numeric config memory");
        update();
        Check(DialogueSettings.Edit.IsDirty && TextStylePatch.Enabled && VisualPatch.Cg == 0.5f, "Failure keeps preview available for retry");
        Check(Plugin.ConfigFile.SaveOnConfigSet && !DialogueSettings.Committing, "Failure restores auto-save and commit flags");
        Check(!Plugin.Log.Errors.Any(x => x.Contains("restore failed")), "Unchanged locked cfg never produces a false restore failure");
    }
    Check(DialogueSettings.Commit(), "Same preview can be saved after unlocking");
    DialogueSettings.End();
    Check(Config.ModifyText.Value && Config.CgModeAlpha.Value == 0.5f && TextStylePatch.Enabled, "Retry persists combined values");

    int notices = Toast.Infos, errors = Toast.Errors;
    saved = File.ReadAllBytes(path);
    using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) DialogueSettings.ToggleReadability();
    Check(Config.ModifyText.Value && TextStylePatch.Enabled && DialogueSettings.Edit == null,
        "Late readability callback outside options is ignored");
    Check(Toast.Infos == notices && Toast.Errors == errors && File.ReadAllBytes(path).AsSpan().SequenceEqual(saved),
        "No hidden immediate-save path or save-failure notice outside options");
    DialogueSettings.Preview(false, 0.9f); DialogueSettings.PreviewFrameRate(30); update();
    Check(Config.NormalAlpha.Value == 0.6f && Config.FrameRate.Value == 60 && File.ReadAllBytes(path).AsSpan().SequenceEqual(saved),
        "Late slider/FPS callbacks cannot change saved values");

    DialogueSettings.Begin(); DialogueSettings.ToggleReadability(); DialogueSettings.Preview(false, 0.9f);
    var external = new ConfigFile(path, false);
    external.Bind("Message.Window", "NormalAlpha", 0f).Value = 0.19f;
    external.Bind("Message.Text", "Modified", true).Value = false;
    external.Save();
    Plugin.ConfigFile.Reload(); update();
    Check(!DialogueSettings.Edit.IsDirty && !DialogueSettings.Edit.Readability && DialogueSettings.Edit.Normal == 0.19f,
        "Internal config reload is synchronized by the updater without a hotkey");
    Check(!TextStylePatch.Enabled && VisualPatch.Normal == 0.19f, "Reload immediately reapplies saved display values");
    DialogueSettings.ToggleReadability();
    saved = File.ReadAllBytes(path);
    File.Delete(path);
    Check(!DialogueSettings.Commit() && !Config.ModifyText.Value && DialogueSettings.Edit.IsDirty,
        "Missing snapshot rejects save without accepting the preview");
    File.WriteAllBytes(path, saved);
    Check(DialogueSettings.Commit(), "Save recovers after missing cfg is restored");
    var reread = new ConfigFile(path, false);
    Check(reread.Bind("Other", "Keep", "lost").Value == "사용자 설정", "Unrelated values survive saves, failure, retry and reload");

    // Exercise the actual FPS service and transaction with a native frame-target adapter.
    DialogueSettings.End();
    FrameRateSettings.NativeSettingsApplied();
    Check(UnityEngine.Application.targetFrameRate == 60, "Configured FPS is applied at native settings boundary");
    int calls = Gc.GcOptionData.Calls.Count;
    DialogueSettings.Apply(); DialogueSettings.Apply();
    Check(Gc.GcOptionData.Calls.Count == calls, "Unchanged frame target is not rewritten on unrelated display refresh");
    DialogueSettings.Begin();
    saved = File.ReadAllBytes(path);
    DialogueSettings.PreviewFrameRate(30);
    Check(UnityEngine.Application.targetFrameRate == 30 && Config.FrameRate.Value == 60 && DialogueSettings.Edit.IsDirty,
        "FPS selection previews immediately without updating persisted config memory");
    Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(saved), "FPS preview never saves cfg");
    DialogueSettings.End();
    Check(UnityEngine.Application.targetFrameRate == 60, "Closing unsaved FPS preview restores saved 60 immediately");

    DialogueSettings.Begin(); DialogueSettings.PreviewFrameRate(30); DialogueSettings.ToggleReadability();
    using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        Check(!DialogueSettings.Commit(), "Combined FPS/readability save rejects file lock");
        Check(Config.FrameRate.Value == 60 && UnityEngine.Application.targetFrameRate == 30 && DialogueSettings.Edit.IsDirty,
            "Save failure rolls cfg back while retaining selected FPS preview for retry");
        Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(saved), "Combined FPS failure preserves exact original file");
    }
    Check(DialogueSettings.Commit() && Config.FrameRate.Value == 30 && !DialogueSettings.Edit.IsDirty,
        "FPS and readability save together after lock is released");
    DialogueSettings.PreviewFrameRate(60); DialogueSettings.End();
    Check(UnityEngine.Application.targetFrameRate == 30, "Save then edit then close restores latest saved FPS");
    DialogueSettings.Begin();
    Check(DialogueSettings.Edit.FrameRate == 30, "Reentry loads saved FPS");
    DialogueSettings.End();

    // No hard-coded native FPS: a game's 15 FPS update must survive cancelling a 60 FPS preview.
    Config.FrameRate.Value = 0; DialogueSettings.Reload();
    UnityEngine.Application.targetFrameRate = 15;
    FrameRateSettings.NativeSettingsApplied();
    DialogueSettings.Begin(); DialogueSettings.PreviewFrameRate(60); DialogueSettings.End();
    Check(Config.FrameRate.Value == 0 && UnityEngine.Application.targetFrameRate == 15,
        "Cancel returns an advanced game-default cfg to its actual native target");
    DialogueSettings.Begin(); DialogueSettings.PreviewFrameRate(60);
    UnityEngine.Application.targetFrameRate = 30;
    FrameRateSettings.NativeSettingsApplied();
    Check(UnityEngine.Application.targetFrameRate == 60, "Native settings reapply keeps active 60 FPS draft");
    DialogueSettings.End();
    Check(UnityEngine.Application.targetFrameRate == 30, "Cancelling that draft follows the latest native target");

    Config.FrameRate.Value = 120; DialogueSettings.Reload(); DialogueSettings.Begin();
    DialogueSettings.Preview(false, 0.22f); Check(DialogueSettings.Commit(), "Unrelated edit can save an advanced FPS cfg");
    Check(Config.FrameRate.Value == 120 && DialogueSettings.Edit.FrameRate == 120, "Untouched advanced FPS is not silently rounded to 30 or 60");
    DialogueSettings.PreviewFrameRate(30);
    external = new ConfigFile(path, false);
    external.Bind("General", "FrameRate", 60).Value = 0; external.Save();
    Plugin.ConfigFile.Reload(); update();
    Check(DialogueSettings.Edit.FrameRate == 0 && !DialogueSettings.Edit.IsDirty && UnityEngine.Application.targetFrameRate == 30,
        "Internal reload discards FPS draft and immediately restores game-default target");
    DialogueSettings.End();
    Config.FrameRate.Value = 60; update();
    Check(UnityEngine.Application.targetFrameRate == 60, "External config change applies on main-thread tick");

    DialogueSettings.Begin(); DialogueSettings.Preview(false, 0.81f); DialogueSettings.ToggleReadability();
    saved = File.ReadAllBytes(path);
    int checks = SettingsUiPatch.LifetimeChecks;
    for (int i = 0; i < 30; i++) update();
    Check(DialogueSettings.Edit.IsDirty && VisualPatch.Normal == 0.81f && File.ReadAllBytes(path).AsSpan().SequenceEqual(saved),
        "Idle frames preserve unsaved preview and never write cfg");
    Check(SettingsUiPatch.LifetimeChecks == checks + 30, "The input-free component continues checking popup lifetime every frame");
    SettingsUiPatch.OnCheckLifetime = () => DialogueSettings.End();
    update();
    SettingsUiPatch.OnCheckLifetime = null;
    Check(DialogueSettings.Edit == null && VisualPatch.Normal == Config.NormalAlpha.Value && TextStylePatch.Enabled == Config.ModifyText.Value,
        "Popup lifetime cleanup restores saved display through the updater");
    Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(saved), "Lifetime cleanup never commits the discarded preview");
    FrameRateSettings.RestoreNative();
    Check(UnityEngine.Application.targetFrameRate == 30, "Unload restores captured native target");
    var restarted = new ConfigFile(path, false);
    Check(restarted.Bind("General", "FrameRate", -1).Value == 60, "A fresh ConfigFile reads the saved FPS");
    Console.WriteLine($"Production dialogue settings with BepInEx/file-lock checks passed: {assertions}");
}
finally
{
    if (File.Exists(path)) File.Delete(path);
    Directory.Delete(directory);
}
