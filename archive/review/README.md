# Girls' Creation 공개 검수 아카이브 Viewer v0.9

`/archive/review/`에서 제공되는 공개 검수 아카이브다.

## 특수 구조와 탐색
- R1: alignment 280 메시지. message index와 손번역 locator를 보존하며 행별 의미 판정과 proposal은 없다.
- R2: R3 안의 R2-W001 한 건을 참조한다. 독립 R02 source와 JSON/HTML 파일은 없다.
- R3: boundary 2건 + proposal ledger 88건. 원래 locator와 R4에서 확인한 위치를 구분한다.
- R4: MAIN 1,102행(본문 1,088 + 제목 14), REVIEW 133건, CONTEXT 92건을 별도 component로 표시한다.
- CONTEXT는 메인 1장에 한정된 당시 초안이며 전역 glossary 승인본이 아니다. 채택 권고도 실제 적용 승인이 아니다.

19 structured entries와 18 JSON files를 제공한다. 특수 화면에서 component·범주를 선택하고 ID, 원문, 판정, locator를 검색할 수 있다. 각 기록과 문서의 원문 전체도 펼쳐 볼 수 있다. R3의 빈 JP/after와 R4의 없는 rationale를 새 내용으로 채우지 않는다.

JSON 로딩 실패 시 원본 HTML로 전환한다. R4의 MAIN/REVIEW/CONTEXT 원본 링크 3개를 제공하며, byte-identical legacy 안의 과거 파일명 링크는 원문 상태로 남아 있다. R1의 별도 문서 안내는 해당 자료가 이 public archive에 포함됐다는 뜻이 아니다.

## 기존 review JSON
- R5: 71행 / 본문 70 / 제목 1 / 신규 7 / 기존 재확인 0
- R6: 578행 / 본문 569 / 제목 9 / 신규 65 / 기존 재확인 2
- R7: 834행 / 본문 823 / 제목 11 / 신규 52 / 기존 재확인 4
- R8: 611행 / 본문 601 / 제목 10 / 신규 69 / 기존 재확인 1
- R9: 740행 / 본문 730 / 제목 10 / 신규 72 / 기존 재확인 0
- R10: 651행 / 본문 641 / 제목 10 / 신규 52 / 기존 재확인 2
- R11: 799행 / 본문 789 / 제목 10 / 신규 50 / 기존 재확인 2
- R12: 655행 / 본문 644 / 제목 11 / 신규 47 / 기존 재확인 1
- R13: 634행 / 신규 32 / 기존 재확인 5
- R14: 674행 / 신규 38 / 기존 재확인 1
- R15: 588행 / 신규 44 / 기존 재확인 1
- R16: 563행 / 본문 교정 57 / 신규 제목 후보 9
- R17: 694행 / 신규 58 / 기존 재확인 7
- R18: 653행 / 신규 57 / 기존 재확인 1
- R35: 839행 / proposal 47

각 라운드는 `data/Rxx.json`을 직접 렌더링하고, JSON 로딩 실패 시 `legacy/Rxx.html`로 fallback한다.

R5~R18은 서로 다른 구형 HTML 형식을 라운드별 추출기로 구조화했고, 원본 헤더 통계·행 수·제안 수·JP/KO/판정 누락 검증을 통과한 것만 게시한다.

이 아카이브는 공개 탐색/공유용이며 authoritative translation checkpoint를 대체하지 않는다.
