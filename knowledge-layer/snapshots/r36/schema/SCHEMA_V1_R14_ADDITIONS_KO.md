# R14 schema v1 문맥 확장
core schema/enums는 변경하지 않는다.
- source entries 674개는 reviewing/translation=null; 의미1차 완료와 승인·runtime은 별개.
- D・A는 실제 표기. 무명 노인·의사·검은 옷 여자·부상 소녀는 local participant. local_event는 검수 메타데이터로만 쓰며 canonical entity enum에 추가하지 않는다.
- 공동 화자 reference는 target_entity_ids 배열로 두 인물을 기록한다. 전역 치환·별도 인물 생성 없음.
- 3화 금발 소녀는 관장 자신의 첫 만남 회상에서 후향 식별. 이름 선택23행과 화자 라벨 변경25행 이전의 source 표시를 바꾸지 않는다.
- 6화 금발 소녀는 local role이며 reported_identification_entity_id와 WITNESS_ATTRIBUTED_NOT_GLOBAL_ALIAS로 노인의 지목만 기록한다. 관측과 실제 정체에 관한 증거 층위를 섞지 않는다.
- FIRST_MEETING_FLASHBACK(3화2~76), WITNESS_REPORTED_FLASHBACK(6화26~68)을 구분한다.
- 魔方陣과 魔法陣의 실제 source 표면형은 서로 유지한다. 기능 확인은 아니다.
- current 검수의 speaker/target/청자절은 실제 발화와 연결하며, 번역 원문과 runtime name replacement는 금지.
