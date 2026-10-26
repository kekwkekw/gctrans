"""NG05 product command/review/work boundaries. Semantic edits are disposable.

Source work tests cover pending, retry, collision and eligibility restoration.
PRO-reviewed completion is covered by the current Novel integration tests.
"""
import copy,json,subprocess,sys,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'));sys.path.insert(0,str(ROOT/'tests'))
from contract_validation import read_json
from build_manifests import encode
from update.diff_review import fingerprint,source_snapshot_input,compare,source_fingerprint,apply_row
from update.apply_review import apply_review,freeze_input,validate_review
from update.operator_review import plan_ui_edit,plan_master_key,apply_package_ui,apply_master_key
from update.source_work import reconcile,mark_failed
from update.novel_intake import create_intake_references
from update.source_scan import SourceProfile,scan_inventory
from ng04_apply_tests import small_package,ui_inputs,source,ActualIO

class Extractor:
    version='NG05_SYNTHETIC_CPU_v1'
    def __call__(self,raw,asset):return {'textassets':[{'name':'script','path_id':1,'container':'synthetic','bytes':b'same-script' if raw.startswith(b'repack') else raw}],'selected':0}

class OperatorTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=ROOT/'tests');self.area=Path(self.temp.name)
        self.pkg=small_package(self.area/'external');self.state=self.area/'author';self.owned,_=freeze_input(self.pkg,self.state)
        self.resource,_=ui_inputs(self.owned)
    def tearDown(self):
        from update.apply_review import discard
        discard(self.area);self.temp.cleanup()
    def ui(self,new='CPU correction'):
        plan=plan_ui_edit(self.owned,self.resource['id'],new);op=plan['review']['rows'][0];op['decision']='APPROVE';return plan['review']
    def test_A01_scan_envelope_bare_and_typed_errors(self):
        bare=source();env={'state':{'profile':bare['profile']},'metrics':{},'sourceSnapshot':bare}
        self.assertEqual(source_snapshot_input(bare),source_snapshot_input(env))
        for bad,reason in [(dict(env,schema='x'),'AMBIGUOUS'),(dict(env,state={'profile':{}}),'PROFILE_MISMATCH'),([], 'CONTRACT_REQUIRED')]:
            with self.assertRaisesRegex(ValueError,reason):source_snapshot_input(bad)
        a=self.area/'a.json';b=self.area/'b.json';report=self.area/'diff.json';a.write_bytes(encode(env));b.write_bytes(encode(bare))
        cmd=[sys.executable,'-X','utf8',str(ROOT/'tools/update/cli.py'),'diff','--previous',str(a),'--current',str(b),'--report',str(report)]
        self.assertEqual(subprocess.run(cmd,capture_output=True).returncode,0)
        b.write_bytes(encode(dict(env,schema='bad')));self.assertEqual(subprocess.run(cmd,capture_output=True).returncode,2);self.assertEqual(read_json(report)['status'],'REJECTED_SOURCE_INPUT')
    def test_A02_unknown_scan_nonzero_valid_metadata_plan_zero(self):
        cmd=[sys.executable,'-X','utf8',str(ROOT/'tools/update/cli.py'),'scan','--platform','PC','--state-root',str(self.area/'source-state'),'--raw-root',str(self.area/'raw'),'--output',str(self.area/'scan.json')]
        result=subprocess.run(cmd,capture_output=True,text=True);self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(json.loads(result.stdout)['operation_status'],'OBSERVATION_UNAVAILABLE')
        asset=self.area/'asset';asset.write_bytes(b'fixture');cmd+=['--local-asset',str(asset),'--asset-key','notinit/novel_script/sts_99999999.dmm']
        result=subprocess.run(cmd,capture_output=True,text=True);self.assertEqual(result.returncode,0,result.stderr);self.assertEqual(json.loads(result.stdout)['operation_status'],'METADATA_ONLY_PLAN')
        self.assertEqual(read_json(self.area/'scan.json')['metrics']['source_payloads_acquired'],0)
    def test_A03_draft_hold_and_explicit_ko_without_raw(self):
        draft=plan_ui_edit(self.owned,self.resource['id'],'CPU fixture')
        self.assertEqual(draft['source_basis'],'package_target');self.assertEqual(draft['review']['rows'][0]['decision'],'HOLD')
        held=apply_review(self.owned,draft['review'],{},None,self.state);self.assertEqual(held['status'],'HELD');self.assertEqual(held['next_package'],str(self.owned))
        result=apply_review(self.owned,self.ui(),{},None,self.state);self.assertEqual(result['status'],'PASS');self.assertEqual(result['apply_metrics']['unchanged_novel_payload_reads'],0)
    def test_A04_own_source_binding_ko_stale_hold_unrelated_allowed(self):
        review=self.ui();op=review['rows'][0];resources=read_json(self.owned/'ui/resources.json');bindings=read_json(self.owned/'ui/bindings.json')
        unrelated=next(r for r in resources['resources'] if r['id']!=self.resource['id']);unrelated['ko']='unrelated';self.assertTrue(apply_package_ui(resources,bindings,op))
        for axis in ('ko','jp','binding'):
            resources=read_json(self.owned/'ui/resources.json');bindings=read_json(self.owned/'ui/bindings.json')
            target=next(r for r in resources['resources'] if r['id']==self.resource['id'])
            if axis=='binding':bindings['bindings'].append({'id':'added','resource_id':target['id'],'enabled':False})
            else:target[axis]+='changed'
            with self.assertRaisesRegex(ValueError,'CONCURRENT_KO|STALE_PACKAGE_TARGET'):apply_package_ui(resources,bindings,op)
    def test_A05_protected_and_unknown_basis_no_bypass(self):
        resources=read_json(self.owned/'ui/resources.json')
        for resource in resources['resources']:
            if resource['kind']=='master_ref' or resource['disposition']!='translate':
                with self.assertRaisesRegex(ValueError,'PROTECTED'):plan_ui_edit(self.owned,resource['id'],'copy')
        review=self.ui();review['rows'][0]['source_basis']['kind']='observed_source';self.assertRaisesRegex(ValueError,'PACKAGE_TARGET',validate_review,review)
        review=self.ui();review['rows'][0]['unexpected']=[];self.assertRaisesRegex(ValueError,'CONTRACT',validate_review,review)
    def master(self):
        doc=read_json(self.owned/'master.json');table=next(iter(doc));field=next(iter(doc[table]));jp=next(iter(doc[table][field]));keys=[table,field,jp]
        rows=[{'locator':{'table':table,'field':field,'id':i},'jp':jp,'context':{'record':i},'structure':{'field_type':'string'}} for i in ('record-1','record-2')]
        snap=source();snap['master']=rows;return doc,keys,snap
    def test_B01_full_key_explicit_scope_one_write(self):
        doc,keys,snap=self.master();plan=plan_master_key(self.owned,snap,keys,'CPU whole-key');op=plan['review']['master_key_rows'][0];op['decision']='APPROVE'
        self.assertEqual(len(op['source_basis']['peers']),2);self.assertTrue(op['all_uses_same_ko']);self.assertTrue(apply_master_key(doc,snap,op));self.assertEqual(doc[keys[0]][keys[1]][keys[2]],'CPU whole-key')
        result=apply_review(self.owned,plan['review'],snap,None,self.state);self.assertEqual(result['status'],'PASS');self.assertEqual(result['changed_payloads'],['master.json'])
    def test_B02_single_record_refusal_preserved_and_duplicates_held(self):
        doc,keys,snap=self.master();row=snap['master'][0]
        op={'decision':'APPROVE','lane':'master','locator':row['locator'],'source_fingerprint':source_fingerprint(snap['profile'],'master',row),'target':{'file':'master.json','keys':keys},'old_ko_sha256':fingerprint(doc[keys[0]][keys[1]][keys[2]]),'new_ko':'x'}
        self.assertRaisesRegex(ValueError,'SHARED_MASTER',apply_row,doc,op,snap)
        plan=plan_master_key(self.owned,snap,keys,'x')['review'];plan['master_key_rows'][0]['decision']='APPROVE';plan['master_key_rows'].append(copy.deepcopy(plan['master_key_rows'][0]));plan['rows']=self.ui()['rows']
        result=apply_review(self.owned,plan,snap,None,self.state);self.assertEqual(len(result['held_operations']),2);self.assertIn('ui/resources.json',result['changed_payloads']);self.assertNotIn('master.json',result['changed_payloads'])
    def test_B03_new_peer_context_or_old_ko_hold_key_only(self):
        doc,keys,snap=self.master();plan=plan_master_key(self.owned,snap,keys,'x')['review'];op=plan['master_key_rows'][0];op['decision']='APPROVE'
        for change in ('peer','context','ko'):
            current=copy.deepcopy(snap);copydoc=copy.deepcopy(doc)
            if change=='peer':current['master'].append(dict(current['master'][0],locator=dict(current['master'][0]['locator'],id='new')))
            elif change=='context':current['master'][0]['context']={'changed':True}
            else:copydoc[keys[0]][keys[1]][keys[2]]='other'
            self.assertRaisesRegex(ValueError,'STALE_MASTER|CONCURRENT_KO',apply_master_key,copydoc,current,op)
    def queue(self,token='one',fetch=None,extra=False,source_id='99999999'):
        profile=SourceProfile('WebGL','R18','Novel','vendor-d-n-h-s-v1');inv={'d':[{'n':f'notinit/novel_script/sts_{source_id}.dmm','h':token,'s':100}]}
        if extra:inv['d'].append({'n':f'notinit/other/sts_{source_id}.dmm','h':'other','s':100})
        result=scan_inventory(profile,inv,state_root=self.area/'src',raw_root=self.area/'raw',fetch_asset=fetch or (lambda item:item['vendor_token'].encode()),extractor=Extractor())
        snapshot=result['sourceSnapshot'];catalog=create_intake_references(snapshot,self.area/'catalog.json');return snapshot,reconcile(snapshot,catalog,self.area/'src')
    def test_C01_first_pending_second_no_new_and_failed_retry(self):
        snapshot,first=self.queue();self.assertEqual(first['new_work'],1);key=first['actionable'][0]['work_id']
        _,second=self.queue();self.assertEqual(second['new_work'],0);self.assertEqual(len(second['actionable']),1)
        mark_failed(self.area/'src',snapshot['profile'],key,'DRAFT_INTERRUPTED');_,third=self.queue();self.assertEqual(third['actionable'][0]['status'],'failed');self.assertEqual(third['new_work'],0)
    def test_C02_collision_all_held_restored_and_repack_no_new(self):
        _,first=self.queue('repack1');_,second=self.queue('repack2');self.assertEqual(second['new_work'],0);self.assertEqual(second['repack_same_script'],1)
        _,collision=self.queue('repack2',extra=True);self.assertEqual(len(collision['actionable']),0);self.assertEqual(collision['held'],1)
        _,restored=self.queue('repack2');self.assertEqual(len(restored['actionable']),1)

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(OperatorTests))
    report={'schema':'gc-ng05-operator-product-tests-v1','status':'PASS' if result.wasSuccessful() else 'FAIL','count':result.testsRun,'failures':len(result.failures)+len(result.errors),'semantic_changes':'DISPOSABLE_FIXTURES_ONLY','source_work':'PENDING_RETRY_COLLISION_RESTORATION','native':'NOT_RUN','game':'NOT_RUN'}
    (ROOT/'tests/NG05_OPERATOR_TEST_RESULTS.json').write_bytes(encode(report));raise SystemExit(0 if result.wasSuccessful() else 1)
