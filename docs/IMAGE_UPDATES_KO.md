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

## 2026-10-28 대표 실기

- 새 DLL과 시험 CDN 설정만 설치했으며 UI/PNG는 수동 복사하지 않았다.
- 시작 시 UI 5,379 exact 및 지역 PNG 6개 다운로드·해시 검증 후 여섯 카드 KO 표시 확인.
- 프레임/투명 배경/캐릭터 합성, 상세 진입과 목록 복귀 확인.
- 일반 UI의 月·日→월·일, 필터 炎·雷→불·번개 확인.
- 사용자명 편집창에 시험 입력한 月은 그대로 표시. 취소 후 기존 사용자명 유지.
- accepted Novel 10010109의 전(前)관장 한자 보존과 본문·화자 표시 확인.
- 후속 UI exact 3개만 GitHub에 추가한 뒤 DLL·이미지 해시는 유지한 채 재시작하여 5,382개 사전 자동 갱신과 새 레어도/선택창 문구 KO 표시를 확인.
- Master/Names/Novel/Knowledge/HOLD 내용 변경 없음. 게임 재화 소비 없음.

이미지 hook 초기 시험에서 RawImage에 직접 선언되지 않은 OnEnable을 지정한 오류가 있었다.
실제 MaskableGraphic.OnEnable에 연결하고 RawImage로 한정하여 수정 후 정상 시작을 확인했다.
이번 교체 실기는 Sprite 6개에 한정하며 RawImage 교체 화면까지 검증했다는 뜻은 아니다.

확장 조사: 시작 로고 `opening_logo_01`/`opening_logo_r_01`은 816×312 Sprite이고
TitlePlatformImageChanger가 Unity UI Image를 사용한다. 회상 배너 표본 `nb0`는 340×128 Sprite다.
한국어 이미지 제작과 각 대상의 실제 적용 확인은 다음 단계이며 이번 시험에서 로고를 바꾸지 않았다.

## 시작/이벤트 로고 제작 기준

현재 이미지 시험은 사용자가 새로 제공한 `GirlsCreation_KO_MainLogo_RGBA.png`와
Codex 첫 번째 이벤트 시안을 사용한다. 앞선 두 번째 시안 선택을 대체한다.
시작 로고는 1536×652 RGBA 입력의 음영과 비율을 유지해 premultiplied-alpha Lanczos로
735×312로 축소한 뒤 원본 816×312 투명 캔버스 가운데 배치한다. 자르거나 획을 다시 그리지 않는다.
`opening_logo_r_01`만 대상으로 하며 일반판 로고는 포함하지 않는다.
`elg3335`의 「빛을 새기며」는 검수된 Master 제목과 같은 표기다. 첫 번째 생성 시안을
602×240으로 축소하고 x=20에 배치하여 원본 텍스처의 640×240 투명 캔버스를 복원한다.
이벤트 메인 `EventQuestView._logoImage`는 RawImage이므로 `kind: texture`로 등록한다.
같은 번들에 Sprite도 존재하지만 실제 표시 컴포넌트의 유형을 대신 판단할 근거는 아니다.
회상 목록의 캐릭터 포함 배너는 별도 자산이며 이 단독 로고 항목의 교체 대상이 아니다.
추출기의 Sprite.image는 잘린 textureRect 크기를 반환할 수 있으므로, 대상 크기는 반드시
원본 Sprite.rect로 확인하고 textureRectOffset에 해당하는 여백을 보존한다.

향후 이벤트 로고는 회상 탭의 작은 정보용 표시 크기에서 읽기 쉬운 제목을 우선한다.
원본의 색상·프레임·장식·배치를 최대한 살리되, 축소 후 획이 뭉치거나 부제 때문에
제목이 작아지지 않게 제작한다. 큰 원본뿐 아니라 실제 작은 화면에서 여백·대비·잘림을 확인한다.
생성형 재현으로 원본 픽셀이 달라졌다면 이를 원본 보존으로 기록하지 않는다.

## 2026-10-29 선택 로고 실기

- 기존 7.2.0 DLL을 유지하고 시험 GitHub ref에서 이미지 카탈로그와 PNG만 자동 갱신했다.
- R판 시작 로고: `sprite:opening_logo_r_01:816x312`, 영상 배경 위 투명 합성과 잘림 없음 확인.
- 이벤트 메인 로고: `texture:elg3335:640x240`, 「빛을 새기며」 표시와 스토리 메뉴 복귀 확인.
- 최초 Sprite 매핑은 이 화면과 맞지 않았다. 원본 캔버스와 실제 RawImage consumer를 확인하여
  데이터만 수정했으며 runtime 소스 및 DLL 변경은 없다.
- 회상 37페이지의 캐릭터 포함 배너, 홈 배너, 퀘스트 선택 카드는 별도 합성 이미지다.
  이번 단독 로고 교체로 이 배너들까지 번역되었다고 판정하지 않는다.
- UI/Master/Names/Novel 본문 변경 없음. AP/BP/코인/예정석 소비 없음.

## 회상 이벤트 배너 첫 제작 묶음

위의 단독 로고와 별도로 최신 회상 페이지의 340×128 배너 네 개를 작성했다.
`Gc.Novel.CategoryScrollCell.banner`는 RawImage이며 `image_small_banner`의 원본 Texture2D를 사용한다.
따라서 다음 항목은 모두 `kind: texture`로 등록한다. 제목은 accepted `mEvents.ml_title[]`를 따른다.

| 대상 | 한국어 제목 |
| --- | --- |
| ebs13240 | 고양이와 간호사와 몬스터 |
| ebs13612 | 고결함과 금화 (기존 Another Side 표제 유지) |
| ebs15017 | 흡혈 기사 스키피오는 잠들지 않는다 (기존 Precious Connections 유지) |
| ebs13335 | 빛을 새기며 |

제작은 built-in image_gen의 원본 참조 편집 후 코드 합성으로 마무리한다.
생성된 그림 전체를 채택하지 않고 지정한 제목 영역만 원본에 합성한다. 해당 영역 밖 RGBA와
전체 alpha가 원본과 같은지 검사하므로 테두리와 캐릭터 대부분을 다시 그리지 않는다.
제목 영역 안의 장식/배경은 생성형 재현이므로 원본 픽셀 보존이라고 주장하지 않는다.
340×128 원본 크기와 실제 목록에 가까운 180×68 축소 크기에서 철자·대비·잔존 JP를 확인한다.

네 배너만 첫 묶음으로 등록했으며 전체 회상 배너 완성을 의미하지 않는다.
후속 묶음도 원본 자산·accepted 제목·실제 표시 유형을 확인하고 같은 절차로 추가한다.
현재 카탈로그에는 12개 대상이 있고 상한은 128개다. 전체 회상 확대 전에 이 상한은 별도 검토한다.
