# Translation Knowledge 현행본 계약

## 역할

- `translations/ko/names.json`: exact 이름 표시의 현재 정본
- `translation/knowledge.jsonl`: 현재 번역에 실제로 사용하는 유효 지식
- `knowledge-layer/`: 원시 관찰, 과거 후보, 해결 전후 conflict, superseded 기록과 provenance

## Legacy 판정 기준

- record가 오래됐거나 과거 review에서 만들어졌다는 사실만으로 legacy가 되지 않는다.
- 현재 Names·새 Knowledge와 모순되거나, 후속 지식으로 대체됐거나, 해결 전 후보·conflict로 남은 경우에만 active에서 제외한다.
- 과거에 일어난 사건도 현재 관계·말투·연속성을 이해하는 데 유효하면 active에 남긴다.

## Active에 남는 정보

- 현재 유효한 character/entity identity와 profile
- 상대별 말투·호칭과 문맥 제한
- 인물 관계와 관계 변화의 source별 관찰
- 사건 전개와 이후 이해에 필요한 continuity
- 작품 고유 용어·기술·조직·장소
- 과잉 일반화와 오역을 막는 scoped claim guard

## Active에서 제외되는 정보

- Names와 충돌하는 옛 이름 후보
- 해결된 conflict와 superseded 결정
- 아직 확정되지 않은 후보
- raw evidence/link wrapper
- 이미 현행 요약에 흡수된 반복 관찰

`approved`는 해당 record의 명시적 scope 안에서 현재 사용할 수 있다는 뜻이다. scope 밖 전역 사실로 자동 승격하지 않는다.
