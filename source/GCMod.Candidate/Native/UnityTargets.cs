#nullable enable
using GCMod.Nextgen;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
namespace GCMod.Candidate.Native;
public static class UnityNative {
 public static long Ptr(Il2CppObjectBase? value)=>value?.Pointer.ToInt64()??0;
 public static bool Alive(UnityEngine.Object? value)=>value!=null;
 public static string Class(Il2CppObjectBase? value)=>value==null||value.Pointer==IntPtr.Zero?"":IL2CPP.il2cpp_type_get_name_(IL2CPP.il2cpp_class_get_type(IL2CPP.il2cpp_object_get_class(value.Pointer)))??"";
 public static Transform? ExactChild(Transform root,IEnumerable<string> names){var current=root;foreach(var name in names){Transform? match=null;int count=0;for(int i=0;i<current.childCount;i++){var child=current.GetChild(i);if(child.name==name){match=child;count++;}}if(count!=1)return null;current=match!;}return current;}
 public static string[] Nodes(Transform target,Transform owner){var nodes=new List<string>();for(int i=0;Alive(target)&&i<48;i++,target=target.parent){if(Ptr(target)==Ptr(owner)){nodes.Reverse();return nodes.ToArray();}nodes.Add(target.name);}return [];}
 public static PhysicalTargetIdentity Identity(TextMeshProUGUI text,long lifetime)=>new((ulong)Ptr(text),text.GetInstanceID(),Class(text),lifetime);
}
/// Conditional writes re-read exact native identity/source at the setter boundary.
public sealed class UnityTextTarget(GameThreadGate gate,TextMeshProUGUI text,Func<TextTargetState> readCurrent):IUiTextTarget,IUiTargetAccessGate {
 TextTargetState? last;
 public bool CanAccess=>gate.CanAccess;
 public TextTargetState ReadState(){gate.Require();if(!UnityNative.Alive(text)){if(last==null)throw new InvalidOperationException("TARGET_DESTROYED_BEFORE_FIRST_READ");return last with {Active=false,DestroyedOrRebound=true};}last=readCurrent();return last;}
 public bool TryWriteText(TextTargetState expected,string value){gate.Require();if(ReadState()!=expected||!expected.Active)return false;text.text=value;return true; /* Readback/geometry are separate owned settlement checks; post-read failure cannot orphan a completed setter. */}
}

