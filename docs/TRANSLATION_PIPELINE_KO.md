# Master·UI → GCMod 한국어 업데이트 배포 준비

새 plan은 current Names/Knowledge 문맥을 포함한다. review의 `knowledge_review`에 신규 용어·중복·충돌 검토 결과를 적는다(신규 후보 없음도 허용). 이 경로의 소유 파일은 계속 Master/UI와 manifest뿐이다. Names/Knowledge 갱신이나 기존 번역값 교정은 [corpus 교정 절차](CORPUS_WORKFLOW_KO.md)에서 명시적으로 검수한다. 문맥 필드가 없는 기존 plan은 이전 계약으로 읽는다.

Novel의 기존 운영 절차는 [OPERATOR_KO.md](OPERATOR_KO.md)를 유지한다. 아래 명령은 저장소 루트에서 실행한다. `work/translation-update`는 Git 제외 작업 폴더이며 크롤러 raw, state, 복호화 helper, 게임 참조 DLL은 공개 저장소에 넣지 않는다.

## 1. 수집

Python 3.12 이상, `httpx==0.27.2`, `UnityPy==1.10.18`과 기존 private helper가 필요하다. Master는 기존 복호화 경계를 사용한다. UI prefab 해석 자체에는 복호화 helper가 필요 없으며 HTTP 취득 인증에 기존 helper를 사용한다.

```powershell
python scripts/crawl_only.py --lane Master --acquire --master-coverage-baseline <local-master.json> --master-baseline-ref <upstream-commit> --output work/translation-update/master-scan.json
python scripts/crawl_only.py --lane UI --acquire --output work/translation-update/ui-scan.json
```

- Master 범위는 기존 field selector, 현재 한국어 Master, 지정한 Master 기준본의 `(table, field)` 합집합이다. 기준본에서는 원문 키만 사용한다.
- UI는 `contracts/ui-source-scope-v1.json`에 따라 scene·popup·공통 prefab·개별 prefab·동적 콘텐츠 prefab을 선택한다. 로그인 보너스·시리얼 입력 같은 별도 팝업도 포함한다. 선택 수량은 현재 inventory에 따라 달라지며, 선택되었다고 해석·번역이 완료된 것은 아니다.
- `--asset-key scene/scene_sub_unit.dmm`로 좁혀 재현하거나 `--ui-scope <json>`으로 범위를 조정할 수 있다. PC는 기존처럼 `--platform PC --local-asset <file> --asset-key <known-key>`를 지정한다. WebGL 캐시를 PC 관측으로 재표기하지 않는다.
- UI `--max-candidates 200`은 배치 크기다. 남은 항목을 pending으로 저장하며 같은 state/raw root와 명령으로 계속 수집한다. `0`은 명시적 무제한이다. Novel/Master의 기존 candidate 제한 동작은 유지한다.
- UI 객체 제한 기본값은 asset당 250,000개다. 일부 component 해석 실패는 성공한 텍스트를 버리지 않고 `ui_diagnostics.failed_components`에 남긴다. 객체 제한 초과·손상 asset·충돌은 해당 asset만 미수집으로 기록한다.
- `m_text`/`m_Text`는 비활성 객체도 수집한다. 이미지 픽셀은 별도 범위로 기록하며 OCR이나 번역 완료를 주장하지 않는다. 사용자 입력·동적 완성 문구는 Native 관측으로 보완한다.
- 메타데이터만 먼저 본 뒤 `--acquire`를 실행해도 최초 본문 취득을 수행한다. 동일 bytes/decoder의 재실행은 저장된 결과를 재사용한다. byte 재확인은 `--integrity`로 요청한다.
- 일부 asset 실패 시 크롤러는 종료 코드 2와 결과 JSON을 남긴다. 아래 plan은 그 JSON의 정상·현재 materialization만 사용할 수 있다. 상태가 불명확한 원문을 조용히 성공으로 세지 않는다.

## 2. 후보·검수·적용

```powershell
python tools/update/translation_update.py plan --baseline <local-master.json> --baseline-ref <upstream-commit> --scan work/translation-update/master-scan.json --scan work/translation-update/ui-scan.json --probe-runtime --output work/translation-update/batch.json
python tools/update/translation_update.py review-template --batch work/translation-update/batch.json --output work/translation-update/review.json
```

`--probe-runtime`은 .NET 9 SDK로 실제 `UiTextLookup.cs`를 실행한다. 이미 처리되는 `5ターン`, `あと1000` 같은 동적 문자열을 새 누락으로 세지 않는다. 이 옵션을 생략하면 exact 사전 대비 후보이므로 동적 중복 가능성을 보고 검수한다.

review의 각 행은 `pending`, `translate`, `hold` 중 하나다. `translate`에는 `ko`, `reviewer`, 의미·용어 판단의 `evidence`를 적는다. 현재 Names/Knowledge와 JP를 사용하며 기존 한국어 exact는 제안으로만 제공한다. HOLD에는 구체적 `reason`과 `evidence`가 필요하다. 단순히 source에 남았다는 이유는 HOLD 근거가 아니다. 전 항목 처리 전에도 검수된 행만 먼저 적용할 수 있고, pending/HOLD는 적용 결과에 그대로 남는다.

```powershell
python tools/update/translation_update.py apply --batch work/translation-update/batch.json --review work/translation-update/review.json --output work/translation-update/applied.json
```

apply는 임시 복사본에서 Master/UI 추가, 결정적 UI export, manifest 갱신을 끝낸 후 실제 package에 반영한다. 기존 번역 덮어쓰기·삭제는 하지 않는다. 실패 시 이전 bytes로 복구한다. 오래된 batch, 중복 결정, 숫자·보호 변수·태그 손실은 오류다. 전각/반각 숫자는 동등하게 비교하고 줄바꿈·일반 괄호 차이는 Native 확인 경고로 남긴다. 실제 의미검수를 자동 승인하지 않는다.

부분 적용 후에는 변경된 package로 새 plan을 만들고 남은 후보를 이어간다. 이전 `applied.json`의 HOLD 근거도 보관한다. 이 도구는 Names나 Novel 본문을 쓰지 않는다. 기존 mNovels 제목 동기화 도구는 accepted Novel을 기준으로 별도 실행한다.

Native 관측은 `--observations <json>`으로 추가할 수 있다. 형식은 `{"schema":"gc-native-ui-observations-v1","rows":[{"jp":"실제 JP 전체 문구","evidence":"화면/문맥/관측 근거"}]}`다. 화면에 없는 문장을 관측했다고 기록하지 않는다.

게임 코드의 문자열 상수는 prefab `m_text`/`m_Text` 수집에 포함되지 않는다. 주간 퀘스트 탭·하단 설명과 Novel 건너뛰기 안내가 이 경우였다. 이때는 화면에서 확인한 문구와, 설치된 PC 코드에서 정확한 원문을 확인한 같은 메뉴의 유한한 형제 문구를 observations로 전달할 수 있다. evidence에 화면 관측과 코드 원문 확인을 구분하고 근거·버전 hash를 남긴다. 검수 후 동일한 apply/export 경로로 exact 사전에 추가하며, 임의 부분 문자열 치환이나 Master 전체의 UI 복사를 도입하지 않는다.

## 3. 빌드·패키지·설치

### PC 코드 상수와 상세 메뉴 보완

prefab 범위에 포함되어도 코드가 나중에 넣는 필터 선택지·팝업 문구는 별도 후보가 필요하다. 다음 도구는 설치된 PC v39 메타데이터의 지정한 문자열 계열만 읽고, 원본 hash와 literal index를 남긴다. 전체 캐시 재색인이나 자동 번역은 하지 않는다.

```powershell
python tools/collect_ui_literals.py --metadata <game>/GC_Data/il2cpp_data/Metadata/global-metadata.dat --contains メモリー --contains スター --output work/translation-update/detail-literals.json
```

결과를 앞의 plan에 `--observations work/translation-update/detail-literals.json`으로 전달한다. `source_kind=pc_metadata_literals`는 실제 화면 관측과 구분하며 표시 여부·문맥을 검수한다. 입력창 편집 문자는 번역하지 않는다. 집무실처럼 Master와 같은 JP를 코드에서 직접 공급하는 경우는 해당 문구만 accepted Master 번역과 맞춰 UI에 추가한다.

전체 누락 점검에서는 키워드를 미리 아는 문구만 찾지 않도록 명시적인 전체 일본어 문자열 수집을 사용한다.

```powershell
python tools/collect_ui_literals.py --metadata <game>/GC_Data/il2cpp_data/Metadata/global-metadata.dat --all-japanese --output work/translation-update/pc-literal-inventory.json
```

이 옵션은 현재 v39의 문자열 상수 테이블에서 일본어로 보이는 문자(가나·한자)가 있는 모든 값을 수집한다. 개발 로그·문장 조각·Unicode/locale 문자열도 수집 단계에서 버리지 않는다. 원본 hash와 literal index를 보존한 뒤 기존 UI 조회, Master/Names/현재 Novel의 번역 재사용, 코드 소비 경로를 대조해 분류한다. `--contains`와 함께 쓸 수 없으며 선택 옵션 없는 암묵적 전체 수집은 허용하지 않는다. 전체 결과를 번역 승인본이나 실제 표시 확인으로 취급하지 않는다.

coverage는 **asset의 Master/직렬화 UI**, **PC 코드 문자열**, **실행 중 조합된 최종 문구**, **이미지·외부/서버 공급 문자열**의 범위를 구분해 보고한다. 전체 asset 취득 완료나 수집 후보 누락 0은 실행 코드와 최종 화면 문구의 완전성을 뜻하지 않는다. 코드의 완성된 표시 문구는 사진이 없다는 이유만으로 제외하지 않고, 소비 경로를 근거로 검수 후보에 포함할 수 있다. 고정 문구는 기존 apply/export, 서식·조각은 최종값 지원 점검을 이어간다. [992개 분류 및 PRO 승인본 반영 사례](PC_UI_LITERAL_REVIEW_20261102_KO.md)를 참조한다.

`ui/detail-families.json`은 확인된 메모리 4그룹×5세트, 유효한 생일 확인 날짜, 현재 Master의 메인 스테이지 범위 안내를 완성된 exact로 export한다. JP 및 이미 KO로 바뀐 Master 이름을 모두 처리하며 새 장이 Master에 들어오면 export 시 따라간다. 범용 부분 치환이나 DLL template engine은 추가하지 않는다. 7.2.0 후보는 한 글자 `炎`·`雷`를 포함한 일반 UI exact를 허용하고 시나리오 렌더러와 사용자 입력을 보호한다. 사전에 키가 있다고 실기 표시 완료로 세지 않는다.

`ui/ap-families.json`은 AP 화면의 두 시간 문구 `00:00~12:00`과 AP 회복량 0~150을 같은 export에서 완성된 exact로 생성한다. 현재 `ui.templates.json`·검수된 UI 샘플·Master 서식의 한국어를 그대로 재사용하고, 다른 번역과 충돌하면 실패한다. 적용 후 기존 `build_manifests.py --modify ui.json`으로 데이터 manifest를 갱신한다. authoring 범위 설정은 게임이 읽는 파일이 아니며, 생성된 `ui.json`을 기존 DLL이 소비한다. [세부 근거와 한계](UI_AP_EXACT_20261101_KO.md)를 따른다.

```powershell
python tools/local_release.py build --game <game-directory>
python tools/local_release.py verify
python tools/local_release.py pack --output work/translation-update/update.zip
python tools/local_release.py verify-archive --archive work/translation-update/update.zip
```

DLL은 `source/GCMod.Candidate`에서 `candidate/GCMod.dll`로 Release 빌드한다. 기존 BepInEx/interop/Utility 참조가 필요하며 build는 게임을 수정하지 않는다. GCMod 한국어 런타임은 검수된 번역 데이터와 필요한 화면별 예외를 적용한다. 수집·검수·패키징은 별도 도구에서 수행한다.

기본 archive는 DLL·cfg·한국어 runtime JSON만 공급하며 게임 본체/interop/Utility/폰트를 제외한다. 개인 설치 묶음이 필요할 때만 `--font <approved-bundle> --utility <Utility.dll>`을 함께 지정한다. 이 묶음은 공개 배포하지 않는다.

게임 종료 후:

```powershell
python tools/local_release.py install --archive work/translation-update/update.zip --game <game-directory> --backup <new-backup-directory-outside-game>
```

설치 전 archive membership·hash·등록된 Novel 존재를 확인하고, 변경 파일의 원본 backup/journal을 만든다.
**기본 업데이트는 기존 cfg의 bytes를 그대로 보존한다.** 불투명도·F5·FPS뿐 아니라 번역 활성화·언어·CDN/offline·사용자 경로·비동기 로딩·Master 테이블·폰트 경로·주석·알 수 없는 키도 보존한다.
cfg가 없을 때만 패키지 기본 cfg를 설치하고, 기존 cfg에 없는 항목은 게임 시작 시 BepInEx가 runtime 기본값으로 바인딩한다.
실행 중 게임에는 설치하지 않는다. 설치 실패는 설치 도구가 변경한 파일을 기존 bytes로 복구한다.

한국어 기본 프로필로 전환하려는 경우에만 위 install 명령에 `--apply-korean-profile`을 추가한다.
이 옵션은 `Translation`, `Translation.MasterData`, `Translation.Font`의 패키지 키를 기본값으로 적용한다.
CDN은 공식 main, 사용자 번역 경로는 빈 값, 비동기 로딩은 false, 언어는 ko, 번역 활성화는 true,
Master는 `*`, 폰트는 패키지 기본 경로로 바뀐다. 해당 섹션의 알 수 없는 키와 일반·대화 설정값은 보존한다.
명시적 프로필 전환에서는 cfg를 재직렬화하므로 주석·서식은 유지되지 않으며 원래 bytes는 백업에서 복구할 수 있다.

공개 업데이트의 폰트 선행 조건은 적용 후 cfg의 실제 경로를 확인한다. 상대 경로는 BepInEx/plugins 기준이고,
절대 경로도 허용하지만 해당 파일을 읽기 확인만 하며 사용자 경로에 파일을 설치하지 않는다.
ZIP을 수동으로 적용할 때도 기존 cfg를 덮어쓰지 않는다.

```powershell
python tools/local_release.py rollback --game <game-directory> --backup <same-backup-directory>
```

설치 도구가 변경한 파일을 사용자가 다시 변경했으면 rollback은 덮어쓰지 않고 충돌을 알린다.
기본 업데이트에서 보존한 기존 cfg는 복구 대상에 포함되지 않아, 업데이트 후 사용자가 바꾼 설정도 유지된다.
새 cfg 설치 또는 명시적 프로필 전환으로 cfg를 변경했다면 cfg도 복구·충돌 검사 대상이다.
실제 실행은 사용자가 제공한 기존 DMM 런처를 따른다. 시작 로그, 대표 Master/static UI, 시나리오 제목·본문·화자와 변경한 화면만 확인한다. 기존 `EnabledTables=*`를 좁은 smoke 목록으로 바꾸지 않는다.

## 4. 커밋 준비

`.github/workflows/translation_update.yml`은 CPU 회귀·package 검사만 한다. 게임 참조가 필요한 DLL 빌드와 Native는 로컬에서 별도 확인하며 CI가 Native PASS를 만들지 않는다. `tools/verify_translation_update.py`로 같은 검사를 실행할 수 있다.

검증된 source/data/DLL과 익명화된 요약만 stage한다. raw/state/helper/폰트/게임 DLL/로그/개인 경로는 제외한다. commit/push/PR/release는 이 도구들에서 수행하지 않는다. 실제 Git 상태와 Native 검증 범위를 최종 보고한다.
