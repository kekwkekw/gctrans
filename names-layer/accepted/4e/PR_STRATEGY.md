# PR 전략 — 기존 #14 보존, 별도 replacement Draft PR 권장

현재 main은 `6c9be9e795a6ce686f35a6ccaec4481066b240e1`이다. #14는 open/draft/unmerged, head `64b37e571f9af25aedb529b99da5a656bc9b4536`, `names/zh_Hans.json` 한 파일 추가이며 mergeable/clean이다. 현재 main과 비교하면 ahead 1 / behind 2다. API의 PR base.sha는 과거 `9e4bfa...`로 표시되어 현재 branch HEAD와 구분해 기록했다. main에 names 또는 names-layer 디렉터리는 없다.

#14는 R34R-M007의 수산한→수상한 교정 하나와 당시 표시명을 보존하는 검수 단위다. 이번 1,070개 전체 acceptance와 같은 key의 새로운 자연화 문구까지 #14에 덧붙이면 과거 교정 승인과 새 전체 표시명 승인이 섞인다. 기술적 merge conflict는 없지만 새 provenance 경계와 review 범위는 별도 PR이 더 분명하다.

권장 전략 B: #14를 수정·닫기·병합하지 않고, 검증된 최신 main에서 별도 `review/names-layer-4e-accepted-1070` branch와 replacement Draft PR을 준비한다. 현재 branch 목록에는 이 이름이 없다. 실제 생성 직전에 상태와 branch 이름을 다시 확인한다. 새 PR에서 #14는 역사적 선행 교정이며 이 PR의 전체 mapping이 후행한다는 관계를 명시한다. #14의 이력은 그대로 둔다.

후속 PR scope 제안: `names/zh_Hans.json` 및 독립 `names-layer/accepted/` provenance/checkpoint/verifier. 기존 `knowledge-layer/**`, novels, words, GCMod, workflow, Pages 설정은 수정하지 않는다. accepted ledger는 Batch09 원순서와 immutable source references를 유지한다. 비밀 signing key나 game bundle cache를 commit하지 않는다. candidate 파일 수/경로 및 staged Git blob SHA를 출력 checkpoint와 대조한 뒤 commit한다.

이번 단계의 선택: local accepted checkpoint와 production candidate를 완료하고 Git mutation은 0으로 유지했다. branch/commit/push/PR 생성은 수행하지 않았다. 이 문서는 제안이며 생성된 PR을 뜻하지 않는다. live delta는 0이므로 새 의미 검수 없이 runtime gate 준비로 넘길 수 있다. 이후 독립 candidate를 테스트 가능한 경로에 준비하더라도 production CDN 교체 및 main merge는 별도 gate다.

#14를 먼저 병합한 다음 덮어쓰거나, 기존 한 건의 승인을 전체 자연화 문구에 소급 적용하지 않는다. runtime 성공 전 merged/deployed/runtime-verified로 표시하지 않는다.
