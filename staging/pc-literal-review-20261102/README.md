# PC 코드 UI 검수 상태

현재 결과는 `pro-reviewed/application-receipt.json`, `coverage-after.json`, `verification.json`을 따른다.
고정 421행 중 승인 419행을 반영했고 2행은 HOLD다. 서식 130행은 의미 승인만 완료했으며 `publication_allowed=false`다.

- `classification-ledger.json`: 원래 미일치 992개의 hash·literal index·분류·현재 적용 상태. 표시 문장 수나 게임 전체 지원율이 아니다.
- `pro-reviewed/format-followup.json`: 서식 130행의 반환 JSON 연결 정보와 후속 기술 확인 상태.
- 루트의 `summary.json`, `verification.json`, `review-preparation.json`, `collector-verification.json`: PRO 반환 **이전**의 수집·분류·초벌 사본 검증 기록. 당시 pending 및 UI 사본 13,990개는 역사적 상태다.

입력 ZIP·raw metadata·전체 코드 참조·분류 근거·준비 실패 기록은 Git 제외 work에 보존했다.
반환 ZIP과 세 파일의 원본 bytes는 `work/text-coverage-20261031/pc-literal-pro-apply-20261102/`에 있다.
공개 기록의 해시와 row ID로 원본을 대조할 수 있다. 정본은 `translations/ko/`이며 서식 반환 원본을 apply에 넣지 않는다.
