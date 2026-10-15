"""Actual product planner/cache/HTTP tests. Synthetic fixtures are not source coverage."""
import json
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

from tools.update.source_scan import (SourceProfile, scan_inventory, digest, verified_blob, parse_source_ids,
    MasterTableExtractor, UnityTextExtractor, MASTER_ML_WORD_MAPPING_CONTRACT)
from tools.update.source_http import fetch_bytes
from tools.update.novel_bridge import create_intake_references, current_source_references


def inventory(*values):
    return {"d": [{"n": f"notinit/novel_script/sts_{identity}.dmm", "h": token, "s": 100}
                  for identity, token in values]}


class FixtureExtractor:
    version = "CPU-FIXTURE-EXTRACTOR-v1"

    def __call__(self, raw, asset):
        return {"textassets": [{"name": "script", "path_id": 1, "container": "fixture-container", "bytes": raw}], "selected": 0}


class SourceTests(unittest.TestCase):
    def setUp(self):
        scratch = Path(__file__).resolve().parent / "scratch-source"
        scratch.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=scratch)
        self.root = Path(self.temporary.name)
        self.profile = SourceProfile("WebGL", "R18", "Novel", "vendor-d-n-h-s-v1")
        self.fetched = []

    def tearDown(self):
        self.temporary.cleanup()

    def fetch(self, asset):
        self.fetched.append(asset["source_id"])
        return ("message,JP," + asset["vendor_token"] + "\r\nunknown,kept<br>\n").encode()

    def test_master_known_typed_field_mapping_keeps_physical_locator(self):
        body = {"textassets": [{"name": "mUnitWords", "bytes": b"private-source"}], "selected": 0}
        with patch.object(UnityTextExtractor, "__init__", lambda obj: setattr(obj, "version", "CPU-EXTRACTOR")):
            adapter = MasterTableExtractor(lambda raw: [{"id": 1, "ml_word": ["JP", ""], "role": 3}], "a" * 64)
        with patch.object(UnityTextExtractor, "__call__", return_value=body):
            row = adapter(b"private-raw", {"asset_key": "master.dmm"})["master"][0]
        self.assertEqual(row["locator"]["field"], "ml_word")
        self.assertEqual(row["translation_target"], {"table": "mUnitWords", "field": "ml_word[]"})
        self.assertEqual(row["structure"]["physical_field"], "ml_word[0]")
        self.assertEqual(row["structure"]["mapping_contract"], MASTER_ML_WORD_MAPPING_CONTRACT)
        with patch.object(UnityTextExtractor, "__init__", lambda obj: setattr(obj, "version", "CPU-EXTRACTOR")):
            invalid = MasterTableExtractor(lambda raw: [{"id": 1, "ml_word": "JP"}], "a" * 64)
        with patch.object(UnityTextExtractor, "__call__", return_value=body):
            with self.assertRaisesRegex(ValueError, "MASTER_JP_FIELD_SCHEMA"):
                invalid(b"private-raw", {"asset_key": "master.dmm"})

    def scan(self, meta, **kwargs):
        defaults = {"state_root": self.root / "state", "raw_root": self.root / "raw",
                    "fetch_asset": self.fetch, "extractor": FixtureExtractor()}
        defaults.update(kwargs)
        return scan_inventory(self.profile, meta, **defaults)

    def test_S01_existing_ID_changed_token_is_fetched(self):
        first = self.scan(inventory(("10011201", "aa")), existing_ids={"10011201"})
        self.assertEqual(first["metrics"]["baseline_only"], 1)
        second = self.scan(inventory(("10011201", "bb")), existing_ids={"10011201"})
        self.assertEqual(second["plan"][0]["reason"], "source_token_changed")
        self.assertEqual(self.fetched, ["10011201"])

    def test_S02_verified_materialization_reuses_without_network(self):
        meta = inventory(("10011201", "aa"))
        self.scan(meta)
        result = self.scan(meta)
        self.assertEqual(result["metrics"]["payload_network"], 0)
        self.assertEqual(result["metrics"]["reused"], 1)
        self.assertEqual(self.fetched, ["10011201"])

    def test_S03_partial_missing_is_not_delete(self):
        self.scan(inventory(("10011201", "aa"), ("10011202", "bb")))
        result = self.scan(inventory(("10011201", "aa")), complete=False)
        self.assertEqual(result["deletion_candidates"], [])
        self.assertEqual(result["state"]["observation"]["status"], "PARTIAL")
        self.assertEqual(len(result["state"]["assets"]), 2)

    def test_S04_complete_missing_only_same_profile_delete_candidate(self):
        self.scan(inventory(("10011201", "aa"), ("10011202", "bb")))
        result = self.scan(inventory(("10011201", "aa")))
        self.assertEqual(len(result["deletion_candidates"]), 1)
        self.assertEqual(result["sourceSnapshot"]["coverage"]["automatic_translation_deletes"], 0)
        changed_contract = SourceProfile("WebGL", "R18", "Novel", "different-schema")
        other = scan_inventory(changed_contract, inventory(("10011201", "aa")), state_root=self.root / "state", raw_root=self.root / "raw")
        self.assertEqual(other["deletion_candidates"], [])

    def test_S05_failed_after_metadata_advance_retries_only_failed(self):
        meta = inventory(("10011201", "aa"), ("10011202", "bb"))
        def failing(asset):
            if asset["source_id"] == "10011202":
                raise OSError("sensitive signed URL must never appear")
            return self.fetch(asset)
        first = self.scan(meta, fetch_asset=failing)
        self.assertEqual(first["metrics"]["failed"], 1)
        self.assertNotIn("sensitive", json.dumps(first))
        second = self.scan(meta)
        self.assertEqual([p["source_id"] for p in second["plan"]], ["10011202"])
        self.assertEqual(self.fetched, ["10011201", "10011202"])

    def test_S06_PC_WebGL_same_ID_different_state(self):
        self.scan(inventory(("10011201", "aa")))
        pc = SourceProfile("PC", "R18", "Novel", "pc-cache-v1")
        result = scan_inventory(pc, None, state_root=self.root / "state", raw_root=self.root / "raw", observation_error="PC_SOURCE_LOCATOR_REQUIRED")
        self.assertNotEqual(self.profile.key, pc.key)
        self.assertEqual(result["sourceSnapshot"]["coverage"]["raw_materialized_assets"], 0)

    def test_S07_extractor_change_uses_verified_raw(self):
        meta = inventory(("10011201", "aa"))
        self.scan(meta)
        other = FixtureExtractor()
        other.version = "CPU-FIXTURE-EXTRACTOR-v2"
        result = self.scan(meta, extractor=other)
        self.assertEqual(result["metrics"]["payload_network"], 0)
        self.assertEqual(result["metrics"]["reextracted"], 1)
        self.assertEqual(result["plan"][0]["reason"], "extractor_changed")

    def test_S08_duplicate_ID_only_collision_is_held(self):
        meta = inventory(("10011201", "aa"), ("10011202", "bb"))
        meta["d"].append({"n": "notinit/other/sts_10011201.dmm", "h": "cc", "s": 50})
        result = self.scan(meta)
        self.assertEqual(result["metrics"]["collision"], 2)
        self.assertEqual(result["metrics"]["materialized"], 1)
        collisions = [r for r in result["sourceSnapshot"]["novel"] if r["source_id"] == "10011201"]
        self.assertTrue(all(r["raw"] and r["selected_textasset"] is None for r in collisions))

    def test_S08_all_TextAsset_bytes_preserved_no_first_match(self):
        class Multiple(FixtureExtractor):
            def __call__(self, raw, asset):
                values = [{"name": name, "path_id": i, "container": "fixture", "bytes": raw + bytes([i])} for i, name in enumerate(("a", "b"), 1)]
                return {"textassets": values, "selected": None}
        result = self.scan(inventory(("10011201", "aa")), extractor=Multiple())
        row = result["sourceSnapshot"]["novel"][0]
        self.assertEqual(row["status"], "COLLISION")
        self.assertEqual(len(row["textassets"]), 2)
        self.assertIsNone(row["selected_textasset"])
        for ref in row["textassets"]:
            self.assertEqual(digest(verified_blob(self.root / "raw", ref)), ref["sha256"])

    def test_S09_bridge_raw_only_is_not_qualified_view(self):
        result = self.scan(inventory(("10011201", "aa")))
        wrapper = create_intake_references(result["sourceSnapshot"], self.root / "intake.json")
        row = wrapper["references"][0]
        self.assertIsNone(row["historical_raw_index"])
        self.assertIsNone(row["source_qualified_locator"])
        self.assertEqual(current_source_references(result["sourceSnapshot"]), {})
        raw = verified_blob(self.root / "raw", row["textasset"])
        self.assertIn(b"unknown,kept<br>\n", raw)
        self.assertIn(b"\r\n", raw)

    def test_S10_PC_missing_Master_UI_are_PARTIAL(self):
        for lane in ("Master", "UI"):
            pc = SourceProfile("PC", "R18", lane, "pc-explicit-local-cache-v1")
            result = scan_inventory(pc, None, state_root=self.root / "state", raw_root=self.root / "raw", observation_error="PC_SOURCE_LOCATOR_REQUIRED")
            self.assertEqual(result["state"]["observation"]["status"], "UNKNOWN")
            self.assertEqual(result["state"]["observation"]["reason"], "PC_SOURCE_LOCATOR_REQUIRED")

    def test_S11_bootstrap_baseline_then_failed_forced_not_downgraded(self):
        meta = inventory(("10011201", "aa"), ("10011202", "bb"))
        first = self.scan(meta, existing_ids={"10011201", "10011202"})
        self.assertEqual(first["metrics"]["payload_network"], 0)
        self.assertEqual(first["metrics"]["baseline_only"], 2)
        def fail(asset): raise OSError("failure")
        self.scan(meta, existing_ids={"10011201", "10011202"}, forced_ids=["10011201"], fetch_asset=fail)
        result = self.scan(meta, existing_ids={"10011201", "10011202"})
        self.assertEqual([p["source_id"] for p in result["plan"]], ["10011201"])
        self.assertEqual(result["metrics"]["baseline_only"], 1)

    def test_cancel_saved_pending_and_owned_temporary_cleanup(self):
        meta = inventory(("10011201", "aa"))
        first = self.scan(meta, cancelled=lambda: True)
        self.assertEqual(first["metrics"]["failed"], 1)
        self.assertEqual(list(self.root.rglob(".ng03-owned-*")), [])
        self.assertEqual(self.scan(meta)["metrics"]["materialized"], 1)

    def test_untrusted_cache_hash_not_mtime_or_size(self):
        meta = inventory(("10011201", "aa"))
        result = self.scan(meta)
        ref = result["sourceSnapshot"]["novel"][0]["raw"]
        target = self.root / "raw" / ref["path"]
        original = target.read_bytes()
        target.write_bytes(b"x" * len(original))
        result = self.scan(meta, integrity_scan=True)
        self.assertEqual(result["plan"][0]["reason"], "cache_invalid")
        self.assertEqual(result["metrics"]["failed"], 0)
        self.assertEqual(result["metrics"]["quarantined_blobs"], 1)
        self.assertEqual(target.read_bytes(), original)
        self.assertEqual(verified_blob(self.root / "raw", result["sourceSnapshot"]["novel"][0]["raw"]), original)

    def test_forced_ids_limits_and_no_secret_exception_state(self):
        self.assertEqual(parse_source_ids("10011201\n10011202,10011201"), ["10011201", "10011202"])
        with self.assertRaisesRegex(ValueError, "SOURCE_CANDIDATE_LIMIT"):
            self.scan(inventory(("10011201", "aa"), ("10011202", "bb")), max_candidates=1)
        self.assertEqual(self.fetched, [])

    def test_metadata_failure_cannot_create_deletion(self):
        self.scan(inventory(("10011201", "aa")))
        result = self.scan(None, observation_error="SOURCE_METADATA_OBSERVATION_FAILED")
        self.assertEqual(result["deletion_candidates"], [])
        self.assertEqual(len(result["state"]["assets"]), 1)

    def test_scope_schema_change_is_unknown_and_preserves_complete_baseline(self):
        self.scan(inventory(("10011201", "aa")))
        result = self.scan({"unexpected_inventory": []})
        self.assertEqual(result["state"]["observation"]["status"], "UNKNOWN")
        self.assertEqual(len(result["state"]["last_complete_inventory"]["asset_keys"]), 1)
        self.assertEqual(result["deletion_candidates"], [])
        self.assertEqual(len(self.scan(inventory(("10011201", "aa")))["plan"]), 0)


class HttpTests(unittest.TestCase):
    def test_bounded_retry_redirect_size_cancel_and_host(self):
        import httpx
        url = "https://cdn-r18.gc.dmmgames.com/files/manifest/webgl/r18/assetbundle.json"
        calls = []
        def handler(request):
            calls.append(str(request.url))
            return httpx.Response(429 if len(calls) < 3 else 200, content=b"ok")
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            self.assertEqual(fetch_bytes(client, url, sleeper=lambda seconds: None), b"ok")
        self.assertEqual(len(calls), 3)
        for response, code in ((httpx.Response(302, headers={"location": "https://other.invalid"}), "SOURCE_REDIRECT_REJECTED"),
                               (httpx.Response(200, content=b"oversize"), "SOURCE_HTTP_SIZE")):
            with httpx.Client(transport=httpx.MockTransport(lambda req: response)) as client:
                with self.assertRaisesRegex(ValueError, code): fetch_bytes(client, url, max_bytes=3)
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            with self.assertRaisesRegex(ValueError, "SOURCE_CANCELLED"): fetch_bytes(client, url, cancelled=lambda: True)
            with self.assertRaisesRegex(ValueError, "SOURCE_HOST_REJECTED"): fetch_bytes(client, "https://unknown.invalid/x")


if __name__ == "__main__":
    unittest.main()
