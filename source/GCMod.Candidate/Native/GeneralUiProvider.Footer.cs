#nullable enable
using System.Text.RegularExpressions;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    readonly HashSet<string> bindingOutcomes = new(StringComparer.Ordinal);
    void BindingOutcome(Owner owner, UiBinding binding, string reason)
    {
        if (bindingOutcomes.Count >= 1024) return;
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string code = $"UIB_{id}_O{owner.Lifetime}_U{host.Snapshot.Generations.Ui}_{reason}";
        if (code.Length <= 96 && bindingOutcomes.Add(code)) log.Record(code);
    }
    RectTransform? FooterContainer(Component owner, UiBinding binding, TextMeshProUGUI text)
    {
        gate.Require();
        if (owner is not Gc.GlobalMenu.GlobalMenuFooterButtons || binding.AdapterId != "ui4.static-caption.v1" ||
            binding.Selector.ScopeKey != "gc.globalmenu.globalmenufooterbuttons" ||
            !CoreDirect(owner, text, binding.Selector.Segments)) return null;
        var button = Parent<InertButton>(text);
        if (!Alive(button) || Ptr(button!.textMeshProUgui) != Ptr(text) || !button.transform.IsChildOf(owner.transform)) return null;
        var rect = button.GetComponent<RectTransform>();
        return Alive(rect) && ContentRectContained(text, rect) ? rect : null;
    }
    bool CurrentFooterContainer(GeometryTicket ticket)
    {
        if (ticket.FooterOwner == null || ticket.FooterContainer == null || !Alive(ticket.FooterOwner) || !Alive(ticket.FooterContainer)) return false;
        var current = FooterContainer(ticket.FooterOwner, ticket.Binding, ticket.Text);
        return Alive(current) && current!.GetInstanceID() == ticket.FooterContainerInstance && Ptr(current) == Ptr(ticket.FooterContainer);
    }
    static Material? FooterMaterial(TextMeshProUGUI text, TMP_CharacterInfo c)
    {
        int index=c.materialReferenceIndex; CanvasRenderer? renderer=null;
        if(index==0) renderer=text.canvasRenderer;
        else if(index is >0 and <=8 && text.m_subTextObjects!=null && index<text.m_subTextObjects.Length)
        {
            var sub=text.m_subTextObjects[index];
            if(Alive(sub)&&Ptr(sub.m_TextComponent)==Ptr(text)&&Ptr(sub.fontAsset)==Ptr(c.fontAsset)) renderer=sub.m_CanvasRenderer;
        }
        return renderer!=null&&Alive(renderer)&&renderer.materialCount>0?renderer.GetMaterial(0):null;
    }
    static Vector3[] FooterVisibleCorners(TextMeshProUGUI text, TMP_CharacterInfo c) => FooterVisibleCorners(text,c,out _);
    static Vector3[] FooterVisibleCorners(TextMeshProUGUI text, TMP_CharacterInfo c, out int reason)
    {
        // TMP's SDF quad includes transparent atlas padding. Only this directly owned
        // footer composite uses current glyph metrics; unsupported effects keep the quad.
        var quad = GlyphCorners(c).ToArray();
        reason = 1;
        try
        {
            if ((c.style & ~FontStyles.Bold) != 0 || c.textElement?.glyph == null) return quad;
            reason = 2;
            int index = c.materialReferenceIndex; var mesh = text.textInfo?.meshInfo;
            if (index < 0 || index > 8 || mesh == null || index >= mesh.Length) return quad;
            var material = FooterMaterial(text,c);
            reason = 3;
            if (material==null || !Alive(material) || !Alive(material.shader) || !material.shader.name.Contains("Distance Field", StringComparison.Ordinal)) return quad;
            var keywords=material.shaderKeywords;
            if(keywords==null||keywords.Length>32) return quad;
            // Native Footer uses UNDERLAY_ON. Its shadow/atlas padding is not text
            // content overflow against the hit button. Full quads still undergo the
            // original active-mask and actual material-channel checks in GlyphFailure.
            // This does not remove/mutate underlay, cull, mask or any native geometry.
            for(int k=0;k<keywords.Length;k++)
                if(keywords[k] is "GLOW_ON") return quad;
            reason = 4;
            string weightProperty = (c.style & FontStyles.Bold) != 0 ? "_WeightBold" : "_WeightNormal";
            foreach (string p in new[] { "_GradientScale", "_ScaleRatioA", "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", weightProperty })
                if (!material.HasProperty(Shader.PropertyToID(p))) return quad;
            float Read(string p)=>material.GetFloat(Shader.PropertyToID(p));
            float gradient = Read("_GradientScale"), ratio = Read("_ScaleRatioA");
            float face = Read("_FaceDilate"), outline = Read("_OutlineWidth"),
                softness = Read("_OutlineSoftness"), weight = Read(weightProperty);
            reason = 5;
            if (new[] {gradient,ratio,face,outline,softness,weight}.Any(n=>!float.IsFinite(n)) || gradient <= 0 || gradient > 512 ||
                ratio <= 0 || ratio > 2 || Math.Abs(face)>1 || outline<0 || outline>1 || softness<0 || softness>1 || Math.Abs(weight)>1) return quad;
            float effect = (Math.Abs(face) + outline + softness + Math.Abs(weight)/4) * gradient * ratio * c.scale;
            var m = c.textElement.glyph.metrics;
            reason = 6;
            if (!UiGlyphInkBounds.TryGet(c.origin, c.baseLine, c.scale, m.horizontalBearingX, m.horizontalBearingY, m.width, m.height, effect,
                quad.Min(q=>q.x), quad.Min(q=>q.y), quad.Max(q=>q.x), quad.Max(q=>q.y), out var ink)) return quad;
            reason = 0;
            return new[] { new Vector3(ink.Left,ink.Bottom,0),new Vector3(ink.Left,ink.Top,0),
                new Vector3(ink.Right,ink.Top,0),new Vector3(ink.Right,ink.Bottom,0) };
        }
        catch (Exception e)
        {
            int kind=e switch { MissingMethodException=>1,TypeInitializationException=>2,NullReferenceException=>3,
                ArgumentException=>4,IndexOutOfRangeException=>5,Il2CppInterop.Runtime.Il2CppException=>6,_=>9 };
            reason = 100+reason*10+kind; return quad;
        }
    }
    bool FooterBaseline(Owner owner, UiBinding binding, TextMeshProUGUI text, RectTransform container)
    {
        var probe = new GeometryTicket { Text = text, Binding = binding, Policy = GeometryPolicy.FooterComposite,
            OwnerGeneration = owner.Lifetime, UiGeneration = host.Snapshot.Generations.Ui,
            PreparedFrame = frame, WrittenFrame = frame };
        var failure = GlyphFailure(text, probe, visibleContainer: container);
        var fields = failure == UiVisualFailure.None ? UiVisualField.None :
            failure == UiVisualFailure.MaskClipped ? UiVisualField.Mask :
            failure == UiVisualFailure.RenderChannelHidden ? UiVisualField.Channel : UiVisualField.GlyphRect;
        if (RenderedGeometryText(text) != MeshSource(text, text.text)) { failure |= UiVisualFailure.MeshStale; fields |= UiVisualField.MeshSequence; }
        if (text.m_havePropertiesChanged || text.m_isLayoutDirty) { failure |= UiVisualFailure.Dirty; fields |= UiVisualField.Dirty; }
        uint detail = (text.isTextTruncated ? 1u : 0u) | (text.firstOverflowCharacterIndex >= 0 ? 2u : 0u) |
            (text.m_havePropertiesChanged ? 4u : 0u) | (text.m_isLayoutDirty ? 8u : 0u);
        if (RecordGeometry(probe, UiVisualPhase.JpBaseline, failure, fields, detail))
        {
            string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            static int M(float n) => float.IsFinite(n) ? (int)Math.Clamp(n * 100, -99999, 99999) : 0;
            var info = text.textInfo;
            var visible = info?.characterInfo == null ? Array.Empty<TMP_CharacterInfo>() :
                info.characterInfo.Take(Math.Clamp(info.characterCount, 0, Math.Min(4096, info.characterInfo.Length))).Where(c => c.isVisible).ToArray();
            var points = visible.SelectMany(c => GlyphCorners(c)).ToArray();
            var local = points.Select(q => container.InverseTransformPoint(text.transform.TransformPoint(q))).ToArray();
            var ink = visible.SelectMany(c => FooterVisibleCorners(text,c)).Select(q => container.InverseTransformPoint(text.transform.TransformPoint(q))).ToArray();
            if (visible.Length > 0)
            {
                var c=visible[0];FooterVisibleCorners(text,c,out int reason);
                var m=c.textElement?.glyph?.metrics;
                var mat=FooterMaterial(text,c);
                string shader=mat!=null&&Alive(mat)&&Alive(mat.shader)?Regex.Replace(mat.shader.name.ToUpperInvariant(),"[^A-Z0-9_]","_"):"UNAVAILABLE";
                log.Record($"GEOFS_{id}_D{reason}_STYLE{(int)c.style}");
                log.Record($"GEOFS_{id}_SH{shader[..Math.Min(48,shader.Length)]}");
                if(mat!=null&&Alive(mat))
                {
                    int flags=0;var keywords=mat.shaderKeywords;
                    if(keywords!=null&&keywords.Length<=32)for(int k=0;k<keywords.Length;k++)flags|=keywords[k] switch{"UNDERLAY_ON"=>1,"UNDERLAY_INNER"=>2,"GLOW_ON"=>4,_=>0};
                    log.Record($"GEOFP_{id}_K{flags}");
                    foreach(var p in new[]{("_GradientScale","G"),("_ScaleRatioA","A"),("_FaceDilate","F"),("_OutlineWidth","O"),("_OutlineSoftness","S"),("_WeightNormal","N"),("_WeightBold","B")})
                        log.Record($"GEOFP_{id}_{p.Item2}{(mat.HasProperty(Shader.PropertyToID(p.Item1))?M(mat.GetFloat(Shader.PropertyToID(p.Item1))):-99999)}");
                }
                if(m!=null) log.Record($"GEOFM_{id}_O{M(c.origin)}_B{M(c.baseLine)}_S{M(c.scale)}_X{M(m.Value.horizontalBearingX)}_Y{M(m.Value.horizontalBearingY)}_W{M(m.Value.width)}_H{M(m.Value.height)}");
            }
            var leaf = text.rectTransform.rect; var button = container.rect;
            log.Record($"GEOFB_{id}_O{owner.Lifetime}_LEAF{(probe.FooterLeafFits?1:0)}_BUTTON{(probe.FooterCompositeFits?1:0)}_V{visible.Length}_R{probe.MaterialChannels:X}_Q{probe.HiddenChannels:X}");
            log.Record($"GEOFR_{id}_LX{M(leaf.xMin)}_{M(leaf.xMax)}_LY{M(leaf.yMin)}_{M(leaf.yMax)}");
            log.Record($"GEOFR_{id}_BX{M(button.xMin)}_{M(button.xMax)}_BY{M(button.yMin)}_{M(button.yMax)}");
            if (points.Length > 0) log.Record($"GEOFG_{id}_LX{M(points.Min(q=>q.x))}_{M(points.Max(q=>q.x))}_LY{M(points.Min(q=>q.y))}_{M(points.Max(q=>q.y))}");
            if (local.Length > 0) log.Record($"GEOFG_{id}_BX{M(local.Min(q=>q.x))}_{M(local.Max(q=>q.x))}_BY{M(local.Min(q=>q.y))}_{M(local.Max(q=>q.y))}");
            if (ink.Length > 0) log.Record($"GEOFI_{id}_BX{M(ink.Min(q=>q.x))}_{M(ink.Max(q=>q.x))}_BY{M(ink.Min(q=>q.y))}_{M(ink.Max(q=>q.y))}");
        }
        return failure == UiVisualFailure.None;
    }
}
