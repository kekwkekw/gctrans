# schema v1 R7 선택 확장
schema_version=1 및 기존 schema/레코드는 그대로 보존한다. 신규 source catalog는 `catalog/source_entries_main03.jsonl`이다.

- `review/speaker_resolutions_r7.jsonl`: 원문 라벨과 canonical 해석을 분리하는 선택 메타데이터. source_locations/evidence_ids와 revealing_evidence_ids를 함께 보존한다. do_not_replace_source_speaker와 do_not_reveal_early는 항상 true. 익명 화자와 花屋를 정체 공개 전 실명으로 바꾸지 않는다. 무화자 1인칭의 초점 인물도 원문 speaker를 덮어쓰지 않는다.
- `review/source_note_separation_r7.jsonl`: 원문 내부 지시어 불일치와 손번역 괄호 풀이의 유형을 기록한다. 원문·손번역 byte 수정은 없고 glossary/style 승인을 암시하지 않는다.
- `references.constraints`: source_locations 및 reveal_safe_only는 이 문맥에서의 연결 범위다. 이름 없는 가게/사람을 전역 동일인으로 만들지 않는다. 모든 match는 exact.
- `knowledge/claim_developments.jsonl`: R6 기록을 append-only 보존하고 R7 단계를 추가한다. 각 phase의 근거/양태가 독립적이며 앞선 추측을 소급 확정하지 않는다.
- `review/prior_reconfirmations_r7.jsonl`: R3-W062/R3-W063/R3-W020을 재확인하되 기존 proposal은 그대로 두고 새로운 중복 proposal을 만들지 않는다.
- 활성 queue/등록부는 reports/NEXT_STEP.json에서 지정한다. 이력상의 R4~R6 큐 수량을 현재 값으로 오인하지 않는다.
