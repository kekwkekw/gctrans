"""Restore inherited Story/Battle eligibility; preserve general UI decisions."""
from pathlib import Path
import sys, copy, json, hashlib
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from contract_validation import read_json, validate_package
from build_manifests import build, encode

def activate(root=ROOT):
    tree=root/'translations/ko';document=read_json(tree/'ui/bindings.json');before=copy.deepcopy(document)
    eligibility={bid:row['legacy_enabled'] for row in read_json(root/'migration/MIGRATION_MAP.json') if row['module'] in {'Story','Battle'} for bid in row['binding_ids']}
    delta=[]
    for binding in document['bindings']:
        if binding['id'] not in eligibility:continue
        old=binding['enabled'];new=eligibility[binding['id']]
        binding['enabled']=new
        if old!=new:delta.append({'binding_id':binding['id'],'adapter_id':binding['adapter_id'],'before':old,'after':new,'basis':'inherited migration legacy_enabled'})
    general=lambda doc:[b for b in doc['bindings'] if not b['adapter_id'].startswith(('story.','battle.'))]
    assert general(before)==general(document)
    immutable={p:hashlib.sha256((tree/p).read_bytes()).hexdigest() for p in ['manifest.json','layout/episode-title.json']}
    (tree/'ui/bindings.json').write_bytes(encode(document));validation=validate_package(tree,root/'contracts');manifest=build(tree)
    assert immutable=={p:hashlib.sha256((tree/p).read_bytes()).hexdigest() for p in immutable}
    report={'schema':'gc-ng02-activation-delta-v1','status':'PASS','delta':delta,'delta_count':len(delta),
            'inherited_eligibility_count':len(eligibility),'general_bindings_unchanged':True,
            'general_enabled':sum(x['enabled'] for x in general(document)),
            'story_enabled':sum(x['enabled'] for x in document['bindings'] if x['adapter_id'].startswith('story.')),
            'battle_enabled':sum(x['enabled'] for x in document['bindings'] if x['adapter_id'].startswith('battle.')),
            'legacy_manifest_and_layout_bytes_unchanged':True,'revision':manifest['revision'],'validation':validation,
            'native_hook_install':'NOT_RUN','game_visual_validation':'NOT_RUN','new_acceptance_events':0}
    (root/'tests/DATA_ACTIVATION_DELTA.json').write_bytes(encode(report));return report

if __name__=='__main__':
    result=activate();print(json.dumps({k:result[k] for k in ['status','delta_count','general_enabled','story_enabled','battle_enabled']}))
