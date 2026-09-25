# Post-R36 export/apply 후보 v1.2 — hold placeholder

## 결론
- 정규 novel JSON 338개를 다시 포함한다.
- 명시 보류 12행은 보류 본문/손번역을 재출력하지 않고, 원래 runtime key를 유지한 채 `[시나리오명 - 행 보류됨]` 안내값으로 치환한다.
- 보류 12행은 검수/승인 완료로 세지 않으며 `counted_as_reviewed=false`, `publish_ready=false` 상태를 유지한다.
- 4개 보류 포함 novel의 비보류 행은 v1.0 승인 후보를 그대로 유지한다.
- 나머지 334개 JSON은 v1.0과 byte-identical하다.
- battle 13행과 speaker sidecar 1건은 regular novel deploy에서 계속 제외한다.

## 보류 placeholder 제목
- 40112011: `[끝없는 탐구심 - 행 보류됨]` (1행)
- 40112092: `[에로스를 섬기는 무녀 - 행 보류됨]` (5행)
- 22001004: `[슬픔과 분노 - 행 보류됨]` (3행)
- 22001006: `[재기를 도와주는 자들 - 행 보류됨]` (3행)

보류의 원문/기존 손번역 본문은 이 보고서와 hold audit에 포함하지 않는다.
