"""Product builder/review/apply tests; fixtures are never semantic acceptance."""
from __future__ import annotations
import copy,json,shutil,sys,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from build_manifests import build,encode,previous_inventory
from contract_validation import read_json,ContractError
from legacy_semantic import compute,root_hash
from update.apply_review import freeze_input,apply_review
from update.diff_review import compare,fingerprint,source_fingerprint,apply_row,ReviewConflict,validate_binding_provenance

PROFILE={'platform':'cpu-fixture','channel':'offline','partition':'ui','inventory_contract':'ng03-test-source-v1'}
def snapshot(row,lane='ui'):
    return {'schema':'gc-ng03-source-snapshot-v1','profile':PROFILE,'master':[row] if lane=='master' else [],'ui':[row] if lane=='ui' else [],'novel':[]}
def first_leaf(doc,path=()):
    for key,value in sorted(doc.items()):
        if isinstance(value,dict):
            found=first_leaf(value,path+(key,))
            if found:return found
        elif isinstance(value,str) and value:return path+(key,),value
def set_leaf(doc,keys,value):
    for key in keys[:-1]:doc=doc[key]
    doc[keys[-1]]=value

class ApplyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary=tempfile.TemporaryDirectory(prefix='scratch-ng03-apply-',dir=ROOT/'tests');cls.base=Path(cls.temporary.name)
        cls.owned,cls.cold=freeze_input(ROOT/'translations/ko',cls.base/'state')
        cls.resources=read_json(cls.owned/'ui/resources.json')
        cls.resource=next(item for item in cls.resources['resources'] if item['kind']=='literal' and item['disposition']=='translate' and item.get('ko'))
        cls.row={'locator':{'resource':cls.resource['id'],'binding':'source-slot','owner':'cpu-owner','slot':'cpu-slot','role':cls.resource['role']},'jp':cls.resource['jp'],'context':{'scope':'test-only'},'structure':{'kind':cls.resource['kind']}}
        cls.source=snapshot(cls.row)
    @classmethod
    def tearDownClass(cls):cls.temporary.cleanup()
    def setUp(self):
        # Independent regression cases are separate authoring lineages. A stale
        # base must not silently replace a successful NG04 next generation.
        self.state=self.base/'state'
        # Reset only this disposable test's authoring reference; keep the once
        # cold-verified baseline and prior immutable generations intact.
        (self.state/'authoring-current.json').unlink(missing_ok=True)
    def review(self,value=None):
        value = value if value is not None else self.resource['ko']+' 교정'
        return {'schema':'gc-ng03-reviewed-change-set-v1','rows':[{'review_id':'UI1','lane':'ui','locator':self.row['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',self.row),'target':{'file':'ui/resources.json','resource_id':self.resource['id']},'old_ko_sha256':fingerprint(self.resource['ko']),'decision':'APPROVE','new_ko':value}]}
    def output(self):return self.base/('candidate-'+next(tempfile._get_candidate_names()))
    def test_D01_repack_without_semantic_review(self):
        old=copy.deepcopy(self.source);old['ui'][0]['raw_container']='old'
        new=copy.deepcopy(self.source);new['ui'][0]['raw_container']='new'
        self.assertEqual('REPACK_ONLY',compare(old,new)['changes'][0]['classification'])
    def test_D02_same_jp_role_context_structure_change(self):
        for key,value in [('context',{'scope':'new'}),('structure',{'kind':'template'}),('locator',{**self.row['locator'],'role':'different'})]:
            changed=copy.deepcopy(self.source);changed['ui'][0][key]=value
            self.assertFalse(all(row['classification']=='NO_CHANGE' for row in compare(self.source,changed)['changes']))
    def test_D03_hold_keep_known_residual_are_not_applied(self):
        review=self.review()
        for state in ('HOLD','KEEP_SOURCE','KNOWN_RESIDUAL'):
            row=copy.deepcopy(review['rows'][0]);row['decision']=state;row['review_id']=state;review['rows'].append(row)
        review['rows']=review['rows'][1:]
        result=apply_review(self.owned,review,self.source,self.output(),self.state)
        self.assertEqual([],result['changed_payloads']);self.assertEqual(3,len(result['held_rows']))
    def test_D04_stale_source_row_only_does_not_reject_independent_row(self):
        review=self.review();bad=copy.deepcopy(review['rows'][0]);bad['review_id']='STALE';bad['source_fingerprint']='0'*64;review['rows'].insert(0,bad)
        result=apply_review(self.owned,review,self.source,self.output(),self.state)
        self.assertEqual('PARTIAL_CONFLICTS',result['status']);self.assertEqual(['ui/resources.json'],result['changed_payloads']);self.assertIn('STALE_SOURCE',result['rejected_rows'][0]['reason'])
    def test_D05_concurrent_ko_never_overwritten(self):
        review=self.review();review['rows'][0]['old_ko_sha256']=fingerprint('earlier correction')
        result=apply_review(self.owned,review,self.source,self.output(),self.state)
        self.assertEqual([],result['changed_payloads']);self.assertEqual('CONCURRENT_KO_CHANGE',result['rejected_rows'][0]['reason'])
    def test_D06_interrupted_transaction_has_no_candidate(self):
        before=(self.owned/'release-manifest.json').read_bytes()
        for point in ('after_payload','after_metadata','before_publish'):
            target=self.output()
            def failure(at):
                if at==point:raise OSError('injected interruption')
            with self.assertRaises(OSError):apply_review(self.owned,self.review(),self.source,target,self.state,fault=failure)
            self.assertFalse(target.exists());self.assertEqual(before,(self.owned/'release-manifest.json').read_bytes())
        self.assertFalse(list(self.base.glob('.owned-apply-*')))
    def test_D07_actual_unchanged_novel_reads_zero(self):
        reads=[];original=Path.read_bytes
        def counted(path):
            if path.parent.name=='novels':reads.append(str(path))
            return original(path)
        with patch.object(Path,'read_bytes',counted):result=apply_review(self.owned,self.review(),self.source,self.output(),self.state)
        self.assertEqual([],reads);self.assertEqual(0,result['apply_metrics']['unchanged_novel_payload_reads']);self.assertEqual('verified_owned_immutable_inventory',result['import_metrics']['mode'])
    def test_stale_novel_return_preserves_independent_ui(self):
        review=self.review();review['novel_returns']=[{'review_id':'STALE_NOVEL'}]
        result=apply_review(self.owned,review,self.source,self.output(),self.state)
        self.assertEqual('PARTIAL_CONFLICTS',result['status']);self.assertEqual(['ui/resources.json'],result['changed_payloads'])
        self.assertEqual('NOVEL_BRIDGE_INPUT_REQUIRED',result['rejected_rows'][0]['reason'])
    def test_reviewed_supported_binding_addition(self):
        bindings=read_json(self.owned/'ui/bindings.json')
        proposal=copy.deepcopy(next(row for row in bindings['bindings'] if row.get('enabled') and row['adapter_id']=='battle.caption.v1'))
        proposal['id']='binding.cpu-reviewed-addition';proposal['selector']['slot_key']='cpu-reviewed-new-slot'
        proposal['selector']['segments']=['CpuReviewedTarget'];proposal['source_identity']['identity']='cpu-reviewed-current-model'
        resource=next(row for row in self.resources['resources'] if row['id']==proposal['resource_id'])
        source={'locator':{'resource':resource['id'],'binding':proposal['id'],'owner':proposal['selector']['scope_key'],'slot':proposal['selector']['slot_key'],'role':resource['role']},'jp':resource['jp'],
                'context':{'scope':'cpu-only-supported-adapter'},'structure':{'adapter_id':proposal['adapter_id'],'selector':proposal['selector'],'source_identity':proposal['source_identity']}}
        review={'schema':'gc-ng03-reviewed-change-set-v1','binding_rows':[{'review_id':'ADD','lane':'ui','locator':source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',source),
                   'decision':'APPROVE','proposal':proposal,'old_binding_sha256':fingerprint(None)}]}
        target=self.output();result=apply_review(self.owned,review,snapshot(source),target,self.state)
        self.assertEqual('PASS',result['status']);self.assertIn('ui/bindings.json',result['changed_payloads'])
        self.assertTrue(any(row['id']==proposal['id'] for row in read_json(target/'ui/bindings.json')['bindings']))
    def test_external_same_size_timestamp_mutation_is_cold_rejected(self):
        target=self.output();shutil.copytree(self.owned,target,copy_function=shutil.copyfile);file=target/'novels/10011201.json'
        data=file.read_bytes();file.write_bytes(data.replace(b'"',b' ',1))
        with self.assertRaisesRegex(ValueError,'PAYLOAD_MISMATCH'):freeze_input(target,self.state)
    def test_O03_O04_O05_O06_semantic_coupling_changed_only(self):
        manifest=read_json(self.owned/'manifest.json');unregistered=next(path for path in (self.owned/'novels').glob('*.json') if path.stem not in manifest['novels'])
        allcases=[]
        for case,file in [('master','master.json'),('names','names.json'),('registered','novels/10011201.json'),('unregistered','novels/'+unregistered.name)]:
            target=self.output();shutil.copytree(self.owned,target,copy_function=shutil.copyfile);doc=read_json(target/file);keys,old=first_leaf(doc);new=old+' [CPU_REVIEW_FIXTURE]';set_leaf(doc,keys,new);(target/file).write_bytes(encode(doc))
            reads=[];original=Path.read_bytes
            def counted(path):
                if path.parent.name=='novels':reads.append(path.name)
                return original(path)
            with patch.object(Path,'read_bytes',counted):build(target,modified=[file])
            updated=read_json(target/'manifest.json');self.assertEqual(root_hash(updated),updated['hash']);self.assertEqual(set(manifest['novels']),set(updated['novels']))
            if case=='unregistered':self.assertEqual(manifest,updated)
            else:self.assertEqual(compute(doc),updated[case] if case in ('master','names') else updated['novels']['10011201'])
            self.assertTrue(all(name==Path(file).name for name in reads))
            previous_inventory(target)
            allcases.append({'case':case,'root':str(target),'file':file,'keys':list(keys),'new_ko':new,'registered_count':len(updated['novels'])})
        (ROOT/'tests/NG03_SEMANTIC_FIXTURES.json').write_bytes(encode({'cases':allcases,'fixture_only':True}))
        # Keep only these generated CPU inputs until the actual DLL store reader
        # runs. They are reproducible and excluded from the returned ZIP.
        preserve=ROOT/'tests/fixtures-ng03-semantic';preserve.mkdir(exist_ok=True)
        for case in allcases:
            dest=preserve/case['case']
            if dest.exists():shutil.rmtree(dest)
            shutil.copytree(case['root'],dest,copy_function=shutil.copyfile);case['root']=str(dest)
        (ROOT/'tests/NG03_SEMANTIC_FIXTURES.json').write_bytes(encode({'cases':allcases,'fixture_only':True}))

class BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.resource={'id':'r','jp':'共通','ko':'공통','kind':'literal','role':'download','disposition':'translate'}
        self.source={'locator':{'resource':'r','binding':'b','owner':'download','slot':'title','role':'download'},'jp':'共通','structure':{'kind':'literal'}}
        self.review={'lane':'ui','locator':self.source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',self.source),'target':{'file':'ui/resources.json','resource_id':'r'},'old_ko_sha256':fingerprint('공통'),'decision':'APPROVE','new_ko':'교정'}
    def test_runtime_target_role_changed_rejected(self):
        self.resource['role']='annotation_heading'
        with self.assertRaisesRegex(ReviewConflict,'ROLE_STRUCTURE_CHANGED'):apply_row({'resources':[self.resource]},self.review,snapshot(self.source))
    def test_runtime_target_kind_changed_rejected(self):
        self.resource['kind']='template_label'
        with self.assertRaisesRegex(ReviewConflict,'ROLE_STRUCTURE_CHANGED'):apply_row({'resources':[self.resource]},self.review,snapshot(self.source))
    def test_master_body_cannot_write_names(self):
        source={'locator':{'table':'mItems','record':'1','field':'ml_description[]'},'jp':'共通','context':{'role':'dictionary_body'}}
        review={**self.review,'lane':'master','locator':source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'master',source),'target':{'file':'names.json','keys':['共通']}}
        with self.assertRaisesRegex(ReviewConflict,'NAMES_QUALIFIED_SOURCE_ADAPTER_REQUIRED'):apply_row({'共通':'공통'},review,snapshot(source,'master'))
    def test_binding_same_jp_different_owner_slot_rejected(self):
        proposal={'id':'b','resource_id':'r','adapter_id':'chrome.caption.v1','selector':{'scope_key':'download','slot_key':'title'},'source_identity':{'exact_jp':'共通','identity':'current','kind':'current-model'}}
        self.source['structure']={'adapter_id':proposal['adapter_id'],'selector':copy.deepcopy(proposal['selector']),'source_identity':copy.deepcopy(proposal['source_identity'])}
        validate_binding_provenance(self.source,proposal,self.resource)
        for field in ('scope_key','slot_key'):
            changed=copy.deepcopy(proposal);changed['selector'][field]='unrelated'
            with self.assertRaisesRegex(ReviewConflict,'OWNER_SLOT_ROLE_MISMATCH'):validate_binding_provenance(self.source,changed,self.resource)
    def test_binding_same_owner_other_callback_rejected(self):
        proposal={'id':'b','resource_id':'r','adapter_id':'chrome.caption.v1','selector':{'scope_key':'download','slot_key':'title'},'source_identity':{'exact_jp':'共通'}}
        self.source['structure']={'adapter_id':'consumers.caption.v1','selector':proposal['selector'],'source_identity':proposal['source_identity']}
        with self.assertRaisesRegex(ReviewConflict,'QUALIFIED_BINDING_STRUCTURE_REQUIRED'):validate_binding_provenance(self.source,proposal,self.resource)
    def test_known_physical_master_array_mapping(self):
        source={'locator':{'table':'mUnitWords','record':'101','field':'ml_word'},'jp':'共通',
                'translation_target':{'table':'mUnitWords','field':'ml_word[]'},
                'structure':{'physical_field':'ml_word[0]','mapping_contract':'upstream-master-ml-word-zero-to-runtime-array-v1'}}
        review={**self.review,'lane':'master','locator':source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'master',source),
                'target':{'file':'master.json','keys':['mUnitWords','ml_word[]','共通']}}
        doc={'mUnitWords':{'ml_word[]':{'共通':'공통'}}}
        self.assertTrue(apply_row(doc,review,snapshot(source,'master')))
        self.assertEqual('교정',doc['mUnitWords']['ml_word[]']['共通'])
        changed=copy.deepcopy(source);changed['translation_target']['field']='ml_description[]'
        with self.assertRaisesRegex(ReviewConflict,'STALE_SOURCE'):apply_row(doc,review,snapshot(changed,'master'))

if __name__=='__main__':
    suite=unittest.TestSuite([unittest.defaultTestLoader.loadTestsFromTestCase(ApplyTests),unittest.defaultTestLoader.loadTestsFromTestCase(BoundaryTests)])
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    report={'schema':'gc-ng03-product-apply-tests-v1','status':'PASS' if result.wasSuccessful() else 'FAIL','count':result.testsRun,'failures':len(result.failures)+len(result.errors),'native':'NOT_RUN','game':'NOT_RUN'}
    (ROOT/'tests/NG03_APPLY_TEST_RESULTS.json').write_bytes(encode(report));raise SystemExit(0 if result.wasSuccessful() else 1)

