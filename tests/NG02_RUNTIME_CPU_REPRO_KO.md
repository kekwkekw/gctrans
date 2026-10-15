# NG02 런타임 CPU 검증 재현

작업 디렉터리는 NG02 패키지 루트다. PowerShell에서 아래 명령을 **순서대로** 실행한다. 공유 `GCMod.Nextgen` 출력 디렉터리를 사용하는 프로젝트 빌드를 동시에 실행하지 않는다.

사용 환경은 .NET SDK 9.0.316, 캐시된 .NET 6 참조, 이미 설치된 읽기 전용 BepInEx/interop 참조다. 후보 프로젝트는 `GameDependencyRoot`를 참조 경로로만 사용한다. 원본 donor의 `GameDir` 배포·복사 타깃을 가져오지 않는다. 새 의존성 다운로드, 게임 실행, 게임 디렉터리 설치는 이 검증에 포함하지 않는다.

## 실제 후보 DLL 빌드와 동일 DLL 검증

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release --nologo -m:1 --no-restore -p:UseSharedCompilation=false -p:GameDependencyRoot=<existing-read-only-game-root>

dotnet build tests/CandidateCpuTests/CandidateCpuTests.csproj -c Release --nologo -m:1 --configfile NuGet.Config -p:UseSharedCompilation=false

dotnet tests/CandidateCpuTests/bin/Release/net9.0/CandidateCpuTests.dll tests/CANDIDATE_CPU_TEST_RESULTS.json tests/shared-validator-cases.json translations/ko tests/fixtures/full-ko-correction tests/fixtures/full-binding-addition

Get-FileHash source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll -Algorithm SHA256
Get-Content tests/CANDIDATE_CPU_TEST_RESULTS.json
```

`CandidateCpuTests.csproj`는 빌드된 실제 `GCMod.dll`만 참조한다. `GCMod.Nextgen` 프로젝트나 파생 Legacy 소스를 중복 컴파일하지 않는다. 실행 파일은 후보 DLL의 공통 런타임, 패키지 로더, 로컬 공급자, 실제 `TranslationSession`·Master 파서를 실행한다. native/Unity 타입의 메서드는 호출하지 않는다. 후보 DLL을 최종 변경한 뒤에는 이 harness를 다시 빌드하여 최신 DLL을 복사해야 한다.

기본 45개 검증에 실제 후보 전용 2개 검증이 추가되어 총 47개다. 추가 검증은 실제 코드 소유 selector shape의 General 172개 커버리지, 순서가 유지된 경로, 임의 slot 거절과 같은 DLL에서의 전체 패키지 KO 수정·지원 binding 추가·안전한 UI 재선택을 확인한다. UI 변경 사이에 DLL을 다시 빌드하지 않았다는 SHA 동일성도 확인한다. 실제 저장된 미등록 Novel을 읽고 성공 task와 Names task 및 session 객체가 UI 재선택 뒤 유지되는지 확인한다.

최종 보고서에서 다음 값을 확인한다.

- `status: PASS`, `count: 47`, `failures: 0`
- `execution_mode: actual_candidate_cpu`
- `assembly_sha256`: 위 원본 후보 DLL SHA256과 같음
- `game_interop_assembly_loaded: false`
- `game_runtime: NOT_RUN`, `native_game_hooks: NOT_RUN`

Native provider의 실제 게임 hook/ABI/렌더러 실행 결과를 CPU PASS로 해석하지 않는다. 후보 전용 natural-bind 검증은 명시적 mock target/evidence를 사용하며, 실제 JSON과 실제 코드 소유 adapter registry를 사용한다.

## 공통 런타임 회귀와 Host 검증

```powershell
dotnet build tests/RuntimeTests/RuntimeTests.csproj -c Release --nologo -m:1 --configfile NuGet.Config -p:UseSharedCompilation=false

dotnet tests/RuntimeTests/bin/Release/net9.0/RuntimeTests.dll tests/NG02_RUNTIME_TEST_RESULTS.json tests/shared-validator-cases.json

dotnet build tests/HostTests/HostTests.csproj -c Release --nologo -m:1 --configfile NuGet.Config -p:UseSharedCompilation=false

dotnet tests/HostTests/bin/Release/net9.0/HostTests.dll tests/NG02_HOST_TEST_RESULTS.json translations/ko tests/fixtures/full-ko-correction
```

Runtime 검증은 45개, Host 검증은 100개다. Runtime harness는 공통 Nextgen 라이브러리 및 실제 파생 Legacy의 portable 소스를 참조한다. `nextgen_managed_cpu`라는 실행 모드를 보고하며 실제 후보 DLL 검증과 구분된다. 공유 validator corpus 20개는 Python과 C#에 동일한 독립 JSON 문서 및 기대 구조/capability 결과를 공급한다.

중요한 Runtime 검증 범위는 준비 단계의 미공개 상태, Choice busy/화면 복원/텍스트 복원 성공 뒤 단일 commit, 읽기·쓰기 실패 시 lease·기존 snapshot 유지, native/glyph 읽기 전 main-thread/lifetime 거절, unload 경쟁 및 worker 취소, UI 변경 시 session/cache 유지, actual Master catalog와 enabled table, current/previous LKG 손상·중단 복구, 시작 시 Novel 본문 0회 읽기, lazy read 해시 검증 및 검증된 captured bytes 재사용, typed Fit3 정책, float 속성 소유 복원·재바인딩이다.

최종 보강 검증은 IL 명령 operand 안의 가짜 opcode를 제외하는 정확한 MethodInfo field 해석, reconcile의 offthread target/glyph 0회 접근, 실제 Master parser의 테이블 비활성화에 따른 해당 binding만 복원, Translation OFF 전체 owned 복원, 복원·hook 해제 실패 시 기존 선택/lease/lifetime 보존과 성공 재시도, cold UI staging의 검증된 immutable payload 재사용, 가짜 complete marker·mutable authoring 재사용 금지다.

Immutable payload 재사용은 정상 검증된 store identity·정확한 content 경로·complete marker·같은 descriptor가 모두 맞을 때만 허용한다. Hardlink가 거절되면 그 immutable 파일의 `File.Copy`를 사용한다. Windows sandbox에서 이 OS byte copy I/O가 실제로 발생했으며, 반복되는 application-level Novel 해시/파싱은 0회였다. `cold_UI_staging_novel_reads`는 `IPackageFileSource.ReadAsync` 호출 수다. OS 수준 복사 I/O까지 0이라는 뜻으로 해석하지 않는다. Novel의 첫 실제 소비는 항상 lazy SHA 검증을 수행한다.

전체 JSON 변경 fixture는 `FULL_CANDIDATE_JSON_FIXTURES.json`에 기록돼 있다. 원본은 `translations/ko`, KO 수정은 `fixtures/full-ko-correction`, 지원 binding 추가는 `fixtures/full-binding-addition`이다. 중복 writer를 만들지 않도록 추가 fixture의 기존 동일 shape binding은 disabled다.

Fixture를 새로 생성할 때는 NG02용 정상 manifest builder를 사용하는 아래 명령을 실행한다. 역사적 import receipt가 필요 없다. generator는 존재하는 출력 폴더를 덮어쓰지 않으므로 매번 새 로컬 이름을 만든다. 아래 재현 실행은 기존 최종 보고서와 fixture를 보존한다.

```powershell
$taskFixtures = Join-Path 'tests' ('fixtures-repro-' + [guid]::NewGuid().ToString('N'))
python tests/prepare_ng02_fixtures.py translations/ko $taskFixtures --report (Join-Path $taskFixtures 'FULL_CANDIDATE_JSON_FIXTURES.json')

dotnet tests/CandidateCpuTests/bin/Release/net9.0/CandidateCpuTests.dll tests/CANDIDATE_CPU_REPRO_RESULTS.json tests/shared-validator-cases.json translations/ko (Join-Path $taskFixtures 'full-ko-correction') (Join-Path $taskFixtures 'full-binding-addition')
```

`tests/data_generate_validator_corpus.py`는 공유 corpus 생성 원본이며 `tests/data_tools_tests.py`는 동일 corpus의 Python 구조/capability 판정을 검증한다. 기존 NG01 `prepare_full_candidate_fixture.py`의 receipt 기반 흐름은 NG02 재현에 사용하지 않는다.

`scratch-runtime-*` 디렉터리는 각 검증이 생성한 합성 로컬 저장소다. 보고서에 기록된 PASS/FAIL 판정의 입력 fixture를 재현하기 위한 일시 파일이며 배포 대상이 아니다. `HOST_TEST_RESULTS.json` 등 NG01 기존 보고서는 이 문서의 NG02 최종 검증 결과로 사용하지 않는다.
