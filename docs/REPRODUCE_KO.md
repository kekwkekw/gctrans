# 실행한 표적 검사 재현

후보를 추출한 루트에서 실행한다. 빌드 단계와 bin 실행은 별개다. 테스트 숫자는 목표/고정 gate가 아니며 NG09/NG10 native 증거는 이전 동일 DLL의 결과로 참조한다. 로컬 검토 보고는 공개 Git/export와 분리한다. 다음은 새 scratch의 폐기 fixture만 변경한다.

```powershell
python -I -S -X utf8 tools/make_trial_fixture.py --implementation . --scratch <new-scratch>/fixture --discarded-fixture-approved
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:NuGetAudit=false
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <new-scratch>/isolated-store --package <fixture-restored-next_package> --based-on NONE
```

현재 DLL 그대로 actual CPU helper를 검사한다. 실제 game/interop를 로드해 native 호출하는 시험이 아니다.

```powershell
dotnet build tests/Ng09HeadingTests/Ng09HeadingTests.csproj -c Release -p:CandidateDllPath=<absolute-candidate/GCMod.dll> -p:NuGetAudit=false
dotnet tests/Ng09HeadingTests/bin/Release/net9.0/Ng09HeadingTests.dll <new-scratch>/heading.json
dotnet build tests/Ng08ConsumersTests/Ng08ConsumersTests.csproj -c Release -p:CandidateAssembly=<absolute-candidate/GCMod.dll> -p:NuGetAudit=false
dotnet tests/Ng08ConsumersTests/bin/Release/net9.0/Ng08ConsumersTests.dll
dotnet build tests/Ng08ChoiceTests/Ng08ChoiceTests.csproj -c Release -p:CandidateDllPath=<absolute-candidate/GCMod.dll> -p:NuGetAudit=false
dotnet tests/Ng08ChoiceTests/bin/Release/net9.0/Ng08ChoiceTests.dll <new-scratch>/choice.json
dotnet build tests/AnnotationDeliveryTests/AnnotationDeliveryTests.csproj -c Release -p:CandidateDllPath=<absolute-candidate/GCMod.dll> -p:NuGetAudit=false
dotnet tests/AnnotationDeliveryTests/bin/Release/net9.0/AnnotationDeliveryTests.dll
dotnet build tests/Ng08UiRequestsTests/Ng08UiRequestsTests.csproj -c Release -p:NuGetAudit=false
dotnet tests/Ng08UiRequestsTests/bin/Release/net9.0/Ng08UiRequestsTests.dll <new-scratch>/fixture/FIXTURE.json <absolute-tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll> <new-scratch>/request-store <new-scratch>/request.json
```

DLL 소스를 수정한 경우에만 다음 빌드를 수행한다. 명시 game dependency root는 읽기 전용 reference이며 빌드가 설치본을 교체하지 않는다. 별도 Medium bundle은 빌드 입력이 아니며 native 실행 시 외부 입력이다.

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=<existing-read-only-game-root> -p:NuGetAudit=false -m:1 -p:BuildInParallel=false
# 산출물 source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll은 candidate 사본으로 검토한 뒤 사용한다.
```

반환 ZIP 자체의 독립 재현은 다음 명령이다. PYTHONPATH/site/editable/private helper를 사용하지 않으며 SDK-only UpdateCli restore source를 비워 외부 네트워크 없이 빌드한다. native와 실제 cfg/store에는 접근하지 않는다.

```powershell
python -I -S -X utf8 tools/replay_extracted.py --archive <this-ZIP> --scratch <new-outside-check> --dotnet <installed-dotnet> --discarded-fixture-approved
```

preflight·staging·실제 draft HOLD→폐기 fixture APPROVE→교정→next_package→복구·isolated prepare-local을 실행한다. 최초 import와 두 apply의 read/hash0, OS copy 비용을 출력한다. 외부 증거 참조의 과거 절대 경로는 당시 실행 기록이며 일상 명령의 필수 입력이 아니다.

NG11 scanner의 직접 script/module/운영 CLI는 현재 checkout만으로 내부 import를 해결한다. 다음 no-locator 실행은 외부 CWD·site 없는 조건에서도 typed UNKNOWN/PC_SOURCE_LOCATOR_REQUIRED/exit2와 지정 scan 및 intake 보고서를 만들어야 한다. WebGL metadata-only에는 실제 httpx dependency가 필요하며 saved inventory/fake transport 제품 검사는 실제 네트워크 취득 PASS가 아니다.

```powershell
python -I -S -X utf8 scripts/crawl_only.py --platform PC --state-root <new-scratch/pc-state> --raw-root <new-scratch/pc-raw> --output <new-scratch/pc-scan.json>
python -X utf8 -m scripts.crawl_only --platform PC --state-root <new-scratch/module-state> --raw-root <new-scratch/module-raw> --output <new-scratch/module-scan.json>
python -I -S -X utf8 tools/update/cli.py scan -- --platform PC --state-root <new-scratch/cli-state> --raw-root <new-scratch/cli-raw> --output <new-scratch/cli-scan.json>
```

지속 source 명령과 scan→diff→actionable intake→Novel prepare→Codex 초벌→PRO 검수→apply는 OPERATOR_KO.md를 참조한다. fresh state의 baseline-only/비교 이력 없음은 persistent state의 token 변경 plan과 구분한다. GitHub workflow invocation/static no-write/fresh 대 persistent fixture는 이번 실제 실행 보고에 기록하며 Actions dispatch/실제 CDN/raw 취득은 실행하지 않는다.

NG10 수정 경계는 다음 대표 검사로 재현한다. helper fault는 dummy file과 GC query mock만 사용하며 실제 게임을 실행하지 않는다. Windows junction fixture를 만들 수 있는 scratch 파일 권한이 필요하다.

```powershell
python -I -S -X utf8 tests/ng10_recovery_tests.py --root . --scratch <new-scratch/recovery>
dotnet build tests/Ng03SupplyTests/Ng03SupplyTests.csproj -c Release -p:NuGetAudit=false
dotnet tests/Ng03SupplyTests/bin/Release/net9.0/Ng03SupplyTests.dll <new-scratch/supply.json> translations/ko <fixture-changed-next_package>
```

실제 export 전체를 mock 서버에서 최초 공급하고 다음 UI 변경은 검증 cache에서 unchanged Novel을 재사용한다. mock transport ref는 합성 고정 SHA이며 실제 public 서버 검사가 아니다. fixture 구성 read/copy와 제품 updater counter를 구분한다.
