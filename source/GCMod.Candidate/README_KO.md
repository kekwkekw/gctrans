# GCMod Korean LocalParity

런타임 기준: `kristina1Q/gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`.

`Plugin → TranslationManager / PatchManager` 구조와 Enhance, Translation, Visual,
UI, MasterData 훅을 사용한다. `GCMod.Nextgen.sources.props`를 import하지 않는다.
기존 `source/GCMod.Nextgen`은 별도 authoring 도구의 소스 의존성으로 남으며 이 DLL에는 포함되지 않는다.

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=C:\path\to\girlscreation_r
python tools/export_local_ui.py
```

DLL은 기존 `candidate/GCMod.dll`에 생성된다. 빌드는 게임 설치 파일을 쓰지 않는다.

7.1.0 후보의 한국어 기본값은 `ko`, CDN은 공식 GitHub main
(`https://raw.githubusercontent.com/kekwkekw/gctrans/main`), Master는 `*`이다.
기존 7.0.0 offline 설치본은 GitHub 변경만으로 업그레이드되지 않는다.
새 DLL과 cfg를 한 번 적용한 뒤에만 데이터 자동 갱신이 가능하다.
자세한 동작과 실기 대기 항목은 [DATA_UPDATES_KO.md](../../docs/DATA_UPDATES_KO.md)를 참조한다.
배포 cfg도 `candidate/GCMod.cfg`에 보관한다. `python tools/check_local_master.py`는
cfg 비활성, Master 파일/테이블/필드/JP 키 부재를 별도 상태로 진단한다.
`--config <실제 cfg> --package <실제 ko 폴더> --log <로그>`로 설치 상태를 읽을 수 있다.
로그의 `NOT_OBSERVED`는 미번역이나 table OFF의 증거가 아니며 해당 세션의 rewrite 미관측을 뜻한다.
`translations/ko/{master.json,names.json,novels/}`를 local 소비자가 직접 읽는다.
`ui.json`은 `ui/general-exact.reviewed.json`, 기존 resources의 `translate` 쌍,
UI 전용 Master 필드에서 결정적으로 생성된다. binding 활성 여부와 kind/role로 번역을 제외하지 않는다.
동일 JP의 한국어가 충돌하면 명시적 reviewed exact를 사용하며, 미검수 충돌은 export를 실패시킨다.
local UI 전체 키 목록으로 누락을 검사한다. 입력 필드 보호는 runtime에서 유지한다.
입력 범위·우선순위·이미지 예외는 [UI_EXPORT_KO.md](../../docs/UI_EXPORT_KO.md)를 참조한다.
`python tools/export_local_ui.py --check`로 산출물 일치를 확인한다.
UI 훅은 ui.json을 읽고 Master 존재 여부를 묻지 않는다. exact 조회를 우선하며,
local의 앞뒤 공백 및 외곽 rich-text 태그 처리를 유지한다.
TMP/legacy 입력 컨트롤의 하위 텍스트와 local의 self-write 재진입을 보호한다.
단일 문자 제외는 local의 Novel 글자 렌더링 보호를 유지한 것이다.

기존 501개 authoring binding과 16개 동적 템플릿은 보존한다. 이 런타임의 binding 소비자는 없다.
구조 예외는 실제 Native에서 필요한 경우에만 작은 별도 패치로 추가한다.

선택지 예외는 `ChoicePatch`다. `EventSelect.AddSelectMessage`의
표시 문자열만 exact 조회로 번역하며 UnityAction/분기 콜백은 변경하지 않는다.
선택지 표시 중 프레임이 원래 화자명을 복원하는 경우, TMP 렌더 직전에도
`EventMessageParentFrame.MessageName`과 정확히 같은 컴포넌트에만 Names를 적용한다.
이름 사전을 일반 UI나 Novel 본문에 합치지 않는다.
`GlossaryPatch`는 용어 사전의 한국어 제목을 정렬하고 실제 목록 위치로 초성 색인을 만든다.
한국어 제목에 중복 표시되는 일본어 읽기는 사전 상세에서만 숨긴다.
장 번호는 `第9章` 같은 전체 문구에 한정해 검수된 `第{0}章` 형식을 사용한다.
이 세 보완의 빌드/비실기 검증과 별개로, Native 표시·색인 클릭·선택지 복귀는 아직 검증 대기다.
`ElementLabelPatch`는 `ElementTypeExtensions.ToName`의 속성 표시명만 번역한다.
Master에 없는 단독 `雷`를 처리하며 일반 UI/Novel의 한 글자 보호는 유지한다.
공방 번호 1~3은 기존 검수 템플릿을 export에서 exact 3항목으로 펼친다.
2026-10-28 실기 잔존 문구에는 `UiTextLookup`의 제한된 전체 문구 패턴을 추가했다.
시즌/층수/박스 회차/창작 횟수/개봉 횟수/남은 일수와 날짜+まで를 처리한다.
추가 실기에서 확인한 남은 수량/시간, 턴 수와 한국어 item/title 뒤의 두 해금 조건도 전체 문구로 처리한다.
숫자는 원문에서 보존하며, 문장 일부를 치환하는 범용 템플릿 엔진은 없다.
local에 대응 형식이 없는 更新まで / 開封期限…まで는 의도적으로 매칭하지 않는다.
던전의 활성화 후 갱신은 기존 setter/OnEnable 훅을 우회하므로
`TextMeshProUGUI.OnPreRenderCanvas` 직전에도 같은 입력 보호/사전 조회를 적용한다.
별도 selector, ownership, generation 또는 Native/Nextgen 계층은 추가하지 않는다.
회귀 검증: `dotnet run --project tests/UiTextLookup -- translations/ko/ui.json`.

Master hook은 local 구현을 사용하며 후보의 추가 ABI hardening은 이식하지 않았다.
upstream의 개발자 PC 고정 경로 Master 덤프만 제외했다.
검증된 `RHR_Medium_GFSAFE_ADH.bundle`을 local의 TMP fallback 로더로 사용한다.

제목 동기화는 `tools/sync_novel_titles.py --package translations/ko --source <raw-mNovels.json> --report <report.json>`.
동일 script ID의 accepted Novel 값만 사용한다. 유일한 전각 숫자/공백 대응은 authoring 단계에서만 처리한다.
Novel 본문과 Names는 수정하지 않는다. 대응 없음/중복 JP 번역 충돌은 보고서에 HOLD로 남긴다.

Master coverage 회복 파이프라인은 `tools/update/MASTER_COVERAGE_KO.md`를 참조한다.
