"""NG03 adapter for the existing crawl_only known WebGL/R18 endpoints.

Default action observes metadata without mass acquisition. Incremental asset
acquisition is explicit --acquire and bounded by the existing candidate limit.
PC uses only an explicitly named local asset; no URL substitution or search.
"""
import argparse
import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
# Direct workflow entrypoint shares the sibling tools import root with Update CLI.
# Never rely on a previous workspace or caller-owned PYTHONPATH.
sys.path.insert(0, str(ROOT / "tools"))
from tools.update.source_scan import (SourceProfile, UnityTextExtractor, MasterTableExtractor,
    atomic_json, digest, parse_source_ids, scan_inventory, normalize_inventory, MAX_ASSET_BYTES)
from tools.update.source_http import fetch_bytes, fetch_json
from tools.update.novel_bridge import create_intake_references
from scripts.crypto import get_url_params, decrypt_master_text, private_helper

BASE_URL = "https://cdn-r18.gc.dmmgames.com"
ASSET_PATH = "/secure/data/production/webgl/resources/"
ASSETBUNDLE_MANIFEST = "/files/manifest/webgl/r18/assetbundle.json"
MASTER_MANIFEST = "/files/manifest/webgl/r18/master.json"


def existing_novel_ids():
    legacy = {p.parent.name for p in (ROOT / "novels").glob("*/zh_Hans.json") if p.parent.name.isdigit()}
    ko = {p.stem for p in (ROOT / "translations/ko/novels").glob("*.json") if p.stem.isdigit()}
    return legacy, ko, legacy | ko


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--platform", choices=("WebGL", "PC"), default="WebGL")
    p.add_argument("--lane", choices=("Novel", "Master", "UI"), default="Novel")
    p.add_argument("--state-root", type=Path, default=ROOT / "source-state")
    p.add_argument("--raw-root", type=Path, default=ROOT / "private-source-cache")
    p.add_argument("--output", type=Path, default=ROOT / "source-out/scan.json")
    p.add_argument("--inventory", type=Path, help="Saved known inventory; offline replay")
    p.add_argument("--acquire", action="store_true")
    p.add_argument("--source-ids", default=os.environ.get("CRAWL_SOURCE_IDS", ""))
    p.add_argument("--max-candidates", type=int, default=int(os.environ.get("CRAWL_MAX_CANDIDATES", "200")))
    p.add_argument("--local-asset", type=Path)
    p.add_argument("--asset-key", help="Explicit existing locator for a selected PC cache asset")
    p.add_argument("--textasset-name", help="Exact name from an established adapter for multiple TextAssets")
    p.add_argument("--partial", action="store_true")
    p.add_argument("--integrity", action="store_true", help="Explicit byte verification; ordinary owned warm scan is metadata-only")
    p.add_argument("--master-field-scope", type=Path, default=ROOT / "contracts/master-source-fields-v1.json",
                   help="Finite table/runtime-field selector, no translation dictionary values")
    return p


def run(args):
    external_metrics = {"external_source_reads": 0, "external_source_read_bytes": 0,
                        "external_source_hash_calls": 0, "external_source_hash_bytes": 0}
    forced = parse_source_ids(args.source_ids)
    _, _, existing = existing_novel_ids()
    profile = SourceProfile(args.platform, "R18", args.lane,
                            "vendor-d-n-h-s-v1" if args.platform == "WebGL" else "pc-explicit-local-cache-v1")
    if args.platform == "PC" and (not args.local_asset or not args.asset_key):
        result = scan_inventory(profile, None, state_root=args.state_root, raw_root=args.raw_root,
                                observation_error="PC_SOURCE_LOCATOR_REQUIRED")
        publish_intake(result,args)
        atomic_json(args.output, result)
        return result
    if args.lane == "UI" and not args.asset_key:
        result = scan_inventory(profile, None, state_root=args.state_root, raw_root=args.raw_root,
                                observation_error="UI_SUPPORTED_SOURCE_LOCATOR_REQUIRED")
        publish_intake(result,args)
        atomic_json(args.output, result)
        return result
    extractor = None
    if args.acquire:
        if args.lane == "Master":
            helper = private_helper()
            scope = json.loads(args.master_field_scope.read_text(encoding="utf-8-sig"))
            if scope.get("schema") != "gc-master-source-field-selectors-v1" or not isinstance(scope.get("fields"), list):
                raise ValueError("MASTER_FIELD_SELECTOR")
            tables = {}
            for selector in scope["fields"]:
                if not isinstance(selector, dict) or set(selector) != {"table", "runtime_field"}:
                    raise ValueError("MASTER_FIELD_SELECTOR")
                tables.setdefault(selector["table"], []).append(selector["runtime_field"])
            extractor = MasterTableExtractor(decrypt_master_text, helper.ng03_helper_sha256, tables=tables)
        else:
            selections = {args.asset_key: args.textasset_name} if args.asset_key and args.textasset_name else None
            extractor = UnityTextExtractor(selections)
    if args.platform == "PC":
        path = args.local_asset.absolute()
        # A saved inventory replay is explicit and does not claim a fresh external
        # PC observation. A newly observed external file is always read/hashed.
        if args.inventory:
            inventory = json.loads(args.inventory.read_bytes())
        else:
            if not path.is_file() or path.is_symlink():
                raise ValueError("PC_SOURCE_LOCATOR_REQUIRED")
            if path.stat().st_size > MAX_ASSET_BYTES:
                raise ValueError("SOURCE_RAW_SIZE")
            raw = path.read_bytes()
            external_metrics["external_source_reads"] += 1
            external_metrics["external_source_read_bytes"] += len(raw)
            external_metrics["external_source_hash_calls"] += 1
            external_metrics["external_source_hash_bytes"] += len(raw)
            inventory = {"d": [{"n": args.asset_key, "h": digest(raw), "s": len(raw)}]}
        def fetch(item):
            if item["asset_key"] != args.asset_key or not path.is_file() or path.is_symlink():
                raise ValueError("PC_SOURCE_LOCATOR_REQUIRED")
            with path.open("rb") as stream:
                acquired = stream.read(MAX_ASSET_BYTES + 1)
            external_metrics["external_source_reads"] += 1
            external_metrics["external_source_read_bytes"] += len(acquired)
            external_metrics["external_source_hash_calls"] += 1
            external_metrics["external_source_hash_bytes"] += len(acquired)
            if len(acquired) > MAX_ASSET_BYTES or digest(acquired) != item["vendor_token"]:
                raise ValueError("PC_SOURCE_IDENTITY_CHANGED")
            return acquired
        result = scan_inventory(profile, inventory, state_root=args.state_root, raw_root=args.raw_root,
                                fetch_asset=fetch if args.acquire else None, extractor=extractor, existing_ids=existing,
                                forced_ids=forced, complete=False, max_candidates=args.max_candidates,
                                integrity_scan=args.integrity)
    else:
        import httpx
        with httpx.Client(timeout=httpx.Timeout(90.0, connect=30.0), follow_redirects=False,
                          headers={"User-Agent": "gctrans-crawl-only/2.0-ng03"}) as client:
            try:
                if args.inventory and args.inventory.stat().st_size > MAX_ASSET_BYTES:
                    raise ValueError("SOURCE_METADATA_SIZE")
                inventory = json.loads(args.inventory.read_bytes()) if args.inventory else fetch_json(
                    client, BASE_URL + (MASTER_MANIFEST if args.lane == "Master" else ASSETBUNDLE_MANIFEST))
            except (ValueError, OSError, httpx.HTTPError):
                result = scan_inventory(profile, None, state_root=args.state_root, raw_root=args.raw_root,
                                        observation_error="SOURCE_METADATA_OBSERVATION_FAILED")
                publish_intake(result,args)
                atomic_json(args.output, result)
                return result
            if not args.acquire and not (args.state_root / (profile.key + ".json")).exists():
                existing |= {i["source_id"] for i in normalize_inventory(inventory, args.lane)["items"]}
            if args.lane == "UI":
                selected = [item for item in inventory.get("d", []) if item.get("n") == args.asset_key]
                if not selected:
                    raise ValueError("UI_SUPPORTED_SOURCE_LOCATOR_REQUIRED")
                inventory = {"d": selected}
            def fetch(item):
                asset_path = ASSET_PATH + item["asset_key"]
                return fetch_bytes(client, BASE_URL + asset_path, params=get_url_params(asset_path, item["vendor_token"]))
            result = scan_inventory(profile, inventory, state_root=args.state_root, raw_root=args.raw_root,
                                    fetch_asset=fetch if args.acquire else None, extractor=extractor,
                                    existing_ids=existing, forced_ids=forced, complete=not args.partial and args.lane != "UI",
                                    max_candidates=args.max_candidates, integrity_scan=args.integrity)
    result["external_observation_metrics"] = external_metrics
    publish_intake(result,args)
    if "lane_diff" in result:
        result["lane_diff"]["novel_bridge_refs"] = [r["bridge_ref"] for r in result["sourceSnapshot"]["novel"] if r["bridge_ref"]]
    atomic_json(args.output, result)
    return result


def publish_intake(result,args):
    result['raw_acquisition_status']='PARTIAL_FAILED' if result['metrics']['failed'] else 'ACQUIRED_OR_REEXTRACTED' if result['metrics']['materialized'] else 'OWNED_METADATA_REUSE_NOT_BYTE_VERIFIED_THIS_SCAN' if result['metrics']['metadata_only_reuse'] else 'NOT_ACQUIRED'
    intake = create_intake_references(result["sourceSnapshot"], args.output.with_name("novel-intake.json"))
    from tools.update.intake_work import reconcile
    actionable=reconcile(result["sourceSnapshot"],intake,args.state_root)
    atomic_json(args.output.with_name("actionable-intake.json"),actionable)
    result["actionable_intake"]={"path":"actionable-intake.json","new_work":actionable["new_work"],"actionable":len(actionable["actionable"]),"held":actionable["held"],"completed":actionable["completed"]}
    result["novel_intake"] = {"path": "novel-intake.json", "references": len(intake["references"]),
                              "kind":"ELIGIBLE_REFERENCE_CATALOG_NOT_WORK_QUEUE", "status": "READY_FOR_NOVEL_PREPARE" if intake["references"] else "NO_MATERIALIZED_NOVEL"}

def args_acquired(result):
    return result["metrics"].get("materialized",0)>0

def main(arguments=None):
    try:
        result = run(parser().parse_args(arguments))
        observation=result["state"]["observation"]
        unavailable=observation.get("status")=="UNKNOWN"
        operation_status="OBSERVATION_UNAVAILABLE" if unavailable else "PARTIAL_ACQUISITION" if result["metrics"]["failed"] else "RAW_ACQUIRED" if args_acquired(result) else "METADATA_ONLY_PLAN"
        print(json.dumps({"operation_status":operation_status,"reason":observation.get("reason"),"metadata":observation["status"],
                          "raw_acquisition_status":result['raw_acquisition_status'],
                          "metrics": result["metrics"], "deletion_candidates": result["deletion_candidates"],
                          "coverage": result["sourceSnapshot"]["coverage"], "automatic_translation_deletes": 0}))
        return 2 if unavailable or result["metrics"]["failed"] else 0
    except ImportError as error:
        name = (error.name or "").split(".")[0]
        code = "SOURCE_UNITY_DECODER_REQUIRED" if name == "UnityPy" else "SOURCE_HTTP_CLIENT_REQUIRED" if name == "httpx" else "SOURCE_OPTIONAL_DEPENDENCY_REQUIRED"
        print(json.dumps({"status": "UNAVAILABLE", "code": code, "raw_acquisition_status": "NOT_ACQUIRED"}))
        return 2
    except (ValueError, OSError) as error:
        code = str(error)
        print(json.dumps({"status": "REJECTED", "code": code if code.startswith(("SOURCE_", "PC_", "MASTER_", "FORCED_", "UI_")) else "SOURCE_SCAN_FAILED"}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
