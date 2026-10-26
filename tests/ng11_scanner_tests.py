"""Current-checkout scanner entrypoints and persistent-state CPU fixtures.

No HTTP request is sent. Saved metadata is observation replay, and the small
extractor fixture below is not Unity decoding, WB acceptance or native proof.
"""
from __future__ import annotations

import argparse
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
ENTRY = ROOT / "scripts/crawl_only.py"
CLI = ROOT / "tools/update/cli.py"
EVIDENCE = {}
SCRATCH_ROOT = ROOT.parent / "scanner-test-scratch"

# The actual entrypoint executes in an isolated child with a finite fake HTTP
# dependency. Client.stream/send/get fail before any transport or socket exists.
# This provides the dependency without relying on site-packages or PYTHONPATH.
MOCK_ENTRY = r'''
import json, runpy, sys, types
counts = {"clients": 0, "sends": 0}
httpx = types.ModuleType("httpx")
class HTTPError(Exception): pass
class Timeout:
    def __init__(self, *args, **kwargs): pass
class Client:
    def __init__(self, *args, **kwargs): counts["clients"] += 1
    def __enter__(self): return self
    def __exit__(self, *args): return False
    def forbidden(self, *args, **kwargs):
        counts["sends"] += 1
        raise AssertionError("NETWORK_NOT_ALLOWED")
    stream = send = get = forbidden
httpx.HTTPError = HTTPError
httpx.Timeout = Timeout
httpx.Client = Client
sys.modules["httpx"] = httpx
kind, root, entry = sys.argv[1:4]
arguments = sys.argv[4:]
sys.argv = [entry, *arguments]
try:
    if kind == "module":
        sys.path.insert(0, root)
        runpy.run_module("scripts.crawl_only", run_name="__main__")
    else:
        runpy.run_path(entry, run_name="__main__")
finally:
    print("FAKE_TRANSPORT_COUNTS=" + json.dumps(counts), file=sys.stderr)
'''


def clean_environment():
    result = os.environ.copy()
    for key in ("PYTHONPATH", "PYTHONHOME", "NG03_PRIVATE_CRYPTO_HELPER",
                "NG03_PRIVATE_CRYPTO_SHA256", "CRAWL_SOURCE_IDS", "CRAWL_MAX_CANDIDATES"):
        result.pop(key, None)
    return result


def semantic(summary):
    return {key: summary[key] for key in ("operation_status", "reason", "metadata",
                                         "raw_acquisition_status", "metrics", "coverage")}


class ScannerEntrypointTests(unittest.TestCase):
    def setUp(self):
        # Explicit scratch is outside this checkout. No historical tree is used.
        # Windows desktop sandboxes may deny child reads in implicit OS temp.
        SCRATCH_ROOT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="ng11-scanner-", dir=SCRATCH_ROOT)
        self.area = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def command(self, kind, arguments, *, mock_http=False):
        if mock_http:
            entry = CLI if kind == "wrapper" else ENTRY
            args = ["scan", *arguments] if kind == "wrapper" else arguments
            command = [sys.executable, "-I", "-S", "-X", "utf8", "-c", MOCK_ENTRY,
                       kind, str(ROOT), str(entry), *args]
            cwd = self.area
        elif kind == "module":
            command = [sys.executable, "-S", "-X", "utf8", "-m", "scripts.crawl_only", *arguments]
            # Module lookup uses only the named checkout CWD, never old PYTHONPATH.
            cwd = ROOT
        else:
            entry = CLI if kind == "wrapper" else ENTRY
            args = ["scan", *arguments] if kind == "wrapper" else arguments
            command = [sys.executable, "-I", "-S", "-X", "utf8", str(entry), *args]
            cwd = self.area
        result = subprocess.run(command, cwd=cwd, env=clean_environment(),
                                text=True, capture_output=True, timeout=45)
        self.assertNotIn("Traceback", result.stderr, result.stderr)
        self.assertNotIn("ModuleNotFoundError", result.stdout + result.stderr)
        return result

    def paths(self, name, *, state=None, inventory=None, platform="PC"):
        folder = self.area / name
        values = ["--platform", platform, "--state-root", str(self.area / (state or name) / "state"),
                  "--raw-root", str(self.area / (state or name) / "raw"), "--output", str(folder / "scan.json")]
        if inventory:
            values += ["--inventory", str(inventory)]
        return values, folder

    def test_C01_direct_pc_unknown_report_without_optional_dependencies(self):
        arguments, folder = self.paths("direct")
        result = self.command("direct", arguments)
        self.assertEqual(result.returncode, 2, result.stderr)
        summary = json.loads(result.stdout)
        self.assertEqual(summary["operation_status"], "OBSERVATION_UNAVAILABLE")
        self.assertEqual(summary["metadata"], "UNKNOWN")
        self.assertEqual(summary["reason"], "PC_SOURCE_LOCATOR_REQUIRED")
        self.assertEqual(summary["raw_acquisition_status"], "NOT_ACQUIRED")
        report = json.loads((folder / "scan.json").read_bytes())
        self.assertEqual(report["state"]["observation"]["reason"], "PC_SOURCE_LOCATOR_REQUIRED")
        self.assertEqual(json.loads((folder / "novel-intake.json").read_bytes())["references"], [])
        self.assertEqual(json.loads((folder / "actionable-intake.json").read_bytes())["actionable"], [])
        EVIDENCE["direct_pc"] = {"exit": result.returncode, "metadata": "UNKNOWN",
                                  "reason": summary["reason"], "reports": 3,
                                  "isolation": "-I -S; external CWD; no PYTHONPATH/private helper"}

    def test_C01_module_wrapper_and_direct_semantics_scan_to_diff(self):
        summaries, outputs = [], []
        for kind in ("direct", "module", "wrapper"):
            arguments, folder = self.paths(kind)
            result = self.command(kind, arguments)
            self.assertEqual(result.returncode, 2, result.stderr)
            summaries.append(semantic(json.loads(result.stdout)))
            outputs.append(folder / "scan.json")
        self.assertEqual(summaries[0], summaries[1])
        self.assertEqual(summaries[0], summaries[2])
        report = self.area / "diff.json"
        result = subprocess.run(
            [sys.executable, "-I", "-S", "-X", "utf8", str(CLI), "diff",
             "--previous", str(outputs[0]), "--current", str(outputs[2]), "--report", str(report)],
            cwd=self.area, env=clean_environment(), text=True, capture_output=True, timeout=45)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(report.read_bytes())["changes"], [])
        EVIDENCE["pc_entry_parity"] = {"entrypoints": ["direct", "module", "wrapper"],
                                        "typed_exit": 2, "scan_to_diff_exit": 0, "diff_changes": 0}

    def inventory(self, filename, token):
        path = self.area / filename
        path.write_text(json.dumps({"d": [{"n": "notinit/novel_script/sts_10011201.dmm",
                                            "h": token, "s": 100}]}), encoding="utf-8")
        return path

    def test_C01_saved_webgl_direct_module_wrapper_fake_transport(self):
        inventory = self.inventory("saved.json", "fixture-v1")
        summaries = []
        for kind in ("direct", "module", "wrapper"):
            arguments, folder = self.paths(kind, inventory=inventory, platform="WebGL")
            result = self.command(kind, arguments, mock_http=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('"sends": 0', result.stderr)
            self.assertIn('"clients": 1', result.stderr)
            summaries.append(semantic(json.loads(result.stdout)))
            report = json.loads((folder / "scan.json").read_bytes())
            self.assertEqual(report["metrics"]["baseline_only"], 1)
            self.assertEqual(report["plan"], [])
            self.assertEqual(report["metrics"]["source_payloads_acquired"], 0)
            self.assertTrue((folder / "novel-intake.json").is_file())
            self.assertTrue((folder / "actionable-intake.json").is_file())
        self.assertEqual(summaries[0], summaries[1])
        self.assertEqual(summaries[0], summaries[2])
        EVIDENCE["saved_webgl"] = {"entrypoints": ["direct", "module", "wrapper"], "exit": 0,
                                    "fake_transport_requests": 0, "baseline_only": 1,
                                    "raw_acquired": 0, "semantic_acceptance": "NOT_PERFORMED"}

    def test_C01_real_missing_optional_imports_are_typed(self):
        arguments, folder = self.paths("http-missing", inventory=self.inventory("http.json", "v1"), platform="WebGL")
        result = self.command("direct", arguments)
        self.assertEqual(result.returncode, 2)
        self.assertEqual(json.loads(result.stdout)["code"], "SOURCE_HTTP_CLIENT_REQUIRED")
        arguments, folder = self.paths("decoder-missing", inventory=self.inventory("decoder.json", "v1"), platform="WebGL")
        result = self.command("direct", [*arguments, "--acquire"])
        self.assertEqual(result.returncode, 2)
        self.assertEqual(json.loads(result.stdout)["code"], "SOURCE_UNITY_DECODER_REQUIRED")
        EVIDENCE["optional_imports"] = {"isolated_missing_httpx": "SOURCE_HTTP_CLIENT_REQUIRED",
                                       "isolated_missing_UnityPy": "SOURCE_UNITY_DECODER_REQUIRED",
                                       "internal_checkout_imports": "RESOLVED", "native": "NOT_RUN"}

    def test_C03_fresh_state_is_baseline_retained_state_detects_changed_token(self):
        one, two = self.inventory("one.json", "fixture-v1"), self.inventory("two.json", "fixture-v2")
        rows = []
        for label, state, inventory in (("fresh_v1", "fresh_a", one), ("fresh_v2", "fresh_b", two),
                                         ("retained_v1", "retained", one), ("retained_v2", "retained", two)):
            arguments, folder = self.paths(label, state=state, inventory=inventory, platform="WebGL")
            result = self.command("direct", arguments, mock_http=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads((folder / "scan.json").read_bytes())
            rows.append({"label": label, "baseline_only": report["metrics"]["baseline_only"],
                         "plan": [item["reason"] for item in report["plan"]],
                         "raw_acquired": report["metrics"]["source_payloads_acquired"],
                         "actionable_new_work": report["actionable_intake"]["new_work"]})
        self.assertEqual([row["baseline_only"] for row in rows], [1, 1, 1, 0])
        self.assertEqual([row["plan"] for row in rows], [[], [], [], ["source_token_changed"]])
        self.assertTrue(all(row["raw_acquired"] == 0 and row["actionable_new_work"] == 0 for row in rows))
        EVIDENCE["fresh_vs_persistent"] = {"steps": rows, "historical_before_bootstrap": "UNKNOWN",
                                             "CI_persistent_state": "NOT_IMPLEMENTED"}

    def test_C03_partial_retry_reuses_success_then_no_new_intake(self):
        sys.path.insert(0, str(ROOT))
        sys.path.insert(0, str(ROOT / "tools"))
        from tools.update.source_scan import SourceProfile, scan_inventory
        from tools.update.novel_intake import create_intake_references
        from tools.update.source_work import reconcile
        profile = SourceProfile("WebGL", "R18", "Novel", "vendor-d-n-h-s-v1")
        inventory = {"d": [{"n": f"notinit/novel_script/sts_{key}.dmm", "h": token, "s": 100}
                            for key, token in (("99990001", "one"), ("99990002", "two"))]}
        class SyntheticExtractor:
            version = "NG11_CPU_SYNTHETIC_NOT_UNITY_DECODER_v1"
            def __call__(self, raw, asset):
                return {"textassets": [{"name": "script", "path_id": 1,
                                         "container": "synthetic-cpu", "bytes": raw}], "selected": 0}
        fetched = []
        def fetch(asset):
            fetched.append(asset["source_id"])
            if asset["source_id"] == "99990002" and fetched.count("99990002") == 1:
                raise OSError("SYNTHETIC_FIRST_FETCH_FAILURE")
            return ("message,synthetic," + asset["vendor_token"]).encode()
        rows = []
        for index in range(3):
            start = len(fetched)
            result = scan_inventory(profile, inventory, state_root=self.area / "state", raw_root=self.area / "raw",
                                    fetch_asset=fetch, extractor=SyntheticExtractor())
            catalog = create_intake_references(result["sourceSnapshot"], self.area / f"catalog-{index}.json")
            queue = reconcile(result["sourceSnapshot"], catalog, self.area / "state")
            rows.append({"fetched": fetched[start:], "failed": result["metrics"]["failed"],
                         "reused": result["metrics"]["reused"], "materialized": result["metrics"]["materialized"],
                         "plan": [item["reason"] for item in result["plan"]], "new_work": queue["new_work"],
                         "raw_reads": result["metrics"]["raw_cache_reads"], "raw_hash_calls": result["metrics"]["raw_hash_calls"]})
        self.assertEqual(rows[0]["failed"], 1)
        self.assertEqual(rows[1]["fetched"], ["99990002"])
        self.assertEqual(rows[1]["plan"], ["failed_retry"])
        self.assertEqual(rows[1]["reused"], 1)
        self.assertEqual(rows[2]["fetched"], [])
        self.assertEqual(rows[2]["plan"], [])
        self.assertEqual(rows[2]["new_work"], 0)
        self.assertEqual(rows[2]["raw_reads"], 0)
        self.assertEqual(rows[2]["raw_hash_calls"], 0)
        EVIDENCE["partial_retry_no_change"] = {"steps": rows,
                                               "basis": "actual scanner/intake with synthetic extractor; not Unity/WB/native proof",
                                               "completed_semantic_reviews": 0}


def main():
    global SCRATCH_ROOT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--scratch-root", type=Path, default=SCRATCH_ROOT)
    args = parser.parse_args()
    SCRATCH_ROOT = args.scratch_root.resolve()
    if SCRATCH_ROOT.is_relative_to(ROOT):
        parser.error("--scratch-root must be outside the checkout")
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(ScannerEntrypointTests))
    print(stream.getvalue())
    report = {"schema": "gc-ng11-scanner-cpu-evidence-v1", "status": "PASS" if result.wasSuccessful() else "FAIL",
              "tests_run": result.testsRun, "cases": EVIDENCE, "native": "NOT_RUN", "game": "NOT_RUN",
              "source_acquisition": "CPU_MOCK_ONLY", "public_write": "NOT_PERFORMED"}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
