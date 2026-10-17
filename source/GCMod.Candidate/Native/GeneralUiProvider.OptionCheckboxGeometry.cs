#nullable enable
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;
public sealed partial class GeneralUiProvider
{
    readonly HashSet<string> optionCheckboxGeometryReports = new(StringComparer.Ordinal);
    sealed record OptionCheckboxGeometryIdentity(long Owner, int OwnerInstance, long Area, int AreaInstance,
        long Speed, int SpeedInstance, long Checkbox, int CheckboxInstance, long Graphics, int GraphicCount, int Member, OptionCheckboxCaptionRole Role,
        long AlternateText, int AlternateTextInstance, int ContainerInstance);
    readonly HashSet<string> optionCheckboxMembershipReports = new(StringComparer.Ordinal);
    bool TryOptionCheckboxGeometry(Component value, UiBinding binding, TextMeshProUGUI text,
        out OptionCheckboxGeometryIdentity? identity, out RectTransform? container, bool diagnose = false, long ownerGeneration = 0, bool requireContainment = true)
    {
        gate.Require(); identity = null; container = null;
        if (!OptionCheckboxGeometryPolicy.Supports(binding) || value is not Gc.Option.OptionPopup owner
            || !Alive(owner) || !Exact(owner, "Gc.Option.OptionPopup") || !Alive(text)
            || !text.transform.IsChildOf(owner.transform) || text.GetComponents<TextMeshProUGUI>().Length != 1) return false;
        var area = owner.soundAndTextOptionArea;
        var speed = Alive(area) ? area!.messageSpeedOption : null;
        var checkbox = Alive(speed) ? speed!.checkBox : null;
        bool typed = Alive(area) && Exact(area!, "Gc.Option.SoundAndTextOptionArea") && Alive(speed)
            && Exact(speed!, "Gc.Option.MessageSpeedOptionUI") && Alive(checkbox) && Exact(checkbox!, "UnityEngine.UI.InertCheckBox")
            && Ptr(Parent<Gc.Option.MessageSpeedOptionUI>(text)) == Ptr(speed)
            && Ptr(Parent<InertCheckBox>(text)) == Ptr(checkbox)
            && Ptr(text.transform.parent) == Ptr(checkbox!.transform)
            && checkbox.transform.IsChildOf(speed!.transform) && speed.transform.IsChildOf(area!.transform);
        if (!typed) return false;
        container = checkbox!.GetComponent<RectTransform>();
        var graphics = checkbox.targetGraphics;
        int count = graphics?.Length ?? 0;
        var currentGraphics = count is > 0 and <= 32 ? Enumerable.Range(0, count).Select(i => Ptr(graphics![i])).ToArray() : Array.Empty<long>();
        int member = OptionCheckboxGeometryPolicy.UniqueGraphicMember(currentGraphics, Ptr(text));
        var path = binding.Producer!.OptionArea!;
        var indexed = CoreProducerPolicy.ResolveAreaTarget(gate.CanAccess, area!.transform, path,
            t => t.childCount, (t, index) => t.GetChild(index), t => t.name);
        int sameNamed = 0; var controlTransform = checkbox.transform;
        if (controlTransform.childCount > 64) sameNamed = 65;
        else for (int i = 0; i < controlTransform.childCount; i++)
            if (controlTransform.GetChild(i).name == "Text_Color") sameNamed++;
        var observation = new OptionCheckboxCaptionObservation(Ptr(text.transform), Ptr(text.transform.parent), Ptr(controlTransform),
            Ptr(indexed), text.transform.name, sameNamed, text.GetComponents<TextMeshProUGUI>().Length);
        var role = count <= 32 ? OptionCheckboxGeometryPolicy.SelectRole(binding, currentGraphics, Ptr(text), observation)
            : OptionCheckboxCaptionRole.Unavailable;
        bool contained = Alive(container) && ContentRectContained(text, container!);
        if (diagnose)
        {
            string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            string code = $"OCGEO_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_N{Math.Clamp(count, 0, 99)}_M{(member >= 0 ? 1 : 0)}_C{(contained ? 1 : 0)}_R{(int)role}";
            if (optionCheckboxMembershipReports.Count < 256 && optionCheckboxMembershipReports.Add(code)) log.Record(code);
        }
        if (role == OptionCheckboxCaptionRole.Unavailable || !Alive(container)
            || requireContainment && role == OptionCheckboxCaptionRole.GraphicMember && !contained) return false;
        var alternate = checkbox.text;
        identity = new(Ptr(owner), owner.GetInstanceID(), Ptr(area), area!.GetInstanceID(), Ptr(speed), speed!.GetInstanceID(),
            Ptr(checkbox), checkbox.GetInstanceID(), Ptr(graphics), count, member, role,
            Ptr(alternate), Alive(alternate) ? alternate.GetInstanceID() : 0, container!.GetInstanceID());
        return true;
    }
    readonly HashSet<string> optionCheckboxBaselineReports = new(StringComparer.Ordinal);
    void RecordOptionCheckboxBaseline(UiBinding binding, long ownerGeneration, UiVisualFailure failure)
    {
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
        string key = $"OCBASE_{id}_O{ownerGeneration}_U{host.Snapshot.Generations.Ui}_M{(uint)failure:X}";
        if (optionCheckboxBaselineReports.Count < 256 && optionCheckboxBaselineReports.Add(key)) log.Record(key);
    }
    bool CurrentOptionCheckboxGeometry(GeometryTicket ticket)
    {
        gate.Require();
        if (ticket.OptionCheckbox == null || !ticket.OwnerCurrent() || !Alive(ticket.Text)) return false;
        var owner = Parent<Gc.Option.OptionPopup>(ticket.Text);
        return Alive(owner) && TryOptionCheckboxGeometry(owner!, ticket.Binding, ticket.Text, out var current, out var container, requireContainment: false)
            && current == ticket.OptionCheckbox && Ptr(container) == Ptr(ticket.OptionCheckboxContainer);
    }
    Func<bool> WithOptionCheckboxSourceGuard(GeometryTicket? ticket, Func<bool> current)
    {
        if (ticket is { OptionCheckbox: not null } guarded) return () => current() && CurrentOptionCheckboxGeometry(guarded);
        return current;
    }
    static bool OptionCheckboxPlainMaterial(TextMeshProUGUI text, TMP_CharacterInfo character)
    {
        var material = FooterMaterial(text, character);
        if (!Alive(material) || !Alive(material!.shader) || (character.style & ~FontStyles.Bold) != 0) return false;
        var keywords = material.shaderKeywords;
        if (keywords == null || keywords.Length > 32) return false;
        var words = new string[keywords.Length];
        for (int i = 0; i < words.Length; i++) words[i] = keywords[i];
        if (!OptionCheckboxGeometryPolicy.PlainSdfMaterial(material.shader.name, words)) return false;
        string weight = (character.style & FontStyles.Bold) != 0 ? "_WeightBold" : "_WeightNormal";
        foreach (string field in new[] { "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", weight })
        {
            int id = Shader.PropertyToID(field);
            if (!material.HasProperty(id)) return false;
            float n = material.GetFloat(id);
            if (!float.IsFinite(n) || n != 0) return false;
        }
        return true;
    }

    bool TryOptionCheckboxCaption(GeometryTicket ticket, out InertCheckBox? checkbox, out InertToggle? toggle, out uint proof)
    {
        gate.Require(); checkbox = null; toggle = null; proof = 0;
        if (ticket.Binding.AdapterId != "ui4.static-caption.v1" || ticket.Binding.Selector.ScopeKey != "gc.option.optionpopup"
            || ticket.Binding.Producer?.OptionArea?.Field != "soundAndTextOptionArea" || !ticket.OwnerCurrent()
            || !Alive(ticket.Text) || ticket.Text.GetInstanceID() != ticket.Instance || !ticket.SourceCurrent()) return false;
        var owner = Parent<Gc.Option.OptionPopup>(ticket.Text);
        var area = Alive(owner) ? owner!.soundAndTextOptionArea : null;
        var speed = Alive(area) ? area!.messageSpeedOption : null;
        var actualSpeed = Parent<Gc.Option.MessageSpeedOptionUI>(ticket.Text);
        // Prefer the current typed option field; the ancestor is only an independent relation proof.
        checkbox = Alive(speed) ? speed!.checkBox : null;
        var ancestorCheckbox = Parent<InertCheckBox>(ticket.Text);
        toggle = Parent<InertToggle>(ticket.Text);
        if (Alive(owner)) proof |= 1;
        if (Alive(area)) proof |= 2;
        if (Alive(area) && Exact(area, "Gc.Option.SoundAndTextOptionArea")) proof |= 4;
        if (Alive(speed)) proof |= 8;
        if (Alive(speed) && Ptr(speed) == Ptr(actualSpeed)) proof |= 16;
        if (Alive(speed) && Alive(area) && speed!.transform.IsChildOf(area!.transform)) proof |= 32;
        if (Alive(checkbox)) proof |= 64;
        if (Alive(checkbox) && Exact(checkbox, "UnityEngine.UI.InertCheckBox")) proof |= 128;
        if (Alive(checkbox) && Ptr(checkbox!.text) == Ptr(ticket.Text)) proof |= 256;
        if (Alive(checkbox) && Ptr(ticket.Text.transform.parent) == Ptr(checkbox!.transform)) proof |= 512;
        if (Alive(checkbox) && Alive(speed) && checkbox!.transform.IsChildOf(speed!.transform)) proof |= 1024;
        if (ticket.Text.GetComponents<TextMeshProUGUI>().Length == 1) proof |= 2048;
        if (Alive(toggle)) proof |= 4096;
        if (Alive(toggle) && Ptr(toggle!.text) == Ptr(ticket.Text)) proof |= 8192;
        if (Alive(toggle) && Ptr(ticket.Text.transform.parent) == Ptr(toggle!.transform)) proof |= 16384;
        if (Alive(checkbox) && Ptr(checkbox) == Ptr(ancestorCheckbox)) proof |= 32768;
        return (proof & 36863) == 36863;
    }
    void RecordOptionCheckboxGeometry(GeometryTicket ticket)
    {
        // A single current typed checkbox caption is sampled only at a real overflow decision.
        // These are measurements, not an ink-policy exemption or a native proof fixture.
        gate.Require();
        if (ticket.Binding.AdapterId != "ui4.static-caption.v1" || ticket.Binding.Selector.ScopeKey != "gc.option.optionpopup"
            || ticket.Binding.Producer?.OptionArea?.Field != "soundAndTextOptionArea" || !ticket.OwnerCurrent()
            || !Alive(ticket.Text) || ticket.Text.GetInstanceID() != ticket.Instance || !ticket.SourceCurrent()) return;
        bool typedControl = TryOptionCheckboxCaption(ticket, out var checkbox, out var toggle, out uint proof);
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ticket.Binding.Id)))[..12];
        string key = $"{id}_O{ticket.OwnerGeneration}_U{ticket.UiGeneration}";
        if (optionCheckboxGeometryReports.Count >= 256 || !optionCheckboxGeometryReports.Add(key)) return;
        var text = ticket.Text; var info = text.textInfo;
        if (info?.characterInfo == null || info.characterCount is <= 0 or > 64 || info.characterCount > info.characterInfo.Length) return;
        var chars = info.characterInfo.Take(info.characterCount).Where(c => c.isVisible).ToArray();
        if (chars.Length == 0) return;
        var quad = chars.SelectMany(c => GlyphCorners(c)).ToArray();
        var ink = new List<Vector3>(); int metricReason = 0, flags = 0;
        foreach (var c in chars)
        {
            ink.AddRange(FooterVisibleCorners(text, c, out int reason)); metricReason |= reason;
            var mat = FooterMaterial(text, c);
            if (!Alive(mat)) { flags |= 8; continue; }
            var keywords = mat!.shaderKeywords;
            if (keywords == null || keywords.Length > 32) { flags |= 8; continue; }
            foreach (var word in keywords) flags |= word switch { "UNDERLAY_ON" => 1, "UNDERLAY_INNER" => 2, "GLOW_ON" => 4,
                "OUTLINE_ON" or "UNITY_UI_CLIP_RECT" or "UNITY_UI_ALPHACLIP" => 0, _ => 8 };
        }
        var leaf = text.rectTransform.rect;
        bool leafQuad = quad.All(q => Within(q, leaf)), metricInk = metricReason == 0 && ink.All(q => Within(q, leaf));
        var control = Alive(checkbox) ? checkbox!.GetComponent<RectTransform>() : Alive(toggle) ? toggle!.GetComponent<RectTransform>() : null;
        bool controlInk = Alive(control) && metricReason == 0 && ink.All(q => Within(control!.InverseTransformPoint(text.transform.TransformPoint(q)), control.rect));
        bool controlQuad = Alive(control) && quad.All(q => Within(control!.InverseTransformPoint(text.transform.TransformPoint(q)), control.rect));
        log.Record($"GEOOC_{key}_T{(typedControl?1:0)}_P{proof:X}_L{(leafQuad?1:0)}_I{(metricInk?1:0)}_C{(controlInk?1:0)}_Q{(controlQuad?1:0)}_K{flags:X}_D{metricReason}");
        static int M(float n) => float.IsFinite(n) ? (int)Math.Clamp(n * 100, -99999, 99999) : 99999;
        log.Record($"GEOOQ_{key}_X{M(quad.Min(p=>p.x))}_{M(quad.Max(p=>p.x))}_Y{M(quad.Min(p=>p.y))}_{M(quad.Max(p=>p.y))}");
        log.Record($"GEOOI_{key}_X{M(ink.Min(p=>p.x))}_{M(ink.Max(p=>p.x))}_Y{M(ink.Min(p=>p.y))}_{M(ink.Max(p=>p.y))}");
        log.Record($"GEOOL_{key}_X{M(leaf.xMin)}_{M(leaf.xMax)}_Y{M(leaf.yMin)}_{M(leaf.yMax)}");
        if (Alive(control))
            log.Record($"GEOOT_{key}_X{M(control!.rect.xMin)}_{M(control.rect.xMax)}_Y{M(control.rect.yMin)}_{M(control.rect.yMax)}");
        var first = FooterMaterial(text, chars[0]);
        if (Alive(first) && Alive(first!.shader))
        {
            string shader = new(first.shader.name.ToUpperInvariant().Select(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' ? c : '_').Take(36).ToArray());
            log.Record($"GEOOS_{key}_{shader}");
            foreach (var pair in new[] { ("_GradientScale", "G"), ("_ScaleRatioA", "A"), ("_ScaleRatioC", "C"),
                ("_FaceDilate", "F"), ("_OutlineWidth", "W"), ("_OutlineSoftness", "S"), ("_WeightNormal", "N"),
                ("_UnderlayOffsetX", "X"), ("_UnderlayOffsetY", "Y"), ("_UnderlayDilate", "D"), ("_UnderlaySoftness", "U") })
                log.Record($"GEOOP_{key}_{pair.Item2}{(first.HasProperty(Shader.PropertyToID(pair.Item1)) ? M(first.GetFloat(Shader.PropertyToID(pair.Item1))) : -99999)}");
        }
    }
}
