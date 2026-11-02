# 현재 LocalParity 지원 범위

2026-11-03 이 tree 기준이다(PR #57 기반 재조사 고정 문구 92행 승인본 포함). 배포 runtime은 GCMod 7.2.1이며
`gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318` 기반의 [GCMod.Candidate](../source/GCMod.Candidate/README_KO.md)를 사용한다.
과거 Nextgen runtime의 검증 결과를 현재 게임 DLL의 PASS로 대체하지 않는다.

- Master: `EnabledTables=*`, `(table, field, exact JP)` 조회. 현재 34,388 exact keys.
- UI: 독립 exact 14,073개와 확인된 범위의 동적 문구. 수치에는 offline 생성한 유한 숫자·시간 exact가 포함된다. Master 존재 여부로 UI를 막지 않는다.
- Novel: 등록 사전 1,939개와 Names direct hook. 사전 수가 전편 신규 번역·전수 의미검수 수라는 뜻은 아니다.
- 이미지: `images.json`의 Sprite 매핑 161개. 지역 카드 6개, 시작·진행 이벤트 로고, 회상 배너와 패배 안내를 포함한다. [유형·크기·hash 제약](IMAGE_UPDATES_KO.md)을 따른다.
- corpus·Knowledge: 위치별 검수와 현행 지식의 scope를 유지한다. [대규모 통합 정비와 볼칸 정책](CORPUS_VOLCAN_CLOSURE_20261031_KO.md)은 종료했으며 이후 변경은 증분 검수한다.
- 한 글자 UI exact는 허용하되 Novel 렌더러와 사용자 입력을 보호한다. 편성 복사 확인문은 팀명을 보존하는 7.2.1의 작은 전용 규칙을 쓴다.

## 남은 범위

- 전 자산 crawler는 보완 중이다. 수집·해석 성공, 원문 후보, 의미검수 승인, runtime 표시 확인을 구분한다. [crawler 안내](CRAWLER_ALL_ASSETS_KO.md), [텍스트 coverage](TEXT_COVERAGE_20261031_KO.md)를 참조한다.
- [전체 텍스트 재조사](FULL_TEXT_RESURVEY_20261102_KO.md) 후 [고정 UI 85·Master 7행을 반영](TEXT_RESURVEY_FOLLOWUP_20261102_KO.md)했다. CSV 141개 표시 문자열 중 136개가 조회되며 나머지는 서식 3·중립 표기 2다. 새 서식 3개는 기존 130개와 함께 의미 승인 상태로 보존하며 배포하지 않았다. PC 구조 미확정 객체 469개와 크레딧 독음 44명/56필드도 남아 있다.
- 전투 Novel 일부는 승인 문장이 있어도 아직 runtime 등록을 보류했다. [후속 검증과 정확한 상태](BATTLE_NOVEL_FOLLOWUP_20261031_KO.md)를 따른다.
- 이전 Master의 내부 trigger·target·dummy 설명 3개 HOLD 근거는 [new-holds.json](../staging/master-source-review-20261028/new-holds.json)에 보존한다. 새 조사 없이 해소했다고 간주하지 않는다.
- AP 시간 exact는 `00:00~12:00`, 수치형 회복 알림은 0~150 범위다. 별개 고정 알림 `AP回復を実行しました`도 PRO 승인 후 추가했다. [AP 범위](UI_AP_EXACT_20261101_KO.md), [고정 문구 419행 반영·HOLD 2행·서식 130행 의미검수 상태](PC_UI_LITERAL_REVIEW_20261102_KO.md)를 구분한다.
- 미지원 이미지, 아직 발견하지 못한 UI, 계정 해금 조건 때문에 확인하지 못한 화면이 남아 있다. 34 UTF-16칸 규칙도 픽셀 단위 적합성 보증은 아니다.

키·파일 수와 실제 화면 관측 수를 구분한다. 과거 개별 실기 결과는 [고정 기록](HISTORY_INDEX_KO.md)으로 보존하고,
이번 저장소 정리로 새 Native PASS를 주장하지 않는다.
