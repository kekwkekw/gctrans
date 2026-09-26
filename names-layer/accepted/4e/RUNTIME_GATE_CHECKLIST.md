# Runtime gate checklist

상태: **NOT_RUN**. 아래 값은 사용자 accepted candidate이며 실기 검증 결과가 아니다. 새 CDN/session 적용 검증은 **게임 완전 종료 → config/CDN 변경 → 게임 재실행** 순서로 한다. F10 reload만으로 통과 처리하지 않는다. production CDN은 이 작업에서 변경하지 않았다.

1. 테스트 candidate의 commit/content SHA와 `names/zh_Hans.json` SHA를 기록한다. 이 checkpoint에는 배포된 test URL이 없다.
2. names dictionary 로딩 성공 및 1,070 entries를 확인한다.
3. 각 fixture의 실제 표시, 글자 폭, 줄바꿈, 잘림을 확인하고 값·스크린샷/로그·novel ID·message index를 기록한다.
4. translation config가 활성화되어 있어야 한다. 보존된 GCMod 6.1 코드의 SetName은 `Translation.novels.ContainsKey(novelId)`가 false이면 names lookup 전에 return한다. 따라서 names dictionary에 key가 있어도 해당 novel이 translation dictionary에 없으면 화면에 적용되지 않을 수 있다. 커버리지 수와 실기 적용 범위를 구분한다. 이를 고치기 위한 GCMod 변경은 이번 범위가 아니다.
5. 문맥 제한 6개는 표시명 로딩/lookup/UI만 검증한다. 제외/held 본문을 복원하거나 문맥을 완전 검증으로 승격하지 않는다.
6. unmapped JP 입력은 exact dictionary miss 후 원래 text가 유지되는지 확인한다. 필요시 비production 테스트 harness에서 대상 외 JP sentinel을 사용하고 candidate에 추가하지 않는다. 주변 공백/전각 변형은 다른 exact target이 아닐 때 자동 alias hit가 없어야 한다.
7. 새 의미 문제는 ChatGPT/PRO로 반환하고 candidate를 임의 자연화하지 않는다. runtime PASS도 merge/deployment 승인과 구분한다.

## 일반 캐릭터 이름

- [ ] `アルテ` → `아르테` — btl_10011109_11 / raw L17 / message 1

## 신규 고유명 음역

- [ ] `クインズウェイ` → `퀸즈웨이` — evs_22002901 / raw L340 / message 78
- [ ] `ヴィーナス` → `비너스` — evs_22002901 / raw L330 / message 77
- [ ] `ギャング` → `갱` — evs_20003205 / raw L190 / message 56

## 익명 role label

- [ ] `誘拐犯` → `납치범` — evs_20002305 / raw L271 / message 55
- [ ] `若者` → `젊은이` — btl_10011109_42 / raw L7 / message 0

## 물음표 / mystery

- [ ] `？？？` → `???` — evs_20000103 / raw L33 / message 10
- [ ] `？？？？` → `????` — evs_20000805 / raw L59 / message 9
- [ ] `猿？` → `원숭이?` — evs_20002401 / raw L14 / message 3
- [ ] `？？？？？？` → `??????` — evs_23000101 / raw L9 / message 3
- [ ] `謎の男` → `수수께끼의 남자` — evs_22000504 / raw L78 / message 27

## 얼터 / 전환

- [ ] `クピド＆ブグロー（オルタ）` → `쿠피도 & 부그로(얼터)` — hmn_40112111 / raw L70 / message 31
- [ ] `ゴッホ（オルタ）` → `고흐(얼터)` — evs_29000401 / raw L233 / message 39
- [ ] `ロダン（オルタ）` → `로댕(얼터)` — evs_29000901 / raw L195 / message 35

## 공동 화자

- [ ] `原始藝術Ａ＆Ｂ` → `원시 예술A & B` — evs_21000853 / raw L177 / message 27
- [ ] `クピド＆ブグロー（オルタ）` → `쿠피도 & 부그로(얼터)` — hmn_40112111 / raw L70 / message 31
- [ ] `原始藝術Ｂ＆Ｃ` → `원시 예술B & C` — evs_21000703 / raw L303 / message 57

## 전각 A/B/C

- [ ] `泥棒Ａ` → `도둑A` — mas_99900001 / raw L202 / message 58
- [ ] `原始藝術Ａ＆Ｂ` → `원시 예술A & B` — evs_21000853 / raw L177 / message 27
- [ ] `原始藝術Ｂ＆Ｃ` → `원시 예술B & C` — evs_21000703 / raw L303 / message 57

## たち 복수 / 일행

- [ ] `死の芸術たち` → `죽음의 예술들` — evs_20001906 / raw L26 / message 1
- [ ] `クマロボたち` → `곰 로봇들` — evs_22001601 / raw L551 / message 114
- [ ] `生徒たち` → `학생들` — evs_20003005 / raw L109 / message 44

## 죽음의 예술 계열

- [ ] `死の芸術Ａ` → `죽음의 예술A` — mas_10010102 / raw L343 / message 111
- [ ] `死の芸術Ｂ` → `죽음의 예술B` — mas_10010102 / raw L350 / message 112
- [ ] `死の芸術` → `죽음의 예술` — evs_20000205 / raw L110 / message 38

## QA01 amendment 5건

- [ ] `誘拐犯` → `납치범` — evs_20002305 / raw L271 / message 55
- [ ] `怪しげな男Ａ` → `수상쩍은 남자A` — mas_10010111 / raw L92 / message 18
- [ ] `怪しげな男Ｂ` → `수상쩍은 남자B` — mas_10010111 / raw L95 / message 20
- [ ] `義妹` → `양부모 집의 여동생` — mas_11010604 / raw L36 / message 3
- [ ] `不審な男改め死の芸術家` → `죽음의 예술가가 된 수상한 남자` — evs_22001601 / raw L412 / message 89

## R34R-M007

- [ ] `不審な男改め死の芸術家` → `죽음의 예술가가 된 수상한 남자` — evs_22001601 / raw L412 / message 89

## 문맥 제한 6건

- [ ] `おばさん` → `아주머니` — btl_10011109_45 / raw L7 / message 0
- [ ] `お姉さん` → `아가씨` — btl_10011109_44 / raw L7 / message 0
- [ ] `少年A` → `소년A` — hmn_40112092 / raw L104 / message 38
- [ ] `少年B` → `소년B` — hmn_40112092 / raw L99 / message 35
- [ ] `画材屋店主` → `화구점 주인` — btl_10011109_46 / raw L7 / message 0
- [ ] `若者` → `젊은이` — btl_10011109_42 / raw L7 / message 0

## 구별되는 exact key

- [ ] `ラぺ` → `라페` — dns_314005 / raw L10 / message 0
- [ ] `ラペ` → `라페` — evs_21000204 / raw L31 / message 5
- [ ] `子どもの声` → `아이의 목소리` — hsn_40200542 / raw L57 / message 20
- [ ] `子供の声` → `아이의 목소리` — evs_20002903 / raw L166 / message 62
