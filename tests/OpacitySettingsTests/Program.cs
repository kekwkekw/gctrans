using GCMod.Services;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
var s = new DialogueEditSession(0.7f, 0f, true);
Check(!s.IsDirty && s.Normal == 0.7f, "Opening preserves saved values");
s.Set(false, 0.31f); s.Set(true, 0.84f);
Check(s.IsDirty && s.SavedNormal == 0.7f && s.SavedCg == 0f, "Preview cannot change persisted baseline");
s.Discard();
Check(!s.IsDirty && s.Normal == 0.7f && s.Cg == 0, "Discard restores both saved values");
s.Set(false, 0.31f); s.Accept(); s.Set(false, 0.95f); s.Discard();
Check(s.Normal == 0.31f && !s.IsDirty, "Save then edit then cancel uses latest save");
s.Set(true, 1); s.Reload(0.73f, 0.27f, true);
Check(!s.IsDirty && s.Normal == 0.73f && s.Cg == 0.27f, "Reload replaces stale preview without 10% quantization");
s.Set(false, 0.01f); Check(s.Normal == 0.01f, "One percent remains one percent");
s.Set(false, -0.5f); s.Set(true, 2f);
Check(s.Normal == 0 && s.Cg == 1, "Range is clamped");
foreach (var value in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
    Check(DialogueEditSession.Normalize(value) == 0, "Non-finite values never reach renderer");
s.Reload(0, 0, true); s.Set(false, 1); s.Set(false, 0);
Check(!s.IsDirty, "Moving back to baseline clears dirty flag");
Console.WriteLine($"Opacity edit-session checks passed: {assertions}");

s.SetReadability(false);
Check(s.IsDirty && s.SavedReadability, "Readability preview is dirty without changing saved readability");
s.Discard();
Check(s.Readability && !s.IsDirty, "Cancel restores readability with opacity");
s.SetReadability(false); s.Accept(); s.SetReadability(true); s.Discard();
Check(!s.Readability && !s.IsDirty, "Cancel after save restores latest readability");
s.SetReadability(true); s.Set(true, 0.6f); s.Reload(0.19f, 0, false);
Check(!s.Readability && s.Normal == 0.19f && s.Cg == 0 && !s.IsDirty, "Internal reload replaces all three draft values");
s.SetReadability(true); s.SetReadability(false);
Check(!s.IsDirty, "Toggling readability twice returns to saved state");

s.Reload(0.19f, 0, true, 0); s.SetFrameRate(60);
Check(s.IsDirty && s.SavedFrameRate == 0, "FPS preview preserves game-default baseline");
s.Discard(); Check(s.FrameRate == 0 && !s.IsDirty, "FPS discard preserves advanced cfg baseline");
s.SetFrameRate(30); s.Accept(); s.SetFrameRate(60); s.Discard();
Check(s.FrameRate == 30 && !s.IsDirty, "FPS save/edit/cancel returns to latest save");
s.Reload(0.19f, 0, true, 120);
Check(s.FrameRate == 120 && !s.IsDirty, "Opening never coerces advanced FPS settings");
bool rejected = false;
try { s.SetFrameRate(120); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected && s.FrameRate == 120 && !s.IsDirty, "UI rejects unsupported new values without altering the baseline");

// Reference wrappers with equal native identity must support an explicit comparison rule.
var wrapped = new ValueOverride<Identity>((a, b) => a.Id == b.Id);
wrapped.Apply(new Identity(1), new Identity(2));
wrapped.Apply(new Identity(2), new Identity(2));
Check(wrapped.Restore(new Identity(2)).Id == 1, "Equal native identities retain the original baseline across wrapper copies");

var material = new ValueOverride<string>();
Check(material.Restore("native") == "native", "Disabled style leaves native material unchanged");
Check(material.Apply("native", "private") == "private" && material.Original == "native", "Private material preserves native baseline");
material.Apply("private", "private-updated");
Check(material.Restore("private-updated") == "native", "Repeated style changes restore original, never another styled material");
material.Apply("native", "private");
Check(material.Restore("new-native-font") == "new-native-font", "Native material changes are not overwritten by cancellation");
material.Apply("native", "private"); material.Apply("new-native-font", "new-private");
Check(material.Restore("new-private") == "new-native-font", "Reused glyph captures its new native font baseline");
var spacing = new ValueOverride<float>();
spacing.Apply(0.25f, 0f); spacing.Apply(0f, 0.1f);
Check(spacing.Restore(0.1f) == 0.25f, "Spacing restore uses captured native value, not a hard-coded zero");
spacing.Apply(0.25f, 0f);
Check(spacing.Restore(0.5f) == 0.5f, "Native spacing updates survive turning the override off");

var tempDir = Path.Combine(Path.GetTempPath(), "gcmod-config-recovery-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
var path = Path.Combine(tempDir, "GCMod.cfg");
byte[] snapshot = System.Text.Encoding.UTF8.GetBytes("# 사용자 설정\r\n[Message.Window]\r\nNormalAlpha = 0.7\r\nCgModeAlpha = 0\r\n[Other]\r\nKeep = unchanged\r\n");
try
{
    File.WriteAllBytes(path, snapshot);
    using (var readLock = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        Check(!ConfigFileSnapshot.RestoreIfChanged(path, snapshot), "An unchanged locked file needs no restore write");
    File.WriteAllText(path, "# partial save");
    Check(ConfigFileSnapshot.RestoreIfChanged(path, snapshot), "Partial write is repaired");
    Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(snapshot), "Recovery preserves all bytes, including unrelated settings");
    File.Delete(path);
    Check(ConfigFileSnapshot.RestoreIfChanged(path, snapshot), "Missing file is recreated");
    Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(snapshot), "Recreated file matches the snapshot");
    File.WriteAllText(path, "# damaged and locked");
    bool restoreRejected = false;
    using (var readLock = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try { ConfigFileSnapshot.RestoreIfChanged(path, snapshot); }
        catch (IOException) { restoreRejected = true; }
    }
    Check(restoreRejected, "An actual blocked recovery must not be reported as success");
    Check(ConfigFileSnapshot.RestoreIfChanged(path, snapshot), "Recovery can be retried after the lock is released");
}
finally
{
    File.Delete(path);
    Directory.Delete(tempDir);
}
Console.WriteLine($"Dialogue state, render override and file-recovery checks passed: {assertions}");
sealed class Identity(int id) { public int Id { get; } = id; }
