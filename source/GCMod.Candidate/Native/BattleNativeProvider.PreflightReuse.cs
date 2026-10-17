#nullable enable
using GCMod.Nextgen;
using UnityEngine;
using CD=Gc.Battle.UI.Detail.UnitInfoScrollCellData;
namespace GCMod.Candidate.Native;
internal sealed partial class BattleNativeProvider
{
    sealed class PreflightOwner(Gc.Battle.UI.Detail.SharedEnemyInfoView view,int instance,int scene)
    {
        public readonly Gc.Battle.UI.Detail.SharedEnemyInfoView View=view;
        public readonly int Instance=instance,Scene=scene;
        public string Observed="",Applied="";public int Stable;public bool WasActive;
    }
    readonly Dictionary<long,PreflightOwner> preflightOwners=new();
    readonly HashSet<string> preflightReports=new(StringComparer.Ordinal);
    void PreflightReport(PreflightOwner p,string reason)
    {
        gate.Require();string key=$"BATTLE_PREFLIGHT_I{p.Instance}_{reason}";
        if(preflightReports.Count<96&&preflightReports.Add(key))log.Record(key);
    }
    bool PreflightCurrent(PreflightOwner p)
    {
        gate.Require();var v=p.View;
        if(!Enabled||!Alive(v)||v.GetInstanceID()!=p.Instance||v.gameObject.scene.handle!=p.Scene)return false;
        int mask=(v.gameObject.activeInHierarchy?1:0)|(v._unit!=null?2:0)|(Alive(v._scrollView)?4:0)|(Alive(v._scrollView)&&v._scrollView.transform.IsChildOf(v.transform)?8:0);
        if(mask!=15){PreflightReport(p,$"G{mask}");return false;}
        int depth=0;for(var t=v.transform;Alive(t)&&depth++<16;t=t.parent)
        {var q=t.GetComponent<Gc.Quest.QuestEnemyInfoView>();if(Alive(q)){int join=(Ptr(q._sharedView)==Ptr(v)?1:0)|(Ptr(q._unit)==Ptr(v._unit)?2:0);PreflightReport(p,$"J{join}");return join==3;}}
        PreflightReport(p,"PARENT_MISSING");return false;
    }
    void WatchPreflight(Gc.Battle.UI.Detail.SharedEnemyInfoView view)
    {
        gate.Require();if(!Enabled||!Alive(view)||Surface(view)!="B01")return;
        long key=Ptr(view);if(preflightOwners.Count>=8&&!preflightOwners.ContainsKey(key))return;
        if(!preflightOwners.TryGetValue(key,out var old)||old.Instance!=view.GetInstanceID())
            preflightOwners[key]=new(view,view.GetInstanceID(),view.gameObject.scene.handle);
        else {old.Observed="";old.Applied="";old.Stable=0;}
    }
    bool CurrentPreflightCell(PreflightOwner owner,Gc.Battle.UI.Detail.UnitInfoScrollCell cell,CD data)
    {
        gate.Require();if(!PreflightCurrent(owner)||!Alive(cell)||!cell.active||!cell.gameObject.activeInHierarchy)return false;
        var view=owner.View._scrollView;var list=view._data;int index=cell.dataIndex;
        if(list==null||list.Count>128||index<0||index>=list.Count)return false;
        int join=(Ptr(list[index])==Ptr(data)?1:0)|(CurrentPreflightPrefab(owner,cell,data)?2:0)|(Ptr(data.Unit)==Ptr(owner.View._unit)?4:0)|(cell.transform.IsChildOf(view.transform)?8:0);
        if(join!=15){PreflightReport(owner,$"CELL_D{index}_J{join}_V{(data.CellView!=null?1:0)}_U{(data.Unit!=null?1:0)}");return false;}
        var active=view._scroller?._activeCellViews;var array=active?.data;int count=active?.Count??-1;
        if(array==null||count<0||count>32||count>array.Length)return false;
        int matches=0;for(int n=0;n<count;n++)if(Alive(array[n])&&Ptr(array[n])==Ptr(cell))matches++;
        return matches==1;
    }
    bool CurrentPreflightPrefab(PreflightOwner owner,Gc.Battle.UI.Detail.UnitInfoScrollCell cell,CD data)
    {
        gate.Require();
        // CellView is the current data's declared template, not the instantiated cell.
        // Join its exact typed producer field separately from active physical membership.
        var view=owner.View;var type=UnityNative.Class(cell);
        var prefab=type switch{"Gc.Battle.UI.Detail.EnemyScrollCell"=>view._enemyPrefab,"Gc.Battle.UI.Detail.ResistScrollCell"=>view._resistPrefab,"Gc.Battle.UI.Detail.ResistTextScrollCell"=>view._resistTextPrefab,_=>null};
        bool match=Alive(prefab)&&Ptr(data.CellView)==Ptr(prefab)&&UnityNative.Class(prefab!)==type;
        if(match)PreflightReport(owner,"CURRENT_DECLARED_TEMPLATE_JOIN");
        return match;
    }
    void TickPreflightReuse()
    {
        gate.Require();
        foreach(var pair in preflightOwners.ToArray())
        {
            var p=pair.Value;var view=p.View;
            if(!Alive(view)||view.GetInstanceID()!=p.Instance||view.gameObject.scene.handle!=p.Scene){preflightOwners.Remove(pair.Key);continue;}
            if(!PreflightCurrent(p)){p.WasActive=false;p.Stable=0;p.Observed="";continue;}
            string model=Model(view._unit);
            if(!p.WasActive||p.Observed!=model){p.WasActive=true;p.Observed=model;p.Stable=0;p.Applied="";continue;}
            if(p.Applied==model||p.Stable>=90)continue;
            p.Stable++;
            if(p.Stable is not (2 or 3 or 8 or 20 or 60 or 90))continue;
            var scroll=view._scrollView;var list=scroll._data;var active=scroll._scroller?._activeCellViews;var array=active?.data;int count=active?.Count??-1;
            if(list==null||list.Count>128||array==null||count<0||count>32||count>array.Length){PreflightReport(p,"REGISTRY_NOT_READY");continue;}
            int queued=0;
            for(int n=0;n<count;n++)
            {
                var cell=array[n]?.TryCast<Gc.Battle.UI.Detail.UnitInfoScrollCell>();if(!Alive(cell))continue;
                string type=UnityNative.Class(cell!);if(type is not ("Gc.Battle.UI.Detail.EnemyScrollCell" or "Gc.Battle.UI.Detail.ResistScrollCell" or "Gc.Battle.UI.Detail.ResistTextScrollCell"))continue;
                int index=cell!.dataIndex;if(index<0||index>=list.Count)continue;var data=list[index];
                if(data==null||!CurrentPreflightCell(p,cell,data))continue;
                // A fresh current data/active-cell join supplies readiness; no native Setup is replayed.
                Queue(cell,type+".Setup","current_active_cell_setup_argument","B01",
                    ()=>CurrentPreflightCell(p,cell,data)?System.Text.Json.JsonSerializer.Serialize(new{data=Ptr(data),unit=Model(data.Unit),cell=Ptr(data.CellView)}):"NOT_CURRENT_PREFLIGHT_CELL",cell);
                queued++;
            }
            if(queued>0){p.Applied=model;log.Record($"BATTLE_PREFLIGHT_CURRENT_CELL_REBIND_I{p.Instance}_C{queued}");}
            else PreflightReport(p,$"NO_JOIN_L{list.Count}_A{count}");
        }
    }
}
