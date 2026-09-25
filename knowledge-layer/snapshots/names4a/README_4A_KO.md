# 걸크리 Names Layer — 4-A Raw Extraction Checkpoint

이 디렉터리는 R3~R36 canonical review corpus에서 관찰된 non-empty
`speaker_jp → speaker_ko` pair를 추출한 **4-A 중간 정본**이다.

- source: 34
- raw pair: 17024
- unique `speaker_jp`: 305
- parse error: 0
- source-structure limitation: 4
- conflict: 4-B로 이월

이번 단계에서는 dedup, conflict 선택, Post-R36 correction overlay,
신규 번역, `names/zh_Hans.json` 생성/반영을 하지 않았다.

파일:
- `speaker_pairs_raw.jsonl`: provenance 포함 원장
- `speaker_pairs_raw.csv`: 동일 원장의 평면 CSV
- `source_extraction_audit.json`: source/adapter/검증 결과
- `schema_adapter_report.md`: 구조 차이와 제한 설명
- `MANIFEST.json`: 산출물 hash/checkpoint

다음 단계 4-B의 입력 정본은 `speaker_pairs_raw.jsonl`이다.
