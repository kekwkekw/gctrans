#nullable enable
using GCMod.Nextgen;
using UnityEngine;

namespace GCMod.Candidate.Native;

internal sealed partial class BattleNativeProvider
{
    sealed record BattleRaceHeadingAnchor(long Race, int RaceInstance, long Value, int ValueInstance, long Group);

    Transform? CaptionChild(Pending p, BattleRoute route, out BattleRaceHeadingAnchor? anchor, out string reason)
    {
        anchor=null;reason="TARGET_ACCESS_CLOSED";
        if(!Enabled||!Alive(p.Owner)||p.Owner.GetInstanceID()!=p.Instance||p.Model()!=p.InitialModel)return null;
        // This recovered literal heading belongs to the current typed _race, not another
        // identically named GroupKind belonging to the same cell's other layout.
        bool raceHeading=p.Owner is Gc.Battle.UI.Detail.EnemyScrollCell&&
            route.Segments.SequenceEqual(new[]{"GroupParameter","GroupKind","TextMain"},StringComparer.Ordinal);
        if(!raceHeading)
        {
            var ordinary=ExactChild(p.Owner.transform,route.Segments,p.Surface=="B05",out int depth,out int count);
            reason=$"PATH_D{depth}_C{count}";return ordinary;
        }
        var cell=(Gc.Battle.UI.Detail.EnemyScrollCell)p.Owner;var race=cell._race;var value=race?._type;
        if(!Alive(race)||UnityNative.Class(race)!="Gc.RaceTypeView") {reason="RACE_FIELD_NOT_CURRENT";return null;}
        if(!Alive(value)||UnityNative.Class(value)!="TMPro.TextMeshProUGUI"||!value!.transform.IsChildOf(race!.transform))
        {reason="RACE_VALUE_NOT_CURRENT";return null;}
        var parent=ExactChild(cell.transform,new[]{"GroupParameter"},false,out int pathDepth,out int pathCount);
        if(!Alive(parent)){reason=$"PATH_D{pathDepth}_C{pathCount}";return null;}
        if(parent!.childCount>256){reason="RACE_GROUP_BOUND";return null;}
        var candidates=new List<Transform>();for(int n=0;n<parent.childCount;n++){var child=parent.GetChild(n);if(child.name=="GroupKind")candidates.Add(child);}
        var ancestors=new List<long>();var current=race!.transform;
        for(int depth=0;Alive(current)&&depth<16&&Ptr(current)!=Ptr(cell.transform);depth++,current=current.parent)ancestors.Add(Ptr(current));
        if(!Alive(current)||Ptr(current)!=Ptr(cell.transform)){reason="RACE_OWNER_ANCESTRY_UNPROVEN";return null;}
        long group=BattleCaptionTargetPolicy.UniqueTypedAncestor(candidates.Select(x=>Ptr(x)).ToArray(),ancestors);
        if(group==0){reason="RACE_GROUP_NOT_UNIQUE";return null;}
        var selectedGroup=candidates.Single(x=>Ptr(x)==group);
        var heading=ExactChild(selectedGroup,new[]{"TextMain"},false,out pathDepth,out pathCount);
        if(!Alive(heading)){reason=$"RACE_HEADING_D{pathDepth}_C{pathCount}";return null;}
        if(Ptr(heading)==Ptr(value!.transform)){reason="RACE_HEADING_IS_VALUE";return null;}
        anchor=new(Ptr(race),race!.GetInstanceID(),Ptr(value),value!.GetInstanceID(),group);
        reason="TYPED_RACE_ANCHOR_READY";return heading;
    }
}
