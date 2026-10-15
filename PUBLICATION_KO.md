# 로컬 공개 검토 후보와 공급 경로

NG11은 `NG11_PUBLICATION_REVIEW_CANDIDATE / PUBLIC_WRITE_NOT_AUTHORIZED`다. 실제 local branch/commit/tree, observed remote base, 모든 변경 path와 공개/제외 목록은 동봉된 로컬 검토용 `PUBLICATION_PLAN.json`을 기준으로 읽는다. 이 문서는 공개 승인이나 영구 설치 권한을 만들지 않는다.

`tools/export_git_rc.py --repo <local-repo> --output <new-scratch/export> --ref <local-commit>`는 해당 commit의 blob을 Git archive로 읽고 선택 파일의 bytes를 검증한다. working-tree CRLF/untracked bytes 또는 과거 overlay를 혼합하지 않는다. safe source/tools/tests/contracts/donor build·선택 translations·후보 DLL·staging·현재 문서를 한 후보로 만든다. `.github`·README·.gitignore도 전체 공개 diff 검토에 포함하며 운영 artifact allowlist만으로 Git push 안전성을 확정하지 않는다. 보존한 base의 WB/Knowledge/Names/비대상 legacy 자료를 운영 ZIP에 중복 복사하거나 전체 ZIP으로 덮어쓰지 않는다.

공개 가능 source와 로컬 검토 자료는 분리한다. raw/cache/state/audit/native report/개인 launcher 기록/cfg backup/font/게임·Utility·interop DLL/credential/private helper는 공개 tracking/export 대상이 아니다. 원래 custody는 삭제하지 않는다. 필수 safe source/build 파일과 `donor_recovery/LICENSE` upstream MIT notice는 남긴다. base의 비대상 `GCMod-cs파일백업`·Knowledge 역사 prompt는 원래 Git에 보존하며 새 운영 artifact에 넣지 않는다. 현행 공개 문서는 parameter 예시를 사용한다. 실제 개인 launcher/custody 경로는 Git 밖 로컬 인계에만 둔다.

`SAFE_GIT_DIFF.patch`는 민감 과거 helper 삭제 hunk와 불필요한 개인 경로 hunk 등을 생략한 검토용이다. 단독 적용/배포 입력이 아니다. 생략 path를 별도로 선언하고 안전한 완성 파일·실제 local commit/tree·모든 변경 path를 함께 검토한다. 기존 credential-free `scripts/crypto.py` 경계를 유지하며 민감 helper 본문을 patch 삭제 내용으로 다시 내보내지 않는다.

main merge 후 기존 소비자가 요청하던 `translations/ko/master.json`·`manifest.json`의 내용이 로컬 후보의 수락 Master와 재생성된 의미 hash로 바뀐다. 경로가 같아도 공급 bytes와 Master 의미 범위가 바뀌므로 기존 legacy 소비자에게도 영향을 준다. Master는 기존 table/field/exact JP→KO dictionary 구조와 string/array/delimited parser 역할을 보존한다. Names/Novel bytes·Novel 등록 계약을 유지하고 새 UI metadata를 legacy Novel manifest에 자동 등록하지 않는다. 미등록 Novel 43개는 관측값이며 계속 on-demand 보관 자격을 유지한다. 수락 UI 활성/HOLD/Known Residual과 번역 의미값을 이번에 변경하지 않는다. Master 62/101/31,566은 보유 데이터 관측값이며 native table 활성 또는 runtime 개수 gate가 아니다.

공급 진입점은 `translations/ko/release-manifest.json`이며 UI manifest의 size/SHA와 legacy semantic/byte hash 계약을 그대로 사용한다. 현재 읽은 public main에는 이 새 release-manifest가 없으므로 원격 최신판이 로컬 후보를 공급한다고 설명하지 않는다. 기본 Nextgen Enabled·CheckUpdate·UiRequestsEnabled는 OFF, locale ko, XUnity OFF이며 Medium/Utility/interop은 외부 입력이다.

기존 updater는 repository/channel을 `/repos/{repository}/commits/{channel}`에서 한 번 SHA로 resolve한 뒤 `{repository}/{resolved-commit}/translations/ko/`의 manifest·JSON을 받는다. transport ref와 논리적 translation authority origin은 별개다. 자신의 commit SHA를 commit 내부 manifest에 넣지 않는다. size/SHA·JSON·경로 계약을 검증한 complete immutable package를 기존 coordinator에 전달한다. UI-only는 legacy session/cache를 유지한 safe commit이고, Master/Names/Novel/font/code 변경은 current를 유지한 pending_restart 및 다음 정상 초기화/LKG 경계다. current/previous/pending_restart를 담당하는 coordinator는 하나다. prepare-local은 pending 준비이고 request-ui의 driver 수신/main-thread commit/자연 rebind가 live 전달이다. data updater는 DLL/font/설치본을 바꾸지 않는다.

`.github/workflows/update_translation.yml`의 새 로컬 정의는 retired 안내용 manual/read-only다. schedule/push/PR trigger, secret 사용, 실제 download/translate/merge/commit/push job이 없다. `scripts/run.py`·`translate_script.py`·`merge.py`와 기존 WB/Knowledge/Names 의미 코드는 비대상으로 보존한다. 실제 원격 workflow의 active/disabled 상태는 읽기 전용 관측 보고를 따르며 로컬 수정만으로 원격 state가 바뀌었다고 하지 않는다. enable/disable/dispatch는 수행하지 않는다.

`.github/workflows/crawl_only.yml`은 fresh state의 one-shot metadata snapshot이다. 기존 known endpoint/helper/forced ID/수량 제한을 사용하고 요약은 실제 scan.json의 status/coverage/plan에서 만든다. 첫 baseline/비교 이력 없음은 이전 대비 변경 없음과 다르다. CI의 7일 artifact가 지속 baseline 정본은 아니며 지속 CI 증분은 구현하지 않았다. 평소 증분은 같은 로컬 `--state-root`/`--raw-root`를 유지하는 OPERATOR_KO.md의 scan→diff→actionable intake→기존 WB/review 명령이다. raw 취득/PC 동등성/semantic 수락을 metadata 관측으로 승인하지 않는다. secret/signed URL/private raw는 public artifact에 넣지 않는다.

| 후속 행위 | 현재 권한과 영향 |
|---|---|
| 로컬 branch/commit/export | 이번 작업 범위. 정확한 tree를 만든다 |
| 원격 candidate branch push/PR | 공개 write이며 별도 명시 허가 필요 |
| main merge | 기존 master/manifest 소비 bytes를 바꾸며 별도 명시 허가 필요 |
| tag/release asset/Pages/CDN | 발행 경로별 별도 허가 필요. 이번에 생성/업로드하지 않음 |
| Actions dispatch/원격 workflow 설정 | 별도 허가 필요. 이번에는 읽기 전용 관측만 |
| 영구 로컬 설치 | 임시 native 시험 정책과 별개이며 이번에 수행하지 않음 |

NG11의 Python/workflow/docs 검사와 기존 동일 DLL native 증거를 구분한다. strict async UNPROVEN, Choice 자연 Release HELD, Battle NOT_OBSERVED, source coverage PARTIAL은 각자의 잔여 상태로 남긴다. 이 상태를 없애려고 새 framework·전수 source 수집·소비 시험을 시작하지 않는다. 후속 공개 권한은 실제 exact diff/제외 목록/공급 경로를 검토한 뒤 해당 행위에만 적용한다.
