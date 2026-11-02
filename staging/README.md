# 유지 중인 검수·설정 입력

이 폴더는 게임 번역 정본이 아니다. 게임이 읽는 데이터는 `translations/ko/`에 있다.

| 자료 | 유지 이유 |
|---|---|
| `GCMod.cfg.template`, `accepted-master-profile.*` | 기존 오프라인 도구·검사의 설정 입력. 현행 배포 cfg는 `candidate/GCMod.cfg` |
| `master-local-coverage-20261027/`, `master-source-review-20261028/` | source coverage와 미확정 Master HOLD 근거. crawler 후속 판단에 사용 |
| `text-coverage-20261031/`, `text-remainder-20261101/`, `ui-ap-exact-20261101/` | 진행 중인 crawler·텍스트 후속 조사, 미적용 전투 Novel 검수 및 AP 경계 기록 |
| `pc-literal-review-20261102/` | PC 코드 992개 분류·고정 문구 419행 적용 영수증·HOLD 2행·서식 130행 후속 상태. 전체 원본/검수 반환은 Git 제외 work에 보존 |
| `text-resurvey-20261102/`, `text-resurvey-followup-20261102/` | 전체 텍스트 재조사·95행 검수 인계와 PRO 고정 92행 반영. 현행 결과는 followup의 `pro-reviewed/`, 루트 요약은 적용 전 기록 |
| `ui-broad-export-20261028/holds.json`, `ui-residual8-20261028/holds.json` | 이후 판정과 대조할 과거 HOLD 근거. 모든 항목이 현재도 미해결이라는 뜻은 아님 |

완료된 Novel 줄배치·UI 캠페인의 전체 반환본·실기 보고는 [Git 이력](../docs/HISTORY_INDEX_KO.md)으로 보존한다.
UI exporter 회귀가 실제 사용하는 두 검수 자료는 `tests/fixtures/ui-*-reviewed*.json`으로 옮겼다.

새 대용량 batch·raw·검수 반환·개인 로그는 `work/` 등 Git 제외 작업 위치에서 관리한다.
staging에는 재사용하거나 후속 판단에 필요한 최소 근거만 선택해 올린다.
