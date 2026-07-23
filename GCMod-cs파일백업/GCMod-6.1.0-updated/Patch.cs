using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils;
using DMM.OLG.Unity.Engine.Internal;
using DMM.OLG.Unity.Extensions.Novel;
using Gc;
using Gc.Battle.SkillWidget;
using Gc.Home;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod
{
    public class Patch
    {
        public static int novelId;

        public static Image NormalFrame;
        public static Image CgModeFrame;
        public static Image BaseNameFrame;

        public static Color DefaultNameColor =
            new Color(0.957f, 0.957f, 0.914f);

        public static void Initialize()
        {
            Harmony.CreateAndPatchAll(typeof(Patch), null);
        }

        public static void ModifyText(
            TextMeshProUGUI text,
            Color color
        )
        {
            if (text == null || text.fontMaterial == null)
            {
                return;
            }

            text.color = color;
            text.fontMaterial.EnableKeyword("OUTLINE_ON");

            text.fontMaterial.SetFloat(
                "_FaceDilate",
                Config.FaceDilate.Value
            );

            text.fontMaterial.SetColor(
                "_OutlineColor",
                Config.OutlineColor
            );

            text.fontMaterial.SetFloat(
                "_OutlineWidth",
                Config.OutlineWidth.Value
            );

            text.fontMaterial.SetFloat(
                "_OutlineSoftness",
                Config.OutlineSoftness.Value
            );
        }

        public static void CancelModifyText(
            TextMeshProUGUI text,
            Color color
        )
        {
            if (text == null || text.fontMaterial == null)
            {
                return;
            }

            text.color = color;

            text.fontMaterial.SetFloat(
                "_FaceDilate",
                0f
            );

            text.fontMaterial.SetColor(
                "_OutlineColor",
                Color.black
            );

            text.fontMaterial.SetFloat(
                "_OutlineWidth",
                0f
            );

            text.fontMaterial.SetFloat(
                "_OutlineSoftness",
                0f
            );

            text.fontMaterial.DisableKeyword("OUTLINE_ON");
        }

        // Offline
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(ConfigData),
            nameof(ConfigData.Set)
        )]
        public static void SetApiDomain(
            string key,
            ref string value
        )
        {
            if (!Config.offline && !Config.Offline.Value)
            {
                return;
            }

            if ("ApiDomain".Equals(key))
            {
                value = Config.OfflineCDN.Value;

                Plugin.Log.LogInfo(
                    $"ApiDomain: {value}"
                );
            }
        }

        // Scenario setup
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(ScriptObjectManager),
            nameof(ScriptObjectManager.Setup)
        )]
        public static void SetupTranslation(
            string prefix,
            string id
        )
        {
            // GCMod 자체 번역 기능이 꺼져 있어도
            // 폰트 로드 및 글로벌 fallback 등록은 유지합니다.
            if (Translation.fontAsset == null)
            {
                Plugin.Instance.StartCoroutine(
                    Translation.LoadFontAsset()
                );
            }
            else
            {
                Translation.RegisterLoadedFontAsGlobalFallback(
                    Translation.fontAsset
                );
            }

            if (!Config.Translation.Value)
            {
                return;
            }

            Plugin.Log.LogInfo(
                $"Prefix: {prefix}, Id: {id}"
            );

            int parsedNovelId;

            if (!int.TryParse(id, out parsedNovelId))
            {
                Plugin.Log.LogWarning(
                    $"Novel id parse failed: {id}"
                );

                return;
            }

            novelId = parsedNovelId;

            if (!Translation.novels.ContainsKey(novelId))
            {
                Task task =
                    Translation.GetNovelTranslationAsync(novelId);

                if (!Config.AsyncMode.Value)
                {
                    task.Wait();
                }
            }
        }

        // Scenario title translation
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(EventTitle),
            nameof(EventTitle.ShowBlurEffect)
        )]
        public static void SetMessageTitle(
            EventTitle __instance
        )
        {
            if (!Config.Translation.Value)
            {
                return;
            }

            if (!Translation.novels.ContainsKey(novelId))
            {
                return;
            }

            string title;

            if (Translation.novels[novelId].TryGetValue(
                __instance._TitleMain.text,
                out title
            ))
            {
                __instance._TitleMain.text = title;
            }
        }

        // Speaker name translation
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(EventMessage),
            nameof(EventMessage.SetName)
        )]
        public static void SetMessageName(
            ref string text
        )
        {
            if (!Config.Translation.Value)
            {
                return;
            }

            if (!Translation.novels.ContainsKey(novelId))
            {
                return;
            }

            string name;

            if (Translation.names.TryGetValue(
                text,
                out name
            ))
            {
                text = name;
            }
        }

        // Scenario body translation
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(EventText),
            nameof(EventText.Parse)
        )]
        public static void SetMessageText(
            EventText __instance,
            ref string message
        )
        {
            if (
                Config.Translation.Value &&
                Translation.novels.ContainsKey(novelId)
            )
            {
                string translatedText;

                if (Translation.novels[novelId].TryGetValue(
                    message,
                    out translatedText
                ))
                {
                    message = translatedText;
                }
            }

            if (Config.ModifyText.Value)
            {
                __instance.fontSpacing =
                    Config.CharacterSpacing.Value;
            }
        }

        // Speaker-name visual style only.
        // 개별 TMP 컴포넌트의 font 프로퍼티는 변경하지 않습니다.
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(EventMessage),
            nameof(EventMessage.SetName)
        )]
        public static void SetMessageNameStyle(
            EventMessage __instance
        )
        {
            if (Config.ModifyText.Value)
            {
                ModifyText(
                    __instance.MessageName,
                    Config.NameTextColor
                );
            }
            else
            {
                CancelModifyText(
                    __instance.MessageName,
                    DefaultNameColor
                );
            }
        }

        // Scenario-body visual style only.
        // 개별 TMP 컴포넌트의 font 프로퍼티는 변경하지 않습니다.
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(EventText),
            nameof(EventText.SetRuby)
        )]
        public static void SetMessageTextStyle(
            GameObject go,
            EventText.Letter letter,
            ref TextMeshProUGUI text
        )
        {
            if (Config.ModifyText.Value)
            {
                ModifyText(
                    text,
                    Config.MessageTextColor
                );
            }
        }

        // Message-window alpha
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(EventMessage),
            nameof(EventMessage.Init)
        )]
        public static void SaveTextBackgound(
            EventMessage __instance
        )
        {
            foreach (
                Image image in
                __instance.GetComponentsInChildren<Image>()
            )
            {
                if (image.name == "Normal")
                {
                    Color color = image.color;
                    color.a = Config.NormalAlpha.Value;
                    image.color = color;
                    NormalFrame = image;
                }

                if (image.name == "MessageWindow")
                {
                    Color color = image.color;
                    color.a = Config.CgModeAlpha.Value;
                    image.color = color;
                    CgModeFrame = image;
                }
            }

            foreach (
                Image image in
                __instance.NameImage
                    .GetComponentsInChildren<Image>()
            )
            {
                if (image.name == "BaseName")
                {
                    Color color = image.color;
                    color.a = Config.NormalAlpha.Value;
                    image.color = color;
                    BaseNameFrame = image;
                }
            }
        }

        // Home character-word translation
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(UnitWordMasterBase),
            nameof(UnitWordMasterBase.Word),
            MethodType.Getter
        )]
        public static void SetHomeWord(
            ref string __result
        )
        {
            if (!Config.Translation.Value)
            {
                return;
            }

            string translatedText;

            if (Translation.words.TryGetValue(
                __result,
                out translatedText
            ))
            {
                __result = translatedText;
            }
        }

        /*
         * 의도적으로 제거한 기존 개별 UI 폰트 교체 패치:
         *
         * - SetMessageTitleFont
         * - SetMessageNameFont의 MessageName.font 대입
         * - SetMessageTextFont의 text.font 대입
         * - SetHomeWordFont
         *
         * 이제 Translation.RegisterLoadedFontAsGlobalFallback()가
         * TMP_Settings.fallbackFontAssets에 폰트를 등록합니다.
         */

        // Skip cut-in movie
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(CutInMoviePlayer),
            nameof(CutInMoviePlayer.PlayAsync)
        )]
        public static void SkipCutin(
            CutInMoviePlayer __instance
        )
        {
            if (Config.IsSkipCutin.Value)
            {
                __instance.Skip();
            }
        }

        // Frame-rate override
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(GcOptionData),
            nameof(GcOptionData.SetPowerSaving)
        )]
        public static void ChangeFrameRate()
        {
            int fps = Config.FrameRate.Value;

            if (fps > 0)
            {
                GcOptionData
                    .ChangeApplicationTargetFrameRate(fps);
            }
        }
    }
}
