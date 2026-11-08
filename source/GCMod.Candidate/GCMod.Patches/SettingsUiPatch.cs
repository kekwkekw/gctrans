using System;
using System.Collections.Generic;
using Gc.Option;
using GCMod.Services;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GCMod.Patches;

[HarmonyPatch]
public static class SettingsUiPatch
{
    private sealed class View
    {
        public OptionPopup Popup;
        public SoundAndTextOptionArea Area;
        public RectTransform Root, Body, ScreenRoot, ScreenBody;
        public ScreenSizeOptionArea ScreenArea;
        public NativeStyle Style;
        public TextMeshProUGUI Status, ReadabilityValue, FrameRateHint;
        public readonly TextMeshProUGUI[] FrameRateChoices = new TextMeshProUGUI[2];
        public float Width;
        public readonly Slider[] Sliders = new Slider[2];
        public readonly TextMeshProUGUI[] Values = new TextMeshProUGUI[2];
        // Keep managed-to-IL2CPP delegates alive for precisely this view's lifetime.
        public readonly List<object> Listeners = new();
    }
    // Share the native visual assets, never clone option behaviours or their callbacks.
    private sealed class NativeStyle
    {
        public Image Panel, TitleLine, Track, Fill, Handle, Button, Minus, Plus, ValueBox;
        public TextMeshProUGUI Label, Value;
        public ColorBlock Colors;

        public static NativeStyle Read(SoundAndTextOptionArea area)
        {
            var source = area.bgmOption;
            if (source == null || source.slider == null || source.sliderValueText == null)
                throw new InvalidOperationException("Native option slider template missing");
            var slider = source.slider;
            var style = new NativeStyle
            {
                Panel = FindImage(area.transform, "Inner_Frame"),
                TitleLine = FindImage(area.transform, "Title_Line"),
                Track = FindImage(slider.transform, "SliderBar"),
                Fill = FindImage(slider.transform, "InnerBar"),
                Handle = FindImage(slider.transform, "image_BtSS"),
                Button = FindImage(slider.MinusButton.transform, "image_Textbtn01_s"),
                Minus = FindImage(slider.MinusButton.transform, "Icon"),
                Plus = FindImage(slider.PlusButton.transform, "Icon"),
                ValueBox = source.sliderValueText.transform.parent.GetComponent<Image>(),
                Value = source.sliderValueText,
                Colors = ColorBlock.defaultColorBlock
            };
            foreach (var text in area.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == "Text_BGM") { style.Label = text; break; }
            if (style.Label == null || style.ValueBox == null)
                throw new InvalidOperationException("Native option text template missing");
            var colors = style.Colors;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.78f, 0.86f, 0.95f, 1f);
            style.Colors = colors;
            return style;
        }
    }
    private static View current;
    private const float DialoguePanelHeight = 390f;
    private const float FrameRatePanelHeight = 210f;

    [HarmonyPostfix, HarmonyPatch(typeof(OptionPopup), "SetupView")]
    public static void Setup(OptionPopup __instance)
    {
        try
        {
            if (current != null && current.Popup == __instance && (current.Root != null || current.ScreenRoot != null)) { Refresh(); return; }
            Cleanup();
            var area = __instance.soundAndTextOptionArea;
            if (area == null || area.messageSpeedOption == null || area.messageSpeedOption.sliderValueText == null)
                throw new InvalidOperationException("Sound/text option template missing");
            var areaRect = area.GetComponent<RectTransform>();
            float height = areaRect.rect.height;
            if (height < 200f || areaRect.rect.width < 1200f) throw new InvalidOperationException("Option layout not ready");
            var view = new View { Popup = __instance, Area = area, Style = NativeStyle.Read(area), Width = areaRect.rect.width };
            current = view;
            bool screenTab = __instance.isTabUse || __instance.isScreenSizeOnly;
            if (!__instance.isScreenSizeOnly)
            {
                float addedHeight = DialoguePanelHeight + (screenTab ? 0 : FrameRatePanelHeight);
                var content = WrapOptions(view, area.transform, height, addedHeight, true, out view.Root, out view.Body);

                var panel = Rect("GCModBackgroundSettings", content, new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, DialoguePanelHeight - 16));
                panel.pivot = new Vector2(0.5f, 1);
                CopyImage(panel, view.Style.Panel).raycastTarget = false;
                Label(view, panel, "대화창", 30, 14, view.Width - 60, 40, 31).alignment = TextAlignmentOptions.Center;
                var titleLine = Rect("TitleLine", panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(view.Width - 100, 15));
                CopyImage(titleLine, view.Style.TitleLine).raycastTarget = false;
                view.Status = Label(view, panel, "", 40, 316, view.Width - 380, 42, 26);
                AddRow(view, panel, false, 40);
                AddRow(view, panel, true, view.Width / 2 + 30);
                Label(view, panel, "대화 글자 가독성 보정", 40, 208, view.Width - 380, 52, 31);
                view.ReadabilityValue = Button(view, panel, "", view.Width - 310, 208, 270, 52, null, DialogueSettings.ToggleReadability);
                Label(view, panel, "투명한 배경에서도 글자를 읽기 쉽게 보정합니다.", 40, 268, view.Width - 80, 36, 26);
                Button(view, panel, "불투명도 초기화", view.Width - 310, 312, 270, 52, null, () =>
                {
                    DialogueSettings.Preview(false, 0);
                    DialogueSettings.Preview(true, 0);
                });
                // Scenario options have no tabs. Retain the inline control there.
                if (!screenTab) AddFrameRatePanel(view, content, DialoguePanelHeight, view.Width);
            }
            if (screenTab)
            {
                view.ScreenArea = __instance.screenSizeOptionArea;
                if (view.ScreenArea == null) throw new InvalidOperationException("Screen option area missing");
                var screenRect = view.ScreenArea.GetComponent<RectTransform>();
                float screenHeight = screenRect.rect.height;
                if (screenHeight < 200 || screenRect.rect.width < 1200)
                    throw new InvalidOperationException("Screen option layout not ready");
                var screenContent = WrapOptions(view, view.ScreenArea.transform, screenHeight,
                    FrameRatePanelHeight, false, out view.ScreenRoot, out view.ScreenBody);
                AddFrameRatePanel(view, screenContent, screenHeight + 16, screenRect.rect.width);
            }
            DialogueSettings.Begin();
            Refresh();
            Plugin.Log.LogInfo($"[SettingsUI] Created native-style dialogue/FPS controls; original area={areaRect.rect.width:0}x{height:0}, tabbed={__instance.isTabUse}, fpsInScreenTab={screenTab}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("[SettingsUI] Could not create controls: " + ex);
            Cleanup();
        }
    }

    // Match the native screen-tab discard path, just as the sound/text Setup hook does.
    [HarmonyPostfix, HarmonyPatch(typeof(ScreenSizeOptionArea), "SetUp")]
    public static void ResetScreenDraft(ScreenSizeOptionArea __instance)
    {
        if (current?.ScreenArea == __instance) DialogueSettings.Reload();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ScreenSizeOptionArea), "IsValueChanged")]
    public static void ScreenChanged(ScreenSizeOptionArea __instance, ref bool __result)
    {
        if (current?.ScreenArea == __instance) __result |= DialogueSettings.Edit?.IsDirty == true;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(SoundAndTextOptionArea), "Setup")]
    public static void ResetDraft(SoundAndTextOptionArea __instance)
    {
        if (current?.Area == __instance) DialogueSettings.Reload();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(SoundAndTextOptionArea), "IsValueChanged")]
    public static void Changed(SoundAndTextOptionArea __instance, ref bool __result)
    {
        if (current?.Area == __instance) __result |= DialogueSettings.Edit?.IsDirty == true;
    }

    // A local save failure must not let the original handler display success.
    [HarmonyPrefix, HarmonyPatch(typeof(OptionPopup), "OnSaveClick")]
    public static bool Save(OptionPopup __instance) => current?.Popup != __instance || DialogueSettings.Commit();

    [HarmonyPrefix, HarmonyPatch(typeof(OptionPopup), "Close")]
    public static void Close(OptionPopup __instance)
    {
        if (current?.Popup == __instance) Cleanup();
    }

    public static void CheckLifetime()
    {
        if (current != null && (current.Popup == null || (current.Root == null && current.ScreenRoot == null))) Cleanup();
    }

    public static void Cleanup()
    {
        var old = current;
        current = null;
        DialogueSettings.End();
        if (old == null) return;
        // Restore the original hierarchy before the popup is removed or on a partial build failure.
        if (old.Body != null && old.Area != null)
            while (old.Body.childCount > 0) old.Body.GetChild(0).SetParent(old.Area.transform, false);
        if (old.Root != null) { old.Root.gameObject.SetActive(false); Object.Destroy(old.Root.gameObject); }
        if (old.ScreenBody != null && old.ScreenArea != null)
            while (old.ScreenBody.childCount > 0) old.ScreenBody.GetChild(0).SetParent(old.ScreenArea.transform, false);
        if (old.ScreenRoot != null) { old.ScreenRoot.gameObject.SetActive(false); Object.Destroy(old.ScreenRoot.gameObject); }
        old.Listeners.Clear();
    }

    public static void Refresh()
    {
        var view = current;
        var edit = DialogueSettings.Edit;
        if (view == null || edit == null) return;
        for (int i = 0; i < 2; i++)
        {
            float value = i == 0 ? edit.Normal : edit.Cg;
            if (view.Sliders[i] != null) view.Sliders[i].SetValueWithoutNotify(value * 100);
            if (view.Values[i] != null) view.Values[i].text = $"{value * 100:0}%";
        }
        if (view.Status != null) view.Status.text = edit.IsDirty
            ? "미리보기 중 · 설정 저장을 눌러 적용해 주세요."
            : "0% 투명 / 100% 불투명";
        if (view.ReadabilityValue != null)
        {
            view.ReadabilityValue.text = edit.Readability ? "켜짐" : "꺼짐";
            view.ReadabilityValue.color = edit.Readability ? view.Style.Value.color : view.Style.Label.color;
        }
        for (int i = 0; i < view.FrameRateChoices.Length; i++)
        {
            var label = view.FrameRateChoices[i];
            if (label == null) continue;
            bool selected = edit.FrameRate == (i == 0 ? 30 : 60);
            label.color = selected ? view.Style.Value.color : view.Style.Label.color;
            label.fontStyle = selected ? view.Style.Label.fontStyle | FontStyles.Bold : view.Style.Label.fontStyle;
        }
        if (view.FrameRateHint != null)
            view.FrameRateHint.text = edit.FrameRate == 30 || edit.FrameRate == 60
                ? "선택 즉시 미리보기됩니다. 설정 저장을 눌러 유지해 주세요."
                : edit.FrameRate <= 0 ? "현재 게임 기본값 사용 중 · 선택 전까지 기존 설정을 유지합니다."
                : $"현재 사용자 지정 {edit.FrameRate} FPS · 선택 전까지 기존 설정을 유지합니다.";
    }

    private static RectTransform WrapOptions(View view, Transform area, float height, float extraHeight,
        bool prepend, out RectTransform root, out RectTransform body)
    {
        var originalChildren = new List<Transform>();
        for (int i = 0; i < area.childCount; i++) originalChildren.Add(area.GetChild(i));
        root = Rect("GCModOptionsViewport", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.gameObject.AddComponent<Image>().color = Color.clear;
        root.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", root, new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, height + extraHeight));
        content.pivot = new Vector2(0.5f, 1);
        body = Rect("OriginalSettings", content, new Vector2(0, 1), Vector2.one,
            new Vector2(0, prepend ? -extraHeight : 0), new Vector2(0, height));
        body.pivot = new Vector2(0.5f, 1);
        foreach (var child in originalChildren) child.SetParent(body, false);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = root;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        var scrollbarRect = Rect("Scrollbar", root, new Vector2(1, 0), Vector2.one, new Vector2(-5, 0), new Vector2(8, 0));
        CopyImage(scrollbarRect, view.Style.Track).color = new Color(0.5f, 0.48f, 0.43f, 0.2f);
        var scrollbarHandle = Rect("Handle", scrollbarRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var scrollbarImage = CopyImage(scrollbarHandle, view.Style.Track);
        scrollbarImage.color = new Color(0.5f, 0.46f, 0.37f, 0.8f);
        var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = scrollbarHandle;
        scrollbar.targetGraphic = scrollbarImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalNormalizedPosition = 1;
        return content;
    }

    private static void AddFrameRatePanel(View view, Transform parent, float top, float width)
    {
        var display = Rect("GCModFrameRateSettings", parent, new Vector2(0, 1), Vector2.one,
            new Vector2(0, -top), new Vector2(0, FrameRatePanelHeight - 16));
        display.pivot = new Vector2(0.5f, 1);
        CopyImage(display, view.Style.Panel).raycastTarget = false;
        Label(view, display, "프레임 제한", 30, 14, width - 60, 40, 31).alignment = TextAlignmentOptions.Center;
        var line = Rect("TitleLine", display, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(width - 100, 15));
        CopyImage(line, view.Style.TitleLine).raycastTarget = false;
        Label(view, display, "최대 프레임", 40, 82, width - 380, 52, 31);
        view.FrameRateChoices[0] = Button(view, display, "30 FPS", width - 310, 82, 130, 52, null, () => DialogueSettings.PreviewFrameRate(30));
        view.FrameRateChoices[1] = Button(view, display, "60 FPS", width - 170, 82, 130, 52, null, () => DialogueSettings.PreviewFrameRate(60));
        view.FrameRateHint = Label(view, display, "", 40, 144, width - 80, 36, 26);
    }

    private static void AddRow(View view, RectTransform parent, bool cg, float x)
    {
        int index = cg ? 1 : 0;
        float width = view.Width / 2 - 70;
        var row = Box(cg ? "CgOpacity" : "NormalOpacity", parent, x, 82, width, 110);
        Label(view, row, cg ? "CG 대화창 불투명도" : "일반 대화창 불투명도", 0, 0, width, 38, 31);
        var valueBox = Box("ValueBox", row, width - 130, 64, 130, 38);
        CopyImage(valueBox, view.Style.ValueBox).raycastTarget = false;
        view.Values[index] = Label(view, valueBox, "", 4, 0, 122, 38, 27, view.Style.Value);
        view.Values[index].alignment = TextAlignmentOptions.Center;

        // Leave space for the round thumb at both endpoints, clear of +/- buttons.
        var sliderRect = Box("Slider", row, 100, 52, width - 340, 58);
        // A generous hit area around the narrow native track also supports touch/drag.
        sliderRect.gameObject.AddComponent<Image>().color = Color.clear;
        var track = Rect("Track", sliderRect, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 12));
        CopyImage(track, view.Style.Track).raycastTarget = false;
        var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CopyImage(fill, view.Style.Fill).raycastTarget = false;
        var handleArea = Rect("HandleArea", sliderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var handle = Rect("Handle", handleArea, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -4), new Vector2(58, 58));
        var handleImage = CopyImage(handle, view.Style.Handle);
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.colors = view.Style.Colors;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        view.Sliders[index] = slider;
        // This IL2CPP build does not marshal UnityAction<float>'s argument reliably.
        // Slider has already set its value before invoking the event; read that field instead.
        UnityAction<float> change = (UnityAction<float>)(Action<float>)(_ => DialogueSettings.Preview(cg, slider.value / 100f));
        view.Listeners.Add(change);
        slider.onValueChanged.AddListener(change);
        Button(view, row, "-", 0, 52, 58, 58, view.Style.Minus, () => DialogueSettings.Preview(cg, (cg ? DialogueSettings.Edit.Cg : DialogueSettings.Edit.Normal) - 0.01f));
        Button(view, row, "+", width - 200, 52, 58, 58, view.Style.Plus, () => DialogueSettings.Preview(cg, (cg ? DialogueSettings.Edit.Cg : DialogueSettings.Edit.Normal) + 0.01f));
    }

    private static Image FindImage(Transform root, string name)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            if (image.name == name && image.sprite != null) return image;
        throw new InvalidOperationException("Native option image missing: " + name);
    }

    private static Image CopyImage(RectTransform rect, Image source)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = source.sprite;
        image.type = source.type;
        image.color = source.color;
        image.preserveAspect = source.preserveAspect;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        return image;
    }

    private static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
        rect.pivot = new Vector2(0, 1);
        return rect;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TextMeshProUGUI Label(View view, Transform parent, string text, float x, float y, float width, float height, float size, TextMeshProUGUI source = null)
    {
        source ??= view.Style.Label;
        var rect = Box("Label", parent, x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = size;
        label.fontStyle = source.fontStyle;
        label.characterSpacing = source.characterSpacing;
        label.color = source.color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static TextMeshProUGUI Button(View view, Transform parent, string text, float x, float y, float width, float height, Image icon, Action action)
    {
        var rect = Box("Button_" + text, parent, x, y, width, height);
        var image = CopyImage(rect, view.Style.Button);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = view.Style.Colors;
        TextMeshProUGUI label = null;
        if (icon != null)
        {
            var iconRect = Rect("Icon", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(40, 40));
            CopyImage(iconRect, icon).raycastTarget = false;
        }
        else
        {
            label = Label(view, rect, text, 8, 0, width - 16, height, 26);
            label.alignment = TextAlignmentOptions.Center;
        }
        UnityAction callback = (UnityAction)action;
        view.Listeners.Add(callback);
        button.onClick.AddListener(callback);
        return label;
    }
}
