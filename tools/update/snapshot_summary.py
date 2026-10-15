"""Summarize a scan envelope without exporting private source/state bytes.

The workflow is a one-shot metadata snapshot. Its artifact is this finite
projection, never the full scan, state, raw cache, signed URL or intake payload.
Persistent local source history remains in the operator's owned state root.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

MAX_REPORT_BYTES = 32 * 1024 * 1024
REASONS = {
    "PC_SOURCE_LOCATOR_REQUIRED", "UI_SUPPORTED_SOURCE_LOCATOR_REQUIRED",
    "SOURCE_METADATA_OBSERVATION_FAILED", "SOURCE_INVENTORY_SCHEMA",
    "SOURCE_INVENTORY_FILTER_NO_MATCHES", "SOURCE_OBSERVATION_UNKNOWN",
}
COVERAGE_ENUMS = {
    "metadata": {"OBSERVED", "PARTIAL", "UNKNOWN"},
    "lane_status": {"PARTIAL", "SELECTED_PROFILE_RAW_EXTRACTED"},
    "master": {"TABLE_BODY_EXTRACTED", "MASTER_BODY_DECODER_REQUIRED"},
    "ui": {"SUPPORTED_SOURCE_ROWS_EXTRACTED", "UI_SOURCE_ADAPTER_REQUIRED"},
    "historical_before_bootstrap": {"UNKNOWN"},
}
COVERAGE_COUNTS = (
    "raw_materialized_assets", "successful_assets", "baseline_only_assets",
    "pending_assets", "failed_assets", "collision_assets",
    "automatic_translation_deletes",
)
METRIC_COUNTS = (
    "materialized", "baseline_only", "failed", "collision", "reused",
    "metadata_only_reuse", "payload_network", "source_payloads_acquired",
    "raw_cache_reads", "raw_cache_read_bytes", "raw_hash_calls", "raw_hash_bytes",
)
RAW_STATUSES = {
    "PARTIAL_FAILED", "ACQUIRED_OR_REEXTRACTED",
    "OWNED_METADATA_REUSE_NOT_BYTE_VERIFIED_THIS_SCAN", "NOT_ACQUIRED",
}


def count(value):
    return value if type(value) is int and 0 <= value <= 2**63 - 1 else None


def enum(value, allowed, fallback="UNKNOWN"):
    return value if isinstance(value, str) and value in allowed else fallback


def project(scan, *, one_shot=False):
    if not isinstance(scan, dict) or not isinstance(scan.get("sourceSnapshot"), dict):
        raise ValueError("SNAPSHOT_SCAN_ENVELOPE_REQUIRED")
    snapshot = scan["sourceSnapshot"]
    if snapshot.get("schema") != "gc-ng03-source-snapshot-v1":
        raise ValueError("SNAPSHOT_SOURCE_SCHEMA_REQUIRED")
    metadata = snapshot.get("metadata")
    coverage = snapshot.get("coverage")
    metrics = scan.get("metrics")
    plan = scan.get("plan")
    if not isinstance(metadata, dict) or not isinstance(coverage, dict) or not isinstance(metrics, dict) or not isinstance(plan, list):
        raise ValueError("SNAPSHOT_SCAN_ENVELOPE_REQUIRED")
    status = enum(metadata.get("status"), {"OBSERVED", "PARTIAL", "UNKNOWN"})
    raw_status = enum(scan.get("raw_acquisition_status"), RAW_STATUSES)
    failed = count(metrics.get("failed"))
    materialized = count(metrics.get("materialized"))
    operation = ("OBSERVATION_UNAVAILABLE" if status == "UNKNOWN" else
                 "PARTIAL_ACQUISITION" if failed else
                 "RAW_ACQUIRED" if materialized else "METADATA_ONLY_PLAN")
    profile = snapshot.get("profile", {})
    profile = profile if isinstance(profile, dict) else {}
    history = scan.get("lane_diff", {}).get("historical_baseline") if isinstance(scan.get("lane_diff"), dict) else None
    history = enum(history, {"KNOWN_SAVED_SNAPSHOT", "UNKNOWN_BASELINE_ONLY"})
    comparison = "NO_PREVIOUS_RUN_HISTORY" if one_shot else (
        "RETAINED_LOCAL_HISTORY" if history == "KNOWN_SAVED_SNAPSHOT" else "UNKNOWN_BASELINE_ONLY")
    reason = metadata.get("reason")
    public_reason = reason if isinstance(reason, str) and reason in REASONS else "OTHER_TYPED_REASON" if reason else None
    result = {
        "schema": "gc-ng11-public-metadata-summary-v1",
        "scope": "ONE_SHOT_METADATA_SNAPSHOT" if one_shot else "LOCAL_SCAN_SUMMARY",
        "operation_status": operation, "observation_status": status,
        "reason": public_reason, "comparison_history": comparison,
        "historical_baseline": history,
        "platform": enum(profile.get("platform"), {"WebGL", "PC"}),
        "lane": enum(profile.get("partition"), {"Novel", "Master", "UI"}),
        "raw_acquisition_status": raw_status, "plan_candidates": len(plan),
        "coverage": {key: enum(coverage.get(key), values) for key, values in COVERAGE_ENUMS.items()},
        "coverage_counts": {key: count(coverage.get(key)) for key in COVERAGE_COUNTS},
        "metrics": {key: count(metrics.get(key)) for key in METRIC_COUNTS},
        "semantic_review": "NOT_PERFORMED_BY_SCAN",
        "pc_webgl_equivalence": "NOT_PROVEN_BY_METADATA",
        "ci_persistent_incremental": "NOT_IMPLEMENTED" if one_shot else "NOT_APPLICABLE",
        "artifact_projection": "FINITE_STATUS_AND_COUNTS_ONLY",
    }
    intake = scan.get("actionable_intake", {})
    if isinstance(intake, dict):
        result["actionable_intake_counts"] = {key: count(intake.get(key)) for key in ("new_work", "actionable", "held", "completed")}
    return result


def markdown(report):
    lines = [
        "# Metadata snapshot summary", "",
        f"- Scope: `{report['scope']}`",
        f"- Operation: `{report['operation_status']}`",
        f"- Metadata observation: `{report['observation_status']}`",
        f"- Comparison history: `{report['comparison_history']}`",
        f"- Planned candidates: `{report['plan_candidates']}`",
        f"- Raw acquisition: `{report['raw_acquisition_status']}`",
        f"- Successful extracted assets: `{report.get('coverage_counts', {}).get('successful_assets')}`",
        f"- Baseline-only assets: `{report.get('coverage_counts', {}).get('baseline_only_assets')}`",
        f"- Failed assets: `{report.get('coverage_counts', {}).get('failed_assets')}`",
        "", "A first baseline has no earlier comparison history. It is not evidence of no change since an earlier workflow run.",
        "", "This snapshot does not approve translations, prove PC/WebGL equivalence, or prove raw acquisition when the report says NOT_ACQUIRED.",
        "", "The artifact contains finite status/count fields only. Retain the same local --state-root and --raw-root for persistent incremental operation.",
    ]
    if report.get("reason"):
        lines.insert(6, f"- Typed reason: `{report['reason']}`")
    return "\n".join(lines) + "\n"


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--one-shot", action="store_true")
    args = parser.parse_args(arguments)
    try:
        if not args.scan.is_file():
            raise ValueError("SNAPSHOT_SCAN_REPORT_REQUIRED")
        if args.scan.stat().st_size > MAX_REPORT_BYTES:
            raise ValueError("SNAPSHOT_SCAN_REPORT_SIZE")
        report = project(json.loads(args.scan.read_bytes()), one_shot=args.one_shot)
        exit_code = 0
    except (OSError, ValueError, UnicodeError) as error:
        reason = str(error)
        reason = reason if reason in {"SNAPSHOT_SCAN_REPORT_REQUIRED", "SNAPSHOT_SCAN_REPORT_SIZE", "SNAPSHOT_SCAN_ENVELOPE_REQUIRED", "SNAPSHOT_SOURCE_SCHEMA_REQUIRED"} else "SNAPSHOT_SCAN_REPORT_INVALID"
        report = {
            "schema": "gc-ng11-public-metadata-summary-v1",
            "scope": "ONE_SHOT_METADATA_SNAPSHOT" if args.one_shot else "LOCAL_SCAN_SUMMARY",
            "operation_status": "OBSERVATION_UNAVAILABLE", "observation_status": "UNKNOWN",
            "reason": reason, "comparison_history": "NO_PREVIOUS_RUN_HISTORY" if args.one_shot else "UNKNOWN_BASELINE_ONLY",
            "plan_candidates": 0, "raw_acquisition_status": "UNKNOWN",
            "semantic_review": "NOT_PERFORMED_BY_SCAN", "artifact_projection": "FINITE_STATUS_AND_COUNTS_ONLY",
        }
        exit_code = 2
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    args.summary.write_text(markdown(report), encoding="utf-8")
    print(json.dumps({"status": "SUMMARY_WRITTEN", "operation_status": report["operation_status"], "reason": report.get("reason")}))
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
