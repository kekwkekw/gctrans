# schema v1 R6 선택 확장 필드

schema_version은 1이며 기존 schema/레코드는 변경하지 않았다. 아래는 검수·provenance용 선택 필드다.

- `knowledge/claim_developments.jsonl`: development_id, phases(시점별 epistemic_kind/summary/evidence/source_locations), retroactive_overwrite_allowed=false, global_fact_approved=false. 기존 claim guard나 관계를 덮어쓰지 않는다.
- speech_address_rules: utterance_mode=direct_address/self_reference/third_person_mention/offscreen_reference. speaker_local_label/target_local_label는 원문 인용이 아닌 로컬 대화 참여자 라벨일 수 있으며 canonical_source_name으로 export하지 않는다. source_locations는 다회차 근거 범위다.
- relationships: local_object_key와 object_literal_ko는 무명 작품/장면 집단을 구분하는 로컬 기술자다. 근거 없는 JP 고유명이나 전역 엔티티가 아니다.
- evidence_links: 기존 entity/term/reference/conflict 외 relationship을 대상으로 하는 추가 근거 연결을 허용한다. 어떤 연결도 translation 승인을 확장하지 않는다.
- source_entries: proposed_translation은 아직 적용되지 않은 제안 합성 미리보기이며 translation은 null. semantic_review_stage 및 review_status는 source status와 별도다.
- corrections: 표적이 제목인 경우 CSV row_identity+field와 그 필드 내 Unicode codepoint offset을 사용한다. 모든 원본은 동일하게 보존한다.
- `review/prior_reconfirmations_r6.jsonl`: 기존 finding/proposal ID를 재사용하며 새로운 중복 proposal을 만들지 않는다.
- 큐 완료 레코드의 SEMANTIC_FIRST_PASS_COMPLETED_NOT_APPROVED는 workflow 상태다. knowledge/source의 status_enum 승인 상태와 동일시하지 않는다.

활성 파일은 reports/NEXT_STEP.json의 active_queue와 all_findings_r6/all_proposals_r6다. 기존 R4/R5 큐·등록부는 이력으로 보존했으며 현재 잔여량으로 오인하지 않는다.
