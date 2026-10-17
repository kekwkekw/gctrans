# Battle F04 표적 CPU 검사

후보 `GCMod.dll`을 먼저 빌드한 뒤 실행한다. 이 프로젝트는 제품 policy를 다시 복사해 컴파일하지 않고 실제 후보 DLL을 참조한다.

```powershell
dotnet build tests/ComprehensiveBattleTests/ComprehensiveBattleTests.csproj -c Release
dotnet tests/ComprehensiveBattleTests/bin/Release/net9.0/ComprehensiveBattleTests.dll translations/ko
```

`CandidateDllPath` MSBuild 속성으로 다른 미설치 후보 경로를 명시할 수 있다. 결과에 실제 참조 DLL SHA256이 나온다.

검사 범위는 실제 `BattleFontPolicy`, `BattleCaptionPolicy`, 현재 활성 Battle caption/command-cost 데이터 전량과 실제 후보 IL의 두 진입 경로다. 한글만 가진 mock donor, 별도 primary/fallback coverage, off-thread read counter, owner/donor 재사용, 지원/미지원 markup, 완성 native parameter의 suffix 보존을 검사한다.

mock callback은 실제 native owner·폰트·ABI 증거가 아니다. hook 설치, 실제 glyph/material/geometry, 최종 화면 정착은 이 CPU 결과로 PASS가 되지 않는다. `TEXT_READBACK` 진단도 문자열 readback일 뿐 mesh/layout 정착 수락이 아니다.
