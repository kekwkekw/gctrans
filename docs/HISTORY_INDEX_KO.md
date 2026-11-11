# 과거 자료와 복구 위치

2026-11-02 정리 직전 main은 `e6730d5ce82165c54183c2f50f7d448311e71d45`다. 아래 고정 링크는 branch 삭제와 무관하게
main의 Git 이력에서 원본을 가리킨다. Git history를 rewrite하거나 기존 release를 삭제하지 않았다.

| 기록 | 고정 원본 |
|---|---|
| 2026-10-24 current Knowledge 정리: 원장별 매핑·계약·보고서 | [폴더](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/knowledge-layer/provenance/20261024_current_knowledge_cleanup) |
| 1,499편 PRO 검수: 결정·승인·해시·적용 보고 | [폴더](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/knowledge-layer/provenance/20261024_full_novel_current_knowledge_pro) |
| Novel 줄배치: wording, reflow, boundary | [wording](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/staging/novel-layout-wording-20261030), [reflow](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/staging/novel-layout-reflow-20261030), [boundary](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/staging/novel-layout-boundary-20261030) |
| 완료된 UI exact·강화·주간·공방·점수 도전 조사 | [당시 staging 전체](https://github.com/kekwkekw/gctrans/tree/e6730d5ce82165c54183c2f50f7d448311e71d45/staging) |
| 과거 Nextgen 후보 재현 명령 | [당시 REPRODUCE](https://github.com/kekwkekw/gctrans/blob/e6730d5ce82165c54183c2f50f7d448311e71d45/docs/REPRODUCE_KO.md) |
| GCMod 6.1 소스·MIT 고지와 CDN 데이터 | [소스](https://github.com/kekwkekw/gctrans/tree/ae8fc9c0d4875f36c3f3cb89bf219d7b12459046/GCMod-cs%ED%8C%8C%EC%9D%BC%EB%B0%B1%EC%97%85/GCMod-6.1.0-updated), [Names](https://github.com/kekwkekw/gctrans/tree/ae8fc9c0d4875f36c3f3cb89bf219d7b12459046/names), [Words](https://github.com/kekwkekw/gctrans/tree/ae8fc9c0d4875f36c3f3cb89bf219d7b12459046/words), [Novel](https://github.com/kekwkekw/gctrans/tree/ae8fc9c0d4875f36c3f3cb89bf219d7b12459046/novels) |
| 지원 종료 직전 트리의 Nextgen 소스·도구·테스트 | [고정 커밋](https://github.com/kekwkekw/gctrans/tree/ae8fc9c0d4875f36c3f3cb89bf219d7b12459046) |
| 이전 Native·Stable 정책 | [Native](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/PROJECT_NATIVE_TEST_POLICY_KO.md), [Stable](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md) |
| 2026-10-26 및 11-02 저장소 정리 | [10월 26일](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/REPOSITORY_CLEANUP_20261026_KO.md), [11월 2일](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/REPOSITORY_CLEANUP_20261102_KO.md) |
| 텍스트 coverage·재조사·반영 및 AP 확장 | [Master/UI 750행](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/TEXT_COVERAGE_20261031_KO.md), [전체 텍스트 재조사](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/FULL_TEXT_RESURVEY_20261102_KO.md), [고정 92행 반영](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/TEXT_RESURVEY_FOLLOWUP_20261102_KO.md), [AP 확장](https://github.com/kekwkekw/gctrans/blob/5188bde0c7bec186c195ecb781a78d1bd8f03282/docs/UI_AP_EXACT_20261101_KO.md) |
| staging 정리 직전 원본과 일회성 검사 | [staging](https://github.com/kekwkekw/gctrans/tree/5188bde0c7bec186c195ecb781a78d1bd8f03282/staging), [tests](https://github.com/kekwkekw/gctrans/tree/5188bde0c7bec186c195ecb781a78d1bd8f03282/tests) · `5188bde0c7bec186c195ecb781a78d1bd8f03282` |

6.1 자료는 지원 종료 직전 커밋에서 확인한다. Nextgen 재현은 당시 REPRODUCE가 가리키는 커밋을 별도 checkout에서 사용한다. 파일 하나를 확인하거나 별도 폴더로 복구할 때는 다음 명령을 사용한다.

```powershell
git show e6730d5ce82165c54183c2f50f7d448311e71d45:staging/novel-layout-wording-20261030/apply.json
git archive --format=zip --output=<new-history.zip> e6730d5ce82165c54183c2f50f7d448311e71d45 -- knowledge-layer/provenance staging
```

현재 corpus 적용 영수증과 볼칸 정책 해소 목록은 [knowledge-layer](../knowledge-layer/README.md)에 남긴다.
미해결 Master·UI·전투 Novel과 crawler의 공개 근거는 [staging 안내](../staging/README.md)에서 확인한다.
과거 검수본의 승인·정적 검사·Git 적용·Native 검증은 서로 다른 상태이며 현재 runtime의 전체 PASS로 승계하지 않는다.

현재 Knowledge에 남은 과거 출처 경로는 위 commit에서 해석한다. 삭제된 원장을 현재 입력으로 되살리거나
현행 record의 scope를 없애는 근거로 사용하지 않는다.
