# Names 4-A 재개 메모 — 원본 미동봉을 명시한 상태 요약

문서 역할: 2026-09-25에 조회한 기존 4-A 보고서의 요약. 원본 원장/검증기를 재생성한 것이 아니며, 아래 PASS는 당시 보고서의 결과다.

## 확인된 마지막 지점

`schema_adapter_report.md`와 `VALIDATION_4A.json`에서 34개 source, raw pairs 17,024행, unique JP speaker keys 305개, parse error 0, source limitation 4개를 확인했다. 상태는 `PASS_WITH_DOCUMENTED_SOURCE_LIMITATIONS`다.

제한 라운드는 R3/R4/R16/R21이다. JP·KO 라벨 양쪽을 동시에 드러내지 않는 원래 HTML 구조 때문에 counterpart를 추정하지 않았고, 이 4개 라운드를 새 의미 검수 대기라고 처리하지 않았다.

`post_r36_overlay_applied=false`, `dedup_or_conflict_resolution_performed=false`. conflict 수는 0이 아니라 null/4-B 이월이다. PR #14와 production은 변경하지 않았다.

## 파일

우선 확보: `gc_names_layer_4A_checkpoint_v1.zip` (Library size 376,941 bytes).

원장 재사용의 최소 입력은 `speaker_pairs_raw.jsonl`(7,132,555 bytes), `source_extraction_audit.json`이며, `schema_adapter_report.md` 및 `VALIDATION_4A.json`을 같이 보존한다.

이전 `gc_names_layer_4A_checkpoint.zip`(416,502 bytes)도 존재한다. 이름이 비슷하다는 이유로 최신 v1과 합산하거나 무접미사 파일을 우선하지 않는다.

## 이번 전달의 한계

Library 목록·보고서 텍스트 조회는 성공했지만, 위 ZIP/원장/보고서들의 raw-file materialization은 “authorized raw-byte materialization path 없음”으로 실패했다. 따라서 새 지원 ZIP에 원본을 넣지 않았다. 원본 파일의 SHA도 이 메모에서 만들어내지 않는다.

검색/조회 성공 ≠ 실제 원본 ZIP 전달 성공. 사용자는 기존 저장 파일 또는 Library의 원본 파일을 새 프로젝트에 추가 첨부하는 것이 좋다. 이 파일의 미동봉은 R36 본체를 사용한 Knowledge 보존 시작을 막지 않는다.

## 재개 순서

먼저 Knowledge Layer GitHub 보존을 진행한다. 그 다음 4-A 원본의 manifest와 행 수를 확인하고 4-B exact dedup / 승인된 speaker 교정 overlay / conflict audit을 진행한다. 원본을 못 가져온 경우 R36의 실제 catalog 관찰로 derived supplement를 만드는 경로를 검토할 수 있으나, 새 파생물을 옛 4-A 원본이라고 부르거나 자동 approved로 만들지 않는다.

## 조회 출처

- `schema_adapter_report.md`, file ID `file_000000000d4481f8882ae77738fdc9a9`: 전체 69 source lines 조회.
- `VALIDATION_4A.json`, file ID `file_0000000056c08208a27604866e94927e`: summary/checks 조회.
- `source_extraction_audit.json`, file ID `file_00000000e3ac820ea837835052af5e21`: policy/summary 조회.
- `gc_names_layer_4A_checkpoint_v1.zip`, file ID `file_00000000948c81fab6ce0860b9bdc6f0`: Library 목록에서 확인.
- 전체 조회·복사 성공 여부: `audit/LIBRARY_RECOVERY_STATUS.json`.

새 프로젝트의 file IDs나 권한이 같다고 가정하지 않는다. 위 ID는 이번 회수의 출처 식별 기록이지 영구 다운로드 주소가 아니다.
