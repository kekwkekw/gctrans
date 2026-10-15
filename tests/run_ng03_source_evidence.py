"""Reproducible actual product CPU, local PC raw and existing M4 bridge evidence.

External inputs are explicit, read-only. Raw/decoded/candidate outputs belong in
private disposable scratch outside the exported candidate repository.
"""
import argparse
import copy
import io
import json
import os
import sys
import unittest
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from tools.update.source_scan import atomic_json, digest
from tools.update.novel_bridge import wb_scene_source_references, validate_accepted_return


def run(args):
    output = args.private_output / str(uuid.uuid4())
    output.mkdir(parents=True)
    log = io.StringIO()
    suite = unittest.defaultTestLoader.discover(str(ROOT / "tests"), pattern="test_ng03_source.py")
    tested = unittest.TextTestRunner(stream=log, verbosity=2).run(suite)
    report = {"schema": "gc-ng03-source-evidence-v1", "product_cpu_mock_http": {
        "status": "PASS" if tested.wasSuccessful() else "FAIL", "tests_run": tested.testsRun,
        "scenarios": [f"S{i:02}" for i in range(1, 12)], "fixture_is_real_source_coverage": False},
        "actual_webgl_metadata": "NOT_RUN", "actual_webgl_bundle_acquisition": "NOT_RUN",
        "actual_pc_master": "NOT_RUN", "actual_pc_ui": {"status": "PARTIAL", "reason": "PC_UI_SUPPORTED_SOURCE_ADAPTER_REQUIRED"},
        "actual_pc_novel": {"status": "PARTIAL", "reason": "PC_SOURCE_LOCATOR_REQUIRED_FOR_REQUESTED_CURRENT_SOURCE"},
        "existing_m4_bridge": "NOT_RUN", "native_hook_installation": "NOT_RUN", "game_validation": "NOT_RUN",
        "publications": "NOT_PERFORMED", "XUnity": "OFF", "game_actions": 0, "installation_writes": 0,
        "credential_material_in_export": False}
    if args.report.exists():
        previous = json.loads(args.report.read_bytes())
        if not args.observe_known_source:
            report["actual_webgl_metadata"] = previous.get("actual_webgl_metadata", "NOT_RUN")
        if not args.pc_master:
            report["actual_pc_master"] = previous.get("actual_pc_master", "NOT_RUN")
        if not args.workbench_root or not args.m4_plan:
            report["existing_m4_bridge"] = previous.get("existing_m4_bridge", "NOT_RUN")
            if "m4_negative_cases" in previous:
                report["m4_negative_cases"] = previous["m4_negative_cases"]
    (args.report.parent / "NG03_SOURCE_TESTS.log").write_text(log.getvalue(), encoding="utf-8")
    if args.pc_master:
        from scripts.crawl_only import parser, run as scan
        if args.private_helper:
            os.environ["NG03_PRIVATE_CRYPTO_HELPER"] = str(args.private_helper)
        options = parser().parse_args(["--platform", "PC", "--lane", "Master", "--local-asset", str(args.pc_master),
                                      "--asset-key", "master.dmm", "--acquire", "--state-root", str(output / "state"),
                                      "--raw-root", str(output / "raw"), "--output", str(output / "pc-master.json")])
        result = scan(options)
        snapshot = result["sourceSnapshot"]
        report["actual_pc_master"] = {"status": "PASS_SELECTED_LOCAL_RAW_AND_TWO_TABLE_FIELDS" if result["metrics"]["materialized"] else "FAIL",
            "inventory_completeness": "PARTIAL_ONE_EXPLICIT_LOCAL_CACHE_GENERATION", "raw": snapshot["asset_materializations"][0]["raw"],
            "all_textassets_preserved": len(snapshot["asset_materializations"][0]["textassets"]),
            "table_fields": sorted({(r["locator"]["table"], r["locator"]["field"]) for r in snapshot["master"]}),
            "translation_target_fields": sorted({(r["translation_target"]["table"], r["translation_target"]["field"]) for r in snapshot["master"] if "translation_target" in r}),
            "mapping_contracts": sorted({r["structure"]["mapping_contract"] for r in snapshot["master"] if "mapping_contract" in r["structure"]}),
            "physical_fields": sorted({r["structure"]["physical_field"] for r in snapshot["master"]}),
            "source_rows": len(snapshot["master"]), "metrics": result["metrics"],
            "other_master_tables": "PARTIAL_NO_FIELD_ADAPTER", "endpoint": "PC_SOURCE_LOCATOR_REQUIRED_FOR_SERVER_INVENTORY",
            "raw_exported": False, "uses_existing_helper": True}
        # Test the actual extracted row against the actual c03 deployment schema
        # in memory. This probe is discarded; it creates no review or KO files.
        from tools.update.diff_review import apply_row, fingerprint, source_fingerprint
        master_path = args.current_package / "master.json"
        master_raw = master_path.read_bytes()
        master_doc = json.loads(master_raw)
        peer_contexts = {}
        for row in snapshot["master"]:
            scope = (row["locator"]["table"], row["locator"]["field"], row["jp"])
            peer_contexts.setdefault(scope, set()).add(fingerprint({"context": row["context"], "structure": row["structure"]}))
        eligible = [row for row in snapshot["master"] if
                    len(peer_contexts[(row["locator"]["table"], row["locator"]["field"], row["jp"])]) == 1 and
                    row["jp"] in master_doc.get(row["translation_target"]["table"], {}).get(row["translation_target"]["field"], {})]
        if not eligible:
            report["actual_pc_master"]["actual_dictionary_apply_boundary"] = {"status": "PARTIAL", "reason": "NO_UNIQUE_CONTEXT_RUNTIME_JP_TARGET"}
        else:
            row = eligible[0]
            keys = [row["translation_target"]["table"], row["translation_target"]["field"], row["jp"]]
            old_ko = master_doc[keys[0]][keys[1]][keys[2]]
            reviewed = {"lane": "master", "locator": row["locator"], "decision": "APPROVE",
                        "source_fingerprint": source_fingerprint(snapshot["profile"], "master", row),
                        "target": {"file": "master.json", "keys": keys},
                        "old_ko_sha256": fingerprint(old_ko), "new_ko": old_ko + " [CPU boundary probe]"}
            disposable_doc = copy.deepcopy(master_doc)
            applied = apply_row(disposable_doc, reviewed, snapshot)
            assert applied and disposable_doc[keys[0]][keys[1]][keys[2]] == reviewed["new_ko"]
            assert digest(master_path.read_bytes()) == digest(master_raw)
            report["actual_pc_master"]["actual_dictionary_apply_boundary"] = {
                "status": "PASS_ACTUAL_SOURCE_ROW_TO_ACTUAL_C03_DICTIONARY_IN_MEMORY",
                "source_locator": row["locator"], "translation_target": row["translation_target"],
                "jp_sha256": digest(row["jp"].encode("utf-8")), "master_input_sha256": digest(master_raw),
                "target_context_unique": True, "discarded_cpu_probe": True,
                "ko_files_changed": 0, "semantic_review_created": False}
    if args.workbench_root and args.m4_plan:
        raw = args.m4_plan.read_bytes()
        plan = json.loads(raw)
        refs, old = {}, {}
        for job in plan["jobs"]:
            argv = job["wb20_args"]
            scene = json.loads(Path(argv[argv.index("--bound-scene") + 1]).read_bytes())
            refs.update(wb_scene_source_references(scene))
            relative = "novels/" + job["script_id"] + ".json"
            old[relative] = digest((args.current_package / relative).read_bytes())
        operation = {"workbench_root": str(args.workbench_root),
                     "implementation_sha256": digest((args.workbench_root / "wb/local_release_cli.py").read_bytes()),
                     "m4_plan": {"path": str(args.m4_plan), "sha256": digest(raw)},
                     "reviewed_source_refs": refs, "old_ko": old}
        try:
            result = validate_accepted_return(operation, args.current_package, refs, output / "m4-positive")
            report["existing_m4_bridge"] = {"status": "PASS_ACTUAL_EXISTING_WB20_M4_REPLAY",
                "existing_jobs_replayed": result["wb_result"]["current_input_replays"], "qualified_sources": len(result["qualified_sources"]),
                "returned_novel_files": len(result["touched"]), "returned_payload_sha256": {p: digest(b) for p, b in result["payloads"].items()},
                "historical_raw_index_invented": False, "automatic_registration": False,
                "new_acceptance_created": False, "return_files_applied_to_ng03": False}
        except ValueError as error:
            report["existing_m4_bridge"] = {"status": "PARTIAL", "reason": str(error)}
        negatives = {}
        changed_refs = dict(refs)
        first = next(iter(changed_refs))
        changed_refs[first] = {**changed_refs[first], "sha256": "0" * 64}
        try:
            validate_accepted_return(operation, args.current_package, changed_refs, output / "m4-stale-source")
        except ValueError as error:
            negatives["stale_source"] = str(error)
        changed_operation = {**operation, "old_ko": {**old, next(iter(old)): "0" * 64}}
        try:
            validate_accepted_return(changed_operation, args.current_package, refs, output / "m4-stale-ko")
        except ValueError as error:
            negatives["concurrent_ko"] = str(error)
        report["m4_negative_cases"] = negatives
    if args.observe_known_source:
        import httpx
        from tools.update.source_http import fetch_json
        from tools.update.source_scan import SourceProfile, scan_inventory, normalize_inventory
        observations = {}
        with httpx.Client(timeout=httpx.Timeout(15, connect=10), follow_redirects=False) as client:
            for lane, endpoint in (("Novel", "assetbundle.json"), ("Master", "master.json")):
                try:
                    manifest = fetch_json(client, "https://cdn-r18.gc.dmmgames.com/files/manifest/webgl/r18/" + endpoint, attempts=1)
                    items = normalize_inventory(manifest, lane)["items"]
                    profile = SourceProfile("WebGL", "R18", lane, "vendor-d-n-h-s-v1")
                    observed = scan_inventory(profile, manifest, state_root=output / "webgl-state", raw_root=output / "webgl-raw",
                                              existing_ids={i["source_id"] for i in items}, max_candidates=200)
                    atomic_json(output / ("observed-" + endpoint), manifest)
                    observations[lane] = {"status": "METADATA_OBSERVED_BASELINE_ONLY", "inventory_entries": len(items),
                                          "metadata_sha256": observed["state"]["observation"]["sha256"],
                                          "raw_assets_fetched": 0, "historical_before_bootstrap": "UNKNOWN"}
                except Exception as error:
                    code = str(error)
                    observations[lane] = {"status": "PARTIAL", "reason": code if code.startswith("SOURCE_") else "KNOWN_ENDPOINT_READ_FAILED", "raw_assets_fetched": 0}
        report["actual_webgl_metadata"] = observations
    atomic_json(args.report, report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if tested.wasSuccessful() else 2


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--private-output", type=Path, required=True)
    p.add_argument("--report", type=Path, required=True)
    p.add_argument("--pc-master", type=Path)
    p.add_argument("--private-helper", type=Path)
    p.add_argument("--workbench-root", type=Path)
    p.add_argument("--m4-plan", type=Path)
    p.add_argument("--current-package", type=Path, default=ROOT / "translations/ko")
    p.add_argument("--observe-known-source", action="store_true")
    raise SystemExit(run(p.parse_args()))
