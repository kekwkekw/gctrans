"""Knowledge migration, exact identity and reviewer changeset regression tests."""
from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import translation_knowledge as knowledge


def record(identity, *, jp="アルテ", status="reviewing", scope="scene-only", sources=None):
    return {
        "id": identity, "kind": "term", "status": status, "scope": scope,
        "source_ids": ["10010101"] if sources is None else sources,
        "content_available": True,
        "value": {"source_text": jp, "translation": "아르테", "status": status},
        "origin": {"review_id": "fixture-review", "source": "fixture-script:1"},
    }


class MigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.seed = knowledge.consolidate_public(ROOT)
        cls.by_id = {item["id"]: item for item in cls.seed}

    def test_original_values_statuses_and_scopes_survive(self):
        total = 0
        directory = ROOT / "knowledge-layer/snapshots/r36/knowledge"
        for ledger, (kind, id_field) in knowledge.LEDGERS.items():
            with (directory / (ledger + ".jsonl")).open(encoding="utf-8") as stream:
                for line in stream:
                    if not line.strip():
                        continue
                    original = json.loads(line)
                    migrated = self.by_id[original[id_field]]
                    self.assertEqual(migrated["value"], original)
                    self.assertEqual(migrated["status"], original["status"])
                    self.assertEqual(migrated["scope"], original.get("scope"))
                    self.assertEqual(migrated["kind"], kind)
                    total += 1
        self.assertEqual(total, 5571)

    def test_missing_candidates_are_not_invented_or_promoted(self):
        bosch = self.by_id["entity:post20260930:bosch"]
        self.assertEqual(bosch["status"], "reviewing")
        self.assertEqual(bosch["value"]["name_status"], "approved")
        missing = [item for item in self.seed if not item["content_available"]]
        self.assertEqual(len(missing), 71)
        projection = knowledge.project(self.seed, speakers=["ボシュ"], source_ids=["20003602"])
        self.assertTrue(projection["missing_value_ids"])
        self.assertTrue(all(item["content_available"] for item in projection["records"]))
        self.assertNotIn("H_BODY", json.dumps(projection, ensure_ascii=False))

    def test_current_names_include_only_existing_approved_exact_sync(self):
        baseline = json.loads((ROOT / "names-layer/accepted/4e/names/zh_Hans.json").read_text())
        current = json.loads((ROOT / "translations/ko/names.json").read_text())
        k02 = json.loads((ROOT / "knowledge-layer/provenance/20261001_postmerge_knowledge/K02_NAMES_CANONICAL_SYNC_16.json").read_text())
        self.assertEqual(len(k02), 16)
        for jp, ko in baseline.items():
            self.assertEqual(current[jp], ko)
        for item in k02:
            self.assertIs(item["user_approved"], True)
            self.assertEqual(current[item["jp"]], item["ko"])
        self.assertIn("街の人Ａ", current)
        self.assertIn("街の人A", current)


class ProjectionAndChangesTests(unittest.TestCase):
    def test_projection_has_exact_jp_and_explicit_reference_scope(self):
        first = record("term:decomposed", jp="カ\u3099", sources=[])
        second = record("term:composed", jp="ガ", sources=[])
        unrelated = record("term:unrelated", jp="完全に無関係", sources=["99999999"])
        selected = knowledge.project([first, second, unrelated], speakers=["カ\u3099"])
        self.assertEqual([r["id"] for r in selected["records"]], [first["id"]])
        self.assertEqual(selected["records"][0], first)
        self.assertEqual(selected["effective_authority"][first["id"]], "reference")

    def test_scope_and_status_do_not_become_global_rules(self):
        approved = record("term:approved", status="approved", scope={"source_ids": ["10010101"]})
        current = knowledge.project([approved], source_ids=["10010101"])
        other = knowledge.project([approved], speakers=["アルテ"], source_ids=["10011201"])
        self.assertEqual(current["effective_authority"][approved["id"]], "approved_within_declared_scope")
        self.assertEqual(other["effective_authority"][approved["id"]], "reference")
        self.assertEqual(other["records"][0]["status"], "approved")
        self.assertEqual(other["records"][0]["scope"], {"source_ids": ["10010101"]})

    def test_evidence_source_is_not_declared_scope_and_free_text_stays_reference(self):
        approved = record("term:approved", status="approved", sources=["10011201"],
                          scope={"source_ids": ["10010101"]})
        evidence_scene = knowledge.project([approved], source_ids=["10011201"])
        self.assertEqual(evidence_scene["effective_authority"][approved["id"]], "reference")
        declared_scene = knowledge.project([approved], source_ids=["10010101"])
        self.assertEqual(declared_scene["records"], [approved])
        self.assertEqual(declared_scene["effective_authority"][approved["id"]], "approved_within_declared_scope")
        for scope in ["scene-only", {"source_ids": ["10011201"], "condition": "a narrower context"}]:
            approved["scope"] = scope
            reference = knowledge.project([approved], source_ids=["10011201"])
            self.assertEqual(reference["records"], [approved])
            self.assertEqual(reference["effective_authority"][approved["id"]], "reference")

    def test_historical_same_novel_id_does_not_prove_current_occurrence(self):
        old = record("term:historical", status="approved")
        old["origin"] = {"commit": knowledge.PUBLIC_BASE, "path": "historical.jsonl", "line": 1}
        selected = knowledge.project([old], source_ids=["10010101"], jp_texts=["アルテ"])
        self.assertEqual(selected["records"][0]["status"], "approved")
        self.assertEqual(selected["effective_authority"][old["id"]], "reference")

    def test_soft_budget_reports_omissions_without_changing_sources(self):
        records = [record("term:" + str(i)) for i in range(3)]
        selected = knowledge.project(records, speakers=["アルテ"], max_chars=1)
        self.assertEqual(selected["records"], [])
        self.assertEqual(set(selected["omitted_ids"]), {r["id"] for r in records})
        self.assertEqual(selected["matched_records"], 3)
        self.assertEqual(records[0]["value"]["source_text"], "アルテ")

    def test_common_speaker_does_not_pull_unrelated_relationships(self):
        speaker = record("entity:director", jp="館長", sources=[])
        speaker["kind"] = "entity"
        speaker["value"] = {"entity_id": "entity:director", "canonical_source_name": "館長"}
        relation = record("relationship:other", jp="", sources=["99999999"])
        relation["kind"] = "relationship"
        relation["value"] = {"subject_entity_id": "entity:director", "object_entity_id": "entity:absent",
                             "claim_ko": "다른 장면에서 관찰된 관계"}
        speech = record("speech:absent", jp="", sources=["99999999"])
        speech["kind"] = "speech"
        speech["value"] = {"speaker_entity_id": "entity:absent", "target_entity_id": "entity:director",
                           "address_source_forms": ["館長"], "register_observation_ko": "다른 인물의 말투"}
        selected = knowledge.project([speaker, relation, speech], speakers=["館長"], source_ids=["10011201"])
        self.assertEqual([item["id"] for item in selected["records"]], [speaker["id"]])

    def test_unapproved_changes_do_not_modify_current_data(self):
        original, names = [record("term:one")], {"アルテ": "아르테"}
        result, result_names = knowledge.apply_reviewed_changes(original, names, [
            {"kind": "name", "jp": "アルテ", "old_ko": "아르테", "ko": "다른 값", "approved": False},
        ], review_id="review")
        self.assertEqual(result, original)
        self.assertEqual(result_names, names)

    def test_same_ko_never_merges_exact_names(self):
        keys = ["人A", "人Ａ", "人B", "人 A", "ｶﾞ", "ガ", "カ\u3099"]
        changes = [{"kind": "name", "jp": key, "old_ko": None, "ko": "같은 표시", "approved": True} for key in keys]
        _, names = knowledge.apply_reviewed_changes([], {}, changes, review_id="review")
        self.assertEqual(set(names), set(keys))
        self.assertEqual(len(names), len(keys))

    def test_stale_preimage_rejects_complete_changeset_without_mutation(self):
        original, names = [record("term:one")], {"アルテ": "아르테"}
        before = deepcopy((original, names))
        replacement = record("term:one", status="approved")
        with self.assertRaisesRegex(ValueError, "preimage"):
            knowledge.apply_reviewed_changes(original, names, [
                {"kind": "name", "jp": "新しい人", "old_ko": None, "ko": "새 사람", "approved": True},
                {"kind": "knowledge", "id": "term:one", "old_sha256": "0" * 64,
                 "value": replacement, "approved": True},
            ], review_id="review")
        self.assertEqual((original, names), before)

    def test_reviewed_replacement_is_exact_and_duplicate_target_is_error(self):
        original = record("term:one")
        replacement = record("term:one", status="approved", scope="global")
        replacement["origin"] = {"review_id": "new-review", "source": "test row"}
        change = {"kind": "knowledge", "id": original["id"], "old_sha256": knowledge.record_sha256(original),
                  "value": replacement, "approved": True}
        result, _ = knowledge.apply_reviewed_changes([original], {}, [change], review_id="new-review")
        self.assertEqual(result, [replacement])
        with self.assertRaisesRegex(ValueError, "duplicate"):
            knowledge.apply_reviewed_changes([original], {}, [change, change], review_id="new-review")

    def test_current_file_roundtrip_and_duplicate_json_key_rejection(self):
        values = [record("term:1", jp="全角Ａ\t カ\u3099 ")]
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "knowledge.jsonl"
            path.write_bytes(knowledge.dump_knowledge(values))
            self.assertEqual(knowledge.load_knowledge(path), values)
            path.write_text('{"id":"first","id":"second"}\n', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate JSON key"):
                knowledge.load_knowledge(path)

    def test_empty_scope_or_evidence_cannot_be_approved(self):
        for scope in [None, "", "  ", [], {}, False, {"source_ids": []}]:
            with self.subTest(scope=scope):
                value = record("term:new", status="approved", scope=scope)
                change = {"kind": "knowledge", "id": value["id"], "old_sha256": None,
                          "value": value, "approved": True}
                with self.assertRaisesRegex(ValueError, "scope"):
                    knowledge.apply_reviewed_changes([], {}, [change], review_id="review")
        value = record("term:new", status="approved", scope="explicit scene")
        value["origin"] = {}
        with self.assertRaisesRegex(ValueError, "provenance"):
            knowledge.apply_reviewed_changes([], {}, [{"kind": "knowledge", "id": value["id"], "old_sha256": None,
                "value": value, "approved": True}], review_id="review")

    def test_reviewed_knowledge_needs_actual_value_and_matching_evidence(self):
        def apply(value):
            return knowledge.apply_reviewed_changes([], {}, [{"kind": "knowledge", "id": value["id"],
                "old_sha256": None, "value": value, "approved": True}], review_id="fixture-review")
        for empty in [{}, {"status": "approved"}, {"evidence_ids": ["evidence:one"]}]:
            value = record("term:new", status="approved")
            value["value"] = empty
            with self.assertRaisesRegex(ValueError, "actual content"):
                apply(value)
        for origin in [{"source": "scene:row"}, {"review_id": "other", "source": "scene:row"},
                       {"review_id": "fixture-review"}]:
            value = record("term:new", status="approved")
            value["origin"] = origin
            with self.assertRaisesRegex(ValueError, "provenance"):
                apply(value)
        valid = record("term:new", status="approved", scope="global")
        updated, _ = apply(valid)
        self.assertEqual(updated, [valid])
        selected = knowledge.project(updated, jp_texts=["アルテ"])
        self.assertEqual(selected["effective_authority"][valid["id"]], "approved_within_declared_scope")


if __name__ == "__main__":
    unittest.main()
