#nullable enable
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
using GCMod.Nextgen;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime;
using DMM.OLG.Unity.Extensions.Novel;

namespace GCMod.Candidate.Native;

/// <summary>Owned Choice visuals and two distinct dictionary-heading routes. Original Letter/Ruby and input controls remain untouched.</summary>
internal sealed partial class StoryNativeProvider
{
    static StoryNativeProvider? current;
    readonly GameThreadGate gate; readonly NativeHookHub hub; readonly AdapterRegistry registry;
    readonly UiOwnershipService ownership; readonly NativeFontProvider fonts; readonly Func<TranslationPackageSnapshot> selected; readonly SafeDiagnostics log;readonly IMasterTranslationView activeMaster;readonly Func<bool> writesEnabled;
    readonly Dictionary<long,AnnotationClickProof> clicks=new(); readonly HashSet<long> synchronous=new();
    readonly Dictionary<long,GcEventClickEvent> clickReceivers=new();
    readonly Dictionary<long,AnnotationRecordProof> records=new(); readonly HashSet<long> claimed=new();
    readonly Dictionary<long,HeadingPending> headings=new();
    ChoiceTransaction? display,retiring; readonly ChoiceCleanupBarrier cleanupBarrier=new(); readonly HashSet<string> choiceKeys=new();
    EventSelect? waiting;ChoicePlan? waitingPlan; int waitingInstance,waitingFrame,frame; long waitingStarted; bool choiceAvailable;
    public ChoiceProofMetrics ProofMetrics { get; } = new();
    sealed class ChoiceTransaction
    {
        public EventSelect Owner=null!;public int Instance;public ChoicePlan Plan=null!;public string Baseline="";public RaycastProof Raycast=null!;
        public Func<bool> StableGuard=()=>false;
        public GameObject[] Visuals=[];public CanvasGroup[] Masks=[],Visibility=[];public TextMeshProUGUI[] Texts=[];public Graphic[][] Graphics=[];
        public bool Committed,CleanupRequested,CleanupFailureLogged;public ChoiceDisplayPhase Phase=ChoiceDisplayPhase.ResolveSource;public ChoiceControlRole ChangedRole;public int Ordinal=-1;public long Generation;
    }
    public StoryNativeProvider(GameThreadGate gate,NativeHookHub hub,AdapterRegistry registry,UiOwnershipService ownership,NativeFontProvider fonts,Func<TranslationPackageSnapshot> selected,SafeDiagnostics log,IMasterTranslationView? activeMaster=null,Func<bool>? writesEnabled=null)
    {this.gate=gate;this.hub=hub;this.registry=registry;this.ownership=ownership;this.fonts=fonts;this.selected=selected;this.log=log;this.activeMaster=activeMaster??MasterTranslationView.Empty;this.writesEnabled=writesEnabled??(()=>false);}
    static bool Alive(UnityEngine.Object? o)=>o!=null;static long Ptr(Il2CppObjectBase? o)=>o?.Pointer.ToInt64()??0;
    public bool IsChoiceBusy=>display is not null||retiring is not null||waiting is not null;
    bool Enabled=>SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess,writesEnabled);
    void CancelWaiting(){waiting=null;waitingPlan=null;}
    public void Initialize()
    {
        current=this;
        bool add=hub.Subscribe(typeof(EventSelect),"AddSelectMessage",new[]{typeof(string),typeof(UnityEngine.Events.UnityAction)},ChoiceSourcePostAdd);
        bool open=hub.Subscribe(typeof(EventSelect),"Open",Type.EmptyTypes,ChoiceOpen);
        bool close=hub.Subscribe(typeof(EventSelect),"Release",Type.EmptyTypes,ChoiceRelease,true);
        // UI14's supported lifecycle is the completed AddSelectMessage group, the natural active root
        // boundary in Tick, and Release. Open is a safe optional notification, never a mandatory hook.
        // A shared/aliased Open remains disarmed in the hub; it cannot disable verified Add/Release work.
        choiceAvailable=StoryChoicePolicy.RequiredLifecycleAvailable(add,close);
        if(choiceAvailable&&!open)log.Record("STORY_CHOICE_ADD_RELEASE_ACTIVE_ROOT_OPEN_UNAVAILABLE");
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        bool click=hub.Subscribe(typeof(GcEventClickEvent),"OnWordClickEvent",new[]{typeof(string)},typeof(StoryNativeProvider).GetMethod(nameof(ClickBefore),flags),typeof(StoryNativeProvider).GetMethod(nameof(ClickAfter),flags),typeof(StoryNativeProvider).GetMethod(nameof(ClickFinally),flags));
        _ = hub.Subscribe(typeof(GcEventClickEvent),"OpenKeywordPopup",new[]{typeof(Gc.Master.NovelKeyWordMaster)},RecordDelivered,true);
        bool popup=hub.Subscribe(typeof(Gc.NovelKeyWordPopup.__c__DisplayClass2_0),"_Create_b__0",new[]{typeof(Gc.NovelKeyWordPopup)},AnnotationDeliveryPost);
        bool dictionary=hub.Subscribe(typeof(Gc.Database.DatabaseTopController),"UpdateKeyWordDetail",new[]{typeof(int)},DictionaryPost);
        registry.Register(new("story.choice.visual.v1","Story",SelectorContract.Host(new[]{"story-choice-list"},new[]{"option-label"},literalPaths:new[]{Array.Empty<string>()}),choiceAvailable,false,choiceAvailable?"OWNED_NATIVE_OVERLAY_ADD_RELEASE_ACTIVE_ROOT":"REQUIRED_CHOICE_ADD_OR_RELEASE_UNAVAILABLE"));
        registry.Register(new("story.annotation-heading.v1","Story",SelectorContract.Host(new[]{"annotation-popup"},new[]{"dictionary-heading"},new[]{"caption"},literalPaths:new[]{Array.Empty<string>()},lifetimes:new[]{"owner"}),popup,false,popup?"VALIDATED_TYPED_FACTORY_OWNED_HEADING":"OPTIONAL_ANNOTATION_FACTORY_UNAVAILABLE"));
        registry.Register(new("story.dictionary-heading.v1","Story",SelectorContract.Host(new[]{"standalone-dictionary"},new[]{"dictionary-heading"},new[]{"caption"},literalPaths:new[]{Array.Empty<string>()},lifetimes:new[]{"owner"}),dictionary,false,dictionary?"CURRENT_SELECTED_DICTIONARY_RECORD":"OPTIONAL_DICTIONARY_CALLBACK_UNAVAILABLE"));
    }
    void ChoiceSourcePostAdd(NativeHookContext context)
    {
        if(!Enabled||context.Instance is not EventSelect owner||!Alive(owner))return;
        if(display!=null&&Ptr(display.Owner)==Ptr(owner))CleanupDisplay();
        if(Ptr(waiting)==Ptr(owner))CancelWaiting();
        var sources=CaptureSources(owner);var plan=sources==null?null:StoryChoicePolicy.Resolve(selected(),sources,activeMaster);if(plan==null)return;
        waiting=owner;waitingPlan=plan;waitingInstance=owner.GetInstanceID();waitingFrame=frame;waitingStarted=Stopwatch.GetTimestamp();
        log.Record($"CH_SOURCE_GROUP_G_{ChoiceDisplayDiagnostics.Key(plan.GroupId)}_OPTIONS_{plan.Options.Length}");
    }
    void ChoiceOpen(NativeHookContext context){if(context.Instance is EventSelect owner)ChoiceSourcePostAdd(new NativeHookContext{Instance=owner});}
    void ChoiceRelease(NativeHookContext context){if(!gate.CanAccess)return;if(context.Instance is EventSelect owner){if(display!=null&&Ptr(display.Owner)==Ptr(owner))CleanupDisplay();if(Ptr(waiting)==Ptr(owner))CancelWaiting();}}
    ChoiceSource[]? CaptureSources(EventSelect owner)
    {
        ProofMetrics.SourceDiscoveryScans++;
        if(!gate.CanAccess||!Alive(owner)||!Alive(owner._root))return null;var cells=owner._selectList;if(cells==null||cells.Count is <1 or >32)return null;
        var result=new List<ChoiceSource>();
        for(int n=0;n<cells.Count;n++)
        {
            var cell=cells[n];if(!Alive(cell)||!Alive(cell._button)||!Alive(cell._textRoot)||!Alive(cell._select)||UnityNative.Nodes(cell.transform,owner.transform).Length==0)return null;
            var first=cell._onClick?.Target?.TryCast<EventSelect.__c__DisplayClass10_0>();var second=first?.clickAction?.Target?.TryCast<ScriptObject.__c__DisplayClass51_0>();var script=second?.__4__this;
            if(first==null||second==null||script==null||!script.IsEnabled||string.IsNullOrEmpty(script.id)||string.IsNullOrEmpty(second.label)||first.clickAction==null)return null;
            string source=cell._text??"";var rows=script.TextList;if(rows==null||rows.Count is <1 or >512)return null;int matches=0;
            for(int i=0;i<rows.Count;i++){var row=rows[i];var fields=row?.splitText;
                if(row!=null&&fields!=null&&fields.Length==3&&fields[0]=="selectif"&&fields[1]==source&&fields[2]==second.label
                    &&row.PlaneText=="selectif,"+source+","+second.label+"\r\n")matches++;}
            if(matches!=1)return null;
            result.Add(new(script.id,second.label,source,Ptr(script),Ptr(cell),Ptr(cell._button),Ptr(cell._onClick)));
        }
        return result.ToArray();
    }
    string Controls(EventSelect owner)
    {
        var source=CaptureSources(owner)??throw new InvalidOperationException("CURRENT_CHOICE_SOURCE_CHANGED");var rows=new List<object>();var list=owner._selectList;
        for(int n=0;n<list.Count;n++){var c=list[n];var button=c._button;var nav=button.navigation;
            rows.Add(new{source=source[n],textRoot=Ptr(c._textRoot),textRect=RectState(c._textRoot.TryCast<RectTransform>()),buttonRect=RectState(button.transform.TryCast<RectTransform>()),selectedRoot=Ptr(c._select),clickTarget=Ptr(c._onClick.Target),branch=Ptr(c._onClick.Target.TryCast<EventSelect.__c__DisplayClass10_0>()!.clickAction),buttonClick=Ptr(button.onClick),button.interactable,
                navigation=(int)nav.mode,up=Ptr(nav.selectOnUp),down=Ptr(nav.selectOnDown),left=Ptr(nav.selectOnLeft),right=Ptr(nav.selectOnRight),inventory=Inventory(c.transform)});}
        ProofMetrics.FullSerializations++;
        return JsonSerializer.Serialize(new{owner=Ptr(owner),instance=owner.GetInstanceID(),root=Ptr(owner._root),letters=Ptr(owner._letters),eventText=Ptr(owner._eventText),action=Ptr(owner._uIaction),rows});
    }
    static object RectState(RectTransform? rect)=>!Alive(rect)?new{present=false}:(object)new{present=true,anchorMin=new[]{rect!.anchorMin.x,rect.anchorMin.y},anchorMax=new[]{rect.anchorMax.x,rect.anchorMax.y},
        pivot=new[]{rect.pivot.x,rect.pivot.y},size=new[]{rect.sizeDelta.x,rect.sizeDelta.y},position=new[]{rect.anchoredPosition.x,rect.anchoredPosition.y},scale=new[]{rect.localScale.x,rect.localScale.y,rect.localScale.z},region=new[]{rect.rect.x,rect.rect.y,rect.rect.width,rect.rect.height}};
    object[] Inventory(Transform root)
    {
        ProofMetrics.FullTreeScans++;
        var rows=new List<object>();var stack=new Stack<(Transform,int)>();stack.Push((root,0));int count=0;
        while(stack.Count>0){var(t,depth)=stack.Pop();if(++count>512||depth>32)throw new InvalidOperationException("CHOICE_COMPONENT_BUDGET");
            if(display!=null&&display.Visuals.Any(x=>Alive(x)&&Ptr(x)==Ptr(t.gameObject)))continue;
            rows.Add(new{node=Ptr(t),instance=t.GetInstanceID(),name=t.name,components=t.GetComponents<Component>().Where(x=>display==null||!display.Masks.Any(m=>Alive(m)&&Ptr(m)==Ptr(x)))
                .Select((x,ordinal)=>new{pointer=Ptr(x),instance=x.GetInstanceID(),nativeClass=UnityNative.Class(x),ordinal}).ToArray(),texts=t.GetComponents<TextMeshProUGUI>().Select(x=>new{pointer=Ptr(x),text=x.text,font=Ptr(x.font),material=Ptr(x.fontSharedMaterial),size=x.fontSize}).ToArray()});
            if(t.childCount+count+stack.Count>512)throw new InvalidOperationException("CHOICE_SUBTREE_BUDGET");for(int n=t.childCount-1;n>=0;n--)stack.Push((t.GetChild(n),depth+1));}
        return rows.ToArray();
    }
    sealed record RaycastHit(long Go,long Button,long Module,int Layer,int Order);
    sealed record RaycastRow(long Button,float X,float Y,ImmutableArray<RaycastHit> Hits);
    sealed record RaycastProof(long EventSystem,ImmutableArray<RaycastRow> Rows)
    {
        public bool SameAs(RaycastProof other)=>EventSystem==other.EventSystem&&Rows.Length==other.Rows.Length&&Rows.Zip(other.Rows).All(pair=>
            pair.First.Button==pair.Second.Button&&pair.First.X==pair.Second.X&&pair.First.Y==pair.Second.Y&&pair.First.Hits.SequenceEqual(pair.Second.Hits));
    }
    RaycastProof Raycasts(EventSelect owner,bool stable=false)
    {
        if(stable)ProofMetrics.StableRaycasts++;else ProofMetrics.BoundaryRaycasts++;
        var es=EventSystem.current;if(!Alive(es))throw new InvalidOperationException("CURRENT_EVENT_SYSTEM_REQUIRED");var rows=ImmutableArray.CreateBuilder<RaycastRow>();
        foreach(var cell in owner._selectList){var button=cell._button;var rect=button.transform.TryCast<RectTransform>();var canvas=button.GetComponentInParent<Canvas>();if(!Alive(rect)||!Alive(canvas))throw new InvalidOperationException("CURRENT_BUTTON_CANVAS_REQUIRED");
            var corners=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);rect!.GetWorldCorners(corners);var screen=RectTransformUtility.WorldToScreenPoint(canvas!.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,(corners[0]+corners[2])*0.5f);
            var pointer=new PointerEventData(es){position=screen};var hits=new Il2CppSystem.Collections.Generic.List<RaycastResult>();es!.RaycastAll(pointer,hits);if(hits.Count is <1 or >128)throw new InvalidOperationException("CURRENT_RAYCAST_BUDGET");bool matched=false;var identities=ImmutableArray.CreateBuilder<RaycastHit>();
            for(int n=0;n<hits.Count;n++){var hit=hits[n];if(!Alive(hit.gameObject))continue;var hitButton=hit.gameObject.GetComponentInParent<InertButton>();matched|=Ptr(hitButton)==Ptr(button);identities.Add(new(Ptr(hit.gameObject),Ptr(hitButton),Ptr(hit.module),hit.sortingLayer,hit.sortingOrder));}
            if(!matched)throw new InvalidOperationException("CURRENT_BUTTON_RAYCAST_REQUIRED");rows.Add(new(Ptr(button),screen.x,screen.y,identities.ToImmutable()));}
        // Hover/highlight selection is ordinary input state; it is not branch/delegate contamination.
        return new(Ptr(es),rows.ToImmutable());
    }
    Func<bool> CaptureStableGuard(EventSelect owner)
    {
        ProofMetrics.GuardCaptures++;
        var guards=new List<Func<bool>>();var sourceScripts=new HashSet<long>();var list=owner._selectList;int count=list.Count,instance=owner.GetInstanceID();
        long listPtr=Ptr(list),root=Ptr(owner._root),letters=Ptr(owner._letters),eventText=Ptr(owner._eventText),action=Ptr(owner._uIaction);
        guards.Add(()=>Alive(owner)&&owner.GetInstanceID()==instance&&owner.gameObject.activeInHierarchy&&Alive(owner._root)&&owner._root.activeInHierarchy
            &&Ptr(owner._selectList)==listPtr&&owner._selectList.Count==count&&Ptr(owner._root)==root&&Ptr(owner._letters)==letters&&Ptr(owner._eventText)==eventText&&Ptr(owner._uIaction)==action);
        for(int n=0;n<count;n++)
        {
            int index=n;var c=list[index];var button=c._button;var first=c._onClick.Target.TryCast<EventSelect.__c__DisplayClass10_0>();
            var second=first?.clickAction?.Target?.TryCast<ScriptObject.__c__DisplayClass51_0>();var script=second?.__4__this;
            if(first==null||second==null||script==null)throw new InvalidOperationException("CURRENT_CHOICE_SOURCE_CHANGED");
            long cell=Ptr(c),buttonPtr=Ptr(button),textRoot=Ptr(c._textRoot),selectRoot=Ptr(c._select),click=Ptr(c._onClick),clickTarget=Ptr(c._onClick.Target),branch=Ptr(first.clickAction),buttonClick=Ptr(button.onClick);
            string source=c._text??"",scriptId=script.id,label=second.label;var nav=button.navigation;bool interactable=button.interactable;
            guards.Add(()=>Alive(c)&&Alive(button)&&UnityNative.Nodes(c.transform,owner.transform).Length>0&&Ptr(owner._selectList[index])==cell&&Ptr(c._button)==buttonPtr&&Ptr(c._textRoot)==textRoot&&Ptr(c._select)==selectRoot
                &&Ptr(c._onClick)==click&&Ptr(c._onClick.Target)==clickTarget&&Ptr(first.clickAction)==branch&&Ptr(first.clickAction.Target)==Ptr(second)
                &&Ptr(second.__4__this)==Ptr(script)&&script.IsEnabled&&script.id==scriptId&&second.label==label&&(c._text??"")==source&&Ptr(button.onClick)==buttonClick
                &&button.interactable==interactable&&(int)button.navigation.mode==(int)nav.mode&&Ptr(button.navigation.selectOnUp)==Ptr(nav.selectOnUp)&&Ptr(button.navigation.selectOnDown)==Ptr(nav.selectOnDown)
                &&Ptr(button.navigation.selectOnLeft)==Ptr(nav.selectOnLeft)&&Ptr(button.navigation.selectOnRight)==Ptr(nav.selectOnRight));
            // There is no verified script mutation notification. Retain every parsed source row's exact identity/fields
            // as cached direct guards, so duplicates or a changed branch cannot hide behind an unchanged list pointer.
            if(sourceScripts.Add(Ptr(script)))
            {
                var rows=script.TextList;long rowsPtr=Ptr(rows);int rowCount=rows.Count;
                guards.Add(()=>Ptr(script.TextList)==rowsPtr&&script.TextList.Count==rowCount);
                for(int k=0;k<rowCount;k++)
                {
                    int at=k;var row=rows[at];long pointer=Ptr(row);string plane=row?.PlaneText??"";var fields=row?.splitText;
                    string?[] values=fields==null?[]:Enumerable.Range(0,fields.Length).Select(i=>fields[i]).ToArray();long fieldsPtr=Ptr(fields);
                    guards.Add(()=>{ProofMetrics.CachedSourceRowChecks++;return Ptr(script.TextList[at])==pointer&&(row?.PlaneText??"")==plane&&Ptr(row?.splitText)==fieldsPtr
                        &&(fields==null?row?.splitText==null:row?.splitText?.Length==values.Length&&Enumerable.Range(0,values.Length).All(i=>row.splitText[i]==values[i]));});
                }
            }
            var stack=new Stack<(Transform,int)>();stack.Push((c.transform,0));int nodes=0;
            while(stack.Count>0)
            {
                var(node,depth)=stack.Pop();if(++nodes>512||depth>32)throw new InvalidOperationException("CHOICE_COMPONENT_BUDGET");
                if(display!=null&&display.Visuals.Any(x=>Alive(x)&&Ptr(x)==Ptr(node.gameObject)))continue;
                int nodeInstance=node.GetInstanceID(),children=node.childCount;string name=node.name;long parent=Ptr(node.parent);
                var components=node.GetComponents<Component>().Where(x=>display==null||!display.Masks.Any(m=>Alive(m)&&Ptr(m)==Ptr(x))).ToArray();
                var identities=components.Select(Ptr).ToArray();var childIdentities=Enumerable.Range(0,children).Select(i=>Ptr(node.GetChild(i))).ToArray();
                // Native selected/highlight visuals may animate normally. The pre-existing control proof binds
                // text/button geometry; other geometry remains covered by the fresh raycast hit proof.
                var rect=Ptr(node)==textRoot||Ptr(node)==Ptr(button.transform)?node.TryCast<RectTransform>():null;
                (Vector2,Vector2,Vector2,Vector2,Vector2,Vector3,Rect)? rectState=rect==null?null:(rect.anchorMin,rect.anchorMax,rect.pivot,rect.sizeDelta,rect.anchoredPosition,rect.localScale,rect.rect);
                guards.Add(()=>{
                    ProofMetrics.CachedNodeChecks++;
                    if(!Alive(node)||node.GetInstanceID()!=nodeInstance||node.childCount!=children||node.name!=name||Ptr(node.parent)!=parent)return false;
                    var now=node.GetComponents<Component>().Where(x=>display==null||!display.Masks.Any(m=>Alive(m)&&Ptr(m)==Ptr(x))).ToArray();
                    if(now.Length!=identities.Length||!now.Select(Ptr).SequenceEqual(identities))return false;
                    for(int j=0;j<children;j++)if(Ptr(node.GetChild(j))!=childIdentities[j])return false;
                    return rectState==null||Alive(rect)&&(rect!.anchorMin,rect.anchorMax,rect.pivot,rect.sizeDelta,rect.anchoredPosition,rect.localScale,rect.rect).Equals(rectState.Value);
                });
                foreach(var text in node.GetComponents<TextMeshProUGUI>())
                {
                    string nativeText=text.text;long font=Ptr(text.font),material=Ptr(text.fontSharedMaterial);float size=text.fontSize;
                    guards.Add(()=>Alive(text)&&text.text==nativeText&&Ptr(text.font)==font&&Ptr(text.fontSharedMaterial)==material&&text.fontSize==size);
                }
                for(int j=children-1;j>=0;j--)stack.Push((node.GetChild(j),depth+1));
            }
        }
        return ()=>{ProofMetrics.StableGuards++;return gate.CanAccess&&guards.All(check=>check());};
    }
    TextMeshProUGUI[] OwnedTexts(Transform root){var stack=new Stack<(Transform,int)>();stack.Push((root,0));var result=new List<TextMeshProUGUI>();int count=0;while(stack.Count>0){var(t,depth)=stack.Pop();if(++count>512||depth>32)throw new InvalidOperationException("CHOICE_GLYPH_BUDGET");result.AddRange(t.GetComponents<TextMeshProUGUI>());if(result.Count>32||t.childCount+count+stack.Count>512)throw new InvalidOperationException("CHOICE_GLYPH_BUDGET");for(int n=0;n<t.childCount;n++)stack.Push((t.GetChild(n),depth+1));}return result.ToArray();}
    void ChoiceFailure(ChoiceTransaction d,ChoiceCommitFailure failure)
    {
        log.Record($"CH_GENERATION_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_GEN_{d.Generation}");
        log.Record(ChoiceDisplayDiagnostics.Code(failure,d.Plan.GroupId));
        log.Record($"CH_EXCEPTION_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_{failure.Exception.ToString().ToUpperInvariant()}_CLEANUP_{failure.CleanupException.ToString().ToUpperInvariant()}");
        if(d.ChangedRole!=ChoiceControlRole.None)log.Record($"CH_CONTROL_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_{d.ChangedRole.ToString().ToUpperInvariant()}");
    }
    bool ChoiceBoundaryUnchanged(ChoiceTransaction d)
    {
        bool post=d.Phase==ChoiceDisplayPhase.Postcommit;
        if(!post)d.Phase=ChoiceDisplayPhase.PrecommitControls;
        var currentControls=Controls(d.Owner);d.ChangedRole=ChoiceDisplayDiagnostics.ControlsChanged(d.Baseline,currentControls);
        if(d.ChangedRole!=ChoiceControlRole.None)throw new ChoiceDisplayException(ChoiceDisplayReason.NativeControlsChanged);
        if(!post)d.Phase=ChoiceDisplayPhase.PrecommitRaycast;
        if(!Raycasts(d.Owner).SameAs(d.Raycast))throw new ChoiceDisplayException(ChoiceDisplayReason.NativeRaycastChanged);
        return true;
    }
    Graphic[] PrepareOwnGraphics(GameObject go,TextMeshProUGUI ownText)
    {
        // Only this newly created root and its verified TMP-generated descendants are mutable/excludable.
        var graphics=new List<Graphic>();var stack=new Stack<(Transform,int)>();stack.Push((go.transform,0));int nodes=0;
        while(stack.Count>0){var(t,depth)=stack.Pop();if(++nodes>32||depth>8)throw new ChoiceDisplayException(ChoiceDisplayReason.OwnGraphicScopeChanged);
            if(t.GetComponents<Selectable>().Length!=0||t.GetComponents<EventTrigger>().Length!=0)throw new ChoiceDisplayException(ChoiceDisplayReason.OwnInputComponent);
            foreach(var g in t.GetComponents<Graphic>()){
                bool rootText=Ptr(g)==Ptr(ownText)&&Ptr(t)==Ptr(go.transform);
                var sub=g.TryCast<TMP_SubMeshUI>();bool generated=Alive(sub)&&Ptr(sub!.textComponent)==Ptr(ownText)&&t.IsChildOf(go.transform);
                if(!rootText&&!generated)throw new ChoiceDisplayException(ChoiceDisplayReason.OwnGraphicScopeChanged);
                g.raycastTarget=false;graphics.Add(g);}
            for(int n=t.childCount-1;n>=0;n--)stack.Push((t.GetChild(n),depth+1));}
        return graphics.ToArray();
    }
    static int Quantity(float value)=>float.IsFinite(value)?Math.Clamp((int)Math.Round(value*10),-100000,100000):-100000;
    void ChoiceFit(ChoiceTransaction d,TextMeshProUGUI text,RectTransform rect,bool medium,bool fits)
    {
        string key=ChoiceDisplayDiagnostics.Key(d.Plan.GroupId);var info=text.textInfo;
        log.Record($"CH_FIT_G_{key}_O_{d.Ordinal}_S_{Quantity(text.fontSize)}_W_{Quantity(rect.rect.width)}_H_{Quantity(rect.rect.height)}");
        log.Record($"CH_GLYPH_G_{key}_O_{d.Ordinal}_C_{Math.Clamp(info?.characterCount??0,0,4096)}_L_{Math.Clamp(info?.lineCount??0,0,128)}_T_{(text.isTextTruncated?1:0)}_M_{(medium?1:0)}_F_{(fits?1:0)}");
        log.Record($"CH_PREFERRED_G_{key}_O_{d.Ordinal}_W_{Quantity(text.preferredWidth)}_H_{Quantity(text.preferredHeight)}");
    }
    void ChoiceMissingGlyph(ChoiceTransaction d,string route,int scalar,bool required)
    {
        log.Record($"CH_GLYPH_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_O_{d.Ordinal}_{route}_U{scalar:X4}_{ChoiceFontRolePolicy.Kind(scalar).ToString().ToUpperInvariant()}_REQUIRED_{(required?1:0)}");
    }
    void PrepareChoice(EventSelect owner)
    {
        if(!Enabled||!choiceAvailable||!owner.gameObject.activeInHierarchy||!owner._root.activeInHierarchy||!DrainRetired())return;
        var source=CaptureSources(owner);if(source==null){log.Record(ChoiceDisplayDiagnostics.Code(new(ChoiceDisplayPhase.ResolveSource,ChoiceDisplayReason.SourceChanged,-1,ChoiceExceptionKind.None),waitingPlan?.GroupId??"unresolved"));return;}
        var plan=StoryChoicePolicy.Resolve(selected(),source,activeMaster);if(plan==null){log.Record(ChoiceDisplayDiagnostics.Code(new(ChoiceDisplayPhase.Plan,ChoiceDisplayReason.PlanUnavailable,-1,ChoiceExceptionKind.None),waitingPlan?.GroupId??"unresolved"));return;}
        CleanupDisplay();if(!DrainRetired())return;
        var d=new ChoiceTransaction{Owner=owner,Instance=owner.GetInstanceID(),Generation=gate.NextLifetime(),Plan=plan,Visuals=new GameObject[plan.Options.Length],Masks=new CanvasGroup[plan.Options.Length],Visibility=new CanvasGroup[plan.Options.Length],Texts=new TextMeshProUGUI[plan.Options.Length],Graphics=new Graphic[plan.Options.Length][]};
        display=d;log.Record($"CH_GENERATION_G_{ChoiceDisplayDiagnostics.Key(plan.GroupId)}_GEN_{d.Generation}");
        try
        {
            d.Phase=ChoiceDisplayPhase.BaselineControls;d.Baseline=Controls(owner);d.Phase=ChoiceDisplayPhase.BeforeRaycast;d.Raycast=Raycasts(owner);
            var list=owner._selectList;
            for(int n=0;n<plan.Options.Length;n++)
            {
                d.Ordinal=n;
                string bindingKey=$"CH_BIND_G_{ChoiceDisplayDiagnostics.Key(plan.GroupId)}_O_{n}_B_{ChoiceDisplayDiagnostics.Key(plan.Options[n].Binding.Id)}";
                if(choiceKeys.Count<128&&choiceKeys.Add(bindingKey))log.Record(bindingKey);
                d.Phase=ChoiceDisplayPhase.FontPrimary;
                var cell=list[n];var root=cell._textRoot;var parent=root.parent;
                if(!Alive(parent)||UnityNative.Nodes(parent!,cell.transform).Length==0||cell._button.transform.IsChildOf(root)||cell._select.transform.IsChildOf(root))throw new ChoiceDisplayException(ChoiceDisplayReason.MaskContainsInput);
                if(root.GetComponents<CanvasGroup>().Length!=0)throw new ChoiceDisplayException(ChoiceDisplayReason.ExistingNativeMask);
                var samples=OwnedTexts(root).Where(x=>Alive(x)&&x.enabled&&x.gameObject.activeInHierarchy&&x.font!=null&&(x.text??"").Length>0).ToArray();
                if(samples.Length==0)throw new ChoiceDisplayException(ChoiceDisplayReason.PrimaryUnavailable);
                if(samples[0].font.name!="GameFont")throw new ChoiceDisplayException(ChoiceDisplayReason.PrimaryRouteMismatch);
                d.Phase=ChoiceDisplayPhase.FontDonor;if(fonts.Current==null||!Alive(fonts.Medium))throw new ChoiceDisplayException(ChoiceDisplayReason.DonorUnavailable);
                d.Phase=ChoiceDisplayPhase.Glyph;var donor=fonts.Current!;string ko=plan.Options[n].Text;
                // Medium owns the Hangul glyphs, not every primary-font whitespace/symbol scalar.
                // Keep the original full-string GameFont/fallback proof and rendered Hangul donor proof below.
                var coverage=ChoiceFontRolePolicy.ProbeDonor(ko,scalar=>fonts.Service.Supports(new System.Text.Rune(scalar).ToString(),donor));
                if(coverage.FirstMissingScalar is int missingAny)ChoiceMissingGlyph(d,"DONOR",missingAny,ChoiceFontRolePolicy.RequiresMedium(missingAny));
                if(coverage.FirstMissingRequiredScalar is int missingHangul){if(coverage.FirstMissingScalar!=missingHangul)ChoiceMissingGlyph(d,"DONOR",missingHangul,true);throw new ChoiceDisplayException(ChoiceDisplayReason.DonorGlyphMissing);}
                if(!fonts.Glyphs(samples[0].font,ko)){
                    int? missingPrimary=ChoiceFontRolePolicy.FirstMissingScalar(ko,scalar=>fonts.Glyphs(samples[0].font,new System.Text.Rune(scalar).ToString()));
                    if(missingPrimary is int missing)ChoiceMissingGlyph(d,"PRIMARY_FALLBACK",missing,true);
                    throw new ChoiceDisplayException(ChoiceDisplayReason.FallbackGlyphMissing);}
                var sample=samples[0];var button=cell._button.transform.TryCast<RectTransform>();if(!Alive(button))throw new ChoiceDisplayException(ChoiceDisplayReason.ButtonRectRequired);
                d.Phase=ChoiceDisplayPhase.CreateOwnVisual;
                var go=new GameObject("GCModChoiceVisual");d.Visuals[n]=go;go.layer=parent!.gameObject.layer;var rect=go.AddComponent<RectTransform>();rect.SetParent(parent,false);
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);var corners=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);button!.GetWorldCorners(corners);
                var p0=parent.InverseTransformPoint(corners[0]);var p2=parent.InverseTransformPoint(corners[2]);float width=Math.Abs(p2.x-p0.x),height=Math.Abs(p2.y-p0.y);if(width<=50||height<=10)throw new ChoiceDisplayException(ChoiceDisplayReason.ButtonLayoutUnsettled);
                rect.localPosition=(p0+p2)*0.5f;rect.sizeDelta=new Vector2(width*0.92f,height*0.92f);rect.localScale=Vector3.one;go.AddComponent<LayoutElement>().ignoreLayout=true;
                var visibility=go.AddComponent<CanvasGroup>();d.Visibility[n]=visibility;visibility.alpha=0;visibility.blocksRaycasts=false;visibility.interactable=false;
                var text=go.AddComponent<TextMeshProUGUI>();d.Texts[n]=text;text.raycastTarget=false;text.font=sample.font;text.fontSharedMaterial=sample.fontSharedMaterial;text.color=sample.color;text.alignment=TextAlignmentOptions.Center;
                text.enableAutoSizing=false;text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Ellipsis;text.margin=Vector4.zero;text.text=plan.Options[n].Text;
                d.Phase=ChoiceDisplayPhase.Fit;float start=sample.fontSize;if(start<12||start>100)throw new ChoiceDisplayException(ChoiceDisplayReason.NativeFontSizeBound);bool fits=false,medium=true;
                for(float size=start;size>=start*0.8f;size-=1){text.fontSize=size;text.ForceMeshUpdate(true,false);d.Graphics[n]=PrepareOwnGraphics(go,text);var info=text.textInfo;
                    fits=info!=null&&info.characterCount==text.text.Length&&info.lineCount==1&&!text.isTextTruncated&&text.preferredWidth<=rect.rect.width+0.5f&&text.preferredHeight<=rect.rect.height+0.5f;
                    medium=true;if(info!=null)for(int k=0;k<info.characterCount;k++){var c=info.characterInfo[k];if(c.character is >= '\uAC00' and <= '\uD7A3')medium&=c.isVisible&&Ptr(c.fontAsset)==Ptr(fonts.Medium);}
                    fits&=medium;if(fits)break;}
                ChoiceFit(d,text,rect,medium,fits);if(!fits)throw new ChoiceDisplayException(ChoiceDisplayReason.OwnedVisualDidNotFit);
                var mask=root.gameObject.AddComponent<CanvasGroup>();d.Masks[n]=mask;mask.alpha=1;mask.interactable=true;mask.blocksRaycasts=true;mask.ignoreParentGroups=false;
            }
            var commit=ChoiceGroupTransaction.TryCommitDetailed(d.Texts.Length,n=>Alive(d.Visuals[n])&&Alive(d.Texts[n])&&Alive(d.Visibility[n])&&Alive(d.Masks[n]),
                ()=>ChoiceBoundaryUnchanged(d),n=>{d.Visibility[n].alpha=1;d.Masks[n].alpha=0;},CleanupDisplay,(phase,ordinal)=>{d.Phase=phase;d.Ordinal=ordinal;});
            if(!commit.Committed){ChoiceFailure(d,commit.Failure! with{Phase=d.Phase});return;}
            // Capture after commit: owned masks/visuals are explicitly excluded, native input components are included.
            d.Phase=ChoiceDisplayPhase.Retained;d.StableGuard=CaptureStableGuard(owner);d.Committed=true;CancelWaiting();log.Record("STORY_CHOICE_GROUP_COMMITTED");
        }
        catch(Exception failure){var outcome=new ChoiceCommitFailure(d.Phase,ChoiceDisplayDiagnostics.Reason(failure),d.Ordinal,ChoiceDisplayDiagnostics.ExceptionKind(failure));
            try{CleanupDisplay();}catch(Exception cleanup){outcome=outcome with{CleanupException=ChoiceDisplayDiagnostics.ExceptionKind(cleanup)};}ChoiceFailure(d,outcome);}
    }
    bool DrainRetired()
    {
        if(!gate.CanAccess)return false;if(retiring==null)return true;var d=retiring;
        if(!cleanupBarrier.Confirm(Time.frameCount,()=>d.Visuals.All(x=>!Alive(x))&&d.Masks.All(x=>!Alive(x))&&d.Visibility.All(x=>!Alive(x))))return false;
        retiring=null;log.Record($"CH_CLEANUP_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_DESTROY_CONFIRMED");return true;
    }
    void CleanupDisplay()
    {
        var d=display;if(d==null)return;if(!gate.CanAccess)throw new InvalidOperationException("CHOICE_CLEANUP_SAFE_THREAD_REQUIRED");
        d.CleanupRequested=true;
        // Complete every owned recovery attempt even when one setter fails; preserve tracked leases for retry.
        var recovered=ChoiceOwnedCleanup.Run(d.Visuals.Length,
            n=>{if(Alive(d.Visibility[n]))d.Visibility[n].alpha=0;if(Alive(d.Visuals[n]))d.Visuals[n].SetActive(false);},
            n=>{if(Alive(d.Masks[n])){if(d.Masks[n].alpha==0)d.Masks[n].alpha=1;UnityEngine.Object.Destroy(d.Masks[n]);}},
            n=>{if(Alive(d.Visuals[n]))UnityEngine.Object.Destroy(d.Visuals[n]);});
        if(!recovered.Completed){if(!d.CleanupFailureLogged){d.CleanupFailureLogged=true;log.Record($"CH_CLEANUP_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_O_{recovered.OptionOrdinal}_{recovered.Role.ToString().ToUpperInvariant()}_{recovered.Exception.ToString().ToUpperInvariant()}_RETRY");}throw new ChoiceDisplayException(ChoiceDisplayReason.CleanupPending);}
        cleanupBarrier.Retire(Time.frameCount);retiring=d;display=null;
        log.Record($"CH_CLEANUP_G_{ChoiceDisplayDiagnostics.Key(d.Plan.GroupId)}_HIDDEN_DESTROY_PENDING");
    }
    static void ClickBefore(GcEventClickEvent __instance,string __0)=>current?.CaptureClick(__instance,__0);
    static void ClickAfter(GcEventClickEvent __instance){var p=current;if(p!=null&&p.gate.CanAccess)p.synchronous.Remove(Ptr(__instance));}
    static Exception? ClickFinally(GcEventClickEvent __instance,Exception? __exception){ClickAfter(__instance);var p=current;if(__exception!=null&&p!=null&&p.gate.CanAccess){long key=Ptr(__instance);if(p.clicks.Remove(key,out var click))p.claimed.Remove(click.Generation);p.clickReceivers.Remove(key);}return __exception;}
    void CaptureClick(GcEventClickEvent receiver,string token)
    {
        if(!Enabled||!Alive(receiver))return;long key=Ptr(receiver);
        // Any new/nested attempt invalidates this receiver's old click; failed capture cannot reuse an earlier token.
        if(clicks.Remove(key,out var previousClick))claimed.Remove(previousClick.Generation);clickReceivers.Remove(key);
        if(synchronous.Contains(key)){synchronous.Remove(key);log.Record("ANNOTATION_NESTED_CLICK_HELD");return;}
        var parent=receiver.parent;if(!Alive(parent)||Ptr(parent.clickEvent)!=key)return;
        var scripts=parent.scriptManager?.ScriptList;if(scripts==null||scripts.Count>32)return;bool story=false;
        var supported=selected().Bindings.Values.Where(b=>b.Enabled&&b.AdapterId=="story.choice.visual.v1").Select(b=>b.Selector.ScriptId).ToHashSet(StringComparer.Ordinal);
        for(int n=0;n<scripts.Count;n++){var script=scripts[n];if(script!=null&&script.IsEnabled&&supported.Contains(script.id))story=true;}
        if(clicks.Count>=32&&!clicks.ContainsKey(key))return;
        clicks[key]=new(gate.NextLifetime(),key,Ptr(parent),receiver.GetInstanceID(),Environment.CurrentManagedThreadId,token??"",Stopwatch.GetTimestamp(),story);clickReceivers[key]=receiver;synchronous.Add(key);
        log.Record("ANNOTATION_CURRENT_CLICK_CAPTURED");
    }
    bool TryCaptureDeliveredRecord(GcEventClickEvent receiver,Gc.Master.NovelKeyWordMaster master,out AnnotationRecordProof? proof)
    {
        proof=null;if(!Enabled||!Alive(receiver)||IL2CPP.il2cpp_object_get_class(receiver.Pointer)!=Il2CppClassPointerStore<GcEventClickEvent>.NativeClassPtr)return false;
        long key=Ptr(receiver);if(!clicks.TryGetValue(key,out var click)||!clickReceivers.TryGetValue(key,out var tracked)||!Alive(tracked)||Ptr(tracked)!=key||tracked.GetInstanceID()!=click.ReceiverInstance)return false;
        var parent=receiver.parent;if(!Alive(parent))return false;
        double age=(Stopwatch.GetTimestamp()-click.Started)/(double)Stopwatch.Frequency;
        if(!AnnotationDeliveryProofPolicy.RecordEligible(click,key,Ptr(parent),Ptr(parent.clickEvent),receiver.GetInstanceID(),Environment.CurrentManagedThreadId,
            clicks[key].Generation,Ptr(master),master.Id,age,claimed.Contains(click.Generation)))return false;
        proof=new(click,Ptr(master),master.Id,master.Keyword??"",master.Description??"",master.Ruby??"",!synchronous.Contains(key));return true;
    }
    void RecordDelivered(NativeHookContext context)
    {
        if(!Enabled||context.Instance is not GcEventClickEvent receiver||context.Args.FirstOrDefault() is not Gc.Master.NovelKeyWordMaster master)return;
        if(!TryCaptureDeliveredRecord(receiver,master,out var proof)||proof==null)return;
        if(records.ContainsKey(Ptr(master))){records.Remove(Ptr(master));return;}if(records.Count>=32)return;records.Add(Ptr(master),proof);
        log.Record("ANNOTATION_PRIVATE_TYPED_RECORD_CAPTURED");
    }
    public void Tick()
    {
        if(!gate.CanAccess)return;frame++;if(!ReconcileDisplay()||!Enabled)return;
        if(display?.Committed==true)try{
            // Cached direct guards replace repeated source discovery, subtree traversal and serialization.
            // No native global raycast/layout mutation generation has been verified; keep the bounded hit proof each frame.
            var d=display;d.Phase=ChoiceDisplayPhase.Retained;d.Ordinal=-1;
            if(!d.StableGuard())throw new ChoiceDisplayException(ChoiceDisplayReason.NativeControlsChanged);
            if(!Raycasts(d.Owner,stable:true).SameAs(d.Raycast))throw new ChoiceDisplayException(ChoiceDisplayReason.NativeRaycastChanged);
            for(int n=0;n<d.Texts.Length;n++){
                d.Ordinal=n;if(!Alive(d.Visuals[n])||!Alive(d.Texts[n])||!Alive(d.Visibility[n])||!Alive(d.Masks[n])||!d.Visuals[n].activeSelf||d.Texts[n].text!=d.Plan.Options[n].Text)
                    throw new ChoiceDisplayException(ChoiceDisplayReason.OwnObjectLost);
                if(d.Graphics[n]==null||d.Graphics[n].Any(g=>!Alive(g)||g.raycastTarget||Ptr(g)!=Ptr(d.Texts[n])&&(!Alive(g.TryCast<TMP_SubMeshUI>())||Ptr(g.TryCast<TMP_SubMeshUI>()!.textComponent)!=Ptr(d.Texts[n])||!g.transform.IsChildOf(d.Visuals[n].transform))))
                    throw new ChoiceDisplayException(ChoiceDisplayReason.OwnGraphicScopeChanged);
            }
        }catch(Exception failure){var d=display;if(d!=null){var outcome=new ChoiceCommitFailure(d.Phase,ChoiceDisplayDiagnostics.Reason(failure),d.Ordinal,ChoiceDisplayDiagnostics.ExceptionKind(failure));
            try{CleanupDisplay();}catch(Exception cleanup){outcome=outcome with{CleanupException=ChoiceDisplayDiagnostics.ExceptionKind(cleanup)};}ChoiceFailure(d,outcome);}}
        if(waiting!=null){double age=(Stopwatch.GetTimestamp()-waitingStarted)/(double)Stopwatch.Frequency;
            if(!Alive(waiting)||waiting.GetInstanceID()!=waitingInstance||age>3||frame-waitingFrame>120)CancelWaiting();
            else if(frame-waitingFrame is 1 or 3 or 6 or 12 or 24 or 45 or 75)try{PrepareChoice(waiting);}catch(Exception failure){
                log.Record(ChoiceDisplayDiagnostics.Code(new(ChoiceDisplayPhase.ResolveSource,ChoiceDisplayDiagnostics.Reason(failure),-1,ChoiceDisplayDiagnostics.ExceptionKind(failure)),waitingPlan?.GroupId??"unresolved"));}}
        TickHeadings();
        foreach(var row in clicks.Where(x=>(Stopwatch.GetTimestamp()-x.Value.Started)/(double)Stopwatch.Frequency>3).ToArray()){clicks.Remove(row.Key);clickReceivers.Remove(row.Key);synchronous.Remove(row.Key);claimed.Remove(row.Value.Generation);}
        foreach(var row in records.Where(x=>!clicks.TryGetValue(x.Value.Click.Receiver,out var click)||click.Generation!=x.Value.Click.Generation).ToArray())records.Remove(row.Key);
    }
    public bool ReconcileDisplay()
    {
        if(!gate.CanAccess)return false;
        try
        {
            if(display?.CleanupRequested==true)CleanupDisplay();
            DrainRetired();
            if(!Enabled){CancelWaiting();Selected();CleanupDisplay();return true;}
            var snapshot=selected();
            if(waitingPlan!=null&&!StoryChoicePolicy.IsPlanAvailable(snapshot,waitingPlan,activeMaster))CancelWaiting();
            if(display!=null&&!StoryChoicePolicy.IsPlanAvailable(snapshot,display.Plan,activeMaster))CleanupDisplay();
            return true;
        }
        catch(ChoiceDisplayException failure)when(failure.Reason==ChoiceDisplayReason.CleanupPending){return false;}
        catch{log.Record("STORY_DISPLAY_RECONCILE_RETRYABLE");return false;}
    }
    public bool RestoreOwned(){if(IsChoiceBusy)return false;return true;}
    public void Selected(){headings.Clear();clicks.Clear();clickReceivers.Clear();records.Clear();claimed.Clear();synchronous.Clear();if(!IsChoiceBusy)choiceKeys.Clear();}
    public bool Dispose(){if(!gate.CanAccess)return display==null&&retiring==null;CancelWaiting();CleanupDisplay();Selected();current=null;return DrainRetired();}
}
