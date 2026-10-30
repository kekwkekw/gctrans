# Girls' Creation 한국어화 — 현재 운영 정본

이 문서 하나만 읽고 정상 Novel 번역·검수·반영 경로를 시작할 수 있어야 한다. 명령은 저장소 루트에서 실행한다.

Master/UI와 GCMod LocalParity의 현행 수집·검수·배포 준비는 [LOCAL_PARITY_PIPELINE_KO.md](LOCAL_PARITY_PIPELINE_KO.md)를 따른다. 아래 Novel 운영 절차는 그대로 유지한다.

현재 소스·번역의 기준은 작업을 시작할 때 확인한 public `main`이다. 실제 PC의 설치 상태, selected current, DLL, 설정과 font는 저장소 상태와 별도로 확인한다.

## 1. 정본과 우선순위

| 위치 | 역할 |
|---|---|
| `translations/ko/names.json` | exact JP 이름 표시의 현재 정본 |
| `translation/knowledge.jsonl` | 현재 번역에 실제로 사용하는 유효 Knowledge |
| `translation/HOLD_POLICY.md` | 사용자가 동결한 유일한 HOLD 판단 기준 |
| `translation/holds.jsonl` | 현행 Novel 작업에서 확정한 행별 HOLD 기록 |
| `translations/ko/novels/` | 검수 완료 후 반영하는 GCMod 7 Novel |
| `work/novels/<batch>/` | 원문·초벌·PRO 자료를 두는 Git 제외 작업 폴더 |
| `knowledge-layer/provenance/` | Knowledge 변경의 역사·근거 보존. 정상 번역 입력이 아님 |

우선순위는 다음과 같다.

1. 현재 JP exact key와 현재 장면
2. `translations/ko/names.json`의 exact 이름 정본
3. `translation/knowledge.jsonl`의 명시된 scope 안에서 유효한 Knowledge
4. 실제 PRO 검수 결정

`knowledge-layer/`, Git 과거 commit, 오래된 보고서·후보·conflict는 정상 초벌이나 PRO의 기본 입력으로 사용하지 않는다. 필요성이 확인된 별도 조사에서만 역사 자료로 조회한다.

## 2. 역할

### Codex

- 현재 public main 확인과 작업 checkout 유지
- source scan·원문 취득·원문 정리
- `novel prepare`와 Codex 초벌
- 구조·태그·변수·정확한 JP identity 검사
- PRO 검수 결과의 exact import
- `novel check`·`novel apply`
- package와 diff 검증
- 사용자가 승인한 범위의 commit·push·PR·merge

### ChatGPT PRO

- JP→KO 의미·누락·추가 검수
- 말투·호칭·관계·사건 연속성 검수
- 행별 HOLD 결정 검수
- Names/Knowledge 변경안의 의미와 적용 범위 검수

도구의 완료 flag나 기계 검사는 실제 PRO 의미검수를 대신하지 않는다.

## 3. Knowledge 계약

### Current Knowledge

`translation/knowledge.jsonl`에는 현재 번역에 실제 사용할 수 있는 지식만 둔다.

- 현재 유효한 캐릭터 성격·말투·호칭
- 인물 관계와 관계 변화
- 사건 연속성과 현재 이해에 필요한 과거 사건
- 작품 고유 용어·조직·장소·기술
- 과잉 일반화와 오역을 막는 scoped guard

record는 `approved`, `content_available: true`, 실제 내용, 명시적 scope와 `origin.review_id`를 가져야 한다. `approved`는 명시된 scope 안에서 사용할 수 있다는 뜻이며 전역 승격을 뜻하지 않는다.

### Names

같은 entity의 이름 표기는 current Names가 우선한다. Knowledge의 과거 관찰형·후보형 표기가 Names를 덮어쓰지 않는다. Names는 exact 이름 표시 정본이며 모든 일반 명사를 대체하는 규칙은 아니다.

### Knowledge 변경과 history

새 Knowledge가 기존 active Knowledge를 대체하면 같은 PRO 검수에서 다음을 함께 처리한다.

1. 기존 record의 preimage와 대체 사유를 최소 provenance로 보존한다.
2. 기존 record를 active 파일에서 제거하거나 새 current record로 교체한다.
3. `translation/knowledge.jsonl`에는 최신 current record만 남긴다.
4. 해결 전 후보·conflict·superseded 값은 정상 context에 넣지 않는다.

과거 원장에서 active Knowledge를 재생성하지 않는다. 정상 운영은 현재 `translation/knowledge.jsonl`만 읽는다.

Novel과 Master/UI 배치는 같은 current-only 문맥 함수를 사용한다. 원문에 실제 등장하고 scope가 맞는 승인 용어는 선택적 참고자료의 크기 제한 때문에 빠지지 않는다. 다른 시나리오의 관찰은 계속 reference이며, corpus 통합을 이유로 전역 승격하지 않는다.

새 배치의 `knowledge_review`에는 신규 후보·중복·충돌을 검토한 결과를 기록한다. 신규 후보가 없다는 결과도 유효하며 `changes: []`를 허용한다. 같은 지식의 갱신은 기존 ID를 재사용한다. 승인된 폐기에는 `operation: "retire"`, 기존 `old_sha256`, 사유가 필요하다. Novel apply는 실제 변경된 Names/Knowledge의 이전·이후 값을 `knowledge-layer/provenance/current-changes/`에 함께 저장한다. 이 경로는 정상 문맥에서 읽지 않는다.

현재 corpus의 위치별 대조와 기존 번역 교정은 [CORPUS_WORKFLOW_KO.md](CORPUS_WORKFLOW_KO.md)를 따른다. 자동 병합이나 기존 Knowledge 전체 재정리는 하지 않는다.

현재 요약은 다음으로 확인한다.

```powershell
python -X utf8 tools/translation_knowledge.py
python -X utf8 tools/update/cli.py novel --help
```

## 4. Novel 파이프라인

정상 순서는 다음과 같다.

> 업데이트 확인 또는 재번역 대상 선정 → JP 원문 정리 → Codex 초벌 → `novel review` 구조 검사 → ChatGPT PRO 의미검수 → `novel check` → `novel apply` → package·diff 검증 → 승인된 Git 반영

### 4.1 업데이트 확인과 raw 취득

기존 source state와 private raw cache를 계속 사용한다.

```powershell
python -X utf8 tools/update/cli.py scan -- --platform WebGL --lane Novel --acquire --state-root source-state --raw-root private-source-cache --output source-out/scan.json
```

- `--acquire`는 실제 raw 취득을 시도한다.
- metadata만 비교할 때는 저장 inventory를 사용하고 `--acquire`를 생략한다.
- PC asset은 명시적 local asset 경로를 사용한다.
- 관측·추출 실패는 해당 source의 기술적 오류이며 콘텐츠 HOLD가 아니다.
- source 삭제 후보가 번역 파일을 자동 삭제하지 않는다.

외부 HTTP·Unity decoder·local helper는 `docs/EXTERNAL_DEPENDENCIES_KO.md`를 따른다.

### 4.2 TextAsset 또는 저장 원문으로 prepare

```powershell
python -X utf8 tools/update/cli.py novel prepare --repo . --batch work/novels/<batch> --source-snapshot source-out/scan.json --raw-root private-source-cache --state-root source-state --source-id <source-id>
```

이미 확보한 원문 파일은 `--source-manifest`로 사용한다. 여러 편은 `--source-id`를 반복한다.

prepare가 만드는 자료:

- `batch.json`
- `source/`
- 동일한 `HOLD_POLICY.md`
- 현재 Names와 필요한 current Knowledge를 담은 `context.json`
- `draft.json`
- `TASK.md`

기존 저품질 KO는 초벌 번역 근거로 넣지 않는다.

작업 이유:

- 기존 저품질 번역 재번역: `legacy_retranslation`
- 기존 번역 교정: `translation_correction`
- 실제 원문 변경: `source_update`

### 4.3 현재 Novel JP key 기반 재번역

raw 취득이 후순위인 기존 재번역은 특정 checkout의 Novel 사전에 보존된 JP exact key를 입력으로 사용할 수 있다.

```powershell
python -X utf8 tools/update/cli.py novel prepare --repo . --batch work/novels/<batch> --source-current-jp-keys --source-id <source-id> --reason legacy_retranslation
```

이 경로는 fresh crawl이나 TextAsset 확인을 주장하지 않는다.

- JP key 순서와 bytes를 그대로 보존한다.
- 기존 KO는 snapshot·context·초벌 입력에 넣지 않는다.
- speaker·command·physical line처럼 확인하지 않은 정보는 추정하지 않는다.
- 대상 Novel의 preimage가 바뀌면 덮어쓰지 않는다.

### 4.4 Codex 초벌

Codex는 원문 전체, 동결 정책, 현재 Names와 current Knowledge를 읽고 `draft.json`에 다음을 작성한다.

- `hold`
- 필요한 `reason`
- `ko`
- 필요한 Names/Knowledge 변경 제안

JP·speaker·행 식별 필드는 바꾸지 않는다. 태그·변수·literal `\n`·보호 토큰을 exact하게 보존한다. 새 배치(`text_rules: gc-novel-34x3-v1`)는 KO의 `<br>`·실제 줄바꿈·경계 공백을 재배치할 수 있다. 대사 본문은 한 줄 최대 **34 UTF-16칸, 최대 3줄**이며 공백·문장부호·따옴표도 센다. 태그는 제외하고 ruby/click 안의 표시 문자는 포함한다. 제목에는 이 본문 제한을 적용하지 않는다.

조사·어절·고정 용어를 부자연스럽게 나누거나, 제한에 맞추려고 의미를 삭제하지 않는다. ruby/click 블록 내부에는 줄바꿈을 넣지 않는다. 기존 KO의 제어 블록 내용 변경은 단순 줄 재배치와 구분해 명시적으로 검수한다. 초벌은 길이를 넘겨도 PRO 교정용으로 전달할 수 있고, 최종 check/apply에서 본문 규격을 확인한다. 길이 초과 자체를 HOLD 사유로 삼지 않는다. 규칙 표식이 없는 기존 배치는 원래의 exact 줄바꿈 계약을 유지한다.

### 4.5 PRO 검수 자료 준비

```powershell
python -X utf8 tools/update/cli.py novel review --repo . --batch work/novels/<batch>
```

이 명령은 구조 검사와 `review.json` 준비만 수행한다. ChatGPT PRO는 원문·정책·context·초벌을 실제로 읽고 다음을 검수한다.

- 의미와 누락·추가
- 말투·호칭·추정 강도
- 에피소드 사이 일관성
- 행별 HOLD와 표시문
- 필요한 Names/Knowledge 변경

완료한 `review.json`에는 실제 최종 행, `review_id`, `pro_review_complete: true`와 새 배치의 `knowledge_review`를 기록한다.

HOLD 출력은 `[시나리오명 - N행 HOLD]` 형식이다. N은 제목을 포함한 번역 대상 행의 1부터 시작하는 순번이다. JP key는 유지한다.

### 4.6 check와 apply

```powershell
python -X utf8 tools/update/cli.py novel check --repo . --batch work/novels/<batch>
python -X utf8 tools/update/cli.py novel apply --repo . --batch work/novels/<batch>
```

`apply`는 원문·검수본·현재 대상의 일치와 문자열 구조를 검사하고 다음을 로컬 checkout에 반영한다.

- 검수 완료 Novel
- 승인된 Names 변경
- 승인된 current Knowledge 변경
- 행별 HOLD 기록
- package metadata

선택하지 않은 Novel·UI·Master는 보존한다. `apply`는 원격 Git 작업이나 게임 설치를 수행하지 않는다.

### 4.7 package와 Git 경계

```powershell
python -X utf8 tools/update/cli.py preflight --candidate-root . --report <local-preflight.json>
python -X utf8 tools/verify_staged_release.py translations/ko --report <local-staging.json>
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:NuGetAudit=false
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <isolated-store> --package <package-path> --based-on NONE
```

Codex는 적용 후 정확한 diff와 package를 확인한다. 대상 선정이나 PRO 검수 요청만으로 public commit·push·PR·merge 권한을 확대하지 않는다. 사용자가 특정 범위의 Git 반영까지 승인했다면 같은 범위에서 반복 승인 요구를 만들지 않는다.

## 5. UI / Master

UI와 Master는 Novel 행 구조에 합치지 않고 전용 경로를 사용한다.

```powershell
python -X utf8 tools/update/cli.py plan-ui-edit --package translations/ko --resource-id <resource-id> --new-ko <reviewed-ko> --draft <local-draft.json> --report <local-plan.json>
python -X utf8 tools/update/cli.py plan-master-key --package translations/ko --current-source <snapshot.json> --table <table> --runtime-field <field> --jp <exact-jp> --new-ko <ko> --draft <local-draft.json> --report <local-plan.json>
python -X utf8 tools/update/cli.py apply-review --package translations/ko --review <review.json> --state-root <local-authoring-state> --report <local-apply.json>
```

지원 범위는 `docs/SUPPORT_SCOPE_KO.md`를 따른다.

## 6. 게임 적용과 6.1 호환

Novel·Names·Master 변경은 완전 package와 재시작 경계를 사용한다. 파일 검증과 실제 게임 화면 검증을 같은 결과로 기록하지 않는다. XUnity는 OFF를 유지한다.

GCMod 6.1 사용자를 위해 다음 public 경로를 유지한다.

- `names/zh_Hans.json`
- `words/zh_Hans.json`
- `novels/<id>/zh_Hans.json`
- `GCMod-cs파일백업/GCMod-6.1.0-updated/`

현행 GCMod 7 정본은 `translations/ko/**`다. 정상 GCMod 7 작업이 6.1 경로를 임의 삭제·이름 변경하지 않는다.

## 7. 사용하지 않는 과거 경로

정상 운영은 다음을 읽거나 실행하지 않는다.

- private workbench
- 과거 WB13~M4 파이프라인
- 과거 Knowledge snapshot에서 active Knowledge를 재생성하는 경로
- 과거 공개 검수 viewer·archive
- 퇴역한 자동 번역 writer
- historical receipt를 현재 번역 authority로 사용하는 경로

역사 확인은 Git history와 필요한 최소 provenance로 수행한다. 현재 작업자는 이 문서와 실제 main의 CLI를 기준으로 판단한다. `.github/workflows/crawl_only.yml`은 선택적인 one-shot metadata 확인용이며 raw 취득이나 자동 번역 writer가 아니다.
