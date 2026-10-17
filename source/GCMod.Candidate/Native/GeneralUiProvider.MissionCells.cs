#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using Il2CppInterop.Runtime;
namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    sealed record MissionCellCaptionIdentity(long Cell,int CellInstance,long Model,long View,long ViewModel,long Control,int TargetInstance);
    bool TryMissionCellCaption(Owner owner,UiBinding b,TextMeshProUGUI text,out MissionCellCaptionIdentity? identity)
    {
        gate.Require();identity=null;
        if(!Current(owner)||!MissionCellCaptionPolicy.Supports(b)||owner.Value is not Gc.Mission.MissionPopup popup||!Alive(text))return false;
        var cell=Parent<Gc.Mission.ContentScrollCell>(text);var scroll=popup._content;
        if(!Alive(cell)||!Exact(cell,"Gc.Mission.ContentScrollCell")||!Alive(scroll)||!cell!.active||!cell.gameObject.activeInHierarchy||
            Ptr(Parent<Gc.Mission.MissionPopup>(cell))!=Ptr(popup)||!cell.transform.IsChildOf(scroll.transform))return false;
        var data=scroll._data;var scroller=scroll._scroller;int index=cell.dataIndex;
        if(data==null||index<0||index>=data.Count||!Alive(scroller))return false;
        var model=cell._data;var view=cell._content;var content=model?._Content_k__BackingField;
        if(model==null||!Exact(model,"Gc.Mission.ContentScrollCellData")||Ptr(data[index])!=Ptr(model)||content==null||
            !Alive(view)||!view.transform.IsChildOf(cell.transform)||Ptr(view._data)!=Ptr(content))return false;
        var active=scroller!._activeCellViews;var all=active?.data;int count=active?.Count??-1;
        if(all==null||count<0||count>32||count>all.Length)return false;
        int membership=0;for(int i=0;i<count;i++)if(Alive(all[i])&&Ptr(all[i])==Ptr(cell))membership++;
        if(membership!=1)return false;
        Component? control=b.Selector.SlotKey=="ui2-b04"?view._receive:view._transition;
        var path=ExactPaths(cell.transform,MissionCellCaptionPolicy.CellPath(b));
        if(!Alive(control)||!control!.transform.IsChildOf(view.transform)||!text.transform.IsChildOf(control.transform)||
            path.Count!=1||Ptr(path[0])!=Ptr(text.transform)||text.GetComponents<TextMeshProUGUI>().Length!=1)return false;
        identity=new(Ptr(cell),cell.GetInstanceID(),Ptr(model),Ptr(view),Ptr(content),Ptr(control),text.GetInstanceID());return true;
    }
    List<Transform> MissionCellTargets(Owner owner,UiBinding b)
    {
        gate.Require();var found=new List<Transform>();
        if(!Current(owner)||owner.Value is not Gc.Mission.MissionPopup popup||!Alive(popup._content))return found;
        var cells=popup._content.GetComponentsInChildren<Gc.Mission.ContentScrollCell>(false);
        if(cells.Length>32)return found;
        foreach(var cell in cells) {
            if(!Alive(cell)||!Exact(cell,"Gc.Mission.ContentScrollCell"))continue;
            var path=ExactPaths(cell.transform,MissionCellCaptionPolicy.CellPath(b));if(path.Count!=1)continue;
            var texts=path[0].GetComponents<TextMeshProUGUI>();
            if(texts.Length==1&&TryMissionCellCaption(owner,b,texts[0],out _))found.Add(path[0]);
        }
        return found;
    }
    bool CurrentMissionCellCaption(Owner owner,UiBinding b,TextMeshProUGUI text,MissionCellCaptionIdentity captured)=>
        TryMissionCellCaption(owner,b,text,out var now)&&now==captured;
}
