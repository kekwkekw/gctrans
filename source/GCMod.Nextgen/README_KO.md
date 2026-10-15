# NG02 공통 runtime

이 source는 독립 .NET 6/.NET 9 라이브러리로 CPU 검사할 수 있고, `GCMod.Candidate`에서는 단일 `GCMod.dll` 안에 함께 컴파일된다. 실제 Unity/native 공급자와 plugin lifecycle은 `GCMod.Candidate/Native` 및 `NativeRuntimeBridge.cs`에 있다. 이 후보는 미설치 산출물이다. 게임/native hook 실행 결과는 **NOT_RUN**이다.

`PackageLoader`는 manifest, UI, layout, Master, Names, 기존 Novel manifest를 같은 captured bytes로 검증하고 parse한다. Novel inventory는 등록·허용 목록으로 바꾸지 않는다. `VerifiedPackageFiles`는 Novel 최초 요청의 captured bytes를 검증하고 재사용하며 startup이나 bind에서 Novel 전체를 읽지 않는다. `TranslationPackageSnapshot.LayoutPolicies`는 Fit3 executor가 사용하는 typed 정책을 보존한다. 문법·참조 충돌은 package reject이고, 정상 문법의 미지원 optional selector는 해당 binding만 실행 불가다. code-owned `SelectorContract.Shapes`는 scope/slot/path/value 조합을 검사하며 path segment 순서를 보존한다.

`RuntimeCoordinator.PrepareAsync`는 선택 상태를 바꾸지 않는다. main-thread `CommitUi`는 최신 request/lifetime, authority 동일성, Choice 안전 경계, native display 정리, 자기 text lease 복구 순서로 검사한다. 복구가 실패하면 old snapshot과 lease를 유지한다. 성공한 경계에서 host가 선택한 snapshot 하나를 runtime 선택으로 공개한다. Master/Names/Novel 변경은 재시작 요구이며, UI-only reload는 실제 `TranslationSession`이나 성공한 Names/Novel task를 재생성하지 않는다. 기존 `TranslationPackageService.TryActivateAsync`는 writer가 없는 초기 호환 경로만 허용하고 live 선택을 거부한다.

`LocalImmutablePackageStore`는 authoring tree를 `packages/<content-id>/`에 stage한 뒤 완료 표식을 기록한다. 현재·이전 pointer는 작은 원자 교체 record다. startup은 검증한 current, 검증한 previous, 원문 순서로 복구한다. incomplete directory는 선택하지 않는다. 손상된 current를 previous로 복사하지 않는다. 명시적인 최초 stage는 payload를 검사하고, UI-only 후속 stage는 이전 immutable package의 동일 descriptor 파일을 hardlink로 재사용할 수 있다. 외부 hash는 bytes 무결성 정보이며 publisher 인증을 주장하지 않는다.

`UiOwnershipService`의 물리 key는 native object + text property다. 복구 결과는 `Restored`, `ExternalChanged`, `DestroyedOrRebound`, `RetryableReadFailure`, `RetryableWriteFailure`를 분리한다. 단순 inactivity는 파괴 증거가 아니며 lease를 유지한다. 실제 adapter는 `DestroyedOrRebound` 표식을 입증한 경우에만 제공한다. `UiFloatOwnershipService`는 fontSize와 같은 별도 property slot에 동일한 원칙을 적용한다. 자연 cell rebind가 입증된 경우 `RestoreAtRebind`가 같은 physical style의 자기 값만 복구한다.

native adapter는 `IUiTargetAccessGate`를 적용하고 읽기 이전에 실제 main-thread/lifetime를 확인한다. `UiModuleHost`도 evidence를 target 및 glyph 읽기보다 먼저 확인한다. 관리되는 text/fontSize 복구 실패는 제한된 다음 안전 callback에서 재시도할 수 있다. unload는 Master callback busy preflight와 실제 `TryUninstall`을 모두 확인하며, 실패하면 translation/font 서비스를 dispose하지 않는다.

파생 legacy source는 `GCMod.Candidate/Legacy`에 있다. sealed donor cache 상속 대신 좁은 `ITranslationCache`를 도입했으며 기존 parser, locator, legacy semantic hash, cancellation, 성공 task 재사용을 보존한다. local store가 Master/Names/Novel bytes를 공급한다. UI용 Master view는 실제 manager의 준비된 catalog, Translation ON, enabled table, selected origin/authority를 함께 확인한다. moving CDN, update checker, addon 초기화, operator/AutoLease는 candidate 운용 경로에 없다. 고정 donor 원본은 수정하지 않았다.

CPU 검사에는 `tests/RuntimeTests`, 기존 회귀에는 `tests/HostTests`, **실제 후보 GCMod.dll** 검사에는 `tests/CandidateCpuTests`를 사용한다. 마지막 harness는 동일 DLL로 전체 JSON KO 교정과 지원 binding 추가의 안전 reload, 실제 legacy session/cache 유지, shared validator corpus를 검사한다. `game_interop_assembly_loaded=false`를 기록하고 Unity/native method를 실행하지 않는다. CPU PASS를 게임 내 PASS로 해석하지 않는다.

빌드 예: `dotnet build source/GCMod.Nextgen/GCMod.Nextgen.csproj -c Release -f net6.0 -m:1 -p:UseSharedCompilation=false`. candidate build는 읽기 전용 `GameDependencyRoot` 참조만 사용하며 설치본으로 복사하는 target을 포함하지 않는다.
