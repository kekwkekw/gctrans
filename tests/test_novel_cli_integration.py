"""Offline main-owned Novel entrypoint and source-work completion regression."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from contract_validation import read_json
from update.source_work import complete_novel_batch, paths, reconcile
from update.novel_intake import create_intake_references
from update.source_scan import SourceProfile, scan_inventory


class FixtureExtractor:
    version = "MAIN-NOVEL-INTEGRATION-TEST"

    def __call__(self, raw, asset):
        return {"textassets": [{"name": asset["source_id"], "path_id": 1,
                               "container": "fixture", "bytes": raw}], "selected": 0}


class NovelCompletionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.state_root = self.root / "state"
        self.profile = SourceProfile("WebGL", "R18", "Novel", "vendor-d-n-h-s-v1")
        self.snapshot, self.catalog, self.queue = self.scan()

    def tearDown(self):
        self.temporary.cleanup()

    def scan(self, token="fixture-1"):
        inventory = {"d": [{"n": f"notinit/novel_script/sts_{identity}.dmm",
                             "h": token, "s": 100}
                            for identity in ("99999991", "99999992")]}
        result = scan_inventory(
            self.profile, inventory, state_root=self.state_root,
            raw_root=self.root / "raw", fetch_asset=lambda item: item["vendor_token"].encode(),
            extractor=FixtureExtractor())
        snapshot = result["sourceSnapshot"]
        catalog = create_intake_references(snapshot, self.root / "catalog.json")
        return snapshot, catalog, reconcile(snapshot, catalog, self.state_root)

    def reviewed(self, source_id="99999991"):
        item = next(item for item in self.snapshot["novel"] if item["source_id"] == source_id)
        return {"source_id": item["source_id"], "asset_key": item["asset_key"],
                "textasset_sha256": item["selected_textasset"]["sha256"], "extractor": item["extractor"]}

    def state(self):
        return paths(self.state_root, self.snapshot["profile"])[0]

    def complete(self, *, review_id="CPU-REVIEW-FIXTURE", payload="a" * 64):
        return complete_novel_batch(self.state_root, self.snapshot, [self.reviewed()],
                                    review_id=review_id, payloads={"novels/99999991.json": payload})

    def test_scan_catalog_exposes_main_owned_workflow(self):
        self.assertEqual(self.catalog["translation_entrypoint"], "tools/update/cli.py novel")
        reference = self.catalog["references"][0]
        self.assertEqual(reference["status"], "READY_FOR_NOVEL_PREPARE")
        self.assertIn("translation/HOLD_POLICY.md", reference["required_inputs"])
        self.assertFalse(any("WB" in value for value in reference["required_inputs"]))
        self.assertNotIn("compatibility_entrypoints", reference)

    def test_success_completes_selected_source_and_preserves_other_work(self):
        before = read_json(self.state())
        result = self.complete()
        after = read_json(self.state())
        self.assertEqual(result["status"], "COMPLETED")
        completed = result["work_ids"][0]
        self.assertEqual(after["work"][completed]["status"], "completed")
        self.assertEqual(after["work"][completed]["result"]["payloads"], {"novels/99999991.json": "a" * 64})
        for key in before["work"]:
            if key != completed:
                self.assertEqual(before["work"][key], after["work"][key])
        _, _, next_queue = self.scan()
        self.assertEqual([work["reference"]["source_id"] for work in next_queue["actionable"]], ["99999992"])

    def test_retry_is_idempotent_and_later_review_can_record_retranslation(self):
        self.complete()
        before = self.state().read_bytes()
        self.assertEqual(self.complete()["status"], "ALREADY_COMPLETED")
        self.assertEqual(before, self.state().read_bytes())
        result = self.complete(review_id="CPU-RETRANSLATION-FIXTURE", payload="b" * 64)
        self.assertEqual(result["status"], "COMPLETED")
        current = read_json(self.state())["work"][result["work_ids"][0]]["result"]
        self.assertEqual(current["review_id"], "CPU-RETRANSLATION-FIXTURE")
        self.assertEqual(current["payloads"], {"novels/99999991.json": "b" * 64})

    def test_stale_source_cannot_complete_or_partially_write_work(self):
        sources = [self.reviewed(), self.reviewed("99999992")]
        sources[1]["textasset_sha256"] = "f" * 64
        before = self.state().read_bytes()
        with self.assertRaisesRegex(ValueError, "INTAKE_NOVEL_SOURCE_CHANGED"):
            complete_novel_batch(self.state_root, self.snapshot, sources, review_id="CPU-FIXTURE",
                                 payloads={"novels/99999991.json": "a" * 64,
                                           "novels/99999992.json": "b" * 64})
        self.assertEqual(before, self.state().read_bytes())

    def test_concurrent_scan_blocks_completion_of_old_reference(self):
        self.scan(token="fixture-new-source")
        before = self.state().read_bytes()
        with self.assertRaisesRegex(ValueError, "INTAKE_WORK_NOT_ACTIONABLE"):
            self.complete()
        self.assertEqual(before, self.state().read_bytes())

    def test_payload_scope_and_unknown_observation_are_errors(self):
        before = self.state().read_bytes()
        with self.assertRaisesRegex(ValueError, "INTAKE_NOVEL_PAYLOAD_SCOPE"):
            complete_novel_batch(self.state_root, self.snapshot, [self.reviewed()], review_id="CPU-FIXTURE",
                                 payloads={"novels/99999992.json": "a" * 64})
        snapshot = copy.deepcopy(self.snapshot)
        snapshot["metadata"]["status"] = "UNKNOWN"
        with self.assertRaisesRegex(ValueError, "INTAKE_CURRENT_OBSERVATION_REQUIRED"):
            complete_novel_batch(self.state_root, snapshot, [self.reviewed()], review_id="CPU-FIXTURE",
                                 payloads={"novels/99999991.json": "a" * 64})
        self.assertEqual(before, self.state().read_bytes())


class NovelCliTests(unittest.TestCase):
    def command(self, *arguments):
        return subprocess.run([sys.executable, "-I", "-S", str(ROOT / "tools/update/cli.py"),
                               *arguments], cwd=ROOT, capture_output=True, text=True)

    def test_current_help_keeps_existing_operations_and_adds_novel(self):
        result = self.command("--help")
        self.assertEqual(result.returncode, 0, result.stderr)
        for command in ("novel", "scan", "plan-ui-edit", "plan-master-key", "apply-review"):
            self.assertIn(command, result.stdout)

    def test_novel_help_dispatches_without_private_or_network_dependencies(self):
        for arguments in (("novel", "--help"), ("novel", "--", "--help")):
            result = self.command(*arguments)
            self.assertEqual(result.returncode, 0, result.stderr)
            for command in ("prepare", "review", "check", "apply"):
                self.assertIn(command, result.stdout)

    def test_retired_commands_are_rejected_by_cli_contract(self):
        for command in ("bind-intake-wb", "intake-failed", "resume-intake-completion"):
            with self.subTest(command=command):
                result = self.command(command)
                self.assertEqual(result.returncode, 2)
                self.assertIn("invalid choice", result.stderr)


if __name__ == "__main__":
    unittest.main()
