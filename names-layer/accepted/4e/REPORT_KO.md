# Names Layer 4-E — Accepted Ledger / Production Candidate

## 결과

- 사용자 acceptance: **1,070 / 1,070**.
- QA01 수정: **5 / 5**, 문맥 제한 보존: **6 / 6**.
- 중복/빈 JP/빈 KO/missing/extra/미해결 acceptance: **모두 0**.
- production candidate: **1,070 entries**, UTF-8 JSON parse와 exact round-trip PASS.
- key normalization, alias merge, Knowledge 승격, 과거 provenance rewrite: **0**.
- candidate SHA256: `0aadbee044a93eea660c5009dad0b5f60b0abfe3eb56827d1c52436c59f74333`.

## 입력 및 승인 계보

세 원본 ZIP의 expected SHA256, CRC, 내부 manifest 전 항목이 일치했다. 4D0 89개 / Batch09 71개 / QA01 25개 manifest-listed 파일을 검증했다. QA01에 보존된 cumulative ledger와 Batch09 정본 bytes가 동일하며 embedded Batch09.zip도 동일 SHA다. frozen lock과 1,070개 master record line/hash, QA amendment 5개 baseline line/hash를 검증했다.

기존 권고 1,059개 + QA 수정 5개 + 문맥 제한 6개를 새 acceptance event에 연결했다. Batch09 권고값을 그대로 수락하는 수는 1,065개다. 원래 pending/proposed 기록은 `sources/` 및 accepted row의 historical 객체에 보존하고, 새 사용자 approval은 별도 top-level 필드에 기록했다. 과거 승인 시각을 추정하지 않았다.

R34R-M007의 수산한→수상한 교정과 이번 `죽음의 예술가가 된 수상한 남자` 전체 문구 승인을 분리했다. surface-only 2개와 fragment-only 4개는 accepted지만 과거의 context limitation은 남아 있다. 女の子/おじさん 등의 다른 부분 잘림 관련 기록도 원본 그대로 보존했다.

## 값 / lookup 검사

1,070개 전체를 ledger→JSON→reload로 비교했다. ラぺ/ラペ, 子どもの声/子供の声, 전각 ABC, mystery, 공동 화자, たち, 얼터/전환, R34 계보, 수정 5개와 제한 6개를 fixture로 포함했다. dictionary miss/충돌/자동 normalized alias hit는 0이다. 이는 정형 및 코드 검사이며 실기 검증이 아니다.

제어문자·한자·가나 포함 output 이상값은 0이다. 한글 없는 승인값은 `???`, `????`, `??????`, `D・A` 4개이며 그대로 유지했다. 문자 범위 검사는 의미적 언어 판정이 아니며 historical Chinese value를 KO source로 사용하지 않았다.

## Live delta

fresh GET: **2026-09-26 11:29:01 UTC**. old/new manifest SHA는 모두 `62355171145edc1a19d8e88e9f2def46fa0e9a23f46c8c295742c168ac0be44d`다.

- total assets 22,684; 후보 1,972; confirmed scripts 1,864; non-script 108.
- 138,822 parsed rows; blank 33,752; nonblank 105,070.
- current exact keys **1,070**; added **0**, removed **0**, unchanged **1,070**.
- cache bundle SHA 1,972개 / script SHA 1,864개 / key별 occurrence count 1,070개가 검증된 4-C2와 일치.
- unresolved failure 0, **DELTA_CLEAR**, PRO delta review 필요 없음.

manifest만 새로 조회하고 동일 frozen asset path/hash의 cache를 전수 재파싱했다. 전체 source 본문을 output하지 않았으며 기존 hold/deferred 문맥이나 six limitation을 재판정하지 않았다.

## Repository / PR

현재 main: `6c9be9e795a6ce686f35a6ccaec4481066b240e1`.

#14: open / draft / unmerged / mergeable clean, head `64b37e571f9af25aedb529b99da5a656bc9b4536`; `names/zh_Hans.json` 1개 추가. 현재 main 대비 ahead 1 / behind 2. PR API base.sha의 과거 값과 현재 main 조회 값을 구분했다.

기존 #14를 보존하고 별도 replacement Draft PR을 권장한다. 이번에는 **branch/commit/push/PR/merge/deploy 모두 0**이다. 기존 novel/names/words/Knowledge/GCMod/workflow 파일을 수정하지 않았다. 시작/종료 git status와 refs 동일.

## Runtime gate

다음 단계는 **runtime gate**다. `RUNTIME_GATE_CHECKLIST.md`와 machine-readable fixture를 제공했다. 전체 게임 종료→config/CDN 변경→재실행이 필요하며 F10만으로 통과 처리하지 않는다.

보존된 GCMod 6.1은 translation 설정 및 해당 novel의 translation dictionary 등록을 names lookup 전에 검사한다. 1,070 mapping을 로드하는 것과 모든 scenario에서 적용되는 것은 구분해야 한다. 코드는 변경하지 않았고 실제 game 실행도 하지 않았다.

새 의미 검수, 새 key 번역, 승인 범위 확대를 수행하지 않는다. 별도 runtime 검증 및 최종 검토 전에는 MERGED / RUNTIME_VERIFIED / PRODUCTION_DEPLOYED로 표시하지 않는다.
