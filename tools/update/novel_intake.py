"""Current materialized Novel eligibility and exact source-reference catalog."""
from __future__ import annotations
import re
from pathlib import Path
from .source_scan import atomic_json, digest, json_bytes, source_item_eligible, verified_blob


def raw_capture_locator(profile: dict, item: dict, textasset: dict) -> str:
    return digest(json_bytes({"profile": profile, "asset_key": item["asset_key"],
                              "textasset": {k: textasset[k] for k in ("name", "path_id", "container")}}))


def eligible_novel_items(snapshot):
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN': return []
    items = snapshot.get("novel", [])
    counts = {}
    for item in items:
        counts[item["source_id"]] = counts.get(item["source_id"], 0) + 1
    return [item for item in items if counts[item["source_id"]] == 1 and source_item_eligible(item)
            and item.get("selected_textasset") is not None]


def create_intake_references(snapshot: dict, destination: Path, *, raw_root=None) -> dict:
    references = []
    for item in eligible_novel_items(snapshot):
        selected = item.get("selected_textasset")
        if raw_root is not None:
            verified_blob(Path(raw_root), item["raw"])
            verified_blob(Path(raw_root), selected)
        references.append({"source_id": item["source_id"], "profile": snapshot["profile"],
                           "asset_key": item["asset_key"], "vendor_token": item["vendor_token"],
                           "bundle": item["raw"], "textasset": selected,
                           "raw_capture_locator": raw_capture_locator(snapshot["profile"], item, selected),
                           "source_qualified_locator": None,
                           "extractor": item["extractor"], "historical_raw_index": None,
                           "old_source": "UNKNOWN_UNLESS_SOURCE_PAIR_SUPPLIED",
                           "entrypoint": "python tools/update/cli.py novel prepare",
                           "status": "READY_FOR_NOVEL_PREPARE",
                           "raw_integrity": "VERIFIED_AT_THIS_INTAKE" if raw_root is not None else "VERIFY_AT_BYTE_CONSUMPTION_REQUIRED",
                           "required_inputs": ["current source snapshot and selected TextAsset bytes",
                                               "current repository Names and Knowledge",
                                               "translation/HOLD_POLICY.md",
                                               "Codex draft followed by PRO-reviewed result"],
                           "automatic_semantic_approval": False})
    result = {"schema": "gc-ng03-novel-intake-references-v1", "kind":"ELIGIBLE_REFERENCE_CATALOG_NOT_ACTIONABLE_QUEUE", "source_snapshot_id": snapshot["snapshot_id"],
              "references": references, "translation_entrypoint": "tools/update/cli.py novel",
              "automatic_registration": False}
    atomic_json(Path(destination), result)
    return result


def consume_intake_reference(snapshot: dict, reference: dict, raw_root: Path, *, metrics=None) -> bytes:
    """Actual byte boundary; metadata-only reuse cannot approve corrupt scripts."""
    candidates = [item for item in eligible_novel_items(snapshot)
                  if item["source_id"] == reference.get("source_id") and item["asset_key"] == reference.get("asset_key")]
    if len(candidates) != 1:
        raise ValueError("SOURCE_INTAKE_NOT_CURRENT_ELIGIBLE")
    item = candidates[0]
    selected = item["selected_textasset"]
    if (reference.get("profile") != snapshot["profile"] or reference.get("vendor_token") != item["vendor_token"]
            or reference.get("extractor") != item["extractor"] or reference.get("bundle") != item["raw"]
            or reference.get("textasset") != selected
            or reference.get("raw_capture_locator") != raw_capture_locator(snapshot["profile"], item, selected)):
        raise ValueError("SOURCE_INTAKE_REFERENCE_CHANGED")
    verified_blob(Path(raw_root), item["raw"], metrics)
    return verified_blob(Path(raw_root), selected, metrics)


def current_source_references(snapshot: dict) -> dict:
    # Bundle/script extraction alone cannot establish a qualified physical/view chain.
    qualified = snapshot.get("qualified_sources", {})
    items = snapshot.get("novel", [])
    allowed = {(item["source_id"], item["asset_key"]) for item in eligible_novel_items(snapshot)}
    ineligible = any((item["source_id"], item["asset_key"]) not in allowed for item in items)
    result = {}
    for key, ref in qualified.items():
        if not isinstance(ref, dict):
            continue
        if (type(ref.get("size")) is not int or ref["size"] < 0
                or not isinstance(ref.get("sha256"), str) or not re.fullmatch(r"[0-9a-f]{64}", ref["sha256"])):
            continue
        has_owner = "source_id" in ref or "asset_key" in ref
        if has_owner and any(not isinstance(ref.get(name), str) or not ref[name]
                             for name in ("source_id", "asset_key")):
            continue
        identity = (ref.get("source_id"), ref.get("asset_key"))
        # Owner tags establish eligibility, not a new locator/pin format.
        # Preserve all other pin fields: unknown extras still fail existing consumer
        # exact comparison instead of being silently stripped into approval.
        pin = {name: value for name, value in ref.items() if name not in {"source_id", "asset_key"}}
        if not has_owner:
            if not ineligible:
                result[key] = pin
        elif identity in allowed:
            result[key] = pin
    return result
