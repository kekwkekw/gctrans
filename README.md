# 걸크리 PC 한국어화

## 설치 및 다운로드

**[GCMod 7.1.0 전체 설치본](https://github.com/kekwkekw/gctrans/releases/tag/v7.1.0)**의
`GCMod-7.1.0-ko-full-win-x64.zip`을 사용한다. 신규 사용자는 게임을 종료하고 ZIP의 내용물을
`GC.exe`가 있는 폴더에 복사한 뒤 평소 사용하는 DMM 실행 경로로 시작한다.
필요한 BepInEx·.NET 런타임·Utility·한국어 폰트·번역 데이터를 포함하며 Python이나 기존 패치는 필요하지 않다.
최초 실행에는 인터넷 연결이 필요하다.

기존 사용자는 **이전 패치 제거 후 전체 설치본으로 재설치**를 권장한다.
설정과 다른 모드를 게임 폴더 밖에 백업한 뒤 `BepInEx/`, `dotnet/`, `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version`만 제거한다. 게임 원본과 DMM 데이터는 제거하지 않는다.
자세한 절차는 릴리스의 `INSTALL_KO.txt`를 따른다. GitHub의 자동 Source code ZIP은 설치 파일이 아니다.

7.1.0은 시작 시 GitHub main의 번역 manifest를 확인하고, 변경된 UI·Master·Names와
진입한 Novel을 로컬 캐시에 갱신한다. DLL·폰트는 자동 갱신하지 않으므로 6.1/7.0 사용자는
7.1.0 설치가 필요하다. 이미지와 일부 미번역/HOLD가 남아 있으며 전체 완역은 아니다.

현재 작업 절차의 단일 진입점은 **[docs/OPERATOR_KO.md](docs/OPERATOR_KO.md)**다. 명령은 저장소 루트에서 실행한다.

## 정상 Novel 흐름

> 업데이트 확인·원문 정리 → Codex 초벌 → ChatGPT PRO 의미검수 → Codex 적용·package 검증 → 승인된 main 반영

- 현재 Names: [`translations/ko/names.json`](translations/ko/names.json)
- 현재 Knowledge: [`translation/knowledge.jsonl`](translation/knowledge.jsonl)
- HOLD 정책: [`translation/HOLD_POLICY.md`](translation/HOLD_POLICY.md)
- 현재 package: [`translations/ko/release-manifest.json`](translations/ko/release-manifest.json)

정상 Novel 경로는 public main의 도구만 사용하며 private workbench나 과거 Knowledge snapshot을 요구하지 않는다.

## UI·runtime

- 7.2.0 이미지/한 글자 UI 후보: [docs/IMAGE_UPDATES_KO.md](docs/IMAGE_UPDATES_KO.md). 공개 설치본과 별도로 검증 중이다.

- 데이터 자동 갱신 동작 및 Native 검증 결과: [docs/DATA_UPDATES_KO.md](docs/DATA_UPDATES_KO.md).

- Master/UI 수집→검수→반영→빌드·패키지·설치: [`docs/LOCAL_PARITY_PIPELINE_KO.md`](docs/LOCAL_PARITY_PIPELINE_KO.md). 여러 UI prefab 수집, 부분 실패 격리, 실제 runtime 동적 조회 확인을 지원한다.

- UI 사전 생성·포함 범위: [`docs/UI_EXPORT_KO.md`](docs/UI_EXPORT_KO.md). 비활성 binding도 검수된 번역은 export하며 local UI 키 coverage를 검사한다.

- 2026-10-27 현재 runtime: [`source/GCMod.Candidate/README_KO.md`](source/GCMod.Candidate/README_KO.md)의 **Korean LocalParity**. `gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318` 기반이며 `candidate/GCMod.dll`로 빌드한다. Master `*`, 독립 exact `ui.json`, 기존 Novel/Names direct hook을 사용한다.
- 기존 Nextgen/store/live-switch/ownership 관련 문서와 Native 도구는 이전 후보의 기록이다. LocalParity의 적용·실행은 아래 LAUNCH 문서와 Candidate README를 우선한다. Novel authoring 절차는 그대로 유지한다.
- 지원 범위: [`docs/SUPPORT_SCOPE_KO.md`](docs/SUPPORT_SCOPE_KO.md)
- 외부 입력: [`docs/EXTERNAL_DEPENDENCIES_KO.md`](docs/EXTERNAL_DEPENDENCIES_KO.md)
- 적용·복구: [`docs/LAUNCH_PROCEDURE_KO.md`](docs/LAUNCH_PROCEDURE_KO.md)
- Stable 정책: [`docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md`](docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md)
- native 시험 정책: [`docs/PROJECT_NATIVE_TEST_POLICY_KO.md`](docs/PROJECT_NATIVE_TEST_POLICY_KO.md)
- 재현: [`docs/REPRODUCE_KO.md`](docs/REPRODUCE_KO.md)
- 공개 경계: [`docs/PUBLICATION_KO.md`](docs/PUBLICATION_KO.md)
- 출처·라이선스: [`docs/UPSTREAM_ATTRIBUTION_KO.md`](docs/UPSTREAM_ATTRIBUTION_KO.md)

## GCMod 6.1 호환

기존 6.1 사용자를 위해 `names/`, `words/`, `novels/`의 `zh_Hans.json` 경로와 `GCMod-cs파일백업/GCMod-6.1.0-updated/`를 유지한다. 현행 GCMod 7 정본은 `translations/ko/**`다.
