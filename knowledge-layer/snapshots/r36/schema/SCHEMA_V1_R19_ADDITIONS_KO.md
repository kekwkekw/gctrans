# Schema v1 — R19 확장

schema_version=1 및 type/category enum은 변경하지 않습니다. R19 catalog에는 character_story 본문765와 story_title10만 추가합니다.

1. source/r19/aligned_rows의 join_separator는 실제 reviewed_key_spans의 구분자입니다. 40106101/raw16은 KO2블록 사이의 개행을 보존합니다. 원래 alignment·hand block을 변경하지 않습니다.
2. 제목 교정1건의 target.surface는 alignment/title_rows_reviewed.csv이며 arca_title_ko 전체 게시물 표제에 대한 Unicode offset·field_preimage_sha256을 보유합니다. 실제 제목 추출 범위 안만 후보화하고 전체 표제는 보존합니다. 본문50개 교정은 기존 block target입니다.
3. speaker_resolutions의 imagined_dialogue / recalled_earlier_conversation / inaudible_murmur / private_self_talk / deceptive_third_party_mention은 원문 라벨을 바꾸지 않는 문맥 메타데이터입니다. 괄호만으로 화자·발화 양식을 결정하지 않습니다.
4. ルノワール는 실제 언급으로 엔티티1개를 추가하지만 직접 발화가 없어 새 캐릭터 말투 프로필은 생성하지 않습니다. 새 profile은0, 기존 profile 문맥관찰은12입니다.
5. 로컬 역할/물건/활동은 canonical person과 다릅니다. 일반 방문·상상 초대·회상 대화를 구분하며 style ID순서만으로 세계 연대를 확정하지 않습니다.
6. 씨앗 심기는 실제 행동, 발아는 기대입니다. 이번 외출을 데이트로 받아들인 직접 대사는 보존하되 정식 교제·혼인·모든 저주 해제로 확대하지 않습니다.
7. 승인/원본 적용/runtime_verified는 별개이며 모든 신규 source entry는 reviewing, translation=null, publish_ready=false, runtime_verified=false입니다.
