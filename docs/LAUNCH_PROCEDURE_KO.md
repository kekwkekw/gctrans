# LocalParity 적용과 실행

현재 runtime은 [GCMod.Candidate/README_KO.md](../source/GCMod.Candidate/README_KO.md)의 local 기반 구현이다. 기존 Nextgen 전용 prepare/stage/restore/verify 도구는 이 패키지의 설치 도구가 아니다.

게임을 정상 종료한 상태에서 기존 GCMod DLL과 cfg를 게임 폴더 밖에 백업하고, 중복 GCMod 본체가 로드되지 않게 한다. 현재 패키지의 배치는 다음과 같다.

```text
BepInEx/plugins/GCMod/GCMod.dll
BepInEx/plugins/GCMod/Utility.dll
BepInEx/plugins/GCMod/fonts/RHR_Medium_GFSAFE_ADH.bundle
BepInEx/plugins/GCMod/translations/ko/{manifest.json,master.json,names.json,ui.json,novels/}
BepInEx/config/GCMod.cfg
```

`Language = ko`, `CDN = offline`, `EnabledTables = *`를 사용한다. 폰트 경로는 `GCMod/fonts/RHR_Medium_GFSAFE_ADH.bundle`이다. 기존 Nextgen local store는 이 runtime에서 사용하지 않는다. 게임 원본/core/interop 및 로그인 자료는 교체하지 않는다.

PowerShell 7에서 사용자가 제공한 기존 `dmm-ex-launcher-runtime-safe.ps1`을 인자 없이 실행한다. 개인 절대 경로는 로컬 인계 보고서에 기록하며 런처 본문이나 인증 자료는 동봉하지 않는다.

```powershell
& '<사용자가 제공한 경로>/dmm-ex-launcher-runtime-safe.ps1'
```

메뉴의 ↑/↓ 방향키로 `girlscreation_r`을 선택한 뒤 Enter를 누른다. 게임 창이 열리면 주의문과 타이틀 화면을 클릭한다. 기존 DMM 로그인 정보를 사용하는 이 런처를 실행 경로로 유지한다.

최소 smoke는 산소비노 툴팁, 대표 Master/static UI, 시나리오 메뉴/진입 제목, Novel 본문/화자, 선택지 표시/분기와 공방 번호로 제한한다. 전수 UI/binding/table 검증을 요구하지 않는다. `EnabledTables = *`는 실제 cfg에서 확인한다.

복구가 필요하면 게임 종료 후 해당 설치 때 백업한 DLL/cfg/번역 파일의 원본을 복원하고, 그 설치가 추가한 파일만 제거한다. 이번 로컬 작업의 정확한 변경 목록과 원본 백업은 게임 밖 `work/native-backup`에 보존했다. 과거 Nextgen 원복 도구를 현재 설치에 실행하지 않는다.
