# 공개 반영과 공급 경계

현행 운영 정본은 [OPERATOR_KO.md](OPERATOR_KO.md)다. 번역·검수·package 구조 검사를 마친 변경을 검토 가능한 branch와 PR로 만들며, main 병합은 사용자가 승인한 대상과 범위에 적용한다. tag, release asset, Pages/CDN 설정, Actions dispatch와 영구 로컬 설치는 각 요청의 범위에 따른다.

`tools/export_git_rc.py --repo <local-repo> --output <new-scratch/export> --ref <commit>`는 고정 commit의 blob을 Git archive로 읽고 파일 membership과 bytes를 검증한다. 현행 source, tools, tests, contracts, staging, 번역 정책·Knowledge, 번역 package, 후보 DLL, 운영 문서와 upstream 라이선스를 포함한다. 빌드 설정은 `source/build/Common.props`, MIT notice는 `THIRD_PARTY_LICENSES/GCMod-upstream-LICENSE`에 보존한다. 과거 구현·검수 기록은 Git history에서 복구한다.

raw/cache/state/audit/native report, 개인 launcher 기록, cfg backup, font, 게임·Utility·interop DLL, credential과 private helper는 공개 tracking/export에 넣지 않는다. 로컬 인계 자료는 공개 문서와 분리하고 명령 예시는 매개변수 경로를 사용한다.

현행 공급은 [LOCAL_PARITY_PIPELINE_KO.md](LOCAL_PARITY_PIPELINE_KO.md)의 `tools/local_release.py`를 사용한다. `translations/ko/release-manifest.json`은 authoring package의 파일·hash 검사 입력이다. DLL은 `source/GCMod.Candidate`에서 빌드하며 ZIP은 허용한 runtime 파일만 포함한다. 런타임은 offline 한국어 데이터와 독립 UI 사전을 읽는다.

설치는 게임 종료 상태에서 backup/journal과 함께 수행하며 다음 게임 시작부터 반영한다. UI live switching, coordinator, pending_restart/LKG runtime은 사용하지 않는다. 이전 Nextgen 공급 도구와 문서는 역사적 참고다. Novel 등록은 기존 명시 검수 범위에만 적용한다. 공개 source에는 익명화된 검증 요약을 넣을 수 있지만 개인 Native 로그·스크린샷·경로·raw는 제외한다.

`.github/workflows/crawl_only.yml`은 manual/read-only metadata snapshot이다. fresh baseline은 이전 대비 변경 없음과 다르며, 7일 CI artifact를 지속 baseline 정본으로 간주하지 않는다. 평소 증분은 같은 로컬 state/raw root를 유지한다. scan의 metadata 관측은 raw 취득·PC 동등성·의미 검수 승인을 대신하지 않는다.

공급 bytes를 바꾸는 번역 변경과 저장소 정리를 구분해 diff 및 검증 결과에 명시한다. 저장소 정리에서는 `translation/`, `translations/ko/`, 후보 DLL, `contracts/`, `staging/`과 6.1 호환 데이터의 membership·해시를 보존한다. native·game 시험의 수행 여부와 기존 Known Residual은 별도로 보고한다.
