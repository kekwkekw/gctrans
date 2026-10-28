using System;
using System.Collections.Generic;
using System.IO;
using GCMod.Services;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GCMod.Patches;

// Only replaces the image reference on a UI component. Game AssetBundles,
// shared source textures, geometry and button actions are never modified.
[HarmonyPatch]
public static class ImagePatch
{
    private static readonly Dictionary<string, byte[]> Payloads = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.Ordinal);
    private static readonly HashSet<int> OwnObjects = new();
    [ThreadStatic] private static bool _applying;

    public static void Initialize(string translationRoot, string language)
    {
        Payloads.Clear();
        string root = Path.Combine(translationRoot, language);
        string path = Path.Combine(root, "images.json");
        if (!File.Exists(path)) return;
        try
        {
            var catalog = ImageCatalog.Parse(File.ReadAllText(path));
            foreach (var entry in catalog.Entries)
            {
                string file = Path.Combine(root, entry.File.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file) || new FileInfo(file).Length != entry.Bytes) continue;
                byte[] bytes = File.ReadAllBytes(file);
                if (entry.Matches(bytes)) Payloads.Add(entry.Key, bytes);
                else Plugin.Log.LogWarning("Image skipped: invalid payload for " + entry.Name);
            }
            Plugin.Log.LogInfo("[ImagePatch] Ready: " + Payloads.Count + " verified image targets");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is System.Text.Json.JsonException)
        {
            Payloads.Clear();
            Plugin.Log.LogWarning("Image catalog unavailable; keeping original images: " + ex.Message);
        }
    }

    private static Texture2D Load(string key)
    {
        if (Textures.TryGetValue(key, out var cached)) return cached;
        if (!Payloads.TryGetValue(key, out var bytes)) return null;
        // Called only at a Unity UI callback, never on the network worker.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, true))
        {
            Object.Destroy(texture);
            Payloads.Remove(key);
            Plugin.Log.LogWarning("Image PNG decode failed: " + key);
            return null;
        }
        texture.name = "GCMod:" + key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Object.DontDestroyOnLoad(texture);
        Textures[key] = texture;
        OwnObjects.Add(texture.GetInstanceID());
        Plugin.Log.LogInfo("[ImagePatch] Replaced " + key);
        return texture;
    }

    private static Sprite Translate(Sprite original)
    {
        if (original == null || OwnObjects.Contains(original.GetInstanceID())) return original;
        var rect = original.rect;
        string key = "sprite:" + original.name + ":" + (int)rect.width + "x" + (int)rect.height;
        string spriteKey = key + ":" + original.GetInstanceID();
        if (Sprites.TryGetValue(spriteKey, out var cached)) return cached;
        var texture = Load(key);
        if (texture == null) return original;
        var pivot = original.pivot;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(pivot.x / rect.width, pivot.y / rect.height), original.pixelsPerUnit,
            0, SpriteMeshType.FullRect, original.border);
        sprite.name = "GCMod:" + original.name;
        Object.DontDestroyOnLoad(sprite);
        Sprites[spriteKey] = sprite;
        OwnObjects.Add(sprite.GetInstanceID());
        return sprite;
    }

    [HarmonyPatch(typeof(Image), "sprite", MethodType.Setter)]
    [HarmonyPatch(typeof(Image), "overrideSprite", MethodType.Setter)]
    [HarmonyPrefix]
    public static void SetSprite(ref Sprite value)
    {
        if (_applying || !Config.Translation.Value || Payloads.Count == 0) return;
        try { value = Translate(value); }
        catch (Exception ex) { Plugin.Log.LogWarning("Image replacement skipped: " + ex.Message); }
    }

    [HarmonyPatch(typeof(Image), "OnEnable")]
    [HarmonyPostfix]
    public static void EnableImage(Image __instance)
    {
        if (_applying || !Config.Translation.Value || Payloads.Count == 0 || __instance == null) return;
        try
        {
            _applying = true;
            var original = __instance.sprite;
            var translated = Translate(original);
            if (translated != original) __instance.sprite = translated;
            // Do not create an override when the game has none: that would pin
            // the old artwork when a recycled card changes its base sprite.
            original = __instance.m_OverrideSprite;
            translated = Translate(original);
            if (translated != original) __instance.overrideSprite = translated;
        }
        catch (Exception ex) { Plugin.Log.LogWarning("Image replacement skipped: " + ex.Message); }
        finally { _applying = false; }
    }

    [HarmonyPatch(typeof(RawImage), "texture", MethodType.Setter)]
    [HarmonyPrefix]
    public static void SetTexture(RawImage __instance, ref Texture value)
    {
        if (_applying || !Config.Translation.Value || Payloads.Count == 0 || value == null ||
            OwnObjects.Contains(value.GetInstanceID()) || __instance.uvRect != new Rect(0, 0, 1, 1)) return;
        try
        {
            var replacement = Load("texture:" + value.name + ":" + value.width + "x" + value.height);
            if (replacement != null) value = replacement;
        }
        catch (Exception ex) { Plugin.Log.LogWarning("Texture replacement skipped: " + ex.Message); }
    }

    [HarmonyPatch(typeof(RawImage), "OnEnable")]
    [HarmonyPostfix]
    public static void EnableTexture(RawImage __instance)
    {
        if (__instance == null) return;
        Texture original = __instance.texture, translated = original;
        SetTexture(__instance, ref translated);
        if (translated != original) __instance.texture = translated;
    }

    public static void Clear()
    {
        // Called after unpatching during normal plugin unload.
        foreach (var sprite in Sprites.Values) if (sprite != null) Object.Destroy(sprite);
        foreach (var texture in Textures.Values) if (texture != null) Object.Destroy(texture);
        Payloads.Clear(); Sprites.Clear(); Textures.Clear(); OwnObjects.Clear();
    }
}
