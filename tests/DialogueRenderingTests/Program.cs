using GCMod;
using GCMod.Patches;
using GCMod.Services;
using DMM.OLG.Unity.Extensions.Novel;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
Material Preset(float width = 0.05f)
{
    var m = new Material();
    m.SetFloat("_FaceDilate", 0.02f); m.SetFloat("_OutlineWidth", width); m.SetFloat("_OutlineSoftness", 0.04f);
    m.SetFloat("_Unrelated", 17); m.SetColor("_OutlineColor", new Color(0.1f, 0.1f, 0.1f));
    m.EnableKeyword("NATIVE_KEYWORD");
    return m;
}
var shared = Preset();
var glyph = new TextMeshProUGUI { fontSharedMaterial = shared, color = new Color(0, 0, 0, 0.4f) };
var ruby = new TextMeshProUGUI { fontSharedMaterial = shared, color = new Color(0.1f, 0.1f, 0.1f, 0.2f) };
var go = new GameObject(); go.Texts.Add(glyph); go.Texts.Add(ruby);
TextStylePatch.AfterLetter(go, glyph);
Check(Material.Instances == 0 && glyph.fontSharedMaterial == shared, "OFF before first use allocates nothing and preserves shared material");
DialogueSettings.ReadabilityEnabled = true;
TextStylePatch.ApplyTracked(); glyph.RenderCanvas(); ruby.RenderCanvas();
Check(Material.Instances == 2 && glyph.fontSharedMaterial != shared && ruby.fontSharedMaterial != shared, "Each TMP obtains one private instance including ruby");
Check(shared.GetFloat("_OutlineWidth") == 0.05f && !shared.shaderKeywords.Contains("OUTLINE_ON"), "Shared game font preset is never modified");
Check(glyph.color.r == 1 && glyph.color.a == 0.4f && ruby.color.a == 0.2f, "Readability preserves independent glyph and ruby fade alpha");
Check(glyph.havePropertiesChanged == false && glyph.PaddingUpdates > 0 && glyph.VertexDirty > 0 && glyph.MaterialDirty > 0,
    "Patch requests normal TMP geometry/material rebuild");
var instance = glyph.fontSharedMaterial;
Check(!ReferenceEquals(instance, glyph.fontSharedMaterial) && instance == glyph.fontSharedMaterial, "Adapters expose distinct wrappers for the same native instance");
TextStylePatch.AfterLetter(go, glyph); // SetRuby can return a text that is also a child of go.
Check(Material.Instances == 2, "Duplicate glyph/ruby tracking does not allocate another material");
glyph.color = new Color(1, 1, 1, 0.7f);
DialogueSettings.ReadabilityEnabled = false; TextStylePatch.ApplyTracked();
glyph.RenderCanvas(); ruby.RenderCanvas();
Check(glyph.color.r == 0 && glyph.color.a == 0.7f && ruby.color.r == 0.1f, "OFF restores RGB while retaining current native fade alpha");
Check(glyph.fontSharedMaterial == instance && instance.GetFloat("_OutlineWidth") == 0.05f && !instance.shaderKeywords.Contains("OUTLINE_ON"),
    "OFF restores properties in place without invalidating fallback reference");
Check(instance.shaderKeywords.Contains("NATIVE_KEYWORD") && instance.GetFloat("_Unrelated") == 17, "Unrelated shader values and keywords survive both paths");

for (int i = 0; i < 100; i++)
{
    DialogueSettings.ReadabilityEnabled = true; TextStylePatch.ApplyTracked(); glyph.RenderCanvas();
    DialogueSettings.ReadabilityEnabled = false; TextStylePatch.ApplyTracked(); glyph.RenderCanvas();
}
Check(Material.Instances == 2 && Object.DestroyCalls == 0, "100 readability cycles keep material count bounded and never destroy live TMP materials");

DialogueSettings.ReadabilityEnabled = true; TextStylePatch.ApplyTracked();
Config.OutlineWidth.Value = 0.45f; TextStylePatch.ApplyTracked();
DialogueSettings.ReadabilityEnabled = false; TextStylePatch.ApplyTracked();
Check(instance.GetFloat("_OutlineWidth") == 0.05f, "Repeated advanced-property changes retain original restore values");
DialogueSettings.ReadabilityEnabled = true; TextStylePatch.ApplyTracked();
instance.SetFloat("_OutlineWidth", 0.18f); glyph.color = new Color(0.3f, 0.2f, 0.1f, 0.25f);
DialogueSettings.ReadabilityEnabled = false; TextStylePatch.ApplyTracked();
Check(instance.GetFloat("_OutlineWidth") == 0.18f && glyph.color.r == 0.3f && glyph.color.a == 0.25f,
    "Native shader/RGB changes after the override are respected on restore");

DialogueSettings.ReadabilityEnabled = true; TextStylePatch.ApplyTracked();
var liveReplacement = Preset(0.16f);
glyph.fontSharedMaterial = liveReplacement;
TextStylePatch.ApplyTracked(); glyph.RenderCanvas();
DialogueSettings.ReadabilityEnabled = false; TextStylePatch.ApplyTracked();
Check(Material.Instances == 2 && glyph.fontSharedMaterial == instance && instance.GetFloat("_OutlineWidth") == 0.16f,
    "Native live preset replacement captures its own baseline and reuses the TMP instance");
Check(liveReplacement.GetFloat("_OutlineWidth") == 0.16f, "Live replacement never mutates new shared preset");

var changedPreset = Preset(0.11f); changedPreset.EnableKeyword("OUTLINE_ON");
DialogueSettings.ReadabilityEnabled = true;
for (int i = 0; i < 100; i++)
{
    TextStylePatch.ReleaseGlyph(go);
    // Simulate native pooled glyph initialization replacing the shared preset on every reuse.
    glyph.fontSharedMaterial = changedPreset; ruby.fontSharedMaterial = shared;
    glyph.color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
    TextStylePatch.AfterLetter(go, glyph); glyph.RenderCanvas();
}
Check(Material.Instances == 2, "100 native preset resets reuse TMP private instances without allocation growth");
TextStylePatch.ReleaseGlyph(go);
Check(instance.GetFloat("_OutlineWidth") == 0.11f && instance.shaderKeywords.Contains("OUTLINE_ON") && glyph.color.r == 0.2f,
    "Pool return restores latest native preset, including originally enabled outline");
Check(changedPreset.GetFloat("_OutlineWidth") == 0.11f, "Changed shared preset also remains untouched");

var name = new TextMeshProUGUI { fontSharedMaterial = shared, color = new Color(0.15f, 0.1f, 0, 0.6f) };
var message = new EventMessage { MessageName = name };
TextStylePatch.AfterName(message);
Check(name.color.r == Config.NameTextColor.r && name.color.a == 0.6f, "Name uses its own style color and native alpha");
TextStylePatch.BeforeName(message);
Check(name.color.r == 0.15f && name.color.a == 0.6f, "Native name reset sees original appearance");
name.color = new Color(0.25f, 0.25f, 0.25f, 0.8f); TextStylePatch.AfterName(message);
var owner = new EventText { fontSpacing = 0.25f }; TextStylePatch.PrepareText(owner);
Check(owner.fontSpacing == 0, "Enabled spacing override captures native value");
TextStylePatch.Clear();
Check(name.color.r == 0.25f && owner.fontSpacing == 0.25f, "New scenario/unload restores registered names and native spacing");
Check(Object.DestroyCalls == 0 && glyph.ParsedOrAdvanced == 0 && ruby.ParsedOrAdvanced == 0, "Cleanup neither destroys TMP materials nor advances/reparses dialogue");

// Bitmap or incomplete shader presets are allowed: only supported properties are changed.
var bitmap = new Material();
var basic = new TextMeshProUGUI { fontSharedMaterial = bitmap, color = new Color(0, 0, 0) };
TextStylePatch.AfterLetter(null, basic); TextStylePatch.Clear();
Check(!basic.fontSharedMaterial.HasProperty("_OutlineWidth") && basic.color.r == 0, "Missing optional shader properties do not prevent RGB restoration");
Console.WriteLine($"Production dialogue-rendering adapter checks passed: {assertions}; native Unity validation still required.");
