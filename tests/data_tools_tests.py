"""NG02 F01-F05 executable data regressions, using isolated synthetic trees."""
from __future__ import annotations
import copy, hashlib, json, shutil, sys, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from build_manifests import build, encode, safe_path
from semantic_diff import descriptor, diff
from contract_validation import read_json, validate_shape, check_models, ContractError, validate_package
from export_ng01 import Exporter, audit_order
from legacy_semantic import compute as legacy_hash, root_hash

def write(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(encode(value))

class DataTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='ng02-data-',dir=ROOT/'tests');self.base=Path(self.temp.name)
        self.corpus=read_json(ROOT/'tests/shared-validator-cases.json')
        self.rschema=read_json(ROOT/'contracts/ui-resources.schema.json');self.bschema=read_json(ROOT/'contracts/ui-bindings.schema.json')
    def tearDown(self):self.temp.cleanup()
    def fixture(self,name='old'):
        root=self.base/name;case=self.corpus['cases'][0]
        write(root/'ui/resources.json',case['resources_document']);write(root/'ui/bindings.json',case['bindings_document'])
        write(root/'layout/episode-title.json',read_json(ROOT/'translations/ko/layout/episode-title.json'))
        master={};names={'名前':'이름'};novel={'row':'aa'}
        manifest={'master':legacy_hash(master),'names':legacy_hash(names),'novels':{'100':legacy_hash(novel)}};manifest['hash']=root_hash(manifest)
        for p,value in [('master.json',master),('names.json',names),('manifest.json',manifest),('novels/100.json',novel)]:write(root/p,value)
        receipt=self.base/(name+'-bootstrap.json')
        write(receipt,{'files':[{'path':p,'bytes':(root/p).stat().st_size,'sha256':hashlib.sha256((root/p).read_bytes()).hexdigest()} for p in ['master.json','names.json','manifest.json','novels/100.json']]})
        build(root,receipt);return root,receipt
    def paired(self):
        old,receipt=self.fixture();new=self.base/'new';shutil.copytree(old,new);return old,new,receipt
    def fast(self,old,new,changeset):
        a=descriptor(old,mode='verified_immutable_inventory');b=descriptor(new,mode='verified_immutable_inventory')
        return diff(old,new,mode='trusted',old_descriptor=a,new_descriptor=b,changeset=changeset)
    def test_F01_new_novel_persists_without_bootstrap(self):
        root,receipt=self.fixture();registration=(root/'manifest.json').read_bytes()
        write(root/'novels/200.json',{'row':'unregistered'});build(root,added=['novels/200.json']);receipt.unlink()
        build(root)
        self.assertIn('novels/200.json',{x['path'] for x in read_json(root/'release-manifest.json')['files']})
        self.assertIn('novels/200.json',{x['path'] for x in read_json(root/'legacy-components.json')['files']})
        self.assertEqual(registration,(root/'manifest.json').read_bytes())
    def test_F01_explicit_modify_preserved_and_remove(self):
        root,_=self.fixture();write(root/'novels/100.json',{'row':'bb'});build(root,modified=['novels/100.json'])
        first=read_json(root/'legacy-components.json');build(root);self.assertEqual(first,read_json(root/'legacy-components.json'))
        (root/'novels/100.json').unlink();build(root,removed=['novels/100.json'],unregister=['100'])
        self.assertNotIn('novels/100.json',{x['path'] for x in read_json(root/'legacy-components.json')['files']})
    def test_F01_implicit_remove_rejected(self):
        root,_=self.fixture();(root/'novels/100.json').unlink()
        with self.assertRaisesRegex(ContractError,'EXPLICIT_REMOVE'):build(root)
    def test_F01_explicit_remove_does_not_hide_present_file(self):
        root,_=self.fixture()
        with self.assertRaisesRegex(ContractError,'REMOVED_COMPONENT_STILL_PRESENT'):build(root,removed=['novels/100.json'])
    def test_F01_conflicting_and_unknown_operations_rejected(self):
        root,_=self.fixture()
        for arguments in [{'added':['novels/100.json']},{'modified':['novels/999.json']},{'removed':['novels/999.json']},{'modified':['names.json'],'removed':['names.json']}]:
            with self.assertRaises(ContractError):build(root,**arguments)
    def test_F04_relocated_without_receipt_or_archive(self):
        root,receipt=self.fixture();moved=self.base/'unrelated'/'authoring';shutil.copytree(root,moved);receipt.unlink()
        r=read_json(moved/'ui/resources.json')
        for x in r['resources']:x['ko']='다운로드 전체'
        write(moved/'ui/resources.json',r);build(moved)
        self.assertTrue(validate_package(moved,ROOT/'contracts'))
    def test_F04_bad_previous_metadata_does_not_fall_back_to_receipt(self):
        root,receipt=self.fixture();release=read_json(root/'release-manifest.json');release['files'][0]['sha256']='0'*64;write(root/'release-manifest.json',release)
        with self.assertRaises(ContractError):build(root,receipt)
    def test_F04_bootstrap_optional_only_when_previous_exists(self):
        root,_=self.fixture();build(root,self.base/'missing-receipt.json')
        (root/'release-manifest.json').unlink()
        with self.assertRaisesRegex(ContractError,'BOOTSTRAP_RECEIPT_REQUIRED'):build(root)
    def test_F02_trusted_nochange_novel_reads_zero(self):
        old,new,_=self.paired();actual=[];original=Path.read_bytes
        def counted(path):
            if path.parent.name=='novels':actual.append(path.as_posix())
            return original(path)
        with patch.object(Path,'read_bytes',counted):result=self.fast(old,new,{'add':[],'modify':[],'remove':[]})
        self.assertEqual('no_change',result['classification']);self.assertEqual([],actual);self.assertEqual(0,result['novel_payload_reads'])
    def test_F02_trusted_ui_only_novel_reads_zero(self):
        old,new,_=self.paired();r=read_json(new/'ui/resources.json')
        for x in r['resources']:x['ko']='다운로드 전체'
        write(new/'ui/resources.json',r);build(new);actual=[];original=Path.read_bytes
        def counted(path):
            if path.parent.name=='novels':actual.append(path.as_posix())
            return original(path)
        with patch.object(Path,'read_bytes',counted):result=self.fast(old,new,{'add':[],'modify':['ui/resources.json'],'remove':[]})
        self.assertEqual('text_only',result['classification']);self.assertEqual([],actual)
    def test_F02_same_length_declared_edit_and_explicit_remove(self):
        old,new,_=self.paired();size=(new/'novels/100.json').stat().st_size
        write(new/'novels/100.json',{'row':'bb'});self.assertEqual(size,(new/'novels/100.json').stat().st_size);build(new,modified=['novels/100.json'])
        result=self.fast(old,new,{'add':[],'modify':['manifest.json','novels/100.json'],'remove':[]})
        self.assertEqual('structural',result['classification']);self.assertEqual(1,result['novel_payload_reads']);self.assertEqual('RESTART_REQUIRED',result['runtime_action'])
        (new/'novels/100.json').unlink();build(new,removed=['novels/100.json'],unregister=['100'])
        result=self.fast(old,new,{'add':[],'modify':['manifest.json'],'remove':['novels/100.json']});self.assertEqual(['novels/100.json'],result['inventory_delta']['remove'])
    def test_F02_cold_scan_detects_same_length_unlisted_mutation(self):
        old,new,_=self.paired();before=(new/'novels/100.json').stat();write(new/'novels/100.json',{'row':'bb'})
        import os
        os.utime(new/'novels/100.json',ns=(before.st_atime_ns,before.st_mtime_ns))
        result=diff(old,new,mode='cold_scan');self.assertEqual('structural',result['classification']);self.assertEqual(2,result['novel_payload_reads'])
        self.assertEqual(['novels/100.json'],result['inventory_delta']['modify'])
    def test_F02_untrusted_incomplete_or_missing_changeset_rejected(self):
        old,new,_=self.paired();a=descriptor(old,mode='verified_immutable_inventory');b=descriptor(new,mode='verified_immutable_inventory')
        with self.assertRaises(ContractError):diff(old,new,mode='trusted',old_descriptor=a,new_descriptor=b)
        b['complete_inventory']=False
        with self.assertRaises(ContractError):diff(old,new,mode='trusted',old_descriptor=a,new_descriptor=b,changeset={'add':[],'modify':[],'remove':[]})
    def test_F02_inventory_change_requires_exact_changeset(self):
        old,new,_=self.paired();write(new/'names.json',{'名前':'성명'});build(new,modified=['names.json'])
        with self.assertRaisesRegex(ContractError,'CHANGESET_INVENTORY'):self.fast(old,new,{'add':[],'modify':[],'remove':[]})
    def test_F02_changed_descriptor_must_match_actual_bytes(self):
        old,new,_=self.paired();write(new/'novels/100.json',{'row':'bb'});build(new,modified=['novels/100.json'])
        write(new/'novels/100.json',{'row':'cc'})
        with self.assertRaisesRegex(ContractError,'CHANGED_PAYLOAD_DESCRIPTOR'):self.fast(old,new,{'add':[],'modify':['manifest.json','novels/100.json'],'remove':[]})
    def test_F05_explicit_ordinal_export_order(self):
        inputs=self.base/'inputs';out=self.base/'audit-export';src=inputs/'ui21/code/helper/src'
        src.mkdir(parents=True);(src/'a.cs').write_text('var a="caption a";',encoding='utf-8');(src/'B.cs').write_text('var b="caption B";',encoding='utf-8')
        exporter=Exporter(inputs,out);files,literals,_=exporter.scan_sources()
        self.assertEqual([x['path'] for x in files],sorted(x['path'] for x in files));self.assertTrue(files[0]['path'].endswith('/B.cs'))
        before=(out/'migration/SOURCE_FILES.json').read_bytes();exporter.scan_sources();self.assertEqual(before,(out/'migration/SOURCE_FILES.json').read_bytes())
        records=[{'source_file':'z/Z.json','json_pointer':'/2'},{'source_file':'B/A.json','json_pointer':'/10'},{'source_file':'B/A.json','json_pointer':'/2'}]
        self.assertEqual(audit_order(records,'source_file','json_pointer'),audit_order(list(reversed(records)),'source_file','json_pointer'))
    def test_paths_reject_noncanonical_or_escape(self):
        for path in ['../outside.json','C:/outside.json','novels//100.json','novels/./100.json','novels\\100.json','/absolute.json']:
            with self.assertRaises(ContractError):safe_path(self.base,path)
    def test_current_activation_contract_and_approved_layout(self):
        # Ordinary regressions use shipped data, not a past migration receipt.
        resources=read_json(ROOT/'translations/ko/ui/resources.json')
        bindings=read_json(ROOT/'translations/ko/ui/bindings.json')
        model=check_models(resources,bindings)
        self.assertGreater(model['enabled_count'],0)
        rmap={r['id']:r for r in resources['resources']}
        for binding in bindings['bindings']:
            if binding['enabled']:
                self.assertEqual('translate',rmap[binding['resource_id']]['disposition'])
            if rmap[binding['resource_id']]['disposition'] in {'hold','keep_source','known_residual'}:
                self.assertFalse(binding['enabled'])
        layout=read_json(ROOT/'translations/ko/layout/episode-title.json');self.assertEqual(35,layout['base_size']);self.assertEqual([34,33,32,31],layout['descending_sizes'])
        validate_shape(read_json(ROOT/'contracts/episode-title-fit.schema.json'),layout)

class RecordingResult(unittest.TextTestResult):
    def __init__(self,*args,**kwargs):super().__init__(*args,**kwargs);self.rows=[]
    def addSuccess(self,test):super().addSuccess(test);self.rows.append({'check':test.id().split('.')[-1],'status':'PASS'})
    def addFailure(self,test,err):super().addFailure(test,err);self.rows.append({'check':test.id().split('.')[-1],'status':'FAIL','reason':str(err[1])})
    def addError(self,test,err):super().addError(test,err);self.rows.append({'check':test.id().split('.')[-1],'status':'FAIL','reason':str(err[1])})

def run_corpus():
    corpus=read_json(ROOT/'tests/shared-validator-cases.json');rows=[]
    for case in corpus['cases']:
        try:
            validate_shape(read_json(ROOT/'contracts/ui-resources.schema.json'),case['resources_document'])
            validate_shape(read_json(ROOT/'contracts/ui-bindings.schema.json'),case['bindings_document'])
            model=check_models(case['resources_document'],case['bindings_document']);accepted=True;reason=None
            # A finite host-registry fixture assesses optional capability after
            # structural validation; it is not native compatibility evidence.
            registry=corpus['host_registry']
            supported=lambda b:(b['adapter_id']==registry['adapter_id'] and b['selector']['scope_key'] in registry['scope_keys'] and b['selector']['slot_key'] in registry['slot_keys'])
            capability='available' if all(supported(b) for b in case['bindings_document']['bindings']) else 'optional_unavailable'
        except (ContractError,ValueError,KeyError) as e:accepted=False;reason=str(e);capability='package_rejected'
        rows.append({'id':case['id'],'expected_accept':case['expected']['accept'],'accept':accepted,
                     'expected_capability':case['expected']['capability'],'capability':capability,
                     'status':'PASS' if accepted==case['expected']['accept'] and capability==case['expected']['capability'] else 'FAIL','rejection':reason})
    return rows

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2,resultclass=RecordingResult).run(unittest.defaultTestLoader.loadTestsFromTestCase(DataTests));corpus=run_corpus()
    rows=result.rows+corpus;failed=sum(x['status']!='PASS' for x in rows)
    report={'schema':'gc-ng02-data-tests-v1','status':'PASS' if failed==0 else 'FAIL','passed':len(rows)-failed,'failed':failed,
            'scope':'F01-F05 offline tooling; Python shared corpus; inherited activation eligibility; no game operations',
            'checks':rows,'python_validator_cases':len(corpus),'csharp_validator_parity':'runtime host runner consumes the same corpus; reported separately',
            'native_hook_install':'NOT_RUN','game_visual_validation':'NOT_RUN','game_operations':0}
    write(ROOT/'tests/DATA_TOOL_TEST_RESULTS.json',report);print(json.dumps({k:report[k] for k in ['status','passed','failed','python_validator_cases']}));raise SystemExit(1 if failed else 0)
