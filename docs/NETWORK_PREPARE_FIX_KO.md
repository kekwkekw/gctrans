# 네트워크 prepare 표적 보완

공개 baseline `c0a0192925899ebe4f67577adc02aa65ffad81f3`의 큰 REST commit 응답은 1,200,912바이트여서 기존 resolve 상한 1MiB를 넘었다. 필요한 SHA만 Git Data API에서 확인하도록 FixedRefDataUpdater를 보완했다. 신규 updater/인증/설치 기능은 없다.

고정 40자리 SHA는 `/git/commits/{sha}` 응답의 SHA와 tree object ID를 검증한다. branch는 `/git/ref/heads/{name}`의 정확한 ref·commit type·SHA를 검사한 뒤 그 SHA로 모든 manifest와 payload URL을 고정한다. slash를 포함한 branch 이름도 지원하며 경로 이탈 segment는 거부한다.

일반 이름은 branch가 실제 404일 때만 tag ref를 확인한다. 권한 거부·손상 JSON·다른 ref/object는 tag로 우회하지 않는다. 명시 `refs/heads/...`, `refs/tags/...`, lightweight tag와 annotated tag를 지원한다. tag object는 자기 SHA를 대조하고 최대 4개까지 peel하며 cycle/다른 object type은 거부한다. 응답의 임의 URL을 따라가지 않는다. 기존 운용에서 사용하는 full SHA/branch가 기본이며 약식 SHA를 새 계약으로 만들지 않는다.

API 형태는 GitHub 공식 [Git references](https://docs.github.com/en/rest/git/refs), [Git commits](https://docs.github.com/en/rest/git/commits), [Git tags](https://docs.github.com/en/rest/git/tags)를 따른다. 기존 resolve 응답 상한 1MiB, 요청당 10초·최대 3회 시도, bounded backoff, redirect/credential 차단, payload size/SHA/JSON 검증과 current/LKG 보존은 유지한다. 변경 파일 patch를 포함하는 REST `/commits/{channel}`로 fallback하지 않는다.

새 배포 DLL SHA256은 `63c548758d06fb3194927db02458d0885dc1d8e3d5ac1cbfe7a0be311e4c5e43`이다. 실제 GCMod.Candidate 소스 빌드 산출물을 candidate/GCMod.dll에 반영하고 UpdateCli는 이 새 DLL을 참조해 빌드한다. 이전 baseline DLL은 `69c16910f35a1f4252353bbd94b0cc61cfc75a03a3cfe406f4f63c0ac00b573a`이다. 이 식별은 runtime pin 또는 JSON manifest 수정 조건이 아니다. 번역 JSON/등록 상태/폰트/native hook 코드와 version string은 바꾸지 않았다.

읽기 전용 기존 reference와 SDK로 빌드한다. 설치본으로 복사하는 target은 없다.

```powershell
dotnet build source/GCMod.Candidate/GCMod.Candidate.csproj -c Release -p:GameDependencyRoot=<read-only-game-root> -p:NuGetAudit=false -p:RestoreConfigFile=<checkout>/NuGet.Config -m:1 -p:BuildInParallel=false
Copy-Item source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll candidate/GCMod.dll
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release -p:NuGetAudit=false -p:RestoreConfigFile=<checkout>/NuGet.Config
dotnet build tests/Ng03SupplyTests/Ng03SupplyTests.csproj -c Release -p:NuGetAudit=false -p:RestoreConfigFile=<checkout>/NuGet.Config
dotnet tests/Ng03SupplyTests/bin/Release/net9.0/Ng03SupplyTests.dll <scratch>/supply-cpu.json
```

mock/CPU는 작은 기존 package fixture에서 fixed SHA·branch/tag·손상 metadata·크기 경계·payload 실패·current/previous/pending 및 LKG를 검사한다. native proof나 실제 HTTP PASS를 fixture로 만들지 않는다.

실제 네트워크 검사는 다음 한 번의 cold 공급을 사용한다. 운영 BepInEx store/config는 사용하지 않는다. 성공 시 완전 package와 pending만 저장되고 current/previous는 생성되지 않는다.

```powershell
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare --store <new-isolated-store> --repository kekwkekw/gctrans --channel c0a0192925899ebe4f67577adc02aa65ffad81f3
```

그 검증된 store를 재사용하는 별도 실제 HTTP branch 검사는 아래와 같다. 이 명령은 **CPU provider만 사용하는 폐기 store**에서 pending을 startup/LKG baseline으로 선택한다. 게임/native Initialize를 호출하지 않으며 운영 store에는 실행하지 않는다. 실제 branch ref/manifest HTTP와 완전 package의 검증 cache를 사용해 같은 SHA와 unchanged payload 다운로드·app read/hash 0, current/LKG 및 CPU 초기화 횟수 불변을 확인한다. source tree/반환 JSON의 scan은 하지 않는다.

```powershell
dotnet tests/Ng03SupplyTests/bin/Release/net9.0/Ng03SupplyTests.dll --actual-branch <same-disposable-store> c0a0192925899ebe4f67577adc02aa65ffad81f3 <scratch>/actual-branch.json
```

이 후보의 실제 GCMod.Candidate/UpdateCli 빌드는 warning/error 0으로 PASS, 공급 CPU/mock은 17/17 PASS였다. 기존 실패 commit의 실제 제품 `prepare`는 UPDATE_PREPARED/exit0으로 payload1,904개(40,189,430바이트)와 완전 package/pending을 준비했고 current/previous는 생성하지 않았다. SHA resolve 응답은 1,533바이트였고 재시도는 0이었다. 그 뒤 같은 폐기 store에서만 CPU startup을 수행한 실제 branch 검사는 동일 c0a01929…/UPDATE_NO_CHANGE, payload 다운로드0·재사용1,904·app payload read/hash0, OS hardlink1,904로 PASS였다. 초기 CPU startup의 Master 읽기를 warm updater의 payload counter0과 혼동하지 않는다. 이 단계에서 생긴 current는 폐기 CPU baseline이며 운영 current가 아니다.

상세 실행 결과는 로컬 표적 보고서로 구분한다. 번역 의미/WB 전수/전체 UI/native를 재검수하지 않는다. updater worker만 바뀌었으므로 기본 CheckUpdate OFF에서 기존 startup/Core를 다시 실기할 필요는 없으며 새 DLL의 직접 native는 NOT_RUN이다. 과거 native 성과를 새 바이너리의 직접 PASS로 옮기지 않는다. XUnity OFF와 HOLD/Known Residual은 유지한다.

이번 후속 변경은 로컬 commit 준비 범위다. PR #25 후속 push/PR 내용 갱신에는 별도 실제 공개 승인이 필요하고 main merge/Release/CDN/영구 설치 권한은 생성되지 않는다.
