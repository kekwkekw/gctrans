# NG09 실제 후보 DLL의 표적 CPU 검사

이 실행 파일은 `candidate/GCMod.dll`의 실제 공개 helper, main-thread/lifetime gate와 ownership 구현을 직접 호출한다. 합성 record/identity와 CPU text/glyph mock은 관계·실패 경계를 검사하기 위한 입력이다. 이 결과는 실제 native class, ABI, factory callback, glyph renderer, hook 또는 게임 표시의 증거가 아니다.

후보 source를 먼저 빌드하고 해당 `GCMod.dll`을 `candidate/GCMod.dll`에 둔 뒤 후보 루트에서 실행한다. 기존 DLL의 검사를 새 빌드의 직접 검사로 사용하지 않도록 결과에 실제 로드한 DLL SHA256을 기록한다.

```powershell
dotnet build tests/Ng09HeadingTests/Ng09HeadingTests.csproj -c Release -p:NuGetAudit=false
dotnet tests/Ng09HeadingTests/bin/Release/net9.0/Ng09HeadingTests.dll heading-cpu-results.json
```

다른 명시 후보를 검사할 때에는 build에 `-p:CandidateDllPath=<GCMod.dll 절대 경로>`를 전달한다. 이 경로는 검사 입력이며 runtime pin이 아니다. 빌드 단계와 실행 단계는 별개다.

검사 범위:

- factory-owned heading과 strict click 인과증명 독립, async/no-click/동일 ID 경쟁/private 충돌의 strict UNPROVEN 유지
- 실제 record 내용·owner/path/localizer/target 고유성·현재 generation 관계, delayed owner/UI 재사용과 외부 writer 차단
- 두 헤더의 Medium Hangul 역할과 공백을 포함한 전체 primary/fallback 경로 분리, donor 무효화·off-thread read 0
- ownership 외부 writer 양보, restore read/write 실패 lease 보존, epoch 전환 보류·안전 재시도

standalone Dictionary의 selected row/list/model/current subscene 검사는 실제 native adapter의 기존 경계로 유지한다. 그 native getter를 합성 `true`로 대체하여 이 CPU 결과에 포함하지 않는다. 해당 경계는 source review와 별도 실제 Dictionary 표본으로 구분하여 보고한다.

공통 Choice/font/cleanup guard 및 live-request 회귀는 기존 `Ng08ChoiceTests`, `Ng08UiRequestsTests`의 실제 후보 DLL 검사 결과를 사용한다. UI 작업을 위해 과거 WB 전체를 재실행하지 않는다.
