using BepInEx.Configuration;

// Real BepInEx configuration and production service; only Unity rendering/toasts are adapters.
namespace GCMod
{
    public static class Plugin
    {
        public static ConfigFile ConfigFile;
        public static TestLog Log = new();
    }
    public sealed class TestLog
    {
        public readonly List<string> Errors = new();
        public void LogInfo(string value) { }
        public void LogError(string value) => Errors.Add(value);
    }
    public static class Config
    {
        public static ConfigEntry<float> NormalAlpha, CgModeAlpha;
        public static ConfigEntry<bool> ModifyText;
        public static ConfigEntry<int> FrameRate;
        public static void Bind()
        {
            NormalAlpha = Plugin.ConfigFile.Bind("Message.Window", "NormalAlpha", 0.7f);
            CgModeAlpha = Plugin.ConfigFile.Bind("Message.Window", "CgModeAlpha", 0f);
            ModifyText = Plugin.ConfigFile.Bind("Message.Text", "Modified", true);
            FrameRate = Plugin.ConfigFile.Bind("General", "FrameRate", 60);
        }
    }
}
namespace UnityEngine
{
    public class MonoBehaviour { }
    public static class Application { public static int targetFrameRate = 30; }
}
namespace Gc
{
    public static class GcOptionData
    {
        public static readonly List<int> Calls = new();
        public static void ChangeApplicationTargetFrameRate(int value)
        {
            Calls.Add(value);
            UnityEngine.Application.targetFrameRate = value;
        }
    }
}
namespace GCMod.Services
{
    public static class AlphaController
    {
        public static float ClampAlpha(float value) => DialogueEditSession.Normalize(value);
    }
}
namespace GCMod.Patches
{
    public static class SettingsUiPatch
    {
        public static int LifetimeChecks;
        public static Action OnCheckLifetime;
        public static void Refresh() { }
        public static void CheckLifetime() { LifetimeChecks++; OnCheckLifetime?.Invoke(); }
    }
    public static class VisualPatch
    {
        public static float Normal, Cg;
        public static void ApplyBackgrounds(float normal, float cg) { Normal = normal; Cg = cg; }
    }
    public static class TextStylePatch
    {
        public static bool Enabled;
        public static void ApplyTracked() => Enabled = Services.DialogueSettings.ReadabilityEnabled;
    }
}
namespace Utility.Notifications
{
    public static class Toast
    {
        public static int Errors, Infos;
        public static void Error(string title, string text) => Errors++;
        public static void Info(string title, string text) => Infos++;
    }
}
