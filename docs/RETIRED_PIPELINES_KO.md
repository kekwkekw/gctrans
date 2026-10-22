# 번역 경로의 현재 사용 범위와 퇴역 이력

## 현재 Novel 경로

정상 Novel 작업은 `tools/update/cli.py novel`의 prepare → review → apply로 실행한다. 정책·현재 Knowledge·Names와 실제 명령은 [OPERATOR_KO.md](OPERATOR_KO.md)에 모았다. 이 경로는 private workbench를 요구하지 않는다.

기존 `bind-intake-wb`와 `apply-review`의 WB Novel adapter는 이미 만들어진 산출물의 호환·재현용으로 보존한다. 과거 snapshot·receipt·원장을 현행 번역의 필수 중간 단계로 요구하지 않는다. 기존 private repository의 내용이나 설정은 이 변경에서 수정하지 않는다.

## 2026-10-17 퇴역 기록

아래의 승인·보존 설명은 당시 변경 범위다.

기준: PR26 병합 `fd84842efec881deebe7399c709e0a678937b57a`. 2026-10-17 사용자가 퇴역과 기존 시나리오 표시 교정의 repository 수정·main 병합을 명시 허용했다. 이 문서는 그중 **퇴역** 범위를 기록한다. Desktop·설치 디렉터리를 조사하거나 삭제하지 않는다.

## 삭제한 15경로

- `run_all.bat`: 자동 pull/autostash → 구 수집/LLM/merge → 전체 add/commit/push 진입점.
- `scripts/run.py`, `scripts/update.py`: 구 Updater 진입점과 gt_input/quest writer.
- `scripts/gpt.py`, `scripts/translate_script.py`: 구 이름/문장 LLM 번역 경로. 현행 Names/Knowledge 검수 경로가 아니다.
- `scripts/merge.py`: 구 번역 캐시의 direct dictionary writer.
- `scripts/parse.py`, `scripts/utils.py`: 위 구 Updater/merge만 사용하는 보조 코드. 현행 scanner는 `tools/update/source_scan.py`의 명시 extractor를 사용한다.
- `scripts/requirements.txt`: 위 구 실행 환경의 dependency 목록. 현행 metadata workflow는 필요한 httpx를 명시하고 현재 source extractor dependency는 operator 문서를 따른다.
- `app.js`, `package.json`, `package-lock.json`, `vercel.json`: upstream으로 redirect하는 과거 Express/Vercel wrapper와 그 전용 실행 환경. 현재 GitHub Pages의 raw data 공급이나 GCMod updater가 아니다.
- `项目GPT字典.txt`: 구 Updater가 소비하던 JP→ZH glossary. 한국어 Names/Knowledge의 정본이 아니다.
- `!!!!!!!!!!!!!!!!!!!최근로그.txt`: 위 구 실행 경로의 로그 사본.

위 파일은 실행하지 않고 repository의 실제 참조·import·exporter·workflow를 대조해 삭제했다. 파일명에 legacy가 들어간다는 이유로 삭제하지 않는다. 외부 Vercel 계정·hosting 설정 변경이나 Git 이력 재작성은 하지 않는다.

## 보존한 현행 경로

`tools/update/cli.py`와 `scripts/crawl_only.py`의 scan/diff/intake/WB bridge/review/apply 연결, `scripts/crypto.py`의 private-helper 경계, `source/`·`tests/`·`contracts/`·`donor_recovery/`·명시 62-table profile은 보존한다. `.github/workflows/crawl_only.yml`은 metadata-only/manual/read-only로 유지하고, 이미 퇴역한 `update_translation.yml`의 안내-only 정의도 유지한다.

`translations/ko/` 전체, legacy `novels/`·`names/`·`words/`·`dicts/` CDN, Names/Knowledge·R1–R36/후속 수락 archive와 원장은 보존한다. 기존 6.1 호환 파일명은 삭제 근거가 아니다. 과거 GCMod source backup도 이 구 번역 파이프라인 삭제 범위에 넣지 않는다. WB13–M4 및 별도 workbench repository는 변경하지 않는다.

현행 operating exporter의 scripts allowlist는 `__init__.py`, `crawl_only.py`, `crypto.py`다. 현재 scanner와 source tools는 삭제한 parse/utils를 import하지 않는다. metadata workflow의 HTTP-only dependency 설치도 유지된다. 진행된 수락본·HOLD·미등록 Novel 등록 상태와 실제 개인 Stable은 변경하지 않는다.

## 검증과 한계

현재 Git tree의 source/tools/tests/contracts/staging와 raw package를 기준으로 검사한다. 단순 문자열 참조 검색 외에 현행 scanner direct/module/wrapper와 saved-source/fake-transport 회귀를 실행한다. 삭제 파일을 실행하는 시험은 없다.

이번 검증은 repository 소스·Python·패키지 보존 검사다. 외부 스케줄러·Desktop·사용자 PC의 live 프로세스·Stable 설치를 조사한 결과가 아니며, 그런 조사 없이 repository 범위의 퇴역을 수행하도록 한 사용자 지시를 따른다. Game 원본·font·private helper·credential를 이 변경으로 업로드하지 않는다.

시나리오 교정은 별도 변경이다. 기존 18,366개 표시 결정을 적용하더라도 새 번역·의미 재판정이 아니며, Novel 변경은 완전 package 재조립 및 restart 대상이다. UI-only live request로 처리하지 않는다.
