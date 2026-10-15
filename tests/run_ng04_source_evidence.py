"""D01: reuse one explicitly named NG03 private PC bundle, no network or WB replay.

Private raw/decoded rows stay outside candidate. Export only coverage and hashes.
"""
import argparse
import json
import os
import sys
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from tools.update import source_scan as source
from scripts.crawl_only import parser as scan_parser, run as scan


def run(args):
    args.private_output.mkdir(parents=True, exist_ok=True)
    old = json.loads(args.prior_evidence.read_bytes())
    expected = old["actual_pc_master"]["raw"]
    input_bundle = args.prior_raw_root / source.safe_key(expected["path"])
    # Actual identity of the already captured bytes, not a new server observation.
    captured = source.verified_blob(args.prior_raw_root, expected)
    assert source.digest(captured) == expected["sha256"]
    del captured
    os.environ["NG03_PRIVATE_CRYPTO_HELPER"] = str(args.private_helper.absolute())
    os.environ["NG03_PRIVATE_CRYPTO_SHA256"] = source.digest(args.private_helper.read_bytes())
    argv = ["--platform", "PC", "--lane", "Master", "--local-asset", str(input_bundle), "--asset-key", "master.dmm",
            "--acquire", "--state-root", str(args.private_output / "state"), "--raw-root", str(args.private_output / "raw"),
            "--output", str(args.private_output / "pc-master.json")]
    first = scan(scan_parser().parse_args(argv))
    snapshot = first["sourceSnapshot"]
    if first["metrics"]["failed"] or not first["metrics"]["materialized"]:
        source.atomic_json(args.report, {"schema": "gc-ng04-source-evidence-v1", "D01": {"status": "FAIL", "metrics": first["metrics"],
                           "error_codes": [r["error_code"] for r in snapshot["asset_materializations"]]}})
        return 1
    current = snapshot["asset_materializations"][0]
    # Compare all old physical TextAsset references; never ship their bodies.
    prior = json.loads(args.prior_scan.read_bytes())["sourceSnapshot"]["asset_materializations"][0]
    def identities(refs):
        return sorted((r["name"], r["path_id"], r["container"], r["size"], r["sha256"]) for r in refs)
    assert identities(current["textassets"]) == identities(prior["textassets"])
    assert current["raw"] == expected
    dictionary = json.loads((args.current_package / "master.json").read_bytes())
    coverage = []
    for field in snapshot["master_field_coverage"]:
        table, runtime_field = field["table"], field["runtime_field"]
        observed = {r["jp"] for r in snapshot["master"] if r["translation_target"] == {"table": table, "field": runtime_field}}
        accepted = set(dictionary.get(table, {}).get(runtime_field, {}))
        coverage.append({**field, "distinct_raw_jp": len(observed), "accepted_keys_observed": len(observed & accepted),
                         "accepted_keys_not_observed_in_local_generation": len(accepted - observed)})
    # Saved inventory replay, so no fresh external bundle read on the warm scan.
    inventory_file = args.private_output / "saved-local-inventory.json"
    source.atomic_json(inventory_file, {"d": [{"n": "master.dmm", "h": expected["sha256"], "s": expected["size"]}]})
    with patch.object(source, "_cache_read", wraps=source._cache_read) as reads, patch.object(source, "_count_hash", wraps=source._count_hash) as hashes:
        warm = scan(scan_parser().parse_args(argv + ["--inventory", str(inventory_file)]))
        measured = {"raw_cache_read_calls": reads.call_count, "raw_hash_calls": hashes.call_count}
    assert measured == {"raw_cache_read_calls": 0, "raw_hash_calls": 0}
    assert warm["metrics"]["metadata_only_reuse"] == 1
    report = {"schema": "gc-ng04-source-evidence-v1", "D01": {
        "status": "PASS_EXISTING_PRIVATE_LOCAL_RAW_FINITE_FIELD_EXTRACTION",
        "inventory_completeness": "PARTIAL_ONE_PREVIOUSLY_CAPTURED_LOCAL_PC_GENERATION",
        "latest_pc_server_inventory": "NOT_CONFIRMED_PC_SOURCE_LOCATOR_REQUIRED",
        "raw": expected, "all_textassets": len(current["textassets"]), "prior_textasset_identity_equality": True,
        "selector_source": "contracts/master-source-fields-v1.json", "selector_fields": len(coverage),
        "source_rows": len(snapshot["master"]), "observed_tables": len({r["locator"]["table"] for r in snapshot["master"]}),
        "field_coverage": coverage, "extraction_metrics": first["metrics"],
        "external_local_file_metrics": first["external_observation_metrics"],
        "prior_bundle_identity_check_reads": 1, "prior_bundle_identity_check_bytes": expected["size"],
        "warm_measured": measured, "warm_metrics": warm["metrics"], "warm_external_local_file_metrics": warm["external_observation_metrics"],
        "new_remote_source_fetches": 0, "dictionary_used_to_synthesize_source": False, "raw_or_decoded_exported": False},
        "lane_coverage": {
            "Master": "ACCEPTED_TRANSLATIONS_RESTORED_SEPARATELY; EXISTING_LOCAL_PC_FINITE_FIELDS_OBSERVED",
            "UI": {"status": "PARTIAL", "required_input": "PC_UI_SUPPORTED_SOURCE_ADAPTER_REQUIRED", "existing_accepted_UI_preserved": True},
            "Novel": {"status": "PARTIAL_CURRENT_PC_INVENTORY", "existing_WB_qualified_chain": "NG03_PRIOR_EVIDENCE_PRESERVED_NO_ALL_JOB_REPLAY",
                      "prior_qualified_source_count": old["existing_m4_bridge"]["qualified_sources"], "required_input": "PC_SOURCE_LOCATOR_REQUIRED_FOR_REQUESTED_CURRENT_SOURCE"},
            "Names": {"status": "PARTIAL", "required_input": "NAMES_QUALIFIED_SOURCE_ADAPTER_REQUIRED", "Master_JP_substitution": False}},
        "known_webgl_metadata": {"fresh_run": "NOT_RUN", "prior_result": old["actual_webgl_metadata"]},
        "existing_WB_parser_or_locator_modified": False, "new_acceptance_created": False,
        "native": "NOT_RUN", "game": "NOT_RUN", "installation": "NOT_PERFORMED", "publication": "NOT_PERFORMED", "XUnity": "OFF"}
    source.atomic_json(args.report, report)
    print(json.dumps({"D01": report["D01"]["status"], "fields": len(coverage), "tables": report["D01"]["observed_tables"],
                      "rows": len(snapshot["master"]), "all_textassets": len(current["textassets"]), "warm_measured": measured}))
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prior-raw-root", type=Path, required=True)
    parser.add_argument("--prior-evidence", type=Path, required=True)
    parser.add_argument("--prior-scan", type=Path, required=True)
    parser.add_argument("--private-helper", type=Path, required=True)
    parser.add_argument("--private-output", type=Path, required=True)
    parser.add_argument("--current-package", type=Path, default=ROOT / "translations/ko")
    parser.add_argument("--report", type=Path, default=ROOT / "audit/NG04_SOURCE_EVIDENCE.json")
    raise SystemExit(run(parser.parse_args()))
