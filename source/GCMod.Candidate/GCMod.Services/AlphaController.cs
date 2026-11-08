using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Services;

public static class AlphaController
{
	public static float ClampAlpha(float value)
	{
		return DialogueEditSession.Normalize(value);
	}

	public static void ApplyAlpha(float alpha, params Image[] images)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		foreach (Image val in images)
		{
			if (!((Object)(object)val == (Object)null))
			{
				Color color = ((Graphic)val).color;
				color.a = alpha;
				((Graphic)val).color = color;
			}
		}
	}
}
