"""Run documented offline commands using only this extracted source and stdlib.

No network/native call, no private helper, no semantic edit in candidate bytes.
Output is an explicit disposable directory, distinct from the authoring/runtime stores.
"""
import argparse, copy, hashlib, json, os, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'tools'), str(ROOT/'tests')]
from build_manifests import encode
from contract_validation import read_json
from ng04_apply_tests import small_package, ui_inputs, correction
from update.apply_review import freeze_input

def run(destination):
    area = destination.resolve(); area.mkdir(parents=True, exist_ok=False)
    env = os.environ.copy()
    for key in ('PYTHONPATH','PYTHONHOME','NG03_PRIVATE_CRYPTO_HELPER','NG03_PRIVATE_CRYPTO_SHA256'):
        env.pop(key, None)
    records = []
    def execute(name, arguments, expected=0, cli=True):
        command = [sys.executable,'-I','-S','-X','utf8'] + ([str(ROOT/'tools/update/cli.py')] if cli else []) + arguments
        result = subprocess.run(command, cwd=area, env=env, capture_output=True, text=True, timeout=120)
        records.append({'name':name,'command':command,'exit':result.returncode,
                        'expected_exit':expected,'status':'PASS' if result.returncode==expected else 'FAIL',
                        'stderr':result.stderr[-2000:]})
        assert result.returncode == expected, (name,result.stdout,result.stderr)
        return json.loads(result.stdout) if result.stdout.strip().startswith('{') else result.stdout
    execute('help',['--help']); execute('scan help',['scan','--help'])
    execute('safe imports',['-c',"import sys;sys.path.insert(0,"+repr(str(ROOT))+");import scripts.crawl_only, scripts.crypto, tools.update.source_scan;print('safe imports PASS')"],cli=False)
    unknown = execute('PC no locator',['scan','--platform','PC','--state-root',str(area/'unknown-state'),'--raw-root',str(area/'unknown-raw'),'--output',str(area/'unknown.json')],2)
    assert unknown['operation_status']=='OBSERVATION_UNAVAILABLE' and unknown['reason']=='PC_SOURCE_LOCATOR_REQUIRED'
    assert (area/'unknown.json').is_file()
    asset = area/'explicit-synthetic-asset'; asset.write_bytes(b'NG06 metadata fixture')
    base = ['scan','--platform','PC','--state-root',str(area/'source-state'),'--raw-root',str(area/'source-raw'),
            '--local-asset',str(asset),'--asset-key','notinit/novel_script/sts_'+next((ROOT/'translations/ko/novels').glob('*.json')).stem+'.dmm']
    for label in ('first','second'):
        observed = execute('metadata '+label,base+['--output',str(area/(label+'.json'))])
        assert observed['operation_status']=='METADATA_ONLY_PLAN' and observed['raw_acquisition_status']=='NOT_ACQUIRED'
    difference = execute('scan envelopes to diff',['diff','--previous',str(area/'first.json'),'--current',str(area/'second.json'),'--report',str(area/'diff.json')])
    assert difference['changes']==[]
    malformed = area/'malformed.json'; malformed.write_text('{}',encoding='utf-8')
    bad = execute('malformed source typed reject',['diff','--previous',str(area/'first.json'),'--current',str(malformed),'--report',str(area/'bad-diff.json')],2)
    assert bad['status']=='REJECTED_SOURCE_INPUT'
    unavailable = execute('optional raw decoder absent only acquisition lane',base+['--acquire','--output',str(area/'acquire.json')],2)
    assert unavailable['code']=='SOURCE_UNITY_DECODER_REQUIRED'
    # Small real-contract package: fixture-only KO, no old archive or runtime dependency.
    package = small_package(area/'fixture-package'); state = area/'authoring'
    owned, cold = freeze_input(package,state); resource,row = ui_inputs(owned)
    plan = execute('UI draft',['plan-ui-edit','--package',str(owned),'--resource-id',resource['id'],'--new-ko','NG06 disposable UI fixture',
                               '--report',str(area/'plan.json'),'--draft',str(area/'draft.json')])
    assert plan['source_basis']=='package_target'
    review = read_json(area/'draft.json'); assert review['rows'][0]['decision']=='HOLD'
    review['rows'][0]['decision']='APPROVE'
    other = next(r for r in read_json(owned/'ui/resources.json')['resources'] if r['id']!=resource['id'] and r['kind']=='literal' and r['disposition']=='translate' and r.get('ko'))
    otherrow = copy.deepcopy(row); otherrow['locator']['resource']=other['id'];otherrow['locator']['role']=other['role'];otherrow['jp']=other['jp']
    observed = correction(other,otherrow,other['ko'],'not approved without observed source')['rows'][0]
    observed['review_id']='requires-current-source';review['rows'].append(observed)
    (area/'reviewed-fixture.json').write_bytes(encode(review))
    applied = execute('explicit fixture review partial commit',['apply-review','--package',str(owned),'--review',str(area/'reviewed-fixture.json'),
                       '--state-root',str(state),'--report',str(area/'apply.json')],2)
    assert applied['status']=='PARTIAL_HOLDS'
    current = Path(applied['next_package']); actual=read_json(current/'ui/resources.json')['resources']
    assert next(r for r in actual if r['id']==resource['id'])['ko']=='NG06 disposable UI fixture'
    assert next(r for r in actual if r['id']==other['id'])==other
    assert applied['apply_metrics']['unchanged_novel_payload_reads']==0 and applied['apply_metrics']['unchanged_novel_payload_hashes']==0
    preflight = execute('artifact axes',['preflight','--candidate-root',str(ROOT),'--report',str(area/'preflight.json')])
    assert preflight['artifact']['status']=='PASS' and preflight['native_trial']['status']=='NOT_EXECUTED_BY_PREFLIGHT'
    report={'schema':'gc-ng06-extracted-command-replay-v1','status':'PASS','root':str(ROOT),'commands':records,
            'environment':{'PYTHONPATH':'REMOVED','site_packages':'DISABLED_WITH_S','isolated_interpreter':True,
                           'private_helper':'NOT_SUPPLIED','original_workspace_source':'NOT_USED'},
            'python':sys.version,'source_observation':'SYNTHETIC_METADATA_ONLY; no real source acquisition',
            'next_package':str(current),'request_outcomes':applied['request_outcomes'],'apply_metrics':applied['apply_metrics'],
            'cold_import_metrics':cold,'semantic_changes':'DISPOSABLE_FIXTURES_ONLY','native':'NOT_RUN','game':'NOT_RUN','network_requests':0}
    (area/'REPLAY_RESULTS.json').write_bytes(encode(report)); print(json.dumps({'status':'PASS','commands':len(records),'report':str(area/'REPLAY_RESULTS.json')}))
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--scratch',type=Path,required=True)
    run(parser.parse_args().scratch)
