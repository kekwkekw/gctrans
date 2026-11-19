# 영문 원문 유지 — 2026-11-19 적용

사용자 확정 기준은 **원문 전체가 영문 표기이면 치환하지 않고 대소문자·공백·문장부호·변수까지 그대로 유지**하는 것이다. Normal을 NORMAL로, Hard를 HARD로 바꾸는 정규화도 하지 않는다. 일본어 혼합 문장은 기존 번역을 유지한다.

## 기존 규칙과 공백

[Crawler 종료 기록](CRAWLER_CLOSEOUT_20261103_KO.md)에는 영문 3종을 번역하지 않는 사용자 결정이 있었고, 기존 `translation_update.make_plan`은 일본어가 없는 UI 후보를 건너뛰었다. 반면 Master 후보는 모든 키를 유지했고, exporter는 기존 Master·UI 자료·검수 사전의 영어 번역을 내보냈다. 전체 경로에 적용되는 영문 원문 유지 규칙은 없었다.

## 적용 수량

| 대상 | 수량 | 처리 |
|---|---:|---|
| 고유 원문 | 96 | 원문 유지 |
| Master 고유 원문 / 저장 위치 | 94 / 101 | 번역값을 원문과 같게 저장 |
| UI 치환 | 26 | 최종 ui.json에서 제외 |
| Master·UI 공통 원문 | 24 | 중복 집계 제외 |
| UI 자료 | 4행 | 기존 keep_source로 변경하고 원문 저장 |
| 활성 authoring binding | 1개 | VOICE 비활성화; 나머지 3개는 이미 비활성 |
| 별도 검수 사전 | VOICE 1개 | 활성 사전에서 제외; 이전 번역은 아래 영수증에 보존 |

UI는 14,078 → **14,052**쌍이며 Master 키 수는 **34,388**개를 유지한다. 원문과 같은 Master 값은 현행 MasterDataTranslator가 치환 목록에 넣지 않는다. 영어를 다른 영어로 치환하는 runtime 규칙을 추가하지 않았다. DLL·Knowledge·Names·Novel 본문·이미지는 이 수정에서 변경하지 않았다. `mNovels`의 영어 제목 두 필드는 Master 메타데이터이며 Novel 본문과 구분한다.

이전 UI 23개 집계는 `2nd`, `3rd`, `RANK{0}` 세 항목을 빠뜨렸다. 세 항목은 Master 목록에는 이미 있어 중복 제외 원문 수는 96개로 같다. Master의 101곳과 UI 26개를 더한 127은 저장 항목 수이며 화면 출현 횟수가 아니다.

## 재유입 방지

* `tools/source_text_policy.py`의 공통 기준은 태그와 서식 인자를 분류에서만 제외하고, 표시 문자가 라틴 문자로 구성되는지 확인한다. 숫자·기호가 동반된 영어 및 영문 표기 고유명사도 포함한다. 숫자만 또는 변수만 있는 원문은 영어로 판정하지 않는다. 원문 자체는 정규화하지 않는다.
* Master/UI plan은 해당 원문과 근거를 `preserved_originals`로 기록한다. 추출 raw를 삭제하거나 검수 대기로 만들지 않는다.
* apply는 과거 검수 batch를 사용하더라도 영문 원문을 번역하거나 대소문자 정규화하려 하면 거부한다.
* UI export는 Master·resources·reviewed·태그 별칭·유한 서식 생성 결과에서 영어 치환을 제외한다. 과거 영어 번역을 authoring 자료에 다시 넣어도 UI에 재생성되지 않는다.
* Novel 제목 → Master 동기화도 영문 제목을 보존하고 기록한다. 이 도구는 Novel 파일을 쓰지 않는다.
* package verify/pack은 Master에 영문 치환이 재유입되면 실패한다. Master 원문 유지 값과 UI 제외를 함께 검사한다.

예: `Normal`, `Hard`, `Very Hard`, `VeryHard`는 각각의 원문을 유지한다. `ランク → 랭크`, `メインクエスト(NORMAL) → 메인 퀘스트(NORMAL)`, `ヴォルカン地区 Hard → 볼칸 지구 Hard`는 기존 번역을 유지한다. 화면 문구의 치환 범위만 바꾸며 ID·enum·내부 객체 이름을 바꾸지 않는다.

## 결정 원문과 이전 번역

로컬 검증은 Python 166개·C# 4개 프로젝트 및 authoring 데이터 계약 17개를 통과했다. 기존 영어 번역의 재주입·오래된 검수본 적용·제목 동기화·Master 재유입을 검증했고, 일본어 혼합 문장의 번역과 96개 결정의 UI 재생성 결과를 확인했다. Knowledge·Names·Novel 본문·이미지·DLL을 포함한 보호 파일 2,124개의 SHA-256은 변경되지 않았다. 이 영어 정책 변경 후 새 게임 화면 확인은 별도이며 main 병합·공개 릴리스는 승인 대기다.

[96개 정책 적용 영수증](english-original-preservation-20261119.json)에 원문·이전 번역·Master 위치·결정과 source-equal 결과를 보존했다. 이는 새 한국어 의미검수나 PRO 검수 완료 기록이 아니다. 기존 한국어 검수 이력은 Git에도 남는다. 아래 표의 번역은 원복 전 값이며 현재 공급되지 않는다.

| 원문 | 이전 치환값 | 저장 위치 | 현재 처리 |
|---|---|---|---|
| `2nd` | 2위 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `3rd` | 3위 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `ALL` | 전체 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `AUTO` | 자동 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Appassionata` | 열정 | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `BATTLE1` | 전투 1 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE1 EASY` | 전투 1 쉬움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE10` | 전투 10 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE10 HYPER EXT` | 전투 10 하이퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE10 VERY HARD` | 전투 10 매우 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE11` | 전투 11 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE11 ULTRA EXT` | 전투 11 울트라 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE11 VERY HARD` | 전투 11 매우 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE12` | 전투 12 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE12 EXTREME` | 전투 12 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE12 ULTRA EXT` | 전투 12 울트라 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE13` | 전투 13 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE13 EXTREME` | 전투 13 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE14` | 전투 14 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE14 EXTREME` | 전투 14 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE15` | 전투 15 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE15 EXTREME` | 전투 15 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE16 SUPER EXT` | 전투 16 슈퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE17 SUPER EXT` | 전투 17 슈퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE18 HYPER EXT` | 전투 18 하이퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE19 HYPER EXT` | 전투 19 하이퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE2` | 전투 2 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE2 NORMAL` | 전투 2 보통 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE20 ULTRA EXT` | 전투 20 울트라 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE21 ULTRA EXT` | 전투 21 울트라 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE3` | 전투 3 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE3 HARD` | 전투 3 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE3 NORMAL` | 전투 3 보통 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE4` | 전투 4 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE4 NORMAL` | 전투 4 보통 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE4 VERY HARD` | 전투 4 매우 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE5` | 전투 5 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE5 EXTREME` | 전투 5 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE5 HARD` | 전투 5 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE6` | 전투 6 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE6 EXTREME` | 전투 6 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE6 HARD` | 전투 6 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE7` | 전투 7 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE7 HARD` | 전투 7 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE7 SUPER EXT` | 전투 7 슈퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE8` | 전투 8 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE8 SUPER EXT` | 전투 8 슈퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE8 VERY HARD` | 전투 8 매우 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE9` | 전투 9 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE9 HYPER EXT` | 전투 9 하이퍼 익스트림 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BATTLE9 VERY HARD` | 전투 9 매우 어려움 | `mQuestStages.ml_name[]` | 원문 유지 |
| `BLUE` | 파랑 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Boss` | 보스 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `CHALLENGE1` | 도전1 | `mQuestStages.ml_name[]` | 원문 유지 |
| `CHALLENGE2` | 도전2 | `mQuestStages.ml_name[]` | 원문 유지 |
| `CHALLENGE3` | 도전3 | `mQuestStages.ml_name[]` | 원문 유지 |
| `DEX` | 기교 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `EXTRA1` | 엑스트라 1 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA2` | 엑스트라 2 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA3` | 엑스트라 3 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA4` | 엑스트라 4 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA5` | 엑스트라 5 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA6` | 엑스트라 6 | `mQuestStages.ml_name[]` | 원문 유지 |
| `EXTRA7` | 엑스트라 7 | `mQuestStages.ml_name[]` | 원문 유지 |
| `Florist Life` | 플로리스트 라이프 | `mLayers.ml_name[]`, `slim_mLoadingTips.ml_title[]` | 원문 유지 |
| `GRAY` | 회색 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `GREEN` | 초록 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `HEART TRICK` | 하트 트릭 | `mLayers.ml_name[]`, `slim_mLoadingTips.ml_title[]` | 원문 유지 |
| `Hard` | HARD | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Let's Make Art!` | 예술을 만들자! | `mHomeLayouts.name` | 원문 유지 |
| `Love in letter` | 편지에 담은 사랑 | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `MAX` | 최대 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Moonlight` | 월광 | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `NEW` | 신규 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Normal` | NORMAL | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `OFF` | 끄기 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `OK` | 확인 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `ON` | 켜기 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `PURPLE` | 보라 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Parts` | 파츠 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Pathetique` | 비창 | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `Quest` | 퀘스트 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `RANK` | 랭크 | `ui.json` | 원문 유지 |
| `RANK{0}` | 랭크 {0} | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `RED` | 빨강 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Ride the Wave` | 파도를 타고 | `mLayers.ml_name[]`, `slim_mLoadingTips.ml_title[]` | 원문 유지 |
| `Stage` | 스테이지 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Summer Night Sparkle` | 서머 나이트 스파클 | `mQuests.ml_name[]`, `mEvents.ml_title[]` | 원문 유지 |
| `Symphony No.5` | 교향곡 5번 | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `Symphony No.5:Ec` | 교향곡 5번:Ec | `mActiveSkills.ml_name1[]` | 원문 유지 |
| `Trick and Trick` | 장난에는 장난으로 | `mNovels.ml_title[]`, `mNovels.title` | 원문 유지 |
| `Trick or Arrest` | 트릭 오어 어레스트 | `mLayers.ml_name[]`, `slim_mLoadingTips.ml_title[]` | 원문 유지 |
| `VOICE` | 음성 | `ui.json` | 원문 유지 |
| `Very Hard` | 매우 어려움 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
| `Wedding Gong` | 웨딩 징 | `mLayers.ml_name[]`, `slim_mLoadingTips.ml_title[]` | 원문 유지 |
| `YELLOW` | 노랑 | `mTextConfig.ml_value[]`, `ui.json` | 원문 유지 |
