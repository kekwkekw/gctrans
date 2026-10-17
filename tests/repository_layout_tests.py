"""Documentation/branch migration regressions; no game, network or account access."""
from pathlib import Path
import ast
import json
import posixpath
import re
import subprocess
import sys
import tempfile
import unittest
from urllib.parse import unquote, urlsplit
ROOT = Path(__file__).resolve().parents[1]
DOCS = ('COMPREHENSIVE_RESTORE_KO.md','EXTERNAL_DEPENDENCIES_KO.md','GEOMETRY_RESTORATION_KO.md',
 'LAUNCH_PROCEDURE_KO.md','NETWORK_PREPARE_FIX_KO.md','OPERATOR_KO.md','PROJECT_NATIVE_TEST_POLICY_KO.md',
 'PROJECT_STABLE_QUALIFICATION_POLICY_KO.md','PUBLICATION_KO.md','README.md','REPRODUCE_KO.md',
 'RESTORATION_CLOSEOUT_KO.md','RESTORATION_MIGRATION_KO.md','RETIRED_PIPELINES_KO.md',
 'SCENARIO_DISPLAY_APPLY_KO.md','STABLE_QUALIFICATION_KO.md','SUPPORT_SCOPE_KO.md',
 'UI_PLAN_RESTART_HANDOFF_KO.md','UI_REGRESSION_FIX_KO.md','UI_REGRESSION_R2_PROMOTION_KO.md',
 'UPSTREAM_ATTRIBUTION_KO.md')
class RepositoryLayoutTests(unittest.TestCase):
    def test_only_entrypoint_readme_in_root(self):
        self.assertEqual(['README.md'], sorted(p.name for p in ROOT.glob('*.md')))
    def test_all_original_docs_preserved(self):
        self.assertTrue(all((ROOT/'docs'/p).is_file() for p in DOCS))
        self.assertTrue((ROOT/'docs/INDEX.md').is_file())
    def test_local_document_links_resolve(self):
        paths=[ROOT/'README.md',ROOT/'source/GCMod.Candidate/README_KO.md',*(ROOT/'docs').glob('*.md')]
        for p in paths:
            for target in re.findall(r'\]\(([^)\n]+)\)',p.read_text(encoding='utf-8-sig')):
                parsed=urlsplit(target)
                if parsed.scheme or target.startswith('#'):continue
                path=unquote(parsed.path)
                resolved=(ROOT/path.lstrip('/')) if path.startswith('/') else p.parent/path
                self.assertTrue(resolved.is_file(),f'{p.relative_to(ROOT)} -> {target}')
    def test_exporter_selects_document_folder(self):
        tree=ast.parse((ROOT/'tools/export_git_rc.py').read_text())
        values={n.targets[0].id:ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name) and n.targets[0].id in {'PREFIXES','ROOT_FILES'}}
        self.assertIn('docs/',values['PREFIXES'])
        self.assertEqual({'README.md'},set(x for x in values['ROOT_FILES'] if x.endswith('.md')))
    def test_actual_branch_probe_uses_current_main(self):
        src=(ROOT/'tests/Ng03SupplyTests/ActualBranchProbe.cs').read_text()
        self.assertNotIn('ng12/publication-candidate',src)
        self.assertIn('new("kekwkekw/gctrans", "main")',src)
        self.assertIn('/git/ref/heads/main',src)
    def test_document_newlines_and_workflow_reference(self):
        self.assertIn('/docs/** -text',(ROOT/'.gitattributes').read_text())
        text=(ROOT/'.github/workflows/update_translation.yml').read_text()
        self.assertIn('Use docs/OPERATOR_KO.md',text)
        self.assertIn('contents: read',text)
    def test_branch_custody_is_not_acceptance(self):
        record=json.loads((ROOT/'docs/BRANCH_RETIREMENT.json').read_bytes())
        self.assertFalse(record['release_or_runtime_approval'])
        self.assertNotIn('main',{x['name'] for x in record['branches']})
        self.assertEqual(len(record['branches']),len({x['name'] for x in record['branches']}))
        self.assertTrue(all(re.fullmatch('[0-9a-f]{40}',x['head']) for x in record['branches']))
    def test_real_committed_export_has_moved_docs(self):
        # The caller uses a disposable clone/fixture. Never add/commit a user checkout.
        if not (ROOT/'.git').exists(): self.skipTest('A committed isolated checkout is required')
        with tempfile.TemporaryDirectory(prefix='gc-doc-export-') as tmp:
            out=Path(tmp)/'export'
            p=subprocess.run([sys.executable,str(ROOT/'tools/export_git_rc.py'),'--repo',str(ROOT),'--ref','HEAD','--output',str(out)],capture_output=True,text=True)
            self.assertEqual(0,p.returncode,p.stderr+p.stdout)
            for name in DOCS:self.assertEqual((ROOT/'docs'/name).read_bytes(),(out/'docs'/name).read_bytes())
            self.assertFalse((out/'OPERATOR_KO.md').exists())
            self.assertTrue((out/'README.md').is_file())
            self.assertTrue((out/'scripts/__init__.py').is_file())
if __name__=='__main__':unittest.main()
