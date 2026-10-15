"""Portable actual-data and finite Master integration checks; no game/native execution."""
import copy
import hashlib
import json
import shutil
import tempfile
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
from contract_validation import read_json
from reconcile_master import (MasterReconciliationError, active_master_refs, flatten_master,
                              master_delta, observed_scope, reconcile_master, reconcile_candidate_lanes, restore_candidate, verify_master_delta)


class MasterReconciliationTests(unittest.TestCase):
    def test_actual_candidate_exact_restoration_and_refs(self):
        root = ROOT/'translations/ko'
        raw = (root/'master.json').read_bytes()
        # One-time accepted restoration assertion, not a DLL/data-updater gate.
        self.assertEqual(hashlib.sha256(raw).hexdigest(), '4fde0c27447fd15f58415c983670fc374012117e9543141afb31837ff5152ede')
        master = read_json(root/'master.json')
        resources, bindings = read_json(root/'ui/resources.json'), read_json(root/'ui/bindings.json')
        refs = active_master_refs(resources, bindings)
        self.assertEqual(len(refs), 5)  # current candidate observation
        leaves = flatten_master(master)
        self.assertTrue(all(leaves.get(key) for key in refs))
        self.assertEqual(observed_scope(master), {'tables': 62, 'fields': 101, 'entries': 31566})
        self.assertFalse(verify_master_delta(master, master, resources=resources, bindings=bindings)['permanent_cardinality_gate'])

    def test_actual_preservation_report_and_metadata(self):
        from build_manifests import previous_inventory
        report = read_json(ROOT/'audit/NG04_MASTER_RESTORATION.json')
        self.assertEqual(report['status'], 'PASS')
        self.assertEqual(report['restored_entry_count'], 28530)
        self.assertEqual(len(report['restored_term_changes']), 2)
        self.assertEqual(report['non_target_inventory_changes'], [])
        self.assertEqual(report['novel_inventory_and_registration'], 'UNCHANGED')
        entries, _ = previous_inventory(ROOT/'translations/ko')
        self.assertEqual(entries['master.json']['sha256'], report['candidate_master_sha256'])

    def test_restore_retries_interrupted_payload_copy_with_stale_metadata(self):
        from build_manifests import build, encode, previous_inventory
        from legacy_semantic import compute, root_hash
        with tempfile.TemporaryDirectory(prefix='scratch-ng04-restore-retry-',dir=ROOT/'tests') as temporary:
            base = Path(temporary); candidate = base/'candidate'; (candidate/'ui').mkdir(parents=True); (candidate/'layout').mkdir()
            for name in ['names.json','ui/resources.json','ui/bindings.json','layout/episode-title.json']:
                (candidate/name).write_bytes((ROOT/'translations/ko'/name).read_bytes())
            old_master = {'mNg04Fixture':{'name':{'JP':'fixture old'}}}
            old_raw = encode(old_master); (candidate/'master.json').write_bytes(old_raw)
            semantic = {'master':compute(old_master),'names':compute(read_json(candidate/'names.json')),'novels':{}}
            semantic['hash'] = root_hash(semantic); (candidate/'manifest.json').write_bytes(encode(semantic))
            files = []
            for path in candidate.rglob('*.json'):
                raw = path.read_bytes(); files.append({'path':path.relative_to(candidate).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
            receipt = base/'bootstrap.json'; receipt.write_bytes(encode({'files':files})); build(candidate,receipt)
            accepted_source = ROOT/'translations/ko/master.json'; accepted_raw = accepted_source.read_bytes(); digest = hashlib.sha256(accepted_raw).hexdigest()
            # Simulate interruption after payload replacement and before builder.
            (candidate/'master.json').write_bytes(accepted_raw)
            plan = base/'plan.json'; plan.write_bytes(encode({'source':{'sha256':digest},'equivalent_available_source':{'sha256':digest},'current_candidate_master_sha256':hashlib.sha256(old_raw).hexdigest()}))
            report_file = base/'report.json'
            report = restore_candidate(candidate,accepted_source,plan,report_file)
            self.assertEqual(report['status'],'PASS')
            entries,_ = previous_inventory(candidate)
            self.assertEqual(entries['master.json']['sha256'],digest)
            self.assertEqual(read_json(candidate/'manifest.json')['master'],compute(read_json(accepted_source)))
            first_report = report_file.read_bytes()
            self.assertEqual(restore_candidate(candidate,accepted_source,plan,report_file)['status'],'NO_CHANGE')
            self.assertEqual(report_file.read_bytes(),first_report)

    def test_baseline_independent_main_master_addition_and_accepted_expansion(self):
        baseline = {'m': {'name': {'共有': '공통'}}}
        accepted = copy.deepcopy(baseline)
        accepted['m']['name']['未公開'] = '수락 확장'
        current = copy.deepcopy(baseline)
        current['m']['name']['新規'] = '독립 신규'
        current['m']['name']['共有'] = '정당 교정'
        result, report = reconcile_master(accepted, current, baseline=baseline)
        self.assertEqual(report['status'], 'PASS')
        self.assertEqual(result['m']['name'], {'共有':'정당 교정', '未公開':'수락 확장', '新規':'독립 신규'})

    def test_conflict_is_leaf_targeted_and_independent_work_survives(self):
        base = {'m': {'name': {'争点':'옛 값', '同じ':'공통'}}}
        accepted = copy.deepcopy(base); accepted['m']['name']['争点'] = '수락 교정'
        accepted['m']['name']['拡張'] = '수락 확장'
        current = copy.deepcopy(base); current['m']['name']['争点'] = 'main 교정'
        current['other'] = {'ml_name[]': {'新規':'독립 신규'}}
        result, report = reconcile_master(accepted, current, baseline=base)
        self.assertEqual(len(report['conflicts']), 1)
        self.assertEqual(report['conflicts'][0]['exact_jp'], '争点')
        self.assertEqual(result['m']['name']['争点'], '수락 교정')
        self.assertEqual(result['m']['name']['拡張'], '수락 확장')
        self.assertEqual(result['other'], current['other'])

    def test_product_first_integration_carries_current_novel_and_targets_master_conflict(self):
        from build_manifests import build, encode, previous_inventory
        with tempfile.TemporaryDirectory(prefix='scratch-ng04-reconciliation-', dir=ROOT/'tests') as temporary:
            base = Path(temporary); current = base/'current'
            shutil.copytree(ROOT/'translations/ko',current)
            baseline = read_json(current/'master.json')
            baseline['mNg04Fixture'] = {'name':{'共通':'기준'}}
            accepted = copy.deepcopy(baseline)
            accepted['mNg04Fixture']['name'].update({'共通':'수락 교정','未公開':'수락 확장'})
            main = copy.deepcopy(baseline)
            main['mNg04Fixture']['name'].update({'共通':'main 교정','独立':'신규 정당 키'})
            (current/'master.json').write_bytes(encode(main))
            semantic = read_json(current/'manifest.json')
            novel_id = next(iter(semantic['novels']))
            novel_path = 'novels/'+novel_id+'.json'
            novel = read_json(current/novel_path)
            key = next(iter(novel)); novel[key] += ' [independent current main CPU fixture]'
            novel_raw = encode(novel); (current/novel_path).write_bytes(novel_raw)
            build(current,modified=['master.json',novel_path])
            current_entries,_ = previous_inventory(current)
            current_registration = read_json(current/'manifest.json')['novels']
            output = base/'reconciled'
            report = reconcile_candidate_lanes(accepted,current,output,baseline_master=baseline)
            result = read_json(output/'master.json')['mNg04Fixture']['name']
            self.assertEqual(result, {'共通':'수락 교정','未公開':'수락 확장','独立':'신규 정당 키'})
            self.assertEqual([r['exact_jp'] for r in report['conflicts']],['共通'])
            self.assertEqual((output/novel_path).read_bytes(),novel_raw)
            self.assertEqual(read_json(output/'manifest.json')['novels'],current_registration)
            output_entries,_ = previous_inventory(output)
            for path,entry in current_entries.items():
                if path not in {'master.json','manifest.json','legacy-components.json'}:
                    self.assertEqual(output_entries[path],entry)
            self.assertEqual(report['non_master_lane_inventory_changes'],[])

    def test_no_baseline_no_blanket_main_or_old_wins(self):
        accepted = {'m':{'name':{'同じ':'수락', '未公開':'확장'}}}
        current = {'m':{'name':{'同じ':'main', '新規':'신규'}}}
        merged, report = reconcile_master(accepted,current)
        self.assertEqual(report['status'], 'PARTIAL_CONFLICTS')
        self.assertEqual(len(report['conflicts']), 1)
        self.assertEqual(merged['m']['name']['新規'],'신규')
        self.assertEqual(merged['m']['name']['未公開'],'확장')
        chosen, scoped = reconcile_master(accepted,current,accepted_authority=[('m','name','同じ')])
        self.assertEqual(scoped['status'],'PASS')
        self.assertEqual(chosen['m']['name']['同じ'],'수락')

    def test_deletion_of_accepted_leaf_is_conflict(self):
        accepted = {'m':{'name':{'JP':'KO'}}}
        merged, report = reconcile_master(accepted,{},baseline=accepted)
        self.assertEqual(merged, accepted)
        self.assertEqual(report['conflicts'][0]['reason'],'CURRENT_DELETION_OF_ACCEPTED_LEAF')

    def test_accepted_modification_and_current_deletion_are_both_changed_conflict(self):
        baseline = {'m':{'name':{'JP':'기준 KO'}}}
        accepted = {'m':{'name':{'JP':'수락 교정'}}}
        merged, report = reconcile_master(accepted,{},baseline=baseline)
        self.assertEqual(merged,accepted)
        self.assertEqual(report['status'],'PARTIAL_CONFLICTS')
        self.assertEqual(report['accepted_leaves_restored'],[])
        self.assertEqual(report['conflicts'],[{'table':'m','field':'name','exact_jp':'JP',
                                            'old_ko':'수락 교정','new_ko':None,'reason':'BOTH_CHANGED'}])

    def test_gate_rejects_unreviewed_deletion_and_term_regression(self):
        before = {'m':{'name':{'JP':'이마쥬','削除':'보존'}}}
        for after in [{'m':{'name':{'JP':'이마주','削除':'보존'}}}, {'m':{'name':{'JP':'이마쥬'}}}]:
            with self.assertRaisesRegex(MasterReconciliationError,'MASTER_UNREVIEWED'):
                verify_master_delta(before, after)

    def test_gate_allows_exact_reviewed_changed_leaf_without_fixed_counts(self):
        before = {'m':{'name':{'JP':'옛 KO'}}}
        after = {'m':{'name':{'JP':'새 KO','追加':'추가'}}}
        approval = {'table':'m','field':'name','exact_jp':'JP','old_ko':'옛 KO','new_ko':'새 KO'}
        report = verify_master_delta(before, after, approved_changes=[approval])
        self.assertEqual((report['modified'],report['added']), (1,1))
        approval['old_ko'] = '오래된 KO'
        with self.assertRaises(MasterReconciliationError):
            verify_master_delta(before, after, approved_changes=[approval])

    def test_gate_active_ref_survives_even_reviewed_removal(self):
        before = {'m':{'name':{'JP':'KO'}}}; after = {'m':{'name':{}}}
        resources = {'resources':[{'id':'r','kind':'master_ref','disposition':'translate',
                                  'master_ref':{'table':'m','field':'name','exact_jp':'JP'}}]}
        bindings = {'bindings':[{'resource_id':'r','enabled':True}]}
        with self.assertRaisesRegex(MasterReconciliationError,'MASTER_ACTIVE_REF_UNRESOLVED'):
            verify_master_delta(before, after, approved_changes=master_delta(before,after)['removed'], resources=resources,bindings=bindings)
        bindings['bindings'][0]['enabled'] = False
        self.assertEqual(verify_master_delta(before,after,approved_changes=master_delta(before,after)['removed'],resources=resources,bindings=bindings)['status'],'PASS')

    def test_translation_off_native_bridge_is_narrow_source_guard(self):
        # Config has Unity dependency; this is SOURCE coverage, not active native execution.
        source = (ROOT/'source/GCMod.Candidate/Legacy/LegacyTranslationRuntime.cs').read_text(encoding='utf-8')
        self.assertIn('!Config.Translation.Value', source)
        self.assertIn('Config.Translation.Value && snapshot.Origin', source)
        self.assertIn('Config.MasterDataTables.Value', source)
        manager = (ROOT/'source/GCMod.Candidate/Legacy/TranslationManager.cs').read_text(encoding='utf-8')
        self.assertIn('|| !Config.Translation.Value', manager)
        self.assertIn('|| !MasterDataTranslator.IsEnabled(tableName, enabled)',manager)


if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(MasterReconciliationTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    report = {'schema':'gc-ng04-master-product-tests-v1','status':'PASS' if result.wasSuccessful() else 'FAIL',
              'count':result.testsRun,'failures':[{'test':str(test),'traceback':trace} for test,trace in result.failures+result.errors],
              'execution_axes':{'actual_candidate_data_and_python_product':'EXECUTED',
                                'Translation_OFF_native_bridge':'SOURCE_INSPECTION_ONLY; managed ownership CPU is separate',
                                'native_hook':'NOT_RUN','game':'NOT_RUN'},
              'finite_merge_policy':'known common baseline or exact leaf authority; conflict keys held, independent Master and Novel work preserved',
              'tests':['A01 exact accepted Master bytes','A02 current non-target metadata/inventory preservation',
                       'A05 finite leaf and first-integration product fixture','targeted Master delta/ref gate'],
              'permanent_cardinality_gate':False}
    (ROOT/'audit/NG04_MASTER_PRODUCT_TESTS.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    raise SystemExit(0 if result.wasSuccessful() else 1)
