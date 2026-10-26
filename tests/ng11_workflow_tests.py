"""NG11 workflow boundary tests using actual local product entrypoints.

Saved WebGL metadata and a network-denying httpx fixture do not establish
current source coverage or an actual GitHub Actions run. No workflow dispatch,
CDN request, native call, translation decision or runtime install occurs here.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from tools.update.snapshot_summary import project, markdown


def envelope():
    return {
        "sourceSnapshot": {
            "schema": "gc-ng03-source-snapshot-v1",
            "profile": {"platform": "WebGL", "partition": "Novel", "private": "private-marker"},
            "metadata": {"status": "OBSERVED", "asset_keys": ["signed-url-marker"]},
            "coverage": {"metadata": "OBSERVED", "lane_status": "PARTIAL",
                         "raw_materialized_assets": 0, "successful_assets": 0,
                         "baseline_only_assets": 1, "pending_assets": 0,
                         "failed_assets": 0, "collision_assets": 0,
                         "master": "MASTER_BODY_DECODER_REQUIRED", "ui": "UI_SOURCE_ADAPTER_REQUIRED",
                         "historical_before_bootstrap": "UNKNOWN", "automatic_translation_deletes": 0},
            "private_raw": "raw-marker",
        },
        "metrics": {"materialized": 0, "failed": 0, "baseline_only": 1, "source_payloads_acquired": 0},
        "plan": [], "raw_acquisition_status": "NOT_ACQUIRED",
        "lane_diff": {"historical_baseline": "UNKNOWN_BASELINE_ONLY"},
        "state": {"raw_root": "private-path-marker"},
        "actionable_intake": {"new_work": 0, "actionable": 0, "held": 0, "completed": 0,
                              "path": "private-path-marker"},
    }


def scalar_command(text, starts_with):
    commands = [match.group(1).strip() for match in re.finditer(r"^\s*run:\s*(.+)$", text, re.MULTILINE)]
    candidates = [command for command in commands if command.startswith(starts_with)]
    if len(candidates) != 1:
        raise AssertionError("WORKFLOW_COMMAND_CARDINALITY")
    return shlex.split(candidates[0])


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="ng11-workflow-fixture-", dir=ROOT / "tests")
        self.area = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def run_summary(self, value=None):
        scan = self.area / "scan.json"
        if value is not None:
            scan.write_text(json.dumps(value), encoding="utf-8")
        command = [sys.executable, "-I", "-S", str(ROOT / "tools/update/snapshot_summary.py"),
                   "--scan", str(scan), "--summary", str(self.area / "summary.md"),
                   "--report", str(self.area / "summary.json"), "--one-shot"]
        process = subprocess.run(command, cwd=self.area, capture_output=True, text=True, timeout=30)
        return process, json.loads((self.area / "summary.json").read_text(encoding="utf-8"))

    def test_C02_retired_writer_is_absent(self):
        self.assertFalse((ROOT / ".github/workflows/update_translation.yml").exists())

    def test_C03_snapshot_has_no_write_raw_or_ci_state_restore(self):
        text = (ROOT / ".github/workflows/crawl_only.yml").read_text(encoding="utf-8")
        self.assertIn("One-shot WebGL Metadata Snapshot", text)
        self.assertRegex(text, r"(?m)^  workflow_dispatch:\s*$")
        events = re.search(r"(?ms)^on:\n(.*?)(?=^[^\s])", text).group(1)
        self.assertEqual(re.findall(r"(?m)^  ([a-z_]+):", events), ["workflow_dispatch"])
        self.assertIn("contents: read", text)
        self.assertIn("persist-credentials: false", text)
        self.assertIn("CRAWL_SOURCE_IDS: ${{ inputs.source_ids }}", text)
        self.assertIn("CRAWL_MAX_CANDIDATES: ${{ inputs.max_candidates }}", text)
        command = scalar_command(text, "python scripts/crawl_only.py")
        self.assertNotIn("--acquire", command)
        self.assertEqual(command[-2:], ["--output", "crawl_out/scan.json"])
        for forbidden in ("schedule:", "push:", "pull_request:", "contents: write", "secrets.",
                          "git add", "git commit", "git push", "scripts/translate_script.py",
                          "SUMMARY.md", "actions/download-artifact", "actions/cache", "scripts/requirements.txt"):
            self.assertNotIn(forbidden, text)
        # The source reports and raw/state are never artifact paths.
        artifact = text.split("uses: actions/upload-artifact@v4", 1)[1]
        paths = re.search(r"(?ms)^\s+path: \|\n(.*?)(?=^\s+if-no-files-found:)", artifact).group(1)
        self.assertEqual([line.strip() for line in paths.splitlines() if line.strip()],
                         ["crawl_out/public-summary.json", "crawl_out/public-summary.md"])
        for command in re.findall(r"(?m)^\s*run:\s*(.*)$", text):
            self.assertNotIn("${{", command, "Unvalidated input interpolation in shell body")

    def test_summary_is_finite_status_projection_not_private_payload(self):
        source = envelope()
        source["sourceSnapshot"]["metadata"]["reason"] = "secret-marker"
        result = project(source, one_shot=True)
        exported = json.dumps(result) + markdown(result)
        for marker in ("private-marker", "signed-url-marker", "raw-marker", "private-path-marker", "secret-marker"):
            self.assertNotIn(marker, exported)
        self.assertEqual(result["reason"], "OTHER_TYPED_REASON")
        self.assertEqual(result["comparison_history"], "NO_PREVIOUS_RUN_HISTORY")
        self.assertEqual(result["ci_persistent_incremental"], "NOT_IMPLEMENTED")
        self.assertEqual(result["raw_acquisition_status"], "NOT_ACQUIRED")
        self.assertEqual(result["semantic_review"], "NOT_PERFORMED_BY_SCAN")
        self.assertEqual(result["pc_webgl_equivalence"], "NOT_PROVEN_BY_METADATA")

    def test_summary_plan_success_history_are_separate(self):
        first = project(envelope(), one_shot=True)
        self.assertEqual(first["historical_baseline"], "UNKNOWN_BASELINE_ONLY")
        self.assertEqual(first["plan_candidates"], 0)
        self.assertEqual(first["coverage_counts"]["successful_assets"], 0)
        current = envelope()
        current["lane_diff"]["historical_baseline"] = "KNOWN_SAVED_SNAPSHOT"
        current["plan"] = [{"vendor_token": "private-marker"}]
        self.assertEqual(project(current)["comparison_history"], "RETAINED_LOCAL_HISTORY")
        self.assertEqual(project(current)["plan_candidates"], 1)
        self.assertEqual(project(current, one_shot=True)["comparison_history"], "NO_PREVIOUS_RUN_HISTORY")

    def test_summary_missing_malformed_and_unknown_reports(self):
        process, report = self.run_summary()
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertEqual(report["reason"], "SNAPSHOT_SCAN_REPORT_REQUIRED")
        process, report = self.run_summary({"private": "private-marker"})
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertEqual(report["reason"], "SNAPSHOT_SCAN_ENVELOPE_REQUIRED")
        unknown = envelope()
        unknown["sourceSnapshot"]["metadata"] = {"status": "UNKNOWN", "reason": "PC_SOURCE_LOCATOR_REQUIRED"}
        process, report = self.run_summary(unknown)
        self.assertEqual(process.returncode, 0, process.stderr)
        self.assertEqual(report["operation_status"], "OBSERVATION_UNAVAILABLE")
        self.assertEqual(report["reason"], "PC_SOURCE_LOCATOR_REQUIRED")

    def test_summary_non_numeric_fields_cannot_copy_private_strings(self):
        source = envelope()
        source["metrics"]["failed"] = "private-marker"
        source["sourceSnapshot"]["coverage"]["successful_assets"] = True
        source["sourceSnapshot"]["coverage"]["master"] = "private-marker"
        result = project(source)
        self.assertIsNone(result["metrics"]["failed"])
        self.assertIsNone(result["coverage_counts"]["successful_assets"])
        self.assertEqual(result["coverage"]["master"], "UNKNOWN")
        self.assertNotIn("private-marker", json.dumps(result))

    def test_actual_workflow_scanner_and_summary_saved_metadata_fake_transport(self):
        workflow = (ROOT / ".github/workflows/crawl_only.yml").read_text(encoding="utf-8")
        command = scalar_command(workflow, "python scripts/crawl_only.py")
        # Isolated safe checkout, not an older PYTHONPATH or editable install.
        checkout = self.area / "checkout"
        checkout.mkdir()
        for folder in ("scripts", "tools", "contracts"):
            shutil.copytree(ROOT / folder, checkout / folder,
                            ignore=shutil.ignore_patterns("__pycache__", "*.pyc", "bin", "obj"))
        inventory = checkout / "saved-inventory.json"
        inventory.write_text(json.dumps({"d": [{"n": "notinit/novel_script/sts_10011201.dmm", "h": "fixture-v1", "s": 100}]}), encoding="utf-8")
        harness = checkout / "fixture_entry.py"
        harness.write_text(
            "import runpy, sys, types\n"
            "class NoNetworkClient:\n"
            " def __init__(self, *a, **k): pass\n"
            " def __enter__(self): return self\n"
            " def __exit__(self, *a): return False\n"
            " def stream(self, *a, **k): raise AssertionError('NETWORK_NOT_ALLOWED')\n"
            " def send(self, *a, **k): raise AssertionError('NETWORK_NOT_ALLOWED')\n"
            "httpx = types.ModuleType('httpx')\n"
            "httpx.Client = NoNetworkClient\n"
            "httpx.Timeout = lambda *a, **k: None\n"
            "httpx.HTTPError = type('HTTPError', (Exception,), {})\n"
            "sys.modules['httpx'] = httpx\n"
            "sys.argv = sys.argv[1:]\n"
            "runpy.run_path(sys.argv[0], run_name='__main__')\n", encoding="utf-8")
        env = {key: value for key, value in os.environ.items()
               if key not in ("PYTHONPATH", "PYTHONHOME", "NG03_PRIVATE_CRYPTO_HELPER", "NG03_PRIVATE_CRYPTO_HELPER_SHA256")}
        env.update(CRAWL_SOURCE_IDS="", CRAWL_MAX_CANDIDATES="200")
        invoked = subprocess.run([sys.executable, "-I", "-S", "-X", "utf8", str(harness)] + command[1:] +
                                 ["--inventory", str(inventory)], cwd=checkout, env=env,
                                 capture_output=True, text=True, encoding="utf-8", timeout=60)
        self.assertEqual(invoked.returncode, 0, invoked.stdout + invoked.stderr)
        output = json.loads(invoked.stdout)
        self.assertEqual(output["operation_status"], "METADATA_ONLY_PLAN")
        scan = json.loads((checkout / "crawl_out/scan.json").read_text(encoding="utf-8"))
        self.assertEqual(scan["metrics"]["baseline_only"], 1)
        self.assertEqual(scan["metrics"]["source_payloads_acquired"], 0)
        self.assertTrue((checkout / "crawl_out/novel-intake.json").is_file())
        self.assertTrue((checkout / "crawl_out/actionable-intake.json").is_file())
        summary_command = scalar_command(workflow, "python tools/update/snapshot_summary.py")
        summarized = subprocess.run([sys.executable, "-I", "-S", "-X", "utf8"] + summary_command[1:],
                                    cwd=checkout, env=env, capture_output=True, text=True, timeout=30)
        self.assertEqual(summarized.returncode, 0, summarized.stdout + summarized.stderr)
        report = json.loads((checkout / "crawl_out/public-summary.json").read_text(encoding="utf-8"))
        self.assertEqual(report["operation_status"], "METADATA_ONLY_PLAN")
        self.assertEqual(report["comparison_history"], "NO_PREVIOUS_RUN_HISTORY")
        self.assertEqual(report["plan_candidates"], 0)
        self.assertEqual(report["coverage_counts"]["baseline_only_assets"], 1)
        self.assertNotIn("fixture-v1", json.dumps(report))
        self.assertNotIn("notinit/", json.dumps(report))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(WorkflowTests))
    report = {"status": "PASS" if result.wasSuccessful() else "FAIL", "tests_run": result.testsRun,
              "failures": len(result.failures), "errors": len(result.errors),
              "basis": "Actual local workflow commands and product summary/scanner; saved metadata + network-denying fake httpx",
              "actual_source_acquisition": "NOT_RUN", "github_actions_dispatch": "NOT_PERFORMED",
              "native": "NOT_RUN", "public_write": "NOT_PERFORMED"}
    if result.wasSuccessful():
        report["local_workflow_boundary"] = {
            "legacy_writer": "RETIRED_MANUAL_NOTICE_ONLY",
            "snapshot": "MANUAL_ONE_SHOT_METADATA_ONLY",
            "permissions": "contents:read", "automatic_triggers": 0,
            "secret_references": 0, "legacy_download_translate_merge_commit_push_jobs": 0,
            "artifact_paths": ["crawl_out/public-summary.json", "crawl_out/public-summary.md"],
            "ci_persistent_incremental": "NOT_IMPLEMENTED",
            "validation": "LOCAL_STATIC_PLUS_EXACT_PRODUCT_COMMANDS_WITH_FIXTURES",
            "github_execution": "NOT_PERFORMED",
        }
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
