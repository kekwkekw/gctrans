"""Repository-only retirement guard. No legacy execution, network or local-PC scan."""
import ast
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]
RETIRED = (
    "run_all.bat", "scripts/run.py", "scripts/update.py", "scripts/gpt.py",
    "scripts/translate_script.py", "scripts/merge.py", "scripts/utils.py",
    "scripts/parse.py", "scripts/requirements.txt", "app.js", "package.json",
    "package-lock.json", "vercel.json", "项目GPT字典.txt", "!!!!!!!!!!!!!!!!!!!최근로그.txt",
)
class RetirementTests(unittest.TestCase):
    def test_retired_entries_absent(self):
        self.assertEqual([], [p for p in RETIRED if (ROOT/p).exists()])
    def test_current_source_and_cdn_entries_remain(self):
        for name in ("scripts/crawl_only.py", "scripts/crypto.py", "tools/update/cli.py",
                     "tools/update/source_scan.py", "tools/update/novel_bridge.py",
                     "tools/build_manifests.py", "staging/accepted-master-profile.json",
                     "translations/ko/manifest.json", "translations/ko/names.json",
                     "translations/ko/master.json", "candidate/GCMod.dll",
                     ".github/workflows/crawl_only.yml", ".github/workflows/update_translation.yml"):
            self.assertTrue((ROOT/name).is_file(), name)
    def test_current_python_has_no_retired_import(self):
        forbidden={"scripts.parse","scripts.utils","scripts.gpt","scripts.update","scripts.merge","scripts.run","scripts.translate_script"}
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
