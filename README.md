# Girls' Creation 한국어 패치 · gctrans

**LLM 번역·의미검수·지식 관리를 실제 게임 배포로 연결하는 현지화 프로젝트입니다.**
GCMod로 시나리오·UI·Master·지원 이미지를 표시하고, Codex 초벌과 ChatGPT PRO 검수 결과를
원문·문맥·적용 범위에 맞춰 관리합니다. AI가 생성한 번역과 게임에 반영할 승인본을 구분합니다.

## 설치

**[GCMod 7.3.1 전체 설치본](https://github.com/kekwkekw/gctrans/releases/tag/v7.3.1)**에서
`GCMod-7.3.1-ko-full-win-x64.zip`을 받으세요. GitHub의 Source code ZIP은 설치 파일이 아닙니다.

1. 게임을 종료합니다.
2. 신규 사용자는 ZIP 내용물을 `GC.exe`가 있는 폴더에 복사합니다.
3. 평소 사용하는 DMM 실행 경로로 게임을 시작합니다. 최초 실행에는 인터넷 연결이 필요합니다.

BepInEx·.NET 런타임·Utility·한국어 폰트·번역 데이터를 포함하므로 Python이나 기존 패치는 필요하지 않습니다.
동일한 로더·Utility·한국어 폰트를 사용하는 기존 7.x 사용자는 업데이트 ZIP을 사용할 수 있습니다.
게임을 종료하고 기존 GCMod 폴더와 cfg를 백업한 뒤 플러그인 파일을 갱신합니다.
**기존 `BepInEx/config/GCMod.cfg`는 보존하세요.** cfg가 없을 때만 패키지 기본 cfg를 설치합니다.
구형 패치의 전체 재설치와 복구 절차는 릴리스의 `INSTALL_KO.txt`를 따르세요.

시작 시 GitHub main의 manifest를 확인해 변경된 Master·UI·Names·지원 이미지를 캐시에 갱신합니다.
Novel은 해당 시나리오 진입 시 필요한 데이터를 로드합니다. **DLL·폰트는 자동 갱신 대상이 아닙니다.**
상세 동작은 [데이터 갱신](docs/DATA_UPDATES_KO.md), 이미지 지원 범위는 [이미지 갱신](docs/IMAGE_UPDATES_KO.md)에 있습니다.

7.3.1은 정상 시작 시 로드 완료 카드를 표시하지 않습니다. 패치 로드 오류가 발생하면 오류 카드가 표시됩니다.
7.3.0은 게임 옵션에서 일반/CG 대화창 불투명도·가독성 보정·30/60 FPS를 조절합니다.
미리보기 후 설정 저장을 누르면 유지되며, 미저장 변경을 폐기하면 저장값으로 돌아갑니다.
GCMod 전용 단축키는 없습니다. cfg나 번역 파일을 직접 수정한 후에는 게임을 재시작하세요.
[7.3.0 변경 및 검증 범위](docs/RELEASE_7_3_0_KO.md)를 확인할 수 있습니다.

## 작업 흐름

```text
게임 원문 수집 → current Names·Knowledge로 문맥 준비
             → Codex 초벌·지식 후보 → ChatGPT PRO 의미검수
             → 승인 위치만 적용 → UI export·package 검사 → GitHub 배포
```

Novel·Master·UI는 기존 파일 구조를 유지합니다. 통합 corpus는 서로 다른 위치의 번역을 함께
조회·검수하는 도구이며, 같은 일본어를 무조건 같은 한국어로 치환하지 않습니다.
Knowledge는 명시된 scope 안에서 사용하고, 과거 원장과 변경 이력은 정상 번역 입력에서 제외합니다.
새 Novel 본문에는 34 UTF-16칸 × 최대 3줄과 태그·변수 보호 규칙을 적용합니다.

| 살펴볼 내용 | 진입점 |
|---|---|
| 전체 운영·역할·신규 Novel 번역 | [OPERATOR_KO.md](docs/OPERATOR_KO.md) |
| corpus 교정·Knowledge 변경·줄배치 규칙 | [CORPUS_WORKFLOW_KO.md](docs/CORPUS_WORKFLOW_KO.md) |
| Master/UI 수집·검수·적용 | [TRANSLATION_PIPELINE_KO.md](docs/TRANSLATION_PIPELINE_KO.md) |
| 전 자산 crawler와 남은 범위 | [CRAWLER_ALL_ASSETS_KO.md](docs/CRAWLER_ALL_ASSETS_KO.md) |
| 게임 runtime 구조·빌드 | [GCMod.Candidate](source/GCMod.Candidate/README_KO.md) |
| 전체 문서·현재와 과거 자료 구분 | [문서 안내](docs/INDEX.md) |

## 저장소 구조

| 경로 | 역할 |
|---|---|
| `translations/ko/` | 게임이 읽는 Novel·Names·Master·UI·이미지 및 UI 작성 원본 |
| `translation/` | current Knowledge, 콘텐츠 HOLD와 번역 정책 |
| `tools/`, `scripts/`, `contracts/`, `tests/` | 수집·검수·반영·패키지 도구와 회귀 검사 |
| `source/GCMod.Candidate/`, `candidate/` | GCMod runtime 소스와 DLL·cfg |
| `knowledge-layer/provenance/` | 승인 변경의 이전·이후 값과 정책 해소 이력. 번역 입력 아님 |
| `staging/` | [미해결 검수·후속 근거](staging/README.md) |
| `work/`, `source-state/`, `private-source-cache/`, `source-out/` | 로컬 작업·원문·캐시. 공개 저장소 밖에서 관리 |

지원 런타임은 GCMod 7.x입니다. GCMod 6.1 지원을 종료했으며, 기존 설치에서 이 저장소의 6.1 CDN 경로를 사용했다면 GCMod 7.x로 업데이트해야 합니다. [과거 소스와 데이터](docs/HISTORY_INDEX_KO.md) · [출처와 라이선스](docs/UPSTREAM_ATTRIBUTION_KO.md)

## 현재 범위와 한계

2026-11-09 이 tree 기준: Novel 사전 1,939개, Master exact 34,388개, UI exact 14,075개,
이미지 매핑 165개, current Knowledge 3,535개입니다. UI 수에는 유한 숫자·시간 범위에서 생성한
exact도 포함합니다. 파일·키 수는 전편 신규 번역량이나 전수 의미검수·실기 완료 수가 아닙니다.

통합 corpus 정비와 볼칸 표기 정책은 완료했고 이후 변경은 증분 검수합니다.
**Crawler 보완 캠페인은 완료**했습니다. 전 자산 선택·취득·추출, raw·실패 근거 보존,
텍스트 재조사와 coverage 측정을 갖췄습니다. 미해석 객체·서식·크레딧·전투 실기는
[종료 기록과 재개 조건](docs/CRAWLER_CLOSEOUT_20261103_KO.md)에 남겼습니다.
영문 UI 3종은 사용자 선택으로 번역하지 않습니다. 전체 완역이나 모든 에셋의 해석 성공을 뜻하지 않습니다.

[지원 범위](docs/SUPPORT_SCOPE_KO.md) · [재현](docs/REPRODUCE_KO.md) ·
[현재 자료와 과거 기록](docs/HISTORY_INDEX_KO.md)
