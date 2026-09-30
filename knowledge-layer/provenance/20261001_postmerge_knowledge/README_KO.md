# 2026-10-01 Knowledge post-merge checkpoint

상태: **K01/K02 완료 / runtime 판정 보류 / pipeline baseline 미전진**

## K01
- 입력 Knowledge 후보: **57**
- 후보 간 중복을 4개 consolidation group으로 정리해 논리 단위 **51개**로 축약했다.
- 기존 R36 profile에 연결되는 후보는 기존 record를 덮어쓰지 않고 **reviewing extension target**으로 기록했다.
- 보스와 페르메르(얼터)는 신규 entity/profile overlay 후보로 분리했다.
- 신규 conflict: **0**. 기존 R36의 앵글/아테네스 시경 conflict는 그대로 유지한다.
- Knowledge global promotion: **0**.

## K02
- main에 병합된 신규 Names **16개**를 `user_approved=true`, `canonical_written=true`로 동기화했다.
- `ボシュ→보스`는 신규 entity overlay와 연결한다.
- `フェルメール（オルタ）→페르메르(얼터)`는 기존 페르메르와 분리된 variant entity overlay로 연결한다.
- 나머지 역할/일반인/고양이/composite speaker label은 안정적인 개체 신원을 임의 생성하지 않는다.
- R36의 과거 관찰값 `페르메이르`, `스킵피오`는 역사 evidence로 보존하고 실제 표시에는 Names canonical `페르메르`, `스키피오`를 사용한다.

## Runtime 경계
스모크 테스트에서 줄바꿈 압박이 관찰됐지만, 현재 한국어 폰트의 굵기/폭 영향 가능성이 커 번역 줄바꿈 문제의 성공/실패를 확정하지 않는다. 따라서 **runtime PASS/FAIL 모두 미확정**, **pipeline baseline 전진 금지** 상태다.

## 불변 사항
- `knowledge-layer/snapshots/r36`은 수정하지 않는다.
- 성인/보류 본문을 일반 Knowledge의 새 근거로 역수입하지 않는다.
- 이 checkpoint는 audit/provenance overlay이며 production Knowledge snapshot 승격이 아니다.

## Provenance
57개 원본 후보 데이터는 최종 PRO 패키지 `GC_20260930_FINAL_PRO_REVIEW_v1.zip`(SHA256 `de74ba1e...`)에서 읽었으며, repository에는 중복 원본을 다시 복제하지 않고 adjudication/checkpoint만 저장한다.