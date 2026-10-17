# 실제 loader/store/coordinator 전환 CPU

`dotnet build tests/RestorationTransitionTests/RestorationTransitionTests.csproj -c Release` 후 `dotnet tests/RestorationTransitionTests/bin/Release/net9.0/RestorationTransitionTests.dll new <NEW_DISPOSABLE_SCRATCH>`를 사용한다. actual candidate library를 참조하며 작은 생성 가능 package와 명시 managed scheduler/features mock을 쓴다. native Initialize/game PASS가 아니다. 구 r2 DLL 비교는 그 보존 library를 CandidateDllPath 또는 별도 실행 폴더의 GCMod.dll로 명시하고 `old` mode/새 scratch를 사용한다. 게임 설치본/운영 store는 읽거나 쓰지 않는다. new-data expected reject와 old current/LKG 보존을 신 client 성공과 분리한다.
