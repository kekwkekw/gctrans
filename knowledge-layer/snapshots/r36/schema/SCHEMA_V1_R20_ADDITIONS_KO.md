# R20 — 안전 보류와 부분 완료

status enum은 그대로 유지한다. 보류는 source entry의 가짜 translation이나 승인 상태가 아니라 별도 `review/deferred_rows_r20.jsonl`의 workflow_state=deferred로 관리한다. 원문을 지우거나 빈 번역으로 runtime 출력하지 않는다.

신규 aligned_rows는 680개 비보류 원문만의 검수 snapshot이다. 원래 691항목의 원본은 상속한 nested archive에 byte 그대로 보존된다. 11행은 위치와 원문 해시만 기록하고 신규 evidence·row_review·완료·큐차감에 넣지 않는다. 4개 기존 큐 항목은 유지하며 큐 밖7행도 별도 보류에 남긴다.

context_group에는 deferred_raw_indices와 semantic_first_pass_complete를 명시한다. NEXT_STEP의 partial_semantic_scope는 전체 완료와 구분되며 후속 독립 배치의 진행을 막지 않는다.
