# 현재 LocalParity 지원 범위

2026-10-28 기준 runtime은 `gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`에서 시작한 `source/GCMod.Candidate`다. 산출물은 `candidate/GCMod.dll`이다. 기존 Nextgen/Native provider/ownership/live-switch의 전체 PASS는 현재 runtime의 검증 근거가 아니다.

- Master는 `EnabledTables=*`, `(table, field, exact JP)` 조회를 사용한다. 한국어 Master 34,020 keys는 pinned local 32,592 keys를 모두 포함한다. 보유 키 수와 실제 화면 표시 검증은 구분한다.
- UI는 독립 `ui.json` 2,109개 exact와 실제 화면에서 확인한 제한된 숫자·날짜·해금 조건을 처리한다. Master 존재 여부로 UI를 차단하지 않는다. 501개 authoring binding은 보존하지만 runtime binding 소비자는 0개다.
- EnhancePatch, UiPatch, TranslationPatch, VisualPatch, MasterDataPatch와 local lifecycle을 유지한다. 작은 Choice 표시·element 명칭 예외만 별도 적용한다. 상세 차이는 [runtime README](../source/GCMod.Candidate/README_KO.md)에 기록한다.
- 검수된 Novel 1,939편과 Names direct hook, 한국어 TMP fallback을 유지한다. 본문 재번역이나 전편 의미 재검수는 수행하지 않았다.
- 기존 3개 Master HOLD는 내부 trigger, target 조건, dummy 상품 설명의 실제 노출 여부가 불명확한 항목이다. `staging/master-source-review-20261028/new-holds.json`에 개별 근거가 있다.
- 레코드 지구 카드 6개는 7.2.0 후보에서 PNG 교체를 실기 확인했다. 이벤트/시작 로고는 같은 Sprite 방식으로 확장할 수 있으나 아직 교체하지 않았다. [이미지 갱신 범위](IMAGE_UPDATES_KO.md)를 참조한다.
- `更新まで`/`開封期限…まで`는 이전 요청의 보류 정책을 유지한다. 일반 날짜+まで와 完成まで는 별도 지원한다.
- 맵 레벨 상승 알림의 exact는 테스트했으나 계정의 획득/한계 돌파를 실행해 재발동하지 않았다.

실기 결과는 `staging/ui-broad-export-20261028/native-smoke.json` 등 해당 DLL·데이터의 개별 근거로 읽는다. 전체 게임 UI 완료나 62 table 전수 화면 PASS로 확대하지 않는다.

현행 crawler/검수/설치 절차는 [LOCAL_PARITY_PIPELINE_KO.md](LOCAL_PARITY_PIPELINE_KO.md)를 따른다. 수집 실패·이미지·개발 placeholder·미검수 pending을 구분하며, 일부 실패 때문에 정상 수집분의 번역 적용을 전부 막지 않는다.
