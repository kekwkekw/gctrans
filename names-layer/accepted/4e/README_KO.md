# Names Layer 4-E accepted candidate

사용자가 수락한 1,070 exact key를 적용한 로컬 checkpoint다. `NAMES_ACCEPTED_1070.jsonl`의 top-level `accepted_ko` / `final_ko`가 이번 사용자 acceptance 결과다. `historical_semantic_record`, `historical_workbench_record`, `historical_qa_limitation`, `qa01_amendment_source`는 원본 객체와 동일한 과거 자료이며 그 안의 pending/proposed/final_ko=null을 현재 승인 상태로 해석하지 않는다.

`names/zh_Hans.json`은 **PRODUCTION_CANDIDATE**이며 runtime 검증·merge·배포는 수행하지 않았다. source key와 ledger 순서를 유지했다. serialization은 기존 `scripts/utils.py`의 UTF-8, ensure_ascii=false, indent=4, insertion order를 따른다.

## 읽기 순서

1. REPORT_KO.md 및 ACCEPTANCE_SUMMARY.json
2. ACCEPTANCE_EVENT.json / USER_ACCEPTANCE_RECORD_KO.md
3. QA01_AMENDMENTS_APPLIED.json / CONTEXT_LIMITATIONS_ACCEPTED_WITH_FLAGS.json
4. VALIDATION_4E.json / LIVE_DELTA_AUDIT.json
5. CURRENT_GITHUB_STATE.json / PR_STRATEGY.md / RUNTIME_GATE_CHECKLIST.md

## 독립 검증

Python 표준 라이브러리만 필요하다. ZIP을 풀고 다음을 실행한다. 경로는 checkpoint 최상위 디렉터리 기준이다.

```text
python -B tools/verify_accepted_names.py
```

검증기는 읽기 전용이며 출력 파일을 생성하지 않는다. 원본 ZIP도 함께 검사하려면 `--input-zip <4D0.zip> --input-zip <Batch09.zip> --input-zip <QA01.zip>`를 추가한다. 기본 검증도 고정된 원본 manifest SHA와 각 immutable source member hash를 대조한다. ZIP 자체의 expected SHA는 SOURCE_MANIFEST.json 및 검증기 PINS에 고정되어 있다. OUTPUT_MANIFEST.json은 자기 자신을 제외한 모든 파일을 포함한다.

live delta는 새 manifest GET 후 동일 SHA를 확인하고 기존 bundle cache 1,972개를 SHA 대조 및 전수 재파싱했다. 새 bundle GET은 0이다. audit 안의 과거 HTTP 200/fetch timestamp는 4-C2 cache의 원래 provenance이며 이번 새 다운로드 주장과 구분한다. game bundle 자체와 전체 story 본문은 이 ZIP에 없다.

`tools/reparse_current_universe.py`는 frozen/current 동일 snapshot 재현용 추출기다. Python 3.12, UnityPy 1.10.18, pycryptodome, httpx 0.27.2 및 검증된 repository source/cache가 필요하다. `--repo`, `--deps`, `--manifest live_delta/MANIFEST_SNAPSHOT.json`, `--cache`, `--run-dir`, `--workers 4`, `--offline`를 지정한다. cache/run-dir는 repository 밖이어야 한다. 원본 updater는 실행하지 않는다. signing key는 스크립트에 포함하지 않고 지정 repo의 기존 구현만 메모리에 로드한다. 새 snapshot용 범용 추출기라는 주장은 하지 않는다.

새 문맥 검수나 번역을 수행하지 않았다. 문맥 제한 6개의 표시명 수락은 evidence 완전성 승격이 아니다. Knowledge 상태·용어 scope·hold 상태·역사적 approval은 그대로다.
