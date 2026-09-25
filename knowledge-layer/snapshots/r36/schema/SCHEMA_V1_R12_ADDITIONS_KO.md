# schema v1 R12 문맥 기록

schema_version=1, core enum, 승인 상태는 유지한다. 본문644+제목11을 source_entries_main08에 reviewing으로 추가한다.

- canonical entity 새 항목은 튈프뿐. 플라워맨(2대째)의 실제 새 source label은 기존 계승자에 reference/evidence로 추가한다.
- 작품 네 개, 무명 조직·역할 인물 등은 review/local_participants_r12.jsonl에 context-local 레코드로 둔다. local_id는 stable key이나 전역 canonical 승격이 아니다.
- 관계가 local 작품/인물을 주어로 가질 때 subject_entity_id=null와 subject_local_key를 사용한다. local_object_key도 동일 resolver로 검증한다. null endpoints는 local key가 반드시 있어야 한다. entity_type의 허용값을 임의 확대하지 않는다.
- speech의 speaker_entity_id가 null이면 speaker_local_key와 실제 source label을 기록한다. 청자는 canonical/로컬/일행/독백을 구분하며 언급된 제3자는 mentioned_entity_ids에 둔다.
- 호칭은 실제 JP 대사/손번역에서 관찰한 값이다. proposed translation의 호칭을 observed 값으로 넣지 않는다. 보정된 preview는 미적용이며 원본 source·speaker는 유지한다.
- 4화9 익명 석상은 11행 이후 후향 식별만 허용. 11화9의 レン는 그대로 두고 전체 이름을 보충하지 않는다.
- 무명 조직은 피니스로 자동 해소하지 않는다. source상의 카ギ/단어 포함 여부 확인은 substring replacement 허용과 다르다.
- knowledge와 원문·runtime을 분리한다. 모든 후보는 reviewing이고 승인·export·publish는 0이다.
