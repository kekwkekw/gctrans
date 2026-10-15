"""Summarize executed product tests without turning portable fixtures into native/game evidence."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8-sig'))

def write(path, value):
    (ROOT/path).write_text(json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2)+'\n', encoding='utf-8')

def main():
    candidate = hashlib.sha256((ROOT/'source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll').read_bytes()).hexdigest()
    evidence = {}
    files = {
        'runtime':'tests/NG03_RUNTIME_TEST_RESULTS.json',
        'supply':'tests/NG03_SUPPLY_CPU_RESULTS.json',
        'semantic_store':'tests/NG03_SEMANTIC_STORE_RESULTS.json',
        'candidate_regression':'tests/NG03_CANDIDATE_REGRESSION_RESULTS.json',
        'portable_runtime':'tests/NG03_PORTABLE_RUNTIME_REGRESSION_RESULTS.json',
        'host':'tests/NG03_HOST_REGRESSION_RESULTS.json',
        'special':'tests/NG03_SPECIAL_REGRESSION_RESULTS.json',
        'python_data':'tests/DATA_TOOL_TEST_RESULTS.json',
        'python_apply':'tests/NG03_APPLY_TEST_RESULTS.json',
        'compile':'audit/NG03_COMPILE_AUDIT.json',
    }
    for key, path in files.items():
        row=read(path)
        if row['status']!='PASS': raise ValueError('FAILED_REPORT:'+path)
        tests=row.get('tests',row.get('results',row.get('checks',[])))
        evidence[key]={'report':path,'status':row['status'],'scenarios':row.get('count',row.get('passed',len(tests)))}
        if key == 'compile':
            evidence[key].pop('scenarios')
            evidence[key].update(source_items=row['source_items'], required_product_types=len(row['required_types']))
        if key in ('runtime','supply','semantic_store','candidate_regression','compile'):
            sha=row.get('dll_sha256',row.get('assembly_sha256',row.get('candidate_sha256')))
            if sha!=candidate:raise ValueError('ACTUAL_CANDIDATE_SHA_MISMATCH:'+path)
    corpus=read('tests/shared-validator-cases.json')
    python={x['id']:x for x in read(files['python_data'])['checks'] if 'id' in x}
    csharp={x['name']:x for x in read(files['candidate_regression'])['tests']}
    parity=[]
    for case in corpus['cases']:
        p=python[case['id']];c=csharp['shared_validator_'+case['id']]
        ok=p['status']=='PASS' and c['status']=='PASS' and p['accept']==case['expected']['accept'] and p['capability']==case['expected']['capability']
        if not ok:raise ValueError('PARITY_FAILED:'+case['id'])
        parity.append({'id':case['id'],'accept':p['accept'],'capability':p['capability'],'python':'PASS','actual_candidate_csharp':'PASS'})
    write('tests/NG03_VALIDATOR_PARITY_RESULTS.json',{'schema':'gc-ng03-shared-validator-parity-v1','status':'PASS','candidate_sha256':candidate,
        'scope':'shared structural acceptance + finite code registry optional capability; no native ABI/game claim','cases':parity})
    wiring=read('audit/RUNTIME_SOURCE_WIRING.json')
    modules=[]
    for module in wiring['modules']:
        provider=module['provider']
        if not (ROOT/provider).is_file():raise ValueError('PROVIDER_SOURCE_MISSING:'+provider)
        modules.append({'module':module['module'],'provider':provider,'source':'NG02_CONNECTION_PRESERVED_NG03_BOUNDARIES_COMPILED',
            'compile':'PASS_ACTUAL_CANDIDATE','cpu_scope':'portable policy/ownership fixtures and actual candidate loading/coordinator tests; native provider not executed',
            'evidence':['tests/NG03_HOST_REGRESSION_RESULTS.json','tests/NG03_SPECIAL_REGRESSION_RESULTS.json','tests/NG03_CANDIDATE_REGRESSION_RESULTS.json'],
            'native_hook_install':'NOT_RUN','game_validation':'NOT_RUN','publication':'NOT_PERFORMED'})
    layers=[
        {'component':'Plugin/NativeRuntimeBridge/Config lifecycle','source':['source/GCMod.Candidate/Plugin.Candidate.cs','source/GCMod.Candidate/NativeRuntimeBridge.cs','source/GCMod.Candidate/Config.Candidate.cs'],
         'compile':'PASS','cpu':'managed mailbox/cleanup/coordinator lifetime executed; Unity/BepInEx config/bridge lifecycle NOT_RUN','evidence':['tests/NG03_RUNTIME_TEST_RESULTS.json']},
        {'component':'Master/Names/registered + unregistered Novel stores','source':['source/GCMod.Candidate/Legacy','source/GCMod.Nextgen/LegacyPayloadContracts.cs','source/GCMod.Nextgen/LegacySemanticHash.cs','tools/build_manifests.py'],
         'compile':'PASS','cpu':'actual candidate corrected KO/root hash loads and preserved registration; parseable bad legacy shapes rejected, Master/Names/root semantic metadata and registered descriptor closure checked; Novel semantic first-use and warm zero-body-read retained','evidence':['tests/NG03_SEMANTIC_STORE_RESULTS.json','tests/NG03_APPLY_TEST_RESULTS.json','tests/NG03_RUNTIME_TEST_RESULTS.json','tests/NG03_SUPPLY_CPU_RESULTS.json']},
        {'component':'Fixed-ref data supply + single coordinator/store','source':['source/GCMod.Nextgen/FixedRefDataUpdater.cs','source/GCMod.Nextgen/ReleaseDescriptor.cs','source/GCMod.Nextgen/RuntimeCoordinator.cs','source/GCMod.Nextgen/LocalImmutablePackageStore.cs'],
         'compile':'PASS','cpu':'actual candidate mock HTTP/filesystem retries, integrity, reuse, pending restart/LKG, stale pending base and safe commit','evidence':['tests/NG03_SUPPLY_CPU_RESULTS.json','tests/NG03_RUNTIME_TEST_RESULTS.json']},
        {'component':'Choice R05 proof cost','source':['source/GCMod.Candidate/Native/StoryNativeProvider.cs','source/GCMod.Nextgen/ChoiceProofMetrics.cs'],
         'compile':'PASS','cpu':'Choice plan/transaction policies only; native cached guard/raycast counters NOT_MEASURED',
         'optimization':'source discovery/tree rebuild/serialization moved to creation/commit; cached exact source/component/control guards kept; conservative stable raycasts retained without verified global mutation token',
         'evidence':['audit/NG03_COMPILE_AUDIT.json','tests/NG03_SPECIAL_REGRESSION_RESULTS.json']},
    ]
    for layer in layers:layer.update(native_hook_install='NOT_RUN',game_validation='NOT_RUN',publication='NOT_PERFORMED')
    report={'schema':'gc-ng03-final-source-compile-cpu-matrix-v1','status':'OFFLINE_VALIDATION_PASS_NATIVE_NOT_RUN','candidate_sha256':candidate,
        'evidence':evidence,'modules':modules,'layers':layers,'validator_parity':'tests/NG03_VALIDATOR_PARITY_RESULTS.json',
        'source_coverage':'separate NG03 source reports govern actual acquisition and PC/Names/other adapter gaps; portable test PASS is not source materialization PASS',
        'source_coverage_evidence':'audit/NG03_SOURCE_EVIDENCE.json',
        'source_cpu_mock_execution':{'cases':19,'command':'python -X utf8 -m unittest discover -s tests -p test_ng03_source.py -v','log':'audit/NG03_OFFLINE_CONSOLE.log','source_materialization_claim':'NONE_FROM_THESE_FIXTURES'},
        'frozen_acceptance':'UI21 v2/UI22 Known Residual ACCEPTED_LOCAL_FROZEN preserved; no residual reassessment',
        'original_donor_DLL_byte_equality':'historical FAIL preserved; not a build prerequisite','XUnity':'OFF',
        'game_operations':0,'installation_changes':0,'public_writes':0,'raw_font_original_game_DLLs':'excluded from return package'}
    write('audit/NG03_FINAL_MODULE_MATRIX.json',report)
    print(json.dumps({'status':report['status'],'candidate_sha256':candidate,'modules':len(modules),'parity_cases':len(parity)}))

if __name__=='__main__':main()
