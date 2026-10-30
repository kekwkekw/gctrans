# Master·UI → LocalParity 배포 준비

새 plan은 current Names/Knowledge 문맥을 포함한다. review의 `knowledge_review`에 신규 용어·중복·충돌 검토 결과를 적는다(신규 후보 없음도 허용). 이 경로의 소유 파일은 계속 Master/UI와 manifest뿐이다. Names/Knowledge 갱신이나 기존 번역값 교정은 [corpus 교정 절차](CORPUS_WORKFLOW_KO.md)에서 명시적으로 검수한다. 문맥 필드가 없는 기존 plan은 이전 계약으로 읽는다.

Novel의 기존 운영 절차는 [OPERATOR_KO.md](OPERATOR_KO.md)를 유지한다. 아래 명령은 저장소 루트에서 실행한다. `work/local-parity`는 Git 제외 작업 폴더이며 크롤러 raw, state, 복호화 helper, 게임 참조 DLL은 공개 저장소에 넣지 않는다.

## 1. 수집

Python 3.12 이상, `httpx==0.27.2`, `UnityPy==1.10.18`과 기존 private helper가 필요하다. Master는 기존 복호화 경계를 사용한다. UI prefab 해석 자체에는 복호화 helper가 필요 없으며 HTTP 취득 인증에 기존 helper를 사용한다.

```powershell
python scripts/crawl_only.py --lane Master --acquire --master-coverage-baseline <local-master.json> --master-baseline-ref <upstream-commit> --output work/local-parity/master-scan.json
python scripts/crawl_only.py --lane UI --acquire --output work/local-parity/ui-scan.json
```

- Master 범위는 기존 field selector, 현재 한국어 Master, 지정한 local Master의 `(table, field)` 합집합이다. local의 중국어 값은 한국어 생성 입력이 아니다.
- UI는 `contracts/ui-source-scope-v1.json`에 따라 scene·popup·공통 prefab·개별 prefab·동적 콘텐츠 prefab을 선택한다. 로그인 보너스·시리얼 입력 같은 별도 팝업도 포함한다. 선택 수량은 현재 inventory에 따라 달라지며, 선택되었다고 해석·번역이 완료된 것은 아니다.
- `--asset-key scene/scene_sub_unit.dmm`로 좁혀 재현하거나 `--ui-scope <json>`으로 범위를 조정할 수 있다. PC는 기존처럼 `--platform PC --local-asset <file> --asset-key <known-key>`를 지정한다. WebGL 캐시를 PC 관측으로 재표기하지 않는다.
- UI `--max-candidates 200`은 배치 크기다. 남은 항목을 pending으로 저장하며 같은 state/raw root와 명령으로 계속 수집한다. `0`은 명시적 무제한이다. Novel/Master의 기존 candidate 제한 동작은 유지한다.
- UI 객체 제한 기본값은 asset당 250,000개다. 일부 component 해석 실패는 성공한 텍스트를 버리지 않고 `ui_diagnostics.failed_components`에 남긴다. 객체 제한 초과·손상 asset·충돌은 해당 asset만 미수집으로 기록한다.
- `m_text`/`m_Text`는 비활성 객체도 수집한다. 이미지 픽셀은 별도 범위로 기록하며 OCR이나 번역 완료를 주장하지 않는다. 사용자 입력·동적 완성 문구는 Native 관측으로 보완한다.
- 메타데이터만 먼저 본 뒤 `--acquire`를 실행해도 최초 본문 취득을 수행한다. 동일 bytes/decoder의 재실행은 저장된 결과를 재사용한다. byte 재확인은 `--integrity`로 요청한다.
- 일부 asset 실패 시 크롤러는 종료 코드 2와 결과 JSON을 남긴다. 아래 plan은 그 JSON의 정상·현재 materialization만 사용할 수 있다. 상태가 불명확한 원문을 조용히 성공으로 세지 않는다.

## 2. 후보·검수·적용

```powershell
python tools/update/local_parity.py plan --baseline <local-master.json> --baseline-ref <upstream-commit> --scan work/local-parity/master-scan.json --scan work/local-parity/ui-scan.json --probe-runtime --output work/local-parity/batch.json
python tools/update/local_parity.py review-template --batch work/local-parity/batch.json --output work/local-parity/review.json
```

`--probe-runtime`은 .NET 9 SDK로 실제 `UiTextLookup.cs`를 실행한다. 이미 처리되는 `5ターン`, `あと1000` 같은 동적 문자열을 새 누락으로 세지 않는다. 이 옵션을 생략하면 exact 사전 대비 후보이므로 동적 중복 가능성을 보고 검수한다.

review의 각 행은 `pending`, `translate`, `hold` 중 하나다. `translate`에는 `ko`, `reviewer`, 의미·용어 판단의 `evidence`를 적는다. 현재 Names/Knowledge와 JP를 사용하며 기존 한국어 exact는 제안으로만 제공한다. HOLD에는 구체적 `reason`과 `evidence`가 필요하다. 단순히 source에 남았다는 이유는 HOLD 근거가 아니다. 전 항목 처리 전에도 검수된 행만 먼저 적용할 수 있고, pending/HOLD는 적용 결과에 그대로 남는다.

```powershell
python tools/update/local_parity.py apply --batch work/local-parity/batch.json --review work/local-parity/review.json --output work/local-parity/applied.json
```

apply는 임시 복사본에서 Master/UI 추가, 결정적 UI export, manifest 갱신을 끝낸 후 실제 package에 반영한다. 기존 번역 덮어쓰기·삭제는 하지 않는다. 실패 시 이전 bytes로 복구한다. 오래된 batch, 중복 결정, 숫자·보호 변수·태그 손실은 오류다. 전각/반각 숫자는 동등하게 비교하고 줄바꿈·일반 괄호 차이는 Native 확인 경고로 남긴다. 실제 의미검수를 자동 승인하지 않는다.

부분 적용 후에는 변경된 package로 새 plan을 만들고 남은 후보를 이어간다. 이전 `applied.json`의 HOLD 근거도 보관한다. 이 도구는 Names나 Novel 본문을 쓰지 않는다. 기존 mNovels 제목 동기화 도구는 accepted Novel을 기준으로 별도 실행한다.

Native 관측은 `--observations <json>`으로 추가할 수 있다. 형식은 `{"schema":"gc-native-ui-observations-v1","rows":[{"jp":"실제 JP 전체 문구","evidence":"화면/문맥/관측 근거"}]}`다. 화면에 없는 문장을 관측했다고 기록하지 않는다.

게임 코드의 문자열 상수는 prefab `m_text`/`m_Text` 수집에 포함되지 않는다. 주간 퀘스트 탭·하단 설명과 Novel 건너뛰기 안내가 이 경우였다. 이때는 화면에서 확인한 문구와, 설치된 PC 코드에서 정확한 원문을 확인한 같은 메뉴의 유한한 형제 문구를 observations로 전달할 수 있다. evidence에 화면 관측과 코드 원문 확인을 구분하고 근거·버전 hash를 남긴다. 검수 후 동일한 apply/export 경로로 exact 사전에 추가하며, 임의 부분 문자열 치환이나 Master 전체의 UI 복사를 도입하지 않는다. local coverage 누락 0은 이러한 게임 코드 문구까지 모두 번역했다는 뜻이 아니다.

## 3. 빌드·패키지·설치

### PC 코드 상수와 상세 메뉴 보완

prefab 범위에 포함되어도 코드가 나중에 넣는 필터 선택지·팝업 문구는 별도 후보가 필요하다. 다음 도구는 설치된 PC v39 메타데이터의 지정한 문자열 계열만 읽고, 원본 hash와 literal index를 남긴다. 전체 캐시 재색인이나 자동 번역은 하지 않는다.

```powershell
python tools/collect_ui_literals.py --metadata <game>/GC_Data/il2cpp_data/Metadata/global-metadata.dat --contains メモリー --contains スター --output work/local-parity/detail-literals.json
```

결과를 앞의 plan에 `--observations work/local-parity/detail-literals.json`으로 전달한다. `source_kind=pc_metadata_literals`는 실제 화면 관측과 구분하며 표시 여부·문맥을 검수한다. 입력창 편집 문자는 번역하지 않는다. 집무실처럼 Master와 같은 JP를 코드에서 직접 공급하는 경우는 해당 문구만 accepted Master 번역과 맞춰 UI에 추가한다.

`ui/detail-families.json`은 확인된 메모리 4그룹×5세트, 유효한 생일 확인 날짜, 현재 Master의 메인 스테이지 범위 안내를 완성된 exact로 export한다. JP 및 이미 KO로 바뀐 Master 이름을 모두 처리하며 새 장이 Master에 들어오면 export 시 따라간다. 범용 부분 치환이나 DLL template engine은 추가하지 않는다. 7.2.0 후보는 한 글자 `炎`·`雷`를 포함한 일반 UI exact를 허용하고 시나리오 렌더러와 사용자 입력을 보호한다. 사전에 키가 있다고 실기 표시 완료로 세지 않는다.

```powershell
python tools/local_release.py build --game <game-directory>
python tools/local_release.py verify
python tools/local_release.py pack --output work/local-parity/update.zip
python tools/local_release.py verify-archive --archive work/local-parity/update.zip
```

DLL은 `source/GCMod.Candidate`에서 `candidate/GCMod.dll`로 Release 빌드한다. 기존 BepInEx/interop/Utility 참조가 필요하며 build는 게임을 수정하지 않는다. 게임 runtime은 local 구조와 검증된 작은 예외를 유지한다. 크롤러·검수·package 트랜잭션을 runtime에 넣지 않는다.

기본 archive는 DLL·cfg·한국어 runtime JSON만 공급하며 게임 본체/interop/Utility/폰트를 제외한다. 개인 설치 묶음이 필요할 때만 `--font <approved-bundle> --utility <Utility.dll>`을 함께 지정한다. 이 묶음은 공개 배포하지 않는다.

게임 종료 후:

```powershell
python tools/local_release.py install --archive work/local-parity/update.zip --game <game-directory> --backup <new-backup-directory-outside-game>
```

설치 전 archive membership·hash·등록된 Novel 존재를 확인하고, 원본 backup/journal을 만든다. 기존 일반 설정은 보존하고 한국어/offline/Master `*`/폰트 경로를 적용한다. 실행 중 게임에는 설치하지 않는다. 설치 실패는 기존 bytes로 복구한다.

```powershell
python tools/local_release.py rollback --game <game-directory> --backup <same-backup-directory>
```

설치 후 사용자가 파일을 변경했으면 rollback은 덮어쓰지 않고 충돌을 알린다. 실제 실행은 사용자가 제공한 기존 DMM 런처를 따른다. 시작 로그, 대표 Master/static UI, 시나리오 제목·본문·화자와 변경한 화면만 확인한다. 기존 `EnabledTables=*`를 좁은 smoke 목록으로 바꾸지 않는다.

## 4. 커밋 준비

`.github/workflows/local_parity.yml`은 CPU 회귀·package 검사만 한다. 게임 참조가 필요한 DLL 빌드와 Native는 로컬에서 별도 확인하며 CI가 Native PASS를 만들지 않는다. `tools/verify_local_parity.py`로 같은 검사를 실행할 수 있다.

검증된 source/data/DLL과 익명화된 요약만 stage한다. raw/state/helper/폰트/게임 DLL/로그/개인 경로는 제외한다. commit/push/PR/release는 이 도구들에서 수행하지 않는다. 실제 Git 상태와 Native 검증 범위를 최종 보고한다.
