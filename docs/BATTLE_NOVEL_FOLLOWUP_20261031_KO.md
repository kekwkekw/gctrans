# 전투 Novel 10파일·19행 후속 조사 — 2026-10-31

기준 checkout과 조회한 GitHub main은 `8f85a2390e4dc4e402d71948b25f447a48787e1c`다. 이번 후속에서는 번역 정본·등록 manifest·Names·Knowledge·HOLD·runtime 소스·DLL·게임 설치를 변경하지 않았다. 원문과 실패 이력은 기존 위치에 보존했다.

## 기존 검수와 보류 이력

[PR #13](https://github.com/kekwkekw/gctrans/pull/13), merge `9e4bfa96`는 일반 Novel 338편을 반영하면서 전투 4파일·13행을 제외했다. 해당 전투 대사는 내용 검수 완료였으며, 실제 GCMod prefix/ID 관측 전 CDN 배포를 보류했다. 일반 Novel의 대표 실기 PASS가 전투 대사까지 포함한다는 뜻은 아니다.

기존 승인본은 [보존된 Git 파일](https://github.com/kekwkekw/gctrans/blob/82a8b0f7ef2c2760fe7139707fda69030eb4571c/knowledge-layer/snapshots/export_candidate_v1_2/candidate/battle/BATTLE_REVIEWED_13.jsonl)에서 복구했다. `candidate_status=REVIEWED_APPROVED_CONTENT_RUNTIME_DEFERRED`, `runtime_verified=false`, `publish_ready=false`의 경계를 그대로 유지한다. 현재 추출 원문의 exact JP·화자와 13/13 일치하고, KO도 승인 파일의 `translation_ko_runtime_candidate`를 그대로 재사용했다. 새 초벌로 덮거나 과거 승인을 새 전체 승인으로 바꾸지 않았다.

[PR #34](https://github.com/kekwkekw/gctrans/pull/34), 적용 commit `f5495cff`는 current Knowledge 기준 일반 Novel 1,499편·114,243행을 재검수한 이력이다. [PR #48](https://github.com/kekwkekw/gctrans/pull/48)의 `d7a2aedf`는 153행 표현·줄바꿈 검수 반영이다. 두 이력 모두 이번 전투 10파일의 승인·실기 완료를 대신하지 않는다.

현재 19행의 상태:

| 호출 ID | 파일 수 | 행 수 | 검수 상태 |
|---|---:|---:|---|
| 11, 21, 22, 31 | 4 | 13 | 기존 승인 KO exact 복구, 전투 실기 대기 |
| 41~46 | 6 | 6 | 반환 PRO 의미검수 완료(2026-11-01 반영), 전투 실기 대기 |

현재 10파일 전부를 새로 PRO 검수해야 한다고 단정한 이전 설명은 부정확했다. 먼저 기존 승인 범위를 복구하는 것이 맞다. 추가 검수의 실제 대상은 신규 6행 및 향후 승인 KO의 의미를 수정하는 경우다.

## 실제 호출 ID와 현재 충돌 검사

설치 게임의 `GameAssembly.dll`/metadata를 정적으로 읽어 아래 native 호출 경로를 확인했다. 게임 실행·강제 해금·전투는 수행하지 않았다.

1. `NormalDataGenerator.<CreateNovels>.MoveNext` (RVA `C5B810`): `mBattleNovels.m_novel_id`로 `NovelMaster`를 얻고 `GetPrefixAndScriptId`를 호출한다(`C5BC2D`).
2. `NovelMaster.GetPrefixAndScriptId` (`A97F10`): `novel_script_id`를 `_`로 나누어 마지막 항목을 ID, 앞의 항목들을 합친 문자열을 prefix로 반환한다.
3. `NovelModel → NovelTable.InitializeData` (`BB8FA0`) → `BattleNovelPlayer.Data`에 prefix/script를 그대로 전달한다.
4. `BattleNovelPlayer.<Play>.MoveNext` (`CCEAE0`) → `GcBaseEventControl.Add` (`33C2F0`). 음성 선취득 callback (`348E00`)도 같은 prefix/ID를 전달한다.
5. `BaseEventControl.AddScript` (`34BD00`) → `ScriptObjectManager.Create` (`372640`). 원본 asset 선택에는 prefix와 ID를 다시 `_`로 연결한다.
6. 현행 `TranslationPatch.SetupTranslation`은 `int.Parse(id)`를 `PrepareNovel`에 전달한다. `TranslationSession.GetNovelAsync`와 `TranslationCache`는 `novels/<id>.json`을 조회한다.

따라서 `btl_10011109_11`의 번역 파일은 `novels/11.json`이다. `1001110911`은 Master 연결 값으로, 번역 경로에 넣으면 호출되지 않는다. 전체 대상 ID는 `11,21,22,31,41,42,43,44,45,46`이다.

현재 `mNovels` 1,967행, asset inventory 23,615항목, 기존 등록/파일 1,939편을 대조하여 대상 ID 충돌 0개를 확인했다. 숫자 접미사의 선행 0도 정수로 정규화해 검사했다. 이 결과는 현재 확보 snapshot의 범위이며 미래 콘텐츠와 모든 플랫폼에 대한 전역 유일성을 보증하지 않는다.

## 11장과 실제 화면 확인 범위

Master 연결은 `mBattleNovels.m_quest_stage_id=1111009 → mQuestStages.m_quest_id=111 → mQuests.chapter_no=11`이다. 전투 스테이지의 원문 표시는 `正義の成れの果て`, 선행 스테이지 조건은 `1111008`이다.

사용자가 현재 10장까지만 해금했다고 알려왔다. 계정 진행도를 별도로 읽거나 변경하지 않았으며, 실제 11장 전투 호출·표시·이벤트 순서는 추후 진행 시 확인한다. PR #13에서 전투의 prefix/ID 관측 보류는 확인했지만, 당시 보류 이유에 “10장까지만 해금”이라는 문구까지 찾은 것은 아니다.

실기 재개 시 기존 GCMod 로그의 `Prefix: btl_10011109, Id: 11` 등과 해당 전투 대사의 exact 원문·화면 표시를 함께 확인한다. 정적 native 추적이나 격리 cache 조회 성공을 실제 전투 PASS로 기록하지 않는다.

## 보존 위치와 후속 처리

- 새 배치: `work/novels/battle-runtime-ids-20261031/`. 기존 saved TextAsset `prepare → draft → review` 경로를 사용했다. 원래 초벌과 검수 대기 파일은 보존하고, 반환 승인본을 `review.pro-20261031.json`에 별도로 저장했다. `changes=[]`다.
- 반환 검수 ID는 `GC-BATTLE-CHEER-6-PRO-20261031`이다. 기존 승인 13행은 모든 필드 exact 보존, 신규 6행 중 5행 유지·`43:0` 1행 교정이다. `「지지 마!<br> 미술관 여러분!!<br> 우린 모두 너희 편이니까~!!」`로 호칭을 다듬었다. 전체 19행의 exact JP identity·태그·본문 34 UTF-16칸×3줄 검사와 기존 Novel `check_review`가 `READY_TO_APPLY`를 반환했다. 이는 의미검수와 기술 계약 통과이며 실기·배포 승격이 아니다.
- 이전 배치 `work/novels/battle-fragments-20261031/`는 10자리 Master ID를 가정한 미적용 이력으로 보존한다. 그 배치를 apply하지 않는다.
- 조사 근거: `work/text-coverage-20261031/battle-followup/`. native disassembly, 버전/해시, GitHub 이력, 승인본 원본, 현재 JP 대조, ID 충돌 검사, 장 연결 및 raw/state/실패 index 참조를 담았다.
- `unapproved-cache/`는 기존 소비 경로를 확인하는 격리 기술 검증 전용이다. 신규 초벌을 포함하므로 배포·설치할 패키지가 아니다.

격리 사본에서는 현행 `TranslationSession`/`TranslationCache`를 사용해 짧은 ID 10개·exact JP 19행 조회가 통과했고, 잘못된 10자리 Master ID 10개는 모두 미조회였다. HTTP 요청은 0회이며, 격리 package 2,120파일의 membership/hash 검사도 통과했다. 이 검증은 runtime 제품 변경 없이 기존 소스를 참조했다. 기존 `TranslationSession`의 CS4014 경고는 그대로이며 이를 이번 범위에서 수정하지 않았다.

초기 후속 조사에서는 사전 기록한 정본·Knowledge·설치 DLL/metadata/config 등 14개 해시가 모두 같았다. PRO 반환 후에는 Master/UI 교정과 그 manifest 변경만 별도로 허용하고, current Knowledge·Names·HOLD·DLL·설치본 해시와 선택한 10개 asset의 state·raw·index 참조 50개를 다시 확인했다. 이전 `unapproved-cache/` 조회 결과는 교정 전의 기술 이력이며 최종 전투 화면 확인을 대신하지 않는다.

반환 원본과 독립 적용 검증은 [PRO 검수 보존 폴더](../staging/text-coverage-20261031/pro-reviewed-20261031/)에 있다. 승인 19행은 staging에 보존했고 `translations/ko/novels/11.json` 등 10파일과 등록 manifest에는 추가하지 않았다. 다음 단계는 해금된 11장 전투에서 실제 호출·표시·이벤트 순서를 확인한 뒤, 기존 Novel check/apply 경로로 등록하는 것이다. 전체 Knowledge 재구축이나 runtime 변경은 필요하지 않다.
