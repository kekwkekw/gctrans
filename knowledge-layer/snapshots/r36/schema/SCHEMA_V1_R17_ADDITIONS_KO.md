# Schema v1 — R17 캐릭터 스토리 확장

기존 schema_version=1과 열거값을 변경하지 않는다. canonical entity와 기존 profile은 그대로이며 캐릭터 스토리 문맥별 근거만 append한다.

- `catalog/source_entries_character_r17.jsonl`: 본문 category=character_story, 실제 제목 category=story_title. context_group=character_story:<novel_id>.
- `source/r17/aligned_rows.jsonl`: 원문 및 원래 hand spans. 제목은 팬 표제 원본 전체를 exact_ko/heading_ko에 보존하고 대괄호 내부의 번역 제목만 ko로 분리한다. 추출 offset과 원래 표제를 함께 보존하며 재번역이 아니다.
- `review/speaker_resolutions_r17.jsonl`: 이름 없는 서술자는 자동으로 관장에 연결하지 않는다. 동일한 死の芸術 라벨은 스타일 사건별 로컬 작품으로 구별한다. 원래 화자 표시를 출력상 교체하지 않는다.
- `speech_address_rules`: 직접 청자, 제3자 언급, 자칭, 부재자 호칭, 독백과 절별 청자 전환을 구분한다. observed_address_ko는 원본 대사에 실제 존재하는 표면형이다. proposed form을 observed로 올리지 않는다.
- `review/local_participants_r17.jsonl`: 무명 작품·역할·물건·장소는 로컬 참조이다. canonical entity_type 열거값을 새로 확장하지 않는다.
- `catalog/context_groups`: 팬 게시물의 스타일 표제는 식별·출처 metadata이다. 실제 일본어 master style_name을 만들거나 공식 번역으로 승인하지 않는다.
- 의미 검수 완료와 사용자 승인·runtime 검증은 별도이다. translation=null/reviewing/두 ready flag=false를 유지한다.
