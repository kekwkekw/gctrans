# 진행 중인 후속 작업 근거

게임이 읽는 번역 정본은 `translations/ko/`에 있다. 이 폴더에는 후속 판단에 필요한 공개 근거를 둔다.

| 자료 | 보존 범위 |
|---|---|
| [Master HOLD](master-holds.json) | 기존 원문 문제 4행과 신규 보류 3행의 원문·판정·레코드 근거 |
| [PC 코드 분류](pc-literal-review-20261102/classification-ledger.json) · [서식 후속](pc-literal-review-20261102/pro-reviewed/format-followup.json) | 분류와 원본의 연결, 의미 승인 후 미배포인 서식 130행 |
| [재조사 서식 후속](text-resurvey-format-followup.json) | 의미 승인 후 미배포인 CSV 서식 3행 |
| [전투 Novel 검수](text-coverage-20261031/pro-reviewed-20261031/Novel_6/review.return.json) · [적용 검증 영수증](text-coverage-20261031/pro-reviewed-20261031/APPLICATION_RECEIPT.json) | 11장 실기 대기 중인 10파일·19행. [호출 ID 및 기존 승인 원본](../docs/BATTLE_NOVEL_FOLLOWUP_20261031_KO.md) |
| [Crawler 종료 요약](crawler-closeout-20261103/summary.json) | 사용자 영문 유지 결정과 재개 조건 |

완료된 검수·적용 자료는 [정리 전 고정 커밋](https://github.com/kekwkekw/gctrans/tree/5188bde0c7bec186c195ecb781a78d1bd8f03282/staging)과 [과거 자료 안내](../docs/HISTORY_INDEX_KO.md)에서 복구한다. UI exporter가 사용하는 검수 fixture는 `tests/fixtures/`에 있다.

새 대용량 배치, raw, 검수 반환본, 개인 로그는 `work/` 등 Git에서 제외된 작업 위치에서 관리한다.
