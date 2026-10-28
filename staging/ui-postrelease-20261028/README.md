# 2026-10-28 팝업·리서치 UI 보완

- `ui.json`: 2,151 → 2,175 exact 항목(24 추가), `フロア` 1건을 `층` → `FLOOR`로 변경.
- 로그인 확인·시리얼 입력은 `popup/*.dmm`가 기본 UI 수집 범위에서 빠져 있었다. 실제 PC prefab에서 원문과 줄바꿈을 확인하고 이 경로를 기본 범위에 추가했다.
- `リサーチショップ`은 upstream/한국어 Master의 mShopCategories와 mShopTabs에 모두 존재한다. 실제 모달 제목은 코드 literal을 사용하여 Master를 우회하므로 UI exact를 추가했다.
- 초기화 날짜·로그인 일차·월별 구매 수량은 런타임 조합 문구다. 기존 exact 우선순위를 유지하고 이 유한 문구 형식만 처리한다. Native 화면의 1~7일차는 기존 7.1.0에서도 작동하는 exact로 제공한다.
- 단독 `フロア`만 FLOOR로 바꾼다. `フロアマップ`, `次のフロアへ`, `現在：{0}周目フロア{1}` 등 완성 문구는 기존 한국어를 유지한다. 영문 `FLOOR`/`FLOOR 4`는 원문 그대로 둔다.
- 시리얼 입력의 지정된 placeholder만 번역을 허용한다. 실제 textComponent와 나머지 입력 하위 객체는 계속 보호한다. 제출 기능/계정 데이터는 변경하지 않는다.
- Master 테이블별 성공 메시지만 Debug로 낮춘다. 콘솔 활성 설정, 시나리오 로드, 시작 시 데이터 업데이트, 경고·오류 로그는 그대로 유지한다. 과거 upstream 버전 확인은 기존처럼 기본 OFF이다.
- Master/Names/Novel/Knowledge/HOLD 변경 없음. 이번 대상은 기존 Master HOLD 3건과 겹치지 않는다. 이미지 배너는 후속 이미지 교체 범위로 남긴다.

## 적용 경계

기존 7.1.0도 재시작 시 공식 main에서 UI 데이터 추가와 FLOOR 변경을 받는다. 날짜/일차의 새 형식, placeholder 보호 개선, 로그 수준 변경에는 7.1.1 DLL이 필요하다. GitHub 변경만으로 설치된 DLL이 자동 교체되지는 않는다.

## 검증

기본 수집 범위 검사, deterministic export, 실제 C# UI lookup(2,175 exact/52 문구/24 negative), 전체 LocalParity 검사 및 DLL build를 수행한다. Native 자동 갱신/신규 DLL 결과는 실행 후 이 문서에 기록한다. 원본 캐시·PC metadata·개인 로그는 공개 저장소에 넣지 않는다.
