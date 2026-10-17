#nullable enable
namespace GCMod.Nextgen;

/// The accepted Shop exchange help row is a display region, not a clipping leaf.
/// Native adapter must independently prove current typed view/model/tab/row.
/// No ink trimming, size change, ancestor search or text rewrite is authorized.
public static class ShopHelpGeometryPolicy
{
    static readonly string[] Path = { "Animation_Main", "Tab_Parent", "Content_Tab_Parent", "Tab_Child", "Content_Tab_Child_Exchange", "Tab_Child_Header", "Text_Message" };
    public static bool Supports(UiBinding b) => b.Enabled && b.AdapterId == "ui.residual-caption.v1"
        && b.SourceIdentity.Kind == "current-model"
        && b.SourceIdentity.ExactJp == "オーブ・下位命鏡・無名の命鏡を藝術家の命鏡に変換できます"
        && b.Selector.ScopeKey == "gc.shop.shoptopcontroller" && b.Selector.SlotKey == "exchange_conversion_help"
        && b.Selector.Type == "exchange" && b.Selector.Lifetime == "owner" && b.Producer == null
        && b.Selector.Segments.SequenceEqual(Path,StringComparer.Ordinal);

    // Full rendered triangles contain the supported shader effects. Nothing is
    // subtracted from the quads. Unknown/custom or shader-only clipping is held.
    public static bool SupportedFullQuadMaterial(string shader,IEnumerable<string> keywords) =>
        shader is "TextMeshPro/Mobile/Distance Field" or "TextMeshPro/Distance Field"
        && keywords.Take(33).ToArray() is var words && words.Length <= 32
        && words.All(k => k is "UNDERLAY_ON" or "UNDERLAY_INNER" or "GLOW_ON" or "BEVEL_ON" or "OUTLINE_ON");

    public static bool RegionEligible(bool currentExactRow,bool fullSequence,bool everyGlyph,
        bool insideDeclaredRow,bool masksContained,bool channelsVisible,bool supportedMaterial,bool controlsSeparate) =>
        currentExactRow && fullSequence && everyGlyph && insideDeclaredRow && masksContained &&
        channelsVisible && supportedMaterial && controlsSeparate;

    // Native adapter supplies each complete rendered quad already transformed
    // into the single declared row's coordinates. No metric/effect subtraction.
    public static bool FullQuadInside(float left,float bottom,float right,float top,IReadOnlyList<float> xy)
    {
        if(xy.Count!=8||new[]{left,bottom,right,top}.Any(n=>!float.IsFinite(n))||right<=left||top<=bottom)return false;
        for(int i=0;i<8;i+=2)
            if(!float.IsFinite(xy[i])||!float.IsFinite(xy[i+1])||xy[i]<left-.05f||xy[i]>right+.05f||xy[i+1]<bottom-.05f||xy[i+1]>top+.05f)return false;
        return true;
    }
}
