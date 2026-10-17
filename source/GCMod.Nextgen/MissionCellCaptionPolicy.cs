#nullable enable
using System.Collections.Immutable;
namespace GCMod.Nextgen;
/// Recovered UI2 button captions repeat per typed mission cell, not once per popup.
public static class MissionCellCaptionPolicy
{
    static readonly string[] Receive={"Frame_Present","Button_Receive","image_Textbtn01_s","Group_Text","Text_Textbtn01"};
    static readonly string[] Challenge={"Frame_Present","Button_Challenge","image_Textbtn01_s","Group_Text","Text_Textbtn01"};
    public static ImmutableArray<string> CellPath(UiBinding b)
    {
        if (!b.Enabled || b.AdapterId!="core.static-caption.v1" || b.Producer!=null || b.SourceIdentity.Kind!="static-asset" ||
            b.Selector.ScopeKey!="gc.mission.missionpopup" || b.Selector.Type!="static" || b.Selector.Lifetime!="owner") return ImmutableArray<string>.Empty;
        var tail=b.Selector.Segments.SkipWhile(s=>s!="MissionCell(Clone)").Skip(1).ToImmutableArray();
        bool receive=b.Selector.SlotKey=="ui2-b04" && b.SourceIdentity.ExactJp=="受け取り" && tail.SequenceEqual(Receive,StringComparer.Ordinal);
        bool challenge=b.Selector.SlotKey=="ui2-b03" && b.SourceIdentity.ExactJp=="挑戦" && tail.SequenceEqual(Challenge,StringComparer.Ordinal);
        return receive||challenge ? tail : ImmutableArray<string>.Empty;
    }
    public static bool Supports(UiBinding b)=>!CellPath(b).IsEmpty;
}
