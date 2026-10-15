"""Small durable work list beside the existing source state, not a queue server.

Catalog acquisition is never semantic completion. Only a validated WB return
and published apply can finish a corresponding work item.
"""
from __future__ import annotations
from pathlib import Path
from contract_validation import read_json
from update.diff_review import fingerprint, ReviewConflict
from update.source_scan import atomic_json
from update.process_lock import ProcessFileLock

def paths(state_root,profile):
    key=fingerprint(profile)
    from update.source_scan import check_private_root
    root=check_private_root(Path(state_root).resolve())
    return root/(key+'.intake.json'),root/(key+'.intake.lock')

def identity(ref):
    asset=ref['textasset']
    return fingerprint({'profile':ref['profile'],'source_id':ref['source_id'],'asset_key':ref['asset_key'],
                        'textasset':{k:asset.get(k) for k in ('name','path_id','container','sha256','size')},
                        'extractor':ref['extractor']})

def reconcile(snapshot,catalog,state_root):
    path,lock=paths(state_root,snapshot['profile'])
    with ProcessFileLock(lock,'INTAKE_WRITER_BUSY'):
        state=read_json(path) if path.exists() else {'schema':'gc-ng05-intake-work-v1','profile':snapshot['profile'],'work':{}}
        if state.get('schema')!='gc-ng05-intake-work-v1' or state.get('profile')!=snapshot['profile']: raise ReviewConflict('INTAKE_STATE_CONTRACT')
        fresh=set();new=0;repack=0
        observed=snapshot.get('metadata',{}).get('status')!='UNKNOWN'
        for ref in catalog['references'] if observed else []:
            key=identity(ref);fresh.add(key);old=state['work'].get(key)
            if old is None:
                old={'work_id':key,'status':'pending','reason':'NEW_OR_CHANGED_SCRIPT','reference':ref,'attempts':0};state['work'][key]=old;new+=1
            else:
                if old['reference']!=ref: repack+=1
                old['reference']=ref
                if old['status']=='held' and old.get('reason') in {'SOURCE_SCOPE_INELIGIBLE','SOURCE_ID_COLLISION'}:
                    old['status']='completed' if old.get('result') else 'pending';old['reason']='SOURCE_ELIGIBILITY_RESTORED'
            old['current_eligible']=True
        for key,work in state['work'].items():
            if key in fresh: continue
            # UNKNOWN observations cannot abandon previously unfinished work;
            # they only block its current delivery until source is observed again.
            work['current_eligible']=False
            work['status']='held';work['reason']='SOURCE_ID_COLLISION' if any(i.get('source_collision') and i.get('source_id')==work['reference']['source_id'] for i in snapshot.get('novel',[])) else 'SOURCE_SCOPE_INELIGIBLE'
        atomic_json(path,state)
        actionable=[w for w in state['work'].values() if w.get('current_eligible') and w['status'] in {'pending','failed'}]
        return {'schema':'gc-ng05-actionable-intake-v1','profile':snapshot['profile'],'state_path':str(path),
                'new_work':new,'repack_same_script':repack,'actionable':actionable,
                'catalog_references':len(catalog['references']),'completed':sum(w['status']=='completed' for w in state['work'].values()),
                'held':sum(w['status']=='held' for w in state['work'].values()),'automatic_semantic_approval':False}

def mark_failed(state_root,profile,work_id,reason):
    import re
    if not isinstance(reason,str) or not re.fullmatch('[A-Z][A-Z0-9_]{0,95}',reason): raise ReviewConflict('INTAKE_TYPED_REASON_REQUIRED')
    path,lock=paths(state_root,profile)
    with ProcessFileLock(lock,'INTAKE_WRITER_BUSY'):
        state=read_json(path);work=state['work'].get(work_id)
        if work is None or not work.get('current_eligible') or work['status']=='completed': raise ReviewConflict('INTAKE_WORK_NOT_ACTIONABLE')
        work.update(status='failed',reason=reason,attempts=work['attempts']+1);atomic_json(path,state)

def validate_apply_link(link,snapshot,operation):
    if not isinstance(link,dict) or set(link)!={'state_root','work_ids'} or not isinstance(link['state_root'],str) or not isinstance(link['work_ids'],list) or not link['work_ids'] or len(set(link['work_ids']))!=len(link['work_ids']):
        raise ReviewConflict('INTAKE_APPLY_LINK_REQUIRED')
    path,_=paths(link['state_root'],snapshot['profile']);state=read_json(path)
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN': raise ReviewConflict('INTAKE_CURRENT_OBSERVATION_REQUIRED')
    refs=snapshot.get('qualified_sources',{});covered={}
    for key,pin in operation['reviewed_source_refs'].items():
        current=refs.get(key)
        if not isinstance(current,dict): continue
        normalized={k:v for k,v in current.items() if k not in {'source_id','asset_key'}}
        if normalized==pin: covered.setdefault((current.get('source_id'),current.get('asset_key')),set()).add(key)
    basis={}
    from update.novel_bridge import _eligible_novel_items
    eligible={(i['source_id'],i['asset_key']):i for i in _eligible_novel_items(snapshot)}
    for key in link['work_ids']:
        work=state.get('work',{}).get(key)
        if not work or work['status'] not in {'pending','failed'} or not work.get('current_eligible'): raise ReviewConflict('INTAKE_WORK_NOT_ACTIONABLE')
        ref=work['reference'];owner=(ref['source_id'],ref['asset_key']);item=eligible.get(owner)
        if item is None or owner not in covered or 'novels/'+ref['source_id']+'.json' not in operation['old_ko']: raise ReviewConflict('INTAKE_WB_RETURN_SCOPE_MISMATCH')
        if ref['vendor_token']!=item['vendor_token'] or ref['textasset']!=item['selected_textasset'] or ref['extractor']!=item['extractor']: raise ReviewConflict('INTAKE_WORK_SOURCE_CHANGED')
        # Existing physical WB locator binds raw TextAsset digest. Owner tags and
        # byte-hash pins alone are insufficient to assert this work was reviewed.
        if not any(isinstance(p,dict) and p.get('raw_textasset_sha256')==ref['textasset']['sha256'] and fingerprint(p)==k for k,p in snapshot.get('qualified_locators',{}).items() if k in covered[owner]):
            raise ReviewConflict('INTAKE_EXISTING_WB_PHYSICAL_LOCATOR_REQUIRED')
        basis[key]=fingerprint(work['reference'])
    return basis

def complete_after_apply(link,snapshot,operation,basis,result_package,returned):
    if returned.get('accepted_return_verified') is not True: raise ReviewConflict('INTAKE_WB_ACCEPTANCE_REQUIRED')
    path,lock=paths(link['state_root'],snapshot['profile'])
    with ProcessFileLock(lock,'INTAKE_WRITER_BUSY'):
        state=read_json(path)
        if all(state['work'].get(key,{}).get('status')=='completed' and state['work'][key].get('result',{}).get('next_package')==str(result_package) and state['work'][key].get('result',{}).get('review_id')==operation['review_id'] for key in basis):
            return {'status':'ALREADY_COMPLETED','work_ids':sorted(basis),'state_path':str(path)}
        for key,pin in basis.items():
            work=state['work'].get(key)
            if work is None or fingerprint(work['reference'])!=pin or not work.get('current_eligible') or work['status'] not in {'pending','failed'}: raise ReviewConflict('INTAKE_CONCURRENT_SOURCE_CHANGE')
        for key in basis:
            state['work'][key].update(status='completed',reason='EXISTING_WB_ACCEPTED_RETURN_AND_APPLY_COMMITTED',
                result={'next_package':str(result_package),'review_id':operation['review_id'],'m4_plan':operation['m4_plan'],'payloads':sorted(returned['payloads'])})
        atomic_json(path,state)
    return {'status':'COMPLETED','work_ids':sorted(basis),'state_path':str(path)}
