# 볼칸 표기 정책과 통합 corpus 캠페인 종료

기준은 PR #52가 병합된 `3c3b33c6aedd5784bcae07c5a23fbba2a4074903`이다. 2026-10-31 사용자가 `ヴォルカン地区 → 볼칸 지구`, 같은 지구의 축약형 `ヴォルカン → 볼칸`을 확정했다. 이전 PRO 검수의 동일 지구 식별 근거와 이번 사용자 정책을 결합한 종료 배치이며, 새로운 독립 PRO 검수를 했다고 주장하지 않는다.

## 반영 결과

현재 정본에서 새 scan/prepare를 수행했다. writable 87개는 기존 정책 보류 10개와 전체 캠페인 보류 77개의 합집합에 정확히 일치했다. 적용이 끝난 과거 batch는 재사용하지 않았다.

| 종류 | 교정 | 유지 | 합계 |
|---|---:|---:|---:|
| Master | 8 | 31 | 39 |
| Novel | 0 | 42 | 42 |
| UI authoring | 0 | 6 | 6 |
| 합계 | 8 | 79 | 87 |

8개 교정은 `mItems.ml_description[]` 4개, `mItems.ml_flavor_text[]` 3개, `mNovelKeyWords.ml_keyword[]` 1개다. 같은 지구의 `볼캉`만 `볼칸`으로 고쳤다. 나머지는 이미 올바른 표기여서 주변 문장과 제어 블록을 보존했다. pending 및 이 정책의 hold는 0개이며, current corpus에서 `볼캉` 번역 잔존은 0개다. 이는 과거 이력·검수 원본까지 검색·치환했다는 뜻이 아니다.

기존 `term:4cc609021faf8013` 한 건을 같은 ID로 수정했다. 지명 표기만 global로 승인하고, 축약형은 같은 지구를 가리킬 때만 사용하도록 명시했다. 기존 source evidence와 수정 전 scope는 변경 이력에 보존했다. Knowledge 수는 3,535개이며 다른 3,534개는 동일하다. entity·관계·사건·말투 record는 수정하거나 전역화하지 않았다.

## 점검 결과

- 기존 corpus apply를 한 번 실행했다. 직접 작성 변경은 Master와 Knowledge 두 파일, 파생 변경은 manifest 네 파일이다.
- 기존 exporter를 실행했으며 `ui.json` 11,586개 결과는 바이트 단위로 동일하다. UI를 직접 편집하지 않았다.
- 기존 패키지 검사: 2,110개 파일, 60,204,646바이트, `EXACT_RUNTIME_WITH_AUTHORING` PASS.
- 데이터 적용 직후 기존 tracked 4,474개 중 데이터·metadata 6개만 바뀌었다. 이후 이 종료 문서와 운영 문서 링크 및 정책 해소 목록을 추가했다. Names, 모든 Novel, 콘텐츠 HOLD, DLL, 구현 코드와 이전 corpus 교정을 보존했다.
- 일반 Novel prepare를 10010104·10010107에 실제 실행해 갱신한 term의 `approved_within_declared_scope`와 `gc-novel-34x3-v1` 규칙을 확인했다. 이는 기존 JP-key snapshot으로 prepare한 확인이며, 신규 시나리오 번역·apply를 수행한 것은 아니다.
- 전체형과 축약형 모두 작은 context 예산에서도 필수 승인 용어로 전달됐다. 관계·사건 지식의 추출 완전성을 보증하는 시험은 아니다.
- GCMod 7.2.1에서 별도 로컬 패키지로 대표 실기 확인: 용어 사전의 `볼칸 지구`, Novel 10010104 본문의 지명, 로그의 `<click=16>` 지명 링크와 연결된 용어 팝업이 정상 표시됐다. 확인한 화면에서 잘림·태그 노출이 없었다.
- 게임을 종료한 뒤 테스트 전 GCMod 설정을 해시 일치로 복원했다. 재화 사용은 없었다. DLL 교체나 설치본 번역 파일 덮어쓰기는 하지 않았다.

## 종료 판단과 범위

**현재 정본 대상의 통합 corpus·Knowledge 대규모 정비는 종료 가능한 상태다.** 기존 전체 캠페인의 마지막 정책 보류 87개가 해소됐고, 실제 Knowledge 수정 → 이력 보존 → 다음 일반 Novel prepare의 재사용도 기존 경로에서 확인했다. 이번 작업에 추가 runtime 계층이나 새 검증 gate를 도입할 이유는 없다.

이 판단은 모든 미래 업데이트의 번역·Knowledge가 영구적으로 완성됐다는 뜻이 아니다. 같은 JP의 문맥별 다른 KO는 계속 허용한다. 이후에는 `source update → prepare → 초벌·지식 후보 → 의미검수 → apply`의 일반 증분 유지보수로 진행한다. 34칸 규칙은 픽셀 단위 화면 적합성 보증이 아니며, 이번 실기는 대표 화면 확인이다.

이 문서는 로컬 반영·검증 결과다. Git 병합과 원격 배포 여부는 별도 소스 관리 상태로 확인한다. 기존에 승인받은 PR #52 병합과 이번 후속 배치의 공개 반영을 혼동하지 않는다.

## 근거

- [적용 영수증: 8개 교정과 Knowledge before/after](../knowledge-layer/provenance/corpus-changes/2c6a1bc233ad62b36e4f101b0ba73459cc53083f229ed7ae512a73f38d8db6b9.json)
- [87개 보류 해소 위치 목록과 검증 요약](../knowledge-layer/provenance/corpus-policy-resolutions/20261031_volcan.json)
- 로컬 증빙 ZIP: `work/corpus/GCMod_Corpus_Volcan_Closure_20261031.zip` — fresh batch, 실제 판정, 해시 대조, 패키지 결과, prepare 확인, 실기 로그·화면, 내부 체크섬.

과거 hold 결정은 역사적 근거로 그대로 보존하며, 위 종료 목록이 동일 occurrence ID의 정책 해소를 기록한다. provenance는 정상 번역 입력으로 사용하지 않는다.
