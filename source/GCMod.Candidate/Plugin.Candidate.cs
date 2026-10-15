using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GCMod.Candidate;
using GCMod.Services;
using UnityEngine;
namespace GCMod;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID,MyPluginInfo.PLUGIN_NAME,MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin:BasePlugin {
 public static ConfigFile ConfigFile; public static new ManualLogSource Log; public static MonoBehaviour Instance; public static TranslationManager Trans;
 NativeRuntimeBridge bridge;MonoBehaviour driver;
 public override void Load(){Log=base.Log;ConfigFile=base.Config;GCMod.Logger.Bind(s=>Log.LogInfo(s),s=>Log.LogWarning(s),s=>Log.LogError(s));GCMod.Config.Initialize();bridge=new(ConfigFile,Log);driver=Instance=AddComponent<NextgenDriver>();NextgenDriver.Bridge=bridge;}
 public override bool Unload(){if(bridge!=null&&!bridge.TryUnload(()=>{if(driver!=null)UnityEngine.Object.Destroy(driver);driver=null;Instance=null;}))return false;NextgenDriver.Bridge=null;return true;}
}
public sealed class NextgenDriver:MonoBehaviour {
 internal static NativeRuntimeBridge Bridge;
 public NextgenDriver(IntPtr pointer):base(pointer){}
 public void Update()=>Bridge?.Tick();
}
