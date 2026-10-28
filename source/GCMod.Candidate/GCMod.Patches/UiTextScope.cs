using DMM.OLG.Unity.Extensions.Novel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Patches;

internal static class UiTextScope
{
    // Protect the renderer, not a character length. This includes untranslated
    // Novel text, retained kanji, ruby and pooled choice glyphs. The surrounding
    // message-frame buttons and speaker name are not part of these renderers.
    public static bool IsProtected(Component text)
    {
        return text == null
            || text.GetComponentInParent<TMP_InputField>(true) != null
            || text.GetComponentInParent<InputField>(true) != null
            || text.GetComponentInParent<EventText>(true) != null
            || text.GetComponentInParent<EventMessageWindow>(true) != null
            || text.GetComponentInParent<EventSelectObj>(true) != null;
    }
}
