# schema v1 R10 추가 레코드

기존 schema_version=1, 이전 ID·원문·승인·정책은 그대로 보존한다.

- source_entries_main06: 본문641+제목10, 상태 reviewing/translation null.
- 같은 canonical_source_name의 별도 entity 허용: フラワーマン은 메인3장 전임자와 메인6장 계승자가 별개다. 이름 고유성을 ID 고유성으로 대체하지 않는다.
- entity.disambiguation_ko / distinct_from_entity_ids / same_surface_is_not_identity: 원문 이름에 가짜 일본어 접미사를 붙이지 않고 해석상 구분한다.
- term.context_constraint.source_locations: 동명이인의 특정 사용 위치만 해소. global_resolution_allowed=false. 참조의 source 위치보다 넓은 치환 금지.
- speech clause_targets / actual_interlocutor_entity_id / quoted_speaker_entity_id: 발화의 직접 청자와 제3자 언급·인용 속 청자를 구분한다.
- speaker_resolutions_r10: 원래 source 화자 표시는 불변. 익명 화자의 후향 연결에는 reveal_at 제한이 있다.
- context_identity_constraints_r10: 계승 인물·생존·실제 사망·미확인 배후·지원 상태의 안전 제약. 번역 승인이 아니다.
- proposal_dependencies_r10: 6화45~46의 반복 어구는 함께 채택/보류 검토. 원본 자동 적용은 하지 않는다.
- prior_reconfirmations: KEEP_OPTIONAL이면 proposal_id=null. 유지 결정을 교정 제안으로 생성하지 않는다.

활성 파일은 reports/NEXT_STEP.json을 따른다. 이전 이름의 queue·보고서는 역사 기록이다.
