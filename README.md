# 걸크리 PC 한국어화

현재 작업 절차의 단일 진입점은 **[docs/OPERATOR_KO.md](docs/OPERATOR_KO.md)**다. 명령은 저장소 루트에서 실행한다.

## 정상 Novel 흐름

> 업데이트 확인·원문 정리 → Codex 초벌 → ChatGPT PRO 의미검수 → Codex 적용·package 검증 → 승인된 main 반영

- 현재 Names: [`translations/ko/names.json`](translations/ko/names.json)
- 현재 Knowledge: [`translation/knowledge.jsonl`](translation/knowledge.jsonl)
- HOLD 정책: [`translation/HOLD_POLICY.md`](translation/HOLD_POLICY.md)
- 현재 package: [`translations/ko/release-manifest.json`](translations/ko/release-manifest.json)

정상 Novel 경로는 public main의 도구만 사용하며 private workbench나 과거 Knowledge snapshot을 요구하지 않는다.

## UI·runtime

- 지원 범위: [`docs/SUPPORT_SCOPE_KO.md`](docs/SUPPORT_SCOPE_KO.md)
- 외부 입력: [`docs/EXTERNAL_DEPENDENCIES_KO.md`](docs/EXTERNAL_DEPENDENCIES_KO.md)
- 적용·복구: [`docs/LAUNCH_PROCEDURE_KO.md`](docs/LAUNCH_PROCEDURE_KO.md)
- Stable 정책: [`docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md`](docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md)
- native 시험 정책: [`docs/PROJECT_NATIVE_TEST_POLICY_KO.md`](docs/PROJECT_NATIVE_TEST_POLICY_KO.md)
- 재현: [`docs/REPRODUCE_KO.md`](docs/REPRODUCE_KO.md)
- 공개 경계: [`docs/PUBLICATION_KO.md`](docs/PUBLICATION_KO.md)
- 출처·라이선스: [`docs/UPSTREAM_ATTRIBUTION_KO.md`](docs/UPSTREAM_ATTRIBUTION_KO.md)

## GCMod 6.1 호환

기존 6.1 사용자를 위해 `names/`, `words/`, `novels/`의 `zh_Hans.json` 경로와 `GCMod-cs파일백업/GCMod-6.1.0-updated/`를 유지한다. 현행 GCMod 7 정본은 `translations/ko/**`다.
