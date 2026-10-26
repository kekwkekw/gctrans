"""Disposable structural fixtures; no actual policy verdict or PRO review.

The explicit fixture review ids exercise the import contract only. They are not
evidence of a real semantic review, service-policy decision, or runtime PASS.
"""
from __future__ import annotations

import copy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tests"))
import novel_workflow as workflow
from build_manifests import build, encode
from contract_validation import read_json
from ng04_apply_tests import small_package
from translation_knowledge import dump_knowledge, load_current
from verify_staged_release import verify


class NovelWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.area = Path(self.temp.name)
        self.repo = self.area / "repo"
        (self.repo / "translations").mkdir(parents=True)
        self.package = small_package(self.repo / "translations/ko")
        # The test may run while root is reconciling Names metadata in its checkout.
        build(self.package, modified=["names.json"])
        (self.repo / "translation").mkdir()
        shutil.copyfile(ROOT / "translation/HOLD_POLICY.md", self.repo / "translation/HOLD_POLICY.md")
        (self.repo / "translation/knowledge.jsonl").write_bytes(b"")
        self.existing = next(iter(read_json(self.package / "manifest.json")["novels"]))
        self.identity = "99999998"
        self.script = self.area / "script.txt"
        self.raw = ("\ufefftitle,機械検証,0\r\n"
                    "message,名前Ａ,  ＡＢＣ<br>■■■<color=#fff>{PLAYER}</color>,0\r\n"
                    "msgvoicesync,voice,名前A,半角ｶﾅとカナ,0\r\n"
                    "message,名前B,同じ,0\r\n"
                    "message,名前C,同じ,0\r\n"
                    "message,名前D,末尾  ,0\r\n"
                    "message_extra,skip,not a message\r\n").encode("utf-8")
        self.script.write_bytes(self.raw)
        self.manifest = self.area / "sources.json"
        self.save_sources()
        self.batch = self.repo / "work/novels/synthetic"

    def tearDown(self):
        self.temp.cleanup()

    def save_sources(self, identity=None):
        identity = identity or self.identity
        workflow.write_json(self.manifest, {"schema": "gc-novel-sources-v1", "sources": [{
            "source_id": identity, "path": "script.txt", "title": "구조 검사 전용", "source_scope": "H_BODY"}]})

    def prepare(self, *, identity=None, register=False):
        identity = identity or self.identity
        self.save_sources(identity)
        return workflow.prepare_batch(self.repo, self.batch, source_manifest=self.manifest,
                                      register=[identity] if register else [], reason="legacy_retranslation" if identity == self.existing else "source_update")

    def complete_draft(self, *, held=False):
        draft = read_json(self.batch / "draft.json")
        for row in draft["rows"]:
            row.update(hold=False, reason="", ko="검증 " + row["jp"])
        if held:
            draft["rows"][-1].update(hold=True, reason="SYNTHETIC_MECHANICS_FIXTURE_NOT_A_POLICY_VERDICT", ko="")
        workflow.write_json(self.batch / "draft.json", draft)
        return draft

    def finish_fixture_review(self, *, held=False):
        self.complete_draft(held=held)
        workflow.prepare_review(self.repo, self.batch)
        review = read_json(self.batch / "review.json")
        review.update(pro_review_complete=True, review_id="SYNTHETIC_FIXTURE_NOT_REAL_PRO")
        workflow.write_json(self.batch / "review.json", review)
        return review

    def test_exact_source_parser_bom_crlf_spans_and_no_normalization(self):
        rows = workflow.parse_script(self.raw, self.identity)
        self.assertEqual(len(rows), 6)
        for row in rows:
            left, right = row["raw_field_bytes"]
            self.assertEqual(self.raw[left:right].decode("utf-8"), row["jp"])
            self.assertNotIn("raw_index", row)
        self.assertEqual(rows[1]["speaker"], "名前Ａ")
        self.assertEqual(rows[2]["speaker"], "名前A")
        self.assertTrue(rows[1]["jp"].startswith("  ＡＢＣ"))
        self.assertEqual(rows[-1]["jp"], "末尾  ")
        special = "message,A,が,0\nmessage,A,か\u3099,0\nmessage,A,X\\nY,0\nmessage,A,X<br>Y,0".encode()
        self.assertEqual([row["jp"] for row in workflow.parse_script(special, self.identity)], ["が", "か\u3099", "X\\nY", "X<br>Y"])

    def test_prepare_is_not_a_semantic_or_hold_decision(self):
        result = self.prepare()
        self.assertEqual(result["semantic_review"], "NOT_RUN")
        draft = read_json(self.batch / "draft.json")
        self.assertTrue(all(row["hold"] is None and row["ko"] == "" for row in draft["rows"]))
        self.assertEqual((self.batch / "HOLD_POLICY.md").read_bytes(), (self.repo / "translation/HOLD_POLICY.md").read_bytes())
        self.assertEqual((self.batch / "source" / (self.identity + ".txt")).read_bytes(), self.raw)
        with self.assertRaisesRegex(ValueError, "decision missing"):
            workflow.prepare_review(self.repo, self.batch)
        self.assertFalse((self.package / f"novels/{self.identity}.json").exists())

    def test_frozen_policy_must_match_exact_bytes(self):
        self.prepare()
        (self.batch / "HOLD_POLICY.md").write_bytes((self.batch / "HOLD_POLICY.md").read_bytes() + b"\n")
        with self.assertRaisesRegex(ValueError, "differs from frozen"):
            workflow.prepare_review(self.repo, self.batch)
        (self.repo / "translation/HOLD_POLICY.md").write_bytes(b"alternate policy")
        with self.assertRaisesRegex(ValueError, "Frozen HOLD_POLICY"):
            workflow.prepare_batch(self.repo, self.repo / "work/new", source_manifest=self.manifest)

    def test_review_template_is_unapproved_and_draft_cannot_bypass(self):
        self.prepare()
        self.complete_draft()
        workflow.prepare_review(self.repo, self.batch)
        with self.assertRaisesRegex(ValueError, "not complete"):
            workflow.check_review(self.repo, self.batch, self.batch / "review.json")
        with self.assertRaisesRegex(ValueError, "Invalid PRO"):
            workflow.apply_review(self.repo, self.batch, self.batch / "draft.json")

    def test_new_novel_hold_placeholder_builder_and_unrelated_preservation(self):
        before = {p.relative_to(self.package).as_posix(): p.read_bytes() for p in self.package.rglob("*") if p.is_file()}
        self.prepare(register=True)
        self.finish_fixture_review(held=True)
        result = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(result["status"], "APPLIED_LOCAL")
        self.assertEqual(result["hold_rows"], 1)
        self.assertEqual(result["game"], "NOT_RUN")
        actual = read_json(self.package / f"novels/{self.identity}.json")
        self.assertEqual(actual["末尾  "], "[구조 검사 전용 - 6행 HOLD]")
        self.assertEqual(len(actual), 5)
        self.assertIn(self.identity, read_json(self.package / "manifest.json")["novels"])
        for name, raw in before.items():
            if name not in workflow.META_FILES:
                self.assertEqual((self.package / name).read_bytes(), raw, name)
        holds = workflow._jsonl(self.repo / "translation/holds.jsonl")
        self.assertEqual(holds[0]["policy_sha256"], workflow.POLICY_SHA256)
        self.assertEqual(holds[0]["source_sha256"], workflow.digest(self.raw))
        self.assertEqual(verify(self.package)["status"], "PASS")
        again = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(again["changed_files"], [])

    def test_existing_novel_retranslation_reason_and_no_registration_side_effects(self):
        registered_before = set(read_json(self.package / "manifest.json")["novels"])
        self.prepare(identity=self.existing)
        self.assertEqual(read_json(self.batch / "batch.json")["reason"], "legacy_retranslation")
        self.finish_fixture_review()
        workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(set(read_json(self.package / "manifest.json")["novels"]), registered_before)
        self.assertEqual(len(read_json(self.package / f"novels/{self.existing}.json")), 5)
        self.assertFalse((self.repo / "translation/holds.jsonl").exists())

    def test_current_jp_key_input_freezes_keys_nulls_unknown_locators_and_preserves_order(self):
        relative = f"novels/{self.existing}.json"
        target = self.package / relative
        ordered = {"Z題": "OLD_TITLE_KO", "A名前Ａ": "OLD_NAME_KO", "M<br>{PLAYER}": "OLD_TAG_KO"}
        target.write_bytes(workflow.encode_novel_payload(ordered))
        names = read_json(self.package / "names.json")
        names["名前Ａ"] = "이름Ａ"
        workflow.write_json(self.package / "names.json", names)
        master = read_json(self.package / "master.json")
        master.setdefault("mNovels", {}).setdefault("ml_title[]", {})["A名前Ａ"] = "표시 제목"
        workflow.write_json(self.package / "master.json", master)
        build(self.package, modified=[relative, "names.json", "master.json"])

        result = workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                                        source_ids=[self.existing], reason="legacy_retranslation")
        self.assertEqual(result["rows"], 3)
        snapshot_path = self.batch / "source" / f"{self.existing}.keys.json"
        snapshot_raw = snapshot_path.read_bytes()
        snapshot = read_json(snapshot_path)
        self.assertEqual(snapshot["keys"], list(ordered))
        self.assertEqual(snapshot["display_title"], "A名前Ａ")
        self.assertEqual(snapshot["display_title_key_ordinal_1based"], 2)
        self.assertFalse(any(value.encode("utf-8") in snapshot_raw for value in ordered.values()))
        document = read_json(self.batch / "batch.json")
        self.assertEqual(document["source_kind"], workflow.CURRENT_KEYS_KIND)
        self.assertEqual(document["row_basis"], workflow.CURRENT_KEYS_ROW_BASIS)
        self.assertEqual(document["sources"][0]["title"], "A名前Ａ")
        self.assertEqual(document["sources"][0]["snapshot_sha256"], workflow.digest(snapshot_raw))
        rows = read_json(self.batch / "draft.json")["rows"]
        self.assertEqual([row["jp"] for row in rows], list(ordered))
        for row in rows:
            self.assertIsNone(row["speaker"])
            self.assertIsNone(row["command"])
            self.assertIsNone(row["physical_line_1based"])
            self.assertIsNone(row["raw_field_bytes"])
        context = read_json(self.batch / "context.json")
        self.assertEqual(context["names"]["名前Ａ"], "이름Ａ")
        self.assertIn("reference only", context["names_reference_basis"])

        self.finish_fixture_review(held=True)
        applied = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(applied["status"], "APPLIED_LOCAL")
        self.assertEqual(list(read_json(target)), list(ordered))
        target_raw = target.read_bytes()
        self.assertLess(target_raw.index("Z題".encode()), target_raw.index("A名前Ａ".encode()))
        self.assertLess(target_raw.index("A名前Ａ".encode()), target_raw.index("M<br>{PLAYER}".encode()))
        hold = workflow._jsonl(self.repo / "translation/holds.jsonl")[0]
        self.assertEqual(hold["source_snapshot_sha256"], workflow.digest(snapshot_raw))
        self.assertNotIn("source_sha256", hold)
        self.assertEqual(hold["physical_line_1based"], None)
        self.assertEqual(workflow.apply_review(self.repo, self.batch, self.batch / "review.json")["changed_files"], [])

    def test_current_jp_key_input_uses_neutral_display_title_without_unique_master_match(self):
        relative = f"novels/{self.existing}.json"
        target = self.package / relative
        target.write_bytes(workflow.encode_novel_payload({"NO_MASTER_TITLE_A": "old A", "NO_MASTER_TITLE_B": "old B"}))
        build(self.package, modified=[relative])
        workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                               source_ids=[self.existing], reason="legacy_retranslation")
        snapshot = read_json(self.batch / "source" / f"{self.existing}.keys.json")
        self.assertEqual(snapshot["display_title"], f"Novel {self.existing}")
        self.assertEqual(snapshot["display_title_basis"], "neutral_source_id_no_unique_master_match")
        self.assertIsNone(snapshot["display_title_key_ordinal_1based"])
        self.assertEqual(read_json(self.batch / "batch.json")["sources"][0]["title"], f"Novel {self.existing}")

    def test_current_jp_key_input_rejects_target_collision_but_not_normal_retry(self):
        workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                               source_ids=[self.existing], reason="legacy_retranslation")
        self.finish_fixture_review()
        target = self.package / f"novels/{self.existing}.json"
        target.write_bytes(b'{"independent":"change"}\n')
        with self.assertRaisesRegex(ValueError, "Target translation changed"):
            workflow.check_review(self.repo, self.batch, self.batch / "review.json")

    def test_current_jp_key_input_requires_explicit_unique_ids(self):
        with self.assertRaisesRegex(ValueError, "requires --source-id"):
            workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                                   reason="legacy_retranslation")
        with self.assertRaisesRegex(ValueError, "unique source ids"):
            workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                                   source_ids=[self.existing, self.existing], reason="legacy_retranslation")
        with self.assertRaisesRegex(ValueError, "requires legacy_retranslation"):
            workflow.prepare_batch(self.repo, self.batch, source_current_jp_keys=True,
                                   source_ids=[self.existing])

    def test_duplicate_exact_jp_conflict_is_error_not_hold(self):
        self.prepare()
        review = self.finish_fixture_review()
        review["rows"][4]["ko"] = "다른 값"
        workflow.write_json(self.batch / "review.json", review)
        with self.assertRaisesRegex(ValueError, "conflicting KO"):
            workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertFalse((self.repo / "translation/holds.jsonl").exists())

    def test_stale_source_draft_and_current_target_are_rejected(self):
        self.prepare(identity=self.existing)
        self.finish_fixture_review()
        self.script.write_bytes(self.raw + b"message,A,new,0\n")
        with self.assertRaisesRegex(ValueError, "source changed"):
            workflow.check_review(self.repo, self.batch, self.batch / "review.json")
        self.script.write_bytes(self.raw)
        draft_raw = (self.batch / "draft.json").read_bytes()
        (self.batch / "draft.json").write_bytes(draft_raw + b"\n")
        with self.assertRaisesRegex(ValueError, "batch/draft"):
            workflow.check_review(self.repo, self.batch, self.batch / "review.json")
        (self.batch / "draft.json").write_bytes(draft_raw)
        (self.package / f"novels/{self.existing}.json").write_bytes(b'{"independent":"change"}\n')
        with self.assertRaisesRegex(ValueError, "Target translation changed"):
            workflow.apply_review(self.repo, self.batch, self.batch / "review.json")

    def test_structure_tokens_checked_without_content_classification(self):
        self.prepare()
        draft = self.complete_draft()
        draft["rows"][1]["ko"] = draft["rows"][1]["ko"].replace("■■■", "")
        workflow.write_json(self.batch / "draft.json", draft)
        with self.assertRaisesRegex(ValueError, "Markup/variable/break mismatch"):
            workflow.prepare_review(self.repo, self.batch)
        self.assertFalse((self.repo / "translation/holds.jsonl").exists())
        self.assertNotEqual(workflow._structure("<color=red>A\\nB</color>"),
                            workflow._structure("<color=red>AB</color>\\n"))

    def test_reviewed_names_and_knowledge_apply_together_and_retry(self):
        self.prepare()
        review = self.finish_fixture_review()
        record = {"id": "synthetic:term", "kind": "term", "status": "approved", "scope": {"source_ids": [self.identity]},
                  "source_ids": [self.identity], "content_available": True,
                  "value": {"jp": "機械検証", "ko": "기계 검증"},
                  "origin": {"review_id": "SYNTHETIC_FIXTURE_NOT_REAL_PRO", "evidence": "SYNTHETIC_FIXTURE_SOURCE"}}
        review["changes"] = [{"kind": "name", "jp": "名前Ａ", "old_ko": None, "ko": "이름Ａ", "approved": True},
                             {"kind": "knowledge", "id": record["id"], "old_sha256": None, "value": record, "approved": True},
                             {"kind": "name", "jp": "名前A", "old_ko": None, "ko": "미승인", "approved": False}]
        workflow.write_json(self.batch / "review.json", review)
        result = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(read_json(self.package / "names.json")["名前Ａ"], "이름Ａ")
        self.assertNotIn("名前A", read_json(self.package / "names.json"))
        self.assertEqual(load_current(self.repo), [record])
        self.assertTrue(any(item["path"] == "translation/knowledge.jsonl" for item in result["changed_files"]))
        self.assertEqual(workflow.apply_review(self.repo, self.batch, self.batch / "review.json")["changed_files"], [])

    def test_concurrent_unrelated_names_are_not_overwritten_during_staging(self):
        self.prepare()
        self.finish_fixture_review()
        actual_build = workflow.build
        def concurrent(*args, **kwargs):
            result = actual_build(*args, **kwargs)
            names = read_json(self.package / "names.json")
            names["別の更新"] = "독립 변경"
            workflow.write_json(self.package / "names.json", names)
            return result
        with patch.object(workflow, "build", side_effect=concurrent):
            with self.assertRaisesRegex(ValueError, "changed while staging"):
                workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(read_json(self.package / "names.json")["別の更新"], "독립 변경")
        self.assertFalse((self.package / f"novels/{self.identity}.json").exists())

    def test_concurrent_knowledge_and_registration_are_preserved(self):
        self.prepare()
        self.finish_fixture_review()
        actual_build = workflow.build
        record = {"id": "independent:term", "kind": "term", "status": "approved", "scope": "global",
                  "source_ids": [], "content_available": True, "value": {"jp": "独立", "ko": "독립"},
                  "origin": {"review_id": "SYNTHETIC_CONCURRENT_REVIEW", "source": "fixture"}}
        unregistered = next(path.stem for path in (self.package / "novels").glob("*.json")
                            if path.stem not in read_json(self.package / "manifest.json")["novels"])
        def concurrent(*args, **kwargs):
            result = actual_build(*args, **kwargs)
            (self.repo / "translation/knowledge.jsonl").write_bytes(dump_knowledge([record]))
            actual_build(self.package, register=[unregistered])
            return result
        with patch.object(workflow, "build", side_effect=concurrent):
            with self.assertRaisesRegex(ValueError, "changed while staging"):
                workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(load_current(self.repo), [record])
        self.assertIn(unregistered, read_json(self.package / "manifest.json")["novels"])
        self.assertEqual(verify(self.package)["status"], "PASS")
        self.assertFalse((self.package / f"novels/{self.identity}.json").exists())

    def test_write_failure_rolls_back_owned_changes(self):
        self.prepare(register=True)
        self.finish_fixture_review()
        before = {p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}
        actual_replace = os.replace
        failed = False
        def once(source, destination):
            nonlocal failed
            if str(Path(destination)) == str(self.package / "release-manifest.json") and not failed:
                failed = True
                raise OSError("SYNTHETIC_WRITE_FAILURE")
            return actual_replace(source, destination)
        with patch.object(workflow.os, "replace", side_effect=once):
            with self.assertRaisesRegex(OSError, "SYNTHETIC_WRITE_FAILURE"):
                workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertTrue(failed)
        self.assertFalse((self.package / f"novels/{self.identity}.json").exists())
        self.assertEqual({p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}, before)

    def test_review_reason_changed_during_staging_is_not_silently_applied(self):
        self.prepare()
        self.finish_fixture_review(held=True)
        before = {p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}
        actual_build = workflow.build
        def concurrent(*args, **kwargs):
            result = actual_build(*args, **kwargs)
            review = read_json(self.batch / "review.json")
            review["rows"][-1]["reason"] = "SYNTHETIC_REVISED_REASON_SAME_KO"
            workflow.write_json(self.batch / "review.json", review)
            return result
        with patch.object(workflow, "build", side_effect=concurrent):
            with self.assertRaisesRegex(ValueError, "Review or inputs changed while staging"):
                workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual({p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}, before)
        self.assertFalse((self.repo / "translation/holds.jsonl").exists())

    def test_partial_temporary_write_and_fsync_failures_leave_no_package_residue(self):
        target = self.package / "names.json"
        old = target.read_bytes()
        before = {p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}
        actual_open = Path.open
        class PartialWriter:
            def __init__(inner, stream):
                inner.stream = stream
            def __enter__(inner):
                return inner
            def __exit__(inner, *args):
                inner.stream.close()
            def write(inner, raw):
                inner.stream.write(raw[:3])
                inner.stream.flush()
                raise OSError("SYNTHETIC_PARTIAL_TEMP_WRITE")
        def partial_open(path, *args, **kwargs):
            stream = actual_open(path, *args, **kwargs)
            return PartialWriter(stream) if path.name.startswith(".novel-") and args[:1] == ("xb",) else stream
        with patch.object(Path, "open", partial_open):
            with self.assertRaisesRegex(OSError, "SYNTHETIC_PARTIAL_TEMP_WRITE"):
                workflow._publish({target: b'{"new":"value"}'}, {target: old})
        self.assertEqual({p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}, before)
        with patch.object(workflow.os, "fsync", side_effect=OSError("SYNTHETIC_TEMP_FSYNC")):
            with self.assertRaisesRegex(OSError, "SYNTHETIC_TEMP_FSYNC"):
                workflow._publish({target: b'{"new":"value"}'}, {target: old})
        self.assertEqual({p: p.read_bytes() for p in self.package.rglob("*") if p.is_file()}, before)
        self.assertEqual(verify(self.package)["status"], "PASS")

    def test_diagnostic_report_failure_reports_successful_canonical_apply(self):
        self.prepare(register=True)
        self.finish_fixture_review()
        actual_write_json = workflow.write_json
        def fail_report(path, value):
            if path == self.batch / "result.json":
                raise OSError("SYNTHETIC_REPORT_WRITE_FAILURE")
            return actual_write_json(path, value)
        with patch.object(workflow, "write_json", side_effect=fail_report):
            result = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(result["status"], "APPLIED_LOCAL")
        self.assertIn("SYNTHETIC_REPORT_WRITE_FAILURE", result["report_write_error"])
        self.assertTrue((self.package / f"novels/{self.identity}.json").exists())
        self.assertFalse((self.batch / "result.json").exists())
        self.assertEqual(verify(self.package)["status"], "PASS")
        self.assertEqual(workflow.apply_review(self.repo, self.batch, self.batch / "review.json")["changed_files"], [])

    def test_scanner_adapter_requires_exact_selected_current_textasset(self):
        from update.source_scan import SourceProfile, scan_inventory
        from update.novel_intake import create_intake_references
        from update.source_work import reconcile
        class Extractor:
            version = "SYNTHETIC_EXTRACTOR"
            def __call__(inner, raw, asset):
                return {"textassets": [{"name": "script", "path_id": 1, "container": "synthetic", "bytes": self.raw}], "selected": 0}
        raw_root = self.area / "raw"
        result = scan_inventory(SourceProfile("PC", "R18", "Novel", "pc-explicit-local-cache-v1"),
                                {"d": [{"n": f"notinit/novel_script/sts_{self.identity}.dmm", "h": "one", "s": 1}]},
                                state_root=self.area / "source-state", raw_root=raw_root,
                                fetch_asset=lambda _: b"fixture", extractor=Extractor())
        snapshot_path = self.area / "snapshot.json"
        workflow.write_json(snapshot_path, result)
        catalog = create_intake_references(result["sourceSnapshot"], self.area / "catalog.json")
        work = reconcile(result["sourceSnapshot"], catalog, self.area / "source-state")
        prepared = workflow.prepare_batch(self.repo, self.batch, source_snapshot=snapshot_path,
                                          raw_root=raw_root, source_ids=[self.identity], state_root=self.area / "source-state")
        self.assertEqual(prepared["rows"], 6)
        self.finish_fixture_review()
        applied = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(applied["intake_completion"]["status"], "COMPLETED")
        state = read_json(Path(work["state_path"]))
        self.assertTrue(all(item["status"] == "completed" for item in state["work"].values()))
        retry = workflow.apply_review(self.repo, self.batch, self.batch / "review.json")
        self.assertEqual(retry["intake_completion"]["status"], "ALREADY_COMPLETED")
        result["sourceSnapshot"]["novel"][0]["source_collision"] = True
        workflow.write_json(snapshot_path, result)
        with self.assertRaisesRegex(ValueError, "not current"):
            workflow.check_review(self.repo, self.batch, self.batch / "review.json")


if __name__ == "__main__":
    unittest.main(verbosity=2)
