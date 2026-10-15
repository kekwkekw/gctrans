# NG08 Choice 제품 검사 경계

이 suite는 새 후보 `GCMod.dll`에 실제 포함되는 `ChoiceGroupTransaction`, `StoryChoicePolicy`의 필수 lifecycle 판정, `ChoiceDisplayDiagnostics`, `ChoiceOwnedCleanup`, `ChoiceCleanupBarrier`를 직접 호출한다. Unity/native 객체의 렌더·폰트·raycast 성공을 합성 bool로 대체하는 시험이 아니다. 실제 renderer 경로는 `StoryNativeProvider`이며 이 CPU suite의 native 호출은 0이다. 후속 허가된 실기 상태는 별도 최종 native 보고서로 구분한다.

확인하여 수정한 source 결함은 다음과 같다.

- 기존 `ChoiceGroupTransaction.catch`가 처음 실패 phase/reason을 잃었고 cleanup 예외가 첫 실패를 덮거나 rollback을 다시 호출할 수 있었다. 새 상세 결과는 준비/precommit/show/postcommit 실패와 cleanup 실패를 별도로 유지한다. 기존 bool API도 유지한다.
- 기존 cleanup은 첫 setter 실패에서 이후 선택지의 복구를 중단할 수 있었다. 새 helper는 모든 자기 시각/자기 mask/자기 Destroy를 시도하고 실패 시 해당 transaction 소유권을 보존하여 재시도한다.
- 기존 cleanup은 Unity의 지연 Destroy가 끝나기 전에 `display=null`만으로 추적을 잃었다. 다음 준비는 이전 자기 mask/visual이 현재 안전 frame에서 실제 소멸했음을 확인한 뒤 허용한다. 같은 frame과 살아 있는 객체는 barrier를 통과하지 못한다.
- 자기 TMP root 아래 Graphic을 일반적으로 끄던 경로를 자신의 text와 `TMP_SubMeshUI.textComponent`가 그 text에 정확히 대응하는 generated descendant로 한정했다. Selectable/EventTrigger나 다른 Graphic이 생기면 group을 거부한다.

후속 허가된 c1ef84e8… DLL 실기에서 JP 유지와 CH 진단 0, AddSelectMessage 관측, AddSelectMessage/Release 설치 및 Open의 shared-body 차단이 확인됐다. 그 시험의 확인된 선행 차단 원인은 `CURRENT_REQUIRED_OPEN_SHARED_BLOCKED`다. 기존 `choiceAvailable=add&&open&&close` 때문에 PrepareChoice가 영구 return했다. 보존 UI14 source는 AddSelectMessage/Release를 설치하고 Open은 metadata-only로 검사(`installTarget=false`)한다. 이에 같은 renderer의 검증된 Add 이후 전체 group/source 증명과 기존 유한 Tick active-root 경계, Release만 필수 lifecycle로 정정했다. shared Open은 계속 hub에서 거부하며 임의 hook/ABI/receiver 우회는 없다. 이 선행 차단 수정만으로 뒤의 실제 font/fit/precommit/표시 성공이 증명되지는 않는다.

후속 R2 실기에서는 전체 source group4와 Prepare 진입 뒤 O0/GLYPH/DONORGLYPHMISSING 및 유한7회 cleanup completion이 확인됐다. 기존 `FontService.Supports(full text, Medium)`는 공백/ASCII까지 모두 Medium에 요구했지만 보존 UI14 renderer는 Medium의 Hangul syllable coverage와 전체 GameFont primary/fallback 경로를 별도로 검증했다. Choice 경계만 `ChoiceFontRolePolicy.ProbeDonor`로 역할을 분리했다. Medium에 없는 첫 scalar와 그 종류·필수 여부를 제한된 `Uxxxx` 코드로 기록하고 Hangul 누락은 계속 거부한다. 전체 문자열의 기존 `fonts.Glyphs(primary, full)`와 실제 rendered Hangul `fontAsset==Medium` proof는 그대로 유지한다. 실제 누락 scalar가 공백/ASCII 역할 문제인지 필요한 Hangul의 실제 누락인지는 R3 표적 native evidence에서 확인해야 한다. 폰트 bytes/registration/generation·GameFont·original text/control은 바꾸지 않았다.

이 결함의 source/CPU 재현은 NG07의 7회 rollback의 실제 원인이라고 단정하지 않는다. 현재 renderer의 원인 확인을 위해 source/plan/baseline/raycast/primary/donor/glyph/fit/precommit/show/postcommit/retained 단계, 닫힌 reason, group·binding hash key, option ordinal, fit 수치와 첫 변경 control 역할을 제한적으로 기록한다. 예외의 임의 message, JP/KO 원문, 포인터, 전체 tree는 로그로 출력하지 않는다. 기존 source/native tree/control/font proof와 매 stable frame의 보수적인 raycast는 유지했다. ForceMeshUpdate는 새 자기 overlay에만 수행한다.

실행 예시(후보 빌드 후):

```powershell
dotnet run --project implementation/tests/Ng08ChoiceTests/Ng08ChoiceTests.csproj -c Release -p:NuGetAudit=false -- implementation/tests/Ng08ChoiceTests/CHOICE_CPU_RESULT.json
```

동일 suite를 빌드 산출물에 직접 연결할 때는 `-p:CandidateDllPath=<absolute GCMod.dll>`을 지정할 수 있다. report의 SHA는 실제 로드한 후보 DLL bytes의 값이다. 보호된 source/Letter/Ruby/button/branch는 native 코드에서 쓰지 않는다. mock은 이 product transaction/cleanup helper의 실패·재시도와 원자 show를 검사하며, 실제 control/raycast의 현재 Unity 값을 증명하지 않는다.
