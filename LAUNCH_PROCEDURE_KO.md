# 상시 정책의 표적 native 실행과 원복

2026-10-13 사용자가 명시 채택한 프로젝트 정책의 안전 표적 시험은 기본 승인이다. 정상 DMM 실행, 외부 백업 후 단일 후보/cfg/store 임시 적용, 무소비 대표 화면과 same-DLL live, 좁은 수정/재시험, 정상 종료/정확한 원복을 진행한다. AP/BP/재화 소비·보상수령·구매·가챠·강화·판매·출격·계정/인증/세이브/진행도/잠금 조작·unknown ABI/RVA·비가역적 원본 변경·공개 작업은 포함되지 않는다. 저장 영향이 불확실한 Choice/Battle 표본만 HELD/NOT_OBSERVED로 남긴다.

운영자가 읽기 확인한 기존 정상 DMM 런처를 인자 없이 실행한다. 정확한 개인 launcher/custody 실행 기록은 공개 Git 밖 로컬 인계에 둔다. 이 문서의 parameter를 실제 확인한 경로로 치환한다. 기존 GC가 실행 중이면 강제 종료/추가 실행을 하지 않는다.

```powershell
if (Get-Process -Name GC -ErrorAction SilentlyContinue) { throw 'EXISTING_GAME_RUNNING_NO_LAUNCH' }
& '<existing-confirmed-DMM-launcher.ps1>'
```

현재 interactive 목록에서 girlscreation_r label과 선택 표시를 확인한 뒤 Enter를 누른다. 과거 index/좌표/window ID/pointer 또는 GC.exe 직접 실행을 사용하지 않는다. 실제 창 title은 `ガールズクリエイションR -少女藝術綺譚-`이며 한 동작마다 새 화면을 확인한다. private 런처 본문/인증 자료는 동봉하지 않는다.

stage 성공 뒤 반드시 `python tools/verify_native_ready.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root>`에서 STAGE_COMPLETE_SINGLE_CANDIDATE/launch_allowed=true를 확인한 뒤 위 런처를 실행한다. phase 미완료·원본+후보 중복·cfg/파일 불일치는 launch를 막는다. 기존 게임 프로세스가 있으면 stage/restore를 하지 않는다.

동봉 tools/prepare_native.py, stage_native.py, replace_trial_candidate.py, capture_native.py, restore_native.py는 기존 NG08 실사용 도구를 좁게 회수해 현재 통합 source/data 입력으로 정리한 코드다. 자동 launcher/installer/daemon이 아니며 게임을 실행하지 않는다. 작업 audit·개인 cfg 제안·backup은 반드시 game/plugin 검색 경로 밖 scratch에 둔다.

```powershell
python -X utf8 tools/prepare_native.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root> --medium <existing-approved-Medium-path>
python -X utf8 tools/stage_native.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root> --execute
```

prepare는 현재 cfg/plugin/font/candidate hash와 store/candidate 존재를 새로 읽고 scratch에만 계획/제안본을 만든다. 이 작은 도구는 원래 store와 후보 폴더가 없는 관측된 trial 상태를 지원한다. 다음 실행에서 이들이 이미 있거나 기존 Nextgen 설정/구8addon/중복 본체가 발견되면 덮어쓰지 않고 exact preservation plan을 요구한다. 그 입력은 새 동의 요구가 아니라 기존 bytes/역할을 보존할 구체적인 write-set 확인이다.

stage는 game process0과 계획 후 파일 집합/각 hash를 다시 대조하고 외부 private_backup에 원래 GCMod.cfg와 GCMod.dll을 복사·검증한다. 실제 변경은 다음 네 경로뿐이다.

- `<game-root>/BepInEx/plugins/GCMod/GCMod.dll`: 기존 본체만 외부 백업 후 제거. GCMod 폴더 전체를 옮기지 않아 Utility/font를 보존한다.
- `<game-root>/BepInEx/plugins/GCMod.Nextgen.Candidate/`: 후보 DLL 하나와 동일 translations만 새로 생성.
- `<game-root>/BepInEx/config/GCMod.cfg`: 기존 무관 설정을 유지한 trial 제안본. 필요한 mTextConfig/mNovelKeyWords/mNovels만 기존 EnabledTables에 추가, ko/기존 승인 Medium/Enabled=true/CheckUpdate=false/UiRequestsEnabled=true.
- `<game-root>/BepInEx/config/GCMod.Nextgen.local`: 매 trial 시작 시 존재·비존재를 다시 검사하는 실제 runtime store. 이 좁은 helper는 원래 없던 store만 지원한다. PackageRoot를 바꿨다는 이유로 이 store가 격리된 것으로 보지 않는다.

좁은 수정 DLL을 다시 시험할 때 정상 종료/GC0 뒤 `python tools/replace_trial_candidate.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root> --execute`를 사용한다. 설치한 이전 hash/CAS를 검사하고 자신의 trial DLL만 교체한다. 새로운 최종 DLL의 직접 실기와 이전 revision evidence를 구분한다. NG10/NG11은 DLL을 바꾸지 않아 이 replacement 경계는 실행하지 않았다.

작은 symbolic GCMod evidence는 `python tools/capture_native.py <UPPERCASE_SESSION_NAME> --work-root <outside-trial-scratch> --game-root <existing-game-root>`로 수집한다. full game/auth log를 결과물에 넣지 않는다. screenshot은 현재 화면을 관측한 좁은 crop만 반환한다.

시험 후 실제 own game의 정상 종료를 확인한다. live 시험을 했다면 fixture의 changed/restored next_package 경로를 `--fixture`로 전달한다.

```powershell
python -X utf8 tools/restore_native.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root> --fixture <discarded-trial-fixture>/FIXTURE.json
python -X utf8 tools/restore_native.py --work-root <outside-trial-scratch> --implementation-root <this-extracted-candidate> --game-root <existing-game-root> --fixture <discarded-trial-fixture>/FIXTURE.json --execute
```

첫 명령은 READY 검증만 한다. 원본 backup SHA, 현재 무관 cfg 값/타 plugin 집합·bytes, 후보 파일 집합/SHA, 모든 trial package의 알려진 generation/SHA와 current/previous/pending/request, 절대 경로/reparse를 검사한다. `--execute`는 원래 cfg/body bytes를 복원하고 검증된 trial plugin/store만 제거한다. 외부 동시 변경이 있으면 덮거나 삭제하지 않는다. 원래 없던 store/후보의 비존재와 원래 cfg/body hash/GC0를 마지막에 다시 확인한다.

도구의 guard가 중단되면 해당 reason과 현재 changed set을 먼저 비교한다. 수동 원복도 동일한 네 경로, 외부 backup bytes, 저장된 expected current hash에만 한정하고 타 plugin·원본 game/core/interop·계정·세이브·게임 자연회복/자동 로그/cache는 원복 대상이 아니다. 개인 cfg/backup/helper 내용은 반환 ZIP에 넣지 않는다.

NG10 helper는 CURRENT_INPUTS/외부 backup과 현재 작은 INSTALL_RECEIPT의 exact write-set을 사용한다. backup 검증 후 최초 원본 변경 전에 receipt를 저장하고, candidate는 plugins 밖 trial-staging에서 완성 후 이동한다. 파일 temp+replace는 개별 atomic이며 여러 경로가 모두 atomic하다는 뜻은 아니다. stage/restore IO 중단 시 파일과 backup을 보존하고 같은 restore를 재실행한다. 원본 cfg가 아직 ORIGINAL이거나 body/cfg가 이미 복원된 상태를 정상 중간상태로 인정한다. 완료 뒤에는 ALREADY_RESTORED로 끝난다. UNKNOWN_CHANGED·외부 파일·reparse는 강제 정리하지 않는다. 기존 store/candidate가 있으면 지원 범위 밖 보존 계획이 필요하다.

NG11은 scanner import·workflow·공개 문서만 바꾼다. 기존 동일 DLL의 NG09 표적 표시/live와 NG10 startup/Core/정확한 원복 증거를 참조하고, 새 native 시험이나 실제 cfg/store/plugin write는 수행하지 않는다. 이 문서의 절차와 상시 정책은 공개 발행·영구 설치 권한을 만들지 않는다.
