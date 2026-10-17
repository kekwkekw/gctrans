# UI 회귀 r2 개발 기준선과 Stable 승격 계획

역사 기록: 아래는 데이터가 같았던 r2 승격 계획이다. 현재 종합 복원 후보에는 새 producer JSON과 full profile이 있으므로 DLL-only·cfg/store 불변 계획을 그대로 적용하지 않는다. 현재 업그레이드/rollback은 [RESTORATION_MIGRATION_KO.md](RESTORATION_MIGRATION_KO.md)를 따른다. 이 문서의 보존이나 로컬 통합이 영구 승격·공개 권한을 만들지 않는다.

상태: **UI_REGRESSION_R2_LOCAL_INTEGRATION_READY**. 아래는 향후 별도 승인 뒤의 최소 전환 계획이며, 현재 설치·cfg·store를 변경하는 실행 지시가 아니다.

## 기준선과 증거

현재 확인한 public main은 `82a8b0f7ef2c2760fe7139707fda69030eb4571c`, tree `d91fc64436b2b64ec192213723c2b48e8d6a6c72`다. 로컬 이력은 다음과 같다.

```text
82a8b0f (public main)
  → 5a5d9ee (병합 후 운영 문서)
  → 448a601 (일반 UI font-role 및 Footer 경계)
  → b74b435 (실제 SDF/material 수정 및 최종 native 결과)
```

검증된 제품 source/DLL 기준 commit은 `b74b435a72e90a5eb9295d2a502a4a045f20c1d8`, tree는 `55ef98b067ae5ec564466982c60e008f1e61d43c`다. 이 문서의 후속 commit은 계획 문서만 추가하며 제품 파일은 바꾸지 않는다.

- Dev DLL SHA256: `6b3f76789eecf50cbea49840ee8a5d5d7c743844f6af40c26b6268fd80ba8c5e`.
- 기존 Stable DLL SHA256: `63c548758d06fb3194927db02458d0885dc1d8e3d5ac1cbfe7a0be311e4c5e43`.
- 원본 `gc_ui_regression_dev_20261016_r2.zip`의 `RESULT.json`, `cpu/SUMMARY.json`, `TARGET_NATIVE_RESULTS.json`, `STABLE_RESTORE_RESULT.json`, 최종 `DEV_6B3F_*` 로그/crop을 참조한다. 과거 b28/intermediate 실행을 최종 DLL 증거로 쓰지 않는다.
- 같은 최종 DLL의 CPU 52/52, 메뉴 9개·Footer 8개 정착/재열기, 정확한 Stable 원복 PASS를 재사용한다. 제품 bytes가 같아 재빌드·CPU/native 재실행은 하지 않았다.

확인은 `git diff --exit-code b74b435a72e90a5eb9295d2a502a4a045f20c1d8 HEAD -- source tools tests candidate translations`와 `Get-FileHash candidate/GCMod.dll -Algorithm SHA256`으로 가능하다. 식별값은 이번 입력 확인용이며 일상 runtime pin이 아니다.

## 유지하는 제품 경계

GeneralUiFontPolicy는 필요한 한글을 현재 승인 Medium에서, 전체 표시 문자열을 실제 GameFont/fallback에서 확인한다. 현재 지원 formatting만 투영하고 unknown tag·stale donor·required glyph/primary route 부재는 거부한다. 전역 FontService 계약은 유지한다.

Footer ink/material 판정은 typed footer owner→직접 button→현재 TMP가 확인된 대상에만 적용한다. 실제 overflow·mask·glyph/submesh channel·source/owner/control·조건부 복구를 유지하며 다른 renderer와 Story/Choice/Battle로 확대하지 않는다. 번역 JSON·등록·Master/Names/Novel·Fit3 수치는 변경하지 않는다.

## 설치 전 읽기 전용 확인

1. 실제 PC의 정상 권한 PowerShell에서 `Get-CimInstance Win32_Process -Filter "Name='GC.exe'" -ErrorAction Stop`으로 확인한다. 조회 실패는 UNKNOWN이다. sandbox의 빈 목록만으로 종료를 판정하지 않는다. 실행 중이면 강제 종료하거나 DLL을 교체하지 않는다.
2. `<game>/BepInEx/plugins/GCMod.Nextgen/GCMod.dll`, `GCMod.cfg`, `GCMod.Nextgen.local`의 존재·목록·SHA를 새로 기록한다. plugin 검색 범위의 본체/GUID 중복과 old 8-addon 혼합을 읽기 확인한다. 예상 외 bytes는 보존하고 그 경로의 전환만 보류한다.
3. cfg의 PackageRoot·Language·Translation/Nextgen Enabled·CheckUpdate·UiRequestsEnabled, 승인 Medium 경로, 현재 선택의 release/UI manifest를 읽는다. `current.json`·`previous.json`·`pending_restart.json`은 각 존재/비존재와 내용을 별도로 기록한다. PackageRoot가 store 격리를 뜻하지 않는다.
4. 실제 cfg와 기존 store 전체를 설치/검색 경로 밖에 독립 복사하고 목록·SHA를 대조한다. immutable package와 선택 기록을 모두 보존하며 원래 존재한 store를 빈 store로 취급하지 않는다. 과거 backup/receipt만으로 현재 상태를 가정하지 않는다.

## 승인 뒤 최소 적용 경로

| 역할 | 처리 |
|---|---|
| `BepInEx/plugins/GCMod.Nextgen/GCMod.dll` | 정상 종료·backup 검증·expected-current CAS 뒤 검증된 Dev bytes 한 개로 교체 |
| `BepInEx/config/GCMod.cfg` | 편집하지 않고 기존 사용자 설정 유지 |
| `BepInEx/config/GCMod.Nextgen.local` | 초기화·삭제·임의 selection/pending 편집 없이 기존 상태 유지 |
| `BepInEx/plugins/GCMod/Utility.dll`, 승인 Medium, core/interop/map, 다른 plugin | 변경·이동하지 않음 |
| 기존 translations/PackageRoot와 수락 데이터 | 변경하지 않음 |

기존 Stable의 Enabled=true/Translation=true/ko와 CheckUpdate=false/UiRequestsEnabled=false/XUnity OFF가 유지되는지 확인한다. 전체 table `*`를 켜지 않는다. 필요한 기존 사용자 table 설정을 보존한다. 새 폰트/DLL 다운로드나 인터넷 신규 번역 자동 적용은 없다.

code/DLL 변경은 재시작 대상이다. live UI request나 prepare-local을 DLL 교체로 사용하지 않는다. 데이터가 같으므로 새 package/current/pending을 만들 필요가 없다. 기존 pending이 있으면 다음 시작이 무엇을 선택할지 검증하고, 검증되지 않은 변경을 자동 소진하거나 덮지 않는다. 임시 파일은 plugins 밖에서 준비한다. 개별 temp+replace를 쓰더라도 여러 경로가 함께 atomic하다고 주장하지 않는다.

## 최소 실제 사용 확인과 복구

향후 설치 승인 범위에서 확인된 DMM 런처를 인자 없이 실행하고 현재 목록의 girlscreation_r를 확인한다. GC.exe 직접 실행이나 과거 index/window/좌표는 쓰지 않는다. startup/Core의 설정·닫기, 메뉴 9개/공백 문구, Footer 8개의 정착·닫기/재열기를 확인한다. 정상 종료 후 한 번 재시작해 같은 단일 DLL/선택 구성과 대표 표시가 유지되는지만 확인한다. Sort/Choice/Museum/Fit3/Battle/WB 전수는 반복하지 않는다.

실패하면 정상 종료와 실제 process0을 확인한다. 설치 DLL이 자신이 적용한 Dev SHA일 때만 검증된 원래 Stable bytes를 원래 경로로 복원한다. 원본 cfg가 그대로면 쓰지 않는다. store도 무조건 backup으로 덮거나 지우지 않는다. startup이 바꾼 파일은 before/after 및 writer 근거와 expected postimage가 확인된 자기 write-set에 한해 CAS로 복구한다. 원래 없던 파일도 자신이 만든 파일만 제거한다. 외부 동시 변경/unknown bytes는 보존하고 충돌을 보고한다.

원본 DLL/cfg·store 목록/bytes/선택의 존재·비존재·다른 plugin이 정확히 복원됐을 때만 복구 PASS다. 임의 store 삭제, force overwrite, 재화/세이브/진행도/잠금 복구는 하지 않는다. NG10의 store 비존재 helper를 이 상시 설치에 그대로 실행하지 않는다.

## 별도 lane와 남은 결정

PRO04의 18,366개 괄호 후보와 PRO05 Museum 3건은 별도 proposal/custody로 보존한다. PRO06 후속 3개 보완은 실제 구현 ref·최종 DLL·JSON/reload 실기 완료를 확인하기 전에는 이 기준선에 병합하지 않는다. future source 연결 시 GeneralUiProvider.cs/Geometry.cs/UiModules.cs의 두 변경을 역할별로 조정해야 한다. Museum 전용 owner/JP envelope를 Footer로 확대하거나 Museum의 KO equality 예외로 공통 font-role 경로를 덮지 않는다. Museum 측 manifest에는 별도 괄호 후보 lineage가 있으므로 통째 복사하지 않는다.

`感情資源増量中！` M80/F1000, 미등록 `藝晶石所持状況`/`キャンペーン`, `20003601` NEW/미관측, Museum 후속 범위를 유지한다. strict async UNPROVEN, Choice Release HELD, Battle NOT_OBSERVED, source PARTIAL 및 UI21/UI22 ACCEPTED_LOCAL_FROZEN/Known Residual은 승격하지 않는다.

개인 PC 영구 전환, GitHub push/PR/main merge, Release/tag/CDN/Pages는 각각 별도 실제 승인 범위다. 이 문서와 로컬 commit은 그 권한을 만들지 않는다.
