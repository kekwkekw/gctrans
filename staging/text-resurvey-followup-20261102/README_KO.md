# 재조사 후속 적용 상태

현재 상태는 `pro-reviewed/application-receipt.json`, `coverage-after.json`, `verification.json`을 따른다.
PRO 승인 고정 92행(UI 85 + Master 7)을 반영했다. 서식 3행은 의미 승인만 완료했으며 publication_allowed=false를 유지한다.

- `pro-reviewed/decision-ledger.json`: 95행 ID·원문/KO 해시·적용 상태. 실제 반환 원본은 Git 제외 work에 보존한다.
- `pro-reviewed/validation.json`: 입력 manifest 174개, 반환 세 파일, 92/3행 identity·순서·메타데이터 검증.
- 루트 `summary.json`, `disposition-counts.json`: PRO 반환 전 수집·초벌 준비 기록이다. 당시 미적용·대기 수는 현재 상태가 아니다.
- 이전 8행 검수 묶음의 Master 7행·레이드 말풍선 UI 1행은 모두 이번에 적용했다. 원본 ZIP은 보존하되 중복 적용하지 않는다.

Knowledge·Names·Novel·DLL은 변경하지 않았다. raw, 미해결 객체와 실패 로그는 유지한다. 실제 게임 표시 확인은 NOT_RUN이다.
