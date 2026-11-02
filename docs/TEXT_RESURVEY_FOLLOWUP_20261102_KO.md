# 전체 텍스트 재조사 후속 검수·반영 — 2026-11-03

기준 정본은 PR #57 병합 `8e7e88884493c7dffc7c29b9e84a868570dbc14a`, 전수 재조사는 `a143c9172b6cf3ca7166fd53bff6d139f230c881`, 검수 준비는 `3f0f1095`다. **2026-11-03 PRO 승인 고정 92행(UI 85 + Master 7)을 로컬 정본에 반영했다.** 서식 3행은 의미 승인만 보존하고 배포하지 않았다. 아래 준비 기록과 현재 적용 결과를 구분한다. main 병합·게임 설치·실기는 아직 수행하지 않았다.

## 준비한 95행과 현재 판정

| 구분 | 행 | 상태 |
| --- | ---: | --- |
| PC 리소스 CSV 고정 UI | 80 | PRO 승인 후 반영 |
| PC 프리팹 고정 UI | 4 | PRO 승인 후 반영, 실기 미확인 |
| 이전 대기 UI 말풍선 | 1 | 원본·preimage 재검증, PRO 승인 후 반영 |
| 이전 대기 Master 크레딧 | 7 | 5명, PRO 승인 후 반영 |
| 새 CSV 서식 | 3 | 의미 승인, publication_allowed=false 유지 |
| 합계 | 95 | 고정 92 반영 + 서식 3 의미 승인 |

이전 `GC_Text_Remaining_8row_PRO_20261101.zip`의 8행은 이번 묶음에 포함했다. 두 ZIP을 중복 적용하지 않는다. 9개 UI 초벌은 같은 JP의 현행 Master/Names 번역을 재사용했다. 현재 파일 해시와 표·필드별 근거를 함께 제공하며, 재사용을 새 PRO 승인으로 간주하지 않는다.

검수 ZIP은 로컬 `local-handoff/GC_Text_Resurvey_95row_PRO_20261102.zip`이다. 요청문과 상세 인계는 동일 이름의 폴더에 있는 `PRO_REQUEST_KO.md`, `HANDOFF_KO.md`에 있다. ZIP SHA-256은 `staging/text-resurvey-followup-20261102/summary.json` 및 ZIP 옆 `.sha256`에 기록했다.

## 분류를 바꾼 근거

CSV raw 누락 후보 89개를 그대로 exact key로 넣지 않았다. 설치된 GameAssembly의 `TextMasterLoader`에서 문자 그대로의 `\n`, `\r`을 LF, CR로 바꾸는 호출을 확인했다(RVA 7018A0, 701930, 7019C0, 701A50). 원문 셀은 보존하고 그 두 변환만 적용한 표시 문자열을 실제 C# lookup으로 재측정했다.

- 이미 번역되는 4개: 캐시 초기화 안내, 연동 완료, 외부 브라우저 이동 확인, 캐시 삭제 중 안내.
- 고정 번역 후보 80개: 로딩 팁·시스템 오류·옵션·연동·튜토리얼 등의 문구.
- 서식 3개: 다운로드 오류 파일명, 오류 코드, 튜토리얼 진행 수치. 정본 배포 목록에서 분리했다.
- 그대로 유지할 중립 표기 2개: 저작권 표기와 `O K`.

PC 프리팹 15개 중 4개를 고정 번역 후보로 옮겼다. `館長IDを入力してください`, `連携パスワードを入力してください`는 입력 컴포넌트가 붙은 `Text`의 형제 `Placeholder`다. 입력 컴포넌트의 m_Placeholder가 라벨을 참조하는 것까지 확인했다. 기존 UiTextScope는 입력 컴포넌트의 자손을 보호하므로 형제 라벨은 해당 차단에 걸리지 않는다. `ユーザ名`, `告知スライド`도 표시용 오브젝트 경로를 확보했다. 나머지 11개는 반복 글자·XX·시각 예시 등 프리팹 샘플로 원문과 근거를 유지한다.

비일본어 코드 후보 22개도 삭제하지 않았다. 16개는 기술 토큰·로그, 3개는 내용 없는 서식 래퍼이며, `Cancel`, `Default`, `Input Text` 3개는 WebGL native input dialog 경로 조사 대상으로 남겼다. UI 호출과 같은 함수에 있다는 것만으로 표시용 번역이라고 판정하지 않는다.

전체 새 후보 126개(89+15+22)의 원문별 분류는 private handoff의 `candidate_dispositions.json`, 공개 집계는 `staging/text-resurvey-followup-20261102/disposition-counts.json`에 있다.

## 초벌 준비 당시 검증 결과와 경계

- CSV 89개 전부 캡처한 TextAsset 원본 셀·ID·label과 일치한다. exact schema와 바이트 roundtrip 근거를 유지했다.
- 고정 UI 85개: 현행 lookup 미일치 85 → 격리 초벌 적용 후 85개 모두 예상 KO 반환.
- 격리 복사본: Master 7항목, UI 85항목 증가. 기존 번역 값 변경 및 추가 alias 0개.
- 95행 모두 숫자·변수·태그 검증 통과, 레이아웃 경고 0개.
- 기존 LocalParity apply, UI exporter, package/manifest 검증 통과. 격리 패키지 UI 14,073쌍, Master 34,388항목, Novel 1,939파일.
- 기존 UiTextScope 회귀 통과. 입력·Novel·선택지 보호 정책을 변경하지 않았다.
- 정본·Knowledge·Names·candidate/runtime 등 보호 파일 2,205개 해시 불변. 설치 GameAssembly와 metadata 해시도 재확인했다.
- 실제 게임 표시 확인은 NOT_RUN이다. 특히 공유 CSV의 Android/WebGL 분기와 재입력 안내의 live consumer를 현재 PC 노출로 단정하지 않는다.
- raw, 미해결 객체, 실패 로그와 이전 검수 입력은 보존했다. native 조사 중 출력 폴더 누락으로 실패한 로그도 보존하고 성공한 재실행을 별도로 기록했다.

## PRO 반환 검증과 정본 반영

반환 ZIP `GC_Text_Resurvey_95row_PRO_Reviewed_20261102.zip`의 SHA-256은 `176775e4bea4c68947234187e260aebea32894e01b2a6eda3f508de287ff5114`다. 세 파일만 포함된 ZIP의 CRC, 입력 manifest 174개 파일, static 92행·formats 3행의 ID/JP/순서/기존 메타데이터를 검증했다. 두 반환의 pro_review_complete=true, changes=[], 전 행 reviewer/evidence, 숫자·변수·태그 보존을 확인했다. 실제 수정은 고정 17행·서식 1행이며 HOLD/pending은 없다.

현행 Knowledge의 `몽환 미술관`, Names의 `아테네스 시경`과 PRO 교정이 일치한다. 성우 `くのいちご`는 승인된 `쿠노이치고`를 적용했다. 기존 번역 재사용 제안 9개 중 4개의 교정은 이번 UI 신규 항목에만 반영했으며 기존 Master·Names 값을 소급 덮어쓰지 않았다.

현재 preimage와 Names/Knowledge 문맥을 검증한 뒤 승인본을 별도 사본에서 먼저 적용했다. 기존 LocalParity apply/export/manifest 경로로 정본에 반영한 결과는 사본과 바이트 단위로 같다. Master는 34,381→34,388, UI는 13,988→14,073이며 기존 모든 번역 값은 유지했다. 정본 변경은 Master/UI authoring/exact와 관련 manifest 7개뿐이다.

실제 C# lookup으로 고정 UI 85개 전부가 승인 KO를 반환한다. 두 CSV의 전체 고유 표시 문자열 141개를 재측정한 결과 조회 성공은 56→136개다. 신규 성공 80개는 이번 CSV 승인 고정 문구와 정확히 일치하고 기존 성공 56개의 결과도 유지된다. 남은 5개는 서식 3개와 중립 표기 2개다. 이는 CSV 묶음에 대한 조회 측정이며 전체 게임 표시율이 아니다.

Python 11개 묶음의 87개 테스트와 C# UiTextLookup/UiTextScope/ImageCache/TranslationCache 4개 검증을 통과했다. 입력·Novel·선택지 보호 및 package/manifest 검증도 통과했다. 정본 변경 7개를 제외한 보호 파일 2,561개가 동일하며 Knowledge·Names·Novel·runtime/DLL, raw·실패 근거·입력/반환 ZIP 원본을 보존했다. 반환 세 파일은 Git 제외 `work/text-resurvey-pro-apply-20261103/returned/`에 있고, 공개 근거는 `staging/text-resurvey-followup-20261102/pro-reviewed/`의 validation/application-receipt/coverage-after/verification/decision-ledger JSON에 있다. 이전 검수 대기 8행은 이번에 모두 해소했으므로 별도 8행 ZIP을 중복 적용하지 않는다.

기존 pack 경로로 `local-handoff/GC_Text_Resurvey_92row_Update_20261103.zip`을 생성했다. 허용된 2,110개 파일의 membership/CRC/bytes를 검증했으며 포함된 GCMod.dll은 기존 candidate와 동일하다. ZIP SHA-256은 `9a25e881ace4490fc8622b809e3ac9f2dd0bf51f5f2e1717141d3bab1541150b`다. 게임 설치는 수행하지 않았다.

## 후속 절차와 남은 항목

LocalParity 도구 자체는 pro_review_complete를 검사하지 않으므로 실제 PRO 반환 확인을 별도로 수행했다. 서식 3개는 의미 승인 이후에도 최종 조합값의 지원 범위와 호출 순서를 검증해야 한다. 이번 raw 서식은 UI 정본에 넣지 않았다.

이번에도 번역 정본에 넣을 근거가 부족한 항목은 남는다. 성우 독음 44명/56필드, 기존 source conflict, Novel HOLD 16행, 11장 전투 실기 대기 19행, 기존 의미 승인 서식 130개와 조합 조각 92개는 기존 상태를 유지한다. PC stripped 객체 469개의 구조 미확정 문제도 남아 있으며, 추정 문자열을 검증된 필드로 승격하지 않았다. 크레딧의 추가 검색 결과만으로 독음이나 다른 명의를 추정해 넣지 않았다.

따라서 이번 결과는 수집된 유효 후보의 검수·정본 반영이며, 게임 전체 번역 완료나 미해결 추출의 해소를 뜻하지 않는다. Knowledge 재구축·Names 변경·새 runtime framework·DLL 변경은 없다. main 병합과 게임 설치·실기는 별도 단계다.
