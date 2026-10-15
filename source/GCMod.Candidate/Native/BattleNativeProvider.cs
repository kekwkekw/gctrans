#nullable enable
using System.Collections.Immutable;
using System.Text.Json;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;
using BU = Gc.Battle.Object.BattleUnit;
using SK = Gc.Battle.Skill.ActiveSkill;
using CD = Gc.Battle.UI.Detail.UnitInfoScrollCellData;

namespace GCMod.Candidate.Native;

/// <summary>UI18's completed B01–B07 producers. Native parameters and control delegates are read only.</summary>
internal sealed class BattleNativeProvider
{
    readonly GameThreadGate gate; readonly NativeHookHub hub; readonly AdapterRegistry registry;
    readonly UiOwnershipService ownership; readonly NativeFontProvider fonts; readonly SafeDiagnostics log;
    readonly Func<TranslationPackageSnapshot> selected;
    readonly IMasterTranslationView activeMaster;
    readonly Func<bool> writesEnabled;
    readonly Dictionary<(long,string,string), Pending> pending = new();
    readonly Dictionary<long,ReadyCard> readyCards=new();
    readonly Dictionary<(long, string), (string Model, long Lifetime)> lifetimes = new();
    int frame;
    sealed record Pending(Component Owner, int Instance, string Selector, string Callback, string Surface, Func<string> Model, string InitialModel, long Started, int Frame);
    sealed record ReadyCard(Gc.Battle.UI.CommandSet Cell,int Instance,Gc.Battle.UI.CommandView.SetParam Param,long ParamPointer,string Source,long Skill,long Started,int Frame);
    public BattleNativeProvider(GameThreadGate gate, NativeHookHub hub, AdapterRegistry registry, UiOwnershipService ownership,
        NativeFontProvider fonts, Func<TranslationPackageSnapshot> selected, SafeDiagnostics log,IMasterTranslationView? activeMaster=null,Func<bool>? writesEnabled=null)
    { this.gate=gate; this.hub=hub; this.registry=registry; this.ownership=ownership; this.fonts=fonts; this.selected=selected; this.log=log;this.activeMaster=activeMaster??MasterTranslationView.Empty;this.writesEnabled=writesEnabled??(()=>false); }
    bool Enabled=>SpecialDisplayPolicy.CanStartNativeWork(gate.CanAccess,writesEnabled);
    static long Ptr(Il2CppObjectBase? x) => x?.Pointer.ToInt64() ?? 0;
    static bool Alive(UnityEngine.Object? x) => x != null;
    static string Model(BU? actor, SK? skill = null) => JsonSerializer.Serialize(new { actor=Ptr(actor), id=actor?.UnitId, master=actor?.MasterId, skill=Ptr(skill), skillId=skill?.Id });
    public void Initialize()
    {
        bool captions = false;
        void Sub(Type type,string method,Type[] args,Action<NativeHookContext> callback) { captions |= hub.Subscribe(type,method,args,callback); }
        Sub(typeof(Gc.Battle.UI.SkillInfo),"Setup",new[]{typeof(BU),typeof(SK)},SkillSetup);
        Sub(typeof(Gc.Battle.UI.Detail.SharedEnemyInfoView),"UpdateScrollView",new[]{typeof(Il2CppSystem.Collections.Generic.List<CD>)},Shared);
        foreach(var type in new[]{typeof(Gc.Battle.UI.Detail.EnemyScrollCell),typeof(Gc.Battle.UI.Detail.ResistScrollCell),typeof(Gc.Battle.UI.Detail.ResistTextScrollCell)}) Sub(type,"Setup",new[]{typeof(CD)},Cell);
        Sub(typeof(Gc.Battle.UI.Detail.EnemyInfoView),"Show",Type.EmptyTypes,Enemy);
        Sub(typeof(Gc.Battle.UI.InstructionView),"UpdateView",Type.EmptyTypes,Instruction);
        Sub(typeof(Gc.Battle.AutoView),"UpdateView",new[]{typeof(bool)},AutoState);
        Sub(typeof(Gc.Battle.TimeScaleView),"UpdateView",new[]{typeof(int)},SpeedState);
        Sub(typeof(Gc.Battle.TimelinePlayView),"UpdateView",new[]{typeof(int)},EffectState);
        Sub(typeof(Gc.Battle.UI.BattleMenuPopup),"OnSet",Type.EmptyTypes,Menu);
        Sub(typeof(Gc.Battle.UI.BattleMenuPopup),"UpdateTabContents",new[]{typeof(int)},Menu);
        Sub(typeof(Gc.Battle.UI.BattleMenuSettingView),"Init",new[]{typeof(UnityEngine.Events.UnityAction)},Setting);
        Sub(typeof(Gc.Quest.QuestSortieController),"SetupUI",Type.EmptyTypes,Sortie);
        Sub(typeof(Gc.Result.MainView),"Show",Type.EmptyTypes,Result);
        bool cost=hub.Subscribe(typeof(Gc.Battle.UI.CommandSet),"Setup",new[]{typeof(Gc.Battle.UI.CommandView.SetParam)},CardCompleted);
        var routes=BattleRoutes.All;
        registry.Register(new("battle.caption.v1","Battle",SelectorContract.Host(routes.Select(r=>"battle-"+r.Surface.ToLowerInvariant()),
            routes.Select(r=>r.Slot),new[]{"caption"},literalPaths:routes.Select(r=>r.Segments),lifetimes:new[]{"owner"}),captions,false,captions?"COMPLETED_NATIVE_B01_B07_CALLBACKS":"OPTIONAL_NATIVE_CALLBACKS_UNAVAILABLE"));
        registry.Register(new("battle.command-cost-title.v1","Battle",SelectorContract.Host(new[]{"battle-command"},new[]{"skill-slot-0-unusable-title","skill-slot-1-unusable-title"},
            new[]{"caption"},literalPaths:new[]{Array.Empty<string>()},templateIds:new[]{"preserve-native-numeric-suffix-v1"},lifetimes:new[]{"owner"}),cost,false,cost?"COMPLETED_NATIVE_SETPARAM":"OPTIONAL_COMMANDSET_CALLBACK_UNAVAILABLE"));
    }
    void Queue(Component owner,string callback,string selector,string surface,Func<string> model,Component? scope)
    {
        if(!Enabled||!Alive(owner)||!owner.gameObject.activeInHierarchy||!Alive(scope))return;
        if(UnityNative.Nodes(scope!.transform,owner.transform).Length==0 && Ptr(scope)!=Ptr(owner))return;
        var source=model(); var key=(Ptr(scope),callback,selector); if(pending.Count>=128&&!pending.ContainsKey(key))return;
        var p=new Pending(scope,scope.GetInstanceID(),selector,callback,surface,model,source,System.Diagnostics.Stopwatch.GetTimestamp(),frame);
        pending[key]=p; Apply(p);
    }
    void Apply(Pending p)
    {
        if(!Enabled||!Alive(p.Owner)||p.Owner.GetInstanceID()!=p.Instance||!p.Owner.gameObject.activeInHierarchy||p.Model()!=p.InitialModel)return;
        var snapshot=selected(); var resolver=new UiResourceResolver(activeMaster);
        string ownerClass=UnityNative.Class(p.Owner);
        foreach(var route in BattleRoutes.All.Where(r=>r.Owner==ownerClass&&r.Callback==p.Callback&&r.Selector==p.Selector&&r.Surface==p.Surface))
        {
            var rules=snapshot.Bindings.Values.Where(b=>b.Enabled&&b.AdapterId=="battle.caption.v1"&&b.SourceIdentity.IdentityJson==JsonSerializer.Serialize(route.SourceId)
                &&b.Selector.ScopeKey=="battle-"+route.Surface.ToLowerInvariant()&&b.Selector.SlotKey==route.Slot&&b.Selector.Segments.SequenceEqual(route.Segments,StringComparer.Ordinal)).ToArray();
            if(rules.Length!=1)continue; var binding=rules[0]; if(!registry.CanExecute(binding,false))continue;var resource=snapshot.Resources[binding.ResourceId];
            var child=ExactChild(p.Owner.transform,route.Segments,p.Surface=="B05"); if(!Alive(child))continue;
            var candidates=child!.GetComponents<TextMeshProUGUI>();if(candidates.Length!=1)continue;var text=candidates[0];
            if(!text.enabled||!text.gameObject.activeInHierarchy||!resolver.TryResolve(resource,out var ko)||fonts.Current==null||!fonts.Service.Supports(ko,fonts.Current)||!fonts.Glyphs(text.font,ko))continue;
            var lk=(Ptr(text),binding.Id);if(lifetimes.Count>=4096&&!lifetimes.ContainsKey(lk))continue; if(!lifetimes.TryGetValue(lk,out var lifetime)||lifetime.Model!=p.InitialModel){lifetime=(p.InitialModel,gate.NextLifetime());lifetimes[lk]=lifetime;}
            var identity=new PhysicalTargetIdentity((ulong)Ptr(text),text.GetInstanceID(),UnityNative.Class(text),lifetime.Lifetime);
            var target=new UnityTextTarget(gate,text,()=>{
                bool live=gate.CanAccess&&Alive(p.Owner)&&Alive(text)&&p.Owner.GetInstanceID()==p.Instance;
                bool ownerActive=live&&p.Owner.gameObject.activeInHierarchy;
                bool bound=ownerActive&&p.Model()==p.InitialModel&&Ptr(ExactChild(p.Owner.transform,route.Segments,p.Surface=="B05"))==Ptr(text.transform);
                bool current=bound&&text.gameObject.activeInHierarchy;
                return new TextTargetState(identity,current?text.text??"":"",p.Instance,lifetime.Lifetime,binding.SourceIdentity.IdentityJson,resource.Jp,current,!live||ownerActive&&!bound);
            });
            var state=target.ReadState();ownership.TryWrite(target,new("Battle",binding.Id,null,binding.Selector.SlotKey,snapshot.Generations.Ui,state),ko);
        }
    }
    static Transform? ExactChild(Transform root,IEnumerable<string> path,bool activeUnique)
    {
        var current=root; foreach(var name in path){Transform? match=null;int count=0;
            if(!Alive(current)||current.childCount>256)return null;
            for(int n=0;n<current.childCount;n++){var child=current.GetChild(n);if(child.name==name&&(!activeUnique||child.gameObject.activeInHierarchy)){match=child;count++;}}
            if(count!=1)return null;current=match!;}return current;
    }
    static string Surface(Component c)
    {
        int depth=0;for(var t=c.transform;Alive(t)&&depth++<16;t=t.parent){var q=t.GetComponent<Gc.Quest.QuestEnemyInfoView>();if(Alive(q)&&Alive(q._sharedView)&&c.transform.IsChildOf(q._sharedView.transform))return "B01";
            var b=t.GetComponent<Gc.Battle.UI.Detail.EnemyInfoView>();if(Alive(b)&&Alive(b._sharedView)&&c.transform.IsChildOf(b._sharedView.transform))return "B05";}return "UNPROVEN";
    }
    void SkillSetup(NativeHookContext c){if(!Enabled||c.Instance is not Gc.Battle.UI.SkillInfo o||c.Args.Length!=2||c.Args[0] is not BU unit||c.Args[1] is not SK skill)return;
        int depth=0;for(var t=o.transform;Alive(t)&&depth++<16;t=t.parent){var q=t.GetComponent<Gc.Quest.QuestEnemySkillInfoView>();if(Alive(q)&&Ptr(q._skillInfo)==Ptr(o)){Queue(q,"Gc.Battle.UI.SkillInfo.Setup","QuestEnemySkillInfoView._skillInfo","B01",()=>Model(unit,skill),q);return;}
            var b=t.GetComponent<Gc.Battle.UI.CommandSkillInfo>();if(Alive(b)&&Ptr(b._skillInfo)==Ptr(o)){Queue(b,"Gc.Battle.UI.SkillInfo.Setup","CommandSkillInfo._skillInfo","B04",()=>Model(unit,skill),b);return;}}}
    void Shared(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.UI.Detail.SharedEnemyInfoView o)Queue(o,"Gc.Battle.UI.Detail.SharedEnemyInfoView.UpdateScrollView","current_shared_owner_active_cells",Surface(o),()=>Model(o._unit),o);}
    void Cell(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.UI.Detail.UnitInfoScrollCell o&&c.Args.FirstOrDefault() is CD data)Queue(o,UnityNative.Class(o)+".Setup","current_active_cell_setup_argument",Surface(o),()=>JsonSerializer.Serialize(new{data=Ptr(data),unit=Model(data.Unit),cell=Ptr(data.CellView)}),o);}
    void Enemy(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.UI.Detail.EnemyInfoView o)Queue(o,"Gc.Battle.UI.Detail.EnemyInfoView.Show","_sharedView","B05",()=>Model(o._sharedView?._unit),o._sharedView);}
    static Gc.Battle.UI.TopView? CurrentTop(Component c){int depth=0;for(var t=c.transform;Alive(t)&&depth++<16;t=t.parent){var v=t.GetComponent<Gc.Battle.UI.TopView>();if(Alive(v))return v;}return null;}
    void AutoState(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.AutoView o){var v=CurrentTop(o);if(Alive(v)&&Ptr(v!.autoFusionButton)==Ptr(o))Queue(v,"Gc.Battle.AutoView.UpdateView","autoFusionButton","B03",()=>JsonSerializer.Serialize(new{value=c.Args[0],action=Ptr(o.buttonAction)}),o);}}
    void SpeedState(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.TimeScaleView o){var v=CurrentTop(o);if(Alive(v)&&Ptr(v!.timeScaleButton)==Ptr(o))Queue(v,"Gc.Battle.TimeScaleView.UpdateView","timeScaleButton","B03",()=>JsonSerializer.Serialize(new{value=c.Args[0],selected=o._selectIndex,action=Ptr(o.buttonAction)}),o);}}
    void EffectState(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.TimelinePlayView o){var v=CurrentTop(o);if(Alive(v)&&Ptr(v!.timelinePlayButton)==Ptr(o))Queue(v,"Gc.Battle.TimelinePlayView.UpdateView","timelinePlayButton","B03",()=>JsonSerializer.Serialize(new{value=c.Args[0],selected=o._selectIndex,action=Ptr(o.buttonAction)}),o);}}
    void Instruction(NativeHookContext c){if(!Enabled||c.Instance is not Gc.Battle.UI.InstructionView o)return;
        Queue(o,"Gc.Battle.UI.InstructionView.UpdateView","_text","B03_OR_B04",()=>JsonSerializer.Serialize(new{type=(int)o._type,layout=(int)o._layout,input=Ptr(o._input),receiver=Ptr(o._input?.Receiver)}),o);
        var command=o._input?._owner?.GetCommandView();if(!Alive(command)||Ptr(command!._input)!=Ptr(o._input))return;
        string cm="Gc.Battle.UI.InstructionView.UpdateView.completed_current_command";Func<string> m=()=>JsonSerializer.Serialize(new{input=Ptr(command._input),receiver=Ptr(command._input?.Receiver),type=(int)command.Type});
        Queue(command,cm,"_shortCutView","B03_OR_B04",m,command._shortCutView);Queue(command,cm,"_waitSet","B03",m,command._waitSet);
        for(int n=0;command._skillSets!=null&&n<Math.Min(5,command._skillSets.Length);n++){var cell=command._skillSets[n];Queue(command,cm,"_skillSets["+n+"]","B03",m,cell);
            if(Alive(cell)&&cell.gameObject.activeInHierarchy&&Alive(cell._button))CaptureCard(cell,cell._button.Param);}
        var views=o._input?._owner?.views?.views;if(views==null||views.Count>24)return;
        foreach(var pair in views){var bottom=pair.Value?.TryCast<Gc.Battle.UI.BottomView>();if(!Alive(bottom)||bottom!._actor==null)continue;
            Queue(bottom,"Gc.Battle.UI.InstructionView.UpdateView.completed_current_bottom","current_views_registry.BottomView._leftStatus","B03",()=>Model(bottom._actor),bottom._leftStatus);}}
    void Menu(NativeHookContext c){if(!Enabled||c.Instance is not Gc.Battle.UI.BattleMenuPopup o)return;var v=o._infoView;
        if(Alive(v)){foreach(var pair in new (string,Component?)[]{("_infoView._evaluate",v._evaluate),("_infoView._winCondition.parent",v._winCondition?.transform.parent),("_infoView._notContinue",v._notContinue?.transform),("_infoView._close",v._close),("_infoView._leave",v._leave),("_infoView._retry",v._retry)})Queue(o,"Gc.Battle.UI.BattleMenuPopup.completed_binding",pair.Item1,"B06",()=>JsonSerializer.Serialize(new{owner=Ptr(o),initialized=o.IsInitialized,quest=v._questName?.text}),pair.Item2);}
        Queue(o,"Gc.Battle.UI.BattleMenuPopup.completed_binding","_settingView","B06",()=>Ptr(o)+":"+o.IsInitialized,o._settingView);
        Queue(o,"Gc.Battle.UI.BattleMenuPopup.OnSet","_settingView","B06",()=>Ptr(o)+":"+o.IsInitialized,o._settingView);
        if(Alive(o._tabController))Queue(o,"Gc.Battle.UI.BattleMenuPopup.completed_binding","_tabController.Tabs","B06",()=>Ptr(o)+":"+o.IsInitialized,ExactChild(o._tabController.transform,new[]{"Tabs"},false));}
    void Setting(NativeHookContext c){if(Enabled&&c.Instance is Gc.Battle.UI.BattleMenuSettingView o)Queue(o,"Gc.Battle.UI.BattleMenuSettingView.Init","current_setting_view","B06",()=>Ptr(o).ToString(),o);}
    void Sortie(NativeHookContext c){if(!Enabled||c.Instance is not Gc.Quest.QuestSortieController o)return;Func<string> model=()=>JsonSerializer.Serialize(new{owner=Ptr(o),stage=o._titleView?._stageId});
        foreach(var pair in new(string,Component?)[]{("_titleView",o._titleView),("_enemyView",o._enemyView),("_dropView",o._dropView),("_buttonView",o._buttonView)})Queue(o,"Gc.Quest.QuestSortieController.SetupUI",pair.Item1,"B02",model,pair.Item2);
        var d=o._deckView?._normalDeckView;if(Alive(d))foreach(var pair in new(string,Component?)[]{("_deckView._normalDeckView._totalCombatPower.parent",d!._totalCombatPower?.transform.parent),("_deckView._normalDeckView._recommendedCombatPower.parent",d._recommendedCombatPower?.transform.parent),("_deckView._normalDeckView._deckMemory",d._deckMemory)})Queue(o,"Gc.Quest.QuestSortieController.SetupUI",pair.Item1,"B02",model,pair.Item2);}
    void Result(NativeHookContext c){if(!Enabled||c.Instance is not Gc.Result.MainView o)return;foreach(var pair in new(string,Component?)[]{("_reward",o._reward),("_userLevel",o._userLevel),("_rightView._button",o._rightView?._button),("_singleRightView._deckMemory",o._singleRightView?._deckMemory)})Queue(o,"Gc.Result.MainView.Show",pair.Item1,"B07",()=>JsonSerializer.Serialize(new{data=Ptr(o._data),stage=o._data?.StageId,quest=o._data?.QuestId}),pair.Item2);}
    static Gc.Battle.UI.CommandView? Command(Gc.Battle.UI.CommandSet o){int depth=0;for(var t=o.transform;Alive(t)&&depth++<16;t=t.parent){var v=t.GetComponent<Gc.Battle.UI.CommandView>();if(Alive(v))return v;}return null;}
    static BU? Actor(Gc.Battle.UI.CommandView view){var views=view._input?._owner?.views?.views; if(views==null||views.Count>24)return null;BU? actor=null;int matches=0;
        foreach(var pair in views){var b=pair.Value?.TryCast<Gc.Battle.UI.BottomView>();if(Alive(b)&&b!.gameObject.activeInHierarchy&&b._actor!=null){actor=b._actor;matches++;}}return matches==1?actor:null;}
    void CardCompleted(NativeHookContext c)
    {
        if(!Enabled||c.Instance is not Gc.Battle.UI.CommandSet cell||c.Args.FirstOrDefault() is not Gc.Battle.UI.CommandView.SetParam param)return;
        CaptureCard(cell,param);
    }
    void CaptureCard(Gc.Battle.UI.CommandSet cell,Gc.Battle.UI.CommandView.SetParam param)
    {
        if(!Enabled||!Alive(cell)||!cell.gameObject.activeInHierarchy||param==null||param.Skill==null)return;
        string source=param.UnusableText??"";if(!source.StartsWith("必要熱情",StringComparison.Ordinal))return;long key=Ptr(cell);
        if(readyCards.TryGetValue(key,out var existing)&&existing.ParamPointer==Ptr(param)&&existing.Source==source&&existing.Skill==Ptr(param.Skill))return;
        if(readyCards.Count>=5&&!readyCards.ContainsKey(key))return;
        var card=new ReadyCard(cell,cell.GetInstanceID(),param,Ptr(param),source,Ptr(param.Skill),System.Diagnostics.Stopwatch.GetTimestamp(),frame);readyCards[key]=card;
        if(TryApplyCard(card))readyCards.Remove(key);
    }
    bool TryApplyCard(ReadyCard card)
    {
        if(!Enabled||!Alive(card.Cell)||card.Cell.GetInstanceID()!=card.Instance||!card.Cell.gameObject.activeInHierarchy||Ptr(card.Param)!=card.ParamPointer||card.Param.UnusableText!=card.Source||Ptr(card.Param.Skill)!=card.Skill)return false;
        var cell=card.Cell;var param=card.Param;var view=Command(cell);var text=cell._button?._unusableText;if(!Alive(view)||!Alive(text)||param.Skill==null)return false;
        int slot=-1,count=0;for(int n=0;view!._skillSets!=null&&n<Math.Min(5,view._skillSets.Length);n++)if(Ptr(view._skillSets[n])==Ptr(cell)){slot=n;count++;}
        var actor=Actor(view!);if(count!=1||slot is <0 or >1||actor==null)return false;
        var snapshot=selected();var bindings=snapshot.Bindings.Values.Where(b=>b.Enabled&&b.AdapterId=="battle.command-cost-title.v1"&&b.Selector.ScopeKey=="battle-command"
            &&b.Selector.SlotKey=="skill-slot-"+slot+"-unusable-title"&&b.Selector.TemplateId=="preserve-native-numeric-suffix-v1"&&b.Selector.Segments.IsEmpty
            &&b.SourceIdentity.Kind=="native-template"&&b.SourceIdentity.IdentityJson==JsonSerializer.Serialize("UI17-TEMPLATE-SLOT-"+slot)).ToArray();
        if(bindings.Length!=1)return false;var binding=bindings[0];if(!registry.CanExecute(binding,false))return false;var resource=snapshot.Resources[binding.ResourceId];string source=card.Source;
        if(!text!.enabled||!text.gameObject.activeInHierarchy||text.text!=source||!BattleCaptionPolicy.TryCostTitle(source,resource,activeMaster,out var output)||fonts.Current==null||!fonts.Service.Supports(output,fonts.Current)||!fonts.Glyphs(text.font,output))return false;
        var actorPtr=Ptr(actor);var skillPtr=Ptr(param.Skill);var parameterPtr=Ptr(param);long life=gate.NextLifetime();int instance=cell.GetInstanceID();
        var identity=new PhysicalTargetIdentity((ulong)Ptr(text),text.GetInstanceID(),UnityNative.Class(text),life);
        var target=new UnityTextTarget(gate,text,()=>{
            bool valid=gate.CanAccess&&Alive(cell)&&Alive(view)&&Alive(text)&&cell.GetInstanceID()==instance;
            bool ownerActive=valid&&cell.gameObject.activeInHierarchy;
            var live=ownerActive?cell._button?.Param:null;
            bool bound=ownerActive&&view!._skillSets!=null&&slot<view._skillSets.Length&&Ptr(view._skillSets[slot])==Ptr(cell)
                &&Ptr(Actor(view))==actorPtr&&Ptr(live)==parameterPtr&&Ptr(live?.Skill)==skillPtr&&live?.UnusableText==source&&Ptr(cell._button?._unusableText)==Ptr(text);
            bool current=bound&&text.gameObject.activeInHierarchy;
            return new TextTargetState(identity,current?text.text??"":"",instance,life,binding.SourceIdentity.IdentityJson,source,current,!valid||ownerActive&&!bound);});
        return ownership.TryWrite(target,new("Battle",binding.Id,null,binding.Selector.SlotKey,snapshot.Generations.Ui,target.ReadState()),output).Written;
    }
    public void Tick(){if(!gate.CanAccess||!ReconcileDisplay()||!Enabled)return;frame++;foreach(var pair in pending.ToArray()){var p=pair.Value;double age=(System.Diagnostics.Stopwatch.GetTimestamp()-p.Started)/(double)System.Diagnostics.Stopwatch.Frequency;
        if(!Alive(p.Owner)||p.Owner.GetInstanceID()!=p.Instance||age>3||frame-p.Frame>120){pending.Remove(pair.Key);continue;}if(frame-p.Frame is 1 or 3 or 10 or 60)try{Apply(p);}catch{log.Record("BATTLE_CALLBACK_ISOLATED");}}
        foreach(var pair in readyCards.ToArray()){var card=pair.Value;double age=(System.Diagnostics.Stopwatch.GetTimestamp()-card.Started)/(double)System.Diagnostics.Stopwatch.Frequency;
            if(!Alive(card.Cell)||card.Cell.GetInstanceID()!=card.Instance||age>3||frame-card.Frame>120){readyCards.Remove(pair.Key);continue;}
            if(frame-card.Frame is 1 or 3 or 10 or 60)try{if(TryApplyCard(card))readyCards.Remove(pair.Key);}catch{readyCards.Remove(pair.Key);log.Record("BATTLE_COMPLETED_CARD_ISOLATED");}}}
    public void Selected(){pending.Clear();readyCards.Clear();lifetimes.Clear();}
    public bool ReconcileDisplay(){if(!gate.CanAccess)return false;if(!Enabled)Selected();return true;}
    public bool RestoreOwned()=>true; // Text restoration is owned by the shared UiOwnershipService.
    public void Dispose(){Selected();}
}
