#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record ShopHelpIdentity(long Page,int PageInstance,long View,int ViewInstance,long Model,
        long Exchange,int ExchangeInstance,long ExchangeModel,int Tab,long Region,int RegionInstance,
        long Target,int TargetInstance,long Lifetime);
    readonly HashSet<string> shopHelpReports = new(StringComparer.Ordinal);
    static string ShopHelpCode(string value,int max)=>new string(value.ToUpperInvariant().Select(c=>c is >= 'A' and <= 'Z' or >= '0' and <= '9'?c:'_').Take(max).ToArray());

    bool TryShopHelpGeometry(Component value,UiBinding binding,TextMeshProUGUI text,
        out ShopHelpIdentity? identity,out RectTransform? region)
    {
        gate.Require();identity=null;region=null;
        bool Reject(string reason)
        {
            string code="SHGI_01357E0A6AEC_"+reason;
            if(shopHelpReports.Count<128&&shopHelpReports.Add(code))log.Record(code);
            return false;
        }
        if(!ShopHelpGeometryPolicy.Supports(binding)||value is not Gc.Shop.ShopTopController page||!Alive(page)||!Alive(text)
            ||!Exact(page,"Gc.Shop.ShopTopController")||!page.gameObject.activeInHierarchy)return false;
        var view=page._view;var model=page._viewData;
        if(!Alive(view)||!Exact(view!,"Gc.Shop.ShopTopView")||Ptr(model)==0||Ptr(view!._viewData)!=Ptr(model)
            ||!view.transform.IsChildOf(page.transform)||!Alive(page.Controller))return Reject("VIEW_MODEL");
        var scenes=page.Controller.createdScene;
        if(scenes==null||scenes.Length>32||!ShopHeaderCurrentTabPolicy.SelectedPage(Ptr(page),page.Controller.currentSubSceneIndex,scenes.Select(s=>Ptr(s)).ToArray()))return Reject("SELECTED_PAGE");
        var exchange=view._exchangeShopView;int tab=model!._CurrentTabIndex_k__BackingField;var tabs=model._ShopDataList_k__BackingField;
        if(!Alive(exchange)||!Exact(exchange!,"Gc.Shop.ExchangeShopView")||!exchange!.gameObject.activeInHierarchy
            ||!exchange.transform.IsChildOf(view.transform)||Ptr(exchange._viewData)==0||tabs==null||tabs.Count>32||tab<0||tab>=tabs.Count
            ||Ptr(tabs[tab])!=Ptr(exchange._viewData)||!text.transform.IsChildOf(exchange.transform))return Reject("EXCHANGE_TAB_MODEL");
        var leaves=ExactPaths(page.transform,binding.Selector.Segments);
        if(leaves.Count!=1||Ptr(leaves[0])!=Ptr(text.transform)||text.GetComponents<TextMeshProUGUI>().Length!=1)return Reject("EXACT_TARGET_PATH");
        // The literal's own declared header row, at exactly one parent. Never walk
        // to successively larger ancestors to find a rectangle that happens to fit.
        var parent=text.transform.parent;var row=Alive(parent)?parent!.GetComponent<RectTransform>():null;
        if(!Alive(row)||row!.name!="Tab_Child_Header"||!row.IsChildOf(exchange.transform)||Ptr(row)==Ptr(text.rectTransform)
            ||!float.IsFinite(row.rect.width)||!float.IsFinite(row.rect.height)||row.rect.width<=0||row.rect.height<=0)return Reject("OWNED_HEADER_ROW");
        if(!owners.TryGetValue(Ptr(page),out var owner)||!Current(owner))return Reject("OWNER_LIFETIME");
        region=row;
        identity=new(Ptr(page),page.GetInstanceID(),Ptr(view),view.GetInstanceID(),Ptr(model),Ptr(exchange),exchange.GetInstanceID(),
            Ptr(exchange._viewData),tab,Ptr(row),row.GetInstanceID(),Ptr(text),text.GetInstanceID(),owner.Lifetime);
        return true;
    }
    bool CurrentShopHelpGeometry(GeometryTicket ticket)
    {
        gate.Require();if(ticket.ShopHelp==null||!ticket.OwnerCurrent()||!Alive(ticket.Text))return false;
        var page=Parent<Gc.Shop.ShopTopController>(ticket.Text);
        return Alive(page)&&TryShopHelpGeometry(page!,ticket.Binding,ticket.Text,out var current,out var row)
            && current==ticket.ShopHelp&&Ptr(row)==Ptr(ticket.ShopHelpRegion);
    }
    Func<bool> WithShopHelpSourceGuard(GeometryTicket? ticket,Func<bool> current)=>
        ticket is {ShopHelp:not null} guarded?()=>current()&&CurrentShopHelpGeometry(guarded):current;

    static bool ShopHelpPlainMaterial(TextMeshProUGUI text,TMP_CharacterInfo c)
    {
        var material=FooterMaterial(text,c); // Resolves the actual channel; no Footer ink policy.
        if(!Alive(material)||!Alive(material!.shader)||(c.style&~FontStyles.Bold)!=0)return false;
        var keys=material.shaderKeywords;if(keys==null||keys.Length>32)return false;
        var words=new string[keys.Length];for(int i=0;i<keys.Length;i++)words[i]=keys[i];
        if(!ShopHelpGeometryPolicy.SupportedFullQuadMaterial(material.shader.name,words))return false;
        // No unproved stencil/custom shader clip. RectMask2D geometry is checked
        // below as well; a shader-only clip keyword is deliberately unavailable.
        if(material.HasProperty("_Stencil")&&material.GetFloat("_Stencil")!=0)return false;
        if(material.HasProperty("_StencilComp")&&material.GetFloat("_StencilComp")!=8)return false;
        return !material.HasProperty("_UseUIAlphaClip")||material.GetFloat("_UseUIAlphaClip")==0;
    }
    bool ShopHelpRegionProof(TextMeshProUGUI text,RectTransform row,UiBinding binding,long ownerGeneration,bool written)
    {
        gate.Require();var info=text.textInfo;
        if(info?.characterInfo==null||info.characterCount<=0||info.characterCount>4096||info.characterCount>info.characterInfo.Length)return false;
        var all=info.characterInfo.Take(info.characterCount).ToArray();var chars=all.Where(c=>c.isVisible).ToArray();
        bool full=RenderedGeometryText(text)==MeshSource(text,text.text)&&!text.m_havePropertiesChanged&&!text.m_isLayoutDirty
            &&!text.isTextTruncated&&text.firstOverflowCharacterIndex<0;
        bool every=chars.Length>0&&all.All(c=>c.isVisible||c.character<=char.MaxValue&&char.IsWhiteSpace((char)c.character));
        bool inside=chars.Length>0&&chars.All(c=>ShopHelpGeometryPolicy.FullQuadInside(row.rect.xMin,row.rect.yMin,row.rect.xMax,row.rect.yMax,
            GlyphCorners(c).Select(q=>row.InverseTransformPoint(text.transform.TransformPoint(q))).SelectMany(q=>new[]{q.x,q.y}).ToArray()));
        bool masks=true;int rectMasks=0,stencils=0,ancestors=0;
        for(var n=text.transform;Alive(n);n=n.parent)
        {
            if(++ancestors>32){masks=false;break;}
            var mask=n.GetComponent<RectMask2D>();var rect=n.GetComponent<RectTransform>();var stencil=n.GetComponent<Mask>();
            if(Alive(stencil)&&stencil.enabled){stencils++;masks=false;}
            if(!Alive(mask)||!mask.enabled)continue;rectMasks++;
            if(!Alive(rect)||chars.Any(c=>GlyphCorners(c).Any(q=>!Within(rect!.InverseTransformPoint(text.transform.TransformPoint(q)),rect.rect))))masks=false;
        }
        bool channels=chars.Length>0&&chars.All(c=>GlyphChannelVisible(text,c));
        bool material=chars.Length>0&&chars.All(c=>ShopHelpPlainMaterial(text,c));
        var controls=row.GetComponentsInChildren<Selectable>(true);bool separate=controls.Length<=16;
        if(separate)foreach(var control in controls)
        {
            if(!Alive(control)||!control.gameObject.activeInHierarchy)continue;
            var cr=control.GetComponent<RectTransform>();if(!Alive(cr)){separate=false;break;}
            foreach(var c in chars)
            {
                var qs=GlyphCorners(c).Select(q=>cr.InverseTransformPoint(text.transform.TransformPoint(q))).ToArray();
                var box=Rect.MinMaxRect(qs.Min(q=>q.x),qs.Min(q=>q.y),qs.Max(q=>q.x),qs.Max(q=>q.y));
                if(cr.rect.Overlaps(box)){separate=false;break;}
            }
        }
        string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string key=$"SGHELP_{id}_O{ownerGeneration}_T{(written?1:0)}_F{(full?1:0)}_E{(every?1:0)}_R{(inside?1:0)}_M{(masks?1:0)}_C{(channels?1:0)}_S{(material?1:0)}_B{(separate?1:0)}";
        if(shopHelpReports.Count<128&&shopHelpReports.Add(key))
        {
            log.Record(key);
            static int N(float x)=>float.IsFinite(x)?(int)Math.Clamp(x*100,-99999,99999):0;
            log.Record($"SGHB_{id}_W{N(text.rectTransform.rect.width)}_H{N(text.rectTransform.rect.height)}_RW{N(row.rect.width)}_RH{N(row.rect.height)}");
            var qs=chars.SelectMany(c=>GlyphCorners(c).Select(q=>row.InverseTransformPoint(text.transform.TransformPoint(q)))).ToArray();
            if(qs.Length>0)log.Record($"SGHQ_{id}_L{N(qs.Min(q=>q.x))}_R{N(qs.Max(q=>q.x))}_B{N(qs.Min(q=>q.y))}_T{N(qs.Max(q=>q.y))}");
            log.Record($"SGHC_{id}_V{chars.Length}_RM{rectMasks}_SM{stencils}_BT{controls.Length}");
            foreach(var c in chars.GroupBy(c=>c.materialReferenceIndex).Select(g=>g.First()))
            {
                var mat=FooterMaterial(text,c);var shader=Alive(mat)&&Alive(mat!.shader)?mat.shader.name:"UNAVAILABLE";
                log.Record($"SGHM_{id}_I{c.materialReferenceIndex}_{ShopHelpCode(shader,45)}");
                var keys=Alive(mat)?mat!.shaderKeywords:null;if(keys!=null&&keys.Length<=32)foreach(var k in keys)log.Record($"SGHK_{id}_I{c.materialReferenceIndex}_{ShopHelpCode(k,45)}");
            }
        }
        return ShopHelpGeometryPolicy.RegionEligible(true,full,every,inside,masks,channels,material,separate);
    }
}
