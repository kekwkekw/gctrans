# 종합 복원 Shop 표적 CPU 검사

실제 후보 GCMod.dll의 역할 정책과 PE/IL 연결을 검사한다. primitive graph 입력은 명시한 mock이며 native class/ABI/화면 증거가 아니다.

```powershell
dotnet build tests/ComprehensiveShopTests/ComprehensiveShopTests.csproj -c Release --no-restore
dotnet tests/ComprehensiveShopTests/bin/Release/net9.0/ComprehensiveShopTests.dll
```

현재 자연 `SetupCurrentTabView` → typed page/view/model → 실제 shared Header의 직접 제목 field → 기존 ownership writer 경로를 확인한다. geometry는 그 정확한 제목의 **현재 직접 부모**가 typed Header._frameUI 안에 있을 때만 사용하는 별도 범위다. 원래 JP와 KO의 전체 SDF quad, 실제 material/channel/mask와 현재 identity를 유지하며 metric ink로 줄여 승인하지 않는다. 모르는 shader/keyword와 타 UI 역할은 차단한다.

부모 container의 시각 geometry 이상은 원래 자신이 쓴 text를 조건부 복구할 identity와 구분한다. 준비 시에만 양수·유한 rect를 요구하고, 그 변화 때문에 같은 살아 있는 target을 외부 재바인딩으로 오인하지 않는다.

실제 상점 표시·재열기·복구 PASS는 별도의 해당 DLL native 증거가 필요하다.
