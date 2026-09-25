# Names Layer 4-A — Source Extraction / Schema Adapter Report

## 범위

- R3~R36 canonical/최신 review source 34개만 사용.
- non-empty `speaker_jp → speaker_ko`의 **관찰 pair만** raw 원장에 기록.
- 의미 재검수, dedup, conflict 판정, Post-R36 승인 overlay, 신규 번역은 수행하지 않음.
- PR #14 / main / production 파일은 수정하지 않음.

## 결과

- canonical source: **34**
- raw pair: **17024**
- unique `speaker_jp`: **305**
- parse error: **0**
- source-structure limitation: **4**
- conflict: **4-B로 이월(이번 단계 미판정)**

## Source-structure limitations

- **R3 / `REVIEW_R3.html`** — Canonical review is finding-oriented and does not expose explicit JP+KO speaker labels; no pair inferred.
- **R4 / `REVIEW_R4.html`** — Some cards expose JP speaker, but no explicit observed KO speaker label; no KO label inferred.
- **R16 / `gc_r16_review.html`** — JP speaker column exists; observed KO speaker label is not exposed in this review HTML. No KO label inferred.
- **R21 / `gc_r21_review.html`** — Observed KO speaker label exists; JP speaker label is not exposed in this review HTML. No JP label inferred.

이 4건은 parser 실패가 아니라 canonical HTML이 JP/KO speaker label 양쪽을 동시에 노출하지 않는 구조적 제한이다. 누락 counterpart는 추정하지 않았다.

## Adapter audit

| Round | Source | Adapter | Source items | Raw pairs | Structural exclusions | Status |
|---|---|---|---:|---:|---:|---|
| R3 | `REVIEW_R3.html` | `r3_findings_only` | 88 | 0 | 0 | SOURCE_LIMITATION |
| R4 | `REVIEW_R4.html` | `r4_context_cards` | 133 | 0 | 0 | SOURCE_LIMITATION |
| R5 | `gc_r5_review.html` | `r5_html` | 71 | 67 | 0 | PASS |
| R6 | `gc_r6_review.html` | `r6_html` | 578 | 527 | 0 | PASS |
| R7 | `gc_r7_review.html` | `r7_html` | 834 | 722 | 0 | PASS |
| R8 | `gc_r8_review.html` | `r8_html` | 611 | 573 | 0 | PASS |
| R9 | `gc_r9_review.html` | `r9_html` | 740 | 713 | 0 | PASS |
| R10 | `gc_r10_review.html` | `r10_html` | 651 | 625 | 0 | PASS |
| R11 | `gc_r11_review.html` | `r11_html` | 799 | 771 | 18 | PASS |
| R12 | `gc_r12_review.html` | `r12_html` | 655 | 594 | 0 | PASS |
| R13 | `gc_r13_review.html` | `r13_html` | 634 | 603 | 0 | PASS |
| R14 | `gc_r14_review.html` | `r14_html` | 674 | 619 | 0 | PASS |
| R15 | `gc_r15_review.html` | `r15_html` | 588 | 550 | 28 | PASS |
| R16 | `gc_r16_review.html` | `r16_table_jp_only` | 563 | 0 | 0 | SOURCE_LIMITATION |
| R17 | `gc_r17_review.html` | `r17_html` | 694 | 591 | 95 | PASS |
| R18 | `gc_r18_review.html` | `r18_html` | 653 | 512 | 133 | PASS |
| R19 | `gc_r19_review.html` | `r19_html` | 775 | 520 | 245 | PASS |
| R20 | `gc_r20_review.html` | `r20_html` | 691 | 548 | 0 | PASS |
| R21 | `gc_r21_review.html` | `r21_ko_only` | 831 | 0 | 0 | SOURCE_LIMITATION |
| R22 | `gc_r22_review.html` | `r22_json` | 694 | 577 | 0 | PASS |
| R23 | `gc_r23_review.html` | `r23_json` | 643 | 455 | 0 | PASS |
| R24 | `gc_r24_review.html` | `r24_json` | 553 | 409 | 0 | PASS |
| R25 | `gc_r25_review.html` | `r25_json` | 781 | 654 | 0 | PASS |
| R26 | `gc_r26_review.html` | `r26_json` | 807 | 651 | 0 | PASS |
| R27 | `gc_r27_review.html` | `r27_json` | 755 | 577 | 0 | PASS |
| R28 | `gc_r28_review.html` | `r28_json` | 817 | 583 | 0 | PASS |
| R29 | `gc_r29_review.html` | `r29_json` | 721 | 562 | 0 | PASS |
| R30 | `gc_r30_review.html` | `r30_json` | 562 | 451 | 0 | PASS |
| R31 | `gc_r31_review.html` | `r31_json` | 777 | 621 | 0 | PASS |
| R32 | `gc_r32_review.html` | `r32_json` | 691 | 638 | 0 | PASS |
| R33 | `gc_r33_rebuilt_review.html` | `r33_json` | 717 | 601 | 0 | PASS |
| R34 | `gc_r34_rework01_review.html` | `r34_json` | 668 | 576 | 0 | PASS |
| R35 | `gc_r35_v2_review.html` | `r35_json` | 839 | 626 | 0 | PASS |
| R36 | `gc_R36_REVIEW.html` | `r36_html_full` | 707 | 508 | 0 | PASS |

## 재사용 경계

4-B는 `speaker_pairs_raw.jsonl`과 `source_extraction_audit.json`만으로 exact dedup/conflict audit을 시작할 수 있다. 단, 위 SOURCE_LIMITATION 4개 round의 미노출 counterpart를 자동 생성하거나 추정해서는 안 된다.
