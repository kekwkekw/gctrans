# Post-R36 export/apply 후보 v1.2 — hold placeholder

- 정규 novel JSON: 338개
- 보류 12행: 실제 보류 본문/손번역을 재출력하지 않고, runtime key는 유지한 채 `[시나리오명 - 행 보류됨]` 안내값으로 치환.
- 보류 12행은 counted_as_reviewed=false / publish_ready=false를 유지하며 의미·editorial 검수 실적에 포함하지 않는다.
- 나머지 334개 파일은 v1.0 후보와 byte-for-byte 동일하다.
- mapped battle 13행과 speaker sidecar 1건은 기존 계획대로 regular novel deploy 대상에서 제외한다.
- GitHub 적용은 작업 branch에서만 수행하고 main/merge/deploy는 별도 단계다.

우선 audit/VALIDATION.json 및 audit/HOLD_PLACEHOLDER_BINDINGS.json을 확인한다.
