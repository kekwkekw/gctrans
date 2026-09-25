# Schema v1 — R18 캐릭터 스토리 확장

schema_version=1 및 canonical entity_type 열거값은 유지합니다. 본문은 character_story, 제목은 story_title입니다.

source/r18에는 원래 raw/손번역 spans/제목 표제 추출 offset을 보존합니다. catalog/source_entries_character_r18.jsonl에는 실제 검수653항목만 있습니다. 제목을 소스에 새로 생성하지 않습니다.

새 character 엔티티2개: フェルメール, ボッティチェリ. 전자는 직접 대사를 토대로 profile1개를 추가하고 후자는 제3자 언급뿐이므로 직접 말투 프로필을 생성하지 않습니다. 원문에 없는 전체 이름은 없습니다.

local_participants의 participant_type은 로컬 레코드 설명이며 canonical entity_type을 확장하지 않습니다. 무명 물건·조직·활동·역할·가설을 문맥 키로 분리합니다.

speaker_resolutions는 실제 라벨을 바꾸지 않습니다. remembered_quotation은 40106082/raw53~54의 회상, recalled_earlier_conversation은 바다 나들이 앞 대화의 회상입니다. 화자와 현재 현장 인물을 분리합니다.

F_TERM_CLARITY는 reviewing correction의 종류이며 source status나 term type이 아닙니다. 같은 회화의 명칭 명료화9건은 dependency로 묶되 global substitution을 만들지 않습니다.

각 쌍의 순서는 유지하지만 숫자 ID 순서만으로 전체 본편 연대를 확정하지 않습니다. 표기 정책·용어 승인·runtime 검증과 의미1차 검수 완료는 독립입니다.
