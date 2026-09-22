# Girls' Creation 공개 검수 아카이브 Viewer v1.0

`/archive/review/`에서 제공되는 공개 검수 아카이브다.

## 특수 구조와 탐색
- R1: alignment 280 메시지. message index와 손번역 locator를 보존하며 행별 의미 판정과 proposal은 없다.
- R2: R3 안의 R2-W001 한 건을 참조한다. 독립 R02 source와 JSON/HTML 파일은 없다.
- R3: boundary 2건 + proposal ledger 88건. 원래 locator와 R4에서 확인한 위치를 구분한다.
- R4: MAIN 1,102행(본문 1,088 + 제목 14), REVIEW 133건, CONTEXT 92건을 별도 component로 표시한다.
- CONTEXT는 메인 1장에 한정된 당시 초안이며 전역 glossary 승인본이 아니다. 채택 권고도 실제 적용 승인이 아니다.

35 structured entries와 34 JSON files를 제공한다. 특수 화면에서 component·범주를 선택하고 ID, 원문, 판정, locator를 검색할 수 있다. 각 기록과 문서의 원문 전체도 펼쳐 볼 수 있다. R3의 빈 JP/after와 R4의 없는 rationale를 새 내용으로 채우지 않는다.

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

## R19~R34 recovered structured archive

기존 review_json 계열과 row renderer를 유지하고, 필요한 round에만 holds/overlays/completion/anomalies를 표시한다. R1~R4 special typed archive와 R5~R18/R35 기존 data/legacy는 변경하지 않았다. R2는 R3 embedded reference이며 R02 파일과 R36은 없다.

Source는 kekwkekw/monmusu-td-ko의 temp/gc-r1-r35-overview-20260921, commit 1e208d426344641c7b385f36234bee09e0464645에 있는 archive/girls_creation/public_r1_r35/rounds/의 **GitHub public source-of-truth copy**다. 각 JSON의 source path/blob SHA로 추적한다. Legacy bytes의 동일성 기준도 이 GitHub public blob이다. 최초 Library raw와의 byte identity를 주장하지 않는다. Source provenance manifest는 과거 1000-line truncation 및 full source 재추출 이력을 기록한다.

- R20: 원래 대상 691 중 680 검수, deferred 11은 본문·번역·proposal 없이 별도 holds에 보존.
- R21: 원래 대상 820 중 local 815 검수/hold 5. R20에서 복귀한 10행과 신규 2건은 overlays에 별도로 보존하며 R21 target에 합산하지 않음. 상속 hold 1도 분리.
- R29: historical source anomaly 2건은 translation proposal과 별도. JP exact key는 자동 수정하지 않음. R31/R32의 source에 실제 기록된 후속 진단도 별도 보존.
- R32: proposal records 40 / affected rows 39. 같은 행의 new/prior를 모두 유지.
- R33: gc_r33_rebuilt_review.html lineage. 원래 723 중 비보류 717 검수, 명시 hold 6. eligible_semantic_first_pass_complete=true, all_original_items_reviewed=false. 비보류 마감 과정의 의미 재검수·추가 제안은 0. 상속 hold 6 별도.
- R34: gc_r34_redo_review.html lineage. 668 검수, 신규 39/기존 2, speaker_ko 1건. 상속 hold 12 별도. R34R ID를 옛 R34-M과 합치지 않음.

Prior의 before/after가 source에 별도 명시되지 않은 경우 빈 값을 유지하고 원래 재확인 기록을 source_record에 보존한다. R3-W035의 whole_hand_block preimage 의미도 유지한다. Raw embedded row/proposal 및 DOM 표시 원문을 source_record에 남기며 새 번역이나 proposal preview를 계산하지 않는다.

별도 기록 검색은 보류/overlay/원문 진단을 본문 검색과 구분한다. 상속 hold의 위치 목록 없이 수치만 공개된 source는 audit 수치와 출처만 표시하며 위치나 본문을 생성하지 않는다. Archive는 historical/public review record이며 authoritative production translation checkpoint가 아니다.

| Round | 원래 대상 | local 검수 | local hold | 신규 | prior | overlay |
|---|---:|---:|---:|---:|---:|---:|
| R19 | 775 | 775 | 0 | 51 | 2 | 0 |
| R20 | 691 | 680 | 11 | 43 | 1 | 0 |
| R21 | 820 | 815 | 5 | 44 | 3 | 10 |
| R22 | 694 | 694 | 0 | 43 | 1 | 0 |
| R23 | 643 | 643 | 0 | 42 | 2 | 0 |
| R24 | 553 | 553 | 0 | 34 | 4 | 0 |
| R25 | 781 | 781 | 0 | 62 | 0 | 0 |
| R26 | 807 | 807 | 0 | 74 | 1 | 0 |
| R27 | 755 | 755 | 0 | 69 | 2 | 0 |
| R28 | 817 | 817 | 0 | 54 | 2 | 0 |
| R29 | 721 | 721 | 0 | 48 | 6 | 0 |
| R30 | 562 | 562 | 0 | 30 | 2 | 0 |
| R31 | 777 | 777 | 0 | 50 | 2 | 0 |
| R32 | 691 | 691 | 0 | 37 | 3 | 0 |
| R33 | 723 | 717 | 6 | 45 | 2 | 0 |
| R34 | 668 | 668 | 0 | 39 | 2 | 0 |
