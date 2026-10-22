# 걸크리 PC 한국어화

현재 작업 절차는 **[docs/OPERATOR_KO.md](docs/OPERATOR_KO.md)**, 문서 전체와 과거 기록은 [docs/INDEX.md](docs/INDEX.md)에서 찾는다. 명령은 저장소 루트에서 실행한다.

## Novel 번역

**업데이트 확인 → 로컬 Codex 초벌 → ChatGPT PRO 검수 → main 반영** 순서로 진행한다. 초벌·원문·검수 파일은 `work/novels/<batch>/`에서 관리하고 production 번역과 분리한다. 정상 Novel 경로는 public main의 도구로 실행되며 private workbench가 필요하지 않다.

- [현재 Knowledge](translation/knowledge.jsonl) · [현재 Names](translations/ko/names.json)
- [동결한 HOLD 정책 원본](translation/HOLD_POLICY.md) · [새 작업의 HOLD 기록](translation/holds.jsonl)
- 현재 Knowledge 요약: `python tools/translation_knowledge.py`
- 번역 명령: `python tools/update/cli.py novel --help`

## UI와 runtime

UI/Master는 기존 전용 도구와 검수 경로를 사용한다. 현재 package 정보는 [release-manifest.json](translations/ko/release-manifest.json)에 있으며, 실제 PC의 Stable/current/지속 PackageRoot는 별도로 확인한다. Novel·Names 변경은 재시작 대상이다. XUnity는 OFF를 유지한다.

- [지원 범위](docs/SUPPORT_SCOPE_KO.md) · [외부 실행 입력](docs/EXTERNAL_DEPENDENCIES_KO.md)
- [개인 Stable 정책](docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md) · [적용·복구](docs/LAUNCH_PROCEDURE_KO.md)
- [재현](docs/REPRODUCE_KO.md) · [퇴역한 경로](docs/RETIRED_PIPELINES_KO.md)
- [이전 시나리오 표시 교정](docs/SCENARIO_DISPLAY_APPLY_KO.md) · [브랜치 역사](docs/BRANCH_RETIREMENT_KO.md)

과거 수락 자료·Knowledge 원장·GCMod 6.1 호환 파일은 역사와 호환성 용도로 보존한다. 현재 Knowledge와 Names의 정본은 위에 연결된 두 파일이다.
