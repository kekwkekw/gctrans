#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    sealed record ShopHeaderGeometryIdentity(long Page, int PageInstance, long Header, int HeaderInstance,
        long Text, int TextInstance, long Frame, int FrameInstance, long Container, int ContainerInstance, long Lifetime);
    readonly HashSet<string> shopHeaderGeometryReports = new(StringComparer.Ordinal);

    static RectTransform? ShopHeaderContainer(TextMeshProUGUI text)
    {
        if (!Alive(text) || !Exact(text,"TMPro.TextMeshProUGUI")) return null;
        var header=Parent<Gc.HeaderTopController>(text);
        if (!Alive(header) || !Exact(header!,"Gc.HeaderTopController") || Ptr(header!._subSceneTitleText)!=Ptr(text)) return null;
        var root=header._frameUI;var parent=text.transform.parent;
        var container=Alive(parent)?parent!.GetComponent<RectTransform>():null;
        if (!Alive(root)||!root!.transform.IsChildOf(header.transform)||!Alive(container)||
            !container!.IsChildOf(root.transform)||Ptr(container)==Ptr(text.rectTransform))
            return null;
        return container;
    }
    static bool ShopHeaderContainerReady(RectTransform? container)=>Alive(container)&&
        float.IsFinite(container!.rect.width)&&float.IsFinite(container.rect.height)&&container.rect.width>0&&container.rect.height>0;

    bool TryShopHeaderGeometry(Component value,UiBinding binding,TextMeshProUGUI text,
        out ShopHeaderGeometryIdentity? identity,out RectTransform? container,bool diagnose=false,long ownerGeneration=0)
    {
        gate.Require();identity=null;container=null;
        if (!ShopHeaderGeometryPolicy.Supports(binding)||value is not Gc.Shop.ShopTopController page||!Alive(page)||!Alive(text)) return false;
        bool current=shopHeaders.TryGetValue(Ptr(page),out var receipt)&&ShopHeaderSourceCurrent(receipt!)&&
            page.gameObject.activeInHierarchy&&ShopSceneCurrent(page,receipt!.Header)&&Ptr(receipt.Text)==Ptr(text)&&
            Ptr(Parent<Gc.HeaderTopController>(text))==Ptr(receipt.Header);
        bool selected=false;
        if(current)
        {
            var controller=page.Controller;var scenes=Alive(controller)?controller!.createdScene:null;
            selected=scenes!=null&&scenes.Length<=32&&ShopHeaderCurrentTabPolicy.SelectedPage(Ptr(page),controller!.currentSubSceneIndex,scenes.Select(x=>Ptr(x)).ToArray());
        }
        var actual=ShopHeaderContainer(text);
        uint proof=(current?1u:0)|(selected?2u:0)|(Alive(actual)?4u:0)|
            (text.GetComponents<TextMeshProUGUI>().Length==1?8u:0);
        if(diagnose)RecordShopHeaderProbe(binding,text,actual,proof,ownerGeneration);
        if(proof!=15)return false;
        container=actual;
        var header=receipt!.Header;var root=header._frameUI;
        identity=new(Ptr(page),page.GetInstanceID(),Ptr(header),header.GetInstanceID(),Ptr(text),text.GetInstanceID(),
            Ptr(root),root.GetInstanceID(),Ptr(actual),actual!.GetInstanceID(),receipt.Lifetime);
        return true;
    }
    bool CurrentShopHeaderGeometry(GeometryTicket ticket)
    {
        gate.Require();
        if(ticket.ShopHeader==null||!ticket.OwnerCurrent()||!Alive(ticket.Text))return false;
        if(!shopHeaders.TryGetValue(ticket.ShopHeader.Page,out var receipt)||!Alive(receipt.Page))return false;
        return TryShopHeaderGeometry(receipt.Page,ticket.Binding,ticket.Text,out var current,out var container)&&
            current==ticket.ShopHeader&&Ptr(container)==Ptr(ticket.ShopHeaderContainer);
    }
    Func<bool> WithShopHeaderSourceGuard(GeometryTicket? ticket,Func<bool> current)=>
        ticket is {ShopHeader:not null} guarded?()=>current()&&CurrentShopHeaderGeometry(guarded):current;

    static bool ShopHeaderPlainMaterial(TextMeshProUGUI text,TMP_CharacterInfo character)
    {
        var material=FooterMaterial(text,character);
        if(!Alive(material)||!Alive(material!.shader)||(character.style&~FontStyles.Bold)!=0)return false;
        var keywords=material.shaderKeywords;if(keywords==null||keywords.Length>32)return false;
        var words=new string[keywords.Length];for(int i=0;i<words.Length;i++)words[i]=keywords[i];
        return ShopHeaderGeometryPolicy.SupportedQuadMaterial(material.shader.name,words);
    }
    void RecordShopHeaderGeometry(GeometryTicket ticket)
    {
        gate.Require();
        if(ticket.ShopHeader!=null&&CurrentShopHeaderGeometry(ticket))
            RecordShopHeaderProbe(ticket.Binding,ticket.Text,ticket.ShopHeaderContainer,15,ticket.OwnerGeneration,true);
    }
    void RecordShopHeaderProbe(UiBinding binding,TextMeshProUGUI text,RectTransform? container,uint proof,long ownerGeneration,bool written=false)
    {
        gate.Require();
        string id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string key=$"SHGEO_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_P{proof:X}_T{(written?1:0)}";
        if(shopHeaderGeometryReports.Count>=128||!shopHeaderGeometryReports.Add(key))return;
        static int M(float n)=>float.IsFinite(n)?(int)Math.Clamp(n*100,-99999,99999):0;
        var info=text.textInfo;var chars=info?.characterInfo==null?Array.Empty<TMP_CharacterInfo>():
            info.characterInfo.Take(Math.Clamp(info.characterCount,0,Math.Min(4096,info.characterInfo.Length))).Where(c=>c.isVisible).ToArray();
        bool leaf=chars.Length>0&&chars.All(c=>GlyphCorners(c).All(q=>Within(q,text.rectTransform.rect)));
        bool composite=Alive(container)&&chars.Length>0&&chars.All(c=>GlyphCorners(c).All(q=>Within(container!.InverseTransformPoint(text.transform.TransformPoint(q)),container.rect)));
        bool materials=chars.Length>0&&chars.All(c=>ShopHeaderPlainMaterial(text,c));
        log.Record(key+$"_L{(leaf?1:0)}_C{(composite?1:0)}_M{(materials?1:0)}_V{chars.Length}");
        log.Record($"SHGEOB_{id}_W{M(text.rectTransform.rect.width)}_H{M(text.rectTransform.rect.height)}_CW{(Alive(container)?M(container!.rect.width):0)}_CH{(Alive(container)?M(container!.rect.height):0)}");
        // One bounded record per actual channel/keyword and JP-or-written phase.
        // These identify the real renderer contract without logging paths, addresses,
        // text, or assuming which keyword caused a previous unknown-material hold.
        static string Code(string value,int limit)=>new(value.Take(limit).Select(c=>c is >= 'a' and <= 'z'?(char)(c-'a'+'A'):
            c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'?c:'_').ToArray());
        foreach(var representative in chars.Where(c=>c.materialReferenceIndex is >=0 and <=8).GroupBy(c=>c.materialReferenceIndex).Take(9).Select(g=>g.First()))
        {
            int channel=representative.materialReferenceIndex;var actual=FooterMaterial(text,representative);
            string shader=Alive(actual)&&Alive(actual!.shader)?actual.shader.name:"UNAVAILABLE";
            int shaderCode=shader switch{"TextMeshPro/Mobile/Distance Field"=>1,"TextMeshPro/Distance Field"=>2,_=>0};
            log.Record($"SHGEOS_{id}_R{channel}_T{(written?1:0)}_S{shaderCode}_{Code(shader,48)}");
            var keys=Alive(actual)?actual!.shaderKeywords:null;int count=keys?.Length??-1;
            log.Record($"SHGEOK_{id}_R{channel}_T{(written?1:0)}_N{Math.Clamp(count,-1,99)}");
            if(keys!=null&&keys.Length<=32)for(int index=0;index<keys.Length;index++)
                log.Record($"SHGEOK_{id}_R{channel}_T{(written?1:0)}_I{index}_{Code(keys[index],40)}");
        }
        // Full rendered quads, channel material and metric ink are reported separately;
        // only the complete quads are used for Shop eligibility and later retention.
        if(chars.Length>0)
        {
            var c=chars[0];var quad=GlyphCorners(c).ToArray();var glyph=c.textElement?.glyph;var material=FooterMaterial(text,c);
            int flags=0;var words=Alive(material)?material!.shaderKeywords:null;
            if(words!=null&&words.Length<=32)for(int i=0;i<words.Length;i++)flags|=words[i] switch{"UNDERLAY_ON"=>1,"UNDERLAY_INNER"=>2,"GLOW_ON"=>4,"BEVEL_ON"=>8,"UNITY_UI_CLIP_RECT"=>16,"UNITY_UI_ALPHACLIP"=>32,"OUTLINE_ON"=>128,_=>64};
            log.Record($"SHGEOM_{id}_R{c.materialReferenceIndex}_K{flags:X}_QY{M(quad.Min(q=>q.y))}_{M(quad.Max(q=>q.y))}");
            if(glyph!=null)
            {
                var m=glyph.metrics;log.Record($"SHGEOI_{id}_S{M(c.scale)}_X{M(m.horizontalBearingX)}_Y{M(m.horizontalBearingY)}_W{M(m.width)}_H{M(m.height)}");
                // Metric ink excludes shader face/outline/shadow expansion. It is a
                // comparison measurement only; the full rendered quad remains required.
                float left=c.origin+m.horizontalBearingX*c.scale,top=c.baseLine+m.horizontalBearingY*c.scale;
                log.Record($"SHGEOIM_{id}_L{M(left)}_B{M(top-m.height*c.scale)}_R{M(left+m.width*c.scale)}_T{M(top)}");
            }
        }
    }
}
