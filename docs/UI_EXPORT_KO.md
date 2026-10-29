# LocalParity UI 사전 생성

`python tools/export_local_ui.py`는 한국어 authoring 데이터를 exact `translations/ko/ui.json`으로 내보낸다. `--check`는 생성 결과가 저장된 파일과 같은지 확인한다. 생성 뒤 `python tools/build_manifests.py translations/ko --modify ui.json`으로 package descriptor를 갱신한다.

## 포함 범위

- `ui/resources.json`: `disposition=translate`이고 비어 있지 않은 JP/KO 쌍을 모두 포함한다. binding의 enabled, kind, role, 과거 owner 분류는 포함 조건이 아니다.
- `master.json`: `mTextConfig.ml_value[]`, `mTutorialPopupDescriptions.ml_title[]`, `mAthenesRecordAreas.name`, `mVirtueEffects.ml_name[]`을 포함한다. 이 필드는 UI 문구/제목/지역명/예상 이름이다. 자동 편성·예상 공명의 static 복사본도 같은 한국어를 사용한다. Novel 본문이나 임의 Master description 전체를 UI 사전으로 평탄화하지 않는다.
- `ui/general-exact.reviewed.json`: 실제 화면과 JP 의미를 검수한 명시적 exact 쌍이며 충돌 해소의 정본이다. 이 파일에 없는 다중 한국어 충돌은 실패한다.
- 동일한 외곽 rich-text 태그를 가진 JP/KO는 장식을 벗긴 plain exact도 생성한다. 기존 exact는 유지하며 모호한 alias는 실패한다.
- 공방 1~3은 검수된 정수 템플릿에서 offline exact로 생성한다.
- `ui/enhancement-families.json`: 실기·PC producer에서 확인한 레벨, 슬롯, 부족 수량, 확률, 경감 수치의 유한 정수 집합을 exact로 생성한다. 허용 범위 밖의 값을 번역 완료로 세지 않는다. 빈 목록·중복·충돌·잘못된 변수는 실패한다.

`ui/local-coverage.json`은 `gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`의 UI **JP 키**만 기록한다. 중국어 번역을 가져오는 파일이 아니다. local의 일본어 키가 export에서 사라지면 실패한다. 숫자·기호·HP 등 언어 변환이 필요 없는 키만 keep_source로 허용한다. 새 upstream을 채택할 때 이 목록도 갱신하고 새 JP를 한국어로 검수한다.

Master 번역과 UI 번역은 독립적인 consumer다. Master에 같은 JP가 있어도 prefab/static label은 그 Master 레코드를 거치지 않을 수 있다. 이 때문에 UI 전용 필드를 **authoring 단계**에서 사전으로 복제한다. runtime에 Master lookup/reservation/ownership을 다시 넣지 않는다.

## 실제 화면에서 발견된 다른 경로

`UiTextLookup`는 exact를 먼저 적용하고, 확인된 완성 문구의 숫자/시간 패턴만 처리한다. `한국어 item + 수량 + 個で解放`, `한국어 title + の閲覧が必要`는 실기에서 확인한 두 형식이다. 입력 컨트롤과 self-write 재진입 보호를 유지한다. 임의 문자열 부분 치환이나 범용 template parser는 없다. Dictionary 키 존재만으로 모든 formatted UI가 표시된다고 주장하지 않는다.

아테네스 레코드의 여섯 지구 카드명은 `sprite_atlas/athenes_record_atlas.dmm`의 `BtArea01`~`BtArea06` 이미지에 포함된다. Master의 같은 지역명은 상세 화면에서 정상 번역된다. 이 이미지 이름은 Master/UI 사전 누락으로 세지 않으며 7.2.0 후보의 이미지 카탈로그로 별도 처리한다. [이미지 갱신](IMAGE_UPDATES_KO.md)을 참조한다. 그림을 text exporter로 수정할 수 있다고 보고하지 않는다.

검증 명령:

```powershell
python -m unittest discover -s tests -p test_export_local_ui.py
dotnet run --project tests/UiTextLookup -- translations/ko/ui.json
```

source가 실재한다는 사실, 한국어 검수, exporter 포함, Native 표시를 구분해 기록한다. 과거 HOLD나 비활성 binding만으로 문구를 누락시키지 않는다.

## 강화·편성 후속 조사

`scene/*.dmm`와 `popup/*.dmm`는 기존 수집 범위에 포함된다. 실제 메뉴를 열기 전 캐시에 없었던 asset의 `NOT_CACHED`는 조사 완료가 아니다. 해당 메뉴를 연 뒤 다시 취득하고, 정상 해석된 asset만 완료로 센다. 확인·결과·실패 토스트·필터 아래쪽은 별도 화면 상태로 기록한다. 코드가 조합하는 문구는 PC 상수와 실제 완성 문자열을 함께 확인한다.

7.2.1의 편성 복사 확인문만 작은 전용 runtime 규칙을 사용한다. `編成の内容を<color=#...>팀명</color>に上書きします` 전체 형태에서 팀명과 색상 태그를 그대로 보존하고, `編成の内容を{0}に上書きします`의 검수 번역을 적용한다. 사용자 팀명을 사전에 수집하거나 부분 치환하지 않는다. 일반 exact 데이터 보완은 7.2.0에도 전달되지만 이 문장 처리는 7.2.1 DLL이 필요하다. Novel 렌더러와 실제 사용자 입력 보호는 유지한다.

## 점수 도전·퀘스트 후속 조사

점수 도전은 준비 화면에서 끝내지 않는다. 보너스 목록의 여덟 설명, 준비 화면과 목록 안의 세 종류 대상 돋보기, 포텐셜 상세, 점수·퀘스트 보상, 레벨 부스트 +/−, 실제 전투의 결과 상세와 보상 수령 화면을 구분해 확인한다. 모든 버튼을 눌렀다는 사실과 각 화면의 한국어 표시를 따로 기록한다. 현재 7.2.1 DLL을 바꾸지 않고 이 경로의 누락을 UI 데이터로 보완할 수 있다.

`gc-mod-local`의 UI 키가 모두 있다고 해서 현재 PC UI가 모두 수집됐다는 뜻은 아니다. `スコアチャレンジ`처럼 Master에 번역이 있어도 다른 static label은 UI exact가 필요하다. 이번 조사에서는 해당 popup/scene prefab과 현재 PC 상수를 좁게 대조하고, 실제 결과 화면에서만 확인된 문자열도 reviewed exact에 추가했다. 새 Master 권한 계층이나 부분 문자열 치환은 추가하지 않는다.

박스의 현재 번호·이후 구간, 도전 난이도·턴 수는 기존 offline 정수 family로 생성한다. 별 개수 뒤 `のメモリー`는 조사 중 `の` 앞뿐 아니라 뒤에도 공백이 있는 실제 문자열이 발견되어, 별 1~5개와 양쪽의 없음/반각/전각 공백을 완성 exact로 다룬다. 고정 S/I/P 이미지가 문장 안에 있는 대상 안내는 기존 배치에 맞는 공백을 번역문에 보존한다. `で解放`는 별 이미지 옆 독립 TextNote 컴포넌트의 전체 값으로만 번역하며 다른 문장 안에서는 치환하지 않는다.

기록은 `staging/ui-quest-score-20261029/`에 둔다. 유한 범위 밖 숫자, 검증하지 않은 방어전 결과 화면, 이미지에 포함된 글자는 이번 실기 통과 범위로 세지 않는다. Names·Novel·Knowledge·기존 세 Master HOLD는 변경하지 않는다.
