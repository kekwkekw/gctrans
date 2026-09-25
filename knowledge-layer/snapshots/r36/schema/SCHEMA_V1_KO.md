# 걸크리 한국어 Translation Knowledge Layer — schema v1

## 핵심 분리

- **entity**: 인물/장소/조직처럼 동일성을 유지하는 대상.
- **reference**: 별칭·축약명·호칭 표면형을 canonical entity에 연결.
- **term**: 지정 범위에서 번역 지식으로 쓰는 일본어 표현.
- **source_entry**: 실제 게임의 안정적인 텍스트 단위. 같은 일본어라도 문맥이 다르면 별 entry.
- **context_group**: 함께 읽어야 하는 텍스트 묶음.
- **character_profile**: 화법·자칭·주의점 등 캐릭터 번역 메타데이터.
- **relationship**: 인물/대상 관계와 호칭 관계. 근거와 범위를 항상 보존.
- **claim_guard**: 추측·거짓 정보·비유를 전역 설정으로 잘못 승격하지 않기 위한 보호 규칙.
- **policy**: 사용자 명시 정책과 아직 열려 있는 프로젝트 정책.

## category와 type

`type`은 "무엇인가", `category`는 "어디에서 쓰이는 텍스트인가"를 뜻한다.

예:
- `type=character`, `category=story`
- `type=system_term`, `category=master_name`
- `type=technique`, `category=story`

## 상태

- `candidate`: 후보
- `reviewing`: 근거는 있으나 아직 승인 전
- `approved`: 프로젝트가 채택한 값
- `deprecated`: 더 이상 쓰지 않음
- `conflict`: 복수 후보/정책이 충돌하여 판정 필요

`confidence`는 상태와 별개다. 근거가 강해도 정책 승인 전이면 `reviewing/high`일 수 있다.

## translation 필드

`translation`은 **승인된 한국어 값에만** 사용한다.
승인 전에는 `proposed_translation`/`observed_translation`에 저장한다.

R4에서 사용자가 명시적으로 채택한 `アルテ → 아르테`만 이번 migration에서 approved translation으로 승격한다.
`피오렌체`는 사용자 정책으로 보존하지만 R4에 일본어 source가 없으므로 term을 만들어 근거를 꾸미지 않는다.

## match

schema v1의 자동 적용 의미는 `exact`뿐이다.
substring replacement는 이 DB 자체가 의미하지 않는다.

## runtime과 분리

이 checkpoint는 authoritative knowledge layer의 초안이다.
GCMod, XUnity exact, `_Substitutions.txt` 등은 후속 compiler가 **approved + scope-safe subset**에서 생성한다.
이번 도구는 runtime 파일을 생성하지 않는다.
