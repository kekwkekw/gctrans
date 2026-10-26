# 현재 UI·Master·runtime 지원 범위

현행 운영은 [OPERATOR_KO.md](OPERATOR_KO.md), 실제 PC 적용·복구는 [LAUNCH_PROCEDURE_KO.md](LAUNCH_PROCEDURE_KO.md)를 따른다. 저장소 정리의 기준은 PR #34 병합 commit `72f30ddc11ba3d522b3a9a6b4411e9caf065106f`다. 정리는 기존 번역 payload와 후보 DLL, 지원 adapter, 수락 table profile을 변경하지 않는다.

수락 Master의 명시적 full profile은 `staging/accepted-master-profile.json`과 `.cfg.fragment`다. 기존 5-table 설정은 과거 smoke 범위이며 수락 데이터 전체 적용 범위가 아니다. profile fragment를 cfg 전체에 덮어쓰지 않는다. 외부 Utility·Medium·interop은 동봉하지 않는다. UI 활성/HOLD/Known Residual과 개별 producer의 지원 범위는 현재 package·source 및 기존 검수 결과를 유지한다.

아래 표의 NG 단계와 DLL별 관측은 Git history에 남은 당시 시험 근거다. 현재 정리 작업의 새 native 실행 결과로 해석하지 않는다. 이후 직접 표본, source-scoped 재사용, 보관·조건부 판정을 구분하며 전체 native 무제한 PASS로 확대하지 않는다. strict async UNPROVEN, Choice 자연 Release HELD, source coverage PARTIAL은 기존 제한으로 유지한다. Museum PRO05/06는 별도 미통합 기능이다.

| 축 | 근거와 범위 |
|---|---|
| NG09 직접 native | 당시 DLL에서 Annotation/standalone Dictionary 헤더·닫기/재열기, Sort9/Profile/Core/Choice4 표시, Fit3 자연 35→34·같은 셀35복구, 같은 DLL live 교정/복구 PASS. 기존 결과 ZIP/최종 DLL identity로 참조 |
| NG10 직접 native | 새 Git export 묶음의 정상 startup·Core 닫기 대표·정상 종료·수정 helper 원복을 별도 보고. NG09 실기를 새 세션 증거로 사용하지 않음 |
| NG11 실행 경계 | Python scanner/workflow/docs 수정. 해당 제품 CPU/mock과 최종 Git export 재현을 검사하며 C#·runtime JSON·원복 helper는 유지. 새 native/game는 NOT_RUN이고 기존 동일 DLL 증거와 구분 |
| main 병합 후 직접 표적 | 최종 `63c54875…` DLL의 정상 startup·Medium publish·실제 OptionPopup callback, `설정`/`닫기` 정착 표시·저장 없이 닫기·정상 종료·정확한 원복 PASS. 타이틀에서 확인하여 계정 진행·보상 화면에는 들어가지 않았다. 실제 main 고정 commit의 제품 prepare도 완전 package/pending 준비 PASS. 이 대표 결과만 추가하며 다른 adapter의 새 DLL 직접 실기를 생성하지 않음 |
| CPU/fault | 실제 Python helper 파일 IO 실패를 합성 scratch에 주입. GC query만 mock. 실제 게임 고의 손상/강제종료/전원차단 없음 |
| strict async | UNPROVEN 유지. generic heading과 다른 인과증명 역할 |
| Choice 분기/자연 Release | 저장·진행도 영향 미확인으로 HELD. 표시·guard·CPU cleanup과 구분 |
| Battle | B01/B02의 출격 전39행(기존 exact write/readback30·적 기법 정보7·inactive1·source variant1)과 나머지 전투 중/완료 parameter69를 구분한다. 현재 표본의 접근/결과는 전달 판정표를 따른다. 비용 getter/계산/AutoLease 없음 |
| Fit3 | 기존 35→34→33→32→31 정책 유지. 자연34와 동일셀35복구의 기존 실기 외 나머지 수치를 강제하지 않음 |
| source coverage | 최신 PC inventory/UI·Novel locator/Names authority PARTIAL. 기존 수락 번역 자격 취소 없음 |

Master 62/101/31,566 및 미등록 Novel43은 관측값이다. table 활성과 보유 번역 개수는 별개다. 과거 NG09~NG12 smoke는 기존 mUnitWords,mSubunitWords에 mTextConfig,mNovelKeyWords,mNovels만 추가했다. 종합 복원 Dev는 staging의 명시62-table profile과 실제 복원 package로 전환·재시작했고, geometry 최종 세션의62 table load도 확인했다. 모두101 field/31,566문장의 화면 전수 PASS와 다르다. configured-off는 원문 유지이며 missing data/callback failure와 구분한다. unknown/unsafe/shared native entry는 해당 feature만 disarm하고 지원했다고 보고하지 않는다.

일반 UI literal/template_label KO 교정과 이미 지원되는 binding/typed layout은 기존 reviewed apply와 next_package로 수행한다. UI-only commit은 Master/Names/Novel session/cache를 갱신하지 않는다. Master/Names/Novel authority·font/code/locale/origin 변경은 restart 경계다. prepare-local은 pending만 준비하며 live request는 별도 signal/수신/commit/화면 관측이다. current/previous/pending_restart와 coordinator는 기존 하나다.

원복 helper는 현재 trial의 정확한 outside backup/write-set만 지원한다. 처음 store/candidate가 존재하면 보존 계획을 요구한다. 알려진 중간상태는 재개하지만 UNKNOWN_CHANGED/unknown file/reparse/실행 중 GC는 보존하고 거부한다. 원래 bytes가 복원되면 남은 자기 경로만 정리하며 재호출은 ALREADY_RESTORED다. 임의 store를 지우는 범용 설치 기능은 없다.

2026-10-13 상시 정책의 안전한 표적 native는 checkpoint 승인 대기로 멈추지 않는다. 소비/계정/세이브/진행도/잠금/unknown ABI 위험/추가 public write는 범위 밖이다. trial 종료 후 원복하며 main 병합은 Release·tag·CDN 승격 또는 영구 설치 권한을 생성하지 않는다.

source scan의 metadata observation, raw materialization, actionable intake, Codex 초벌·PRO 검수와 Novel apply는 서로 다른 상태다. 로컬은 persistent state/raw custody를 사용한다. GitHub은 one-shot metadata snapshot이며 지속 CI 증분·raw 취득·최신 PC 동등성·semantic approval을 제공하지 않는다. PC locator 또는 Names authority 미확인은 해당 lane의 PARTIAL/typed reason으로 남긴다. source PARTIAL을 이유로 기존 수락 번역이나 offline 제품 마감을 취소하지 않는다.
