# 외부 입력과 역할

현행 절차는 [LOCAL_PARITY_PIPELINE_KO.md](LOCAL_PARITY_PIPELINE_KO.md)를 따른다. 공개 경로에 개인 PC 주소·로그인 정보·private helper를 고정하지 않는다.

| 입력 | 용도 | 공개 package |
|---|---|---|
| 기존 DMM 런처 | 사용자가 제공한 interactive girlscreation_r 실행 | 제외 |
| 기존 game/BepInEx/core/interop | DLL 빌드와 게임 실행 참조 | 제외 |
| 기존 Utility.dll | local AssetBundleLoader/알림 의존성 | 기본 update 제외 |
| RHR_Medium_GFSAFE_ADH.bundle | 검증된 한국어 TMP font/fallback | 기본 update 제외 |
| Python 3.12 이상 | crawler/export/review/package 도구 | 실행 환경 |
| .NET 9 SDK | CPU lookup 검사; net6.0 candidate 빌드 | 실행 환경 |
| httpx 0.27.2, UnityPy 1.10.18 | metadata HTTP와 Unity bundle 추출 | 개발 의존성 |
| 기존 private helper | live WebGL 취득 인증 및 Master decode | 항상 제외 |

`python tools/local_release.py build --game <existing-game-root>`가 `candidate/GCMod.dll`을 만든다. 직접 빌드는 `dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDir=<existing-game-root>`와 같다. build 자체는 설치본을 수정하지 않는다.

폰트 기본 경로는 `GCMod/fonts/RHR_Medium_GFSAFE_ADH.bundle`이며 BepInEx/plugins 기준이다. 기본 공개 업데이트에는 font/Utility/게임/interop를 넣지 않는다. `pack --font ... --utility ...`는 사용자 개인 설치 묶음이며 공개 release로 올리지 않는다.

metadata-only scan과 CPU 검사는 private helper 없이 실행할 수 있다. 이미 보관된 raw의 UI prefab 재해석도 인증키를 요구하지 않는다. HTTP 인증과 Master 복호화에 필요한 helper/키를 Git, CI artifact, 설치 ZIP에 포함하지 않는다.

LocalParity 설정은 `ko`, `offline`, Master `*`다. 과거 5/7-table smoke 목록과 Nextgen.PackageRoot/UiRequestsEnabled 설정은 현재 runtime 구성에 사용하지 않는다.
