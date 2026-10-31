# 전 자산 수집과 coverage 측정

`scripts/crawl_only.py --lane All`은 알려진 WebGL/R18의 assetbundle 및 master
manifest 전체를 선택하는 명시적 조사 모드다. 기존 기본값 `Novel`, Novel intake,
Master/UI의 기존 선택 계약과 재개 state는 그대로 유지한다. PC는 종전처럼
`--local-asset`와 `--asset-key`로 지정한 실제 로컬 자산만 읽으며 WebGL 결과를
PC에서 관측한 결과로 바꾸지 않는다.

## 실행

기존 `NG03_PRIVATE_CRYPTO_HELPER`와 SHA pin을 로컬 세션에 설정한다. helper,
서명 URL, 인증 값 및 게임 원본은 Git 또는 공개 산출물에 포함하지 않는다.

```powershell
# 취득 전 현재 inventory/표시 용량/디스크 여유 확인
python -X utf8 scripts/crawl_only.py --lane All `
  --output source-out/all-assets/scan.json

# 전 자산 취득 및 추출. 순차 처리 기본값을 필요 시 소수 worker로 늘린다.
python -X utf8 scripts/crawl_only.py --lane All --acquire --max-candidates 0 --all-workers 4 `
  --cancel-file source-state/all-assets/stop `
  --output source-out/all-assets/scan.json

# 같은 inventory 시점으로 재현하려면 두 manifest를 함께 지정한다.
python -X utf8 scripts/crawl_only.py --lane All --acquire --max-candidates 0 `
  --inventory source-out/assetbundle-inventory.json `
  --all-master-inventory source-out/master-inventory.json `
  --output source-out/all-assets/replay.json
```

All 기본 `--max-candidates 0`은 전체를 처리한다. 기존 lane 기본값은 200이며
명시한 `CRAWL_MAX_CANDIDATES` 환경변수는 계속 적용된다. `--max-candidates N`은
한 번에 새로 처리할 자산 수를 제한하고 나머지를 pending으로 남긴다.
`--asset-key`나 반복 가능한 `--asset-pattern`으로 임의 자산을 선택할 수 있다.
All에서 `--source-ids`는 받지 않는다. 서로 다른 경로의 같은 Novel 번호를
하나로 합치지 않고 자산 경로 전체를 식별자로 쓴다.

All은 inventory 개수, metadata/raw byte 수, 경로 유형에 따른 제외가 없다.
`--all-max-objects 0`도 무제한이다. 이 옵션들은 기존 lane의 기본 한도를 바꾸지
않는다. TLS, 알려진 host, redirect 거절, 경로 검사, private helper, 재시도와
상태 잠금은 유지한다. 무제한은 물리 메모리/디스크/decoder 지원을 보장하는
말이 아니다. HTTP 수신은 파일 스트리밍이며 UnityPy의 번들 해석은 메모리를
사용한다. 메모리 사용량이 큰 자산에서는 worker 수를 줄일 수 있다.

## 보존·실패·재개

- `source-state/all-assets/<platform>/inventory.json`: 관측한 inventory.
- 같은 경로의 `assets/<prefix>/<asset-sha>.json`: token별 raw, 추출 결과,
  단계 상태, 실패 사유, 추출기 버전. token 변경 전 상태는 `history/`로 보존한다.
- `private-source-cache/bundles/`: SHA-256 주소의 raw. 성공한 취득을 먼저 저장한
  뒤 추출한다. vendor `h`는 서명용 token이며 raw SHA-256로 부르지 않는다.
- `private-source-cache/partial/`: 실패한 다운로드의 부분 파일과 HTTP 상태/
  byte 수/정형 오류 코드. 재시도한 이전 부분 파일도 보존한다. 부분 파일은
  완전한 raw로 승격하지 않으며 재개는 미완료 자산을 다시 받는 방식이다.
- `private-source-cache/extracted/<asset-sha>/<attempt>/`: TextAsset bytes,
  복호화된 Master 전체, 이미지 PNG, decoder가 지원하는 audio, typetree,
  serialized object bytes. 알려지지 않은 객체/필드도 원본을 보존한다.
- 각 추출의 `objects.jsonl`, `strings.jsonl`, `failures.jsonl`: container,
  path_id, field, 분류, export SHA/크기 및 단계별 실패. 파싱 일부 실패가 다른
  객체의 성공을 지우지 않는다. 이전 추출 디렉터리도 덮어쓰지 않는다.

`--cancel-file`에 지정한 파일을 생성하거나 Ctrl+C로 취소한다. 실행을 마친 뒤
그 중단 파일을 제거하고 같은 명령으로 재개한다. 성공한 취득·추출은 재사용하고
실패한 취득/중단 작업은 다시 처리한다. 파싱의 `PARTIAL`/`UNSUPPORTED`는 동일
decoder로 매번 반복하지 않는다. `--retry-extraction-failures`를 명시하거나
추출기 버전이 달라지면 보존된 raw에서 다시 추출한다.

Sprite가 외부 CAB/texture를 요구하면 같은 플랫폼·현재 inventory token에 맞는
소유 raw의 객체 인덱스에서만 의존성을 찾고, raw 해시를 검증해 기존 UnityPy
환경에 로드한다. 의존 객체는 중복 export/coverage 집계하지 않으며 사용한
asset/token/raw를 추출 기록에 남긴다. 참조 자산이 나중에 취득된 경우에는 전체
취득 후 해당 의존성 실패만 한 번 더 처리한다. 모호하거나 없는 참조는 명시적
실패로 보존하며, 의존 token이 바뀌면 기존 추출 결과를 무조건 재사용하지 않는다.

All의 `--acquire --integrity`는 재사용하는 raw와 추출 index의 byte 해시를 확인한다. 일반 재개는
소유한 파일의 크기/버전 확인이며 매번 전체 export PNG/audio를 해시하는 검사로
표현하지 않는다. 실패·중단의 종료 코드는 2이며 metadata-only는 취득 성공을
뜻하지 않는다. `progress.json`은 진행 관측이고 최종 `scan.json`과 구분한다.

## 번역 후보와 실제 coverage

Spine 경로/이름과 skeleton JSON 구조를 인식하여 `spine/`에 격리한다. 이미지,
오디오, 기타 구조 데이터도 별도 분류한다. 번들 raw와 serialized object는
계속 남는다. Novel script bytes는 보존하지만 All에서 Novel intake를 생성하지
않는다. Novel 번역은 기존 lane을 사용한다.

크기 0×0이고 픽셀/stream 데이터가 없는 동적 font atlas는
`EMPTY_TEXTURE_NO_PIXELS`로 기록하고 serialized bytes만 보존한다.
실제 픽셀이 없는 객체에 임의의 PNG를 만들지 않는다.

UnityPy 1.10.18의 native typetree reader가 PlayableDirector 객체 끝에서
4바이트를 더 읽으려는 오류는, 전수 확인한 Unity 6000.3.8f1의 내장 schema
SHA-256에 한해 기존 UnityPy Python reader/writer로 재검증한다. 빈
`m_ExposedReferences.m_References`, 원본 길이의 정확한 소비, 원본과 byte 단위
round-trip 일치를 모두 확인해야 복구로 인정한다. 전역 C reader 설정이나 schema,
UnityPy 설치 파일은 바꾸지 않는다. 다른 유형/schema/오류, 잘린 데이터, 남는
바이트, padding 불일치 및 비어 있지 않은 참조 map에는 이 fallback을 적용하지 않는다.

복구 객체의 `objects.jsonl`에는 `typetree_recovery`로 원래 오류, schema/raw hash,
소비 바이트와 round-trip 결과를 남긴다. 이전 PARTIAL을 복구하려면 기존
`--retry-extraction-failures`와 자산 선택 옵션을 사용한다. 이전 raw·추출 폴더·
실패 인덱스는 history로 보존하며, 이 복구는 번역 승인이나 runtime 수정이 아니다.

2026-10-31 확보 snapshot은 23,616자산·35,281,654,000 raw bytes다. 남은
727개 PARTIAL 자산의 공통 원인은 위 reader 오류였으며 3,889개 PlayableDirector를
복구해 전부 EXTRACTED로 확인했다. 해당 727개 raw 해시, 이전 index 2,181개,
실패 3,889행, serialized object 244,675개의 해시는 보존됐다. 문자열 index도
727개 모두 동일하여 **이 복구 자체의 신규 Master/UI 텍스트 기여는 0개**다.
전체 취득 텍스트에서 별도로 검수한 750개 반영 결과는
[텍스트 coverage 보고서](TEXT_COVERAGE_20261031_KO.md)에 있다.

`coverage.json`은 현재 `translations/ko/master.json`과 `ui.json`만 비교한다.
Knowledge, legacy zh_Hans, archive/provenance를 번역 입력으로 읽지 않는다.

- 취득 자산 수/raw bytes, 추출 성공/부분/미지원/실패/미처리 수를 구분한다.
- Master는 전체 테이블을 복호화해 보존하며, 후보는 table/field/exact JP로
  비교한다. 언어 배열의 첫 요소만 JP 후보로 쓴다. 기존 selector와 현재 KO에
  선언된 필드의 값은 비일본어 문자열도 비교하며 script별 수량을 분리한다.
  그 밖의 mapping은 `unreviewed_field`다. 새로운 field를 runtime 지원
  필드나 번역 누락으로 자동 확정하지 않는다.
- UI `m_text`/`m_Text`는 기존 `text_rows` 경로를 재사용한다. 수집한 표시값의
  일본어/비일본어를 구분하고, 기존 UI 경로 밖에서만 추가로 관측한 exact 및
  그중 현재 사전에 없는 일본어 수도 보고한다.
- 다른 TextAsset/typetree 문자열은 `unreviewed_text`다. 내부 ID·객체 이름·
  asset 경로가 번역 가능한 화면 문자열로 자동 승인되지 않는다.
- `present`는 현재 exact key에 다른 값이 존재함, `source_equal`은 JP 그대로,
  `missing`은 key가 없음을 뜻한다. 어느 것도 의미 검수나 실제 표시를 보증하지
  않는다. 수치형 placeholder와 `可変テキスト`도 먼저 검토해야 한다.

`coverage-candidates.jsonl`은 위치·플랫폼·raw hash를 포함하는 검토 자료다.
자동 적용 수와 실기 확인 수는 0이다. 코드 literal, enum, 동적으로 조립한 문장,
이미지 속 글자/OCR는 이 크롤러의 coverage와 구분한다. 추가 번역은 기존 승인된
Novel/corpus prepare·검수·apply/export 흐름으로 별도로 진행한다.

## 필요한 회귀

```powershell
python -m unittest tests.test_ng03_source tests.test_ng04_source `
  tests.test_ui_source tests.test_all_asset_source
git -c core.whitespace=cr-at-eol diff --check
```

합성 테스트 수치를 실제 CDN coverage라고 보고하지 않는다. 크롤러만 변경한
작업에서 전체 Knowledge 재구축·번역 정본 갱신·DLL 배포·전체 Native 전수 검증을
추가 gate로 만들지 않는다.
