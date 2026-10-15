#nullable enable
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;
using Cell=Gc.Novel.ContentScrollCell;
using CellData=Gc.Novel.ContentScrollCellData;

namespace GCMod.Candidate.Native;

/// <summary>Current episode-list titles use active Master text and typed package layout policy; only fontSize is owned.</summary>
internal sealed class Fit3NativeProvider
{
    readonly GameThreadGate gate;readonly NativeHookHub hub;readonly UiFloatOwnershipService ownership;readonly NativeFontProvider fonts;
    readonly Func<TranslationPackageSnapshot> selected;readonly Func<string,bool> activeMasterTitle;readonly SafeDiagnostics log;readonly Func<bool> writesEnabled;
    readonly Dictionary<long,State> states=new();readonly Dictionary<long,string> completed=new();int frame;
    enum Phase { SOURCE_BOUND,WAIT_SOURCE,WAIT_MESH,YIELD,KEEP_35,KEEP_OWN_SIZE,OWN_SIZE_APPLY,RESTORE,WAIT_RESTORE }
    [Flags] enum WaitMask : uint { None=0,Owner=1,Target=2,Membership=4,Records=8,Model=16,Policy=32,Master=64,Font=128,Supplier=256,Fresh=512,Ink=1024,AutoSize=2048,ExternalSize=4096,ContextStyle=8192,Window=16384,WriteDeclined=32768,AlreadyCompleted=65536,ConfiguredOff=131072,ReadFailure=262144,PostWriteRead=524288 }
    sealed class State
    {
        public Cell Owner=null!;public int Instance,Frame;public long Started,Lifetime,Data;public string Model="";
        public TitleFitMeasurement? Original;public ImmutableArray<int> Tested=ImmutableArray<int>.Empty;public bool Done,CarryRestore;
        public PhysicalTargetIdentity? Identity;public FloatTarget? Target;public float Last=35;
        public readonly HashSet<string> Diagnostics=new(StringComparer.Ordinal);public int WaitDiagnostics;
    }
    void Trace(State state,Phase phase,WaitMask mask=WaitMask.None,float? size=null)
    {
        // Pure managed transition facts already read by the executor. No additional native reads or proof claims.
        string scalar=size is >=31 and <=35&&size.Value==(int)size.Value?((int)size.Value).ToString(System.Globalization.CultureInfo.InvariantCulture):size.HasValue?"OTHER":"NONE";
        string key=phase+":"+(uint)mask+":"+scalar;if(state.Diagnostics.Contains(key))return;
        bool wait=phase is Phase.WAIT_SOURCE or Phase.WAIT_MESH or Phase.WAIT_RESTORE;
        if(wait&&state.WaitDiagnostics>=4||state.Diagnostics.Count>=16)return;
        if(wait)state.WaitDiagnostics++;state.Diagnostics.Add(key);
        log.Record($"FIT3_L_{state.Lifetime}_P_EPISODE_TITLE_{phase}_M_{(uint)mask:X}_S_{scalar}");
    }
    static WaitMask EligibilityMask(TitleFitMeasurement value)
    {
        WaitMask mask=WaitMask.None;if(!value.Current)mask|=WaitMask.Owner;if(!value.Supplier)mask|=WaitMask.Supplier;
        if(!value.Fresh)mask|=WaitMask.Fresh;if(!value.VisibleInk)mask|=WaitMask.Ink;if(value.AutoSize)mask|=WaitMask.AutoSize;
        if(value.Text!=value.ModelText)mask|=WaitMask.Model;return mask==WaitMask.None?WaitMask.Policy:mask;
    }
    sealed record Measurement(TextMeshProUGUI Text,TitleFitMeasurement Value,long Data,PhysicalTargetIdentity Identity,FloatTargetState Property);
    sealed class FloatTarget(GameThreadGate gate,TextMeshProUGUI text,Func<FloatTargetState> read):IUiFloatTarget,IUiTargetAccessGate
    {
        public bool CanAccess=>gate.CanAccess;
        public FloatTargetState ReadState(){gate.Require();return read();}
        public bool TryWriteFloat(FloatTargetState expected,float value){gate.Require();if(ReadState()!=expected||!expected.Active)return false;text.fontSize=value;return true;}
    }
    public Fit3NativeProvider(GameThreadGate gate,NativeHookHub hub,UiFloatOwnershipService ownership,NativeFontProvider fonts,
        Func<TranslationPackageSnapshot> selected,Func<string,bool> activeMasterTitle,SafeDiagnostics log,Func<bool>? writesEnabled=null)
    {this.gate=gate;this.hub=hub;this.ownership=ownership;this.fonts=fonts;this.selected=selected;this.activeMasterTitle=activeMasterTitle;this.log=log;this.writesEnabled=writesEnabled??(()=>false);}
    bool Enabled=>SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess,writesEnabled);
    static bool Alive(UnityEngine.Object? x)=>x!=null;static long Ptr(Il2CppObjectBase? x)=>x?.Pointer.ToInt64()??0;
    public void Initialize()
    {
        hub.Subscribe(typeof(Cell),"Setup",new[]{typeof(CellData)},BeforeSetup,true);
        hub.Subscribe(typeof(Cell),"Setup",new[]{typeof(CellData)},SetupPost);
        hub.Subscribe(typeof(Cell),"UpdateView",Type.EmptyTypes,UpdatePost);
    }
    static bool Membership(Cell cell,TextMeshProUGUI title)
    {
        var expected=new[]{"StoryNovelList_ui","Novel","Root","Canvas","View"};
        static List<(long Pointer,string Name)> Ancestors(Transform t){var result=new List<(long,string)>();for(int n=0;Alive(t)&&n<24;n++,t=t.parent)result.Add((Ptr(t.gameObject),t.gameObject.name));return result;}
        var a=Ancestors(cell.transform);var b=Ancestors(title.transform);int i=a.FindIndex(x=>x.Name==expected[0]),j=b.FindIndex(x=>x.Name==expected[0]);
        return i>=0&&j>=0&&a.Skip(i).Select(x=>x.Name).SequenceEqual(expected)&&b.Skip(j).Select(x=>x.Name).SequenceEqual(expected)&&a[i].Pointer==b[j].Pointer&&b.Any(x=>x.Pointer==Ptr(cell.gameObject));
    }
    static object Vec(Vector3 v)=>new{v.x,v.y,v.z};static object Vec4(Vector4 v)=>new{v.x,v.y,v.z,v.w};
    static string ProtectedStyle(TextMeshProUGUI text)
    {
        var rect=text.m_rectTransform;if(!Alive(rect))return "";var r=rect!.rect;var t=text.transform;
        var parents=new List<object>();int depth=0;for(var p=t.parent;Alive(p)&&depth++<24;p=p.parent)parents.Add(new{pointer=Ptr(p),instance=p.GetInstanceID(),scale=Vec(p.localScale),lossy=Vec(p.lossyScale)});
        var style=new{font=Ptr(text.m_fontAsset),fontInstance=text.m_fontAsset?.GetInstanceID(),material=Ptr(text.m_sharedMaterial),materialInstance=text.m_sharedMaterial?.GetInstanceID(),
            auto=text.m_enableAutoSizing,min=text.m_fontSizeMin,max=text.m_fontSizeMax,weight=text.m_fontWeight,style=text.m_fontStyle,alignment=text.m_textAlignment,
            character=text.m_characterSpacing,word=text.m_wordSpacing,line=text.m_lineSpacing,paragraph=text.m_paragraphSpacing,wrap=text.m_TextWrappingMode,overflow=text.m_overflowMode,
            first=text.m_firstVisibleCharacter,maxCharacters=text.m_maxVisibleCharacters,maxLines=text.m_maxVisibleLines,margin=Vec4(text.m_margin),rect=new{r.x,r.y,r.width,r.height},
            scale=Vec(t.localScale),lossy=Vec(t.lossyScale),position=Vec(t.localPosition),rotation=new{t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w},
            anchors=new{minX=rect.anchorMin.x,minY=rect.anchorMin.y,maxX=rect.anchorMax.x,maxY=rect.anchorMax.y,pivotX=rect.pivot.x,pivotY=rect.pivot.y},parents};
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(style)));
    }
    static bool Ink(Mesh? mesh,CanvasRenderer? renderer){if(!Alive(mesh)||!Alive(renderer)||renderer!.cull||renderer.GetAlpha()<=0)return false;var size=mesh!.bounds.size;return float.IsFinite(size.x)&&float.IsFinite(size.y)&&size.x>0&&size.y>0;}
    Measurement? Read(State state)
    {
        gate.Require();var owner=state.Owner;if(!Alive(owner)||owner.GetInstanceID()!=state.Instance||!owner.gameObject.activeInHierarchy||!owner.enabled){Trace(state,Phase.WAIT_SOURCE,WaitMask.Owner);return null;}
        var data=owner._data;var text=owner._title;if(data==null||!Alive(text)||!text!.enabled||!text.gameObject.activeInHierarchy){Trace(state,Phase.WAIT_SOURCE,WaitMask.Target);return null;}
        if(!Membership(owner,text)){Trace(state,Phase.WAIT_SOURCE,WaitMask.Membership);return null;}
        var records=data._NovelInfo_k__BackingField;if(records==null||records.Length is <1 or >64){Trace(state,Phase.WAIT_SOURCE,WaitMask.Records);return null;}var ids=new List<int>();foreach(var record in records){if(record==null){Trace(state,Phase.WAIT_SOURCE,WaitMask.Records);return null;}ids.Add(record._NovelId_k__BackingField);}
        string model=data._Title_k__BackingField??"",value=text.m_text??"";if(model.Length is <1 or >256||value.Length>256){Trace(state,Phase.WAIT_SOURCE,WaitMask.Model);return null;}
        var policy=selected().LayoutPolicies.GetValueOrDefault("episode-title.fit.v1");if(policy==null||policy.FontProfile!=FontService.ApprovedProfile){Trace(state,Phase.WAIT_SOURCE,WaitMask.Policy);return null;}
        var medium=fonts.Medium;var primary=text.m_fontAsset;bool fontReady=Alive(medium)&&Alive(primary)&&primary!.name=="GameFont";bool approved=fontReady&&activeMasterTitle(model);
        if(!approved)Trace(state,Phase.WAIT_SOURCE,fontReady?WaitMask.Master:WaitMask.Font);
        var chars=text.m_textInfo?.characterInfo;var info=text.m_textInfo;int count=info?.characterCount??0;bool bounded=chars!=null&&count>0&&count<=chars.Length&&count<=value.Length+8&&count<=264;
        var consumed=new StringBuilder();bool suppliers=approved,visibleSupplier=approved,sawHangul=false,geometry=true;int visible=0;
        if(bounded)for(int n=0;n<count;n++){var c=chars![n];if(c==null){suppliers=false;geometry=false;continue;}consumed.Append(c.character);
            bool hangul=c.character is >= '\uAC00' and <= '\uD7A3' or >= '\u3130' and <= '\u318F';if(hangul){bool same=Alive(c.fontAsset)&&Alive(medium)&&Ptr(c.fontAsset)==Ptr(medium)&&c.fontAsset.GetInstanceID()==medium!.GetInstanceID();suppliers&=same;if(c.isVisible){sawHangul=true;visibleSupplier&=same;}}
            if(c.isVisible){visible++;geometry&=float.IsFinite(c.topLeft.x)&&float.IsFinite(c.topLeft.y)&&float.IsFinite(c.bottomRight.x)&&float.IsFinite(c.bottomRight.y)&&c.bottomRight.x>c.topLeft.x&&c.topLeft.y>c.bottomRight.y;}}
        string sequence=consumed.ToString();bool full=bounded&&sequence==value;
        string prefix=sequence.EndsWith("\u2026\u0003",StringComparison.Ordinal)?sequence[..^2]:sequence.EndsWith("\u2026",StringComparison.Ordinal)?sequence[..^1]:sequence;
        bool currentSequence=full||bounded&&text.m_isTextTruncated&&text.m_firstOverflowCharacterIndex>=0&&prefix.Length>0&&prefix.Length<=text.m_firstOverflowCharacterIndex&&value.StartsWith(prefix,StringComparison.Ordinal);
        bool ink=Ink(text.m_mesh,text.m_canvasRenderer);var subs=text.m_subTextObjects;if(subs!=null&&subs.Length<=8)foreach(var sub in subs){if(Alive(sub)&&sub.gameObject.activeInHierarchy&&sub.enabled&&Ptr(sub.m_TextComponent)==Ptr(text))ink|=Ink(sub.m_mesh,sub.m_CanvasRenderer);}
        string physical=Ptr(owner)+":"+owner.GetInstanceID()+":"+Ptr(text)+":"+text.GetInstanceID();string binding=Ptr(data)+":"+string.Join(",",ids)+":"+model;
        var style=ProtectedStyle(text);var m=new TitleFitMeasurement(physical,binding,style,value,model,text.m_fontSize,true,visibleSupplier&&sawHangul,
            currentSequence&&!text.m_havePropertiesChanged&&!text.m_isLayoutDirty,ink&&visible>0&&geometry,full&&info!.lineCount==1&&suppliers,text.m_isTextTruncated,text.m_firstOverflowCharacterIndex,text.m_enableAutoSizing);
        var identity=new PhysicalTargetIdentity((ulong)Ptr(text),text.GetInstanceID(),UnityNative.Class(text),state.Lifetime);
        return new(text,m,Ptr(data),identity,new(identity,"fontSize",m.Size,state.Instance,state.Lifetime,style,true));
    }
    FloatTarget Target(State state,Measurement measurement)
    {
        return new(gate,measurement.Text,()=>{
            gate.Require();
            if(!Alive(state.Owner)||state.Owner.GetInstanceID()!=state.Instance||!Alive(measurement.Text)||measurement.Text.GetInstanceID()!=measurement.Identity.InstanceId)
                return measurement.Property with{Active=false,DestroyedOrRebound=true};
            if(!state.Owner.gameObject.activeInHierarchy||!state.Owner.enabled)return measurement.Property with{Active=false};
            var now=Read(state);if(now==null)return measurement.Property with{Active=false};
            return now.Data==measurement.Data&&now.Value.BindingContext==measurement.Value.BindingContext&&now.Identity==measurement.Identity
                ?now.Property:measurement.Property with{Active=false,DestroyedOrRebound=true};});
    }
    void BeforeSetup(NativeHookContext context)
    {
        if(!gate.CanAccess||context.Instance is not Cell cell||!states.TryGetValue(Ptr(cell),out var state)||state.Identity==null||state.Last==35)return;
        if(!Enabled)
        {
            var outcome=RestoreState(state);
            if(outcome is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){state.CarryRestore=true;state.Done=true;return;}
            state.Last=35;states.Remove(Ptr(cell));return;
        }
        if(context.Args.FirstOrDefault() is CellData same&&Ptr(same)==state.Data)return;
        try
        {
            var measurement=Read(state);if(measurement==null){state.CarryRestore=true;state.Done=true;return;}
            var result=ownership.Restore(state.Identity,"fontSize");if(result is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){state.CarryRestore=true;state.Done=true;return;}
            state.Last=35;states.Remove(Ptr(cell));
        }
        catch{state.CarryRestore=true;state.Done=true;log.Record("FIT3_REBIND_RESTORE_DEFERRED");}
    }
    void SetupPost(NativeHookContext context){if(context.Instance is Cell cell&&context.Args.FirstOrDefault() is CellData data)Natural(cell,data);}
    void UpdatePost(NativeHookContext context){if(context.Instance is Cell cell)Natural(cell,null);}
    void Natural(Cell owner,CellData? incoming)
    {
        // A natural Setup/UpdateView can precede activation. Retain only the live owner and
        // its witnessed data/model primitives; Read still gates active target/renderer access.
        if(!Enabled||!Alive(owner))return;var data=owner._data;if(data==null||incoming!=null&&Ptr(incoming)!=Ptr(data))return;
        string model=data._Title_k__BackingField??"";long key=Ptr(owner);if(model.Length is <1 or >256)return;
        if(states.TryGetValue(key,out var old)&&old.Instance==owner.GetInstanceID()&&old.Data==Ptr(data)&&old.Model==model)return;
        if(states.Count>=12&&!states.ContainsKey(key))return;
        var state=new State{Owner=owner,Instance=owner.GetInstanceID(),Data=Ptr(data),Model=model,Frame=frame,Started=Stopwatch.GetTimestamp(),Lifetime=gate.NextLifetime()};
        // A witnessed Setup may carry a size lease through an inactive old binding. The next active measurement conditionally restores that exact owned value.
        if(old?.CarryRestore==true&&old.Identity!=null){state.CarryRestore=true;state.Lifetime=old.Lifetime;state.Identity=old.Identity;state.Target=old.Target;state.Last=old.Last;state.Original=old.Original;}
        states[key]=state;
        Trace(state,Phase.SOURCE_BOUND);
    }
    void RememberCompleted(long key,string binding){if(completed.Count<4096||completed.ContainsKey(key))completed[key]=binding;}
    RestoreOutcome RestoreState(State state)
    {
        if(state.Identity==null||state.Last==35)return RestoreOutcome.NotOwned;
        if(!state.CarryRestore)return ownership.Restore(state.Identity,"fontSize");
        try
        {
            var measurement=Read(state);if(measurement==null)return RestoreOutcome.RetryableReadFailure;
            return ownership.RestoreAtRebind(state.Identity,"fontSize",Target(state,measurement),measurement.Property);
        }
        catch{return RestoreOutcome.RetryableReadFailure;}
    }
    public bool ReconcileDisplay()
    {
        if(!gate.CanAccess)return false;
        try
        {
            bool enabled=Enabled,complete=true;
            foreach(var pair in states.ToArray())
            {
                var state=pair.Value;if(enabled&&activeMasterTitle(state.Model))continue;
                Trace(state,Phase.WAIT_SOURCE,enabled?WaitMask.Master:WaitMask.ConfiguredOff);
                var outcome=RestoreState(state);
                if(outcome is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){Trace(state,Phase.WAIT_RESTORE,WaitMask.ReadFailure);state.Done=true;complete=false;continue;}
                if(outcome==RestoreOutcome.Restored)Trace(state,Phase.RESTORE,size:35);else Trace(state,Phase.YIELD,enabled?WaitMask.Master:WaitMask.ConfiguredOff);
                state.Last=35;states.Remove(pair.Key);completed.Remove(pair.Key);
            }
            if(!enabled){complete&=ownership.RestoreAll();if(complete)Selected();}
            return complete;
        }
        catch{log.Record("FIT3_DISPLAY_RECONCILE_RETRYABLE");return false;}
    }
    public void Tick()
    {
        if(!gate.CanAccess||!ReconcileDisplay()||!Enabled)return;frame++;var policy=selected().LayoutPolicies.GetValueOrDefault("episode-title.fit.v1");if(policy==null)return;
        foreach(var pair in states.ToArray())
        {
            var state=pair.Value;if(state.Done&&!state.CarryRestore)continue;
            if(!Alive(state.Owner)||state.Owner.GetInstanceID()!=state.Instance){states.Remove(pair.Key);continue;}
            double age=(Stopwatch.GetTimestamp()-state.Started)/(double)Stopwatch.Frequency;if(frame-state.Frame>60||age>2){
                if(state.Identity!=null&&state.Last!=35){var expired=ownership.Restore(state.Identity,"fontSize");if(expired is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){Trace(state,Phase.WAIT_RESTORE,WaitMask.Window);continue;}if(expired==RestoreOutcome.Restored)Trace(state,Phase.RESTORE,WaitMask.Window,35);state.Last=35;}
                Trace(state,Phase.YIELD,WaitMask.Window);
                state.Done=true;continue;}
            try
            {
                var measurement=Read(state);if(measurement==null)continue;if(measurement.Data!=state.Data||measurement.Value.ModelText!=state.Model){Trace(state,Phase.WAIT_SOURCE,WaitMask.Model);continue;}
                if(state.CarryRestore)
                {
                    if(!measurement.Value.Fresh||state.Original==null||state.Identity==null){Trace(state,Phase.WAIT_RESTORE,WaitMask.Fresh);continue;}
                    var target=Target(state,measurement);
                    var restored=ownership.RestoreAtRebind(state.Identity,"fontSize",target,measurement.Property);
                    if(restored is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){Trace(state,Phase.WAIT_RESTORE,WaitMask.ReadFailure);continue;}
                    if(restored==RestoreOutcome.Restored)Trace(state,Phase.RESTORE,size:35);
                    state.CarryRestore=false;state.Original=null;state.Identity=null;state.Last=35;
                    if(restored is RestoreOutcome.ExternalChanged or RestoreOutcome.DestroyedOrRebound){Trace(state,Phase.YIELD,WaitMask.ExternalSize);state.Done=true;continue;}
                    measurement=Read(state);if(measurement==null||measurement.Value.Size!=35){Trace(state,Phase.YIELD,WaitMask.PostWriteRead);state.Done=true;continue;}
                }
                var value=measurement.Value;
                if(!EpisodeTitleFitPolicy.Eligible(policy,value)){Trace(state,Phase.WAIT_SOURCE,EligibilityMask(value),value.Size);continue;}
                if(!value.Fresh||!value.VisibleInk){Trace(state,Phase.WAIT_MESH,(!value.Fresh?WaitMask.Fresh:WaitMask.None)|(!value.VisibleInk?WaitMask.Ink:WaitMask.None),value.Size);continue;}
                if(state.Original==null){if(value.Size!=policy.BaseSize||completed.TryGetValue(pair.Key,out var done)&&done==value.BindingContext){Trace(state,Phase.YIELD,value.Size!=policy.BaseSize?WaitMask.ExternalSize:WaitMask.AlreadyCompleted,value.Size);state.Done=true;continue;}
                    state.Original=value;state.Tested=ImmutableArray.Create(policy.BaseSize);state.Identity=measurement.Identity;state.Target=Target(state,measurement);}
                var decision=EpisodeTitleFitPolicy.Decide(policy,state.Original,value,state.Tested);
                if(decision.Action=="WAIT"){Trace(state,Phase.WAIT_MESH,EligibilityMask(value),value.Size);continue;}
                if(decision.Action=="YIELD"){Trace(state,Phase.YIELD,WaitMask.ContextStyle,value.Size);state.Done=true;continue;}
                if(decision.Action=="KEEP"){Trace(state,value.Size==35?Phase.KEEP_35:Phase.KEEP_OWN_SIZE,size:value.Size);state.Done=true;RememberCompleted(pair.Key,value.BindingContext);continue;}
                if(decision.Action=="RESTORE"){var outcome=ownership.Restore(state.Identity!,"fontSize");if(outcome is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure){Trace(state,Phase.WAIT_RESTORE,WaitMask.ReadFailure);continue;}if(outcome==RestoreOutcome.Restored)Trace(state,Phase.RESTORE,size:35);else Trace(state,Phase.YIELD,WaitMask.ExternalSize);state.Last=35;state.Done=true;RememberCompleted(pair.Key,value.BindingContext);continue;}
                var written=ownership.TryWrite(state.Target!,"Fit3:episode-title",measurement.Property,decision.Size);if(!written.Written){Trace(state,Phase.YIELD,WaitMask.WriteDeclined,value.Size);state.Done=true;continue;}
                Trace(state,Phase.OWN_SIZE_APPLY,size:decision.Size);
                state.Last=decision.Size;state.Tested=state.Tested.Add((int)decision.Size);
                var current=Read(state);if(current==null||current.Value.Size!=decision.Size||current.Value.ProtectedStyle!=value.ProtectedStyle){Trace(state,Phase.YIELD,WaitMask.PostWriteRead,decision.Size);state.Done=true;continue;}
                measurement.Text.ForceMeshUpdate(false,false);log.Record("FIT3_CURRENT_OWNED_FONT_SIZE");
            }
            catch{Trace(state,Phase.YIELD,WaitMask.ReadFailure);log.Record("FIT3_CALLBACK_ISOLATED");state.Done=true;}
        }
        foreach(var pair in states.Where(x=>x.Value.Done&&!x.Value.CarryRestore&&x.Value.Last==35).ToArray()){
            var state=pair.Value;if(state.Original!=null)RememberCompleted(pair.Key,state.Original.BindingContext);states.Remove(pair.Key);}
    }
    public bool RestoreOwned()=>ownership.RestoreAll();
    public void Selected(){states.Clear();completed.Clear();}
    public bool Dispose(){if(!RestoreOwned())return false;Selected();return true;}
}
