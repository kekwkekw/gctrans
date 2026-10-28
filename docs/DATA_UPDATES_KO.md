# 7.1.0 번역 데이터 갱신

상태: **PR #38 대표 Native 동작 및 UI/Novel 갱신 확인, main 병합 완료**.
설치 파일과 안내는 [7.1.0 릴리스](https://github.com/kekwkekw/gctrans/releases/tag/v7.1.0)에 있다.

기존 7.0.0 전체 설치본은 CDN=offline이며, 파일이 있으면 원격 조회 없이 읽는다.
GitHub만 수정해 그 DLL에 새로운 기능을 전달할 수 없다. 7.1.0 DLL과 cfg를 한 번
적용한 이후부터 아래 데이터 전달 경로를 사용할 수 있다. 실기에서는 새 DLL/cfg를
적용한 뒤 별도 시험 브랜치로 아래 데이터 전달 경로를 확인했다.

## 전달되는 것

- 시작 시 공식 GitHub main의 `translations/ko/manifest.json`을 확인한다.
- Names, Master, UI는 hooks 설치 전에 최대 15초 동안 갱신한다.
- Novel은 기존 ID별 진입 경로에서 필요할 때 해당 manifest 해시로 갱신한다.
- 로컬과 manifest의 해시가 같으면 payload를 다운로드하지 않는다.
- JSON 파싱과 기존 TranslationHash 계약을 통과한 데이터만 파일별 임시 파일로 교체한다.
- 실패·시간 초과·해시 불일치 시 기존 파일을 사용한다. 실행 중 UI live switching은 없다.
- 각 원격 요청은 최대 5초로 제한한다. 느린 연결에서는 기존 파일을 유지하고 다음 재시작 때 재시도한다.
- CDN=offline이면 네트워크 요청 없이 동작한다.

DLL, 폰트, BepInEx, cfg는 다운로드하거나 변경하지 않는다. 새로운 패턴·hook은
DLL 업데이트가 필요하다. 이미 지원하는 exact 키·번역값의 변경은 데이터 변경으로 전달된다.
기존 세 잔존 해결에는 새 DLL이 필요하며, 이후의 데이터 갱신과 구분한다.

이는 파일별 캐시 갱신이다. 인터넷 실패로 일부 파일만 최신일 수 있으며 완전한
패키지 트랜잭션을 보장하지 않는다. 다음 재시작에서 다시 확인한다. 서버 게시 중
manifest와 payload의 버전이 다르거나 CDN이 오래된 응답을 주면 해시 불일치로
기존 파일을 유지한다. 즉시 전달 성공을 로그 없이 단정하지 않는다.

## 게시 전 authoring

검수·apply 경로는 기존 그대로다. UI 수정은 reviewed/resources에서 export하고,
변경 목록을 manifest builder에 전달한다.

```powershell
python tools/export_local_ui.py
python tools/build_manifests.py translations/ko --modify ui.json
python tools/verify_local_parity.py --output work/local-parity/check.json
```

manifest에 ui 해시가 추가되며 root hash도 갱신된다. Novel 등록 map과 Names 해시는
그대로 유지된다. 기존 Novel apply도 새 필드를 보존하며 manifest를 생성한다.
Master 변경은 기존 apply 또는 `--modify master.json`으로 등록한다.
번역 데이터와 generated metadata를 같은 커밋으로 main에 반영해야 한다.
개별 JSON만 GitHub에서 편집하면 갱신이 보장되지 않는다.

## Native 시험 절차

1. 새 기준본을 한 번 적용하고, 장 번호·용어 사전 한글 색인 이동·선택지 뒤 화자명과
   선택 후 복귀를 확인한다. 사전 popup/목록 재사용도 확인한다.
2. 기준본 설치 후에는 로컬 DLL/cfg/번역 파일을 수동 수정하지 않는다.
3. 이미 지원하는 UI exact의 한국어 값 하나를 정상 authoring 경로에서 변경하고
   manifest와 함께 승인된 GitHub 대상에 게시한다. 테스트 브랜치를 쓸 경우 CDN은
   기준본 설치 시 해당 브랜치로 설정한다.
4. 게임을 재시작하여 `Updated translation: ko/ui hash=...` 로그, 설치 UI 해시,
   실제 문구를 확인한다. Novel은 해당 ID 진입 후 로그/본문을 확인한다.
5. 두 번째 재시작에서는 변경 없는 payload가 다시 다운로드되지 않는지 확인한다.

현재 설치된 7.0.0을 그대로 둔 채 이 시험에 성공했다고 보고하면 안 된다.
비실기 캐시/전송 검증은 Native 렌더링 검증을 대신하지 않는다.

## 2026-10-28 Native 결과

- 장 번호 `제9장·제10장·제11장`, 사전 정·역순 색인 이동과 설명 갱신,
  선택지 뒤 `관장·산소비노` 화자명 및 선택 후 복귀를 실제 화면에서 확인했다.
- 사전의 일부 단독 자모가 네모로 표시되는 문제를 발견하여, 동일한 초성 그룹을
  완성형 `가·나·다…`로 표시하도록 수정하고 재검증했다. 폰트나 전역 한 글자 규칙은 유지했다.
- 격리된 `test/pr38-native-delivery` 브랜치에서 UI `용어 사전`과 이미 읽은 Novel
  `99900001`의 한 문장에 시험 표식을 게시했다(`139dcaf1`). UI는 재시작 시,
  Novel은 해당 ID 진입 시 갱신됐고, 실행 중 UI는 바뀌지 않았다.
- 같은 상태로 다시 실행하고 Novel에 진입했을 때 manifest만 조회했다.
  UI·Novel payload 재다운로드 없이 캐시의 변경된 문구가 정상 표시됐다.
- 시험 커밋을 GitHub에서 되돌린 뒤(`794a667b`), 재시작/Novel 진입만으로 두 문구와
  로컬 캐시 해시가 시험 전 값으로 복원됐다. 전달 시험 동안 DLL·cfg는 변경하지 않았다.
- 실제 설정은 `EnabledTables=*`, 로딩된 Master table 60개의 rewrite를 확인했다.
  새 오류 로그는 없었고 기존 Il2Cpp 경고는 별도로 유지됐다.
- main/PR의 Master·Names·Novel·Knowledge·HOLD 정본에는 시험 문구를 넣지 않았다.

이는 대표 동작과 UI/Novel 전달 경로의 통과 결과다. 전체 게임 번역 범위, 이미지 교체,
실제 네트워크 차단 상황은 이번 Native 시험 범위가 아니다. 통신 실패·timeout·잘못된
payload의 기존 파일 보존은 별도 캐시 테스트에서 확인했다.

위 시험 당시 CDN은 원복된 시험 브랜치를 사용했다. 이후 승인된 PR #38의 main 병합으로
공식 경로 전환 및 배포를 진행한다. 배포 cfg의 CDN은 공식 main이다.
7.0.0 사용자는 기능 도입을 위한 새 DLL/cfg 1회 적용이 필요하다.
