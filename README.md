# 걸크리 PC 한국어화 로컬 공개 검토 후보

NG12 공개 baseline은 Draft PR #25의 `c0a0192925899ebe4f67577adc02aa65ffad81f3`이다. 이 후속 후보는 수락 번역을 유지하고 [네트워크 prepare의 SHA resolve만 보완](NETWORK_PREPARE_FIX_KO.md)한다. 공개 반영 상태는 실제 Git ref로 확인하며, 현재 public main이 이 후보의 release-manifest를 공급한다고 가정하지 않는다.

- [운용 명령](OPERATOR_KO.md): UI draft/review/apply, next_package, local source state, prepare-local과 live request.
- [지원 범위](SUPPORT_SCOPE_KO.md): 직접 native 증거, CPU 검사, known residual과 source coverage를 구분한다.
- [공개 영향과 공급 계약](PUBLICATION_KO.md): exact Git tree, workflow 경계, 소비자 영향과 별도 공개 권한.
- [외부 입력](EXTERNAL_DEPENDENCIES_KO.md)과 [표적 native/원복](LAUNCH_PROCEDURE_KO.md): Medium·Utility·interop은 동봉하지 않는다.
- [재현 명령](REPRODUCE_KO.md)과 [상시 native 정책](PROJECT_NATIVE_TEST_POLICY_KO.md).

기본 Nextgen Enabled·CheckUpdate·UiRequestsEnabled는 OFF, locale은 ko, XUnity는 OFF다. 네트워크 resolve 변경으로 재빌드한 DLL의 CPU/실제 HTTP 검사와 이전 DLL의 native 증거를 구분한다. 번역 의미값·등록 상태와 UI21/UI22 ACCEPTED_LOCAL_FROZEN·Known Residual은 유지한다.
