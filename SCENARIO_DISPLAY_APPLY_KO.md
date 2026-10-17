# 기존 시나리오 외곽 괄호 표시 교정 (2026-10-17)

## 적용 근거와 범위

PR26 병합 `fd84842efec881deebe7399c709e0a678937b57a`의 Novel subtree는 기존 PRO04 before 기준 `05ceb7a4fc427251bb268c2019372984102f6f2c`와 동일했다. 기존 PRO04 plan·예외48·실제 생성 결과를 그대로 사용하며 재번역·proposal 재선정·의미 재검수를 하지 않았다.

사용자는 2026-10-17 이 시나리오 표시 수정과 구 파이프라인 퇴역의 repository 수정·main merge를 명시 허용했다. 이 범위로 실제 파일을 반영한다. 과거 제안/검수/수락 ledger를 덮어써 ACCEPTED로 바꾸거나, UI21/UI22·Names/Knowledge·WB13–M4·전체 Novel 의미에 대한 새 수락 이벤트를 만들지 않는다.

## 변경과 보존

338 Novel JSON의 22,760 entry 중 **18,366개 값**만 `「` + 기존 한국어 전체 + `」`로 바꾼다. 나머지 **4,394개와 그 안의 HOLD12**는 그대로다. 예외48도 미리 검토한 정확한 source/value에만 적용한다. 일본어의 불균형 괄호 등 원문 이상은 원문 key를 수정하지 않는다.

기존 UTF-8 직렬화·JP key와 순서·KO 내부·click/ruby/tag·줄바꿈·공백·마침표는 보존한다. 변경 Novel byte 증가는 정확히 **110,196**이다. 행별 before/after와 파일 해시가 기존 PRO04 실제 후보 결과에 모두 일치한다. 기존 JP–KO 줄바꿈 차이/태그 차이를 이번 교정에 섞지 않는다.

1,897 Novel 중 등록1854/미등록43을 유지한다. `20003601`과 그 줄바꿈 B0/I0/W0/WI는 변경하지 않는다. Master/Names/UI resources/bindings/layout/명시62-table/DLL과 legacy 6.1 CDN은 보존한다. 현행 `tools/build_manifests.py`가 변경 Novel 338개를 명시하여 legacy manifest/index/UI manifest/release manifest만 다시 만든다. 옛 complete manifest를 덮어쓰지 않는다.

## 완전 package와 설치 경계

새 complete ContentId(UI manifest SHA256)는 `efb4e3d02f38822ce3e5f50ac01dbbc8d505387b9bf8903bdb9b0b424d61946a`다. payload는 1,904개이며 현재 loader의 whole-package/offline 검사를 통과했다. 이 값은 새 repository 데이터 식별값이지 사용자 PC에 이미 선택되었다는 뜻이 아니다.

기존 개인 Stable은 검증된 DLL `96114569…` + complete `b1bedaac…` + 명시62-table + actual current/지속 PackageRoot다. 이번 Git 작업은 그 설치·cfg·current/previous/pending을 직접 변경하지 않는다. Novel 변경은 **restart-required**이며 `request-ui`/UI-only live 교정으로 적용하지 않는다. 데이터 updater는 DLL/폰트/개인 cfg 설치기가 아니다.

Museum3와 미실행 PRO06의 최신-main 구현은 별도 작업이다. Footer 미술관·Master MuseumBlocks와 혼동하지 않는다. XUnity는 OFF를 유지한다. 신규 game/native·전체 화면 전수 검증·Stable 승격·Release/tag는 이 반영의 결과로 주장하지 않는다.

## 재현·보존 기록

[WRAP_SCOPE.json](archive/display/20261017/WRAP_SCOPE.json)은 기존 proposal에서 추출한 정확한 dictionary ordinal bitset과 before/after Novel tree를 보존한다. source/KO 내용으로 후보를 다시 판정하는 규칙이 아니다. before tree가 다르면 현재 source와 대조 없이 이 ordinal을 재사용하지 않는다. 변경을 재조립할 때에는 현행 builder와 일본어 key·KO 내부 불변 검사를 사용한다.

원형 PRO04 plan SHA256: `f23bcbca432e03dd0f1f4fbad48831c52a90ec9f14c0057a349c84826797cea3`. 과거 raw index/game-native ID를 dictionary ordinal로 대체하지 않는다.
