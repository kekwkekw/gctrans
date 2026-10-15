# 걸크리 PC 한국어화 — Native 실기 상시 승인 정책

기준일: 2026-10-13. 이 문서는 이후 걸크리 PC 한국어화 작업의 **상시 프로젝트 지침**이다. 최신 사용자의 명시 지시가 충돌하면 최신 지시가 우선한다.

## 1. 기본 원칙

Native 실기 테스트는 별도의 사용자 확인이 없는 한 **기본 승인 상태**로 취급한다. 장시간 Codex 작업이 중간 승인 대기 때문에 끊기지 않도록, 아래 안전 범위 안에서는 구현 → 빌드 → 표적 native 시험 → 좁은 수정 → 재빌드/재시험 → 정상 종료 → 정확한 원복까지 계속 진행한다.

과거 NG07~NG09 문서의 `별도 실기 승인 필요`, `AUTHORIZATION_REQUIRED`, `run_authorized=false` 같은 표현은 해당 시점의 역사 기록이며, **이 정책 이후의 작업 권한을 제한하는 현재 규칙으로 해석하지 않는다.**

## 2. 별도 승인 없이 허용되는 범위

- 기존에 확인된 정상 DMM 런처를 통한 게임 실행·종료.
- 시험 직전 현재 plugin/cfg/store 및 관련 파일 존재·비존재를 확인하고 설치 밖에 정확히 백업.
- 단일 시험 후보 DLL과 trial cfg/local store의 임시 적용 및 시험 후 정확한 원복.
- 기존 승인 Utility/Medium/interop dependency의 사용과, 필요한 최소 Master table의 trial-only 활성화.
- Core/일반 UI/Consumers/Profile/Choice/Annotation/Dictionary/Fit3 및 **재화·진행도에 영향을 주지 않는 Battle 표본**의 대표 native 시험.
- 정상적인 UI 열기·닫기·재열기·hover·선택·취소·목록 이동·무해한 버튼 선택. Choice 분기 선택도 계정/세이브/진행도 영향이 없다고 확인된 표본이면 기본 허용 범위다.
- 같은 DLL의 JSON 교정·live reload·재시작·복구 시험.
- 표적 screenshot/crop/log 수집.
- 실기에서 확인된 좁은 구현 오류의 수정, 빌드, 동일 표본 재시험.
- 예상된 안전 실패(disarm/no-op/held)의 기록과 독립 기능의 계속 진행.

위 범위에서는 checkpoint마다 사용자 승인을 요구하지 않는다.

## 3. 새 사용자 승인이 필요한 범위

다음은 상시 승인에 포함되지 않는다.

- AP/BP/재화 소비, 구매, 가챠, 강화, 판매, 보상 수령 등 계정 자산 변화.
- 세이브·진행도·잠금 상태의 변경 또는 우회, 강제 unlock/강제 분기, 저장 데이터 조작.
- 계정·인증·런처 계정 설정의 본질적 변경.
- 검증되지 않은 unknown ABI/RVA 호출, 무작위 native probe, 새로운 위험한 자동조작처럼 기존 안전 계약을 크게 벗어나는 실험.
- 게임 원본 파일의 비가역적 변경 또는 광범위한 설치 환경 변경.
- 공개 push/PR/merge/release/CDN/Pages/workflow dispatch 등 public write.
- 그 밖의 명백히 비가역적이거나 계정 상태에 영향을 줄 수 있는 새 행위.

## 4. 불확실한 표본 처리

특정 표본이 계정/세이브/진행도에 영향을 줄 가능성이 불확실하면 **전체 작업을 멈추지 않는다.** 그 표본만 `HELD`/`NOT_OBSERVED`로 남기고, 독립적인 구현·CPU 검사·다른 native 표본·원복 검증을 계속 진행한다.

Battle에서 무소비 표본이 없거나 Choice 선택의 저장 영향이 불명확한 경우도 같은 원칙을 적용한다. 이를 이유로 장시간 작업 전체에 새 승인을 요구하지 않는다.

## 5. 안전·원복 계약

- 매 trial 시작 시 current plugin/cfg/store와 원래 파일의 존재·비존재를 기록한다.
- old GCMod/8 addons와 차세대 단일 후보를 동시에 로드하지 않는다. Utility/Medium 등 dependency-only 파일은 역할을 구분해 유지한다.
- 사용자 또는 다른 프로세스의 동시 변경을 덮어쓰지 않는다.
- 예상 밖 native 위험이 나타나면 그 feature만 중단하고 own lease/trial write-set을 안전하게 원복한 뒤 독립 작업을 계속한다.
- 종료 후 trial-owned 변경을 정확히 되돌리고 원복 결과를 확인한다. 게임 자체의 자연회복·자동 로그/cache를 세이브 조작으로 되돌리지 않는다.
- XUnity OFF, UI21/UI22 원형·외부 ledger·기존 수락 상태 보존 원칙을 유지한다.

## 6. 보고

`source/build`, `CPU`, `native hook/callback`, `display/control`, `live reload/restart`, `restore`, `game/account effect`, `publication`을 구분한다. 안전한 rollback/no-op을 표시 기능 PASS로 쓰지 않고, 이전 revision의 native 결과를 새 DLL의 직접 PASS로 승격하지 않는다.
