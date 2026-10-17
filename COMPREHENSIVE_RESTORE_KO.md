# 수락 UI 범위 종합 복원 Dev 후보

이 문서는 종합 복원 r1의 변경 역할을 보존한 이력이다. 현재 geometry 최종본의 main 통합 후보와 구 client/62-profile 전환 안내는 [RESTORATION_MIGRATION_KO.md](RESTORATION_MIGRATION_KO.md)를 우선한다. 아래 r2 기준 SHA·5-table smoke·당시 실행 보고를 현재 제품의 축소된 지원 범위나 현재 미복원으로 읽지 않는다.

이번 closeout의 실제 callback 역할·popup/미션 cell 보완·일관 DLL/data/profile 전환과 원복은 [RESTORATION_CLOSEOUT_KO.md](RESTORATION_CLOSEOUT_KO.md)를 함께 따른다. 실행 결과는 전달된 전체 scope 표와 DLL별 증거 수준으로 구분한다.

기준은 r2 통합 commit `3c1455091dfc9dc57863d7541d900008eff37bc1`이다. 이 후보는 개인 PC의 r2 Stable을 영구 교체하거나 공개 main을 변경하지 않는다. 최종 실행 결과와 DLL/source 식별은 함께 반환한 `RESTORATION_RESULT_KO.md`와 `RESULT.json`을 따른다.

## 적용 프로필

수락 Master 데이터 보유와 실제 table 활성은 별개다. 기존 5-table 설정은 검증에 사용된 작은 표본이며 전체 수락 범위의 설정이 아니다. `staging/accepted-master-profile.json`과 `.cfg.fragment`는 수락된 table의 명시 목록이다. `*`나 미래 table 자동 활성화가 아니다. fragment를 전체 사용자 cfg에 덮어쓰지 않는다. Master는 로드 이전 경계이므로 profile 변경에는 정상 재시작이 필요하다. 모든 field의 CPU 검사와 실제 load 및 화면 관측은 서로 다른 축이다.

## 제품 변경

- Core type0 literal은 정확한 expected metadata key를 source identity로 검증한다. key가 비어 있지 않다는 이유만으로 거부하거나 Master lookup으로 바꾸지 않는다. 초기 producer와 소유 중 source 검사가 같은 계약을 사용한다.
- Options는 실제 typed area reference와 area 내부의 정확한 경로를 사용한다. dynamic root wrapper 이름을 선결조건으로 삼지 않는다. 비활성 화면 설정 후보는 자동 활성화하지 않는다.
- Battle caption 및 completed command-cost의 폰트 검사는 필요한 한글 Medium coverage와 실제 GameFont/fallback의 전체 visible 문자를 나눈다. 지원 formatting만 투영하며 native 숫자·공백·CRLF·tag suffix는 보존한다. getter 재호출, 재계산, AutoLease는 없다.
- UI6의 수락된 여섯 consumer 역할은 CheckBox/Toggle, Equipment Sort rarity/acquisition, Shop frame/section을 각각 현재 typed producer와 대조한다. 다른 disabled 후보는 보존한다. Shop frame과 section은 서로 다른 compiled selector slot이며 중복 validator를 우회하지 않는다.
- 진단은 정확한 callback 요청 및 binding·owner generation·phase·reason의 유한한 코드를 사용한다. hook 설치 하나, WRITE 하나 또는 안전 disarm을 전체 scope 표시 PASS로 해석하지 않는다.

## 추가로 확인한 표시 계약

Options의 즉시 표시 checkbox는 실제 native 선언 caption과 전체 버튼 container를 대조하고, 빈 targetGraphic 목록을 membership 성공으로 바꾸지 않는다. Filter의 reset/전체 선택 해제는 각 typed 버튼과 native ContentFitDriver를 대조한다. Category option은 현재 Setting/unique value/toggle/target이 같고 실제 TMP AutoSizing의 enable/min/max가 유지될 때만 native fontSize 정착을 허용한다. 숫자 범위 밖이나 수동 크기 변경에는 기존 차단을 유지한다.

Character 메뉴의 artist_enhance caption만 실제 `_unitEnhance`의 직접 text와 현재 scene/control을 검증해 다른 형제의 애니메이션과 분리한다. 자신의 rect/container/fitter/glyph/material/control 검사는 유지한다. Header power와 Shop header도 각각 실제 typed container를 확인하며 다른 renderer의 검사로 확대하지 않는다. Campaign은 실제 hidden/invalid target에 쓰지 않고 현재 표시 자격이 확인될 때까지 보류한다.

## 최소 재현

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=<READ_ONLY_GAME_DEPENDENCY_ROOT> -p:UseSharedCompilation=false
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:UseSharedCompilation=false
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <NEW_ISOLATED_STORE> --package <CANDIDATE_ROOT>/translations/ko --based-on NONE
```

CPU 재현은 `tests/ComprehensiveCoreTests`(repository root 인자), `ComprehensiveBattleTests`(translations/ko 인자), `ComprehensiveChromeTests`, `ComprehensiveDiagnosticsTests`, `ComprehensiveShopTests`, `UiRegressionTests`, `Ui6RestorationTests`(bindings 경로와 새 report 경로 인자)의 각 csproj를 Release 빌드하고 net9.0 DLL을 실행한다. 기존 수락-derived Master fixture는 `tests/ComprehensiveMasterTests`의 README를 따른다. 이 명령은 native 검증을 대신하지 않는다. 필수 source와 외부 dependency 역할은 `EXTERNAL_DEPENDENCIES_KO.md`, 결과별 정확한 실행은 반환 보고서를 따른다.

## 운용과 보존

data updater는 DLL·font를 설치하지 않는다. 기존 단일 coordinator/current/previous/pending_restart/request/LKG와 UI-only legacy session 보존을 유지한다. 일반 교정은 `OPERATOR_KO.md`의 reviewed apply→next_package를 사용한다. 이번의 accepted producer metadata/consumer 활성 복원과 새 의미의 KO 교정은 구분한다.

UI21/UI22 ACCEPTED_LOCAL_FROZEN, 원형과 외부 ledger, Master/Names/Novel의 값과 Novel 등록은 보존한다. Library 접근 제한을 identity 추정으로 채우지 않는다. KEEP_SOURCE/HOLD 복구 목록은 근거를 가진 역사 대조이며 새 의미 승인이나 일괄 활성화가 아니다.

r2의 공통 font-role/Footer 판정, Fit3 35→34→33→32→31, Story/Choice/Annotation/standalone Dictionary/Master의 역할과 WB13~M4는 유지한다. PRO04/05/06 Museum 변경을 자동 병합하지 않는다. strict async UNPROVEN, Choice Release HELD, 비용 없는 completed-param 미관측, source coverage PARTIAL은 독립 축으로 기록한다. 새로 확인된 미복원을 기존 Known Residual로 바꾸어 닫지 않는다.

기본 opt-in/CheckUpdate/UiRequestsEnabled OFF와 XUnity OFF를 유지한다. Utility·승인 Medium·interop는 별도 입력이며 반환물에 넣지 않는다. 파일 FOUND는 ABI·glyph·hook PASS가 아니다. 상시 native 정책은 안전한 일시 시험과 정확한 원복의 권한이며 영구 설치나 공개 권한이 아니다.

## 다음 통합

후속 main 통합은 이 후보의 exact local commit/tree와 변경 path를 사용한다. public main에는 아직 이 복원과 r2 수정이 반영됐다고 주장하지 않는다. 별도 공개 승인 뒤 main merge SHA를 [UI 작업 계획 정리]의 기준으로 인계한다. 해당 작업에는 Museum 전용 owner/reload 로직을 별도로 대조하며 겹치는 `GeneralUiProvider.cs`, `Geometry.cs`, `UiModules.cs`를 통째로 덮어쓰지 않는다. PRO04 18,366 후보/HOLD와 20003601 NEW/미관측은 그대로다.
