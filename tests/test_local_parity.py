import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from local_parity_fixture import fixture, save, sha
from update.local_parity import make_plan, apply_review, review_template, validate_translation, GUARDED
from local_release import verify_repo
from build_manifests import build
from legacy_semantic import compute, root_hash


class ReviewPipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.p=fixture(self.root)
    def tearDown(self): self.temp.cleanup()
    def plan(self, scans=()): return make_plan(self.p,self.root/'baseline.json','pinned',scans)
    def decision(self,batch):
        d=review_template(batch);d['rows'][0].update(action='translate',ko='추가',reviewer='test',evidence='JP meaning and source field')
        return d

    def test_apply_actual_export_and_manifest_preserves_novel_names(self):
        protected={p:sha(self.p/p) for p in ('names.json','novels/1.json')}
        b=self.plan();result=apply_review(self.p,b,self.decision(b))
        self.assertEqual(result['added_master'],1)
        self.assertEqual(json.loads((self.p/'ui.json').read_bytes())['追加'],'추가')
        self.assertEqual(verify_repo(self.root)['status'],'PASS')
        self.assertEqual(protected,{p:sha(self.p/p) for p in protected})
        manifest=json.loads((self.p/'manifest.json').read_bytes())
        self.assertEqual(manifest['ui'],compute(json.loads((self.p/'ui.json').read_bytes())))
        self.assertEqual(manifest['hash'],root_hash(manifest))
        with self.assertRaisesRegex(ValueError,'CHANGED_SINCE_PLAN'): apply_review(self.p,b,self.decision(b))

    def test_novel_metadata_rebuild_preserves_ui_registration(self):
        build(self.p,modified=['ui.json'])
        before=json.loads((self.p/'manifest.json').read_bytes())
        save(self.p/'novels/1.json',{'本文':'갱신된 본문'})
        build(self.p,modified=['novels/1.json'])
        after=json.loads((self.p/'manifest.json').read_bytes())
        self.assertEqual(before['ui'],after['ui'])
        self.assertEqual(before['names'],after['names'])
        self.assertNotEqual(before['novels']['1'],after['novels']['1'])
        self.assertEqual(after['hash'],root_hash(after))

    def test_partial_asset_does_not_block_good_rows_or_baseline(self):
        path=self.root/'scan.json';good={'asset_key':'scene/good.dmm','status':'MATERIALIZED','current_eligible':True,
            'raw':{},'extractor':'test','vendor_token':'v','materialized_token':'v'}
        save(path,{'metadata':{'status':'PARTIAL'},'asset_materializations':[good,{'asset_key':'scene/bad.dmm','status':'FAILED'}],
            'coverage':{'failed_assets':1},'ui':[{'jp':'画面','locator':{'asset_key':'scene/good.dmm'}},
                                               {'jp':'拒否','locator':{'asset_key':'scene/bad.dmm'}}]})
        b=self.plan([path]);self.assertEqual({r['jp'] for r in b['rows']},{'画面','追加'})
        d=review_template(b)
        for r in d['rows']:
            if r['jp']=='追加': r.update(action='translate',ko='추가',reviewer='test',evidence='current field')
        result=apply_review(self.p,b,d)
        self.assertEqual(len(result['pending']),1);self.assertEqual(result['added_master'],1)
        self.assertEqual(len(b['sources'][0]['excluded_assets']),1)

    def test_coverage_preimage_change_and_duplicate_review_fail_without_mutation(self):
        b=self.plan();d=self.decision(b);d['rows']*=2
        before=sha(self.p/'master.json')
        with self.assertRaisesRegex(ValueError,'DUPLICATE_DECISION'):apply_review(self.p,b,d)
        (self.p/'ui/local-coverage.json').write_text('{}')
        with self.assertRaisesRegex(ValueError,'CHANGED_SINCE_PLAN'):apply_review(self.p,b,self.decision(b))
        self.assertEqual(before,sha(self.p/'master.json'))

    def test_apply_failure_restores_all_published_files(self):
        b=self.plan();d=self.decision(b);before={p:(self.p/p).read_bytes() for p in GUARDED}
        import shutil
        original=shutil.copyfile
        def fail(src,dst,*a,**kw):
            if Path(dst)==self.p/'ui.json':raise OSError('injected publication failure')
            return original(src,dst,*a,**kw)
        with patch('update.local_parity.shutil.copyfile',side_effect=fail):
            with self.assertRaises(OSError):apply_review(self.p,b,d)
        self.assertEqual(before,{p:(self.p/p).read_bytes() for p in GUARDED})

    def test_hold_requires_evidence_and_pending_is_not_claimed_reviewed(self):
        b=self.plan();d=review_template(b)
        d['rows'][0].update(action='hold',reason='designer placeholder')
        with self.assertRaisesRegex(ValueError,'HOLD_REASON'):apply_review(self.p,b,d)
        d['rows'][0]['evidence']='Object named DummyText with preview-only placeholder'
        result=apply_review(self.p,b,d);self.assertEqual(len(result['holds']),1);self.assertEqual(result['added_master'],0)

    def test_layout_relaxed_but_numbers_and_variables_preserved(self):
        self.assertEqual(validate_translation('１０回\n{0}','10회 {0}'),['LAYOUT_CHANGED_REVIEW_NATIVE'])
        for ko in ('11회 {0}','10회 {1}'):
            with self.assertRaises(ValueError):validate_translation('１０回\n{0}',ko)

if __name__=='__main__':unittest.main()
