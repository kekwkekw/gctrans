# 외부 입력과 역할

운영 후보에는 번역 JSON·GCMod 후보·safe source/tools/tests만 들어 있다. 이 문서의 FOUND/path는 metadata와 custody 역할이며 ABI/glyph/hook 성공을 뜻하지 않는다.

| 입력 | 현재 명시 경로/역할 | 포함 |
|---|---|---|
| DMM launcher | `<existing-DMM-launcher.ps1>`, 인자 없음, 현재 interactive girlscreation_r | private 본문 제외 |
| Game root | `<existing-game-root>` | 게임 DLL 제외 |
| BepInEx IL2CPP/core | `<game>/BepInEx/core`, read-only compile/runtime references | 제외 |
| interop/map | `<game>/BepInEx/interop/Assembly-CSharp.dll`, `MethodAddressToToken.db` 및 기존 interop assemblies | 제외 |
| Utility | `<game>/BepInEx/plugins/GCMod/Utility.dll`, AssetBundleLoader dependency | 제외, 기존 폴더 유지 |
| 승인 Medium | `<approved-external-custody>/RHR_Medium_GFSAFE_ADH.bundle` | 제외 |
| 보존 Medium custody | `<preserved-external-custody>/RHR_Medium_GFSAFE_ADH.bundle` | 제외 |
| Python/.NET | Python3 및 .NET SDK9; candidate는 net6.0, UpdateCli는 net9.0 | 실행 환경 외부 |
| source HTTP/decoder | known WebGL metadata는 httpx, 실제 Unity bundle 취득/추출은 UnityPy 및 lane별 별도 입력 | UI-only에 불필요 |

staging/GCMod.cfg.template의 `GCMod/fonts/tsukuardgothic-std-medium`은 기존 code default와의 일치를 기록한다. 승인 Medium이 그 경로에 있다는 보증이 아니다. 실제 trial에서는 읽기 확인한 위 승인 custody를 Translation.Font.AssetBundlePath에 명시하고 원복한다. 상대 font 경로는 BepInEx/plugins 기준이며 PackageRoot 기준이 아니다. 새 폰트 생성/다운로드/검색은 하지 않는다.

현재 source build는 `dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=<existing-game-reference-root> -p:NuGetAudit=false`다. 출력은 source build 폴더이며 설치본으로 복사하는 target이 없다. build와 반환 candidate/DLL provenance를 구분한다. informational version명만으로 재빌드하지 않는다.

scripts/crypto.py는 credential-free 경계만 제공한다. 실제 live WebGL signing/decryption에는 운영자의 별도 private helper가 필요할 수 있으나 UI 교정·mock source·CPU·metadata-only scan은 이를 요구하지 않는다. 실제 HTTP/Unity decoder 누락은 typed optional-dependency reason으로 구분하고 checkout 내부 module 누락을 외부 설치 요구로 숨기지 않는다. helper/키/private raw를 전달물에 넣지 않는다. 현재 최신 PC inventory/UI·Novel locator/Names authority 부족은 source coverage 축에 따로 기록하며 완료된 제품 경로의 blocker가 아니다.

실제 NG10 관측 경로와 launcher 실행 기록은 Git 밖 로컬 인계의 좁은 metadata에만 기록한다. 공개 source/config에 개인 custody를 고정하지 않는다. trial 활성 table 최소 합집합은 mUnitWords,mSubunitWords,mTextConfig,mNovelKeyWords,mNovels이며 기본 개인 설정은 원복한다. 전체62 table native PASS가 아니다. staging 기본 opt-in/CheckUpdate/UiRequestsEnabled는 OFF, locale ko, XUnity OFF다. `Nextgen.PackageRoot`는 후보의 `translations/ko` 폴더이며 같은 위치의 release-manifest와 ui/manifest를 가리킨다.
