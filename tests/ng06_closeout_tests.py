"""NG06 actual product boundaries; all semantic changes are disposable fixtures."""
import copy, json, subprocess, sys, unittest
from pathlib import Path
from unittest.mock import patch
ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'tools'), str(ROOT/'tests')]
from ng05_operator_tests import OperatorTests
from ng04_apply_tests import correction, layout_review, ui_inputs
from update.apply_review import apply_review
from update.operator_review import plan_ui_edit
from update.diff_review import fingerprint
from update.preflight import preflight
from contract_validation import read_json
from build_manifests import encode

class CloseoutTests(OperatorTests):
    # Reuse fixture setup, not the inherited test catalog (run separately).
    def test_G02_every_source_obligation_is_held_while_UI_commits(self):
        resources = read_json(self.owned/'ui/resources.json')['resources']
        other = next(r for r in resources if r['id'] != self.resource['id'] and r['kind'] == 'literal' and r['disposition'] == 'translate' and r.get('ko'))
        _, row = ui_inputs(self.owned); row = copy.deepcopy(row)
        row['locator']['resource'] = other['id']; row['locator']['role'] = other['role']; row['jp'] = other['jp']
        review = self.ui('NG06 disposable correction')
        observed = correction(other, row, other['ko'], 'must remain original')['rows'][0]
        observed['review_id'] = 'missing-row'; review['rows'].append(observed)
        basis = {k: observed[k] for k in ('lane', 'locator', 'source_fingerprint')}
        binding = copy.deepcopy(read_json(self.owned/'ui/bindings.json')['bindings'][0])
        review.update(resource_rows=[dict(basis, review_id='missing-resource', decision='APPROVE', proposal=copy.deepcopy(other), old_resource_sha256=fingerprint(other))],
                      binding_rows=[dict(basis, review_id='missing-binding', decision='APPROVE', proposal=binding, old_binding_sha256=fingerprint(binding))],
                      master_key_rows=[{'review_id':'missing-key','decision':'APPROVE','target':{'file':'master.json','keys':['t','f','jp']},'source_basis':{},'source_basis_sha256':fingerprint({}),'old_ko_sha256':fingerprint('ko'),'new_ko':'fixture','all_uses_same_ko':True}])
        result = apply_review(self.owned, review, None, None, self.state)
        self.assertEqual(result['status'], 'PARTIAL_HOLDS')
        self.assertEqual(result['requested_count'], result['consumed_count'])
        self.assertEqual(len(result['held_operations']), 4)
        self.assertTrue(all(o['reason'] == 'CURRENT_OBSERVED_SOURCE_REQUIRED' for o in result['held_operations']))
        current = Path(result['next_package'])
        actual = read_json(current/'ui/resources.json')['resources']
        self.assertEqual(next(r for r in actual if r['id'] == self.resource['id'])['ko'], 'NG06 disposable correction')
        self.assertEqual(next(r for r in actual if r['id'] == other['id']), other)
        for rel in ('master.json','names.json','ui/bindings.json','manifest.json'):
            self.assertEqual((current/rel).read_bytes(), (self.owned/rel).read_bytes())
        self.assertEqual(result['apply_metrics']['unchanged_novel_payload_reads'], 0)

    def test_G02_HOLD_fixed_layout_registration_and_early_reject(self):
        review = self.ui('mixed HOLD fixture')
        review['master_key_rows'] = [{'review_id':'retained-HOLD','decision':'HOLD'}]
        review['layout_rows'] = layout_review(self.owned)['layout_rows']
        manifest = read_json(self.owned/'manifest.json')
        review['register'] = [next(iter(manifest['novels']))]
        result = apply_review(self.owned, review, None, None, self.state)
        self.assertEqual(result['status'], 'PARTIAL_HOLDS')
        self.assertEqual(result['held_operations'][0]['reason'], 'HOLD')
        self.assertTrue(all(o['status'] in {'APPLIED','NO_CHANGE'} for o in result['request_outcomes'] if o['operation'] != 'master_key_rows'))
        current = Path(result['next_package'])
        for mutation in ('operation','decision','unknown'):
            bad = plan_ui_edit(current, self.resource['id'], 'not applied')['review']
            if mutation == 'operation': bad['rows'][0]['operation'] = 'invented'
            elif mutation == 'decision': bad['rows'][0]['decision'] = 'YES'
            else: bad['invented_array'] = []
            with self.assertRaises(ValueError): apply_review(current, bad, None, None, self.state)
        # Explicit malformed source remains an early CLI reject, never omission.
        review_file = self.area/'review.json'; source_file = self.area/'bad-source.json'; report = self.area/'report.json'
        review_file.write_bytes(encode(plan_ui_edit(current, self.resource['id'], 'not applied')['review']))
        source_file.write_text('{}', encoding='utf-8')
        r = subprocess.run([sys.executable,'-X','utf8',str(ROOT/'tools/update/cli.py'),'apply-review','--package',str(current),'--state-root',str(self.state),'--review',str(review_file),'--current-source',str(source_file),'--report',str(report)],capture_output=True)
        self.assertEqual(r.returncode, 2); self.assertEqual(read_json(report)['status'], 'REJECTED_REQUEST')
        self.assertEqual(read_json(self.state/'authoring-current.json')['next_package'], str(current))

    def test_G03_axes_and_duplicate_risk_are_metadata_only(self):
        result = preflight(ROOT)
        self.assertEqual(result['status'], 'OFFLINE_ARTIFACT_PASS')
        self.assertEqual(result['artifact']['status'], 'PASS')
        self.assertTrue(all(d['status'] == 'NOT_CHECKED' for d in result['dependencies']))
        self.assertEqual(result['font']['status'], 'NOT_CHECKED')
        self.assertEqual(result['native_trial']['status'], 'NOT_EXECUTED_BY_PREFLIGHT')
        plugins = self.area/'explicit-plugins'; plugins.mkdir()
        (plugins/'GCMod.dll').write_bytes(b'MZ'+b'\x01\x00\x05GCMod')
        (plugins/'GCUI12Residuals.dll').write_bytes(b'fixture')
        medium = self.area/'medium-fixture'; medium.write_bytes(b'fixture-not-font-proof')
        result = preflight(ROOT, self.area/'missing-dependencies', medium, plugins)
        self.assertTrue(all(d['status'] == 'MISSING' for d in result['dependencies']))
        self.assertEqual(result['font']['status'], 'FOUND')
        self.assertEqual(result['plugins']['status'], 'RISKS_FOUND')
        self.assertEqual(len(result['plugins']['risks']), 2)
        self.assertEqual(result['native_trial']['glyph'], 'NOT_RUN')

if __name__ == '__main__':
    names = [name for name in CloseoutTests.__dict__ if name.startswith('test_G')]
    suite = unittest.TestSuite(CloseoutTests(name) for name in names)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    report = {'schema':'gc-ng06-closeout-regression-v1','status':'PASS' if result.wasSuccessful() else 'FAIL',
              'count':result.testsRun,'failures':len(result.failures)+len(result.errors),
              'semantic_changes':'DISPOSABLE_FIXTURES_ONLY','novel_workflow':'COVERED_BY_SEPARATE_NOVEL_TESTS',
              'native':'NOT_RUN','game':'NOT_RUN'}
    (ROOT/'tests/NG06_CLOSEOUT_TEST_RESULTS.json').write_bytes(encode(report))
    raise SystemExit(0 if result.wasSuccessful() else 1)
