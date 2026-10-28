# 7.1.0 후보의 번역 데이터 갱신

상태: **PRE_NATIVE**. 기존 7.0.0 초안과 공개 main을 대체하는 릴리스가 아니다.

기존 7.0.0 전체 설치본은 CDN=offline이며, 파일이 있으면 원격 조회 없이 읽는다.
GitHub만 수정해 그 DLL에 새로운 기능을 전달할 수 없다. 7.1.0 DLL과 cfg를 한 번
적용한 이후부터 아래 데이터 전달 경로를 사용할 수 있다. 이번 준비 작업에서는
실제 게임 설치 폴더를 변경하거나 게임을 실행하지 않는다.

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

## 다음 Native 시험

1. 새 기준본을 한 번 적용하고, 장 번호·용어 사전 초성 이동·선택지 뒤 화자명과
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
