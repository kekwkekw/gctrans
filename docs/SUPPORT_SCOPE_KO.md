# 현재 GCMod 한국어 업데이트 지원 범위

2026-11-09 데이터 기준이다. 배포 runtime은 GCMod 7.3.1이며
gctrans의 [GCMod.Candidate](../source/GCMod.Candidate/README_KO.md)를 사용한다.
과거 Nextgen runtime의 검증 결과를 현재 게임 DLL의 PASS로 대체하지 않는다.

- Master: `EnabledTables=*`, `(table, field, exact JP)` 조회. 현재 34,388 exact keys.
- UI: 독립 exact 14,075개와 확인된 범위의 동적 문구. 수치에는 offline 생성한 유한 숫자·시간 exact가 포함된다. Master 존재 여부로 UI를 막지 않는다.
- Novel: 등록 사전 1,939개와 Names direct hook. 사전 수가 전편 신규 번역·전수 의미검수 수라는 뜻은 아니다.
- 이미지: `images.json`의 Sprite 매핑 165개. 지역 카드 6개, 시작·진행 이벤트 로고, 회상 배너와 패배 안내를 포함한다. [유형·크기·hash 제약](IMAGE_UPDATES_KO.md)을 따른다.
- corpus·Knowledge: 위치별 검수와 현행 지식의 scope를 유지한다. [대규모 통합 정비와 볼칸 정책](CORPUS_VOLCAN_CLOSURE_20261031_KO.md)은 종료했으며 이후 변경은 증분 검수한다.
- 한 글자 UI exact는 허용하되 Novel 렌더러와 사용자 입력을 보호한다. 편성 복사 확인문은 팀명을 보존하는 7.2.1의 작은 전용 규칙을 쓴다.

## 남은 범위

- 전 자산 crawler 보완 캠페인은 완료했다. 수집·해석 성공, 원문 후보, 의미검수 승인, runtime 표시 확인은 계속 구분한다. 영문 UI 3종은 사용자 선택으로 번역하지 않으며, 미해석·서식·실기 대기는 [종료 기록과 재개 조건](CRAWLER_CLOSEOUT_20261103_KO.md)에 보존한다. [crawler 사용법](CRAWLER_ALL_ASSETS_KO.md)은 이후 증분 수집에도 적용한다.
- [전체 텍스트 재조사](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/FULL_TEXT_RESURVEY_20261102_KO.md) 후 [고정 UI 85·Master 7행을 반영](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/TEXT_RESURVEY_FOLLOWUP_20261102_KO.md)했다. 당시 CSV 표시 문자열 141개 중 136개가 조회됐고 나머지는 서식 3·중립 표기 2다. [새 CSV 서식 3행](../staging/text-resurvey-format-followup.json)과 [PC 서식 130행](../staging/pc-literal-review-20261102/pro-reviewed/format-followup.json)은 의미 승인 상태이며 실제 인자와 조합 순서 확인까지 배포를 보류한다. PC 구조 미확정 객체 469개와 크레딧 독음 44명/56필드도 남아 있다.
- 전투 Novel 일부는 승인 문장이 있어도 아직 runtime 등록을 보류했다. [후속 검증과 정확한 상태](BATTLE_NOVEL_FOLLOWUP_20261031_KO.md)를 따른다.
- [Master HOLD 7행](../staging/master-holds.json)은 원문 불명 2행, 원문 충돌 2행, 내부 trigger·target·dummy 설명 3행이다. 가챠 안내의 `季品石`·`頭性` 원문 충돌 근거는 [텍스트 coverage 기록](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/TEXT_COVERAGE_20261031_KO.md)에 있다.
- AP의 `更新までHH:mm`·`回数回復までHH:mm`은 두 자리 시각 `00:00~12:00`, 수치형 회복 알림은 정수 0~150을 지원한다. 별개 고정 알림 `AP回復を実行しました`도 승인 후 추가했다. [AP 조사](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/UI_AP_EXACT_20261101_KO.md) · [PC 고정 문구와 HOLD](PC_UI_LITERAL_REVIEW_20261102_KO.md)
- `開封期限：YYYY/MM/DD HH:mm まで`는 날짜 전체를 지원하는 형식이 아니다. `開封期限：2099/12/31 00:00 まで`는 현행 exact 사전에 있다. 다른 날짜에서 노출되면 실제 조합값을 확인한다. [과거 화면 조사](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/staging/ui-residual8-20261028/holds.json)
- `吸血騎士とスキッピオは眠らない`·`輝きを刻みながら`는 과거 퀘스트·이벤트 화면의 이미지 문구 후보다. 회상 배너 `ebs15017`·`ebs13335`는 이미지 카탈로그에 등록됐다. 별도 퀘스트·이벤트 화면에서 일본어 이미지가 관측되면 해당 에셋 키를 확인한다. [이미지 적용 범위](IMAGE_UPDATES_KO.md) · [과거 화면 조사](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/staging/ui-residual8-20261028/holds.json)
- 미지원 이미지, 아직 발견하지 못한 UI, 계정 해금 조건 때문에 확인하지 못한 화면이 남아 있다. 34 UTF-16칸 규칙도 픽셀 단위 적합성 보증은 아니다.

키·파일 수와 실제 화면 관측 수를 구분한다. 과거 개별 실기 결과는 [고정 기록](HISTORY_INDEX_KO.md)으로 보존하고,
이번 저장소 정리로 새 Native PASS를 주장하지 않는다.
