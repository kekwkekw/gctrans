# 추가 운영 규칙 — 의미 QA와 punctuation/editorial pass 분리

기존 지침을 대체하지 않는 사용자 추가 규칙이다. 원문·손번역 불변, evidence/proposal, 화자와 청자, 추측·전언·사실 구분, exact/context-only, 승인 오염 금지, 큐/partial, 기존 보류와 production 분리는 전부 계속 적용한다.

순수 `...`/`......` ↔ `……`, `---`/`------` ↔ `――`, 완결문 마침표 등 표기 통일은 지금 source/observed/proposal에 반영하지 않는다. `review/editorial_deferred_issues_r23.jsonl`에 workflow issue로만 남긴다. 기존 민감 내용 보류 6행과 별도이며 queue에서 차감하거나 schema status enum·approved policy를 추가하지 않는다.

부호 차이가 머뭇거림·침묵·발화 중단·연결 등 실제 의미/연출을 바꾸는 경우만 기존 semantic QA에서 근거와 함께 검토한다. 어절 자체의 명백한 오기나 주체·목적어·시점 교정은 별도 국소 proposal이며 전체 문체 교정으로 확대하지 않는다.

R22~R36 의미 QA 완료 후 별도의 punctuation/editorial pass를 수행한다. 실행 시점의 최신 approved/published baseline을 기준으로 문맥별 정책을 검토하며, 예시 치환은 미리 승인된 전역 replace 규칙이 아니다. `<click>`, `<ruby>`, `<br>` 태그와 exact JP key, 이미 채택한 의미 교정·제목·태그 결정을 보존한다.

Main M1은 현재 재export하지 않는다. R21 observed에서 재생성하지 않고 published M1과 이후 승인 결과를 명시적으로 계승한다. 이 메모의 발견 시점은 R21과 R22 사이의 Main M1 게시 이후 사용자 runtime 관찰이며, 이번에 새 runtime 확인을 한 것이 아니다.
