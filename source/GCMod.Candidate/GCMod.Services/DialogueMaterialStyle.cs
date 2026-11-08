using UnityEngine;

namespace GCMod.Services;

// The TMP component owns this instance (obtained through fontMaterial).
// Restore only our shader writes; never destroy a material referenced by TMP's fallback cache.
public sealed class DialogueMaterialStyle
{
    public Material Material { get; }
    private readonly ValueOverride<float> face = new(), width = new(), softness = new();
    private readonly ValueOverride<Color> outline = new(SameColor);
    private readonly ValueOverride<bool> keyword = new();

    public DialogueMaterialStyle(Material material) => Material = material;
    public static bool SameColor(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    public void Apply(float faceDilate, Color outlineColor, float outlineWidth, float outlineSoftness)
    {
        if (Material == null) return;
        ApplyFloat("_FaceDilate", face, faceDilate);
        ApplyFloat("_OutlineWidth", width, outlineWidth);
        ApplyFloat("_OutlineSoftness", softness, outlineSoftness);
        if (Material.HasProperty("_OutlineColor"))
            Material.SetColor("_OutlineColor", outline.Apply(Material.GetColor("_OutlineColor"), outlineColor));
        if (Material.HasProperty("_OutlineWidth"))
        {
            keyword.Apply(OutlineEnabled(), true);
            Material.EnableKeyword("OUTLINE_ON");
        }
    }

    public void Restore()
    {
        if (Material == null) return;
        RestoreFloat("_FaceDilate", face);
        RestoreFloat("_OutlineWidth", width);
        RestoreFloat("_OutlineSoftness", softness);
        if (outline.Active && Material.HasProperty("_OutlineColor"))
            Material.SetColor("_OutlineColor", outline.Restore(Material.GetColor("_OutlineColor")));
        if (keyword.Active)
        {
            bool enabled = keyword.Restore(OutlineEnabled());
            if (enabled) Material.EnableKeyword("OUTLINE_ON"); else Material.DisableKeyword("OUTLINE_ON");
        }
    }

    private bool OutlineEnabled()
    {
        // This game's interop IsKeywordEnabled(string) contains a broken restored
        // ReadOnlySpan call. shaderKeywords invokes the native method directly.
        var keywords = Material.shaderKeywords;
        if (keywords == null) return false;
        for (int i = 0; i < keywords.Length; i++)
            if (keywords[i] == "OUTLINE_ON") return true;
        return false;
    }

    private void ApplyFloat(string name, ValueOverride<float> state, float value)
    {
        if (Material.HasProperty(name)) Material.SetFloat(name, state.Apply(Material.GetFloat(name), value));
    }
    private void RestoreFloat(string name, ValueOverride<float> state)
    {
        if (state.Active && Material.HasProperty(name)) Material.SetFloat(name, state.Restore(Material.GetFloat(name)));
    }
}
