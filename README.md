# 걸크리 PC 한국어화 로컬 공개 검토 후보

NG11은 수락된 번역과 단일 GCMod 후보를 유지한 로컬 Git 검토 단계다. 공개 push·PR·merge·release·Actions dispatch·영구 설치는 수행하지 않았다. 현재 public main이 이 후보의 release-manifest를 공급한다고 가정하지 않는다.

- [운용 명령](OPERATOR_KO.md): UI draft/review/apply, next_package, local source state, prepare-local과 live request.
- [지원 범위](SUPPORT_SCOPE_KO.md): 직접 native 증거, CPU 검사, known residual과 source coverage를 구분한다.
- [공개 영향과 공급 계약](PUBLICATION_KO.md): exact Git tree, workflow 경계, 소비자 영향과 별도 공개 권한.
- [외부 입력](EXTERNAL_DEPENDENCIES_KO.md)과 [표적 native/원복](LAUNCH_PROCEDURE_KO.md): Medium·Utility·interop은 동봉하지 않는다.
- [재현 명령](REPRODUCE_KO.md)과 [상시 native 정책](PROJECT_NATIVE_TEST_POLICY_KO.md).

기본 Nextgen Enabled·CheckUpdate·UiRequestsEnabled는 OFF, locale은 ko, XUnity는 OFF다. Python/workflow/docs-only 변경 때문에 같은 DLL의 기존 native 결과를 새 시험으로 기록하지 않는다. 번역 의미값·등록 상태와 UI21/UI22 ACCEPTED_LOCAL_FROZEN·Known Residual은 유지한다.
