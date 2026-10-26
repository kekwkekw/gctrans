# 1,499편 현행 Knowledge PRO 검수 및 적용 기록

- 검수 ID: `GC-FULL-1499-CURRENT-KNOWLEDGE-PRO-20261024`
- PRO 검수일: 2026-10-24
- 로컬 적용·검증일: 2026-10-26 (Asia/Seoul)
- 적용 기반 main: `f80efdc427a7b5393b14a1f549b8188de51d5d95`
- 입력 ZIP SHA-256: `20195e44d840fd7f670e84779dcdb0c8e50f23f26c2d4a42679a8548e20d8118`

PRO 결정에 따라 1,499편·114,243행을 422개 canonical batch로 적용했다. 초벌 대비 수정은 3,242행이며, HOLD는 3편·16행이다. source별 최종 rows 및 mapping 해시를 제공된 oracle과 대조했다.

Knowledge는 정돈된 3,534건에서 승인된 `イマージュ` global 확정과 신규 `音精 → 음정령` 두 건만 변경해 3,535건이 됐다. 최종 `translation/knowledge.jsonl` bytes는 제공된 expected 파일과 일치한다. 기존 knowledge-layer 원본은 보존했다.

일반 review 저장기가 변경안 내부의 JSON 키를 정렬해 승인 레코드 2개의 직렬화 순서가 expected와 달랐던 점을 확인했다. 3,535건의 의미 값 차이는 0건이었다. 해당 fresh review의 Knowledge 변경안 키 순서를 제공된 PRO 원본대로 보존한 뒤 기존 apply로 반영해 expected bytes와 일치시켰다. 번역 행·Knowledge 값·scope·승인 내용은 바꾸지 않았다.

422개 fresh batch를 모두 먼저 준비하고 check한 뒤 apply했다. 적용에 사용한 Names·Knowledge 값·범위·상태는 PRO clean context와 같은 현행 원장을 기준으로 확인했다. projection의 참고 항목 선택 구성은 다를 수 있으며, 과거 context hash를 재사용하지 않았다. Knowledge 변경 batch는 마지막에 한 번만 적용했다.

일반 batch의 apply는 서로 겹치지 않는 4개 격리 package 사본에서 기존 `novel apply` 구현으로 수행했다. 결과를 모을 때 각 source의 현재 preimage와 검수 완료 payload bytes를 대조하고 기존 builder로 package metadata를 갱신했다. 그 뒤 최종 checkout에서 422개 batch를 모두 다시 check한 후 Knowledge batch를 적용했다.

최신 main에 추가된 시나리오 42편과 Names 5개를 유지했다. 기존 Names 값, Novel 등록 상태, 선택하지 않은 package payload는 변경하지 않았다. 전체 package는 release payload 1,946개(메타데이터 포함 실제 파일 1,948개)의 exact membership과 bytes/hash 검증을 통과했다. 인계 기준의 release payload 1,904개에서 main 추가분 42개가 늘어난 구성이다.

앞서 검수·로컬 적용돼 원본 checkout에 남아 있던 마네 6편과 JP-key 입력 경로·회귀 검사·운영 문서도 함께 보존했다. 이번 full-draft 결정은 그 6편을 다시 번역하거나 덮어쓰지 않았다.

최종 Knowledge까지 반영된 상태에서 대표 accept·patch·HOLD·Knowledge batch의 재적용이 모두 `changed_files: []`임을 확인했다. 전체 batch를 두 번 적용하지 않았다. 일반 대표 batch는 변경된 Knowledge record를 참고하지 않는 배치를 선택해, 동결된 context를 재투영하지 않고 최종 상태를 검증했다. 관련 회귀 검사 47개가 통과했다. 게임 runtime 검증은 수행하지 않았다.

이 폴더의 PRO 결정·metadata·해시가 이번 반영의 기록이며, 구 PRO 결정본은 authority로 사용하지 않았다. 로컬 batch/source/context/draft 전체는 `work/novels/`에 보관하고 Git에는 넣지 않는다.
