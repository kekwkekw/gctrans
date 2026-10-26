"""Current UI/Master, registration and transaction regressions.

Default runs portable fixtures and generates the full candidate continuous chain
for the actual DLL CPU reader. Novel regressions use the current Novel workflow tests.
"""
from __future__ import annotations
import argparse, copy, hashlib, json, os, re, shutil, subprocess, sys, tempfile, time, unittest
from types import SimpleNamespace
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1]; sys.path.insert(0,str(ROOT/'tools'))
from build_manifests import build, encode, sha, previous_inventory
from contract_validation import read_json
from legacy_semantic import root_hash
from update.apply_review import apply_review, freeze_input, discard, validate_review
from update.diff_review import fingerprint, source_fingerprint, apply_row

PROFILE={'platform':'cpu-fixture','channel':'offline','partition':'ui','inventory_contract':'ng04-test-v1'}
def source(*rows): return {'schema':'gc-ng03-source-snapshot-v1','profile':PROFILE,'master':[],'ui':list(rows),'novel':[]}
def ui_inputs(package):
    resource=next(r for r in read_json(package/'ui/resources.json')['resources'] if r['kind']=='literal' and r['disposition']=='translate' and r.get('ko'))
    row={'locator':{'resource':resource['id'],'binding':'cpu-source','owner':'cpu-owner','slot':'cpu-slot','role':resource['role']},'jp':resource['jp'],'context':{'scope':'CPU fixture only'},'structure':{'kind':resource['kind']}}
    return resource,row
def correction(resource,row,old,new):
    return {'schema':'gc-ng03-reviewed-change-set-v1','rows':[{'review_id':'UI','lane':'ui','locator':row['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',row),'decision':'APPROVE',
            'target':{'file':'ui/resources.json','resource_id':resource['id']},'old_ko_sha256':fingerprint(old),'new_ko':new}]}
def layout_review(package,proposal=None):
    old=read_json(package/'layout/episode-title.json')
    return {'schema':'gc-ng03-reviewed-change-set-v1','layout_rows':[{'review_id':'LAYOUT','decision':'APPROVE','operation':'replace_typed_policy',
            'target':{'file':'layout/episode-title.json','policy_id':'episode-title.fit.v1'},'old_layout_sha256':fingerprint(old),'proposal':proposal if proposal is not None else old}]}

def small_package(destination):
    """Real UI/Master contract with three disposable Novel files, fresh manifests."""
    destination.mkdir(parents=True); baseline=ROOT/'translations/ko'; manifest=read_json(baseline/'manifest.json')
    registered=sorted(manifest['novels'])[:2]
    unregistered=next(p.stem for p in (baseline/'novels').glob('*.json') if p.stem not in manifest['novels'])
    files=['master.json','names.json','manifest.json','ui/resources.json','ui/bindings.json','layout/episode-title.json']+['novels/'+i+'.json' for i in registered+[unregistered]]
    for file in files:
        target=destination/file; target.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(baseline/file,target)
    manifest['novels']={i:manifest['novels'][i] for i in registered}; manifest['hash']=root_hash(manifest); (destination/'manifest.json').write_bytes(encode(manifest))
    receipt=destination.parent/'bootstrap.json'; receipt.write_bytes(encode({'files':[{'path':file,'bytes':len((destination/file).read_bytes()),'sha256':sha((destination/file).read_bytes())} for file in files]}))
    build(destination,receipt); return destination

class ActualIO:
    def __init__(self,novel_bytes): self.novel_bytes=novel_bytes; self.reads=[]; self.hashes=0
    def __enter__(self):
        self.old_read=Path.read_bytes; self.old_hash=hashlib.sha256
        def read(path):
            if path.parent.name=='novels': self.reads.append(str(path))
            return self.old_read(path)
        def hashed(raw=b'',*args,**kwargs):
            if isinstance(raw,bytes) and raw in self.novel_bytes: self.hashes+=1
            return self.old_hash(raw,*args,**kwargs)
        self.patches=[patch.object(Path,'read_bytes',read),patch.object(hashlib,'sha256',hashed)]
        for item in self.patches: item.start()
        return self
    def __exit__(self,*unused):
        for item in reversed(self.patches): item.stop()
    def report(self): return {'novel_read_calls':len(self.reads),'novel_hash_calls':self.hashes,'method':'actual Path.read_bytes and hashlib.sha256 payload bytes; OS copy excluded'}

def generate_chain(destination,report_path):
    if destination.exists(): discard(destination)
    destination.mkdir(parents=True); external=destination/'external-baseline'
    shutil.copytree(ROOT/'translations/ko',external,copy_function=shutil.copyfile)
    policy=read_json(external/'layout/episode-title.json')
    # Disposable formatting-only layout correction: all const policy values stay.
    (external/'layout/episode-title.json').write_bytes(json.dumps(policy,ensure_ascii=False,separators=(',',':')).encode())
    fixture_bindings=read_json(external/'ui/bindings.json')
    original_binding=next(b for b in fixture_bindings['bindings'] if b['enabled'] and b['adapter_id']=='battle.caption.v1')
    binding_template=copy.deepcopy(original_binding)
    # Keep the compiled route/SourceIdentity intact. Only the disposable baseline
    # leaves this existing route unbound so the reviewed addition is executable.
    routes=[re.findall(r'"([^"]*)"',line) for line in (ROOT/'source/GCMod.Candidate/Native/BattleRoutes.cs').read_text(encoding='utf-8-sig').splitlines() if line.startswith('new BattleRoute(')]
    route_values=[r for r in routes if r[0]==binding_template['source_identity']['identity']]
    assert len(route_values)==1,'Binding fixture requires exactly one existing compiled BattleRoute'
    route_values=route_values[0]
    binding_route=dict(zip(('source_id','slot','owner','selector','callback','surface'),route_values[:6])); binding_route['segments']=route_values[6:]
    selector=binding_template['selector']
    assert selector['scope_key']=='battle-'+binding_route['surface'].lower() and selector['slot_key']==binding_route['slot'] and selector['segments']==binding_route['segments'] and selector['lifetime']=='owner'
    def matches_route(binding):
        return binding['enabled'] and binding['adapter_id']=='battle.caption.v1' and binding['source_identity']['identity']==binding_route['source_id'] and binding['selector']['scope_key']==selector['scope_key'] and binding['selector']['slot_key']==selector['slot_key'] and binding['selector'].get('segments',[])==selector['segments']
    assert sum(matches_route(b) for b in fixture_bindings['bindings'])==1,'Existing route fixture must not duplicate an enabled native binding'
    fixture_bindings['bindings'].remove(original_binding)
    (external/'ui/bindings.json').write_bytes(encode(fixture_bindings))
    build(external,modified=['layout/episode-title.json','ui/bindings.json'])
    raw_novels={p.read_bytes() for p in (external/'novels').glob('*.json')}
    state=destination/'state'
    with ActualIO(raw_novels) as cold_io: owned,cold=freeze_input(external,state)
    resource,row=ui_inputs(owned); first=resource['ko']+' 교정1'; second=resource['ko']+' 교정2'
    stages=[]; current=owned
    def stage(id,review,current_source,ko):
        nonlocal current
        with ActualIO(raw_novels) as io:
            result=apply_review(current,review,current_source,destination/('export-'+id),state)
        assert result['status']=='PASS',result
        assert io.report()['novel_read_calls']==0 and io.report()['novel_hash_calls']==0,io.report()
        current=Path(result['next_package']); actual=next(r for r in read_json(current/'ui/resources.json')['resources'] if r['id']==resource['id'])
        assert actual['ko']==ko
        stages.append({'id':id,'root':str(current),'export_root':result['output'],'resource_id':resource['id'],'expected_ko':ko,'import_metrics':result['import_metrics'],'apply_metrics':result['apply_metrics'],'actual_io':io.report()})
    stage('correction1',correction(resource,row,resource['ko'],first),source(row),first)
    stage('correction2',correction(resource,row,first,second),source(row),second)
    proposal=copy.deepcopy(binding_template); proposal['id']='binding.cpu-ng04-chain'
    bound_resource=next(r for r in read_json(current/'ui/resources.json')['resources'] if r['id']==proposal['resource_id'])
    binding_source={'locator':{'resource':bound_resource['id'],'binding':proposal['id'],'owner':proposal['selector']['scope_key'],'slot':proposal['selector']['slot_key'],'role':bound_resource['role']},'jp':bound_resource['jp'],
                    'context':{'scope':'CPU fixture only'},'structure':{'adapter_id':proposal['adapter_id'],'selector':proposal['selector'],'source_identity':proposal['source_identity']}}
    review={'schema':'gc-ng03-reviewed-change-set-v1','binding_rows':[{'review_id':'BINDING','decision':'APPROVE','lane':'ui','locator':binding_source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',binding_source),'proposal':proposal,'old_binding_sha256':fingerprint(None)}]}
    stage('binding',review,source(binding_source),second)
    stage('layout',layout_review(current),source(),second)
    assert next(b for b in read_json(current/'ui/bindings.json')['bindings'] if b['id']==proposal['id'])==proposal
    assert sum(matches_route(b) for b in read_json(current/'ui/bindings.json')['bindings'])==1
    assert read_json(current/'layout/episode-title.json')==policy
    report={'schema':'gc-ng04-chain-fixtures-v1','fixture_only':True,'initial_root':str(owned),'stages':stages,'final_root':str(current),
            'resource_id':resource['id'],'final_ko':second,'binding_id':proposal['id'],'binding_route':binding_route,'fixture_removed_binding_id':binding_template['id'],'binding_source_identity':proposal['source_identity'],'layout_file':'layout/episode-title.json','layout_policy_id':'episode-title.fit.v1','layout':policy,'layout_sha256':fingerprint(policy),
            'cold_import_metrics':cold,'cold_actual_io':cold_io.report(),'unchanged_novel_count':len(raw_novels),'layout_change':'format canonicalization only; accepted 35/34/33/32/31 const values unchanged',
            'native':'NOT_RUN','game':'NOT_RUN'}
    report_path.write_bytes(encode(report)); return report

class ApplyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp=tempfile.TemporaryDirectory(prefix='scratch-ng04-apply-',dir=ROOT/'tests'); cls.base=Path(cls.temp.name); cls.package=small_package(cls.base/'package')
    @classmethod
    def tearDownClass(cls): cls.temp.cleanup()
    def setUp(self):
        self.area=self.base/next(tempfile._get_candidate_names()); self.area.mkdir(); self.state=self.area/'state'; self.owned,_=freeze_input(self.package,self.state)
        self.resource,self.row=ui_inputs(self.owned); self.review=correction(self.resource,self.row,self.resource['ko'],self.resource['ko']+' 교정'); self.src=source(self.row)
    def run_apply(self,review=None,**kwargs): return apply_review(self.owned,review if review is not None else self.review,self.src,self.area/'export',self.state,**kwargs)
    def test_B08_unknown_top_level_types_operation_decision(self):
        bads=[{'schema':'gc-ng03-reviewed-change-set-v1','layout_typo':[]},{'schema':'gc-ng03-reviewed-change-set-v1','rows':{}},{'schema':'gc-ng03-reviewed-change-set-v1','rows':[None]}]
        for field,value in [('operation','native_call'),('decision','MAYBE'),('decision',[]),('new_ko',42),('source_fingerprint',False)]:
            bad=copy.deepcopy(self.review); bad['rows'][0][field]=value; bads.append(bad)
        for bad in bads:
            with self.assertRaises(ValueError): self.run_apply(bad)
        self.assertFalse((self.area/'export').exists()); self.assertFalse((self.state/'authoring-current.json').exists())
    def test_B08_cli_rejects_unknown_request_with_report(self):
        review=self.area/'bad.json'; review.write_bytes(encode({'schema':'gc-ng03-reviewed-change-set-v1','unknown_rows':[]})); current=self.area/'source.json'; current.write_bytes(encode(self.src)); report=self.area/'cli-report.json'
        cli=subprocess.run([sys.executable,'-X','utf8',str(ROOT/'tools/update/cli.py'),'apply-review','--package',str(self.owned),'--review',str(review),'--current-source',str(current),'--state-root',str(self.state),'--report',str(report)],capture_output=True,text=True,timeout=20)
        self.assertEqual(2,cli.returncode); self.assertEqual('REJECTED_REQUEST',read_json(report)['status']); self.assertFalse((self.state/'authoring-current.json').exists())
    def test_B10_empty_and_already_same_value(self):
        result=self.run_apply({'schema':'gc-ng03-reviewed-change-set-v1'}); self.assertEqual('NO_CHANGE',result['status']); self.assertEqual(str(self.owned),result['next_package'])
        discard(self.area/'export'); same=correction(self.resource,self.row,self.resource['ko'],self.resource['ko']); result=self.run_apply(same)
        self.assertEqual('NO_CHANGE',result['status']); self.assertEqual('NO_CHANGE',result['request_outcomes'][0]['status'])
    def test_B09_layout_valid_nochange_stale_bounds_unknown_property(self):
        for mutation,reason in [('stale','CONCURRENT_LAYOUT_CHANGE'),('base','SCHEMA'),('script','SCHEMA')]:
            review=layout_review(self.owned)
            if mutation=='stale': review['layout_rows'][0]['old_layout_sha256']='0'*64
            elif mutation=='base': review['layout_rows'][0]['proposal']['base_size']=36
            else: review['layout_rows'][0]['proposal']['native_method']='Arbitrary'
            result=self.run_apply(review); self.assertIn(reason,result['rejected_rows'][0]['reason']); discard(self.area/'export')
        result=self.run_apply(layout_review(self.owned)); self.assertEqual('NO_CHANGE',result['status'])
    def test_B02_external_same_size_timestamp_never_trusted(self):
        external=self.area/'external'; shutil.copytree(self.package,external); novel=next((external/'novels').glob('*.json')); oldstat=novel.stat(); raw=novel.read_bytes()
        novel.write_bytes(raw.replace(b'"',b' ',1)); os.utime(novel,ns=(oldstat.st_atime_ns,oldstat.st_mtime_ns))
        with self.assertRaisesRegex(ValueError,'PAYLOAD_MISMATCH'): freeze_input(external,self.area/'external-state')
        exported=self.run_apply(); _,metrics=freeze_input(Path(exported['output']),self.area/'other-state'); self.assertEqual('cold_external_import',metrics['mode'])
    def test_B03_faults_preserve_previous_reference(self):
        first=self.run_apply(); current=Path(first['next_package']); before=(self.state/'authoring-current.json').read_bytes()
        review=correction(self.resource,self.row,self.resource['ko']+' 교정',self.resource['ko']+' 다음')
        for point in ('after_payload','after_metadata','before_publish','after_generation','before_authoring_select'):
            output=self.area/('failure-'+point)
            def fault(at):
                if at==point: raise OSError('injected transaction interruption')
            with self.assertRaises(OSError): apply_review(current,review,self.src,output,self.state,fault=fault)
            if point=='before_authoring_select':
                self.assertTrue(output.exists()); previous_inventory(output)  # complete export only; authoring reference unchanged
            else: self.assertFalse(output.exists())
            self.assertEqual(before,(self.state/'authoring-current.json').read_bytes())
            self.assertFalse(list((self.state/'verified-packages').glob('.owned-apply-*')))
    def test_B03_mid_export_failure_removes_partial_output(self):
        original=shutil.copytree
        def copy_fail(src,dst,*args,**kwargs):
            if Path(dst).name.startswith('.owned-export-'):
                (Path(dst)/'partial').write_text('interrupted'); raise OSError('injected copy')
            return original(src,dst,*args,**kwargs)
        with patch('update.apply_review.shutil.copytree',copy_fail):
            with self.assertRaises(OSError): self.run_apply()
        self.assertFalse((self.area/'export').exists()); self.assertFalse((self.state/'authoring-current.json').exists())
        self.assertFalse(list(self.area.glob('.owned-export-*')))
    def test_export_race_preserves_unowned_existing_directory(self):
        output=self.area/'export'
        def fault(point):
            if point=='before_export_publish': output.mkdir(); (output/'operator-owned.txt').write_text('preserve unrelated caller data')
        with self.assertRaises(OSError): self.run_apply(fault=fault)
        self.assertEqual('preserve unrelated caller data',(output/'operator-owned.txt').read_text())
        self.assertFalse((self.state/'authoring-current.json').exists()); self.assertFalse(list(self.area.glob('.owned-export-*')))
    def test_B04_stale_expected_base_rejected(self):
        self.run_apply()
        with self.assertRaisesRegex(ValueError,'AUTHORING_EXPECTED_BASE_CONFLICT'): apply_review(self.owned,self.review,self.src,self.area/'stale',self.state)
    def test_master_change_targeted_guard_and_ui_no_replay(self):
        from reconcile_master import verify_master_delta
        with patch('reconcile_master.verify_master_delta',wraps=verify_master_delta) as guard: self.run_apply()
        self.assertEqual(0,guard.call_count)
        current=Path(read_json(self.state/'authoring-current.json')['next_package']); master=read_json(current/'master.json')
        table=next(iter(master)); field=next(iter(master[table])); jp=next(iter(master[table][field])); old=master[table][field][jp]
        sr={'locator':{'table':table,'field':field,'record':'cpu-fixture'},'jp':jp,'context':{'scope':'CPU fixture only'}}
        snap=source(); snap['master']=[sr]
        row={'review_id':'M1','decision':'APPROVE','lane':'master','locator':sr['locator'],'source_fingerprint':source_fingerprint(PROFILE,'master',sr),'target':{'file':'master.json','keys':[table,field,jp]},'old_ko_sha256':fingerprint(old),'new_ko':old+' [1]'}
        second={**row,'review_id':'M2','old_ko_sha256':fingerprint(old+' [1]'),'new_ko':old+' [2]'}
        review={'schema':'gc-ng03-reviewed-change-set-v1','rows':[row,second]}
        with patch('reconcile_master.verify_master_delta',wraps=verify_master_delta) as guard:
            result=apply_review(current,review,snap,self.area/'master-export',self.state)
        self.assertEqual(1,guard.call_count); self.assertEqual(old+' [2]',read_json(Path(result['next_package'])/'master.json')[table][field][jp]); self.assertEqual(2,result['consumed_count'])
    def test_master_field_mapping_finite_scalar_array_delimited(self):
        selectors=read_json(ROOT/'contracts/master-source-fields-v1.json')['fields']
        for kind in ('scalar','array','delimited'):
            pair=next(p for p in selectors if (p['runtime_field'].endswith('[]') if kind=='array' else p['runtime_field'].endswith('|') if kind=='delimited' else not p['runtime_field'].endswith(('[]','|'))))
            table,runtime=pair['table'],pair['runtime_field']; field=runtime[:-2] if kind=='array' else runtime[:-1] if kind=='delimited' else runtime
            structure={'mapping_contract':'MasterDataTranslator-field-grammar-v1','physical_field':field+'[0]' if kind=='array' else field,'field_type':'ml-array' if kind=='array' else 'delimited-string' if kind=='delimited' else 'string'}
            if kind=='array': structure['jp_index']=0
            if kind=='delimited': structure.update(delimiter='|',suffix='|preserved')
            sr={'locator':{'table':table,'record':'CPU','field':field},'jp':'原文','translation_target':{'table':table,'field':runtime},'structure':structure}
            snap=source(); snap['master']=[sr]; doc={table:{runtime:{'原文':'원문'}}}
            row={'lane':'master','decision':'APPROVE','locator':sr['locator'],'source_fingerprint':source_fingerprint(PROFILE,'master',sr),'target':{'file':'master.json','keys':[table,runtime,'原文']},'old_ko_sha256':fingerprint('원문'),'new_ko':'교정'}
            self.assertTrue(apply_row(doc,row,snap)); self.assertEqual('교정',doc[table][runtime]['原文'])
            sr['translation_target']['field']='unlisted[]'; row['source_fingerprint']=source_fingerprint(PROFILE,'master',sr)
            with self.assertRaisesRegex(ValueError,'UNSUPPORTED_MASTER_FIELD_MAPPING'): apply_row(doc,row,snap)
    def test_explicit_registration_no_automatic_register(self):
        manifest=read_json(self.owned/'manifest.json'); id=next(p.stem for p in (self.owned/'novels').glob('*.json') if p.stem not in manifest['novels'])
        with ActualIO({(self.owned/'novels'/f'{id}.json').read_bytes()}) as io:
            result=self.run_apply({'schema':'gc-ng03-reviewed-change-set-v1','register':[id],'unregister':['99999999']})
        updated=read_json(Path(result['next_package'])/'manifest.json')
        self.assertIn(id,updated['novels']); self.assertEqual(2,result['consumed_count']); self.assertEqual('UNREGISTER_UNKNOWN_NOVEL',result['rejected_rows'][0]['reason'])
        self.assertGreater(io.report()['novel_read_calls'],0); self.assertGreater(io.report()['novel_hash_calls'],0)
        self.assertGreater(result['apply_metrics']['novel_payload_reads'],0); self.assertGreater(result['apply_metrics']['unchanged_novel_payload_reads'],0); self.assertGreater(result['apply_metrics']['unchanged_novel_payload_hashes'],0)
    def test_registration_tampered_owned_novel_aborts_candidate(self):
        manifest=read_json(self.owned/'manifest.json'); file=next(p for p in (self.owned/'novels').glob('*.json') if p.stem not in manifest['novels']); raw=file.read_bytes(); file.chmod(0o600)
        file.write_bytes(raw.replace(b'"',b' ',1))
        with self.assertRaisesRegex(ValueError,'OWNED_CONSUMPTION_IDENTITY_MISMATCH'): self.run_apply({'schema':'gc-ng03-reviewed-change-set-v1','register':[file.stem]})
        self.assertFalse((self.area/'export').exists()); self.assertFalse((self.state/'authoring-current.json').exists())
    def test_owned_touched_same_size_tamper_rejected_at_consumption(self):
        file=self.owned/'ui/resources.json'; file.chmod(0o600); raw=file.read_bytes(); file.write_bytes(raw.replace(self.resource['ko'].encode(),('X'*len(self.resource['ko'].encode())).encode(),1))
        with self.assertRaisesRegex(ValueError,'OWNED_CONSUMPTION_IDENTITY_MISMATCH'): self.run_apply()
        self.assertFalse((self.area/'export').exists()); self.assertFalse((self.state/'authoring-current.json').exists())
    def test_B04_live_process_busy_orphan_recovery_and_stale(self):
        review=self.area/'review.json'; review.write_bytes(encode(self.review)); snapshot=self.area/'source.json'; snapshot.write_bytes(encode(self.src)); ready=self.area/'ready'
        argv=[sys.executable,'-X','utf8',str(Path(__file__)),'--writer',str(self.owned),str(review),str(snapshot),str(self.state),str(self.area/'process-export')]
        process=subprocess.Popen(argv+['--hold',str(ready)],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        try:
            deadline=time.monotonic()+15
            while not ready.exists() and process.poll() is None and time.monotonic()<deadline: time.sleep(.05)
            self.assertTrue(ready.exists())
            busy=subprocess.run(argv,capture_output=True,text=True,timeout=20); self.assertNotEqual(0,busy.returncode); self.assertIn('AUTHORING_STATE_WRITER_BUSY',busy.stdout)
        finally: process.terminate(); process.communicate(timeout=10)
        # Its own scratch after abrupt termination is not selected; the OS lock
        # releases without deleting the lock file or terminating another writer.
        recovered=subprocess.run(argv,capture_output=True,text=True,timeout=30); self.assertEqual(0,recovered.returncode,recovered.stdout+recovered.stderr)
        stale=subprocess.run(argv[:-1]+[str(self.area/'stale-process')],capture_output=True,text=True,timeout=30); self.assertNotEqual(0,stale.returncode); self.assertIn('AUTHORING_EXPECTED_BASE_CONFLICT',stale.stdout)

def writer(args):
    package,review,snapshot,state,output=map(Path,args.writer)
    def fault(point):
        if args.hold and point=='before_publish':
            args.hold.write_text('own process ready')
            deadline=time.monotonic()+30
            while time.monotonic()<deadline: time.sleep(.05)
    try:
        result=apply_review(package,read_json(review),read_json(snapshot),output,state,fault=fault); print(json.dumps(result)); return 0
    except ValueError as error: print(str(error)); return 2


if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--writer',nargs=5); parser.add_argument('--hold',type=Path); parser.add_argument('--skip-chain',action='store_true'); parser.add_argument('--chain-only',action='store_true')
    args=parser.parse_args()
    if args.writer: raise SystemExit(writer(args))
    result=None
    if not args.chain_only: result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ApplyTests))
    chain=None
    if not args.skip_chain and (result is None or result.wasSuccessful()): chain=generate_chain(ROOT/'tests/fixtures-ng04-chain',ROOT/'tests/NG04_CHAIN_FIXTURES.json')
    report={'schema':'gc-ng04-product-apply-tests-v1','status':'PASS' if result is None or result.wasSuccessful() else 'FAIL','count':result.testsRun if result else 0,'failures':len(result.failures)+len(result.errors) if result else 0,
            'chain_fixture_report':'tests/NG04_CHAIN_FIXTURES.json' if chain else None,'transaction_stub_scope':'UI/Master/registration fixtures only; Novel uses the separate current workflow','native':'NOT_RUN','game':'NOT_RUN'}
    (ROOT/'tests/NG04_APPLY_TEST_RESULTS.json').write_bytes(encode(report)); raise SystemExit(0 if report['status']=='PASS' else 1)
