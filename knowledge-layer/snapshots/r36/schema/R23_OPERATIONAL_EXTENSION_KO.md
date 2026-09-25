# R23 운영 확장 — schema v1 유지

승인 enum candidate/reviewing/approved/deprecated/conflict 및 lineage1.0.1 유지.
review/row_reviews_r23.jsonl은 행별 판정·기존 큐 membership을 연결한다. semantic coverage와 final approval는 분리된다.
speaker_resolutions_r23의 mode/audience_refs/audience_resolution/heard_by_target는 함께 읽는다. null은 false와 다르다. retrospective_overhearer는 뒤에서 밝혀진 엿듣기이지 원본 라벨·주된 청자 재작성 명령이 아니다.
local_participants_r23는 문맥 한정 사람·사물이고 canonical entity 추가가 아니다.
semantic_contracts_r23는 실제 검수한 역할·가정·결과에 관한 회귀 조건이다. 자동언어판정의 완전성이나 전역승인사실이 아니다.
editorial_deferred_issues_r23는 후속부호통일workflow이며 민감내용deferred registry·승인enum과 별개다. 모든 기존 지침에 PUNCTUATION_EDITORIAL_ADDENDUM_KO.md를 추가해 읽는다.
