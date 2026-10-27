# LocalParity UI 사전 생성

`python tools/export_local_ui.py`는 한국어 authoring 데이터를 exact `translations/ko/ui.json`으로 내보낸다. `--check`는 생성 결과가 저장된 파일과 같은지 확인한다. 생성 뒤 `python tools/build_manifests.py translations/ko --modify ui.json`으로 package descriptor를 갱신한다.

## 포함 범위

- `ui/resources.json`: `disposition=translate`이고 비어 있지 않은 JP/KO 쌍을 모두 포함한다. binding의 enabled, kind, role, 과거 owner 분류는 포함 조건이 아니다.
- `master.json`: `mTextConfig.ml_value[]`, `mTutorialPopupDescriptions.ml_title[]`, `mAthenesRecordAreas.name`을 포함한다. 이 필드는 UI 문구/제목/지역 표시명이다. Novel 본문이나 임의 Master description 전체를 UI 사전으로 평탄화하지 않는다.
- `ui/general-exact.reviewed.json`: 실제 화면과 JP 의미를 검수한 명시적 exact 쌍이며 충돌 해소의 정본이다. 이 파일에 없는 다중 한국어 충돌은 실패한다.
- 동일한 외곽 rich-text 태그를 가진 JP/KO는 장식을 벗긴 plain exact도 생성한다. 기존 exact는 유지하며 모호한 alias는 실패한다.
- 공방 1~3은 검수된 정수 템플릿에서 offline exact로 생성한다.

`ui/local-coverage.json`은 `gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`의 UI **JP 키**만 기록한다. 중국어 번역을 가져오는 파일이 아니다. local의 일본어 키가 export에서 사라지면 실패한다. 숫자·기호·HP 등 언어 변환이 필요 없는 키만 keep_source로 허용한다. 새 upstream을 채택할 때 이 목록도 갱신하고 새 JP를 한국어로 검수한다.

Master 번역과 UI 번역은 독립적인 consumer다. Master에 같은 JP가 있어도 prefab/static label은 그 Master 레코드를 거치지 않을 수 있다. 이 때문에 UI 전용 필드를 **authoring 단계**에서 사전으로 복제한다. runtime에 Master lookup/reservation/ownership을 다시 넣지 않는다.

## 실제 화면에서 발견된 다른 경로

`UiTextLookup`는 exact를 먼저 적용하고, 확인된 완성 문구의 숫자/시간 패턴만 처리한다. `한국어 item + 수량 + 個で解放`, `한국어 title + の閲覧が必要`는 실기에서 확인한 두 형식이다. 입력 컨트롤과 self-write 재진입 보호를 유지한다. 임의 문자열 부분 치환이나 범용 template parser는 없다. Dictionary 키 존재만으로 모든 formatted UI가 표시된다고 주장하지 않는다.

아테네스 레코드의 여섯 지구 카드명은 `sprite_atlas/athenes_record_atlas.dmm`의 `BtArea01`~`BtArea06` 이미지에 포함된다. Master의 같은 지역명은 상세 화면에서 정상 번역된다. 이 이미지 이름은 Master/UI 사전 누락으로 세지 않으며 별도 이미지 교체 작업 대상으로 남긴다. 그림을 text exporter로 수정할 수 있다고 보고하지 않는다.

검증 명령:

```powershell
python -m unittest discover -s tests -p test_export_local_ui.py
dotnet run --project tests/UiTextLookup -- translations/ko/ui.json
```

source가 실재한다는 사실, 한국어 검수, exporter 포함, Native 표시를 구분해 기록한다. 과거 HOLD나 비활성 binding만으로 문구를 누락시키지 않는다.
