# 걸크리 PC 한국어화 — Knowledge Layer GitHub 보존 → Names Layer 완성 → GCMod/CDN·파이프라인
## 새 프로젝트 인계 및 단계별 실행 지시 v1.0

기준일: 2026-09-25 / 시간대: Asia/Seoul  
문서 역할: 새 프로젝트의 진행 순서·작업 경계·정본 탐색 지침. 원본 Knowledge Layer나 과거 승인 원장을 대체하지 않는다.

---

## 0. 이 프로젝트에서 이어갈 작업

이 프로젝트는 「ガールズクリエイション / 걸크리」 PC판 한국어화를 이어간다.

사용자가 확정한 순서는 다음과 같다.

**R1~R36 의미 검수 → Post-R36 최종 의미 판정·사용자 승인 → editorial 반영 → PR #13 main 병합·기존 Pages 배포까지 완료**

이후:

**A. 누적 Knowledge Layer와 결정·배포 연결 정보를 GitHub에 보존**  
**B. Names Layer 전체 완성**  
**C. GCMod/CDN 구조 업데이트 및 번역 파이프라인 구축 등 후속 작업**

새 프로젝트에서는 A부터 시작한다. Names 작업의 4-A까지 한 일을 버리지 않되, 그 다음 단계를 A보다 먼저 수행하지 않는다.

현재 필요한 것은 새 용어집을 처음부터 작성하거나 R1~R36을 다시 읽는 것이 아니라, **이미 존재하는 지식·근거·승인·배포 이력을 구분해 보존하고 필요한 범위만 재사용할 수 있게 만드는 작업**이다.

다음은 하지 않는다.

- R35/R36 의미 검수 재시작, 전체 corpus 재번역, PR #13 재검수·재배포.
- 이전 ZIP의 `NEXT_STEP`을 현재 작업 지시로 그대로 실행.
- 용어 레코드 전체의 approved 승격 또는 관찰 KO의 production 값 덮어쓰기.
- Knowledge Layer 보존 PR에 Names/GCMod/번역 본문/자동 번역 workflow 변경을 혼합.
- 기존 채팅·프로젝트가 기술적으로 손상됐다고 단정. 확인된 것은 이전 실행·컨텍스트 문제와 프로젝트 이전 결정이다.

## 1. 권한과 첫 요청의 범위

이 문서를 새 프로젝트 시작 지시로 붙여넣은 경우, **첫 응답은 A0의 읽기 전용 확인과 보존 설계**를 실제 수행한다.
첨부 ZIP 조사, 무결성·필수 경로 확인, 최신 GitHub의 제한된 조회, 로컬 인계·보존 후보 문서 생성까지 진행한다.

첫 응답에서 원격 commit/PR 수정/merge/배포, GCMod 변경, 새 번역·새 승인, 사용자 로컬 저장소 정리를 실행하지 않는다.
이후 사용자가 해당 단계 진행을 지시하면 그 단계의 작업 브랜치·후보 산출물을 진행한다. 이미 승인된 내용은 반복해서 승인받지 않지만, 새 언어 판단이나 공개 범위 변경까지 과거 승인으로 간주하지 않는다.

구분할 권한:

- 구조 확인·복사·후보 생성.
- 새 언어 판단과 용어/이름 정책 선택.
- 해당 후보의 GitHub commit/PR 게시.
- 실제 최종 HEAD의 merge 및 배포.
- GCMod/runtime 개조와 실기 시험.

공개 저장소에서는 **브랜치 commit도 이미 공개**일 수 있다. 개인정보·비밀키·비공개 경로·민감한 보류 본문 검토를 main merge 직전까지 미루지 않는다. 저장소 workflow와 Pages 설정을 확인하기 전 “이 경로의 commit은 배포와 무관하다”고 가정하지 않는다.

## 2. 첨부 구성과 처음 읽는 순서

기본 첨부는 다음 3개다.

1. `gc_r36_event12_v1_final01.zip` — R1~R36 누적 데이터 본체. 업로드명에 `(1)`이 붙어 있어도 아래 SHA가 같으면 같은 정본이다.
2. `gc_POST_R36_MEANING_BASELINE_v1_0.zip` — 최종 의미 결정층.
3. `gc_RESTART_SUPPORT_20260925_v1_0.zip` — 본 문서, 현재 상태, 추가 원본 5종, 후속 설계 참고, 검증·파일 안내.

추가 보존 권장:

- `gc_names_layer_4A_checkpoint_v1.zip` — Names 4-A 최신 기록으로 확인된 ZIP. 지원팩에는 이 ZIP의 원본 바이트가 포함되어 있지 않다. 자세한 상태는 `references/NAMES_4A_RESUME_NOTE_KO.md`와 `audit/LIBRARY_RECOVERY_STATUS.json` 참조.

지원팩을 풀면 다음 순서로 읽는다.

1. `START_HERE.md`
2. 이 문서 `NEW_PROJECT_PROMPT_KO.md`
3. `CURRENT_STATE.json`
4. `audit/INPUT_VERIFICATION.json` 및 `audit/SOURCE_INDEX.json`
5. `originals/gc_R36_PRODUCTION_MERGE_RECEIPT_v1_0.zip`의 `STATE.json`
6. R36의 `reports/PROVENANCE.json`, `reports/COUNTS.json`, `reports/COMPLETION_R36.json`
7. Meaning baseline의 `reports/COUNTS.json`, `records/USER_APPROVAL_RECEIPT.json`
8. 이후 실제 작업에 필요한 경로만 열기.

전체 history, review HTML, evidence 원장을 첫 응답에서 전부 출력하지 않는다.

필수 원본 식별:

```text
R36 ZIP SHA256
6a51ca0cf4d1a771e4e00951231a7e581708c45351db9c286eb70d3bf7af85c7
내부 루트: gc_r36_event12_v1/

Meaning baseline ZIP SHA256
41b503cd48948558ac24644902ff68d02474882afc1012e2b94a0f72d146164d
내부 루트: gc_POST_R36_MEANING_BASELINE_v1_0/
```

다른 원본의 전체 SHA·크기·내부 루트는 `FILES_TO_ATTACH_KO.md`와 `audit/INPUT_VERIFICATION.json`에 있다. 파일명 끝의 `(1)/(2)`를 정본 버전으로 해석하지 않는다.

## 3. 정본 우선순위는 하나의 “최신 파일”로 합치지 않는다

| 판단할 대상 | 우선 자료 | 주의 |
|---|---|---|
| 실제 현재 GitHub 상태 | 해당 저장소의 live ref/PR metadata | 과거 PR 본문에 남은 “미병합” 문구보다 실제 merged 상태가 우선 |
| 완료된 production 계보·배포 결과 | 최종 merge receipt + 해당 commit | 과거 candidate/review 보고서의 HEAD와 구분 |
| 최종 의미 채택·유지·역주 결정 | Meaning baseline의 사용자 결정·승인 receipt | PRO 권고와 사용자 결정을 혼동하지 않음 |
| editorial 정책의 원래 판단 근거 | Editorial policy PRO + 후속 적용/회귀/병합 기록 | 정책 ZIP의 `user_approved=false`는 작성 당시 상태 |
| R1~R36 원문·관찰·지식·근거 | R36 본체 및 그 PROVENANCE가 지시하는 파일 | production 결과로 원형을 소급 갱신하지 않음 |
| Names 추출 실적 | 4-A 원장·감사·검증 기록 | 추출 PASS는 이름 전체 승인/coverage 완료가 아님 |
| legacy 후보 | recovery candidates | 최신 자료와 대조 후 승계·중복·해소·미확정을 판정 |
| 미래 파이프라인 | 이 문서 + 참고 roadmap | 미구현 제안을 현재 기능으로 표현하지 않음 |

충돌하면 양쪽 출처·범위·날짜를 기록한다. 일반 원칙은 **동일한 대상과 범위에서 더 나중의 유효한 결정이 현재 상태를 정한다**는 것이다. 원본 문서 자체를 고쳐 과거 상태를 숨기지 않는다.

## 4. 완료된 production 기준 — 되돌리지 말 것

2026-09-25 인계 작성 중 GitHub connector로 확인한 값:

```text
repository: kekwkekw/gctrans
main: 9e4bfa9691fe4251096a4aa12155794a182305cd

PR #13: closed / merged=true
최종 head: 65355c110a0b000ce04ad44905aa7251c91501a9
merge commit: 9e4bfa9691fe4251096a4aa12155794a182305cd
merge 전 main: 340a3d2f5bcc793501887f15237b1095a1b21b7a
merge/head tree: 819cc44e402a8030d911cf42b7694e0277e70a85
rollback branch: rollback/pre-r36-translation-20260924
rollback target: 340a3d2f5bcc793501887f15237b1095a1b21b7a
```

Production merge receipt가 보존하는 완료 내용:

- 정규 novel 변경 파일 338개.
- 기존 명시 보류 12행은 검수·승인하지 않고 neutral placeholder로 보호.
- Pages run `35974594420`: completed / success.
- 당시 다운로드하여 검증한 artifact `10797372255`: 338개 중 336개가 sealed v1.2와 byte-identical, 나머지 2개는 승인된 trailing `<br>` 제거만 존재. 누락 0.
- 정규 novel 반영에 GCMod DLL 교체는 필요하지 않았음.

PR #13의 최소 runtime gate는 이미 닫혔다. 확인된 대표 시험은 `99900001`, `10010209`, `40112011` 보류 placeholder다. 추가 P1 사례는 찾기 어려워 미실행이었으며, 실패로 기록하지 않는다. 전체 338개 시나리오 완주 시험이 완료됐다는 뜻도 아니다.

회귀 보고서의 `head=c6cb609...`는 **최종 국소 정리 이전** 대상을 가리킨다. 그 뒤 최종 `65355c...`에서 다음 두 위치의 trailing `<br>`만 정리되었고 병합 receipt에 보존돼 있다.

- `20001501/raw37`
- `22001605/raw37`

따라서 `PR13-RV002`를 현재 미처리 번역 문제로 다시 열지 않는다.

**기존 CDN에 R36 정규 novel 결과를 배포하는 일은 완료됐다.** 사용자가 예정한 “CDN 업데이트”는 Names 복구/전체화 및 향후 GCMod·CDN 구조 현대화라는 별도 작업이다.

## 5. R36 데이터 본체 — 실제 누적 지식이 존재한다

Schema v1 / migration lineage 1.0.1을 유지한다.
R36 ZIP 내부 `knowledge/*.jsonl`은 14개이며, 별도의 역사적 notice 파일까지 `knowledge/` 아래 파일은 15개다.

| 데이터 | 실제 레코드 수 |
|---|---:|
| evidence | 22,859 |
| entities | 100 |
| references | 460 |
| terms | 495 |
| character_profiles | 85 |
| character_profile_observations | 463 |
| speech_address_rules | 1,763 |
| relationships | 606 |
| claim_guards | 1,076 |
| claim_developments | 206 |
| evidence_links | 308 |
| policies | 6 |
| policy_evidence_links | 1 |
| conflicts | 2 |
| catalog/context_groups | 342 |

누적 findings 1,691 / proposals 1,687. R36 자체는 본문 695 + 제목 12 = 707항목 대조 완료, 활성 큐 0, 일반 큐 잔여 7개는 기존 보류이며 큐 밖 보류 5개와 합쳐 12행이다. 전체 게임의 미수집 업데이트까지 완료라는 뜻은 아니다.

중요: `terms.jsonl`의 495건은 승인 용어 495개가 아니다.

```text
reviewing: 492
conflict: 2
approved: 1  — アルテ → 아르테
```

인물 프로필·관계·호칭 관찰의 다수가 reviewing인 것도 원형의 의도된 상태다. 번역 문장에 대한 최종 사용자 승인과 모든 관련 용어·세계관 주장에 대한 GLOBAL 승인은 별개다. `reviewing` 레코드를 버리지 말고 참고·근거·주의 정보로 보존하되 강제 치환 규칙으로 내보내지 않는다.

기존 Knowledge Layer는 GCMod `names/words`의 상위 정본이다. `words/zh_Hans.json`이나 기존 중국어 `项目GPT字典.txt`가 이 지식층을 대체하지 않는다.

## 6. 의미 결정층·editorial의 현재 상태

Meaning baseline은 R36 본체를 대체하는 ZIP이 아니라 동반 결정층이다.

```text
누적 proposal 1,687
= 기존 Main M1 dispositions 668 + Post-R36 사용자 결정 1,019

Post-R36 1,019
= approve_original 987
+ approve_amended 6
+ keep_observed 23
+ note_policy_approved 3

effective changeset 996 = 987 + 6 + 3
```

996건 전부가 정규 novel 본문에 들어간 것은 아니다. PR13 회귀 기록의 분리:

- 정규 novel 의미/역주 적용 993 = 문구·제목 교정 990 + 역주 분리 3.
- battle 의미 교정 2건은 battle sidecar에 남음.
- speaker-label 교정 1건은 Names Layer에 남음.

역주 정책:

- `R3-W035`: 본문에서 분리, 역주 원장 보존, 기본 사용자 비노출.
- `R3-W039`: 본문에서 분리, 역주 원장 보존, 향후 용어집/툴팁 노출 가능성만 열어 둠.
- `R21-M044`: 번역 불가 관련 메모를 본문에서 제거, 번역 audit/history에만 보존, 사용자 비노출.

이 승인이 새 `<click=ID>` 연동이나 툴팁 구현까지 승인한 것은 아니다.

이미 수락·반영된 editorial 원칙:

- 대응 원문에 맞춰 말줄임표를 `…`로 정리하고 실제 run 길이를 보존.
- 구조용 대시는 `―`로 정리하되 발음 늘임·외침·효과음 62회 보호.
- 서술문 종결 후보는 19행 `.` 및 자문형 1행 `?`의 개별 판단.
- 공백 후보 40회 중 명시 10회 제거, 30회 유지.
- 대사·제목의 종결부호를 일괄 삽입/삭제하지 않음.
- 태그·괄호·보류·원문 이상·분할/병합 경계 보호.

Editorial policy ZIP은 승인 전 권고를 보존하므로 `user_approved=false`, `applied=0`이 남아 있다. 이것을 근거로 사용자에게 같은 정책을 다시 수락하라고 요구하지 않는다. 정규 novel에 반영된 정책은 PR #13·회귀·병합 기록과 연결한다. 원본 권고 파일 자체는 불변으로 남긴다.

## 7. Names Layer — 어디까지 했는가

### 7.1 실제 PR #14 상태

2026-09-25 읽기 전용 재확인:

```text
PR #14: Add minimal reviewed Korean names layer
state: open / draft=true / merged=false
branch: review/name-layer-speaker-r34r-20260924
base: main / 9e4bfa9691fe4251096a4aa12155794a182305cd
head: 64b37e571f9af25aedb529b99da5a656bc9b4536
변경 파일: names/zh_Hans.json 1개
```

현재 1건:

```json
{"不審な男改め死の芸術家":"수상한 남자 바꿔서 죽음의 예술가"}
```

`R34R-M007`, `22001601/raw89`의 `수산한 → 수상한` 국소 오탈자 수정이다. 나머지 `바꿔서` 등을 자연화한 승인이 아니다.
전체 Names 조사와 runtime gate를 마치기 전 PR #14를 자동 merge하지 않는다.

### 7.2 4-A는 완료 기록이 있다 — 0으로 되돌리지 말 것

검증 보고서에서 확인한 상태:

- canonical review source 34개, R3~R36.
- raw observed speaker pairs 17,024행.
- unique Japanese speaker keys 305개.
- parse error 0, 필수 필드 누락 0, 중복 provenance key 0.
- `PASS_WITH_DOCUMENTED_SOURCE_LIMITATIONS`.
- source limitation 4개: R3, R4, R16, R21.
- dedup, conflict 판정, Post-R36 overlay, 신규 번역, PR/main 변경은 미실행.

4개의 구조적 제한은 원래 HTML이 JP/KO speaker 양쪽을 함께 노출하지 않는 문제다. parser 실패나 R1~R36 의미 검수 미완료를 뜻하지 않는다. 미노출 counterpart를 추측해 채우지 않았다.

305개는 **관찰된 JP key 수**이지 전체 게임 names 수나 승인된 names 수가 아니다.

4-A의 마지막 ZIP으로 확인된 파일명은 `gc_names_layer_4A_checkpoint_v1.zip`이다. 이전 무접미사 `gc_names_layer_4A_checkpoint.zip`도 존재하므로 먼저 최신 v1의 manifest·내용을 확인하고 둘을 합산하지 않는다.

인계 작성 때 4-A의 Library 기록과 감사 텍스트는 조회됐지만 raw ZIP을 작업 컨테이너로 복사하는 기능은 실패했다. 따라서 지원팩에 원본이 들어 있다고 주장하지 않는다. 원본 ZIP 또는 `speaker_pairs_raw.jsonl` + `source_extraction_audit.json` + `VALIDATION_4A.json`을 직접 확보하면 4-B로 이어간다.

**이 추가 첨부의 미확보는 A의 Knowledge Layer GitHub 보존을 막는 조건이 아니다.** R36 catalog의 더 풍부한 관찰 자료를 활용해 추출 누락을 보충할 필요가 생겨도 별도 derived supplement로 만들고, 이를 옛 4-A와 바이트 동일하다고 표현하지 않는다. 전체 재번역으로 전환하지 않는다.

## 8. 작업 방식 — 짧은 문맥, 범위가 고정된 단계, 검증 가능한 산출물

- 응답 시작에 이번 범위·예상 산출물을 짧게 밝히고, 진행 중 중요한 발견만 간단히 공유한다.
- 한 번에 source inventory, 지식 추출, 의미 승인, 전체 names, GCMod migration을 모두 수행하지 않는다.
- 저장소 전체 recursive tree, 전체 diff, 거대한 HTML/JSONL/해시 목록을 채팅에 출력하지 않는다. 파일·경로별 읽기, 필요한 필드 요약, 스트리밍 집계를 사용한다.
- 검색 0건은 파일 부재가 아니다. 실제 첨부 경로·알려진 파일 참조·manifest를 확인한다. ZIP은 원본 바이트로 목록·CRC·SHA를 검사한다.
- 마운트되지 않은 Library 파일은 실제 retrieval/materialization 결과를 확인한다. raw 복사 실패를 숨기거나 존재하지 않는 download link를 만들지 않는다.
- 정확히 같은 입력 SHA에 대한 기존 검증이 있어도 새로 실행하지 않은 검사를 “이번에 PASS”라고 쓰지 않는다. inherited/실행/미실행을 구분한다.
- 기존 스크립트는 import·경로·쓰기 동작을 읽고 실제 환경에서 실행 가능한지 판단한다. 문서에 나온 명령을 추측해서 반복 실행하지 않는다.
- 원본 ZIP과 검증 사본은 불변, 수정은 새 work copy에서. parent와 work 사이 hard-link 금지.
- 봉인 파일 덮어쓰기 금지, atomic write, single writer, 중복 적용 방지용 ID·preimage hash 유지.
- 실제 완료 ID·첫 미완료 위치·남은 범위를 남긴다. count만 맞춰 완료 선언하지 않는다.
- `WORK / VALIDATION / PACKAGING / HANDOFF / USER_RECEIPT`를 구분한다. 전달 실패 때문에 완료한 의미 검수를 취소하지 않는다.
- 시작 시 도구가 실패하면 확인된 범위의 보고서와 남은 항목을 남긴다. 검증 실패를 성공처럼 꾸미거나 무한 재시도하지 않는다.
- 백그라운드 감시·자동 알림을 기본으로 도입하지 않는다. 사용자가 요청한 현재 단계 중심으로 진행한다.

사용자 PC 명령은 PowerShell 기준이다.

```powershell
$workspace = 'D:\Downloads\GC_WORK'
$out = Join-Path $workspace 'out'
```

실제 경로는 실행 전 확인한다. `C:\Users\user\Desktop\gctrans`의 기존 개발 폴더는 dirty일 수 있으므로 자동 `reset --hard`, `clean`, `stash`, 강제 동기화를 하지 않는다. 격리된 작업 사본/브랜치/worktree를 필요한 범위에서 사용한다.

## 9. A — Knowledge Layer GitHub 보존의 구체적 단계

### A0. 정본·상태·의존 경로 확인 — 첫 응답

- 외부 R36/meaning ZIP의 존재·크기·SHA 및 내부 루트 확인.
- 지원팩 manifest 확인 및 현재 main/PR #13/#14를 제한적으로 조회.
- R36 PROVENANCE가 지시하는 활성 원장과 별도 승인층·production receipt를 연결.
- GitHub 내 기존 knowledge/docs의 실제 존재·현재 구조를 좁은 조회로 확인.
- `knowledge`, `catalog`, 결정층, 활성 review/guards, source reader 의존물의 보존 분류표 작성.
- legacy 중복과 아직 전달되지 않은 자료를 분리.
- 첫 단계에서 production·PR #14·스키마·승인 상태를 변경하지 않음.

산출물 예: `CURRENT_STATE_CHECK.json`, `PRESERVATION_PLAN_KO.md`, `SOURCE_DEPENDENCY_MAP.json`, `NEXT_STEP.json`.

### A1. 로컬 보존 후보 패키지 생성

목표는 무조건 가장 작은 ZIP이 아니라 **필요한 근거를 잃지 않는 최소 보존 집합**이다.

반드시 고려할 범위:

- `knowledge/*.jsonl` 14개 원장: ID·필드·상태·원문·관찰·근거 그대로.
- `catalog/context_groups.jsonl` 및 필요한 `catalog/source_entries_*.jsonl`.
- `schema/`의 본체와 실제 사용되는 확장 규칙.
- `review/all_findings_r36.jsonl`, `review/all_proposals_r36.jsonl`.
- `review/deferred_registry_effective_r36.jsonl`, coverage overlay, speaker resolutions, source anomalies.
- 회차별 semantic contracts·event consistency·local participants 등 현재 knowledge 단일 파일만으로 재현되지 않는 활성 문맥 계약.
- 의미 기준본의 사용자 결정·effective changeset·keep 23·역주 정책·anomaly 13·승인 receipt.
- Main M1의 기존 dispositions·published receipt.
- editorial 정책의 승인 이후 상태 연결, 최종 적용/회귀/정리 기록.
- production merge receipt 및 source/decision/editorial/production의 버전 연결 manifest.

특히 **`history/`라는 이름만으로 삭제하지 않는다.** R36 PROVENANCE는 `history/handoff_r22/gc_main_m1_rc1.zip` 등을 실제 published 기준 의존물로 참조한다. `source/gc_pro_review_r4.zip` 내부 nested `r3.zip`에 원문·mapping·hand blocks가 남아 있는 구조도 있다. 해당 reader를 유지할 경우 필요한 원형을 유지하거나, 검증 가능한 대체 snapshot과 locator remap을 먼저 만들어야 한다.

삼분류를 권장한다.

1. GitHub 활성 정본/필수 문서: 지식·결정·핵심 근거·스키마·연결 정보.
2. 별도 불변 원본 보관: 전체 원형 ZIP, 큰 historical checkpoints, 재현에 필요한 nested source. 영구 조회 위치와 hash를 남김.
3. GitHub 활성 경로에서 제외 가능한 중복·재생성 캐시: 재개에 불필요함을 확인한 screenshots/one-off 로그/중간 실행 사본. **로컬 원본 삭제 지시가 아니다.**

원본 디렉터리 구조를 통째로 바꿀 필요는 없다. 아래는 설계 예시이며 새 스키마나 확정 경로가 아니다.

```text
knowledge/                  # 원래 지식 원장 또는 versioned snapshot
catalog/                    # source/context 식별
review/                     # active ledgers + cross-row/context contracts
approvals/                  # 의미·editorial·names 승인층을 구분
provenance/                 # source/decision/export/production 연결
schemas/                    # 기존 schema의 실제 역할을 보존
docs/                       # 현재 상태·정책·잔여·재개 절차
indexes/                    # 재생성 가능한 조회 인덱스
```

경로를 옮길 때는 원래 archive/member/SHA와 새 경로의 대응을 manifest에 남긴다. 파생 요약/인덱스/독자용 glossary가 별도의 수동 편집 정본이 되지 않게 한다. 승인 vocabulary가 적어도 후보·관찰·문맥 규칙을 삭제하지 않는다.

### A2. 후보 검증 및 허가된 GitHub 반영

- 레코드 수·중복 ID·참조 ID·evidence/context 링크를 점검.
- 후보 경로가 source/reader 의존성을 끊지 않는지 확인.
- 원문/observed/승인/보류 변경 없음. 새 GLOBAL 승격 없음.
- 기존 production `novels`, `words`, `names` 및 runtime 코드는 보존.
- 개인정보·토큰·불필요한 사용자 로컬 경로·민감한 보류 본문을 공개하지 않음.
- 승인된 보존 범위만 별도 작업 브랜치·PR에 게시. 기존 Names PR #14와 분리.
- 현재 HEAD 기준 재검증. 과거 승인을 다른 HEAD에 자동 전이하지 않음.
- main merge와 그에 따른 배포 영향은 별도 범위로 확인.

보류 원형을 공개 파일에 통째로 복제해 “증거 보존”이라고 하지 않는다. 제한 본문은 현재 안전 정책을 유지하고 필요한 위치·해시·placeholder·접근 경계로 추적한다.

### A3. GitHub-only 재개 시험

“commit 성공”만으로 끝내지 않는다.
새 작업 사본에서 저장소 문서와 버전 고정 자료만으로 다음이 가능한지 시험한다.

- 어떤 R36 source/meaning decision/production commit을 쓰는지 식별.
- terms와 speaker/target 규칙, 해당 evidence·scene 제한 검색.
- 원문 이상·보류·역주·사칭/익명 라벨의 예외 구분.
- Names 후속 작업이 현재 자료만으로 어떤 범위까지 가능한지 확인.
- hash만 있고 실제 내려받을 수 없는 외부 dependency를 탐지.

전체 raw source를 별도 보관한 경우 재개 수준을 정확히 표현한다.
`KNOWLEDGE_QUERY_READY`, `NAMES_RESUME_READY`, `FULL_REBUILD_READY` 같은 상태를 별도로 기록할 수 있으나, 이것은 새 워크플로용 표시이며 원래 승인 enum을 바꾸라는 뜻이 아니다.

완료 조건: 선택된 지식·승인·근거가 검증되어 GitHub에 존재하고, 필요한 조회/재개 시험이 통과하며, 외부 의존·누락·민감 범위가 명시된 상태.

## 10. legacy 후보 통합에서 이미 확인된 중복과 함정

이번 인계에서 실제 bytes 비교로 확인:

- legacy `candidates/semantic_contracts_r35_v2.json` 52건은 R36 `review/semantic_contracts_r35_v2.json`과 동일.
- legacy `candidates/editorial_examples_r35_v2.jsonl` 75건은 R36 `review/editorial_examples_r36.jsonl`과 동일.

둘을 새 지식/새 큐로 추가 합산하지 않는다. 단, 다음 GitHub 보존 후보에서 이 파일을 빠뜨리지 않도록 실제 canonical 위치로 연결한다.

다른 후보 처리:

- legacy anomaly/hold 목록은 최신 R36/meaning anomaly 13·hold 12에 ID/locator로 대조. 무조건 새 건수로 더하지 않음.
- 오래된 editorial 대기 목록은 이미 완료된 Post-R36 적용 기록과 대조. 과거 `deferred` 문구만으로 75건 전체를 다시 미처리로 열지 않음.
- `recovery_capsule.py`는 R34 전용 base hash를 하드코딩한 legacy reference. 현재 main 또는 R36에 그대로 실행하지 않음.
- legacy 정책 19개는 신규 승인 원장이 아니라 후보. 현재 정책과 일치하는 것은 근거 연결로 승계하고, 실질적 정책 변경은 별도로 식별.

추가로 R36에는 `audit/INHERITED_SCHEMA_DIAGNOSTIC_R36.json`이 있다.
R23에서 상속된 term_type 경고 6개는 알려진 기존 경고이며 신규 invalid term 0이다. GitHub 보존 과정에서 자동 schema migration이나 용어 재분류로 “수정”하지 않는다. 경고를 드러낸 상태로 보존하고 필요한 adapter/스키마 개선은 별도 작업으로 분리한다.

## 11. B — Names Layer 전체 완성

A 완료 후 다음 순서로 진행한다. 이미 완료된 4-A는 읽기 전용으로 검증·재사용한다.

### 4-B. 정확한 중복 정리와 충돌 감사

- raw pair를 불변으로 보존하고 별도 derived inventory를 생성.
- exact JP key별 observed KO variants·관찰 횟수·R/novel/raw·원본 hash를 유지.
- Post-R36에서 승인된 **speaker_ko 필드** 교정만 위치를 확인하여 overlay. 본문 교정을 화자 라벨에 적용하지 않음.
- 원래 관찰과 overlay 적용 후 결과를 구분.
- 같은 JP의 다른 KO를 단순 빈도·confidence로 자동 선택하지 않음.
- source limitation 4라운드의 보충이 필요하면 R36 catalog/원형에서 실제 양쪽 라벨이 확인되는 경우만 별도 supplement로 추가. 별도 재추출 실적을 기록.

### 4-C. 전체 대상 universe와 누락량 확정

별도 집합:

1. 4-A 및 보충 원형에서 실제 관찰된 JP speaker keys.
2. 과거 삭제 직전 names 921개 JP keys. **values는 중국어이므로 번역 source로 사용 금지.**
3. 해당 단계에서 실제 확인한 최신 upstream 또는 게임 source의 speaker-key universe.

최신 upstream/game snapshot은 해당 시점의 commit/hash/date로 고정한다. 921 또는 305를 현재 게임 전체 개수로 가정하지 않는다.
coverage는 고정된 universe 대비 matched/missing/conflict/excluded로 계산한다. 전체 미확인 상태에서 “게임 names 100% 완료”라고 하지 않는다.

### 언어 판정과 후보 생성

- 기존 단일 observed 후보도 승인 상태를 근거 없이 전역 approved로 바꾸지 않는다.
- 새 고유명사 선택, 다른 KO 간 실제 의미 충돌, 별칭/정체/역할 판정은 PRO로 분리.
- missing label은 원문·용도·필요 문맥을 확보한 뒤 번역. 중국어 value를 음역해 채우지 않음.
- 정체 공개 전 `？？？`, 역할명, 군중/A/B/C, 변장·사칭·전환 label을 사후 캐릭터명으로 바꾸지 않음.
- canonical entity와 표시되는 speaker surface key를 별도로 유지.
- 일반 단어 substring 치환 금지. exact key 유지.
- 동일 JP 문자열이 문맥에 따라 서로 다른 KO를 요구하면, 현재 global names 딕셔너리로 안전하게 표현 가능한지 먼저 판단. 한 값을 억지로 고르거나 runtime 확장을 몰래 추가하지 않음.

### 전체판 검증·배포

- 완성 대상 snapshot에 대해 누락·충돌을 해결하거나 명시적으로 제외·미해결로 남김. reviewed-only 부분판을 전체판이라고 부르지 않음.
- JSON parse, duplicate keys, 빈 value, 문자열 literal/태그, 중국어 잔존, provenance/승인 연결 검증.
- 현재 6.1-compatible 경로 `names/zh_Hans.json` 유지. `ko-KR` 등으로 파일명만 바꾸지 않음.
- PR #14를 확장할지 대체할지는 실제 최신 상태와 사용자의 해당 단계 지시에 따라 결정. 무조건 새 PR 생성/기존 PR 폐기하지 않음.
- immutable test CDN에서 names load, R34R-M007, 일반/익명/전환/미매핑 라벨을 시험.
- 검사한 6.1 custom의 CDN 변경은 **게임 완전 종료 → cfg 변경 → 게임 재실행**. F10만으로 CDN/session이 교체된다고 가정하지 않음.
- 최종 승인·검증 HEAD에만 적용하고, 새 merge·배포 receipt 및 rollback 기준을 남김.
- 정규 novel 338개를 이 작업 때문에 다시 수정/재export하지 않음.

## 12. C — Names 이후 GCMod/CDN·runtime 후속 작업

이 단계는 앞으로의 작업이며 이미 구현된 것으로 표현하지 않는다.
현재 확인된 안정 운용 기준은 GCMod 6.1 기반 한국어 커스텀 + XUnity + GitHub Pages다. 시나리오 제목/본문은 GCMod, 일반 UI는 XUnity, 한국어 TMP fallback은 커스텀 GCMod가 맡던 구성을 보존한다.

시작할 때 실제 설치 DLL/config·repository code·새 upstream 릴리스·CDN schema를 다시 확인한다. 과거 GCMod 7.x 조사 내용을 최신 버전 확정값으로 쓰지 않는다.

후속 범위:

- 현재 names/words/novels 구조와 다음 runtime/CDN 구조의 차이 조사.
- 구버전 호환 exporter와 새 exporter를 같은 source/accepted decision에서 생성.
- 새 master 구조가 table/field/source 기반인 경우 그 identity를 보존. `words` 이름만 바꾸는 migration 금지.
- 실제 ko locale 지원, fallback font, 타이핑/개행, 이름 처리, 태그·ruby·click 동작 검증.
- staging/immutable CDN → 제한된 smoke → 승인된 전환 → rollback 절차.
- 선택지 라벨은 전체 문자열·선택 branch ID와 연결해서 조사. 부분치환으로 분기 ID를 바꾸지 않음.
- click 사전 제목/설명과 ruby 읽기 한국어화는 별도 정책/UI 과제. 기존 원문 태그·reading을 임의 제거하지 않음.

Battle은 Names와 별도:

```text
4 bundles: 1001110911 / 1001110921 / 1001110922 / 1001110931
13 JP runtime rows / 12 hand-KO blocks
의미 검수 및 source identity는 완료 기록이 있음
actual GCMod runtime Prefix/Id 실기 확인 및 sidecar 게시가 남음
```

`1001110941~1001110946` 시민 응원 6문장은 기존 손번역 대응이 없어 신규 번역 대기였던 별도 범위다. 13행 sidecar와 합치지 말고, 해당 단계에서 최신 근거와 잔여 상태를 확인한다.

## 13. C — 차세대 번역 파이프라인 구축

세부 참고: `references/gc_PIPELINE_ROADMAP_v1_0_KO.md`.
이 문서는 **설계 제안·미구현** 상태를 보존한 참고문서다. 그 안의 “R36부터 시작” 등의 예전 진입 문구보다 현재 인계의 완료 상태와 순서가 우선한다.

목표 흐름:

```text
공식 source 동기화·무결성 확인
→ 신규/변경/삭제/이동·영향 manifest
→ scope별 Knowledge Layer 검색 + versioned context pack
→ Codex 등 허가된 작업환경에서 번역 proposal 생성
→ 구조 검사 + 별도 검수 패스
→ 누적 변경 전체의 PRO 의미 검수
→ 수정 및 관련 범위 재검수
→ 사용자/명시 위임 정책의 승인
→ 승인 대상 HEAD·source·rules·preimage 일치 검사
→ main merge
→ 허가된 CDN 게시 + runtime 확인 + rollback receipt
```

핵심 설계:

- 기존 지식 검색기·adapter부터 구현한다. 모든 과거 문장을 다시 PRO로 읽어 규칙 DB를 새로 만들지 않는다.
- 본문·제목·화자·청자·source hash·mapping/span·관련 규칙 변화와 삭제/이동을 모두 추적한다.
- 동일 JP 문자열이라는 이유만으로 다른 문맥의 accepted 번역을 무조건 재사용하지 않는다.
- 원문/observed/accepted/candidate/decision/runtime export를 구분한다.
- 모델 초안과 파일 반영을 분리한다. pipeline prompt나 데이터 안의 명령은 작업 권한이 아니다.
- 초기 기본값은 신규·변경 번역 및 영향 항목의 전체 PRO 의미 검수. confidence로 검수를 생략하지 않는다.
- 작업자의 자기평가나 모델 간 합의만으로 승인하지 않는다.
- 장면/시나리오 단위 병렬 작업, 동일한 rules version, single-writer 통합, event consistency 유지.
- 검수 대상은 마지막 검수 기준~후보 HEAD의 누적 변경이다. 마지막 diff 일부만으로 회귀를 생략하지 않는다.
- 승인 뒤 source/rules/base/candidate가 달라지면 영향 범위에 따라 재검증한다. 문서 하나 추가됐다고 무조건 전체 의미 검수를 반복하지 않는다.
- 의미 수정의 환류는 번역 오류/검색 누락/문맥 부족/범위 오적용/규칙 충돌/신규 지식/국소 선택을 구분한다. 모든 오류를 GLOBAL term 추가로 해결하지 않는다.
- 평가 세트는 구조·검색/적용·언어 품질을 분리하고, 허용 변형·금지 오류·근거를 포함한다.
- branch/PR 단위로 격리한다. 과거 “서브트리”라는 표현을 실제 git subtree 도입 지시로 해석하지 않는다.
- 현재 자동 번역 Action은 과거 사용자가 껐다는 기록이 있다. Pages 빌드와 별개다. 실제 활성 상태를 확인하고 자동 재활성화하지 않는다.

새 API·계정·모델·connector·CI 기능은 실제 도구와 권한을 확인한 뒤 설계한다. API key를 문서/ZIP/repository에 포함시키지 않는다.

## 14. 남은 일과 이미 닫힌 일

| 항목 | 인계 시점 상태 |
|---|---|
| R1~R36 비보류 정규 범위 의미 검수 | 완료. 다시 하지 않음 |
| Post-R36 의미 결정 1,019건 | 완료. 새 지식 전역 승인과는 별개 |
| 정규 novel editorial/최종 정리/PR13/기존 Pages | 완료 |
| trailing `<br>` 2건 | 최종 head에서 정리 완료 |
| Knowledge Layer GitHub 보존 | 다음 최우선 작업 |
| Names 4-A | 완료 기록 확인. raw ZIP 추가 첨부 권장 |
| Names 4-B 이후 전체화 | 미착수/후속. 실제 최신 산출물 있으면 그 증거 우선 |
| PR14 | Draft / 미병합 / mapping 1건 |
| 보류 12행 | 검수·승인되지 않음. 현재 placeholder 유지 |
| source anomaly 13건 | 별도 보존. 공식 게임의 확정 오류로 승인된 상태 아님 |
| term conflict 2건 | 기존 미해결 상태 보존 |
| inherited schema warning 6건 | 기록·보존. 자동 migration 금지 |
| battle sidecar 13행 | actual Prefix/Id 및 게시 미완료 |
| 시민 응원 6문장 | 별도 신규 번역 후보 범위 |
| ruby/click/선택지·새 GCMod/CDN | 후속 조사·설계 |
| pipeline v2 | 계획. 구현 완료 아님 |

Export 완전 재현에 관해 추가 기록:
PR13 회귀 패킷은 `gc_POST_R36_EDITORIAL_CHANGESET_v1_0.zip`과 `gc_POST_R36_EXPORT_APPLY_CANDIDATE_v1_2_HOLD_PLACEHOLDERS.zip`을 입력으로 참조한다. 이번 기본 인계 첨부에는 이 두 원본 ZIP이 없다. 해당 이름·SHA·역할은 `audit/REFERENCED_NOT_INCLUDED.json`에 남겼다.
Knowledge 보존 시작이나 현재 production 결과 식별을 막지는 않지만, **과거 exporter 전체를 원본 inputs부터 재실행해 동일 bytes를 재현했다고 주장하려면** 그 의존물을 확보하거나 검증된 대체 입력을 만들고 차이를 기록해야 한다.

구형 v1.2 `verify_candidate.py`는 누락된 `reports/COUNTS.json` 참조로 실패한 기록이 있다. PR13 회귀 패킷의 `tools/verify_candidate_readonly.py`가 별도 제공됐으며, 실행 조건을 확인하고 사용한다. 구형 검증기 실패를 번역 결과 손상으로 오해하지 않는다.

## 15. 첫 응답에서 실제로 할 일

A0만 우선 수행한다.

1. 필수 ZIP과 지원팩 존재·SHA·내부 루트 확인. 원본을 수정하지 않는다.
2. 작은 상태·PROVENANCE·COUNTS·승인 receipt만 읽어 계보 연결.
3. 현재 GitHub main/PR13/PR14와 기존 knowledge 경로 여부를 제한적으로 확인.
4. 필수 원장·문맥 계약·source dependency·승인/production 연결의 보존 분류표 작성.
5. legacy 중복 및 미포함 4-A/추가 입력 상태를 표시.
6. 다음 A1에서 만들 GitHub 보존 후보의 파일 범위와 검증 항목을 제시하고 실제 inventory 파일을 남김.

첫 응답에 이름 전체 번역, 전체 history 출력, 모든 verifier 반복 실행, PR14 확장, GCMod 전환을 합치지 않는다. 필요한 필수 경로에 접근 실패가 있어도 가능한 구조·상태 조사와 증거를 먼저 남기고, 정확히 필요한 추가 입력만 요청한다.

### 모델 역할 안내

- 새 프로젝트 시작 및 A0/A1의 파일 확인·무결성·구조·provenance·중복 조사: **매우 높음으로 충분**.
- Names exact dedup·통계·기존 승인 overlay: 매우 높음으로 진행 가능.
- 새로운 일본어 의미/이름 표기/호칭/관계/전역 정책 선택 또는 충돌 해결: **PRO 권장/필요 범위를 명시**.
- 후속 pipeline의 새 번역 결과 의미 검수: 초기 운영은 PRO 전체 검수 기본.

이는 사용자와 합의한 역할 배분이지 공식 성능 순위가 아니다. 실제 모델 모드를 임의로 바꿨다고 주장하지 않는다.

각 단계 말미에는 완료 범위, 산출물, 미완료/제외, 첫 다음 작업, 매우 높음/PRO 판단을 짧게 남긴다.

## 16. 이후 재개 문서의 최소 계약

GitHub 보존 작업이 끝나면 다음을 사람이 읽을 수 있는 한국어 안내와 기계-readable manifest로 남긴다.

- 현재 source/knowledge/decision/production의 버전과 실제 조회 위치.
- 승인·관찰·후보·충돌·보류·runtime 상태의 분리.
- 작업 단계와 실제 완료/미완료 범위, 다음 범위.
- 필요한 도구·입력·읽는 순서. 전체 로그를 읽지 않는 재개 경로.
- known residual, legacy 문서의 supersession, 외부 보관 의존물.
- `GitHub에 올라갔다`와 `새 프로젝트에서 파일을 실제 다시 읽고 검증했다`의 구분.

인계 문서를 만드는 과정 자체에서 위 원장들을 새로 approved로 승격하지 않는다.

---

## 출처 탐색

이 문서의 핵심 수치·정본 연결은 지원팩 `audit/SOURCE_INDEX.json`, `audit/INPUT_VERIFICATION.json`, `audit/R36_DATA_AUDIT.json`, `audit/LIVE_GITHUB_SNAPSHOT.json`으로 추적한다.

Names 4-A는 원본 바이트 확보와 구분하여 `references/NAMES_4A_RESUME_NOTE_KO.md`에 조회 출처·제약을 남겼다.
기존 원본 ZIP의 역사적 status는 원본 그대로 보존했다. 현재 작업 순서는 이 문서와 사용자의 최신 명시 지시를 따른다.
