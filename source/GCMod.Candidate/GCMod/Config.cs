using System;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;
using Utility.Notifications;

namespace GCMod;

public static class Config
{
	public static ConfigEntry<int> FrameRate;

	public static ConfigEntry<bool> Translation;

	public static ConfigEntry<string> TranslationCDN;

	public static ConfigEntry<string> TranslationLanguage;

	public static ConfigEntry<bool> AsyncMode;

	public static ConfigEntry<string[]> MasterDataTables;

	public static ConfigEntry<string> FontBundlePath;

	public static ConfigEntry<float> NormalAlpha;

	public static ConfigEntry<float> CgModeAlpha;

	public static ConfigEntry<bool> ModifyText;

	public static ConfigEntry<string> NameTextColorHex;

	public static ConfigEntry<string> MessageTextColorHex;

	public static ConfigEntry<float> FaceDilate;

	public static ConfigEntry<string> OutlineColorHex;

	public static ConfigEntry<float> OutlineWidth;

	public static ConfigEntry<float> OutlineSoftness;

	public static ConfigEntry<float> CharacterSpacing;

	public static ConfigEntry<string> CustomTranslationPath;

	public static Color NameTextColor { get; private set; } = Color.white;


	public static Color MessageTextColor { get; private set; } = Color.white;


	public static Color OutlineColor { get; private set; } = new Color(0.235f, 0.235f, 0.235f);


	public static void Initialize()
	{
		BindAllEntries();
		BindColorEntries();
		BindSettingChangedLog();
	}

	private static void BindAllEntries()
	{
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Expected O, but got Unknown
		FrameRate = Plugin.ConfigFile.Bind<int>("General", "FrameRate", 60, "프레임 제한. 게임 옵션에서 30/60 FPS를 선택하고 저장할 수 있습니다. 0 이하는 게임 기본값을 따르는 고급 설정입니다. 다른 양수는 기존 사용자 값을 보존하지만 이번 UI의 검증 범위는 30/60입니다.");
		CustomTranslationPath = Plugin.ConfigFile.Bind<string>("Translation", "CustomPath", "", "고급 사용자 번역 폴더의 절대 경로. 비워 두면 BepInEx/plugins/GCMod/translations를 사용합니다.");
		Translation = Plugin.ConfigFile.Bind<bool>("Translation", "Enabled", true, "대사·Master·UI·이미지 번역 적용을 제어하는 진단용 설정. 이미 표시된 내용과 폰트까지 원래 상태로 복원하는 통합 전환은 아닙니다.");
		TranslationCDN = Plugin.ConfigFile.Bind<string>("Translation", "CDN", "https://raw.githubusercontent.com/kekwkekw/gctrans/main", "시작 시 번역 manifest 확인 및 변경 데이터 갱신. offline으로 설정하면 네트워크를 사용하지 않습니다. DLL/폰트는 자동 갱신하지 않습니다.");
		TranslationLanguage = Plugin.ConfigFile.Bind<string>("Translation", "Language", "ko", "번역 폴더의 locale. 한국어 패키지: ko");
		AsyncMode = Plugin.ConfigFile.Bind<bool>("Translation", "AsyncMode", false, "고급 대사 번역 비동기 로딩. false이면 대사와 Master 진입 시 최대 10초 기다립니다. 시간 초과 시 원문으로 진행하고 백그라운드 로딩을 계속합니다. Master는 이 값과 무관하게 같은 대기 상한을 사용합니다.");
		if (!TomlTypeConverter.CanConvert(typeof(string[])))
		{
			TomlTypeConverter.AddConverter(typeof(string[]), new TypeConverter
			{
				ConvertToString = (object value, Type _) => string.Join(", ", (string[])value),
				ConvertToObject = (string value, Type _) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			});
		}
		MasterDataTables = Plugin.ConfigFile.Bind<string[]>("Translation.MasterData", "EnabledTables", new string[1] { "*" }, "진단용 Master 번역 테이블 선택. 기본값 *는 전체 활성화입니다. mItems, mUnits처럼 쉼표로 구분하며 빈 값은 전체 비활성화합니다. 대소문자를 구분하고 변경 후에는 게임 재시작을 권장합니다.");
		FontBundlePath = Plugin.ConfigFile.Bind<string>("Translation.Font", "AssetBundlePath", "GCMod/fonts/RHR_Medium_GFSAFE_ADH.bundle", "검증된 한국어 TMP 폰트. plugins 기준 상대 경로 또는 절대 경로");
		NormalAlpha = Plugin.ConfigFile.Bind<float>("Message.Window", "NormalAlpha", 0f, "일반 대화창과 화자명 배경 불투명도. 0=투명, 1=불투명. 게임 옵션에서 미리보기 후 저장할 수 있습니다.");
		CgModeAlpha = Plugin.ConfigFile.Bind<float>("Message.Window", "CgModeAlpha", 0f, "CG 대화창 배경 불투명도. 0=투명, 1=불투명. 게임 옵션에서 미리보기 후 저장할 수 있습니다.");
		ModifyText = Plugin.ConfigFile.Bind<bool>("Message.Text", "Modified", true, "대화 글자 가독성 보정. 게임 옵션에서 미리보기 후 설정 저장을 눌러 적용합니다.");
		NameTextColorHex = Plugin.ConfigFile.Bind<string>("Message.Text", "NameColor", "FFFFFFFF", "화자명 보정 색상. RGB만 적용하고 글자 표시/페이드의 알파는 게임이 제어합니다.");
		MessageTextColorHex = Plugin.ConfigFile.Bind<string>("Message.Text", "MessageColor", "FFFFFFFF", "본문·루비 보정 색상. RGB만 적용하고 글자 표시/페이드의 알파는 게임이 제어합니다.");
		FaceDilate = Plugin.ConfigFile.Bind<float>("Message.Text", "FaceDilate", 0.3f, "고급 글자 굵기 보정. 설정 범위 -1~1.");
		OutlineColorHex = Plugin.ConfigFile.Bind<string>("Message.Text", "OutlineColor", "3A3A3AFF", "고급 글자 외곽선 색상. 16진수 RGB 또는 RGBA 형식.");
		OutlineWidth = Plugin.ConfigFile.Bind<float>("Message.Text", "OutlineWidth", 0.3f, "고급 글자 외곽선 두께. 설정 범위 0~1.");
		OutlineSoftness = Plugin.ConfigFile.Bind<float>("Message.Text", "OutlineSoftness", 0.01f, "고급 글자 외곽선 부드러움. 설정 범위 0~1.");
		CharacterSpacing = Plugin.ConfigFile.Bind<float>("Message.Text", "CharacterSpacing", 0f, "고급 본문 자간. 기본값 0. 가독성 보정이 켜져 있을 때 사용하며 배치는 다음 대사부터 반영합니다. 화자명에는 적용하지 않습니다.");
	}

	private static void BindSettingChangedLog()
	{
		Plugin.ConfigFile.SettingChanged += delegate(object _, SettingChangedEventArgs e)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Expected O, but got Unknown
			ConfigEntryBase changedSetting = e.ChangedSetting;
			if (Services.DialogueSettings.Committing && (changedSetting == NormalAlpha || changedSetting == CgModeAlpha || changedSetting == ModifyText || changedSetting == FrameRate)) return;
			object obj = ((changedSetting.BoxedValue is string[] value) ? string.Join(", ", value) : changedSetting.BoxedValue);
			Plugin.Log.LogInfo($"[{changedSetting.Definition.Section}] {changedSetting.Definition.Key} => {obj}");
			Toast.Info("[" + changedSetting.Definition.Section + "]", $"{changedSetting.Definition.Key} => {obj}");
		};
	}

	private static void BindColorEntries()
	{
		(ConfigEntry<string>, Action<Color>)[] array = new(ConfigEntry<string>, Action<Color>)[3]
		{
			(NameTextColorHex, delegate(Color c)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				NameTextColor = c;
			}),
			(MessageTextColorHex, delegate(Color c)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				MessageTextColor = c;
			}),
			(OutlineColorHex, delegate(Color c)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				OutlineColor = c;
			})
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (entry, setter) = array[i];
			ParseAndSetColor(entry, setter);
			entry.SettingChanged += delegate
			{
				ParseAndSetColor(entry, setter);
			};
		}
	}

	private static void ParseAndSetColor(ConfigEntry<string> entry, Action<Color> setter)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Color obj = default(Color);
		if (ColorUtility.TryParseHtmlString(NormalizeHexColor(entry.Value), out obj))
		{
			setter(obj);
		}
	}

	private static string NormalizeHexColor(string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			return "#FFFFFF";
		}
		hex = hex.Trim().TrimStart('#');
		return "#" + hex;
	}
}
