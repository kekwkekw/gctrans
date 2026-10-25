# Girls' Creation Translation Knowledge 현행본 정돈 보고서

- 정돈 ID: `GC-KNOWLEDGE-CURRENT-STATE-PRO-20261024`
- 기준 commit: `8cebfe3e51cd0659bdd8417f55796f9d9968484d`
- 입력: 마네 첫 batch Knowledge까지 포함한 `translation/knowledge.jsonl` 5,647건
- 결과: 현행 active Knowledge 3,534건
- 1,499편 번역 PRO 검수: **이번 단계에서 실행하지 않음**

## 정돈 원칙

1. 과거 origin이라는 이유만으로 유효한 지식을 삭제하지 않았다.
2. 캐릭터 말투·호칭·관계·사건·세계관·용어와 과잉 일반화 방지 guard를 현재 정보로 유지했다.
3. Names와 충돌하는 과거 이름 후보는 Names 정본으로 교정하고, 후보/충돌 기록은 knowledge-layer 이력으로만 남겼다.
4. raw observation/evidence/reference wrapper는 현재 요약에 흡수하거나 history-only로 분류했다.
5. 미해결 후보는 번역 prompt에 노출하지 않고 후속 판단 자료로 보존했다.
6. 모든 active record는 `approved`이지만, 승인은 명시된 source scope 안에서만 유효하다. 전역 승격을 뜻하지 않는다.

## 수량

| 종류 | 입력 | 현행 |
|---|---:|---:|
| `candidate_gap` | 71 | 0 |
| `claim_development` | 206 | 206 |
| `claim_guard` | 1,076 | 1,072 |
| `conflict` | 2 | 0 |
| `display_alignment` | 2 | 0 |
| `editorial_preference` | 6 | 0 |
| `entity` | 102 | 102 |
| `evidence_link` | 308 | 0 |
| `preference_evidence` | 1 | 0 |
| `profile` | 85 | 85 |
| `profile_observation` | 463 | 190 |
| `reference` | 460 | 230 |
| `relationship` | 607 | 520 |
| `speech` | 1,763 | 914 |
| `term` | 495 | 215 |

| **합계** | **5,647** | **3,534** |

### 주요 통합

- profile observation: 463개 원행 → 190개 profile/source-context continuity record
- speech: 1,763개 원행 → 914개 화자·상대·발화모드 rule. 호칭뿐 아니라 기존 PRO가 정리한 말투·독백·행동 관찰도 보존했다.
- relationship: 607개 원행 → 520개 관계 슬롯
- evidence/candidate/conflict 기록은 active에서 제거했지만 원본 `knowledge-layer`와 migration map에 provenance를 유지했다.

### 5,647건의 처리 결과

| 처리 | 수량 | 의미 |
|---|---:|---|
| 기존 record를 현행 형태로 유지 | 3,122 | 현재도 유효한 의미 내용을 정리해 유지 |
| 여러 record를 현행 record로 통합 | 1,621 | 말투·관계·profile observation 등의 중복 wrapper를 하나로 흡수 |
| history-only | 904 | 원시 evidence, 중복 binding, Names 중복, 미해결 후보, 해결된 conflict |

`history-only`는 삭제가 아니다. 기존 `knowledge-layer` 원본은 그대로 두며, `MIGRATION_MAP.jsonl`과 `ACTIVE_SOURCE_MAP.jsonl`로 현행 record와 역사 record의 연결을 추적할 수 있다.

### 현행 Knowledge가 보존하는 내용

- entity 102건과 profile 85건
- 후속 성격·사건 관찰 190개 묶음
- 관계와 관계 변화 520건
- 상대별 호칭·말투·독백·행동 관찰 914건
- 사건 전개 206건과 과잉 일반화 방지 guard 1,072건
- 현행 용어 215건과 문맥 한정 identity/alias 230건

과거에 일어난 사건이라는 이유만으로 history-only로 내리지 않았다. 현재 시나리오의 인물·관계·사건 연속성을 이해하는 데 유효하면 active에 남겼다.

## 이름 정본

다음 legacy 표기는 active Knowledge에서 0건이다.

- `앵그르` → `앵글`
- `피오렌체` → `피오렌차`
- `페르메이르` → `페르메르`
- `안프룰러` → `안프룰라`
- `안쥬` → `안주`
- `쟝 형사` → `장 형사`
- `아테네스 경찰청` → `아테네스 시경`
- `쿠마로보` → `곰 로봇`
- `플라워 맨` → `플라워맨`
- `스킵피오` → `스키피오`

## 정돈의 근거와 한계

- 기존 5,647건의 prior PRO-reviewed 의미 기록과 현재 Names 정본을 기준으로 reconciliation했다.
- record의 생성 시점이나 `reviewing`이라는 옛 상태만으로 삭제하지 않았다.
- 원문 114,243행을 이번 단계에서 다시 번역·의미검수하지 않았다. 미해결 후보는 history-only로 남겨 다음 번역 PRO가 필요할 때 별도 조회할 수 있게 했다.
- 431개 기존 batch에 대해 현행 Knowledge projection smoke를 수행했으며 0개 선택 context나 구조 오류는 없었다.

## 이번 단계에서 하지 않은 일

- 1,499편 Codex 초벌 수정 또는 재생성
- 이전 PRO 번역 결정 재사용·적용
- `novel review/check/apply`
- package 재생성
- Git commit/push/PR/merge
- 게임 runtime 검증

다음 단계에서는 이 현행 Knowledge를 기준으로 새 context를 생성한 뒤, 기존 1,499편 초벌을 새로 PRO 검수한다.
