# 현재 번역·업데이트 운용

명령은 저장소 루트에서 실행한다. 현재 소스·번역의 기준은 public `gctrans`의 작업 대상 main이며, 실제 PC의 설치 상태는 별도로 확인한다.

## 운영에 사용하는 파일

| 위치 | 용도 |
|---|---|
| [translation/HOLD_POLICY.md](../translation/HOLD_POLICY.md) | 사용자가 동결한 HOLD 판단 기준 원본 |
| [translation/knowledge.jsonl](../translation/knowledge.jsonl) | 현재 번역에 사용하는 실제 Knowledge 값·범위·상태·근거 |
| [translations/ko/names.json](../translations/ko/names.json) | 현재 GCMod 7 Names 정본 |
| [translation/holds.jsonl](../translation/holds.jsonl) | 새 흐름에서 기록한 행별 HOLD 결정과 사유 |
| `work/novels/<batch>/` | 로컬 원문·초벌·PRO 검수 작업 폴더. Git에서 제외 |
| `translations/ko/novels/` | 검수 완료 후 반영하는 Novel 번역 |

HOLD 판단은 위 정책 파일을 그대로 읽어 수행한다. 이 문서나 도구에 별도의 판단 조건을 두지 않는다. `holds.jsonl`은 새 작업의 결정 기록이며 과거 기록 전체의 재판정 결과가 아니다.

Knowledge의 현재 수량·상태·해시는 다음 명령으로 확인한다. 과거 원장과 승인 기록은 출처로 보존되며, 일상 번역에서는 현재 파일을 사용한다.

```powershell
python -X utf8 tools/translation_knowledge.py
python -X utf8 tools/update/cli.py novel --help
```

일상 번역·조회·반영에는 이 저장소의 도구와 현재 파일만 사용한다. `tools/translation_knowledge.py --initialize`는 과거 원장을 현재 파일로 모으는 최초 통합용 명령이며, 그때만 과거 원장 파일이 필요하다. 현재 `translation/knowledge.jsonl`이 이미 있으면 덮어쓰지 않는다. 정상 작업에서 초기화를 다시 하거나 private workbench를 준비할 필요는 없다.

## Novel: 업데이트 → 초벌 → PRO 검수 → main 반영

### 1. 업데이트 확인과 JP 원문 정리

사용자의 업데이트 요청을 받으면 기존 crawl 경로를 실행한다. 같은 `source-state`와 `private-source-cache`를 계속 사용하고, scan 출력은 필요하면 작업별 경로로 나눈다.

```powershell
python -X utf8 tools/update/cli.py scan -- --platform WebGL --lane Novel --acquire --state-root source-state --raw-root private-source-cache --output source-out/scan.json
```

`--acquire`는 기존 source endpoint에서 필요한 raw를 취득한다. 실행에 필요한 HTTP·Unity decoder·로컬 helper는 [외부 입력](EXTERNAL_DEPENDENCIES_KO.md)을 따른다. 저장된 inventory만 비교하려면 `--inventory <saved-inventory.json>`을 주고 `--acquire`를 생략한다. PC 저장 asset은 기존 `--platform PC --local-asset <bundle> --asset-key <known-key>` 경로를 사용한다.

`scan.json`의 변경 내용과 `actionable-intake.json`의 미처리 작업에서 이번 대상 ID를 선택한다. metadata만 확보한 항목은 raw 취득 후 진행한다. 관측·추출 오류는 해당 source의 오류로 보고한다. 첫 실행의 baseline과 이후 비교 결과는 구분하며, source 삭제 후보가 번역 파일을 자동 삭제하지는 않는다.

기존 scan의 `--source-ids`는 강제 재취득 목록이다. 아래 `novel prepare --source-id`는 실제 번역할 대상 선택이다.

### 2. 작업 폴더 생성과 Codex 초벌

아래 `<batch>`와 `<source-id>`는 실제 작업 이름과 선택 ID로 바꾼다. 여러 편은 `--source-id`를 반복한다.

```powershell
python -X utf8 tools/update/cli.py novel prepare --repo . --batch work/novels/<batch> --source-snapshot source-out/scan.json --raw-root private-source-cache --state-root source-state --source-id <source-id>
```

작업 폴더에 `batch.json`, 정확한 원문 사본 `source/<id>.txt`, 동일한 `HOLD_POLICY.md`, 필요한 Names/Knowledge를 담은 `context.json`, `draft.json`, `TASK.md`가 생성된다. Codex는 이 입력을 읽고 `draft.json`의 행별 `hold`, `reason`, `ko`와 필요한 `changes`를 작성한다. 원문·speaker·행 식별 필드는 바꾸지 않는다.

문맥 자료는 필요한 항목만 선택하며, 선택·누락 수는 prepare 결과와 `context.json`에 표시된다. 추가 문맥이 필요한 작업은 `--knowledge-max-chars`로 자료 크기를 조절한다. 이는 번역 행 수나 HOLD 판단 기준이 아니다.

등록이 필요한 선택 시나리오는 준비 명령에 `--register <source-id>`를 명시한다. 그 외 기존 미등록 항목의 상태는 유지한다. 기존 저품질 번역의 재번역은 `--reason legacy_retranslation`, 기존 번역의 교정은 `--reason translation_correction`으로 기록한다. 과거 저품질 KO를 초벌의 번역 근거로 사용하지 않는다.

이미 확보한 원문 파일로 시작할 때에는 `--source-snapshot` 대신 `--source-manifest <sources.json>`을 사용한다. source manifest의 경로는 manifest 파일 기준이며, 원문 파일과 이 manifest는 로컬 작업 자료다.

```json
{
  "schema": "gc-novel-sources-v1",
  "sources": [
    {"source_id": "99999999", "path": "source/99999999.txt", "title": "작업할 시나리오명"}
  ]
}
```

`sha256`을 추가하면 저장 원문의 정확한 bytes를 확인한다. 이 입력 방식은 자체적으로 게임 서버의 최신 여부를 확인하지 않는다. `--state-root`는 scan snapshot에서 시작한 작업의 완료 기록에 사용한다.

### 3. PRO 검수

초벌이 끝나면 다음 명령으로 파일 검사를 하고 PRO 검수 파일을 만든다.

```powershell
python -X utf8 tools/update/cli.py novel review --repo . --batch work/novels/<batch>
```

ChatGPT PRO에 해당 작업 폴더의 검수 자료를 전달한다. 번역과 Names/Knowledge 변경안을 같은 묶음으로 검수한다. PRO가 완료한 `review.json`에는 최종 행·변경안, `review_id`, `pro_review_complete: true`가 기록된다. 이 표시는 실제로 수행한 검수의 기록이며 도구가 검수를 대신 수행하는 기능이 아니다.

HOLD 행의 KO 출력은 `[시나리오명 - N행 HOLD]`로 작성되고 사유가 남는다. 여기의 행 번호는 제목을 포함한 번역 대상 행의 1부터 시작하는 순번이며, 원문 파일의 물리적 줄 번호는 별도 필드로 보존한다. JP key는 그대로 유지한다.

Names/Knowledge의 `changes` 중 검수된 항목만 `approved: true`로 기록한다. 기존 참고 항목이나 문장 수락만으로 전역 규칙을 자동 승격하지 않는다. 현재 JP와 해당 문맥을 우선하고, 각 규칙의 적용 범위를 유지한다.

변경안 형식은 `prepare`가 생성한 `work/novels/<batch>/TASK.md`의 **선택적인 Names/Knowledge 변경안** 예시를 따른다. 초벌의 제안은 `approved: false`로 둔다. 기존 Names 변경에는 실제 `old_ko`, 기존 Knowledge 변경에는 해당 레코드의 `old_sha256`을 넣으며, 새 항목의 변경 전 값은 `null`이다. 승인하는 Knowledge 레코드에는 비어 있지 않은 실제 `value`와 적용 범위가 필요하다. 레코드의 `origin.review_id`는 최종 `review.json`의 `review_id`와 같아야 하며, `origin`에 원문 행 등 별도의 source/evidence도 기록한다.

### 4. 검수본 반영

```powershell
python -X utf8 tools/update/cli.py novel apply --repo . --batch work/novels/<batch>
```

기본 입력은 작업 폴더의 `review.json`이다. 별도 경로는 `--review <review.json>`으로 지정한다. `apply`가 원문·검수본·현재 대상 파일의 일치와 문자열 구조를 확인하고, 검수된 Novel·Names·Knowledge·HOLD 기록을 로컬 checkout에 반영한다. 필요한 package metadata는 기존 builder가 갱신한다. 선택하지 않은 Novel·UI·Master 내용은 보존한다.

적용 전 결과만 확인하려면 동일한 인자로 `novel check`를 사용한다. 구조 검사 오류는 오류로 반환하며 콘텐츠 HOLD 결정으로 바꾸지 않는다. JP의 Unicode·폭·kana·공백·문장부호·접미사를 정규화하거나 서로 다른 JP key를 합치지 않는다. 현재 runtime의 exact key와 태그·줄바꿈 표현을 보존한다.

채택한 일상 운영 순서는 **PRO 최종 검수 완료 → 적용 결과 확인 → 해당 변경을 main에 commit/merge**다. Codex가 실제 변경 목록을 확인하고 Git 반영을 수행한다. `novel apply` 자체는 원격 Git 작업이나 게임 설치를 실행하지 않는다.

scan의 `--state-root`를 연결한 작업은 성공적으로 적용한 source만 완료 처리한다. 적용 후 완료 기록만 실패하면 결과의 `APPLIED_LOCAL_COMPLETION_PENDING` 상태를 확인하고 같은 apply를 다시 실행해 완료 기록을 재시도한다. 이미 적용한 번역을 다시 생성하지 않는다.

Novel·Names 변경의 실제 게임 반영은 기존 완전 package와 재시작 경계를 사용한다. 파일 검사 결과는 실제 화면 검증 결과와 구분한다.

## UI / Master 작업

UI와 Master는 기존 전용 경로를 사용한다. Novel 작업 폴더나 행 구조에 합치지 않는다.

```powershell
python -X utf8 tools/update/cli.py plan-ui-edit --package translations/ko --resource-id <resource-id> --new-ko <reviewed-ko> --draft <local-draft.json> --report <local-plan.json>
python -X utf8 tools/update/cli.py apply-review --package translations/ko --review <review.json> --state-root <local-authoring-state> --report <local-apply.json>
```

UI draft의 검토 결정과 실제 대상의 source·binding·old-KO를 확인한다. 다음 UI 교정의 `--package`에는 적용 결과의 `next_package`를 사용한다. `plan-master-key --help`는 관측한 Master 원문의 같은 runtime key 전체를 검토하는 기존 경로다. 기존의 비활성 binding과 지원 범위는 [지원 범위](SUPPORT_SCOPE_KO.md)를 따른다.

## 패키지 확인과 게임 적용

```powershell
python -X utf8 tools/update/cli.py preflight --candidate-root . --report <local-preflight.json>
python -X utf8 tools/verify_staged_release.py translations/ko --report <local-staging.json>
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:NuGetAudit=false
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <isolated-store> --package <package-path> --based-on NONE
```

`prepare-local`은 완전 package와 `pending_restart`를 준비한다. 실제 selected current·설정·DLL·font는 기존 [개인 Stable 정책](PROJECT_STABLE_QUALIFICATION_POLICY_KO.md), [적용·복구 절차](LAUNCH_PROCEDURE_KO.md), [지원 범위](SUPPORT_SCOPE_KO.md)에 따라 확인한다. XUnity는 OFF를 유지한다.

기존 UI-only live 경로가 필요한 작업에서는 다음 명령을 사용한다.

```powershell
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll request-ui --store <trial-store> --package <next-package> --based-on <current-content-id>
```

실제 UI driver의 수신·commit·화면 표시까지 확인한다. Novel·Names·Master 변경은 이 UI-only 경로로 처리하지 않는다. 고정된 public commit으로 완전 package를 준비하려면 `prepare --store <isolated-store> --repository kekwkekw/gctrans --channel <commit-sha>`를 사용한다. 현재 공급 값은 `translations/ko/release-manifest.json`에서 읽고 오래된 문서의 ContentId를 현재 값으로 사용하지 않는다.

## 과거 경로와 기록

기존 `bind-intake-wb`, `apply-review`의 WB Novel return, `intake-failed`, `resume-intake-completion`은 이미 생성된 WB 자료의 호환·재현용으로 남아 있다. 위의 정상 Novel 경로는 private workbench를 읽거나 실행하지 않는다. private repository를 현행 프로젝트 상태의 기준으로 삼지 않는다.

- [구 경로 퇴역·보존 범위](RETIRED_PIPELINES_KO.md)
- [이전 운영 문서 원본](https://github.com/kekwkekw/gctrans/blob/5d05dd5c03a5da8e69cfcf04380eaa33a9f841b0/docs/OPERATOR_KO.md)
- [고정 Git export와 재현](REPRODUCE_KO.md)
- [과거 qualification](STABLE_QUALIFICATION_KO.md), [시나리오 표시 교정](SCENARIO_DISPLAY_APPLY_KO.md)

과거 NG 단계의 미병합·trial 원복 문장은 당시 기록이다. 현재 운영 절차는 이 문서와 실제 checkout의 명령을 따른다. `.github/workflows/crawl_only.yml`은 기존 one-shot metadata 확인용이며, 지속 자동 번역이나 예약 실행은 추가하지 않는다.
