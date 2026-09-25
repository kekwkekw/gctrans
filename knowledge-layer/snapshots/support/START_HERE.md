# 새 프로젝트 시작 안내

**R36·최종 의미 검수·PR13 production은 완료 상태다.**
이번 순서: **Knowledge Layer GitHub 보존 → Names 전체 완성 → GCMod/CDN·파이프라인**.

## 처음 첨부할 것

1. `gc_r36_event12_v1_final01.zip` — 별도 첨부.
2. `gc_POST_R36_MEANING_BASELINE_v1_0.zip` — 별도 첨부.
3. 이 지원 ZIP `gc_RESTART_SUPPORT_20260925_v1_0.zip`.

`gc_names_layer_4A_checkpoint_v1.zip`은 추가 권장. 이 지원 ZIP에는 원본 bytes가 없다. 미확보여도 Knowledge 보존부터 시작한다.

## 읽기 순서

`NEW_PROJECT_PROMPT_KO.md` → `CURRENT_STATE.json` → `audit/INPUT_VERIFICATION.json` → `audit/SOURCE_INDEX.json` → 해당 단계의 필요한 원본 경로만.

사용자가 첫 메시지로 붙여넣을 문구는 `FIRST_MESSAGE_KO.txt`다. 첫 작업은 **A0 읽기 전용 정본/상태/의존성 조사**이며, 자동 GitHub 쓰기·merge·runtime 변경은 하지 않는다.

## 지원팩에 들어 있는 것

`originals/`에는 legacy 회수 후보, PRO adjudication, editorial policy PRO, PR13 PRO 회귀, production merge receipt 원본 ZIP 5개가 그대로 들어 있다.
`references/`에는 미래 파이프라인 참고문서와 Names 4-A 상태/전달 한계가 있다.
`audit/`에는 이번 입력의 byte 검증, source 경로, 실제 조회한 GitHub 상태, 중복 비교, 추가 의존물이 있다.

이 팩은 **인계·탐색용**이며, GitHub용 Knowledge Layer 구축 완료본이 아니다. 전체 R36 원형을 여기서 다시 packing하지 않았다. 기존 원본/승인 상태를 소급 변경하지 않았다.

파일 안내는 `FILES_TO_ATTACH_KO.md`. 읽기 전용 integrity checker는 `verify_support.py`다.
