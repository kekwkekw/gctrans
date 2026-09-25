# schema v1 — R5의 선택적 누적 테이블

schema_version은 1이다. 기존 schema_v1.json과 기존 knowledge 레코드는 변경하지 않았다.
다음 테이블은 기존 canonical 레코드를 덮어쓰지 않고 새로운 검수 문맥을 연결한다.

## 1. knowledge/evidence_links.jsonl

`link_id`, `target_kind`, `target_id`, `evidence_ids`, `context_groups`, `scope`, `observation_ko`.
기존 term/entity/reference/conflict에 메인 2장 1화의 추가 관찰을 연결한다.
기존 term의 `scope`나 `approved` 범위를 확대하는 레코드가 아니다. 모든 링크는 reviewing이며
`extends_translation_approval=false`이다. 기존 엔티티/용어의 본래 문맥과 이번 관찰 문맥을 합쳐 읽되
그 자체로 global substitution이나 출력 승인을 얻지 않는다.

## 2. knowledge/character_profile_observations.jsonl

기존 `profile_id`와 `entity_id`를 참조하는 문맥 한정 관찰 조각이다.
기존 R4 프로필 18개는 그대로 남았다. 새 인물 2명은 본래 character_profiles에 추가했다.
기존 인물 4명의 이번 관찰은 이 테이블에 저장한다. 소비자는 대상 프로필과 관련 문맥을 함께 읽는다.
`overrides_existing_profile=false`이며 1장의 다른 상대/시점 말투를 덮어쓰지 않는다.

## 3. knowledge/speech_address_rules.jsonl

`speaker_entity_id → target_entity_id`와 실제 호칭, 관찰된 한국어, 높임 수준, evidence를 연결한다.
현재 15개 모두 reviewing, `global_style_approved=false`이다. 이름 호칭과 문장 어미는 별개다.
같은 이름이 3인칭 언급으로 나온 경우를 직접 호칭 사례와 구분한다.
초면 인사의 존대→반말 전환을 본 것만으로 충돌을 자동 생성하거나 모든 대사를 통일하지 않는다.

## 4. source/evidence 및 proposal의 보존 규칙

- 본문 entry_id: `story:10010201:raw:0001` 형태.
- 제목 entry_id: 기존 v1과 같은 `story:10010201:title`.
- `translation=null`, `status=reviewing`, `publish_ready=false`, `runtime_verified=false`.
- `proposed_translation`은 미적용 교정안일 뿐이며 승인 값이 아니다.
- `ko_exact_fragment`는 기존 손번역 block의 원래 slice다. 두 병합 블록의 분할 경계 공백도 보존한다.
- `ko_original_aligned`는 R3에서 이미 검토된 표시용 단위다. R5가 새 문구를 원본에 적용한 것이 아니다.
- proposal의 start/end는 원래 block 내 Unicode codepoint 기준 0부터 시작하는 반개구간이다.
- proposal은 before 및 block preimage SHA-256을 포함한다. 변조되거나 다른 버전이면 적용하면 안 된다.
- `source/episode`는 편의용 subset이다. 전체 원본은 `source/gc_pro_review_r4.zip → source/r3.zip` 안에 보존된다.
- 원문에는 source message의 literal `\n`과 `<click=22>`가 그대로 남는다. 보고서의 줄바꿈은 표시 목적이다.
- 53행의 원 화자 표시는 `？？？` 그대로다. `speaker.resolution`은 뒤의 소개로 확인한 해석 메타데이터이며
  `do_not_replace_source_speaker=true`, `do_not_reveal_early=true`다.

## 5. 검수 완료와 승인/큐의 차이

`review_status`와 `semantic_review_stage`는 실제 1차 검수 이력이다. `status=approved`와 다르다.
일반 큐에서 빠졌다고 번역이 승인된 것이 아니다. R3 분할 검토만 끝나 큐에 없던 행도 회차 검수에는 포함한다.
현재 active queue는 NEXT_STEP.json에 적힌 `queues/semantic_review_remaining_r5.jsonl`뿐이다.
이전 r4 queue는 비교·감사용 원본이므로 두 큐를 합산하거나 원본을 최신 큐로 읽지 않는다.

## 6. 출력과 독립

모든 term/reference match는 exact다. 검증 코드의 일본어 표면형 등장 확인은 evidence 점검이며 substring
치환 규칙 생성이 아니다. runtime exporter/CDN/XUnity 파일은 생성하지 않았다. VALIDATION PASS는 무결성,
근거 연결 및 검수 범위의 점검이지 의미 판단의 무오류 보증이나 사용자 승인/실기 통과가 아니다.
