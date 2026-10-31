# 확보 텍스트 반영 결과 — 2026-10-31

기준은 `main 8f85a2390e4dc4e402d71948b25f447a48787e1c`와 PlayableDirector 복구 후의 전체 asset 조사다. 앞선 크롤러/복구 단계에서는 번역을 바꾸지 않았고, 후속 번역 반영 요청에 따라 원문·current 문맥을 검토한 데이터를 추가했다. **2026-11-01 반환 PRO 검수 반영 완료:** 신규 Master 361개·UI 389개 전부 승인됐으며, 초벌 중 Master 8개·UI 11개를 교정했다. 전투 응원 6행도 의미검수를 완료했지만 전투 19행의 정본 등록은 11장 실기 확인까지 보류한다.

| 영역 | 이번 정본 반영 | 검증 결과 |
|---|---:|---|
| Novel | 원문 오탈자 키 1개 | 표준 1,939파일·145,339행의 누락 키 1→0. 기존 HOLD 16행 유지 |
| Master | 361개 | 확보된 33,169개 대상 중 present 32,476→32,837, missing 617→256 |
| UI | 389개 | 확보된 2,054개 값 중 exact 1,077→1,466. 최초 누락 977개를 실제 조회한 결과 38→427개 처리 |

Master는 적·보스 이름, 배틀 랭크, 시즌명, 프로필 문구, 기본 닉네임, 상자 선택 이름 등 12개 필드에 추가했다. 기존 값은 덮어쓰지 않았다. UI는 계정 연동·삭제, 해지·결제 안내, 던전, 장비, 아테네스, 뮤지엄 등 확보된 exact 문자열을 기존 reviewed/export 경로로 반영했다. 새 범용 치환 규칙이나 runtime 코드는 없다.

`staging/text-coverage-20261031/accepted.json`은 최초 Codex 초벌과 취득 근거를 보존한 이력이다. 그 파일의 `reviewer=Codex`나 UI authoring 파일의 `reviewed`라는 이름을 PRO 승인 근거로 사용하지 않는다. 최종 결정은 [반환 검수 및 적용 영수증](../staging/text-coverage-20261031/pro-reviewed-20261031/APPLICATION_RECEIPT.json)에 연결돼 있다. Master/UI 750행 중 keep 731, correct 19, hold/pending 0이며 기존 전체 Master/UI를 다시 검수한 것은 아니다.

인계 묶음은 Git 제외 `local-handoff/GC_Text_756row_PRO_20261031.zip`이다. Master/UI는 기존 `corpus_consistency.prepare`로 한 개의 750행 batch를 생성하고, 보기용으로 Master 181/180개와 UI 130/130/129개로 나눴다. 반환 ZIP SHA-256은 `faf30bf3d08432f6d87074ac425ea65a52f21df55ac4705a158748bb8fb18bfb`다. 입력 32파일·반환 8파일 해시와 live preimage를 독립 검증하고 기존 corpus apply로 한 번에 반영했다. 전투는 별도의 기존 Novel check를 통과했다. 두 검수 모두 `changes=[]`이며 current Knowledge·Names는 바이트 단위로 유지했다. 기존 적용기가 생성한 corpus provenance는 변경 영수증이며 current Knowledge 변경이 아니다.

Novel `29000202`의 `思えは→思えば`는 한 글자 원문 오탈자 수정이다. 같은 파일의 기존 KO를 바이트 단위로 재사용한 alias이며 구 키도 남겼다. 새로운 PRO 검수를 받은 것으로 표시하지 않았다. 본문 의미 변경은 0개다.

## 잔여 항목과 다음 작업

UI 최초 누락 후보 977개는 신규 반영 389, 기존 runtime 처리 38, 언어 중립 330, prefab 샘플·삽입용 표제·글자 수 시험 200, 개발 UI 19, 원문 충돌 1개로 분류했다. 가챠 안내의 `季品石`·`頭性`는 정식 용어와 충돌하여 원문 확인이 필요하다. 숫자 샘플의 exact 추가가 임의의 실제 숫자 조합을 지원한다는 뜻은 아니다.

Master 후보 693개는 신규 반영 361, 언어 중립 172, 기존 GlossaryPatch가 처리하는 색인·루비 59, 독음 미확인 크레딧 원문 유지 63, 일러스트레이터 placeholder 31, 내부 설명 3, 원문 충돌 4개다. source=KO인 기존 76개는 그대로 유지했다. 이 중 일본어 7개는 내부 설명 3개와 원문 충돌 4개다. 기존 HOLD를 임의 해제하지 않았다.

표준 Novel 파일명 밖에서 **전투 대사 10파일·19행**을 추가로 확인했다. `mNovels`의 `1001110911` 등은 Master 연결 ID이며, 번역 조회 ID와 다르다. 설치된 게임의 native 호출 경로를 추적한 결과 `btl_10011109_11`은 `prefix=btl_10011109, id=11`로 전달된다. 올바른 번역 ID는 `11,21,22,31,41,42,43,44,45,46`이다. 이전 10자리 ID 배치는 미적용 조사 이력으로 보존하고, 올바른 ID의 배치를 `work/novels/battle-runtime-ids-20261031/`에 새로 준비했다. 정본 등록은 하지 않았다.

[후속 조사](BATTLE_NOVEL_FOLLOWUP_20261031_KO.md)에서 과거 [PR #13](https://github.com/kekwkekw/gctrans/pull/13)의 **4파일·13행 내용 검수 완료 / 실제 prefix·ID 확인 전 배포 보류** 기록과 승인본을 복구했다. 현재 JP·화자가 13/13 일치하여 승인 KO를 exact 재사용했다. 반환 검수에서는 이 13행 전체를 그대로 유지하고 신규 응원 6행을 승인했으며, `43:0` 한 행의 호칭을 교정했다. 전체 19행의 exact identity·태그·34 UTF-16칸×3줄 검사도 통과했다. 전투 실기는 `NOT_RUN`이다.

이 대사는 `mQuestStages 1111009 → mQuests 111 → chapter_no 11`에 속한다. 사용자가 현재 10장까지 해금했다고 알려왔으므로 실제 전투 호출·화면 검사는 11장 진행 후 후속 항목으로 남긴다. 과거 PR에는 prefix/ID 미관측에 따른 보류가 명시되어 있지만, 당시 10장 해금 상태라는 구체적 이유까지 확인한 것은 아니다. 원문·승인본·기술 검증과 실기 대기를 구분하며 Knowledge와 runtime은 변경하지 않는다.

그다음은 이번 변경 화면의 실제 표시·줄바꿈 확인이다. 획득 범위를 다시 넓히거나 전체 Knowledge를 재구축할 필요는 없다. 코드에서 조합하는 문구·이미지 속 글자·PC 별도 asset은 이번 WebGL exact 조사만으로 완료를 주장하지 않는다.

## 검증과 보존

- 추가한 750개 키를 원본 hash가 확인된 155개 asset의 추출 JSON과 대조했다.
- 기존 Master/UI 값 전부, 다른 Novel 파일, Names, Knowledge, HOLD 기록, runtime 소스와 DLL을 보존했다. raw/state/실패 이력은 읽기 전용으로 사용했다.
- 최종 Python 회귀: 기존 CI harness의 8개 suite 총 65개와 corpus consistency 17개 통과. 앞선 Novel workflow 24개 및 NG03 source 19개도 통과했다.
- 실제 UiTextLookup: 11,975 exact 쌍, 56개 wrapper/숫자/날짜, 24개 비일치·HOLD 경계 통과. UiTextScope의 입력·Novel·choice·ruby 경계도 통과.
- 현행 MasterDataTranslator를 직접 컴파일하여 신규 361개를 포함한 421 fixture × 단일/배열 record = 842개 검사 통과. 원래 Master 종합 테스트는 제거된 Candidate/Nextgen 타입을 참조해 실행 불가였으며, 그 테스트를 통과했다고 기록하지 않았다.
- 최종 exporter 및 package membership/manifest/hash 검사 통과. 게임 설치·Native 확인은 수행하지 않았다. 아래 로컬 ZIP은 반환 교정 전의 역사 자료다.

정본 추가 근거는 `staging/text-coverage-20261031/accepted.json`에 보존했다. private 원문과 전체 후보별 분류, 변경 전 복사본, 실행 증거는 Git 제외 `work/text-coverage-20261031/`에 있다. 전투 검수 자료의 현재 경로는 `work/novels/battle-runtime-ids-20261031/`이며, 이전 `battle-fragments-20261031/`은 잘못 가정한 ID의 미적용 이력이다.

이전 로컬 확인용 `work/text-coverage-20261031/GCMod_TextCoverage_20261031.zip`은 PRO 교정 전 초벌이며 최종 배포본으로 사용하지 않는다. 당시 미공개 번역 보호를 위해 ZIP 안의 cfg만 `CDN=offline`으로 만들었으며 설치하지 않았다. 저장소 cfg와 기존 DLL은 이번에도 변경하지 않았다. 최종 승인 데이터는 저장소의 기존 번역 배포 경로를 따른다.
