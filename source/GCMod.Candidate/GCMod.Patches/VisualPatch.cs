using DMM.OLG.Unity.Extensions.Novel;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using GCMod.Services;

namespace GCMod.Patches;

[HarmonyPatch]
public static class VisualPatch
{
	private sealed class Backgrounds
	{
		public EventMessage Owner;
		public readonly List<Image> Normal = new();
		public readonly List<Image> Cg = new();
	}
	private static readonly Dictionary<int, Backgrounds> BackgroundOwners = new();

	public static void ApplyBackgrounds(float normal, float cg)
	{
		var expired = new List<int>();
		foreach (var pair in BackgroundOwners)
		{
			var frames = pair.Value;
			if (frames.Owner == null) { expired.Add(pair.Key); continue; }
			foreach (var image in frames.Normal) ApplyImageAlpha(image, AlphaController.ClampAlpha(normal));
			foreach (var image in frames.Cg) ApplyImageAlpha(image, AlphaController.ClampAlpha(cg));
		}
		foreach (var id in expired) BackgroundOwners.Remove(id);
	}

	public static void ClearBackgrounds() => BackgroundOwners.Clear();

	private static void ApplyImageAlpha(Image image, float alpha)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)image == (Object)null))
		{
			Color color = ((Graphic)image).color;
			color.a = alpha;
			((Graphic)image).color = color;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(EventMessage), "Init")]
	public static void SaveTextBackground(EventMessage __instance)
	{
		if (__instance == null) return;
		var frames = new Backgrounds { Owner = __instance };
		foreach (var image in __instance.GetComponentsInChildren<Image>(true))
		{
			// The CG frame also owns a BaseName image. Classify by frame ancestry
			// so changing normal opacity cannot change the CG speaker background.
			string frame = FindFrame(image.transform, __instance.transform);
			if (frame == "NormalFrame" && (image.name == "Normal" || image.name == "BaseName")) frames.Normal.Add(image);
			else if (frame == "CgModeFrame" && (image.name == "MessageWindow" || image.name == "BaseName")) frames.Cg.Add(image);
		}
		bool first = !BackgroundOwners.TryGetValue(__instance.GetInstanceID(), out var prior)
			|| prior.Normal.Count != frames.Normal.Count || prior.Cg.Count != frames.Cg.Count;
		BackgroundOwners[__instance.GetInstanceID()] = frames;
		if (first)
		{
			Plugin.Log.LogInfo($"[Opacity] Bound message {__instance.GetInstanceID()}: normal={frames.Normal.Count}, cg={frames.Cg.Count}");
			foreach (var image in frames.Normal) Plugin.Log.LogInfo("[Opacity] Normal target: " + ImagePath(image.transform));
			foreach (var image in frames.Cg) Plugin.Log.LogInfo("[Opacity] CG target: " + ImagePath(image.transform));
		}
		DialogueSettings.Apply();
	}

	private static string FindFrame(Transform transform, Transform owner)
	{
		while (transform != null && transform != owner)
		{
			if (transform.name == "NormalFrame" || transform.name == "CgModeFrame") return transform.name;
			transform = transform.parent;
		}
		return null;
	}

	private static string ImagePath(Transform transform)
	{
		var path = transform.name;
		for (int i = 0; i < 8 && transform.parent != null; i++)
		{ transform = transform.parent; path = transform.name + "/" + path; }
		return path;
	}

	[HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "OnEnable")]
	public static void BackgroundEnabled(EventMessage __instance) => SaveTextBackground(__instance);
	[HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeParentFrameType")]
	public static void ParentFrameChanged(EventMessage __instance) => SaveTextBackground(__instance);
	[HarmonyPostfix, HarmonyPatch(typeof(EventMessage), "ChangeFrameType")]
	public static void FrameChanged(EventMessage __instance) => SaveTextBackground(__instance);
}
