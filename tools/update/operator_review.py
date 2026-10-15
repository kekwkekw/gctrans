"""Narrow package-target KO review and explicit whole Master runtime-key review.

These plans never establish new source observation or native eligibility.
"""
from __future__ import annotations
import copy
from pathlib import Path
from contract_validation import read_json
from update.diff_review import ReviewConflict, fingerprint, apply_row, source_fingerprint, index

def ui_basis(resources, bindings, resource_id):
    matches=[r for r in resources['resources'] if r['id']==resource_id]
    if len(matches)!=1: raise ReviewConflict('RESOURCE_TARGET_REQUIRED')
    resource=matches[0]
    if resource.get('kind') not in {'literal','template_label'} or resource.get('disposition')!='translate':
        raise ReviewConflict('PROTECTED_OR_MASTER_REF_RESOURCE')
    peers=sorted((copy.deepcopy(b) for b in bindings['bindings'] if b['resource_id']==resource_id),key=lambda b:b['id'])
    return {'kind':'package_target','contract':'gc-ng05-existing-ui-ko-v1',
            'resource':{k:v for k,v in resource.items() if k!='ko'}, 'bindings':peers},resource

def plan_ui_edit(package, resource_id, new_ko):
    if not isinstance(new_ko,str): raise ReviewConflict('REVIEWED_KO_STRING_REQUIRED')
    basis,resource=ui_basis(read_json(Path(package)/'ui/resources.json'),read_json(Path(package)/'ui/bindings.json'),resource_id)
    draft={'review_id':'ui-edit:'+resource_id,'operation':'set_ko','decision':'HOLD','lane':'ui',
           'source_basis':basis,'source_basis_sha256':fingerprint(basis),
           'target':{'file':'ui/resources.json','resource_id':resource_id},
           'old_ko_sha256':fingerprint(resource.get('ko')),'new_ko':new_ko}
    return {'schema':'gc-ng05-ui-edit-plan-v1','status':'DRAFT_REVIEW_REQUIRED','source_basis':'package_target',
            'resource_id':resource_id,'jp':resource['jp'],'ko':resource.get('ko'),'role':resource['role'],'kind':resource['kind'],
            'bindings':basis['bindings'],'disposition':resource['disposition'],
            'scope':'KO only; no fresh PC raw observation or new native proof',
            'review':{'schema':'gc-ng03-reviewed-change-set-v1','rows':[draft]}}

def apply_package_ui(doc,bindings,op):
    if op.get('lane')!='ui' or op.get('target',{}).get('file')!='ui/resources.json':
        raise ReviewConflict('PACKAGE_TARGET_UI_KO_ONLY')
    basis,resource=ui_basis(doc,bindings,op['target'].get('resource_id'))
    if op.get('source_basis')!=basis or op.get('source_basis_sha256')!=fingerprint(basis):
        raise ReviewConflict('STALE_PACKAGE_TARGET_SOURCE_OR_BINDING')
    if fingerprint(resource.get('ko'))!=op['old_ko_sha256']: raise ReviewConflict('CONCURRENT_KO_CHANGE')
    changed=resource.get('ko')!=op['new_ko']
    if changed: resource['ko']=op['new_ko']
    return changed

def master_key_basis(doc,snapshot,keys):
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN': raise ReviewConflict('CURRENT_SOURCE_OBSERVATION_UNKNOWN')
    if len(keys)!=3 or any(not isinstance(k,str) for k in keys): raise ReviewConflict('EXACT_MASTER_RUNTIME_KEY_REQUIRED')
    table,field,jp=keys
    try: old=doc[table][field][jp]
    except (KeyError,TypeError): raise ReviewConflict('ACCEPTED_MASTER_KEY_REQUIRED') from None
    records,collisions=index(snapshot)
    peers=[]
    for lane,row in records.values():
        if lane!='master' or row['jp']!=jp or row['locator'].get('table')!=table: continue
        mapped=row.get('translation_target',{'table':table,'field':row['locator'].get('field')})
        if not isinstance(mapped,dict) or mapped.get('field')!=field: continue
        from update.diff_review import validate_master_target
        validate_master_target(row,{'file':'master.json','keys':keys})
        peers.append({k:copy.deepcopy(row.get(k)) for k in ('locator','jp','context','structure','translation_target')})
    if not peers: raise ReviewConflict('MASTER_KEY_OBSERVED_PEERS_REQUIRED')
    # Any colliding locator for this exact key is ambiguous, even if index removed it.
    from update.diff_review import row_key
    for row in snapshot.get('master',[]):
        if row['jp']==jp and row['locator'].get('table')==table and row_key(snapshot['profile'],'master',row) in collisions:
            raise ReviewConflict('MASTER_KEY_SOURCE_COLLISION')
    peers.sort(key=fingerprint)
    coverage={'metadata':{k:snapshot.get('metadata',{}).get(k) for k in ('status','complete')},
              'field_scope':[v for v in snapshot.get('master_field_coverage',[]) if v.get('table')==table and v.get('runtime_field',v.get('field'))==field],
              'completeness':'OBSERVED_PEERS_ONLY_NOT_LATEST_PC_SERVER'}
    return {'kind':'observed_runtime_key','contract':'gc-ng05-master-key-v1','profile':snapshot['profile'],
            'runtime_key':{'table':table,'runtime_field':field,'exact_jp':jp},'peers':peers,'coverage':coverage},old

def plan_master_key(package,snapshot,keys,new_ko):
    basis,old=master_key_basis(read_json(Path(package)/'master.json'),snapshot,keys)
    op={'review_id':'master-key:'+fingerprint(keys)[:16],'operation':'set_runtime_key_ko','decision':'HOLD',
        'target':{'file':'master.json','keys':keys},'source_basis':basis,'source_basis_sha256':fingerprint(basis),
        'old_ko_sha256':fingerprint(old),'new_ko':new_ko,'all_uses_same_ko':True}
    return {'schema':'gc-ng05-master-key-plan-v1','status':'DRAFT_REVIEW_REQUIRED','old_ko':old,
            'effect':'One dictionary leaf; same KO at every runtime use of this exact key, including unobserved uses',
            'accepted_key_correction':True,'new_source_approval':False,'basis':basis,
            'review':{'schema':'gc-ng03-reviewed-change-set-v1','master_key_rows':[op]}}

def apply_master_key(doc,snapshot,op):
    if op.get('all_uses_same_ko') is not True or op['target']['file']!='master.json':
        raise ReviewConflict('MASTER_KEY_ALL_USES_APPROVAL_REQUIRED')
    keys=op['target']['keys'];basis,old=master_key_basis(doc,snapshot,keys)
    if op.get('source_basis')!=basis or op.get('source_basis_sha256')!=fingerprint(basis):
        raise ReviewConflict('STALE_MASTER_KEY_PEERS_OR_CONTEXT')
    if fingerprint(old)!=op['old_ko_sha256']: raise ReviewConflict('CONCURRENT_KO_CHANGE')
    if old==op['new_ko']: return False
    doc[keys[0]][keys[1]][keys[2]]=op['new_ko'];return True
