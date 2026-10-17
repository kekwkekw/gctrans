# 걸크리 PC 한국어화

운용·정책·검수 문서는 **[docs/INDEX.md](docs/INDEX.md)** 한 곳에서 찾는다. 명령은 `docs/`가 아니라 저장소 루트에서 실행한다.

## 현재 상태

PR27에서 구형 번역 파이프라인 퇴역과 기존 대사 외곽 괄호 **18,366행** 적용을 완료했다. 이번 문서/브랜치 정리는 게임 코드·DLL·번역·manifest·수락 상태를 바꾸지 않는다.

- [운용 절차](docs/OPERATOR_KO.md) · [재현](docs/REPRODUCE_KO.md) · [지원 범위](docs/SUPPORT_SCOPE_KO.md)
- [개인 Stable 정책](docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md) · [native 안전 범위](docs/PROJECT_NATIVE_TEST_POLICY_KO.md)
- [완료된 시나리오 교정](docs/SCENARIO_DISPLAY_APPLY_KO.md) · [퇴역 파이프라인](docs/RETIRED_PIPELINES_KO.md)
- [브랜치 정리와 역사 복구](docs/BRANCH_RETIREMENT_KO.md) · [이전 루트 README 원문](docs/README.md)

Repository package는 `efb4e3d02f38822ce3e5f50ac01dbbc8d505387b9bf8903bdb9b0b424d61946a`, DLL은 `96114569fbe38f113d49ad5e9554801ad1e55c6f062335e5085de428316dce2a`다. 실제 PC의 Stable/current/지속 PackageRoot는 별도 확인 대상이다. Novel 변경은 **restart-required**이며 UI-only live 변경이 아니다.

다음 작업은 최신 main을 로컬 Stable로 일관되게 전환한 뒤 누적 UI 기대 기준 전체와 현재 화면을 대조하는 것이다. Museum3는 Footer 미술관·Master MuseumBlocks와 다른 별도 미통합 기능이다. XUnity는 OFF를 유지한다.
