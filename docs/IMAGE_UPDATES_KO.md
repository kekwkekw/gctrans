# 이미지 데이터 갱신 (7.2.0 후보)

7.2.0 DLL을 한 번 설치해야 한다. 7.1.x DLL에는 이미지 교체 consumer가 없으므로
이미지 파일만 GitHub에 추가해도 적용되지 않는다. 후보의 실기 결과와 공개 릴리스 상태는 별도로 구분한다.

## 데이터와 표시 경로

`manifest.json.images`는 `images.json` 원본 UTF-8 바이트의 SHA-256이다.
카탈로그의 각 항목은 `kind`, `name`, `width`, `height`, `file`, `sha256`, `bytes`를 가진다.
PNG 파일명은 `images/<sha256>.png`이며 원본과 같은 크기여야 한다.
종류·이름·크기의 exact 조합이 표시 대상이다. 같은 이름과 크기가 다른 화면에도 쓰이는지
원본 자산을 확인한 뒤 등록한다. 경로나 URL을 임의 실행하는 기능은 없다.

시작 시 기존 번역 manifest 확인 뒤 변경된 카탈로그와 필요한 PNG만 내려받는다.
카탈로그 해시, PNG 해시·바이트 수·크기를 확인하고 모든 파일이 준비된 뒤 카탈로그를 교체한다.
실패하거나 시간이 초과되면 기존 카탈로그를 유지한다. 첫 설치에 이미지 캐시가 없으면 원본이 표시된다.
오프라인에서는 검증된 로컬 파일을 사용한다. 실행 중 원격 변경을 즉시 적용하지 않으며 재시작이 필요하다.

Unity UI callback에서 새 Texture2D/Sprite를 만들고 컴포넌트 참조만 바꾼다.
게임의 AssetBundle, 원본 공유 텍스처, 버튼 동작은 수정하지 않는다.
삭제는 카탈로그에서 항목을 제거한 뒤 새 manifest를 배포한다. 캐시에 남은 미참조 PNG는 표시하지 않는다.
전체 이미지 기능을 해제할 때도 `entries: []` 카탈로그를 배포한다.

## 지원 범위

- `UnityEngine.UI.Image`의 sprite/overrideSprite: 원본 rect 크기·pivot·border·pixelsPerUnit 유지.
- `UnityEngine.UI.RawImage`의 texture: 전체 UV `(0,0,1,1)`만 지원.
- PNG, 최대 4096×4096, 파일당 8 MiB, 카탈로그 최대 128항목/64 MiB.
- Sprite atlas 안의 개별 Sprite는 지원한다. 전체 atlas 교체, UV 부분 영역 RawImage,
  custom shader/mesh, 영상, Spine/Live2D 등의 지원은 주장하지 않는다.

지역 카드 파일은 `BtArea01`~`BtArea06`의 396×396 Sprite다.
`tools/build_region_images.py`는 추출한 원본과 한국어 폰트를 입력받아 지명 영역만 교체한다.
그 밖의 모든 RGBA 픽셀이 원본과 동일한지 검사하며 폰트 파일은 배포하지 않는다.
시작 화면·회상 로고도 위 UI 유형과 대상 식별 조건을 만족하면 같은 카탈로그로 확장할 수 있다.
각 로고의 실제 표시 유형과 교체 이미지는 별도 확인 대상이다.

## 반영 절차

1. PNG와 대상 매핑을 검수하고 `images.json`을 작성한다.
2. 새 PNG는 `--add images/<hash>.png`, 카탈로그 변경은 `--modify images.json`으로
   `python tools/build_manifests.py translations/ko`에 전달한다. 최초 카탈로그는 `--add images.json`이다.
3. `python tools/verify_local_parity.py`와 package build를 실행한다.
4. 승인된 GitHub ref에 카탈로그·PNG·생성된 manifest를 함께 반영하고 게임을 재시작한다.

Novel/Names/Knowledge/HOLD 정본은 이미지 작성 도구의 수정 대상이 아니다.
