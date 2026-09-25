# 걸크리 Post-R36 Editorial Changeset v1.0 마감 보고서

기준일: 2026-09-23

## 1. 결론
사용자가 승인한 punctuation/editorial 정책을 실제 export 이전의 **안전 changeset overlay**로 고정했다.
실제 production/GCMod/GitHub에는 아직 적용하지 않았다.

- 편집 연산: **11,486건**
- 실제 영향 entry: **9,056개**
- Main M1 게시 RC1에 직접 바인딩된 entry: **2,952개**
- post-M1 hand span: **6,094개**
- post-M1 title extraction: **3개**
- mapped battle hand span: **7개**
- 기존 보류 12행 / source anomaly 13위치: **변경 0건**

## 2. 승인된 실제 연산 수
- 말줄임표 ASCII 점 묶음 → 원문 길이 보존 `…`: **10,489건**
- 원문상 발화 중단/전환 대시 복원: **2건**
- 구조 대시 → 원문 길이 보존 `―`: **963건**
- 서술 종결 마침표: **19건**
- 서술자 자문 물음표: **1건**
- 부호 결합을 끊는 공백 제거: **10건**
- 국소 내부 서술 경계 마침표: **1건**
- 잘린 전언 문장 연결: **1건**

발음 늘임·효과음 62회, source anomaly와 겹치는 부호, 정책상 보존 대상으로 판정된 공백 등은 changeset에서 제외했다.

## 3. preimage와 대상 계층
Main M1은 old observed에서 재생성하지 않았다. `gc_main_m1_rc1`의 실제 `runtime_key/runtime_value`와 deploy JSON value가 effective baseline과 일치하는지 확인한 뒤 changeset target을 묶었다.

Main M1 이후 범위는 현재 누적 원형의 source reader가 복원한 원래 hand span/title extraction을 target provenance로 기록했다. 실제 편집 offset은 사용자 승인 의미 교정 996건이 가상 반영된 **effective baseline SHA**에 고정했다.

따라서 export 시에는 반드시 최신 허가된 production value가 이 preimage와 호환되는지 다시 검사해야 한다. 이 changeset을 오래된 observed 파일에 직접 치환하는 방식은 허용하지 않는다.

## 4. 조립 검증
- changeset 자체 검사: **11/11 PASS**
- operation 수와 action별 집계 일치
- 모든 old token/span이 effective baseline에서 exact match
- 선택 구간 겹침 0
- HTML tag 내부 치환 0
- 변경 후 tag sequence 불변
- 보류/source anomaly 위치 변경 0
- 모든 영향 entry에 target binding 존재
- `export_authorized=false` 유지
- 별도 verifier: `audit/VERIFIER_FINAL.stdout.txt`

## 5. 권한 경계
이번 단계에서 확정된 것은 editorial **결정과 변경 명세**다.
아직 수행하거나 허가하지 않은 것: production export, GitHub commit/merge, GCMod 버전업, runtime 변경, 배포.

다음 단계는 최신 허가된 production baseline을 확인한 뒤 의미 996건과 editorial 11,486건을 한 번에 합성할 export/apply 패킷을 준비하는 것이다. 그 작업은 의미 재판정이 아니라 구조·적용 작업이므로 매우 높음으로 충분하다.
