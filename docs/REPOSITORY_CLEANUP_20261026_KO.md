# 저장소 최종 정리 검증 (2026-10-26)

PR #34 병합 commit `72f30ddc11ba3d522b3a9a6b4411e9caf065106f`를 기준으로 현행 번역 파이프라인과 배포 데이터를 유지하면서 역사 자료와 퇴역 실행 경로를 정리했다. 과거 파일은 해당 commit의 Git history에서 복구할 수 있다.

## 반영 범위

- README·INDEX·OPERATOR를 현재 Novel prepare → Codex 초벌 → PRO 검수 → check/apply 흐름과 current Knowledge 계약으로 통합했다. 지원 범위·공개·재현·출처 문서의 삭제된 링크와 과거 운영 안내를 정리했다.
- 역사 viewer·Names layer·Knowledge 원장·이관표·이전 Manet 검수·회수 wrapper·일회성 문서·퇴역 workflow 등 계획된 추적 파일 554개를 제거했다. 과거 `/archive/review/` URL은 현행 tree에서 사라진다.
- 과거 Knowledge seed·Names sync 및 초기화 CLI를 제거했다. 현행 Knowledge에는 approved, 실제 content, 명시 scope, review provenance를 요구한다. exact JP, context soft budget, record hash와 reviewed replacement의 preimage 검사는 유지한다.
- WB 호환 CLI와 UI/Master apply의 Novel return 경로를 제거했다. 퇴역 return 입력은 staging 전에 unknown property로 거부한다. 현재 source scan·재시도·Novel 검수 완료 기능은 `novel_intake.py`와 `source_work.py`로 분리해 보존했다.
- 빌드 설정을 `source/build/Common.props`, upstream MIT 라이선스를 `THIRD_PARTY_LICENSES/GCMod-upstream-LICENSE`로 원본 bytes 그대로 옮겼다. csproj·exporter·attribution과 Git ignore 규칙을 갱신했다.
- 제거한 이전 PRO 검수의 항목만 기존 checksum 목록에서 제외했다. 유지한 최종 PRO 영수증의 checksum 16개는 모두 일치한다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 관련 Python 회귀 | 16개 모듈, 200개 시험 PASS, skip 0 |
| Knowledge | 3,535개 모두 approved·available, 중복 ID 없음, current scope·review provenance 유효 |
| 완전 package gate | EXACT membership, 1,946개 payload, 40,575,909바이트 PASS |
| preflight | OFFLINE_ARTIFACT_PASS |
| 후보 DLL source build | 외부 보관 참조 DLL로 Release 빌드 PASS, 경고·오류 0 |
| UpdateCli build | Release 빌드 PASS, 경고·오류 0 |
| 불변 대상 | 3,914개 파일, membership·SHA-256 변경 0 |
| 이동한 props·라이선스 | 각각 원본 SHA-256 일치 |
| 퇴역 Python import | scripts·tools·tests AST 검사 0 |
| 문서 링크·export | 현재 링크와 고정 commit export bytes 검사 PASS |
| diff 공백 검사 | 기존 CRLF를 인정하는 `git -c core.whitespace=cr-at-eol diff --check` PASS |

Python 범위는 Knowledge, Novel workflow·CLI·완료 queue, NG03/04 source·apply·Master, NG05 operator, NG06 closeout, NG11 scanner·workflow, frozen HOLD policy, data tools, repository layout, retirement guard다.

불변 대상에는 `translation/`, `translations/ko/`, `candidate/GCMod.dll`, `contracts/`, `staging/`, 6.1 source backup과 `names/zh_Hans.json`, `words/zh_Hans.json`, `novels/*/zh_Hans.json`이 포함된다. Knowledge SHA-256은 `f65e42392cd1f84240fe4680413c3c1962702f9907e9f93269b4e0592f3cb153`이다.

## 남은 범위와 해석

퇴역 경로·명령 문자열은 부재와 거부를 확인하는 negative regression 상수에만 남아 있다. 실행 import나 정상 입력으로 사용하지 않는다. 현재 provenance·Knowledge에 기록된 과거 출처는 그 당시 기록이며 원장을 다시 읽는 경로가 아니다.

최초 후보 빌드는 현재 설치본의 interop에 `SortPopup.inertToggleGroup`이 없어 실패했다. 당시 보관된 외부 참조 DLL을 Git 밖 검증 폴더에 복원한 뒤 빌드했다. 설치본 수정이나 `candidate/GCMod.dll` 교체는 없으며 이번 검증은 현재 게임 ABI 호환성·화면 관측 결과를 새로 만들지 않는다. native·game 실행, 영구 설치, tag·release·Actions dispatch는 수행하지 않았다.

GitHub 정리 요청에 따라 검증된 별도 브랜치와 PR로 전달한다. main 병합은 이번 정리 PR에 대한 별도 승인 대상이다. PR #34의 병합 승인을 후속 정리 PR에 확대하지 않는다.
