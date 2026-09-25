# 걸크리 Post-R36 Editorial Changeset v1.0

이 패키지는 사용자가 승인한 punctuation/editorial 정책을 **실행 가능한 변경 명세로 고정한 동반 체크포인트**다. 실제 production/export/GitHub/GCMod에는 적용하지 않았다.

## 확정 수량
- editorial operation: **11,486건**
- 실제 영향 entry: **9,056개**
- 말줄임표 glyph 정규화: 10,489건
- 원문 대시로 복원하는 중단/전환: 2건
- 구조 대시 정규화: 963건
- 서술 종결: 마침표 19건 + 물음표 1건
- 결합 공백 제거: 10건
- 국소 문장 경계: 2건
- 발음 늘임/효과음, 보류12, source anomaly13 등 보호 대상은 changeset에 넣지 않았다.

## 대상 바인딩
Main M1은 과거 observed가 아니라 **게시 RC1 runtime key/value**에 묶었다. 이후 범위는 authoritative source reader가 복원한 **원래 hand span/title extraction**에 묶고, 실제 치환 offset은 사용자 승인 의미 교정이 가상 적용된 effective baseline SHA에 고정했다.

따라서 이 패키지를 오래된 source/observed에 곧바로 덮어쓰면 안 된다. export 단계에서 최신 허가된 production preimage를 다시 확인해야 한다.

## 권한
editorial 정책은 사용자 승인됨. **export/commit/GCMod/runtime/deploy는 아직 미허가**.
