"""C01-C08 through production scanner/consumers, including real OS subprocesses.

Synthetic raw is CPU evidence only. D01 real private raw has a separate runner.
"""
import copy
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from tools.update import source_scan as source
from tools.update.novel_bridge import create_intake_references, consume_intake_reference, current_source_references

OBSERVATIONS = {}


def inventory(*values):
    return {"d": [{"n": f"notinit/novel_script/sts_{identity}.dmm", "h": token, "s": 100}
                  for identity, token in values]}


class FixtureExtractor:
    version = "NG04-CPU-FIXTURE-v1"
    def __call__(self, raw, asset):
        return {"textassets": [{"name": "script", "path_id": 1, "container": "CPU-fixture", "bytes": b"script:" + raw}], "selected": 0}


class Ng04SourceTests(unittest.TestCase):
    def setUp(self):
        scratch = ROOT / "tests" / "scratch-source-regression"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=scratch)
        self.root = Path(self.temp.name)
        self.profile = source.SourceProfile("WebGL", "R18", "Novel", "vendor-d-n-h-s-v1")
        self.fetched = []

    def tearDown(self):
        self.temp.cleanup()

    def fetch(self, asset):
        self.fetched.append(asset["asset_key"])
        return ("bundle:" + asset["vendor_token"]).encode()

    def scan(self, metadata, **options):
        defaults = dict(state_root=self.root / "state", raw_root=self.root / "raw", fetch_asset=self.fetch,
                        extractor=FixtureExtractor())
        defaults.update(options)
        return source.scan_inventory(self.profile, metadata, **defaults)

    def duplicate(self):
        meta = inventory(("10011201", "a"), ("10011202", "b"))
        meta["d"].append({"n": "notinit/other/sts_10011201.dmm", "h": "c", "s": 100})
        return meta

    def test_C01_materialized_to_duplicate_all_variants_withheld(self):
        first = self.scan(inventory(("10011201", "a"), ("10011202", "b")))
        previous_ref = first["sourceSnapshot"]["novel"][0]["raw"]
        result = self.scan(self.duplicate())
        rows = [r for r in result["sourceSnapshot"]["novel"] if r["source_id"] == "10011201"]
        self.assertTrue(all(r["status"] == "COLLISION" and r["selected_textasset"] is None and not r["current_eligible"] for r in rows))
        self.assertTrue((self.root / "raw" / previous_ref["path"]).exists())
        self.assertEqual(next(r for r in result["sourceSnapshot"]["novel"] if r["source_id"] == "10011202")["status"], "MATERIALIZED")
        intake = create_intake_references(result["sourceSnapshot"], self.root / "intake.json")
        self.assertEqual([r["source_id"] for r in intake["references"]], ["10011202"])
        # Consumer independently rejects a legacy/forged MATERIALIZED flag.
        stale = copy.deepcopy(result["sourceSnapshot"])
        for row in stale["novel"]:
            row["status"] = "MATERIALIZED"
            row["source_collision"] = False
            row["selected_textasset"] = row["textassets"][0]
        self.assertEqual([r["source_id"] for r in create_intake_references(stale, self.root / "forged.json")["references"]], ["10011202"])
        stale["qualified_sources"] = {"opaque-old": {"size": 1, "sha256": "a" * 64}}
        self.assertEqual(current_source_references(stale), {})
        OBSERVATIONS["C01"] = {"ambiguous_intake": 0, "variants": len(rows), "unrelated": "MATERIALIZED", "raw_preserved": True}

    def test_C01_typed_qualified_refs_preserve_unrelated_canonical_wb_pin(self):
        self.scan(inventory(("10011201", "a"), ("10011202", "b")))
        snapshot = self.scan(self.duplicate())["sourceSnapshot"]
        pin = {"size": 10, "sha256": "a" * 64}
        good_asset = "notinit/novel_script/sts_10011202.dmm"
        bad_asset = "notinit/novel_script/sts_10011201.dmm"
        snapshot["qualified_sources"] = {
            "unrelated": {**pin, "source_id": "10011202", "asset_key": good_asset},
            "duplicate": {**pin, "source_id": "10011201", "asset_key": bad_asset},
            "legacy-opaque": pin,
            "bad-size": {**pin, "size": True, "source_id": "10011202", "asset_key": good_asset},
            "bad-sha": {**pin, "sha256": "bad", "source_id": "10011202", "asset_key": good_asset},
            "non-object": "invalid",
            "bad-owner": {**pin, "source_id": [], "asset_key": good_asset},
            "null-owner": {**pin, "source_id": None, "asset_key": None},
            "extra-pin-field": {**pin, "extra": "existing exact WB comparison must reject this", "source_id": "10011202", "asset_key": good_asset},
        }
        eligible = current_source_references(snapshot)
        self.assertEqual(eligible["unrelated"], pin)
        self.assertEqual(eligible["extra-pin-field"]["extra"], "existing exact WB comparison must reject this")
        self.assertEqual(set(eligible), {"unrelated", "extra-pin-field"})
        self.assertNotIn("source_id", eligible["unrelated"])
        self.assertNotIn("asset_key", eligible["unrelated"])
        OBSERVATIONS.setdefault("C01", {}).update(typed_unrelated_pin_canonical=True, duplicate_pin_held=True,
                                                  legacy_opaque_conservative_hold=True, malformed_pin_types_rejected=True)

    def test_C02_collision_to_unique_matching_token_extractor_reuses_raw(self):
        self.scan(inventory(("10011201", "a"), ("10011202", "b")))
        self.scan(self.duplicate())
        count = len(self.fetched)
        result = self.scan(inventory(("10011201", "a"), ("10011202", "b")))
        self.assertEqual(len(self.fetched), count)
        self.assertEqual(result["plan"][0]["reason"], "source_collision_resolved")
        self.assertEqual(len(create_intake_references(result["sourceSnapshot"], self.root / "intake.json", raw_root=self.root / "raw")["references"]), 2)
        OBSERVATIONS["C02"] = {"network_payloads": result["metrics"]["payload_network"], "metrics": result["metrics"], "unique_recovery": True}

    def test_C03_textasset_duplicates_never_select_first_or_last(self):
        class Ambiguous(FixtureExtractor):
            def __call__(self, raw, asset):
                return {"textassets": [{"name": "same", "path_id": n, "container": "fixture", "bytes": raw + bytes([n])} for n in (1, 2)], "selected": 0}
        result = self.scan(inventory(("10011201", "a")), extractor=Ambiguous())
        row = result["sourceSnapshot"]["novel"][0]
        self.assertEqual(row["status"], "COLLISION")
        self.assertEqual(len(row["textassets"]), 2)
        self.assertIsNone(row["selected_textasset"])
        self.assertEqual(create_intake_references(result["sourceSnapshot"], self.root / "intake.json")["references"], [])
        for ref in row["textassets"]:
            source.verified_blob(self.root / "raw", ref)
        OBSERVATIONS["C03"] = {"textassets_preserved": 2, "selected": None}

    def test_C04_corrupt_blob_ref_revoked_quarantined_atomic_repair(self):
        meta = inventory(("10011201", "a"), ("10011202", "b"))
        result = self.scan(meta)
        ref = result["sourceSnapshot"]["novel"][0]["raw"]
        target = self.root / "raw" / ref["path"]
        original = target.read_bytes()
        linked = self.root / "old-hardlink.bytes"
        os.link(target, linked)
        target.write_bytes(b"X" * len(original))
        count = len(self.fetched)
        result = self.scan(meta, integrity_scan=True)
        self.assertEqual(len(self.fetched), count + 1)
        self.assertEqual(target.read_bytes(), original)
        self.assertEqual(linked.read_bytes(), b"X" * len(original))
        self.assertEqual(result["metrics"]["quarantined_blobs"], 1)
        self.assertEqual(result["metrics"]["failed"], 0)
        self.assertEqual(len(result["state"]["assets"][meta["d"][0]["n"]]["revoked_raw_refs"]), 1)
        warm = self.scan(meta)
        self.assertEqual(warm["plan"], [])
        self.assertEqual(warm["metrics"]["raw_cache_reads"], 0)
        OBSERVATIONS["C04"] = {"valid_refetches": 1, "quarantined": 1, "old_hardlink_preserved": True, "repeated_failure": False}

    def test_C05_actual_subprocess_live_and_terminated_writer(self):
        lock = self.root / "state" / (self.profile.key + ".writer.lock")
        lock.parent.mkdir(parents=True)
        lock.write_bytes(b"")
        self.scan(inventory(("10011201", "a")))
        code = "from pathlib import Path; import sys; from tools.update.process_lock import ProcessFileLock; lock=ProcessFileLock(Path(sys.argv[1])); lock.__enter__(); print('LOCKED',flush=True); sys.stdin.read()"
        child = subprocess.Popen([sys.executable, "-B", "-X", "utf8", "-c", code, str(lock)], cwd=ROOT,
                                 stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            self.assertEqual(child.stdout.readline().strip(), "LOCKED")
            with self.assertRaisesRegex(ValueError, "SOURCE_STATE_WRITER_BUSY"):
                self.scan(inventory(("10011201", "a")))
            self.assertIsNone(child.poll())
            child.terminate()  # This test's own child only; simulate writer termination.
            child.wait(timeout=15)
            recovered = self.scan(inventory(("10011201", "a")))
            self.assertEqual(recovered["metrics"]["reused"], 1)
        finally:
            if child.poll() is None:
                child.terminate()
                child.wait(timeout=15)
            for stream in (child.stdin, child.stdout, child.stderr):
                stream.close()
        OBSERVATIONS["C05"] = {"platform": sys.platform, "os_lock": "msvcrt" if os.name == "nt" else "fcntl", "live_writer_protected": True, "terminated_writer_recovered": True, "empty_leftover_recovered": True}

    def test_C05_metadata_write_failure_releases_acquired_os_lock(self):
        from tools.update.process_lock import ProcessFileLock
        path = self.root / "writer.lock"
        with patch("tools.update.process_lock.json.dumps", side_effect=OSError("CPU injected owner metadata failure")):
            with self.assertRaises(OSError):
                with ProcessFileLock(path):
                    self.fail("owner metadata failure must propagate")
        with ProcessFileLock(path):
            self.assertTrue(path.exists())
        OBSERVATIONS.setdefault("C05", {})["owner_metadata_failure_releases_lock"] = True

    def test_C06_metadata_only_warm_actual_raw_reads_hash_zero(self):
        meta = inventory(("10011201", "a"), ("10011202", "b"), ("10011203", "c"))
        self.scan(meta)
        with patch.object(source, "_cache_read", wraps=source._cache_read) as reads, patch.object(source, "_count_hash", wraps=source._count_hash) as hashes:
            result = self.scan(meta)
            self.assertEqual(reads.call_count, 0)
            self.assertEqual(hashes.call_count, 0)
        self.assertEqual(result["metrics"]["raw_cache_reads"], 0)
        self.assertEqual(result["metrics"]["raw_hash_bytes"], 0)
        self.assertEqual(result["metrics"]["metadata_only_reuse"], 3)
        self.assertTrue(all(r["cache_validation"] == "METADATA_ONLY_OWNED_PROVENANCE" for r in result["sourceSnapshot"]["novel"]))
        OBSERVATIONS["C06"] = {"actual_read_calls": 0, "actual_hash_calls": 0, "assets": 3, "metrics": result["metrics"]}

    def test_C07_consumption_and_reextraction_reject_then_repair_corrupt_raw(self):
        meta = inventory(("10011201", "a"))
        first = self.scan(meta)
        intake = create_intake_references(first["sourceSnapshot"], self.root / "intake.json")["references"][0]
        script = self.root / "raw" / intake["textasset"]["path"]
        original = script.read_bytes()
        script.write_bytes(b"X" * len(original))
        warm = self.scan(meta)
        self.assertEqual(warm["metrics"]["raw_cache_reads"], 0)
        with self.assertRaisesRegex(ValueError, "SOURCE_RAW_IDENTITY"):
            consume_intake_reference(warm["sourceSnapshot"], intake, self.root / "raw")
        with self.assertRaisesRegex(ValueError, "SOURCE_RAW_IDENTITY"):
            create_intake_references(warm["sourceSnapshot"], self.root / "checked-intake.json", raw_root=self.root / "raw")
        changed = FixtureExtractor()
        changed.version = "NG04-CPU-FIXTURE-v2"
        repaired = self.scan(meta, extractor=changed)
        self.assertEqual(repaired["metrics"]["payload_network"], 0)
        self.assertEqual(script.read_bytes(), original)
        self.assertEqual(repaired["metrics"]["quarantined_blobs"], 1)
        new_intake = create_intake_references(repaired["sourceSnapshot"], self.root / "new-intake.json")["references"][0]
        self.assertEqual(consume_intake_reference(repaired["sourceSnapshot"], new_intake, self.root / "raw"), original)
        # Damaged bundle discovered during reextract is revoked and refetched once.
        bundle = self.root / "raw" / new_intake["bundle"]["path"]
        bundle.write_bytes(b"X" * new_intake["bundle"]["size"])
        changed.version = "NG04-CPU-FIXTURE-v3"
        fetched = len(self.fetched)
        recovered = self.scan(meta, extractor=changed)
        self.assertEqual(len(self.fetched), fetched + 1)
        self.assertEqual(recovered["metrics"]["failed"], 0)
        OBSERVATIONS["C07"] = {"corrupt_consumption_rejected": True, "script_reextract_from_verified_bundle": True, "bundle_ref_revoked_refetched": True}

    def test_C08_partial_failures_retry_only_failed_same_metadata(self):
        meta = inventory(("10011201", "a"), ("10011202", "b"))
        def fetch(item):
            if item["source_id"] == "10011202":
                raise OSError("fixture failure")
            return self.fetch(item)
        first = self.scan(meta, fetch_asset=fetch)
        self.assertEqual(first["metrics"]["failed"], 1)
        second = self.scan(meta, complete=False, existing_ids={"10011201", "10011202"})
        self.assertEqual([p["source_id"] for p in second["plan"]], ["10011202"])
        self.assertEqual(second["deletion_candidates"], [])
        self.assertEqual(second["metrics"]["baseline_only"], 0)
        self.assertEqual(second["metrics"]["materialized"], 1)
        OBSERVATIONS.setdefault("C08", {}).update(failed_only_retry=True, retry_source_ids=["10011202"], successful_unrelated_preserved=True)

    def test_A04_source_three_grammars_single_records_field_scoped_unknown(self):
        selectors = {"Scalar": ["name"], "Array": ["ml_name[]"], "Delimited": ["ml_message|"], "Missing": ["name"], "mCurrencies": ["ml_name[]"]}
        records = {b"scalar": {"id": 1, "name": "JP", "role": 3},
                   b"array": [{"id": 2, "ml_name": ["JP2", "second-language", "tail"]}],
                   b"pipe": [{"id": 3, "ml_message": "JP3|other|tail"}, {"id": 4, "ml_message": 9}],
                   b"currency": [{"item_type": 1, "ml_name": ["JP4", "tail"]}]}
        assets = {"textassets": [{"name": name, "bytes": raw} for name, raw in (("Scalar", b"scalar"), ("Array", b"array"), ("Delimited", b"pipe"), ("mCurrencies", b"currency"))]}
        with patch.object(source.UnityTextExtractor, "__init__", lambda obj: setattr(obj, "version", "fixture")):
            extractor = source.MasterTableExtractor(records.__getitem__, "a" * 64, tables=selectors)
        with patch.object(source.UnityTextExtractor, "__call__", return_value=assets):
            result = extractor(b"raw", {"asset_key": "master.dmm"})
        rows = {r["translation_target"]["table"]: r for r in result["master"]}
        self.assertEqual(rows["Scalar"]["jp"], "JP")
        self.assertEqual(rows["Array"]["structure"]["physical_field"], "ml_name[0]")
        self.assertEqual(rows["Array"]["structure"]["language_tail"], ["second-language", "tail"])
        self.assertEqual(rows["Delimited"]["jp"], "JP3")
        self.assertEqual(rows["Delimited"]["structure"]["suffix"], "|other|tail")
        coverage = {r["table"]: r for r in result["master_field_coverage"]}
        self.assertEqual(coverage["Delimited"]["unsupported_type"], 1)
        self.assertEqual(coverage["Missing"]["status"], "TABLE_TEXTASSET_MISSING")
        self.assertEqual(rows["mCurrencies"]["locator"]["record"], "item_type:1")
        self.assertEqual(len(result["decoded_bodies"]), 4)
        OBSERVATIONS["A04_source"] = {"scalar": True, "array_first_language_tail_preserved": True, "delimited_first_segment_suffix_preserved": True, "unavailable_field_isolated": True}

    def test_C08_http_typed_failure_bounded_no_sensitive_details(self):
        import httpx
        from tools.update.source_http import fetch_bytes
        calls = []
        def handler(request):
            calls.append(1)
            raise httpx.ReadTimeout("fixture signed URL or credential text must not escape")
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            with self.assertRaisesRegex(ValueError, "^SOURCE_HTTP_RETRIES_EXHAUSTED_TIMEOUT$"):
                fetch_bytes(client, "https://cdn-r18.gc.dmmgames.com/files/manifest/webgl/r18/master.json", sleeper=lambda _: None)
        self.assertEqual(len(calls), 3)
        OBSERVATIONS.setdefault("C08", {})["http_timeout_attempts"] = 3


if __name__ == "__main__":
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Ng04SourceTests))
    log = stream.getvalue()
    print(log)
    report = {"schema": "gc-ng04-source-cpu-evidence-v1", "status": "PASS" if result.wasSuccessful() else "FAIL",
              "tests_run": result.testsRun, "platform": sys.platform, "scope": "actual product with synthetic private CPU fixtures; no real-source coverage",
              "cases": OBSERVATIONS, "native": "NOT_RUN", "game": "NOT_RUN"}
    source.atomic_json(ROOT / "audit/NG04_SOURCE_CPU_EVIDENCE.json", report)
    (ROOT / "audit/NG04_SOURCE_TESTS.log").write_text(log, encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
