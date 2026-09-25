# 걸크리 Post-R36 의미 기준본 결정 체크포인트 v1.0

이 패키지는 R36 원문/관찰 번역을 복제하거나 덮어쓰는 새 corpus ZIP이 아니라, `gc_r36_event12_v1_final01.zip`과 `gc_POST_R36_PRO_ADJUDICATION_v1.zip` 위에 사용자 승인 결정을 봉인한 **decision baseline companion**이다.

## 확정 상태
- Main M1 기존 게시 disposition 668건은 그대로 승계.
- Post-R36 1,019건은 사용자 결정 완료: 기존 후보 채택 987, 수정안 채택 6, 기존 번역 유지 23, 역주 정책 승인 3.
- 따라서 누적 proposal 1,687건은 모두 기존 게시 disposition 또는 이번 사용자 결정에 연결된다.
- 향후 실제 스토리 변경 후보는 996건이다: 의미 교정 993 + 본문에서 분리할 역주/작업 메모 3.
- source anomaly 13건은 JP observed를 수정하지 않는다. 문맥상 올바른 KO를 일부러 원문 오기에 맞추지 않으며, 별도 승인된 KO proposal만 적용한다.
- 기존 명시 보류 12행은 이번 승인과 무관하며 그대로 남는다.

## 역주 3건
- R3-W035: 본문에서 분리, 역주 원장 보존, 기본 비노출. `<click=ID>` 커스텀 팝업 연구는 후순위.
- R3-W039: 본문에서 분리, 역주 원장 보존, 향후 툴팁/용어집 후보. `<click=ID>` 연구는 후순위.
- R21-M044: 본문에서 제거, audit/history에만 보존, 사용자 비노출.

## 권한 경계
이 체크포인트는 의미/역주 정책 승인 기록이다. 실제 export, GitHub/commit, GCMod 버전업, runtime 변경, 배포, 파이프라인 구현 권한은 부여하지 않는다.

## 다음 단계
별도 punctuation/editorial pass 준비 및 정책 검토. 의미 판정 전체를 다시 시작하지 않는다.
