# schema v1 R9 optional additions
기존 schema_version=1 및 이전 레코드/ID는 그대로다.

- catalog/source_entries_main05.jsonl: 본문730+제목10, source status는 reviewing, translation=null이다.
- knowledge/policy_evidence_links.jsonl: 기존 사용자 피오렌체 정책의 역사적 source 미확인 행을 고치지 않고 실제 フィオレンツァ evidence와 연결한다. binding_stage=SOURCE_ATTESTED, resolves_source_attestation_only=true. effective_unresolved_source=false는 이 연결의 source 확보 상태이지 승인 승격이 아니다. 기존 policy6개는 byte-identical하다.
- term의 user_policy_ref: 제안 표기의 근거 정책. term reviewing/translation null은 유지한다.
- source_form_roles: 말투 기록의 자칭/직접 호칭/제3자 언급을 구분하는 선택 맵이다. 발화의 청자와 언급된 인물을 혼동하지 않는다.
- utterance_scope_jp: 한 발화에서 청자가 바뀌는 경우 실제 원문 절을 기록한다. 뒤러의 あなた가 4화55/5화30에서 화상을 가리키는 근거다.
- review/speaker_resolutions_r9.jsonl: entity_ids 복수 목록으로 공동 화자를 해석한다. 원문 라벨 변경·새 공동인물 생성 금지.
- review/local_participants_r9.jsonl: 무명 화상/귀족/장면별 기사 집단. canonical_entity_id=null, global_resolution_allowed=false.
- review/second_pass_r9.json: 같은 검수자의 실제 고위험 재대조. 독립 정확도 시험이 아니다.
- audit/CANDIDATE_REVISIONS_R9.json: 문체 후보 철회4건과 confidence 조정3건. 원본 변경은 아니다.

활성 큐·등록부·NEXT_STEP은 reports/NEXT_STEP.json을 따른다. 과거 라운드는 보존 이력이다.
