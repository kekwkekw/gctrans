# 문서 안내 — 현재 운용과 역사 기록

문서 본문은 이 `docs/` 폴더에 모았다. 루트 README는 짧은 진입점이다. **코드/CLI의 상대경로는 저장소 루트 기준**이며 문서 폴더에서 명령을 실행하지 않는다. 과거 commit에 고정된 문서와 수락 archive의 원형 경로는 수정하지 않는다.

## 현재 작업 순서

시나리오 교정은 PR27에서 완료했다. GitHub 문서/브랜치 정리 후 **로컬 Stable을 최신 main으로 업데이트 → GC_UI_EXPECTED_BASELINE 누적 자료 검증 → 누락/추가 UI 종합 조사 → 근거가 확보된 UI 구현** 순으로 진행한다. 예전 5-table/r2를 개발 또는 복구 기준으로 사용하지 않는다.

현행 공급 ContentId `efb4e3d02f38822ce3e5f50ac01dbbc8d505387b9bf8903bdb9b0b424d61946a`와 DLL `96114569fbe38f113d49ad5e9554801ad1e55c6f062335e5085de428316dce2a`는 문서 이동으로 바뀌지 않는다. 실제 설치 상태는 파일·cfg·actual current·지속 PackageRoot를 확인한다. 기존 `b1bedaac…` 패키지의 PC 설치 기록을 새 main 설치로 바꿔 쓰지 않는다.

## 운용·재현·권한

| 문서 | 용도 |
|---|---|
| [OPERATOR_KO.md](OPERATOR_KO.md) | 현재 CLI·prepare/restart/UI-only 경계 |
| [REPRODUCE_KO.md](REPRODUCE_KO.md) | 고정 Git export·표적 시험 |
| [LAUNCH_PROCEDURE_KO.md](LAUNCH_PROCEDURE_KO.md) | 현재 상태 백업과 native trial/원복 |
| [PROJECT_NATIVE_TEST_POLICY_KO.md](PROJECT_NATIVE_TEST_POLICY_KO.md) | 무소비 실기 상시 승인과 금지 범위 |
| [PROJECT_STABLE_QUALIFICATION_POLICY_KO.md](PROJECT_STABLE_QUALIFICATION_POLICY_KO.md) | 종합 검증 후 Stable 유지, 실패 시 실제 preimage 복구 |
| [STABLE_QUALIFICATION_KO.md](STABLE_QUALIFICATION_KO.md) | PR26 qualification의 실제 증거·한계 |
| [EXTERNAL_DEPENDENCIES_KO.md](EXTERNAL_DEPENDENCIES_KO.md) | Medium·Utility·interop 등 외부 입력 |
| [SUPPORT_SCOPE_KO.md](SUPPORT_SCOPE_KO.md) | 전체 선언과 직접/재사용/미관측 구분 |
| [PUBLICATION_KO.md](PUBLICATION_KO.md) | 공개 허가·공급 경계와 역사 |

## 완료한 변경·누적 복원

| 문서 | 용도 |
|---|---|
| [SCENARIO_DISPLAY_APPLY_KO.md](SCENARIO_DISPLAY_APPLY_KO.md) | 18,366 교정·4,394 보존·HOLD12·현재 manifests |
| [RETIRED_PIPELINES_KO.md](RETIRED_PIPELINES_KO.md) | PR27에서 삭제한 구형 파이프라인과 유지 경로 |
| [BRANCH_RETIREMENT_KO.md](BRANCH_RETIREMENT_KO.md) | 원래 branch tip·역사 보존 tag·복구 방법 |
| [COMPREHENSIVE_RESTORE_KO.md](COMPREHENSIVE_RESTORE_KO.md) | 누적 accepted UI 복원 범위 |
| [RESTORATION_CLOSEOUT_KO.md](RESTORATION_CLOSEOUT_KO.md) | callback/producer 복원과 근거 |
| [RESTORATION_MIGRATION_KO.md](RESTORATION_MIGRATION_KO.md) | 완전 package/profile/DLL 전환 경계 |
| [GEOMETRY_RESTORATION_KO.md](GEOMETRY_RESTORATION_KO.md) | 좁은 표시/재사용 보정 |
| [UI_PLAN_RESTART_HANDOFF_KO.md](UI_PLAN_RESTART_HANDOFF_KO.md) | Museum 등 별도 작업과 main 복원의 관계 |
| [NETWORK_PREPARE_FIX_KO.md](NETWORK_PREPARE_FIX_KO.md) | fixed-ref resolver 수정 |
| [UI_REGRESSION_FIX_KO.md](UI_REGRESSION_FIX_KO.md) | 최초 회귀 복구 이력, 현재 Stable 지시가 아님 |
| [UI_REGRESSION_R2_PROMOTION_KO.md](UI_REGRESSION_R2_PROMOTION_KO.md) | 역사적 r2 기록, 현재 원복 기준이 아님 |
| [UPSTREAM_ATTRIBUTION_KO.md](UPSTREAM_ATTRIBUTION_KO.md) | 출처·라이선스 보존 |
| [README.md](README.md) | 이전 루트 README 본문, 중첩된 역사 설명 보존 |

과거 글의 PENDING/미병합/미설치 문구는 해당 당시 사실이다. 현재 사실은 최신 Git/actual PC 결과와 연결한다. 이 문서 정리 자체는 번역 수락이나 native PASS를 추가하지 않는다.
