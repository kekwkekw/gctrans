현재 운영 명령·경로·반환본 빌드는 [NG06_OPERATOR_KO.md](../../docs/NG06_OPERATOR_KO.md)를 따릅니다. 아래 NG02 설명은 최초 연결의 역사 기록이며 현재 PackageRoot는 GCMod.Nextgen.Candidate/translations/ko입니다. 반환 GCMod.dll은 NG05 빌드 bytes를 재사용합니다.

# GCMod 단일 DLL NG02 후보

Plugin.Load → NativeRuntimeBridge → RuntimeCoordinator → 기존 TranslationManager/Session 및 NativeHookHub → 실제 current owner/target adapter를 하나의 GCMod.dll로 컴파일한다. Core·Consumers·Chrome·Residuals·Captions·Story·Battle·Fit3를 내부 모듈로 연결한다. 이전 validation-only bootstrap은 제거했다. 고정 donor와 수락 원형은 비교 자료로 보존한다.

Nextgen.Enabled는 기본 false다. opt-in에서는 local package를 준비하고 main-thread commit 뒤 실제 callback과 기존 Master/Names/Novel·폰트 lifecycle을 초기화한다. Nextgen.PackageRoot는 plugins 기준 GCMod/ng02/translations/ko이며 명시 로컬 경로도 지원한다. Nextgen.ReloadPackage는 UI-only 준비 후 Choice 종료·자기 표시 복원·단일 선택 commit 경계를 사용한다. 옛 import receipt, archive, 부모 DLL SHA 및 과거 절대 작업경로는 runtime 입력이 아니다.

UI MasterRef는 실제 TranslationManager의 활성 catalog·origin·enabled table을 공유한다. UI-only 선택은 실제 session·Names cache·Novel task를 갱신하지 않는다. Master/Names/Novel·폰트·코드 변경은 기본 RESTART_REQUIRED다. local immutable package와 current/previous 기록은 재시작 LKG를 제공하며 Novel은 최초 read/변경/명시 staging 경계에서 검증한다.

callback마다 managed main-thread/lifetime gate를 먼저 확인하고 현재 API/ABI·native identity·parameter·owner·direct target을 좁게 검사한다. 가용성이 확인되지 않은 기능은 no-op이다. shared hook 구독은 같은 원본 메서드의 물리 hook을 공유한다. global TMP setter·substring hook·operator·AutoLease·HTTP update/crawler를 초기화하지 않는다. Translation OFF 및 unavailable MasterRef는 자기 표시의 조건부 회수 경계를 사용한다.

빌드는 GameDependencyRoot 또는 GC_GAME_DEPENDENCY_ROOT의 기존 BepInEx/interop/Utility 참조를 읽기만 한다. reference는 Private=false이고 설치/복사 target을 import하지 않는다. 최종 ZIP에는 새 후보 GCMod.dll 하나만 포함한다. 게임 원본 DLL·Utility.dll·폰트 bundle은 포함하지 않는다.

재현은 tests/run_ng02_offline.ps1 및 tests/NG02_RUNTIME_CPU_REPRO_KO.md를 따른다. source wiring·compile·host/mock PASS와 native hook 설치·게임 표시 검증은 별개다. 게임/설치/공개 작업은 수행하지 않았으며 native hook 설치와 게임 검증은 NOT_RUN이다. 실제 현재 환경의 metadata/renderer 검증은 후속 승인된 표적 검수에서 수행한다.
