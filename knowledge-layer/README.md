# Knowledge 이력 보관

**현행 Knowledge는 [`translation/knowledge.jsonl`](../translation/knowledge.jsonl) 하나다.**
이 폴더는 정상 초벌·PRO 문맥의 입력이 아니며 과거 원장을 읽어 current를 재생성하지 않는다.
current의 source-scoped 기록이 범위 밖에서 `reference`로 선택되는 것은 퇴역 자료의 재사용과 다르다.

유지하는 이력:

- `provenance/corpus-changes/`: corpus apply가 만든 exact 위치 교정·Knowledge before/after 영수증.
- `provenance/corpus-policy-resolutions/`: 과거 정책 hold의 해소 위치와 근거.
- `provenance/current-changes/`: Novel apply에서 실제 Names/Knowledge 변경이 생길 때 생성하는 이력.

2026-10-24 정리·전체 Novel 검수의 원장·매핑·중복 payload는 Git 이력으로 보존한다.
정확한 고정 commit과 복구 위치는 [과거 기록 안내](../docs/HISTORY_INDEX_KO.md)에 있다.
current record의 `origin.source_map_ref` 등 당시 출처 문자열은 역사 메타데이터로 그대로 유지한다.

새 증분 변경은 기존 ID·preimage·scope·승인 근거를 사용한다. 수량을 늘리거나 과거 데이터를 다시
합치기 위한 정리 작업은 하지 않는다. [운영 계약](../docs/CORPUS_WORKFLOW_KO.md)을 따른다.
