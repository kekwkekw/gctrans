using GCMod.Services;
using UnityEngine;

namespace GCMod;

// Main-thread settings maintenance and the plugin's coroutine host.
// This component deliberately has no keyboard/input handling.
public sealed class DialogueSettingsUpdater : MonoBehaviour
{
    private void Update() => DialogueSettings.Tick();
}
