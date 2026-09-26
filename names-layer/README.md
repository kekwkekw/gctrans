# Accepted Names candidate and runtime gate

`names/zh_Hans.json` contains the 1,070 exact speaker keys accepted by the user after Batch09 semantic review and final QA01. Five QA01 amendments are applied; all six context limitations and historical approval boundaries remain recorded. This file is a **PRODUCTION_CANDIDATE** pending in-game runtime verification and final merge review.

The immutable pre-publication checkpoint is preserved byte-for-byte in `names-layer/accepted/4e/`:

- Original checkpoint ZIP SHA256: `13355d93b2693b354bb14810022c98a11ecf77c8f8c40e9216d251b8931df78a`
- Candidate SHA256: `0aadbee044a93eea660c5009dad0b5f60b0abfe3eb56827d1c52436c59f74333`
- Checkpoint-time live delta: 2026-09-26 11:29:01 UTC, added 0 / removed 0 / current 1,070.

Checkpoint files reporting zero Git mutations describe the completed 4-E local stage; they are historical records, not claims that this subsequent preservation PR has no changes. They are intentionally not rewritten. Its copied `names/zh_Hans.json` must remain byte-identical to the repository's top-level candidate.

Read `accepted/4e/REPORT_KO.md`, `ACCEPTANCE_EVENT.json`, `RUNTIME_GATE_CHECKLIST.md`, and `PR_STRATEGY.md` for provenance and gates. Historical pending/proposed source objects are preserved as such; the new acceptance event defines the accepted display values. R34R-M007's original typo-only approval is separate from QA01's newly accepted full wording.

Run the read-only verifier from repository root:

```text
python -B names-layer/accepted/4e/tools/verify_accepted_names.py
```

The verifier checks the preserved checkpoint. Additionally compare SHA256 of top-level `names/zh_Hans.json` with the checkpoint candidate above. Neither script changes repository files or runs game/update/translation workflows.

PR #14 is preserved unchanged as the historical one-entry correction proposal. This full candidate is its proposed replacement; do not merge both proposals independently. No Knowledge state/scope, novel/word translation, GCMod, workflow, Pages setting, or production CDN is changed.

Runtime testing requires a full game exit, a commit-pinned test base URL, and a restart. F10 alone is insufficient. Dictionary/transport tests do not prove in-game UI rendering. No merged/deployed/runtime-verified state is claimed here.
