#nullable enable
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
using System.Diagnostics;
using HarmonyLib;
using BepInEx;
using GCMod.Nextgen;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
namespace GCMod.Candidate.Native;
public sealed class NativeHookContext {
 public object? Instance{get;init;} public object[] Args{get;init;}=[]; public MethodBase Method{get;init;}=null!;
 public long Lifetime{get;init;} public bool IsPrefix{get;init;} public bool Active{get;internal set;}=true; public Exception? Exception{get;init;}
}
/// One patched body per callback, with subscriptions isolated and bounded; never a global setter.
public sealed class NativeHookHub:IDisposable {
 sealed record Subscription(Action<NativeHookContext> Callback,bool Prefix,bool Finalizer);
 static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodBase,NativeHookHub> dispatch=new();
 readonly Dictionary<MethodBase,List<Subscription>> subscribers=new();
 readonly Dictionary<MethodBase,NativeMethodObservation> observations=new();
 readonly HashSet<MethodBase> observedCallbacks=new();
 readonly HashSet<string> requestedSignatures=new(StringComparer.Ordinal);
 readonly Harmony harmony=new("local.gc.nextgen.shared-hooks."+Guid.NewGuid().ToString("N")); readonly GameThreadGate gate; readonly SafeDiagnostics log;
 NativeMethodMap? aliases; bool disposed,uninstalled;
 public NativeHookHub(GameThreadGate gate,SafeDiagnostics log){this.gate=gate;this.log=log;}
 public IReadOnlyDictionary<MethodBase,NativeMethodObservation> Observations=>observations;
 public bool Subscribe(Type owner,string method,Type[] args,Action<NativeHookContext> callback,bool prefix=false,bool finalizer=false){
  gate.Require();if(disposed)return false;
  string[] names=args.Select(x=>x.FullName??x.Name).ToArray();string identity=HookDiagnosticCodes.Identity(owner.FullName??owner.Name,method,names);
  if(requestedSignatures.Add(identity))foreach(string code in HookDiagnosticCodes.Signature(owner.FullName??owner.Name,method,names))log.Record(code);
  void Outcome(string status,string reason)=>log.Record(HookDiagnosticCodes.Outcome(owner.FullName??owner.Name,method,names,status,reason));
  try{var m=Find(owner,method,args);if(m==null){Outcome("UNAVAILABLE","MANAGED_EXACT_SIGNATURE_MISSING");return false;}
   if(!subscribers.TryGetValue(m,out var list)){var o=Inspect(m);var assessment=new CompatibilityService().Assess(o.Signature,o);if(!assessment.Available){Diagnostic("UNAVAILABLE",owner,method,assessment.Reason);Outcome("UNAVAILABLE",assessment.Reason);return false;}
    observations[m]=o;list=new();subscribers.Add(m,list);
    if(!dispatch.TryAdd(m,this)){subscribers.Remove(m);observations.Remove(m);log.Record("NATIVE_CALLBACK_ALREADY_OWNED_BY_ANOTHER_HUB");return false;}
    try{harmony.Patch(m,prefix:new HarmonyMethod(typeof(NativeHookHub).GetMethod(nameof(Before),BindingFlags.Static|BindingFlags.NonPublic)!),postfix:new HarmonyMethod(typeof(NativeHookHub).GetMethod(nameof(After),BindingFlags.Static|BindingFlags.NonPublic)!),finalizer:new HarmonyMethod(typeof(NativeHookHub).GetMethod(nameof(FinalizeCallback),BindingFlags.Static|BindingFlags.NonPublic)!));}
    catch{try{harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);}catch{log.Record("NATIVE_CALLBACK_FAILED_INSTALL_UNPATCH_DEFERRED");}subscribers.Remove(m);RemoveOwnedDispatch(m);observations.Remove(m);throw;}
   }
   list.Add(new(callback,prefix,finalizer));Diagnostic("INSTALLED",owner,method,"OK");Outcome("INSTALLED","VALIDATED_SIGNATURE_AND_ABI");return true;
  }catch(Exception ex){var reason=ex is InvalidDataException?ex.Message:ex.GetType().Name;Diagnostic("ISOLATED",owner,method,reason);Outcome("ISOLATED",reason);return false;}
 }
 void Diagnostic(string status,Type owner,string method,string reason){
  static string Symbol(string s,int limit)=>new(s.ToUpperInvariant().Select(c=>c is >= 'A' and <= 'Z' or >= '0' and <= '9'?c:'_').Take(limit).ToArray());
  log.Record("NATIVE_"+status+"_"+Symbol(owner.Name,20)+"_"+Symbol(method,20)+"_"+Symbol(reason,36));
 }
 public bool Subscribe(Type owner,string method,Type[] args,MethodInfo? prefix,MethodInfo? postfix,MethodInfo? finalizer){
  // Typed donor callbacks remain typed. They share the same validated physical hook boundary.
  bool ok=true;if(prefix!=null)ok&=Subscribe(owner,method,args,c=>InvokeTyped(prefix,c),true);
  if(postfix!=null)ok&=Subscribe(owner,method,args,c=>InvokeTyped(postfix,c));
  if(finalizer!=null)ok&=Subscribe(owner,method,args,c=>InvokeTyped(finalizer,c),false,true);return ok;
 }
 static void InvokeTyped(MethodInfo callback,NativeHookContext c){var ps=callback.GetParameters();var values=new object?[ps.Length];for(int i=0;i<ps.Length;i++){var p=ps[i];if(p.Name=="__exception")values[i]=c.Exception;else if(p.Name=="__instance")values[i]=c.Instance;else if(p.Name=="__args")values[i]=c.Args;else if(p.Name=="__originalMethod")values[i]=c.Method;else if(p.Name!=null&&p.Name.StartsWith("__")&&int.TryParse(p.Name[2..],out var index)&&index<c.Args.Length)values[i]=c.Args[index];else throw new InvalidOperationException("UNSUPPORTED_TYPED_CALLBACK_PARAMETER");}callback.Invoke(null,values);}
 static MethodInfo? Find(Type t,string name,Type[] args)=>t.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly,null,args,null);
 static string Normalize(string? s)=>(s??"").Replace("Il2CppSystem.","System.").Replace("Il2Cpp.","");
 static string TypeName(Type type){if(type.IsArray)return TypeName(type.GetElementType()!)+"[]";if(type.IsGenericType){if(type.GetGenericTypeDefinition().FullName=="Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1"||type.GetGenericTypeDefinition().FullName=="Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1")return TypeName(type.GetGenericArguments()[0])+"[]";var name=Normalize(type.GetGenericTypeDefinition().FullName);var at=name.IndexOf('`');if(at>=0)name=name[..at];return name+"<"+string.Join(",",type.GetGenericArguments().Select(TypeName))+">";}return Normalize(type.FullName);}
 static IntPtr ClassPointer(Type type){RuntimeHelpers.RunClassConstructor(type.TypeHandle);var store=typeof(Il2CppClassPointerStore<>).MakeGenericType(type);RuntimeHelpers.RunClassConstructor(store.TypeHandle);return (IntPtr)(store.GetField("NativeClassPtr",BindingFlags.Public|BindingFlags.Static)?.GetValue(null)??IntPtr.Zero);}
 static bool TypeMatches(IntPtr native,Type managed){if(typeof(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase).IsAssignableFrom(managed)){var expected=ClassPointer(managed);return expected!=IntPtr.Zero&&IL2CPP.il2cpp_class_from_il2cpp_type(native)==expected;}return Normalize(IL2CPP.il2cpp_type_get_name_(native))==TypeName(managed);}

 unsafe NativeMethodObservation Inspect(MethodInfo m){
  var type=m.DeclaringType!;RuntimeHelpers.RunClassConstructor(type.TypeHandle);
  if(m.IsGenericMethod||m.GetParameters().Any(p=>p.ParameterType.IsByRef))throw new InvalidDataException("UNSUPPORTED_NATIVE_ABI");
  // Generated wrappers load their MethodInfo pointer via ldsfld; resolve its metadata token, never guessed offsets.
  var fields=ManagedIlReader.StaticHandleFields(m,"NativeMethodInfoPtr_"+m.Name+"_");
  if(fields.Length!=1)throw new InvalidDataException("UNIQUE_NATIVE_METHOD_HANDLE");
  var n=(IntPtr)fields[0].GetValue(null)!;if(n==IntPtr.Zero||IL2CPP.il2cpp_method_get_param_count(n)!=m.GetParameters().Length)throw new InvalidDataException("NATIVE_PARAMETER_COUNT");
  string? owner=IL2CPP.il2cpp_type_get_name_(IL2CPP.il2cpp_class_get_type(IL2CPP.il2cpp_method_get_class(n)));
  string? ret=IL2CPP.il2cpp_type_get_name_(IL2CPP.il2cpp_method_get_return_type(n));
  if(string.IsNullOrEmpty(owner)||string.IsNullOrEmpty(ret))throw new InvalidDataException("NATIVE_TYPE_NAME_UNAVAILABLE");
  if(IL2CPP.il2cpp_method_get_class(n)!=ClassPointer(type)||!TypeMatches(IL2CPP.il2cpp_method_get_return_type(n),m.ReturnType))throw new InvalidDataException("NATIVE_SIGNATURE_MISMATCH");
  uint impl=0, flags=IL2CPP.il2cpp_method_get_flags(n,ref impl);if(((flags&0x10)!=0)!=m.IsStatic)throw new InvalidDataException("NATIVE_STATIC_SIGNATURE_MISMATCH");
  var ps=m.GetParameters();for(uint i=0;i<ps.Length;i++){string? native=IL2CPP.il2cpp_type_get_name_(IL2CPP.il2cpp_method_get_param(n,i));if(string.IsNullOrEmpty(native)||!TypeMatches(IL2CPP.il2cpp_method_get_param(n,i),ps[i].ParameterType))throw new InvalidDataException("NATIVE_PARAMETER_TYPE_MISMATCH");}
  var wrapper=UnityVersionHandler.Wrap((Il2CppMethodInfo*)n.ToPointer());if(wrapper.GetType().FullName!="Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo.NativeMethodInfoStructHandler_29_2+NativeStructWrapper")throw new InvalidDataException("UNSUPPORTED_INTEROP_LAYOUT");var ptr=wrapper.MethodPointer;
  using var proc=Process.GetCurrentProcess();var module=proc.Modules.Cast<ProcessModule>().Single(x=>x.ModuleName=="GameAssembly.dll");long rva=ptr.ToInt64()-module.BaseAddress.ToInt64();
  if(rva<0||rva>=module.ModuleMemorySize||!Executable(ptr))throw new InvalidDataException("NON_EXECUTABLE_NATIVE_TARGET");
  LoadAliases();int count=aliases!.Count(rva);
  // Pass91 writes NewMethod tokens from the generated managed assembly. Match that wrapper,
  // independently of the IL2CPP MethodInfo signature/ABI checks above; never assume native tokens equal.
  if(count==1&&!aliases.MatchesUniqueWrapper(rva,m.Module.Assembly.FullName??"",m.MetadataToken))
   throw new InvalidDataException("NATIVE_METHOD_MAP_WRAPPER_MISMATCH");
  var signature=new NativeMethodContract(type.FullName!,m.Name,m.IsStatic,m.ReturnType.FullName!,ps.Select(p=>new NativeParameter(p.ParameterType.FullName!)).ToImmutableArray(),"IL2CPP_GENERATED_WRAPPER_V1");
  return new(signature,1,true,true,count,count>1,type.FullName!,signature.Abi);
 }
 void LoadAliases(){if(aliases!=null)return;var path=Path.Combine(Paths.BepInExRootPath,"interop","MethodAddressToToken.db");using var stream=File.OpenRead(path);aliases=NativeMethodMap.Read(stream);}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct MemoryBasicInformation{public IntPtr BaseAddress,AllocationBase;public uint AllocationProtect;public ushort PartitionId;public UIntPtr RegionSize;public uint State,Protect,Type;}
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern UIntPtr VirtualQuery(IntPtr address,out MemoryBasicInformation info,UIntPtr size);
 static bool Executable(IntPtr address)=>VirtualQuery(address,out var m,(UIntPtr)System.Runtime.InteropServices.Marshal.SizeOf<MemoryBasicInformation>())!=UIntPtr.Zero&&m.State==0x1000&&(m.Protect&0xff) is 0x10 or 0x20 or 0x40 or 0x80;
 static void Before(MethodBase __originalMethod,object? __instance,object[] __args){if(dispatch.TryGetValue(__originalMethod,out var hub))hub.Dispatch(__originalMethod,__instance,__args,true);}
 static void After(MethodBase __originalMethod,object? __instance,object[] __args){if(dispatch.TryGetValue(__originalMethod,out var hub))hub.Dispatch(__originalMethod,__instance,__args,false);}
 static Exception? FinalizeCallback(MethodBase __originalMethod,object? __instance,object[] __args,Exception? __exception){if(dispatch.TryGetValue(__originalMethod,out var hub))hub.Dispatch(__originalMethod,__instance,__args,false,true,__exception);return __exception;}
 void Dispatch(MethodBase method,object? instance,object[] args,bool prefix,bool finalizer=false,Exception? exception=null){if(disposed||!gate.CanAccess)return;if(observedCallbacks.Add(method))Diagnostic("OBSERVED",method.DeclaringType!,method.Name,"MAIN_THREAD");var context=new NativeHookContext{Instance=instance,Args=args,Method=method,Lifetime=gate.NextLifetime(),IsPrefix=prefix,Exception=exception};try{foreach(var s in subscribers[method].Where(s=>s.Prefix==prefix&&s.Finalizer==finalizer).ToArray())try{s.Callback(context);}catch{log.Record("NATIVE_CALLBACK_FAILURE_ISOLATED");}}finally{context.Active=false;}}
 void RemoveOwnedDispatch(MethodBase method)=>((ICollection<KeyValuePair<MethodBase,NativeHookHub>>)dispatch).Remove(new(method,this));
 public bool TryUninstall(){gate.Require();if(uninstalled)return true;
  try{harmony.UnpatchSelf();}catch{log.Record("NATIVE_HOOK_UNINSTALL_RETRYABLE");return false;}
  // Keep dispatch and subscriptions live through a failed unpatch. Service/gate disposal follows this acknowledgement.
  foreach(var m in subscribers.Keys)RemoveOwnedDispatch(m);subscribers.Clear();uninstalled=true;disposed=true;return true;
 }
 public void Dispose(){if(!TryUninstall())log.Record("NATIVE_HOOK_DISPOSE_DEFERRED");}
}
