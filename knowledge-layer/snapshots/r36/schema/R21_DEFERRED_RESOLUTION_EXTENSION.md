# R21 schema-v1 operational extension
No enum in schema_v1.json is changed. Status and translation approval remain separate from coverage.

- `review/deferred_rows_r20.jsonl` and historical R20 context records are frozen historical observations.
- `review/deferred_resolutions_r21.jsonl` records explicit KEEP_DEFERRED or RESTORE_AND_REVIEW decisions against old IDs and hashes.
- `review/context_coverage_updates_r21.jsonl` is an append-only coverage overlay for those six R20 contexts. Resolve it after the base `catalog/context_groups.jsonl` for current completion; never treat the old 11 holds as the effective current registry.
- Current effective open holds are only in `review/deferred_registry_effective_r21.jsonl` (six rows). Four are in the remaining general queue; two were already outside it.
- Restored ten rows have new R21 source entries and evidence with original JP/KO unchanged. Reclassification is not approval.
- R21 820 originals partition into 815 reviewed / 5 deferred. New snapshot 825 = 815 plus ten restored R20 rows. Snapshot is not a replacement for the inherited complete originals.
