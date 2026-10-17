# 일반 UI 표시 회귀: Dev 검증과 Stable 원복

역사 기록: 이 문서는 r2의 데이터 동일 DLL 교정·시험 당시 결과다. 현재 종합 복원 업그레이드는 [RESTORATION_MIGRATION_KO.md](RESTORATION_MIGRATION_KO.md)의 DLL+완전 JSON+명시 profile+실제 선택 단위를 따른다. 아래의 cfg/store 불변 시험 조건을 새 producer 복원 전환의 DLL-only 절차로 사용하지 않는다.

상태: **UI_REGRESSION_DEV_NATIVE_VERIFIED_STABLE_RESTORED**. 표적 메뉴/Footer의 결과이며 전체 UI 재수락이나 Stable 영구 교체가 아니다.

## 최종 식별과 실제 결과

- 최종 Dev DLL: `6b3f76789eecf50cbea49840ee8a5d5d7c743844f6af40c26b6268fd80ba8c5e`.
- 시험 전/원복 Stable DLL: `63c548758d06fb3194927db02458d0885dc1d8e3d5ac1cbfe7a0be311e4c5e43`.
- 실제 selected current: `7643c7b8e776d9a25c73c7f3c3f0d9ec97a8c358ed7c3e9c1d812d0bfab50401`, UI generation 1. cfg/JSON/store 선택을 바꾸지 않았다. NG03 이름은 버전 판정에 쓰지 않는다.
- 동일 최종 DLL에서 메뉴 9개와 Footer 8개가 한국어로 정착하고 닫기/재열기 후 유지됐다. 공백 포함 메뉴 4개도 실제 화면과 `SETTLED_M0_F0`로 확인했다.
- Footer는 16개 활성 binding 중 실제 writer 8개가 정착했다. 같은 대상의 나머지 binding은 이미 쓰인 KO 때문에 exact-JP 조건이 달라 독립적으로 쓰지 않았다. 이를 8개 표시 누락 또는 16개 독립 native PASS로 해석하지 않는다.
- 표적의 geometry overflow/clip/channel 실패 원복은 0건이다. 메뉴 닫기의 `OwnerRebound M1/F1` 원복은 정상 lifecycle이며 재열기에서 새 owner가 정착했다. 별도 `gc.headertopcontroller`의 `7B9736717208` M80/F1000은 남겨서 보고한다.
- 정상 종료 후 Stable DLL, cfg, 기존 store 1,908개 파일, plugin DLL 27개가 원본 목록/SHA와 일치했다. Dev 경로는 제거됐다. 이 수는 이번 파일 관측치이며 제품 gate가 아니다.

## 원인과 수정

일반 UI의 기존 `FontService.Supports(전체 KO)`는 승인 Medium에 없는 U+0020과 formatting tag까지 Medium glyph로 요구했다. `GeneralUiFontPolicy`/`IGeneralUiFontTarget`을 실제 host/Unity target에 연결해 한글은 현재 Medium, 전체 표시 문자는 현재 GameFont/fallback으로 검증한다. 현재 지원 color/cspace formatting만 투영한다. unknown/손상 tag, richText OFF의 실제 문자, donor profile/instance/generation 및 main-thread/lifetime 검사는 유지한다. 전역 FontService와 Story/Choice/Battle 계약은 바꾸지 않았다.

Footer의 일본어 원문부터 작은 TMP leaf를 벗어나고 직접 button 하단도 약 1~2.5 단위 벗어났다. 실제 material은 `TextMeshPro/Mobile/Distance Field`, `UNDERLAY_ON`, face dilate/outline/softness 0이었다. 예를 들어 편성 원문의 button 하단 -69에 대해 padded quad 하단은 -71.10, 검증된 face metric 하단은 -66.84였다. 원문의 shadow/SDF 여백을 문자 본체의 overflow로 취급했던 것이다.

기존 typed owner→holder→button→TMP/exact path가 맞는 Footer에서만 현재 glyph metrics와 실제 material을 사용한다. face/outline의 내용 경계는 직접 button 안에 있어야 하며, shadow 포함 전체 quad는 기존 active mask와 실제 glyph/submesh renderer channel 검사를 계속 통과해야 한다. fontSize/rect/cull/mask/material/원문 모델/controls를 쓰지 않는다. unsupported style/material/effect/metric은 quad 검사로 돌아가고 malformed 값은 통과하지 않는다. 다른 renderer의 strict leaf 검사는 유지한다.

중간 시험에서는 원문 preflight 보류, material 정보 경로, keyword/property 조회 예외를 각각 분리해 고쳤다. 실제 CanvasRenderer material과 현재 Unity의 keyword 배열/int property 경로를 사용하며 unknown ABI나 hook 우회는 없다. 최종 native에서 8개 Footer 모두 `LEAF0_BUTTON1`, `SETTLED_M0_F0`가 나왔다. bounded 단계/실패 mask/숫자 진단은 전체 tree/주소/JP/KO의 frame 로그를 만들지 않는다.

## 재현과 검사 경계

Build PASS: warnings 0/errors 0. 동일 최종 assembly의 CPU 52 PASS: UiRegression 15, Ng08Consumers 20, Ng09Heading 17. 명시 mock font/target 입력의 CPU 검사이며 native proof로 대체하지 않는다.

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=<existing-game-root> -p:NuGetAudit=false
dotnet run --project tests/UiRegressionTests/UiRegressionTests.csproj -c Release -p:NuGetAudit=false
dotnet run --project tests/Ng08ConsumersTests/Ng08ConsumersTests.csproj -c Release -p:CandidateAssembly=<absolute-built-Dev-GCMod.dll> -p:NuGetAudit=false
dotnet run --project tests/Ng09HeadingTests/Ng09HeadingTests.csproj -c Release -p:CandidateDllPath=<absolute-built-Dev-GCMod.dll> -p:NuGetAudit=false
```

별도 human 승인에 따라 설치 밖 검증 백업, 단일 Dev 일시 전환, 기존 DMM launcher, 표적 메뉴/Footer 확인, 정상 종료, exact 원복을 수행했다. 기존 store를 비존재로 가정하지 않았다. cfg/번역 편집·store 초기화·영구 전환·공개 작업은 0이다. sandbox의 process 0만으로 종료를 판단하지 않고 실제 창 부재 및 설치 밖 CIM GC.exe 0도 확인했다. 실행 중이던 다른 게임은 건드리지 않았다.

`藝晶石所持状況`, `キャンペーン`은 실제 source/TMP locator가 필요한 별도 coverage다. 미관측 활성 binding은 데이터 누락과 구분한다. 번역 의미값·등록/disabled/HOLD·Master/Names/Novel·Fit3 수치는 불변이다. strict async UNPROVEN, Choice Release HELD, Battle NOT_OBSERVED, source PARTIAL과 UI21/UI22 ACCEPTED_LOCAL_FROZEN/Known Residual을 유지한다. 전수 WB/크롤링/전체 UI 시험은 하지 않았다.

Stable 영구 전환과 public push는 이번 결과에 포함되지 않는다. 원본 및 기존 결과는 보존하고 source diff/실제 최종 Dev DLL/표적 로그·crop/CPU·원복 기록만 별도로 반환한다.
