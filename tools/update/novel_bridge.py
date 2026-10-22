"""Source catalog and compatibility adapter for existing WB13-WB20/M4 returns.

Ordinary translation uses novel_workflow. Existing WB return replay remains
available for historical work; source acquisition alone is not semantic review.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

from .source_scan import (MAX_ASSET_BYTES, atomic_json, digest, json_bytes, safe_key,
                          source_item_eligible, verified_blob)


class NovelReviewConflict(ValueError):
    """Expected per-return review/pin/QA refusal; independent lanes may proceed."""


def _required_input_bytes(path: Path, code: str) -> bytes:
    try:
        return Path(path).read_bytes()
    except OSError:
        raise NovelReviewConflict(code) from None


def raw_capture_locator(profile: dict, item: dict, textasset: dict) -> str:
    return digest(json_bytes({"profile": profile, "asset_key": item["asset_key"],
                              "textasset": {k: textasset[k] for k in ("name", "path_id", "container")}}))


def _eligible_novel_items(snapshot):
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN': return []
    items = snapshot.get("novel", [])
    counts = {}
    for item in items:
        counts[item["source_id"]] = counts.get(item["source_id"], 0) + 1
    return [item for item in items if counts[item["source_id"]] == 1 and source_item_eligible(item)
            and item.get("selected_textasset") is not None]


def create_intake_references(snapshot: dict, destination: Path, *, raw_root=None) -> dict:
    references = []
    for item in _eligible_novel_items(snapshot):
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
                           "compatibility_entrypoints": ["wb.work_items_cli", "wb.source_scene_cli", "wb.source_pilot_cli", "wb.local_release_cli"],
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
    candidates = [item for item in _eligible_novel_items(snapshot)
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
    # Bundle/script extraction alone cannot establish the WB19 physical/view chain.
    qualified = snapshot.get("qualified_sources", {})
    items = snapshot.get("novel", [])
    allowed = {(item["source_id"], item["asset_key"]) for item in _eligible_novel_items(snapshot)}
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
        # Owner tags establish eligibility, not a new WB locator/pin format.
        # Preserve all other pin fields: unknown extras still fail existing WB
        # exact comparison instead of being silently stripped into approval.
        pin = {name: value for name, value in ref.items() if name not in {"source_id", "asset_key"}}
        if not has_owner:
            if not ineligible:
                result[key] = pin
        elif identity in allowed:
            result[key] = pin
    return result


def wb_scene_source_references(scene: dict) -> dict:
    """Existing WB19 locators only; caller must validate the actual WB inputs.

    This reads locator metadata from an existing bound scene. It does not admit
    the scene into apply; validate_accepted_return replays WB20/M4 before output.
    """
    if scene.get("schema") != "WB19_BOUND_SOURCE_QUALIFIED_SCENE_V1":
        raise NovelReviewConflict("NOVEL_WB19_BOUND_SCENE_REQUIRED")
    refs = {}
    for row in scene["rows"]:
        locator = row["source_locator"]
        if not isinstance(locator, dict) or not {"parser", "transform", "position_mapping", "raw_textasset_sha256", "parser_view_sha256", "source_chain_digest"} <= locator.keys():
            raise NovelReviewConflict("NOVEL_EXISTING_QUALIFIED_LOCATOR_REQUIRED")
        # Exact existing wb.safety.canonical/digest contract; no new locator ID.
        encoded = json.dumps(locator, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")
        refs[digest(encoded)] = {"size": len(encoded), "sha256": digest(encoded)}
    return refs

def bind_existing_wb_scene(snapshot: dict, scene_path: Path, scene_sha256: str,
                           source_id: str, asset_key: str, raw_root: Path) -> dict:
    """Attach the existing WB19 physical pins to exactly one eligible raw capture.

    No parser/locator is invented and no semantic review is completed here.
    """
    import copy
    raw=_required_input_bytes(scene_path,'NOVEL_EXISTING_WB_SCENE_IO')
    if digest(raw)!=scene_sha256: raise NovelReviewConflict('NOVEL_EXISTING_WB_SCENE_PIN')
    scene=json.loads(raw);pins=wb_scene_source_references(scene)
    items=[i for i in _eligible_novel_items(snapshot) if i['source_id']==source_id and i['asset_key']==asset_key]
    if len(items)!=1: raise NovelReviewConflict('NOVEL_CURRENT_RAW_CAPTURE_REQUIRED')
    item=items[0];selected=item['selected_textasset']
    verified_blob(Path(raw_root),item['raw']);verified_blob(Path(raw_root),selected)
    locators={digest(json.dumps(row['source_locator'],ensure_ascii=False,sort_keys=True,separators=(',',':'),allow_nan=False).encode('utf-8')):row['source_locator'] for row in scene['rows']}
    if not pins or any(p.get('raw_textasset_sha256')!=selected['sha256'] for p in locators.values()):
        raise NovelReviewConflict('NOVEL_WB_PHYSICAL_RAW_CAPTURE_MISMATCH')
    output=copy.deepcopy(snapshot)
    for key,pin in pins.items():
        value=dict(pin,source_id=source_id,asset_key=asset_key)
        existing=output.setdefault('qualified_sources',{}).get(key)
        if existing is not None and existing!=value: raise NovelReviewConflict('NOVEL_WB_QUALIFIED_OWNER_COLLISION')
        output['qualified_sources'][key]=value;output.setdefault('qualified_locators',{})[key]=locators[key]
    output['snapshot_id']=digest(json_bytes({k:v for k,v in output.items() if k!='snapshot_id'}))
    return output


def validate_accepted_return(operation: dict, current_package: Path, current_source_refs: dict,
                             output_root: Path) -> dict:
    """Re-run actual M4: current WB20 inputs -> acceptance -> runtime bytes.

    Caller supplies current source references; a past review's original source
    file alone cannot establish current freshness. Compare only selected KO
    files, so unrelated package changes do not invalidate this review.
    """
    required = {"workbench_root", "implementation_sha256", "m4_plan", "reviewed_source_refs", "old_ko"}
    if not isinstance(operation, dict) or not required <= operation.keys():
        raise NovelReviewConflict("NOVEL_BRIDGE_INPUT_REQUIRED")
    reviewed = operation["reviewed_source_refs"]
    if not isinstance(reviewed, dict) or not reviewed or not isinstance(current_source_refs, dict):
        raise NovelReviewConflict("NOVEL_BRIDGE_INPUT_REQUIRED")
    if any(current_source_refs.get(key) != ref for key, ref in reviewed.items()):
        raise NovelReviewConflict("NOVEL_STALE_SOURCE_REVIEW")
    root = Path(operation["workbench_root"]).resolve()
    implementation = root / "wb" / "local_release_cli.py"
    if digest(_required_input_bytes(implementation, "NOVEL_WB_IMPLEMENTATION_INPUT_IO")) != operation["implementation_sha256"]:
        raise NovelReviewConflict("NOVEL_WB_IMPLEMENTATION_CHANGED")
    old_ko = operation["old_ko"]
    if not isinstance(old_ko, dict) or not old_ko:
        raise NovelReviewConflict("NOVEL_BRIDGE_INPUT_REQUIRED")
    for relative, expected in old_ko.items():
        try:
            safe_key(relative)
        except ValueError:
            raise NovelReviewConflict("NOVEL_RETURN_TARGET") from None
        if not relative.startswith("novels/") or relative.count("/") != 1 or not relative.endswith(".json"):
            raise NovelReviewConflict("NOVEL_RETURN_TARGET")
        target = Path(current_package) / relative
        if digest(_required_input_bytes(target, "NOVEL_CURRENT_KO_INPUT_IO")) != expected:
            raise NovelReviewConflict("NOVEL_CONCURRENT_KO_MODIFICATION")
    plan_ref = operation["m4_plan"]
    plan = Path(plan_ref["path"])
    raw = _required_input_bytes(plan, "NOVEL_M4_PLAN_INPUT_IO")
    if len(raw) > 1024 * 1024 or digest(raw) != plan_ref["sha256"]:
        raise NovelReviewConflict("NOVEL_M4_PLAN_PIN")
    try:
        parsed = json.loads(raw)
    except (ValueError, UnicodeError):
        raise NovelReviewConflict("NOVEL_M4_PLAN_SCHEMA") from None
    if parsed.get("schema") != "M4_LOCAL_ACCEPTED_NOVEL_PLAN_V1":
        raise NovelReviewConflict("NOVEL_M4_PLAN_SCHEMA")
    plan_targets = {f"novels/{job['script_id']}.json" for job in parsed["jobs"]}
    if plan_targets != set(old_ko):
        raise NovelReviewConflict("NOVEL_RETURN_SCOPE")
    output_root = Path(output_root).absolute()
    if output_root.exists() or output_root.resolve().is_relative_to(root):
        raise NovelReviewConflict("NOVEL_BRIDGE_OUTPUT_REQUIRED_NEW")
    env = dict(os.environ)
    env.pop("PYTHONPATH", None)
    env.pop("PYTHONHOME", None)
    try:
        process = subprocess.run([sys.executable, "-B", "-X", "utf8", str(Path(__file__).with_name("novel_bridge_worker.py")),
                                  "--workbench-root", str(root), "--current-package", str(Path(current_package).absolute()),
                                  "--plan", str(plan.absolute()), "--plan-sha256", plan_ref["sha256"],
                                  "--output", str(output_root)], cwd=root, env=env, capture_output=True, timeout=300)
    except subprocess.TimeoutExpired:
        raise NovelReviewConflict("NOVEL_WB_REPLAY_TIMEOUT") from None
    try:
        result = json.loads(process.stdout)
    except (ValueError, UnicodeError):
        raise RuntimeError("NOVEL_WB_REPLAY_INTERNAL_FAILURE") from None
    if process.returncode != 0 or result.get("status") != "EXISTING_WB20_M4_ACCEPTED_RUNTIME_RETURN_VERIFIED":
        code = str(result.get("code", "REJECTED"))
        if code == "INVALID_INPUT_OR_IO":
            raise RuntimeError("NOVEL_WB_REPLAY_INTERNAL_OR_IO_FAILURE")
        raise NovelReviewConflict("NOVEL_WB_REPLAY_REJECTED:" + code)
    qualified = {}
    for job in parsed["jobs"]:
        arguments = job["wb20_args"]
        scene_path = Path(arguments[arguments.index("--bound-scene") + 1])
        if not scene_path.is_absolute():
            scene_path = root / scene_path  # identical base to the existing worker cwd
        scene_pin = arguments[arguments.index("--bound-scene-sha256") + 1]
        scene_bytes = _required_input_bytes(scene_path, "NOVEL_CURRENT_WB_SOURCE_INPUT_IO")
        if digest(scene_bytes) != scene_pin:
            raise NovelReviewConflict("NOVEL_CURRENT_WB_SOURCE_CHANGED")
        qualified.update(wb_scene_source_references(json.loads(scene_bytes)))
    if any(qualified.get(key) != ref for key, ref in reviewed.items()):
        raise NovelReviewConflict("NOVEL_CURRENT_WB_SOURCE_CHANGED")
    # Recheck concurrency after the potentially lengthy existing QA replay.
    for relative, expected in old_ko.items():
        if digest(_required_input_bytes(Path(current_package) / relative, "NOVEL_CURRENT_KO_INPUT_IO")) != expected:
            raise NovelReviewConflict("NOVEL_CONCURRENT_KO_MODIFICATION")
    payloads = {}
    for relative in sorted(plan_targets):
        candidate_file = output_root / "runtime-return" / relative
        body = candidate_file.read_bytes()
        if len(body) > MAX_ASSET_BYTES or not isinstance(json.loads(body), dict):
            raise NovelReviewConflict("NOVEL_RUNTIME_RETURN_SCHEMA")
        if digest(body) != result["payloads"][relative]["sha256"] or len(body) != result["payloads"][relative]["size"]:
            raise NovelReviewConflict("NOVEL_RUNTIME_RETURN_IDENTITY")
        payloads[relative] = body
    return {"payloads": payloads, "touched": sorted(payloads), "wb_result": result,
            "qualified_sources": qualified,
            "accepted_return_verified": True, "semantic_approval_created": False,
            "historical_raw_index_invented": False, "automatic_registration": False}
