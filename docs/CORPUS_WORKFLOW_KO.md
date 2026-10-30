# 현재 Novel·Master·UI corpus 운영

통합 대상은 번역의 조회·문맥·검수 절차다. 게임은 기존 `translations/ko/{names,master,ui}.json`, `ui.templates.json`, `novels/<id>.json`을 그대로 읽는다. 별도의 runtime authority나 두 번째 용어 DB는 만들지 않는다.

## 정본과 경계

| 자료 | 역할 |
|---|---|
| `translations/ko/names.json` | exact 인명 표시 정본. 일반 명사 전체 치환 규칙이 아님 |
| `translation/knowledge.jsonl` | 현재 승인된 지식. 각 record의 scope를 유지 |
| `translations/ko/novels/`, `master.json` | 현재 번역과 원문 exact key |
| `ui/general-exact.reviewed.json`, `ui/resources.json`, `ui.templates.json`, UI family 파일 | 현재 UI authoring 입력 |
| `ui.json` | exporter가 만든 runtime 결과. 직접 교정하지 않음 |
| `work/corpus/` | 재생성 가능한 색인·검수 묶음. 새 정본이 아님 |
| `knowledge-layer/provenance/` | 변경 이력. 정상 번역 입력에서 제외 |

legacy `zh_Hans`, 과거 Knowledge 원장, archive, 백업은 자동 검색·문맥 생성에서 제외한다. 지금 유효한 3,535개 Knowledge를 일괄 재분류·재생성하지 않는다. 중복처럼 보여도 scope와 근거를 비교한 뒤 개별 승인한다.

`reference`는 current에 있지만 이번 대상의 승인 범위 밖인 참고 지식이다. legacy라는 뜻이 아니다. current record의 출처 메타데이터가 provenance 경로를 가리키는 것은 허용하며, 그 경로의 과거 원장을 정상 번역 입력으로 다시 읽지는 않는다.

## 조회 → 의미검수 → 반영

명령은 저장소 루트에서 실행한다. 출력 폴더는 새 경로를 지정한다.

```powershell
python -X utf8 tools/corpus_consistency.py scan --output work/corpus/audit --term プランタン --term ヴォルカン
python -X utf8 tools/corpus_consistency.py prepare --output work/corpus/review-01 --term プランタン
# 또는 index.jsonl의 정확한 occurrence id로 범위를 좁힌다.
python -X utf8 tools/corpus_consistency.py prepare --output work/corpus/review-one --id <occurrence-id>
```

`scan`은 위치·exact JP·현재 KO·파일 해시를 `index.jsonl`에 기록하고 동일 JP의 서로 다른 KO와 지정 용어의 등장 위치를 ledger에 모은다. **후보 수는 오역 수가 아니다.** 같은 JP라도 인명/일반명사, 장면·화자·문체에 따라 번역이 다를 수 있다. UI export와 authoring의 중복도 별도 occurrence로 표시한다. JP가 다른 표현의 의미상 동의어는 자동으로 판단하지 않으므로 `--term`으로 추가 조사한다.

`prepare`의 `batch.json`에는 선택 위치, current Names/Knowledge, Novel 앞뒤 2개 dictionary 항목을 담는다. 인접 dictionary 항목은 원본 script 화자/진행 순서의 확정 근거가 아니다. cross-corpus 문맥은 특정 Novel scope를 주장하지 않는다. source-scoped 지식은 참고로 남고 전역 용어가 되지 않는다. `ui_export`와 기존 HOLD placeholder는 교정 대상으로 선택하지 않는다.

PRO는 `review.json`에서 다음을 결정한다.

- `keep`: 문맥상 유지. `reason`에 이유를 기록한다.
- `correct`: 해당 위치의 `ko_final`과 수정 근거를 기록한다. KO 문자열 전체 검색·치환은 하지 않는다.
- `hold`: 용어 정책 또는 문맥이 불명확한 후보. 근거를 남기고 정본은 그대로 둔다. Novel 콘텐츠 HOLD 정본을 변경하는 명령은 아니다.
- `pending`: 아직 검수하지 않은 위치. 완료로 세지 않는다.

판단 근거에 `CANONICAL_CONFLICT`, `TYPO`, `INTENTIONAL_STYLE`, `CONTEXTUAL_VARIANT`, `POLICY_PENDING` 등 문제 유형을 함께 적으면 다음 검수에 도움이 된다. UI를 항상 우선하거나 Novel 전체를 같은 단어로 강제 치환하지 않는다.

검수자는 실제 `review_id`, `pro_review_complete: true`, `knowledge_review`를 기록한다. 신규 지식이 없으면 그 판단을 적고 `changes: []`를 유지한다. 신규/수정 Knowledge는 기존 Novel과 같은 승인 변경 형식을 사용한다. 의미 검수는 도구가 대신 수행하지 않는다.

```powershell
python -X utf8 tools/corpus_consistency.py apply --batch work/corpus/review-01/batch.json --review work/corpus/review-01/review.json --output work/corpus/applied-01
```

반영은 기존 두 publisher의 lock을 사용한다. 선택 위치·사용 문맥의 preimage 확인 → 임시 package에 KO 교정 → 기존 UI exporter/manifest builder → 변경 파일과 before/after 이력 반영 순서다. 원문 키·기존 HOLD·비선택 번역은 바꾸지 않는다. Novel 제목을 바꾸면 대응 `mNovels.title/ml_title[]`도 같은 검수에서 일치시킨다. UI family로 생성되는 문자열은 원래 family 입력에서 고치며 exporter 충돌은 그대로 드러낸다.

실제 변경 이력은 `knowledge-layer/provenance/corpus-changes/`에 저장된다. Git commit/push/merge, DLL 빌드, 게임 실행은 이 명령이 수행하지 않는다. 이미 반영한 batch는 preimage가 달라지므로 후속 교정은 다시 prepare한다.

### 제목 대응과 숫자 표기

제목을 서로 찾을 때는 `sync_novel_titles.py`와 같이 JP를 비교한다. 전각 숫자·전각 공백의 폭 차이는 비교할 때만 정규화하며, 저장된 JP 키는 보존한다.

Master 교정에서 일반 숫자 검사가 `NUMBER_MISMATCH`를 반환하면 아래 여섯 위치에 한해 숫자 표기의 동등성을 확인한다. 이때는 폭 차이를 정규화하지 않은 **exact JP**로 Novel 제목이 하나만 대응해야 한다. 그 Novel의 ID와 승인 한국어가 표와 일치하고 Master의 최종 KO도 같아야 한다. Novel 제목은 현행 정본 또는 같은 검수에서 승인한 최종값을 사용한다. 대응 제목이 없거나 여러 개이거나 보류 상태이면 거절한다.

| exact JP | 승인 KO | Novel ID | 허용 Master 필드 |
|---|---|---|---|
| 二代目 | 2대째 | 10010802 | `mQuestStages.ml_name[]` |
| 二代目との接触 | 2대째와의 접촉 | 11011102 | `mQuestStages.ml_name[]` |
| ３人の出会い | 세 사람의 만남 | 11011001 | `mQuestStages.ml_name[]` |
| ３人の少女たち | 세 명의 소녀들 | 10011006 | `mQuestStages.ml_name[]`, `mNovels.title`, `mNovels.ml_title[]` |

숫자 의미는 표의 승인된 번역 쌍으로 한정한다. 예를 들어 `二代目 → 3대째`는 거절한다. 허용된 각 위치는 적용 이력의 `numeric_title_equivalences`에 Novel ID 및 Master 필드와 함께 기록한다. 가격·수량·레벨·확률 등 다른 Master 필드, UI, Novel 본문에는 이 예외가 적용되지 않는다.

## Knowledge를 다시 섞지 않는 방법

Novel과 Master/UI 모두 `translation_context()`로 current만 읽는다. 원문에 실제 등장하는 승인 용어 중 현재 scope가 맞는 항목은 문맥 크기 제한의 예외로 포함하고 초과 여부를 표시한다. 나머지는 기존 관련성·크기 제한을 유지한다. `omitted_ids`는 참고자료 생략 목록이며 신규 지식 누락이 없다는 증명이 아니다.

같은 지식의 수정은 기존 ID를 유지한다. 중복 record의 폐기만 필요한 경우 다음처럼 명시적으로 검수한다.

```json
{
  "kind": "knowledge",
  "id": "기존-record-id",
  "operation": "retire",
  "old_sha256": "기존-record의-SHA256",
  "approved": true,
  "reason": "어느 current record로 통합했고 왜 기존 scope를 보존할 수 있는지"
}
```

폐기·수정 전 내용은 apply와 함께 history에 보존한다. history를 읽어 current를 재구축하지 않는다. prepare 후 추가된 지식 중 이번 원문에 적용되는 신규 용어만 문맥 재검토 대상으로 삼는다. 무관한 새 지식 때문에 모든 배치를 다시 만들지는 않는다.

일반 신규 Novel의 `prepare → review → check/apply`에도 `knowledge_review`를 연결한다. 이는 후보 검토를 기록하는 조건이며 새 관계·프로필·사건을 빠짐없이 자동 추출한다는 보증은 아니다. 기존 Novel의 같은 검수본 재적용은 유지한다. corpus의 재prepare 정책을 Novel에 확대하지 않는다.

## Novel 표시 규칙

새 Novel 배치와 corpus의 Novel 교정에 **본문 34 UTF-16칸 × 최대 3줄**을 적용한다. 일반 한글·공백·문장부호는 1칸, BMP 밖 문자는 2칸이다. `<br>`와 실제 줄바꿈은 구분자, 태그 자체는 0칸이며 ruby/click 내부 표시 글자는 센다. 제목·UI에는 본문 제한을 확대하지 않는다.

본문에는 화자 없는 서술·독백도 포함된다. `dialogue` 표시는 화자 유무가 아니라 본문 규격의 적용 여부다. 두 문자로 저장된 literal `\n`은 기존 보호 토큰대로 보존하며 실제 줄바꿈이나 `<br>`로 자동 변환하지 않는다. 현재 길이 계산에서는 두 문자로 센다.

KO 줄바꿈 위치와 경계 공백은 조정할 수 있다. JP key·다른 태그·변수는 보존한다. 의미를 잘라내지 말고 자연스러운 어절 단위로 교정한다. corpus의 기존 KO 교정에서는 ruby/click 블록 전체를 보존한다. 해당 블록의 용어 자체를 바꿔야 한다면 별도의 명시적 제어문자 검수로 처리한다. 단순 길이 초과를 콘텐츠 HOLD로 바꾸지 않는다.

구조 검사는 줄바꿈을 지우기 전의 원본 토큰을 비교한다. `{PLA<br>YER}`처럼 변수 내부를 나누거나 태그 이름·속성 안에 줄바꿈을 넣은 입력은 거절한다. 태그를 제외한 양쪽 본문도 합쳐서 새 변수로 해석하지 않는다. 일반 표시문 영역의 줄바꿈만 재배치할 수 있다.

이는 기존 의미 검수에 추가되는 표시 규칙이며 픽셀 단위 적합성 보증은 아니다. 이미 준비된 구형 Novel 배치에는 새 필드나 줄 규격을 소급 강제하지 않는다. 새로운 runtime 계층이나 전수 Native gate도 추가하지 않는다.
