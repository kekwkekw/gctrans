## 현재 기준 — PR26 병합 후 안내 (2026-10-17)

PR #26은 병합 완료다. 이 안내를 작성할 때 실제 main은 `fd84842efec881deebe7399c709e0a678937b57a`이며 tree는 `fee1131032c024efb50b6815421adf5738520609`다. 새로운 작업 시작 시에는 원격 main의 후속 변경을 다시 확인한다. 이 식별값은 runtime pin이 아니다.

검증된 Stable 구성은 DLL `96114569fbe38f113d49ad5e9554801ad1e55c6f062335e5085de428316dce2a` + 완전 content `b1bedaac6c4d6efdab1680ab4f84e32f4912e7048319f51222de404d972cc56a` + `staging/accepted-master-profile.json`의 명시 수락 table 목록 + 실제 selected current/지속 PackageRoot다. 데이터 updater가 DLL·font·개인 cfg를 자동 설치하는 것은 아니다.

아래 NG 단계·로컬 후보·82a8…/63c5…·public 미실행 설명은 당시 검토 이력이다. 그것을 현재 설치나 현재 main의 상태로 해석하지 않는다. 최신 복원 지원 범위는 379 binding/377 resource와 217/69/5/88의 구분을 유지하며 전체 native 무제한 PASS가 아니다. Museum PRO05/06는 아직 별도 미통합 기능이다. PRO04 외곽 괄호 교정은 현재 Novel preimage와 기존 proposal을 연결하는 별도 표시 변경이며, 원래 의미 수락 ledger를 소급 변경하지 않는다.

기존 개인 Stable과 XUnity.AutoTranslator OFF를 보존한다. 2026-10-17 후속 사용자 지시는 구 파이프라인 퇴역과 기존 시나리오 표시 교정의 repository 수정·main 병합을 명시 허용했다. 이는 Museum·신규 번역의 의미 수락, 자동 설치, Release/tag 또는 XUnity 활성화의 포괄 허가가 아니다. 이번 퇴역 범위와 보존 경로는 [RETIRED_PIPELINES_KO.md](RETIRED_PIPELINES_KO.md)를 따른다.

---

최신 적용·복구 규칙은 [STABLE_QUALIFICATION_KO.md](STABLE_QUALIFICATION_KO.md)와 [조건부 개인 Stable 정책](PROJECT_STABLE_QUALIFICATION_POLICY_KO.md)을 따른다. 종합 검수 성공 후 개인 PC에서는 새 완전 구성을 유지하며, 실패 시에만 원래 구성을 복구한다. 아래의 무조건 trial 원복/미설치 문구는 이전 geometry 통합 회차 기록이고 현재 성공 경로를 되돌리는 지시가 아니다. prepare/live/source의 기존 경계는 그대로다.

# main 통합 후보 운용

현재 종합 복원 후보의 적용 순서는 [RESTORATION_MIGRATION_KO.md](RESTORATION_MIGRATION_KO.md)를 우선한다. 구 r2/main loader는 새 producer JSON을 거부하며 LKG를 유지한다. 신 DLL·완전 JSON·명시62-table·actual selected content를 함께 준비하고, 시험 후에는 원래 cfg/store/선택을 먼저 복원한 뒤 구 DLL을 마지막에 복원한다. 아래 prepare/live/source 명령과 단일 coordinator 경계는 그대로다. 이 로컬 후보는 아직 merged main이나 영구 설치가 아니다.

이번 closeout의 실제 callback 역할·popup/미션 cell 보완·일관 DLL/data/profile 전환과 원복은 [RESTORATION_CLOSEOUT_KO.md](RESTORATION_CLOSEOUT_KO.md)를 함께 따른다. 실행 결과는 전달된 전체 scope 표와 DLL별 증거 수준으로 구분한다.

현재 로컬 종합 복원 Dev 후보의 적용 profile·producer 차이와 인계는 [COMPREHENSIVE_RESTORE_KO.md](COMPREHENSIVE_RESTORE_KO.md)를 참조한다. 수락 Master 전체 profile과 과거 5-table smoke profile은 다르다. 이 로컬 후보가 public main이나 설치 Stable에 반영됐다는 뜻은 아니다.

운영 후보는 source/, scripts/, tools/, tests/, contracts/, donor_recovery/reproduction/Common.props, candidate/GCMod.dll, translations/ko, staging/로 구성된다. PR #25는 main에 병합됐으며 수락 번역값과 등록 상태를 유지한다. 네트워크 resolve 보완의 DLL 식별과 표적 명령은 [NETWORK_PREPARE_FIX_KO.md](NETWORK_PREPARE_FIX_KO.md)를 참조한다. 병합 후 검증 기준 commit은 `82a8b0f7ef2c2760fe7139707fda69030eb4571c`이다.

명령은 추출한 후보 루트에서 실행한다. Python 3와 .NET SDK 9가 외부 실행 입력이다. UI-only 교정/검사는 Unity raw decoder·Medium·게임 DLL·private source helper를 요구하지 않는다. DLL 재빌드와 실제 native에는 EXTERNAL_DEPENDENCIES_KO.md의 별도 입력이 필요하다.

```powershell
python -X utf8 tools/update/cli.py --help
python -X utf8 tools/update/cli.py preflight --candidate-root . --report <outside-scratch>/preflight.json
python -X utf8 tools/verify_staged_release.py translations/ko --report <outside-scratch>/staging.json
```

preflight는 artifact/entrypoint/layout, 명시 dependency/font custody, native 미실행을 분리한다. FOUND는 ABI/glyph/hook PASS가 아니다. 마지막 whole-package staging은 한 번 수행한다. 매 UI 교정에 과거 Novel 전체를 다시 읽는 일상 경로가 아니다.

기존 literal/template_label KO 교정은 draft를 먼저 만든다.

```powershell
python -X utf8 tools/update/cli.py plan-ui-edit --package translations/ko --resource-id <existing-resource-id> --new-ko <review-proposal> --draft <outside-scratch>/draft.json --report <outside-scratch>/plan.json
# 해당 교정의 내용을 검토한 뒤 draft의 decision을 명시 APPROVE/HOLD로 저장한다.
python -X utf8 tools/update/cli.py apply-review --package translations/ko --review <outside-scratch>/review.json --state-root <outside-scratch>/authoring --report <outside-scratch>/apply.json
```

다음 교정의 --package에는 apply.json의 next_package를 사용한다. 같은 owned custody의 예상 base/CAS와 해당 JP/kind/role/binding/old-KO를 검사한다. 기존 대상의 KO 교정에는 새 PC UI locator를 요구하지 않는다. source·binding·활성 범위 변경은 기존 observed-source 계약을 유지한다. master_ref KO 사본, HOLD/KEEP_SOURCE/Known Residual 또는 disabled binding 자동 활성화는 금지다. PARTIAL_HOLDS/exit2이면 request_outcomes의 APPLIED와 HELD를 나눠 읽고 이미 성공한 교정을 반복하지 않는다.

최초 외부 import는 Novel을 읽고 hash한다. 이후 next_package 연속 UI 교정에서 변경 없는 Novel의 app read/hash는 0이며, OS copy/link/stat 비용까지 0이라는 뜻은 아니다. 임의 mutable export를 trusted custody로 승격하지 않는다.

UpdateCli는 source build와 bin 실행을 구분한다.

```powershell
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:NuGetAudit=false
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <new-isolated-test-store> --package <next_package> --based-on NONE
```

prepare-local은 완전 후보와 pending_restart만 저장한다. current 선택·native Initialize·live 전달을 실행하지 않는다. 최초 테스트는 실제 BepInEx store와 다른 scratch를 사용한다.

승인된 trial에서 기존 driver의 UiRequestsEnabled를 true로 opt-in한 뒤 UI-only live 요청은 다음과 같다.

```powershell
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll request-ui --store <approved-trial-store> --package <next_package> --based-on <current-content-id>
```

UI_REQUEST_PUBLISHED는 신호 저장 결과다. 실제 driver 수신 → PREPARED → main-thread COMMITTED → 자연 rebind 화면을 확인해야 live 표시 결과다. 같은 DLL에서 폐기 fixture 교정과 원래 KO 복구를 한 쌍으로 확인한다. signal은 request ID/candidate content ID/expected-current/ui_reload 계약이며 임의 path/URL/코드/native 명령을 받지 않는다.

UI-only commit은 기존 Master/Names/Novel session/cache와 pending_restart를 유지한다. Choice 자연 종료/자기 restore 실패 보류, 단일 coordinator/current/previous/LKG 경계를 유지한다. Master/Names/Novel authority·locale/origin·font/code/DLL 변경은 RESTART_REQUIRED다. data updater가 DLL/font를 설치하지 않는다. Fit3 typed layout은 기존 property/schema/bounds 범위이며 현재 35→34→33→32→31을 변경하지 않았다.

기본 staging 설정은 Enabled=false, UiRequestsEnabled=false, CheckUpdate=false, ko, XUnity OFF다. 실제 trial 권한은 현재 사용자가 채택한 2026-10-13 프로젝트 정책을 따른다. preflight나 파일 자체가 새로운 권한을 만들지는 않는다. LAUNCH_PROCEDURE_KO.md에 정확한 임시 write-set과 원복 도구가 있다.

최종 반환본의 별도 추출 재현은 동봉된 작은 도구로 수행할 수 있다. game root 밖의 아직 없는 scratch만 지정한다. 이 명령은 새 설치나 native 시험을 시작하지 않는다.

```powershell
python -I -S -X utf8 tools/replay_extracted.py --archive <current-operational.zip> --scratch <new-outside-return-check> --dotnet <installed-dotnet-executable> --discarded-fixture-approved
```

ZIP 경로를 검증해 별도 추출하고, 추출본 entrypoint/help/import/preflight/whole-package staging을 실행한다. 폐기 fixture에만 기존 close KO 교정/복구의 명시 decision과 next_package 경로를 재현한다. 이후 SDK의 NuGet source를 비운 UpdateCli build → 같은 후보 DLL을 참조한 bin 실행 → isolated prepare-local을 수행한다. current/previous 생성과 native Initialize가 없고 pending_restart만 저장되는지 확인한다. 기록은 scratch/REPLAY_RESULTS.json, STAGING.json, PREPARE_LOCAL.json, fixture/*-apply.json이며 이 CPU 재현은 native PASS가 아니다. 최초 import/cold staging 비용과 이어지는 unchanged Novel app read/hash0, OS copy 비용을 구분한다.

public main은 새 release-manifest와 번역 데이터를 제공한다. 재현 가능한 격리 공급 검사는 아래처럼 고정 commit을 사용한다. 기존 대형 commit metadata 오류는 수정됐고, prepare는 완전 package/pending만 준비하며 native Initialize나 현재 선택을 실행하지 않는다.

```powershell
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare --store <new-isolated-store> --repository kekwkekw/gctrans --channel fd84842efec881deebe7399c709e0a678937b57a
```

기본 Nextgen Enabled·CheckUpdate·UiRequestsEnabled OFF, XUnity OFF를 유지한다. 실제 운영 store는 BepInEx/config/GCMod.Nextgen.local이며 PackageRoot만 바꾸어 격리됐다고 해석하지 않는다. 영구 설치·기존 plugin/config/store 전환, 외부 Medium/Utility/interop 입력, 정상 종료·백업·원복 및 이후 업데이트 opt-in은 별도 설치 결정이다. main 통합이나 격리 pending 준비가 설치·전체 native 수락을 대신하지 않는다. 공개 전 계약 검토 이력은 PUBLICATION_KO.md, 현재 native/residual은 SUPPORT_SCOPE_KO.md를 참조한다.

source 증분 운용은 같은 private state/raw root를 계속 사용하는 로컬 명령이다. output만 run별로 나눈다. 아래 metadata-only 예시는 이미 보유한 known inventory를 사용하며 raw 취득·최신 PC server 관측·의미 수락을 만들지 않는다. direct script와 운영 CLI는 같은 checkout의 소스를 사용한다.

```powershell
python -X utf8 scripts/crawl_only.py --platform WebGL --lane Novel --inventory <saved-known-inventory-v1.json> --state-root <persistent-private-state> --raw-root <persistent-private-raw> --output <outside-run1>/scan.json
python -X utf8 tools/update/cli.py scan -- --platform WebGL --lane Novel --inventory <saved-known-inventory-v2.json> --state-root <persistent-private-state> --raw-root <persistent-private-raw> --output <outside-run2>/scan.json
python -X utf8 tools/update/cli.py diff --previous <outside-run1>/scan.json --current <outside-run2>/scan.json --report <outside-run2>/diff.json
```

scan envelope와 bare snapshot은 diff/apply가 같은 규칙으로 읽는다. PC locator가 없으면 `--platform PC` scan은 `PC_SOURCE_LOCATOR_REQUIRED`/관측 UNKNOWN/exit2와 지정 보고서를 만든다. 이것은 내부 import 실패나 raw 취득 성공이 아니다. metadata-only 계획은 raw 미취득이고, 부분 취득 실패는 다른 성공을 보존한 채 실패 항목을 다음 scan에서 재시도한다. baseline-only는 이전 이력 없음이며 이전 대비 no-change가 아니다. state가 사라지면 새로운 baseline이다. 첫 baseline만으로 기존 Novel 전체를 재취득하거나 검수 작업으로 만들지 않는다. 실제 제한된 raw 취득은 established locator/endpoint와 외부 입력을 갖춘 뒤 `--acquire`를 명시하는 별도 단계다.

scan 옆 `novel-intake.json`은 모든 eligible reference의 catalog다. `actionable-intake.json`은 새/변경/실패/미처리 work만 담는다. raw 취득 성공과 scan 성공은 semantic 검수 완료가 아니다. pending/failed는 유지되고, collision/불완전 source는 해당 scope만 held다. 완료 work는 accepted return과 성공 apply가 대응할 때만 반복 공급을 멈춘다.

```powershell
python -X utf8 tools/update/cli.py bind-intake-wb --current-source <run/scan.json> --bound-scene <existing-WB-bound-scene.json> --scene-sha256 <exact-scene-sha256> --source-id <observed-source-id> --asset-key <observed-asset-key> --raw-root <persistent-private-raw> --report <run/wb-bind.json>
# 기존 WB13~M4 intake/validator/review/accepted return을 실행한다. parser/locator를 새로 만들지 않는다.
python -X utf8 tools/update/cli.py apply-review --package <authoring-next_package> --review <explicit-accepted-review.json> --current-source <run/scan.json> --state-root <owned-authoring-custody> --report <run/apply.json>
python -X utf8 tools/update/cli.py intake-failed --state-root <persistent-private-state> --current-source <run/scan.json> --work-id <actionable-work-id> --reason <TYPED_FAILURE_REASON> --report <run/intake-failed.json>
python -X utf8 tools/update/cli.py resume-intake-completion --state-root <owned-authoring-custody> --report <run/completion.json>
```

WB binding에는 실제 source-qualified/physical locator와 검증된 raw identity가 필요하다. accepted Novel return의 reviewed-change-set에 기존 intake link가 있어야 해당 work가 완료된다. 일반 UI 교정에서 관계없는 WB 전수 replay를 하지 않는다. 지원된 operation만 적용하고 나머지는 request_outcomes로 APPLIED/HELD/REJECTED를 보고한다.

GitHub의 `crawl_only.yml`은 fresh state의 one-shot metadata snapshot이다. 지속 CI 증분은 구현하지 않았다. artifact 7일 보존은 durable source baseline을 대신하지 않는다. workflow 요약은 실제 scan.json의 status/coverage/plan을 사용하며 raw/PC 동등성/semantic 수락 PASS를 주장하지 않는다. main의 `update_translation.yml` 정의는 retired manual/read-only 안내뿐이다. 원격 workflow의 enabled/disabled 상태 및 실제 dispatch는 별도이며 이번 작업에서 변경·실행하지 않는다.

```powershell
python -I -S -X utf8 tools/update/snapshot_summary.py --scan <run/scan.json> --summary <run/public-summary.md> --report <run/public-summary.json> --one-shot
```

`--one-shot`은 CI의 이전 run 이력 없음 표시다. local persistent state 결과의 요약에는 이 옵션을 붙이지 않는다. CI artifact는 유한한 enum/숫자 projection인 public-summary.json/md만 보관하고 실제 scan/raw/state/서명 URL·private source를 올리지 않는다.
