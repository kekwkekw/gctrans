"""Measure actual candidate fast-path Novel reads and explicit staging scan."""
from pathlib import Path
from unittest.mock import patch
import sys,json
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from build_manifests import build,encode
from semantic_diff import descriptor,diff
from verify_staged_release import verify

def run():
    tree=ROOT/'translations/ko';raw_read=Path.read_bytes;reads=[]
    def counted(path):
        if path.parent.name=='novels':reads.append(path.relative_to(tree).as_posix())
        return raw_read(path)
    with patch.object(Path,'read_bytes',counted):
        build(tree)
        before=descriptor(tree,mode='verified_immutable_inventory')
        after=descriptor(tree,mode='verified_immutable_inventory')
        result=diff(tree,tree,mode='trusted',old_descriptor=before,new_descriptor=after,changeset={'add':[],'modify':[],'remove':[]})
    if reads or result['classification']!='no_change':raise AssertionError('actual candidate fast path opened unchanged Novel or changed semantics')
    staging=verify(tree)
    report={'schema':'gc-ng02-full-candidate-data-check-v1','status':'PASS','normal_manifest_build_and_nochange_diff_novel_reads':len(reads),
            'novels_in_verified_inventory':sum(e['kind']=='novel' for e in before['files']),
            'comparison_mode':result['comparison_mode'],'trust_condition':result['trust_condition'],
            'historical_receipt_used':False,'full_staging_scan':staging,'game_operations':0,'native_hook_install':'NOT_RUN','game_visual_validation':'NOT_RUN'}
    (ROOT/'tests/DATA_FULL_CANDIDATE_RESULTS.json').write_bytes(encode(report));return report

if __name__=='__main__':
    report=run();print(json.dumps({k:report[k] for k in ['status','normal_manifest_build_and_nochange_diff_novel_reads','novels_in_verified_inventory','historical_receipt_used']}))
