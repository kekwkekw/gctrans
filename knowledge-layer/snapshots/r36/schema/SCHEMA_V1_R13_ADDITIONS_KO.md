# schema v1 R13 문맥 기록

core schema/enums는 변경하지 않는다. 원문/손번역·승인 데이터와 runtime은 분리한다.
- 메인9장 634개 source entry는 reviewing, translation=null. 의미1차 완료는 사용자 승인이 아니다.
- 레이아·비르지니아 2명을 canonical entity로 추가하고 source full name·단축형은 references로 연결한다. 부친·경찰 역할·무명 친구·계획 작품은 local context keys로만 식별한다.
- reference의 target_entity_id=null일 때 target_local_key를 사용할 수 있으며 실제 local_participants_r13에 존재해야 한다. 전역 별칭 해소 금지.
- speaker resolution의 embedded_quote_speaker_entity_id는 인용 속 글 작성자를 뜻한다. 실제 관장 화자/본문을 변경하지 않는다.
- controlled_body_interpretation은 부친/서장의 조종 공개 근거를 연결할 뿐 source label을 바꾸거나 조기 정체를 출력하지 않는다.
- scene_time=FIRST_MEETING_FLASHBACK은 8화2~22 범위만 해당한다. 23행이 회상임을 명시한다.
- source 원문태그·루비와 손번역 풀이를 그대로 보존한다. observed 호칭에 제안 표기를 섞지 않는다.
- 미승인 용어, source 없는 이름, global substring 치환, runtime 산출물은 생성하지 않는다.
