# 불필요한 작업 브랜치 정리 — 2026-10-17

이 정리는 현재 개발 branch를 단순화하되 과거 원문·검수·검증·실패 이력을 잃지 않기 위한 것이다. **과거 미병합 변경을 main에 병합하거나 수락하지 않는다.**

## 보존과 삭제 범위

확인 당시 main 외 종료된 작업 branch는 32개였다. 16개는 main의 조상이고, 16개는 commit ancestry상 미병합이다. 미병합에는 squash로 대응된 9개 archive, 과거 relay/probe와 1행 Names 초안·검증 기록 등이 포함된다.

[BRANCH_RETIREMENT.json](BRANCH_RETIREMENT.json)에 이름·정확한 head·tree·분류를 보존한다. 삭제 전에 `archive/branch-cleanup-20261017`이라는 **역사 보존 전용 tag**가 모든 기록 tip을 도달 가능하게 하는지 확인한다. 이 tag는 소프트웨어 Release가 아니며 설치·수락 기준으로 사용하지 않는다. archive commit의 다중 부모는 이력 보존용 연결이고 main의 조상이 아니다.

보존 ref를 원격에서 검증한 뒤에만 정확한 expected SHA를 명시한 lease/atomic push로 계획된 branch refs를 삭제한다. 그 사이 변경된 branch, 새 branch, protected branch 또는 새 열린 PR은 임의 삭제하지 않는다. main은 삭제·강제갱신하지 않는다. 실제 수행 결과는 병합 PR과 반환 증거에 별도로 기록한다.

## 종료할 과거 Draft PR

#6/#7은 do-not-merge Codex bridge probe, #8은 종료된 R9/R10 relay다. #14는 과거 1행 Names 초안으로 이후 accepted Names 1070 기준이 존재한다. #14의 예전 표현을 현재 값으로 다시 적용하지 않으며, 이 PR들을 새로운 merge 없이 종료한다. 원래 대화와 commit은 보존한다.

## 필요한 역사 파일 회수

저장소 루트에서 실행한다. 이것은 과거 자료 회수 방법이지 Stable 복구 지시가 아니다.

```bash
git fetch origin refs/tags/archive/branch-cleanup-20261017:refs/tags/archive/branch-cleanup-20261017
# JSON에서 필요한 옛 branch의 head SHA를 확인한다.
git show <recorded-head-sha>:<historical-file-path>
# 과거 branch가 꼭 필요할 때에만 새 로컬 branch로 복원한다.
git branch recovered/<local-name> <recorded-head-sha>
```

main의 Names/Knowledge/수락 archive/ledger/WB 연결은 변경하지 않는다. 과거 원장 안의 당시 branch 이름은 역사 사실로 보존하고, 해당 이름의 현재 ref가 사라졌을 때는 이 복구표의 SHA를 사용한다. 실행 시험의 `ActualBranchProbe`만 종료된 `ng12/publication-candidate` 대신 현재 `main`을 조회하도록 수정했다.
