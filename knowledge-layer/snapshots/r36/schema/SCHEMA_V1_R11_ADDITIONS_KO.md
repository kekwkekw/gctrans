# schema v1 R11 문맥 기록

schema_version=1과 기존 상태/타입 정의는 유지한다. 이름·term 승인과 의미 검수 완료는 별개다.

- source_entries_main07: 본문789+제목10. reviewing/translation=null.
- 기존 entity는 수정하지 않고 evidence_links 및 claim_developments로 후속 근거를 추가한다.
- 테레사 방문자: 7화 닮음 언급/10화55 후속 서술 확인을 시간순으로 기록. retrospective_identity_only 및 resolution_available_after는 조기 이름 출력 금지이다.
- speech: 실제 청자, 제3자 언급, 자칭, 가상 호칭을 구별한다. local speaker는 canonical 인물을 임의 생성하지 않는다.
- context_identity_constraints: 이번 문맥의 인물·가설 범위 회귀 제약이지 승인된 lore나 runtime 검증이 아니다.
- 새 표면형은 exact/context-only이다. source의 하위 문자열을 확인해 evidence를 연결하는 것은 substring replacement 허용을 뜻하지 않는다.
- START_HERE 안내의 부모 stale 문제는 audit/METADATA_REPAIRS_R11.json에 남긴다.
