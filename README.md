# 걸크리 PC 한국어화 main 통합 후보

현재 checkout의 종합 복원은 **로컬 공개 검토 후보**다. SelectAll·Shop 안내·캐시 탭 배지의 검증된 geometry Dev를 유지한다. [복원 업그레이드·구 client 호환·rollback](RESTORATION_MIGRATION_KO.md)을 먼저 읽는다. 신 DLL+완전 producer JSON+명시 Master profile+실제 선택 content가 적용 단위다. 아래 PR #25/main 기록은 이미 병합된 이전 기준이며, 이 복원 후보까지 공개되거나 설치 Stable로 승격됐다는 뜻은 아니다.

PR #25는 main에 병합됐다. 병합 검증 기준은 commit `82a8b0f7ef2c2760fe7139707fda69030eb4571c`, tree `d91fc64436b2b64ec192213723c2b48e8d6a6c72`이며 영구 runtime pin이 아니다. main은 새 release-manifest와 수락 번역 데이터를 제공한다. [네트워크 prepare의 대형 commit metadata 오류](NETWORK_PREPARE_FIX_KO.md)는 수정됐다.

소스·데이터의 main 통합은 전체 native 수락이나 정식 설치와 별개다. 실제 게임에 영구 설치하려면 외부 dependency, 기존 plugin/config/store 보존과 전환 범위를 별도로 결정해야 한다. main 병합을 게임 설치·자동 업데이트 opt-in·별도 Release 자산 발행의 승인으로 해석하지 않는다.

- [운용 명령](OPERATOR_KO.md): UI draft/review/apply, next_package, local source state, prepare-local과 live request.
- [지원 범위](SUPPORT_SCOPE_KO.md): 직접 native 증거, CPU 검사, known residual과 source coverage를 구분한다.
- [공개 전 영향·계약 검토 이력](PUBLICATION_KO.md): 당시 exact Git tree, workflow 경계와 소비자 영향. 현재 병합·공급 상태는 이 문서와 OPERATOR_KO.md를 따른다.
- [외부 입력](EXTERNAL_DEPENDENCIES_KO.md)과 [표적 native/원복](LAUNCH_PROCEDURE_KO.md): Medium·Utility·interop은 동봉하지 않는다.
- [재현 명령](REPRODUCE_KO.md)과 [상시 native 정책](PROJECT_NATIVE_TEST_POLICY_KO.md).

기본 Nextgen Enabled·CheckUpdate·UiRequestsEnabled는 OFF, locale은 ko, XUnity는 OFF다. 네트워크 resolve 변경으로 재빌드한 DLL의 CPU/실제 HTTP 검사와 이전 DLL의 native 증거를 구분한다. 번역 의미값·등록 상태와 UI21/UI22 ACCEPTED_LOCAL_FROZEN·Known Residual은 유지한다.
