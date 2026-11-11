"""Repository-only retirement guard. No legacy execution, network or local-PC scan."""
import ast
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]
RETIRED = (
    "run_all.bat", "scripts/run.py", "scripts/update.py", "scripts/gpt.py",
    "scripts/translate_script.py", "scripts/merge.py", "scripts/utils.py",
    "scripts/parse.py", "scripts/requirements.txt", "app.js", "package.json",
    "package-lock.json", "vercel.json",
    "tools/update/novel_bridge.py", "tools/update/novel_bridge_worker.py",
    "tools/update/intake_work.py", ".github/workflows/update_translation.yml", "项目GPT字典.txt", "!!!!!!!!!!!!!!!!!!!최근로그.txt",
    "GCMod-cs파일백업", "names", "words", "novels", "source/GCMod.Nextgen",
    "source/build/Common.props", "tools/UpdateCli", "tools/CompileAudit", "tools/verify_staged_release.py",
    "staging/GCMod.cfg.template", "staging/accepted-master-profile.cfg.fragment",
    "staging/accepted-master-profile.json", "staging/master-local-coverage-20261027",
    "staging/master-source-review-20261028", "staging/text-remainder-20261101",
    "staging/text-resurvey-20261102", "staging/text-resurvey-followup-20261102",
    "staging/ui-ap-exact-20261101", "staging/ui-broad-export-20261028",
    "staging/ui-residual8-20261028",
    "docs/PROJECT_NATIVE_TEST_POLICY_KO.md",
    "docs/PROJECT_STABLE_QUALIFICATION_POLICY_KO.md",
    "docs/REPOSITORY_CLEANUP_20261026_KO.md",
    "docs/REPOSITORY_CLEANUP_20261102_KO.md",
    "docs/TEXT_COVERAGE_20261031_KO.md",
    "docs/FULL_TEXT_RESURVEY_20261102_KO.md",
    "docs/TEXT_RESURVEY_FOLLOWUP_20261102_KO.md",
    "docs/UI_AP_EXACT_20261101_KO.md",
    "tests/NG05_LOCAL_OPERATIONS_CPU.json",
    "tests/verify_import.py",
    "tests/data_activate_ng02.py",
    "tests/data_full_candidate_check.py",
    "tests/data_generate_validator_corpus.py",
    "tests/run_ng03_source_evidence.py",
    "tests/run_ng04_source_evidence.py",
    "tests/ng06_reference_review_boundaries.py",
    "tests/ng06_reference_assert_fixed_results.py",
    "tests/ng03_apply_tests.py",
)
class RetirementTests(unittest.TestCase):
    def test_retired_entries_absent(self):
        self.assertEqual([], [p for p in RETIRED if (ROOT/p).exists()])
    def test_current_source_and_cdn_entries_remain(self):
        for name in ("scripts/crawl_only.py", "scripts/crypto.py", "tools/update/cli.py",
                     "tools/update/source_scan.py", "tools/update/novel_intake.py", "tools/update/source_work.py",
                     "tools/build_manifests.py",
                     "staging/master-holds.json", "staging/text-resurvey-format-followup.json",
                     "staging/crawler-closeout-20261103/summary.json",
                     "staging/pc-literal-review-20261102/classification-ledger.json",
                     "staging/pc-literal-review-20261102/pro-reviewed/format-followup.json",
                     "staging/text-coverage-20261031/pro-reviewed-20261031/APPLICATION_RECEIPT.json",
                     "staging/text-coverage-20261031/pro-reviewed-20261031/Novel_6/review.return.json",
                     "translations/ko/manifest.json", "translations/ko/names.json",
                     "translations/ko/master.json", "candidate/GCMod.dll",
                     ".github/workflows/crawl_only.yml"):
            self.assertTrue((ROOT/name).is_file(), name)
    def test_current_python_has_no_retired_import(self):
        forbidden={"scripts.parse","scripts.utils","scripts.gpt","scripts.update","scripts.merge","scripts.run","scripts.translate_script","update.novel_bridge","update.novel_bridge_worker","update.intake_work"}
        for root in (ROOT/"scripts",ROOT/"tools"):
            for path in root.rglob("*.py"):
                nodes=ast.walk(ast.parse(path.read_text(encoding="utf-8-sig")))
                for node in nodes:
                    if isinstance(node,ast.Import):
                        for alias in node.names:self.assertNotIn(alias.name,forbidden,str(path))
                    if isinstance(node,ast.ImportFrom):self.assertNotIn(node.module,forbidden,str(path))
    def test_operating_exporter_does_not_require_deleted_helpers(self):
        src=(ROOT/"tools/export_git_rc.py").read_text(encoding="utf-8-sig")
        tree=ast.parse(src);value=None
        for node in tree.body:
            if isinstance(node,ast.Assign) and any(isinstance(x,ast.Name) and x.id=="SCRIPTS" for x in node.targets):value=ast.literal_eval(node.value)
        self.assertIsNotNone(value)
        self.assertFalse(set(RETIRED)&value)
        self.assertTrue(all((ROOT/p).is_file() for p in value))
if __name__=="__main__":unittest.main()
