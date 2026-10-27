# Master coverage 회복 경로

Master 원문은 `scripts/crawl_only.py --lane Master`로 취득한다. `--lane UI`는 지정 prefab/TextAsset을 읽는 별도 경로이며 Master corpus 갱신을 대신하지 않는다.

Coverage 기준은 `kristina1Q/gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`의 `translations/zh-Hans/master.json`에 있는 `(table, field, exact JP)` 32,592개다. 값은 버리며 중국어 번역을 한국어 초벌 입력으로 사용하지 않는다. 기존 한국어 전용 키도 보존한다.

```powershell
python scripts/crawl_only.py --lane Master --platform WebGL --acquire --master-coverage-baseline <local-master.json> --master-baseline-ref 0cdcd9e84fffa438dfdac63176685a6c47a07318 --output <work/scan.json>
python tools/update/master_coverage.py apply-draft --plan <work/master-coverage.json> --draft <work/recovery-draft.json> --package translations/ko --output <work/applied.json>
python tools/update/master_coverage.py plan --baseline <local-master.json> --baseline-ref 0cdcd9e84fffa438dfdac63176685a6c47a07318 --package translations/ko --source-snapshot <work/scan.json> --output <work/coverage-after.json>
```

원문 취득에는 기존 private crypto helper 환경 설정을 사용한다. helper와 raw cache는 배포하지 않는다. PC는 기존의 명시적 local asset/asset key 경로를 유지한다. WebGL 취득을 PC 최신 취득으로 표시하지 않는다.

초벌 파일은 plan의 `missing`과 정확히 같은 키를 갖는 JSON 배열이다. 각 행에 `table`, `field`, `jp`, `ko`를 넣는다. 숫자/태그/변수/줄바꿈 보존 검사 후 빠진 키만 추가한다. 기존 Master가 plan 이후 바뀌었으면 중단한다. `manifest.json`은 기존 빌더로 갱신한다. 중립적 AP·숫자 포맷·내부 식별자만 `disposition: keep_source`로 명시해 원문을 보존할 수 있다. 일본어를 그대로 넣어 coverage를 채우는 것은 허용하지 않는다.

`BASELINE_KEYS_PRESENT`는 local 키가 모두 존재한다는 뜻이다. 전체 의미 검수 완료나 현재 게임 전체 번역 완료를 뜻하지 않는다. 신규 초벌은 `CODEX_DRAFT_NOT_PRO_REVIEWED`로 표시한다.

2026-10-27 회복: 최초 HEAD의 누락 1,026개 중 이전 작업의 12개와 1,014개 추가로 키 누락 0개를 만들었다. 당시 총 32,685키였고, 1,014개 중 72개는 중립 원문 유지였다. 기존 2,248 gap은 A(local 존재) 817개, B(PC source 존재/local 없음) 1,431개로 재분류했다. `STATIC_SOURCE_RESIDUAL`은 번역 불필요 판정으로 사용하지 않는다.

2026-10-28 후속 검수: B의 남은 1,338개를 다시 판단하여 1,335개를 번역하고 3개를 근거와 함께 HOLD했다. 기존 초벌 1,014개를 JP/Names/현재 용어와 대조하여 66개 값을 수정했다(62개 교정, 4개 원문 문제 HOLD). 앞서 추가한 12개도 확인했다. 현재 Master는 34,020키이며 local 32,592키는 모두 존재하지만, 그중 4개는 추정 번역을 철회하여 JP를 유지한다. 따라서 키 존재와 번역 완료를 동일시하지 않는다. 신규분에는 현재 source의 표시용 필드 및 향후 공개될 레코드가 포함되며, 모든 레코드의 화면 노출을 관측했다고 주장하지 않는다.

신규 HOLD는 내부 trigger/target 설명 2개와 개발용 dummy 상품 설명 1개다. 기존 HOLD는 葵의 의미 불명 2개와 같은 record의 색상 name/description 충돌 2개다. 레코드·원문 해시·판정은 `staging/master-source-review-20261028/`에 있다. 임시 상품 설명 `C…_交換アイテム…`/`R…交換アイテム…` 104개도 번역했으며, source 잔여라는 이유로 제외하지 않았다.

기존 초벌 교정은 다음처럼 정확한 이전 값과 검수 처분을 명시한다.

```powershell
python tools/update/master_coverage.py apply-reviewed --plan <add-plan.json> --draft <new-reviewed.json> --reviews <prior-reviewed.json> --package translations/ko --output <review-result.json>
```

전체 입력을 먼저 검증하고, 현재 Master SHA 및 각 교정 행의 `before_ko`가 일치할 때만 반영한다. HOLD 원문 복원은 `HOLD_*` 처분·구체적 이유·source 레코드 근거를 요구한다. 이는 번역으로 세지 않는다. 일반 `apply-draft`의 JP 미번역 거부 규칙은 그대로 유지한다. 검수 주체는 Codex이며 PRO 승인으로 표시하지 않는다. 실기에서 확인된 가챠 목록/안내문 명칭 차이도 기존 명칭에 맞춰 15키 보정했다.
