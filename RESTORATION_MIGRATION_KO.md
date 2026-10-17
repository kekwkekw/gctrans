# 종합 복원 통합 후보: 적용과 되돌리기

이 묶음은 검증된 geometry Dev의 **로컬 main 통합 후보**다. 공개 main에 병합되거나 개인 PC Stable로 영구 승격된 상태가 아니다. 기존 PR #25의 main 병합과 이번 복원 후보의 향후 병합을 구분한다. 공개 push/PR/merge/Release와 영구 설치 권한은 각각 별도다.

## 적용 단위와 구 client 영향

다음 네 요소를 같은 검토 tree의 한 업그레이드 단위로 사용한다.

1. 복원 producer 계약을 지원하는 새 `candidate/GCMod.dll`.
2. `translations/ko`의 **완전 패키지**. producer16과 수락 UI6 활성 복원이 포함된다.
3. `staging/accepted-master-profile.json`의 명시 table profile. 현재 수락62개의 목록이며 `*`나 미래 모든 table의 자동 승인이 아니다.
4. 기존 nonempty store에서 정상 pending/startup commit으로 선택된 실제 content.

ContentId는 DLL과 설정을 식별하지 않는다. 같은 content라도 과거5-table 설정이면 전체 적용 복원이 아니다. DLL-only, cfg-only, PackageRoot-only 전환을 완료로 보고하지 않는다.

구 r2/현재 public-main client는 새 producer JSON을 안전하게 거부하고 유효한 기존 current/LKG를 보존한다. 이는 구 client의 최신 데이터 적용 성공이 아니다. 구 schema+구 bindings는 허용되고 구 schema+새 bindings는 UNKNOWN_PROPERTY다. 새 schema는 구/신 bindings 형식을 읽지만 새 DLL+구 JSON에는 producer/활성 복원이 없다. producer를 제거하거나 unknown 속성을 무시해 구 client를 억지 허용하지 않는다.

기존 Master/Names/Novel/UI resources의 의미값과 legacy 등록 manifest는 보존된다. 변경 데이터는 복원 bindings와 관련 UI/release manifest이며 기존 WB13~M4·Names/Knowledge의 parser/locator/검수 계약을 대체하지 않는다. data updater는 DLL이나 font를 설치하지 않는다. 코드/DLL·Master profile 변경은 정상 재시작 대상이다.

## 승인 뒤의 최소 전환

게임을 정상 종료하고 권한 있는 실제 process 조회가 성공한 상태에서 진행한다. 실행 중인 사용자 게임은 강제 종료하지 않는다. 현재 DLL/cfg/기존 store 전체와 current/previous/pending_restart/request의 존재·비존재를 설치/검색 경로 밖에 새로 백업하고 목록·bytes를 확인한다. 원래 없다는 과거 기록을 현재 상태로 가정하지 않는다.

검토한 완전 패키지를 plugins 밖에 준비한다. **새 CLI/새 DLL의 store 계약**으로 expected-current를 명시해 pending을 준비한다. 아래는 승인된 전환에서만 사용하는 경계 예시이며 자동 설치 명령이 아니다.

```powershell
dotnet build tools/UpdateCli/UpdateCli.csproj -c Release
dotnet tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll prepare-local --store <approved-store> --package <complete-package> --based-on <actual-current-content-id>
```

prepare-local은 완전 후보와 pending만 저장하며 current 선택/native Initialize/live reload를 실행하지 않는다. current.json을 수동 편집하지 않는다. trial 이외의 첫 검사는 별도 isolated store를 사용한다.

검증한 DLL 하나만 expected-current CAS로 반영하고 구 GCMod/8-addon과 중복 로드하지 않는다. Utility·승인 Medium·core/interop/map·다른 plugin은 유지한다. GCMod 폴더 전체를 옮겨 dependency를 제거하지 않는다. profile fragment는 cfg 전체를 덮는 파일이 아니다. 해당 EnabledTables만 명시 목록과 대조하고 무관한 개인 설정은 유지한다. 기존 사용자 추가 table과의 충돌은 leaf별로 보존 검토한다.

정상 DMM 시작 후 실제 DLL, successful runtime commit, 선택 content, producer와 명시 table 설정을 함께 확인한다. 유효한 current/pending은 bootstrap PackageRoot보다 우선할 수 있다. PackageRoot 변경을 store 격리로 설명하지 않는다. 단일 coordinator/current/previous/pending_restart/LKG와 UI-only legacy session/cache 보존은 그대로다.

기본 opt-in/Nextgen Enabled·CheckUpdate·UiRequestsEnabled는 OFF, locale ko, XUnity OFF다. 승인된 로컬 전환에서 필요한 opt-in만 별도로 적용한다. 인터넷 업데이트를 이유로 신규 번역·새 font/DLL을 자동 적용하지 않는다. 설정/팀 저장·출격·스킵·소비·수령·교환 확정은 시험 절차에 없다.

## rollback과 영구 전환

정상 종료 후 자신이 쓴 postimage와 원본을 CAS로 대조한다. 원래 cfg/store/pending/current 관계를 먼저 복원하고 구 DLL은 마지막에 돌린다. 구 DLL+새 producer current가 함께 남은 상태로 실행하지 않는다. 원래 immutable packages·선택 기록을 지우거나 store를 초기화하지 않는다. unknown bytes/외부 동시 변경/reparse는 보존하고 충돌을 보고한다. 여러 파일 모두가 함께 atomic하다고 주장하지 않는다.

기존 nonempty store의 전환·재시작·정확 원복과 실제 old/new C# loader/LKG 증거는 source/data가 같은 경계에서 재사용한다. 문서만 바뀌었다고 전체 corpus/network/native를 다시 검사하지 않는다. 필요 trial의 actual selection과 정확한 own-write 복구는 별도로 확인한다. 설치본에 새 묶음을 유지하는 영구 승격은 별도 실제 승인 뒤에만 한다.

## 지원 범위와 다음 UI 작업

SelectAll·교환 상점 안내문·캐시 탭 복귀 배지는 같은 최종 geometry DLL의 실제 정착/재열기 근거를 유지한다. 전체379 binding/377 resource와 Master62-table/101-field는 감사 범위이며 runtime 개수 gate나 전체 native PASS가 아니다. table translator load는 모든 field의 화면 관측이 아니다.

Battle B01/B02의39행은 출격 전 준비/적 상세/적 기법 정보 계열이다. 이전 exact write/readback30, 적 기법 정보7, 현재 비활성 memory 안내1, Water 규칙과 Thunder source variant 불일치1을 구분한다. write/readback·이전 화면·현재 call-path 동등성·현재 DLL 직접 관측은 별도 축이다. 일반 캐릭터 기법 popup을 적 기법 정보의 proof로 쓰지 않는다. 출격 라벨 조회와 실제 출격은 다른 행위다. 39개를 신규 결함이나 강제 재시험 개수로 취급하지 않는다.

통합 배치에서는 동일한 최종 geometry DLL로 실제 적 상세의 기법 아이콘을 열어 7행의 한국어 정착을 직접 확인했다. 같은 준비/적 상세 경로에서 37행의 exact write/readback도 자연 관측됐지만 viewport 밖의 leaf나 모든 model variant의 화면 PASS로 확대하지 않는다. memory 안내는 현재 비활성이며 水 규칙의 위치에는 雷 source가 있으므로 나머지2행은 조건부로 보존한다. source를 바꾸거나 출격/스킵으로 표본을 만들지 않았다.

나머지 Battle69/cost는 실제 전투 owner/완료 parameter가 없는 범위로 유지한다. hidden Campaign·현재 CheckBox rarity shape·exact CV variant, strict async UNPROVEN·Choice Release HELD·source PARTIAL도 별도다. 확인 미복원은 정확한 ID와 이유를 남기며 Known Residual로 이름만 바꿔 닫지 않는다.

PRO04 괄호18,366 proposal/HOLD, PRO05 Museum3, PRO06의 최종 build/native 미확인 owner/reload, 20003601 NEW는 자동 병합하지 않는다. 후속 UI 작업은 **실제로 병합된 main SHA/tree를 다시 읽은 뒤** 그 기준에서 필요한 method만 reconcile한다. 로컬 후보 SHA를 이미 merged main으로 부르지 않는다. GeneralUiProvider/Geometry/Rows/UiModules의 공통 font/Footer·현재 Shop 재진입·producer/ownership을 Museum 파일 전체 덮어쓰기로 잃지 않는다. 자세한 lineage는 [UI 작업 재개 인계](UI_PLAN_RESTART_HANDOFF_KO.md)를 따른다.
