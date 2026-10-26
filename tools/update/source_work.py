"""Small durable work list beside the existing source state.

Catalog acquisition is never review completion. Only a successfully applied PRO-reviewed Novel batch can finish matching work items.
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

def complete_novel_batch(state_root, snapshot, reviewed_sources, *, review_id, payloads):
    """Record a successfully applied PRO-reviewed Novel batch.

    The caller validates PRO review and atomically writes the payloads first.
    This only completes the corresponding current source work. Repeating the
    same completion is harmless; a later reviewed retranslation can replace
    its latest result after the caller's target-preimage check.
    """
    import re
    from update.novel_intake import eligible_novel_items
    if not isinstance(review_id,str) or not review_id:
        raise ReviewConflict('INTAKE_NOVEL_REVIEW_ID_REQUIRED')
    if not isinstance(reviewed_sources,list) or not reviewed_sources or not isinstance(payloads,dict):
        raise ReviewConflict('INTAKE_NOVEL_COMPLETION_INPUT')
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN':
        raise ReviewConflict('INTAKE_CURRENT_OBSERVATION_REQUIRED')
    wanted={}
    for source in reviewed_sources:
        if (not isinstance(source,dict)
                or set(source)!={'source_id','asset_key','textasset_sha256','extractor'}
                or not isinstance(source['source_id'],str)
                or not re.fullmatch(r'[0-9]{1,16}',source['source_id'])
                or not isinstance(source['asset_key'],str)
                or not isinstance(source['textasset_sha256'],str)
                or not re.fullmatch(r'[0-9a-f]{64}',source['textasset_sha256'])):
            raise ReviewConflict('INTAKE_NOVEL_COMPLETION_INPUT')
        owner=(source['source_id'],source['asset_key'])
        if owner in wanted: raise ReviewConflict('INTAKE_NOVEL_DUPLICATE_SOURCE')
        wanted[owner]=source
    expected_paths={'novels/'+source['source_id']+'.json' for source in reviewed_sources}
    if (len(expected_paths)!=len(wanted) or set(payloads)!=expected_paths
            or any(not isinstance(value,str) or not re.fullmatch(r'[0-9a-f]{64}',value) for value in payloads.values())):
        raise ReviewConflict('INTAKE_NOVEL_PAYLOAD_SCOPE')
    current={(item['source_id'],item['asset_key']):item for item in eligible_novel_items(snapshot)}
    path,lock=paths(state_root,snapshot['profile'])
    with ProcessFileLock(lock,'INTAKE_WRITER_BUSY'):
        state=read_json(path)
        if state.get('schema')!='gc-ng05-intake-work-v1' or state.get('profile')!=snapshot['profile']:
            raise ReviewConflict('INTAKE_STATE_CONTRACT')
        updates={}
        already=[]
        for owner,source in wanted.items():
            item=current.get(owner)
            if (item is None or item['selected_textasset']['sha256']!=source['textasset_sha256']
                    or item['extractor']!=source['extractor']):
                raise ReviewConflict('INTAKE_NOVEL_SOURCE_CHANGED')
            reference={'profile':snapshot['profile'],'source_id':item['source_id'],
                       'asset_key':item['asset_key'],'textasset':item['selected_textasset'],
                       'extractor':item['extractor']}
            key=identity(reference);work=state.get('work',{}).get(key)
            if (not isinstance(work,dict) or not work.get('current_eligible')
                    or work.get('status') not in {'pending','failed','completed'}):
                raise ReviewConflict('INTAKE_WORK_NOT_ACTIONABLE')
            ref=work['reference']
            if (any(ref.get(name)!=value for name,value in reference.items())
                    or ref.get('vendor_token')!=item['vendor_token'] or ref.get('bundle')!=item['raw']):
                raise ReviewConflict('INTAKE_WORK_SOURCE_CHANGED')
            payload_path='novels/'+source['source_id']+'.json'
            result={'workflow':'main-novel','review_id':review_id,
                    'textasset_sha256':source['textasset_sha256'],
                    'payloads':{payload_path:payloads[payload_path]}}
            if work['status']=='completed' and work.get('result')==result:
                already.append(key)
            else:
                updates[key]=result
        for key,result in updates.items():
            state['work'][key].update(status='completed',reason='REVIEWED_NOVEL_APPLIED',result=result)
        if updates: atomic_json(path,state)
    return {'status':'COMPLETED' if updates else 'ALREADY_COMPLETED',
            'work_ids':sorted([*updates,*already]),'state_path':str(path)}
