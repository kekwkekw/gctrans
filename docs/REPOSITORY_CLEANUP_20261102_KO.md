# 2026-11-02 저장소 정리

기준 main은 `e6730d5ce82165c54183c2f50f7d448311e71d45`다. 10월 26일 정리 이후 추가된 335개 파일과
남은 이전 Knowledge 자료를 확인하여, 현재 운영 입력·진행 중 조사·완료 기록을 구분했다.
게임 runtime과 번역 데이터의 변경은 포함하지 않는다.

## 정리 범위

| 항목 | 처리 |
|---|---|
| 10월 24일 Knowledge 정리·1,499편 검수 자료 21개 | 완료된 원장 매핑·검수 원본·보고서는 고정 Git 이력으로 보존 |
| 완료된 Novel 줄배치·UI 조사 자료 47개 | 현재 tree에서 제거하고 과거 자료 안내로 연결 |
| UI 회귀 검사가 실제 읽는 검수 파일 2개 | bytes 그대로 `tests/fixtures/`로 이동하고 검사 경로 갱신 |
| current Knowledge 3,535개 | 그대로 유지. 과거 원장으로 재생성하지 않음 |
| corpus 변경 영수증 5개·정책 해소 목록 1개 | `knowledge-layer/provenance/`에 유지 |
| crawler·Master coverage·미해결 HOLD·전투 Novel 자료 | 보수적으로 유지. 미적용 자료를 승인본으로 처리하지 않음 |
| 런타임·수집·검수·반영 도구, 번역 정본·DLL·6.1 CDN | 그대로 유지 |

실제 제거는 68개 파일, 18,260,980바이트다. 파일 이동 2개는 이 수량에 포함하지 않는다.
Git 이력과 기존 릴리스는 보존하므로 checkout의 중복 자료는 줄지만 저장소의 과거 이력 용량을 줄이는 작업은 아니다.
정리 전 제거·이동 대상 70개를 별도 로컬 ZIP으로 보존하고 각 파일의 bytes와 CRC를 확인했다.
공개 복구 위치는 [HISTORY_INDEX_KO.md](HISTORY_INDEX_KO.md)에 고정 commit 링크로 남겼다.

## 현행 진입점

- README: 설치 버전을 7.2.1로 갱신하고, 작업 흐름·저장소 구조·현재 수치와 한계를 정리했다.
- [문서 안내](INDEX.md): 현행 운영, 미완료 crawler, 완료 기록, 이전 Nextgen 정책을 구분했다.
- [Knowledge 안내](../knowledge-layer/README.md): current와 변경 이력의 역할 및 과거 출처 경로 해석을 명시했다.
- [staging 안내](../staging/README.md): 유지할 실행·검사 입력과 진행 중 근거를 구분했다.
- `/work/` 전체를 Git 제외하여 이후 로컬 초벌·캐시·실기 증거가 작업별로 누적 tracking되지 않도록 했다.

`source/GCMod.Nextgen/`은 현재 게임 DLL에 포함되지 않지만 오프라인 authoring 도구의 의존성이므로 유지한다.
현재 번역의 `origin.source_map_ref`는 과거 출처 메타데이터다. 원본은 고정 commit에서 확인하며,
경로 정리를 이유로 current Knowledge의 내용·scope를 수정하거나 legacy를 다시 입력에 섞지 않았다.

## 브랜치

main에 포함된 완료 원격 브랜치 9개는 head SHA와 복구 bundle을 보존한 뒤 정리했다.
`main`, 진행 중인 `feat/all-assets-crawler`, 이번 정리 PR 브랜치는 유지한다.
다른 로컬 worktree에서 checkout 중인 브랜치와 미커밋 crawler 자료는 보존한다.
알려진 작업본에서는 사용 중이지 않은 병합 완료 로컬 브랜치 3개도 `git branch -d`로 정리했다.
브랜치 삭제는 main의 커밋 이력이나 릴리스를 삭제하는 작업이 아니다.

## 검증

기존 6개 suite의 81개 테스트, UI export 일치, `local_release.py verify`, `git diff --check`를 통과했다.
변경 전 tracked 파일 4,501개 중 4,418개는 같은 SHA-256이며, 70개는 위 제거·이동 대상,
13개는 문서·Git 제외 설정·검사 경로와 옛 빌드 구조 가정의 갱신이다. 신규 안내 문서와 fixture는 별도다.
원래 작업본에서 조사 시작 시 기록한 미커밋 crawler 파일 12개의 해시도 유지됐다.

최초 테스트 시 Windows sandbox 임시 디렉터리의 접근 거부가 있어 정상 권한으로 다시 실행했다.
기존 문서 검사의 `Common.props` 직접 import 가정은 현재 runtime csproj의 외부 참조 설정과
`CheckGameReferences` 검사로 맞췄다. 런타임 csproj와 공용 props 자체는 변경하지 않았다.
이전 staging 전용 CLI를 authoring source tree에 직접 실행하면 membership 불일치가 발생하므로,
재현 문서에는 현행 CI가 사용하는 `local_release.py verify`를 안내했다. 이전 검사기를 완화하지 않았다.

실행 로그와 원격 브랜치 정리 결과는 로컬 정리 증빙에 보존했다.
이번 정리에서 DLL 재빌드, 게임 실행, 번역 재적용, 릴리스 변경은 수행하지 않는다.
