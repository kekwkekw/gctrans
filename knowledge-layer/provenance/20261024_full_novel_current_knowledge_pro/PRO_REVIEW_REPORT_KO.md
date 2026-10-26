# Girls' Creation Novel 1,499편 — 현행 Knowledge 기준 ChatGPT PRO 검수 보고서

- 검수일: 2026-10-24 (Asia/Seoul)
- 검수 ID: `GC-FULL-1499-CURRENT-KNOWLEDGE-PRO-20261024`
- 작업 사유: `legacy_retranslation`
- 최종 상태: **PRO_REVIEW_COMPLETE**
- 사용자 판단 대기: **0건**

## 1. 검수 기준

기존 1,499편 Codex 초벌은 재생성하지 않았다. 다음 자료를 기준으로 새로 검수했다.

- 114,243개 exact JP와 Codex 초벌 KO
- 현재 `translations/ko/names.json`에서 투영한 Names 정본
- 정돈된 현행 `translation/knowledge.jsonl` 3,534건
- 동결된 `HOLD_POLICY.md`
- 중복 source 30개의 모든 variant

이전 두 PRO 결정본은 최종 authority로 사용하지 않았다. 과거 검수에서 발견된 수정 후보는 JP·초벌·Names·현행 Knowledge와 다시 대조한 뒤 근거가 유지되는 것만 새 결정본에 포함했다.

## 2. 최종 결정

| outcome | source 수 |
|---|---:|
| `accept_variant` | 1,007 |
| `patch_variant` | 489 |
| `hold` | 3 |
| `needs_user_decision` | 0 |
| **합계** | **1,499** |

- 최종 행: **114,243개**
- 수정 행: **3,242개**
- 최종 HOLD: **16행 / 3개 source**
- 중복 source canonical 선택: **30/30 완료**
- Names 변경: **0건**
- active Knowledge 변경: **2건** — 기존 `イマージュ` record의 global 확정 1건, 신규 `音精` → `음정령` 1건

## 3. 주요 교정

- 대화 외곽 `「」`와 보호 토큰 복원
- `イマージュ` → `이마쥬`, `音精` → `음정령`
- 현재 Names의 `앵글·피오렌차·페르메르·라파엘로·캬르트` 등 적용
- 현행 Knowledge의 `노와르·에그조티·프란탄`, `신예술기사단`, `몽환 미술관`, `하르모니아` 적용
- exact JP와 현행 표기를 행별로 대조한 인명·지명·조직·시설명 정리
- 주어·목적어·부정·수식 관계·제목·기계 직역 오류 교정
- `앵글` 표기 변경에 따른 조사와 서술격 교정

정돈 Knowledge의 source scope를 전역 규칙으로 자동 승격하지 않았다. scope 밖 고유명사 교정은 동일 exact JP와 장면의 entity를 행별로 확인한 번역 결정이며 Knowledge 전역 승격이 아니다. 일반어·기법·문화어는 문맥별 자연스러운 초벌을 우선했고, `アトリエ`, `画材`, `目隠し`, `レプリカ`, `海の家`, `晴れ着` 같은 표현을 일괄 치환하지 않았다.

## 4. HOLD

| source | HOLD 행 |
|---|---:|
| `10028` | 2 |
| `40112073` | 13 |
| `40112093` | 1 |
| **합계** | **16** |

명시적인 비성인 대상이 성적 관찰·청취에 직접 포함된 기존 16행만 유지했다. 외모·호칭·복장이나 애매한 표현만으로 새 HOLD를 만들지 않았다.

## 5. 중복 source

- `301001`–`314006`의 중복 대상 26편: `full_draft_004_short_301001_314006`
- `40113103`, `40113111`, `40113112`: `full_draft_303_story_40113103_40113111_40113112`
- `99900003`: `full_draft_005_short_20001_99900003`

`303001`은 선택 variant의 이중 부정을 별도 교정했다.

## 6. 검수 방식의 한계

전체 행에 구조·용어·Names·HOLD·중복·일관성 진단을 수행했고, 진단 후보와 기존 검수 후보를 JP 및 장면 문맥으로 직접 재대조했다. 모든 114,243행을 처음부터 새 문학 번역으로 다시 쓴 것은 아니며, 근거 없는 취향 교정은 배제했다.

## 7. 최종 검증

- 결정 JSON Schema: PASS
- 대상 1,499 / 누락·중복 0
- 최종 행 114,243
- 비-HOLD 빈 KO 0
- 태그·`<br>` 순서 불일치 0
- 마크업 밖 가나 잔존 0
- legacy 이름 표기 잔존 0
- 주요 현행 고유명사·용어 불일치 0
- scoped Knowledge 자동 전역 승격 0
- 일반어·기법 일괄 치환 0
- 변경 사유 누락 0
- clean context 431개
- 최종 validator issue 0

## 8. 후속 범위

Codex는 정돈 Knowledge를 로컬 checkout에 반영하고, 기존 초벌 KO를 그대로 사용해 clean context의 fresh batch/review를 재생성한다. `PRO_REVIEW_DECISIONS.json`과 `PRO_KNOWLEDGE_CHANGES.json`을 exact source/index로 연결한 뒤 `novel check → novel apply → package 검증 → idempotency → 정확한 diff 확인`까지 수행한다.

이 PRO 단계에서는 apply, package 재생성, 게임 runtime, Git commit·push·PR·merge를 실행하지 않았다.
