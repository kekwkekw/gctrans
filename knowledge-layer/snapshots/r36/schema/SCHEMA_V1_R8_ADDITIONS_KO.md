# schema v1 R8 선택 확장
기존 schema_version=1 및 이전 레코드를 그대로 보존한다. 신규 catalog는 source_entries_main04.jsonl이다.

- review/speaker_resolutions_r8.jsonl: 원래 女の子/ロダン？/老婆/右のセザンヌ/左のセザンヌ 라벨을 보존하며 정체 해석과 공개시점의 근거를 별도 기록. ロダン？의 후속 간접 식별은 medium이며 직접 변장 해제가 아니다.
- review/local_participants_r8.jsonl: 무명 옛 조직·적대 조직·초대 조직·테레사라는 미확인 권유자·의태 작품·로컬 역할. canonical_entity_id=null, global_resolution_allowed=false. 정본 entities의 지어낸 고유명을 대신하지 않는다.
- local_object_key: 위 local participant 또는 art 문맥 키를 가리킬 수 있다. named canonical과 별도다.
- references.constraints: source_locations에서 확인한 exact 표면형만 연결하며 부분문자열 치환·조기 화자 공개를 허용하지 않는다.
- speaker→target rules: 실제 화자 근거만 evidence_ids에 넣는다. 오른쪽 세잔은 포니, 왼쪽은 실제 세잔. 偽カティ/偽キャルト는 작품군이며 인물 말투 근거가 아니다.
- claim_developments: 초반 가설을 후반 자백·목격과 시간순으로 연결하되 소급 승인하지 않는다.
- prior_reconfirmations_r8.jsonl: R3-W065의 기존 제안 및 원본을 유지하고 근거만 추가.
- status reviewing/high confidence는 사용자 승인과 별개다. source translation은 모두 null.
활성 큐·등록부는 reports/NEXT_STEP.json을 따른다. 이전 라운드 파일은 이력이다.
