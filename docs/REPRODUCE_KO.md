# 현행 검사 재현

저장소 루트에서 실행한다. 변경한 경로에 필요한 기존 검사만 선택하며, 모든 명령을 매번 필수 gate로 만들지 않는다.
Python 3.12 이상을 사용하고 외부 dependency는 [별도 안내](EXTERNAL_DEPENDENCIES_KO.md)를 따른다.

## 문맥·검수 도구

```powershell
python -m unittest discover -s tests -p test_translation_knowledge.py
python -m unittest discover -s tests -p test_corpus_consistency.py
python -m unittest discover -s tests -p test_novel_workflow.py
python -m unittest discover -s tests -p test_novel_cli_integration.py
```

임시 fixture에서 current-only 문맥, 승인 변경, preimage·scope, 줄배치·보호 토큰과 기존 배치 호환을 검사한다.
실제 정본을 재번역하거나 게임을 실행하지 않는다.

## UI export·package

```powershell
python tools/export_local_ui.py --check
python tools/local_release.py verify
```

`local_release.py verify`가 현행 source package의 inventory·해시·이미지·UI export를 확인한다.
더 넓은 Master/UI/crawler 및 CPU runtime 회귀가 필요할 때는 CI와 같은 명령을 사용한다.
.NET 9 SDK와 `httpx==0.27.2`, `UnityPy==1.10.18`이 필요하다.

```powershell
python tools/verify_translation_update.py --output work/translation-update/check.json
```

`verify_translation_update.py`는 게임 참조 DLL을 사용하는 빌드나 실제 Native 화면 확인을 하지 않는다.
코드를 수정한 DLL 빌드·패키지·설치는 [GCMod 한국어 업데이트 절차](TRANSLATION_PIPELINE_KO.md)를 따른다.

## 문서·정리 확인

```powershell
python -m unittest discover -s tests -p repository_layout_tests.py
python -m unittest discover -s tests -p retirement_scope_tests.py
git -c core.whitespace=cr-at-eol diff --check
```

Git export 시험은 현재 커밋의 파일을 확인한다. 미커밋 변경의 검증과 구분한다.
`tools/export_git_rc.py`는 고정 commit의 운영 파일을 새 폴더로 export하며 개인 raw/cache를 포함하지 않는다.

이전 Nextgen/NG08~NG11 공급·시험 명령은 [과거 기록](HISTORY_INDEX_KO.md)에서 확인한다.
현행 게임 DLL에 포함되지 않는 이전 후보의 CPU 시험을 GCMod 한국어 업데이트 실기 검증으로 쓰지 않는다.
