"""NG05 product command/review/work boundaries. Semantic edits are disposable.

WB completion tests here use an explicitly labelled transaction stub; the real
unmodified WB replay is separate and cannot be replaced by these tests.
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
from update.intake_work import reconcile,mark_failed,validate_apply_link,complete_after_apply
from update.novel_bridge import create_intake_references
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
        mark_failed(self.area/'src',snapshot['profile'],key,'INTERRUPTED_WB');_,third=self.queue();self.assertEqual(third['actionable'][0]['status'],'failed');self.assertEqual(third['new_work'],0)
    def test_C02_collision_all_held_restored_and_repack_no_new(self):
        _,first=self.queue('repack1');_,second=self.queue('repack2');self.assertEqual(second['new_work'],0);self.assertEqual(second['repack_same_script'],1)
        _,collision=self.queue('repack2',extra=True);self.assertEqual(len(collision['actionable']),0);self.assertEqual(collision['held'],1)
        _,restored=self.queue('repack2');self.assertEqual(len(restored['actionable']),1)
    def test_C03_completion_requires_WB_physical_and_published_apply(self):
        snapshot,queue=self.queue();work=queue['actionable'][0];ref=work['reference'];locator={'raw_textasset_sha256':ref['textasset']['sha256'],'parser':'synthetic-WB-transaction-stub','transform':{},'position_mapping':{},'parser_view_sha256':'1'*64,'source_chain_digest':'2'*64};key=fingerprint(locator)
        pin={'size':len(json.dumps(locator,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()),'sha256':key}
        snapshot['qualified_sources']={key:dict(pin,source_id=ref['source_id'],asset_key=ref['asset_key'])};snapshot['qualified_locators']={key:locator}
        from update.novel_bridge import bind_existing_wb_scene
        from update.source_scan import digest
        scene={'schema':'WB19_BOUND_SOURCE_QUALIFIED_SCENE_V1','rows':[{'source_locator':locator}]};scene_path=self.area/'synthetic-bound-scene.json';scene_path.write_bytes(encode(scene))
        bound=bind_existing_wb_scene(snapshot,scene_path,digest(scene_path.read_bytes()),ref['source_id'],ref['asset_key'],self.area/'raw')
        self.assertEqual(bound['qualified_sources'][key],snapshot['qualified_sources'][key])
        link={'state_root':str(self.area/'src'),'work_ids':[work['work_id']]};op={'review_id':'EXPLICIT_CPU_TRANSACTION_STUB','reviewed_source_refs':{key:pin},'old_ko':{'novels/99999999.json':'3'*64},'m4_plan':{'path':'synthetic-not-WB','sha256':'4'*64}}
        basis=validate_apply_link(link,snapshot,op)
        self.assertRaisesRegex(ValueError,'ACCEPTANCE_REQUIRED',complete_after_apply,link,snapshot,op,basis,self.owned,{'accepted_return_verified':False})
        complete_after_apply(link,snapshot,op,basis,self.owned,{'accepted_return_verified':True,'payloads':{'novels/99999999.json':b'{}'}})
        _,again=self.queue();self.assertEqual(len(again['actionable']),0);self.assertEqual(again['completed'],1)
        _,changed=self.queue('changed-script');self.assertEqual(changed['new_work'],1)
    def test_C04_actual_apply_completion_fault_resume_and_aborted_intent(self):
        from update.apply_review import resume_intake_completion
        from update.source_scan import digest
        source_id=next(iter(read_json(self.owned/'manifest.json')['novels']));snapshot,queue=self.queue(source_id=source_id)
        work=queue['actionable'][0];ref=work['reference'];locator={'raw_textasset_sha256':ref['textasset']['sha256'],'parser':'EXPLICIT_SYNTHETIC_TRANSACTION_STUB','transform':{},'position_mapping':{},'parser_view_sha256':'1'*64,'source_chain_digest':'2'*64};key=fingerprint(locator)
        pin={'size':len(json.dumps(locator,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()),'sha256':key}
        snapshot['qualified_sources']={key:dict(pin,source_id=source_id,asset_key=ref['asset_key'])};snapshot['qualified_locators']={key:locator}
        file='novels/'+source_id+'.json';raw=(self.owned/file).read_bytes()
        op={'review_id':'APPLY-WB-TRANSACTION-STUB','decision':'APPROVE','operation':'accepted_return','workbench_root':'EXPLICIT_STUB_NOT_REAL_WB','implementation_sha256':'5'*64,'m4_plan':{'path':'STUB','sha256':'6'*64},'reviewed_source_refs':{key:pin},'old_ko':{file:digest(raw)},'intake_work':{'state_root':str(self.area/'src'),'work_ids':[work['work_id']]}}
        review={'schema':'gc-ng03-reviewed-change-set-v1','novel_returns':[op]};returned={'payloads':{file:raw},'accepted_return_verified':True}
        # NO_CHANGE return with abort before authoring CAS cannot complete work.
        apply_review(self.owned,{'schema':'gc-ng03-reviewed-change-set-v1'},{},None,self.state)
        def abort(point):
            if point=='before_authoring_select':raise OSError('SYNTHETIC_ABORT')
        with patch('update.novel_bridge.validate_accepted_return',return_value=returned):
            self.assertRaisesRegex(OSError,'SYNTHETIC_ABORT',apply_review,self.owned,review,snapshot,None,self.state,fault=abort)
        self.assertEqual(resume_intake_completion(self.state)['results'],[])
        review['rows']=self.ui()['rows']
        with patch('update.novel_bridge.validate_accepted_return',return_value=returned),patch('update.intake_work.complete_after_apply',side_effect=OSError('SYNTHETIC_LATE_STATE_WRITE_FAILURE')):
            result=apply_review(self.owned,review,snapshot,None,self.state)
        self.assertEqual(result['status'],'PASS');self.assertEqual(result['intake_completion'][0]['status'],'HELD_COMPLETION')
        # An unrelated subsequent authoring generation cannot lose completion of
        # the already committed WB apply; an aborted sibling cannot authorize it.
        current=Path(result['next_package']);second=plan_ui_edit(current,self.resource['id'],'CPU later correction')['review'];second['rows'][0]['decision']='APPROVE'
        apply_review(current,second,{},None,self.state)
        self.assertEqual(resume_intake_completion(self.state)['results'][0]['status'],'COMPLETED')
        _,again=self.queue(source_id=source_id);self.assertEqual(again['actionable'],[])

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(OperatorTests))
    report={'schema':'gc-ng05-operator-product-tests-v1','status':'PASS' if result.wasSuccessful() else 'FAIL','count':result.testsRun,'failures':len(result.failures)+len(result.errors),'semantic_changes':'DISPOSABLE_FIXTURES_ONLY','WB_queue_completion':'SYNTHETIC_TRANSACTION_STUB_NOT_REAL_WB','native':'NOT_RUN','game':'NOT_RUN'}
    (ROOT/'tests/NG05_OPERATOR_TEST_RESULTS.json').write_bytes(encode(report));raise SystemExit(0 if result.wasSuccessful() else 1)
