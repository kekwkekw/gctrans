# 전체 텍스트 잔여 재조사 — 2026-11-02

기준 main은 PR #57 병합본 `8e7e88884493c7dffc7c29b9e84a868570dbc14a`다. 이번 작업은 수집·분류·현재 사전 대조이며, 번역 적용이나 배포가 아니다. 현행 WebGL, 설치 PC, PC 캐시의 여러 버전, 과거 압축본을 구분했다.

**새로 확인한 주요 공백은 설치 PC의 현지화 CSV다.** 두 TextAsset에서 일본어 열 153행·141종을 추출했고, 현재 `UiTextLookup`에 없는 89종을 후속 검수 목록으로 남겼다. 기존 번역 재사용 후보는 9종이다. 파일이 설치돼 있다는 사실만으로 현재 화면 노출까지 승인하지 않는다.

| 조사 영역 | 확인한 범위 | 결과 |
|---|---:|---|
| 현행 WebGL | 23,616개 에셋 | 최신 manifest와 기존 취득본의 추가·삭제·토큰 변경 0. 문자열 값 384,976종, 일반 문자열 출현 4,415,457건 |
| 설치 PC 캐시 | 4,358개 번들 | 모든 타입의 문자열을 조사. Master 3버전의 583개 테이블도 기존 private helper로 복호화 |
| 설치 PC 기본 리소스 | 9개 파일 | 정확히 일치하는 캐시 타입 정보와 전체 바이트 왕복 검증으로 24,549개 객체 해석. 미해결 469개 보존 |
| PC 코드 | 21,952개 literal | 언어·빈 문자열 필터 없이 수집. 기존 offline 참조 조사에서 21,943종의 참조 확인, 미참조 9종도 보존 |
| PC 부가 파일 | 설정, 캐시 정보, managed resource 등 | XML의 값·속성·주석, CSV의 모든 열, locale 후순위 값도 보존 |
| 과거 압축본 | 설치 캐시에 없는 583개 번들 | 801,278,012바이트를 별도 추출·조사. 현행 누락으로 합산하지 않음 |

384,976종에는 기술명·파일명·서식·숫자도 포함된다. 다른 source plane과 중복되는 값도 있으므로 에셋 수, 출현 수, 번역 문장 수를 서로 바꾸어 쓰면 안 된다. 빈 값과 반복 구조 키는 해당 자산별 개수·첫 위치로 집계하며 원본 파일을 함께 보존했다. 기존 Unity typetree index의 모든 비어 있지 않은 값은 재사용하고, 기존 index가 생략했던 Spine/Novel 묶음의 일반 객체 트리는 다시 읽었다. TextAsset JSON과 Master는 키·중첩 값·모든 배열 위치를 펼쳤다.

## 잔여 결과

| 영역 | 이번 대조 결과 | 처리 |
|---|---|---|
| 일반 Novel | 1,939파일·145,339행, 추가 누락 키 0 | 기존 HOLD 16행 유지. 동일 원문 값 34종·1,009회는 일본어 문자가 없는 값 |
| 전투 Novel | 기존 10파일·19행 | 사용자 지시대로 11장 실기·반영 보류 유지 |
| 추가 Novel 명령 | `selectif` 선택지 4종 | 모두 현재 UI 조회에 일치. 기존 `ChoicePatch`의 `EventSelect.AddSelectMessage` 경로 확인 |
| 현행 Master | 기존 missing 256 + source-equal 76 = 332항목 | 이전 상세 잔여와 값 변경 0. 크레딧·루비/색인·중립 값·placeholder·원문 충돌의 기존 분류 유지 |
| 현행 asset UI | 이전 exact 미등록 588종 중 현재 조회 일치 38, 미일치 550 | 미일치에는 일본어 표시 후보 220종과 숫자·기호·라틴 문자 330종 포함. 기존 샘플·debug·원문 충돌·레이드 말풍선 분류와 연결 |
| PC 현지화 CSV | 조회 미일치 89종 | `MlDictionaryTextConfigInApps` 57종, `MlDictionaryInApps` 32종. 로딩 팁·앱 안내 등을 ID/label/언어 열과 함께 보관 |
| 추가 PC UI 원문 | 15종 | 기본 리소스 12, 설치 캐시 3. 입력 안내와 샘플·미완성 시간 표기 등이 함께 있어 소비 경로 확인 필요 |
| 비일본어 코드 표시 소비자 후보 | 22종 | 텍스트 setter 등을 호출하는 함수의 literal. 인자 데이터 흐름이나 실제 노출을 확인한 결과는 아님 |

기존 8행 PRO 준비본, 고정 문구 hold 2개, 서식 의미 승인 130개, 조합 조각 92개는 기존 상태를 유지한다. 새 후보를 전부 “화면에 남은 미번역”으로 확정하지 않는다. 특히 입력 필드·외부 SDK·서식 조합은 기존 hook의 적용 범위를 먼저 확인해야 한다.

## 수집에서 바꾼 기준

- 일본어 포함 여부나 기존 Master 필드 등록 여부로 **수집을 제한하지 않는다**. 표시 여부 분류는 수집 후 수행한다.
- Spine라는 경로 이름으로 묶음 전체를 제외하지 않는다. 타입 또는 payload 식별 표식으로 확인한 음성·영상·이미지·Spine만 제외한다.
- 알 수 없는 바이너리와 타입 해석 실패는 성공이나 제외로 바꾸지 않는다. 원본, 위치, 해시, 사유를 남긴다.
- PC의 타입 정보가 없는 객체에 UnityPy의 추정 기본 schema를 적용하지 않는다. Unity 버전·class·type hash·script ID가 모두 맞고 전체 바이트 왕복 검증을 통과할 때만 복구한다.
- 해석되지 않은 객체의 길이 기반 UTF-8 문자열은 **미검증 단서**로 별도 보존한다. 번역 적용 입력으로 사용하지 않는다.
- TextAsset의 파일 본문을 보존하면서 현지화 CSV 셀, 일반 CSV 셀, XML 값도 별도로 추출한다. 암호화된 Master payload를 평문 수집 완료로 간주하지 않는다.
- 동적 후보는 현재 조회에 일치하는 원문도 포함한다. 원문 서식의 사전 존재와 실제 최종 조합 문자열 지원을 구분하며, 기존 130개 서식의 `publication_allowed=false`를 유지한다.

이번 조사는 AP와 같은 코드 고정 문구, 설치 PC 기본 리소스, 현지화 표, 조합 조각을 asset UI와 함께 확인하는 기준을 만든다. 서버가 나중에 보내는 값, 아직 취득하지 못한 에셋, 이미지 안의 글자, 실행 중 생성되는 모든 조합까지 조사했다는 뜻은 아니다.

## 보존·검증

WebGL에서 미해석으로 남았던 2,420개 TextAsset은 원본 표식 확인 결과 `GC Movie` 영상이다. PC 캐시의 543개도 Spine 497개·동일 영상 46개로 확인했다. 기존 실패 기록은 삭제하지 않고 해소 근거를 별도로 남겼다.

PC 기본 리소스의 469개는 타입 또는 구조 해석이 미완료다. serialized 원본과 실패 근거, 추출 가능한 미검증 문자열을 보존했다. 처음의 추정 schema 시도 결과도 보존하되, 현재 대조에는 검증된 기본 리소스 재조사만 사용했다.

PC 원본 8,754개·4,422,194,600바이트를 별도 소유 저장소에 보존했다. 과거 압축본과 설치 파일의 중복 경로 7,503건을 CRC·크기로 대조했으며, 변경된 캐시 시간 정보 `__info`를 제외한 내용 차이는 0이었다. WebGL은 기존 raw 소유 해시와 파일 크기·존재를 확인했고, 소비한 추출 파일 163,385개는 해시를 재검증했다. 이번에 WebGL raw 35GB 전체를 다시 해시한 것은 아니다.

번역 정본·Names·Knowledge·runtime/candidate를 포함한 보호 파일 **2,205개 해시 변경 0**. Python 회귀 87개와 기존 C# 검증 프로젝트 4개를 확인했다. 새 corpus 후보의 실제 게임 표시는 실행하지 않았다. AP 고정 알림의 현재 조회 결과는 기존 `AP를 회복했습니다`를 유지한다.

## 기록 위치와 재실행

공개 요약: `staging/text-resurvey-20261102/summary.json`.

전체 원문·실패 자료는 Git 제외 경로 `work/text-resurvey-20261102/`에 있다. `README_KO.md`가 상세 파일 안내다. 핵심 결과는 `coverage-rows.jsonl`, `gap-ledger.jsonl`, `localization-review-candidates.json`, `additional-pc-ui-source-candidates.json`, `non-japanese-display-consumer-candidates.json`, `dynamic-registry.json`이다. 원문 전체·HOLD 원문·private helper 내용은 공개 staging에 넣지 않는다.

`tools/audit_text_sources.py`는 기존 추출본을 읽는 SQLite 조사 도구다. 기존 Novel prepare/apply 경로를 변경하지 않는다. 별도 출력 폴더를 사용하며 입력 또는 구현 해시가 바뀐 결과를 같은 DB에 이어 붙이지 않는다.

```powershell
python tools/audit_text_sources.py webgl --inventory <assetbundle.json> --inventory <master.json> --output <private-new-output>
python tools/audit_text_sources.py pc --pc-game-data <GC_Data> --harvest-cache-schemas --decode-pc-master --output <private-new-output>
python tools/collect_ui_literals.py --metadata <global-metadata.dat> --all-literals --output <private-code-index.json>
```

PC Master 복호화에는 기존 `NG03_PRIVATE_CRYPTO_HELPER`와 SHA pin을 그대로 사용한다. 과거 압축본은 별도 디렉터리에 추출하고 `--pc-provenance historical`로 조사한다. 새 검수는 CSV 89종의 소비 경로와 재사용 9종부터 확인한 뒤 기존 초벌 → PRO → 현재 preimage 검증 절차로 진행할 수 있다.
