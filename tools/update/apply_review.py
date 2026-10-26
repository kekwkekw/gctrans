"""Offline reviewed apply with owned immutable authoring generations.

Exports never lend trust. Inventory carries unchanged lazy Novel identity;
touched payloads and runtime lazy consumption verify actual bytes.
Authoring current is unrelated to RuntimeCoordinator selection.
"""
from __future__ import annotations
import copy, os, re, shutil, stat, sys, uuid
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_manifests import build, previous_inventory, safe_path, encode, sha
from contract_validation import read_json, validate_package, validate_shape, ContractError
from verify_staged_release import verify
from update.diff_review import ReviewConflict, apply_row, fingerprint, find_source, validate_binding_provenance
from update.process_lock import ProcessFileLock

CONTRACTS = Path(__file__).resolve().parents[2]/'contracts'
HELD = {'HOLD','KEEP_SOURCE','KNOWN_RESIDUAL'}
ARRAYS = ('rows','resource_rows','binding_rows','layout_rows','master_key_rows','register','unregister')

class CustodyIntegrityError(ValueError):
    """Abort the whole candidate when previously owned bytes lost integrity."""

def obj(value, allowed, required=(), code='REVIEW_OPERATION_CONTRACT'):
    if not isinstance(value,dict) or set(value)-set(allowed) or set(required)-set(value): raise ReviewConflict(code)

def string(value):
    if not isinstance(value,str) or not value or '\x00' in value: raise ReviewConflict('REVIEW_STRING_REQUIRED')

def pin(value):
    if not isinstance(value,str) or not re.fullmatch('[0-9a-f]{64}',value): raise ReviewConflict('REVIEW_SHA256_REQUIRED')

def validate_review(review):
    """Fail closed before capture/staging for unknown instructions and bad types."""
    obj(review, {'schema','expected_base',*ARRAYS}, {'schema'}, 'REVIEW_SET_UNKNOWN_OR_MISSING_PROPERTY')
    if review['schema']!='gc-ng03-reviewed-change-set-v1': raise ReviewConflict('REVIEW_SET_CONTRACT_REQUIRED')
    if 'expected_base' in review: string(review['expected_base'])
    for group in ARRAYS:
        if group not in review: continue
        if not isinstance(review[group],list): raise ReviewConflict('REVIEW_ARRAY_REQUIRED:'+group)
        if group in {'register','unregister'}:
            if any(not isinstance(i,str) or not re.fullmatch('[0-9]{1,16}',i) for i in review[group]): raise ReviewConflict('INVALID_REGISTRATION_OPERATION')
            continue
        for op in review[group]:
            common={'review_id','decision','operation'}
            if group=='master_key_rows': allowed=common|{'target','source_basis','source_basis_sha256','old_ko_sha256','new_ko','all_uses_same_ko'}
            elif group=='layout_rows': allowed=common|{'target','proposal','old_layout_sha256'}
            else:
                allowed=common|{'lane','locator','source_fingerprint'}
                allowed|={'target','old_ko_sha256','new_ko','old_resource_sha256','source_basis','source_basis_sha256'} if group=='rows' else {'proposal','old_resource_sha256' if group=='resource_rows' else 'old_binding_sha256'}
            obj(op,allowed,{'review_id'}); string(op['review_id'])
            decision=op.get('decision')
            if not isinstance(decision,str) or decision not in HELD|{'APPROVE'}: raise ReviewConflict('EXPLICIT_REVIEW_DECISION_REQUIRED')
            supported={'rows':{'set_ko'},'resource_rows':{'add_or_modify'},'binding_rows':{'add_or_modify','remove'},'layout_rows':{'replace_typed_policy'},'master_key_rows':{'set_runtime_key_ko'}}
            if 'operation' in op and (not isinstance(op['operation'],str) or op['operation'] not in supported[group]): raise ReviewConflict('UNSUPPORTED_REVIEW_OPERATION:'+group)
            if decision in HELD:
                for name in ('lane','new_ko'):
                    if name in op and not isinstance(op[name],str): raise ReviewConflict('REVIEW_STRING_REQUIRED')
                for name in ('locator','target','proposal','source_basis'):
                    if name in op and not isinstance(op[name],dict): raise ReviewConflict('REVIEW_OBJECT_REQUIRED:'+name)
                for name in ('source_fingerprint','old_ko_sha256','old_resource_sha256','old_binding_sha256','old_layout_sha256','source_basis_sha256'):
                    if name in op: pin(op[name])
                continue
            if group=='master_key_rows' or (group=='rows' and 'source_basis' in op):
                obj(op,allowed,{'target','source_basis','source_basis_sha256','old_ko_sha256','new_ko'})
                obj(op['target'],{'file','keys'} if group=='master_key_rows' else {'file','resource_id'},{'file','keys'} if group=='master_key_rows' else {'file','resource_id'})
                string(op['target']['file']);pin(op['old_ko_sha256']);pin(op['source_basis_sha256'])
                if not isinstance(op['new_ko'],str) or not isinstance(op['source_basis'],dict): raise ReviewConflict('REVIEW_PACKAGE_BASIS_REQUIRED')
                if group=='master_key_rows':
                    if op.get('all_uses_same_ko') is not True or not isinstance(op['target']['keys'],list) or len(op['target']['keys'])!=3 or any(not isinstance(k,str) for k in op['target']['keys']): raise ReviewConflict('MASTER_KEY_ALL_USES_APPROVAL_REQUIRED')
                elif op.get('lane')!='ui' or op['source_basis'].get('kind')!='package_target' or any(k in op for k in ('locator','source_fingerprint','old_resource_sha256')): raise ReviewConflict('PACKAGE_TARGET_UI_KO_ONLY')
                continue
            if group=='layout_rows':
                obj(op,allowed,{'target','proposal','old_layout_sha256'})
                obj(op['target'],{'file','policy_id'},{'file','policy_id'})
                string(op['target']['file']); string(op['target']['policy_id']); pin(op['old_layout_sha256'])
                if not isinstance(op['proposal'],dict): raise ReviewConflict('TYPED_LAYOUT_OBJECT_REQUIRED')
                continue
            obj(op,allowed,{'lane','locator','source_fingerprint'})
            if not isinstance(op['lane'],str) or op['lane'] not in {'master','ui'} or not isinstance(op['locator'],dict): raise ReviewConflict('REVIEW_LANE_LOCATOR_REQUIRED')
            pin(op['source_fingerprint'])
            if group=='rows':
                obj(op,allowed,{'target','old_ko_sha256','new_ko'}); obj(op['target'],{'file','keys','resource_id'},{'file'})
                string(op['target']['file']); pin(op['old_ko_sha256'])
                if not isinstance(op['new_ko'],str): raise ReviewConflict('REVIEWED_KO_STRING_REQUIRED')
                if 'keys' in op['target'] and (not isinstance(op['target']['keys'],list) or any(not isinstance(k,str) for k in op['target']['keys'])): raise ReviewConflict('EXACT_LEGACY_TARGET_KEYS_REQUIRED')
                if 'resource_id' in op['target']: string(op['target']['resource_id'])
                if 'old_resource_sha256' in op: pin(op['old_resource_sha256'])
            else:
                key='old_resource_sha256' if group=='resource_rows' else 'old_binding_sha256'
                obj(op,allowed,{'proposal',key}); pin(op[key])
                if not isinstance(op['proposal'],dict): raise ReviewConflict('REVIEW_PROPOSAL_OBJECT_REQUIRED')
                schema=read_json(CONTRACTS/('ui-resources.schema.json' if group=='resource_rows' else 'ui-bindings.schema.json'))
                validate_shape(schema['properties']['resources' if group=='resource_rows' else 'bindings']['items'],op['proposal'])
    ids=review.get('register',[])+review.get('unregister',[])
    if len(ids)!=len(set(ids)): raise ReviewConflict('INVALID_REGISTRATION_OPERATION')

def writable(root):
    for p in root.rglob('*'):
        if p.is_file(): p.chmod(stat.S_IREAD|stat.S_IWRITE)

def immutable(root):
    for p in root.rglob('*'):
        if p.is_file(): p.chmod(stat.S_IREAD)

def discard(root):
    def recover(function,path,error):
        os.chmod(path,stat.S_IREAD|stat.S_IWRITE); function(path)
    if root.exists(): shutil.rmtree(root,onerror=recover)

def atomic_json(path,value):
    path.parent.mkdir(parents=True,exist_ok=True); temp=path.with_name('.authoring-'+uuid.uuid4().hex+'.json')
    try:
        with temp.open('xb') as stream: stream.write(encode(value)); stream.flush(); os.fsync(stream.fileno())
        os.replace(temp,path)
    finally: temp.unlink(missing_ok=True)

def exclusive_directory_publish(temporary,target):
    """Publish our export directory without replacing any caller-owned path."""
    if os.name=='nt':
        os.rename(temporary,target)  # Windows rename refuses an existing target.
    elif sys.platform.startswith('linux'):
        import ctypes
        libc=ctypes.CDLL(None,use_errno=True)
        function=getattr(libc,'renameat2',None)
        if function is None: raise ContractError('EXCLUSIVE_EXPORT_PUBLISH_UNSUPPORTED')
        function.argtypes=[ctypes.c_int,ctypes.c_char_p,ctypes.c_int,ctypes.c_char_p,ctypes.c_uint]
        function.restype=ctypes.c_int
        if function(-100,os.fsencode(temporary),-100,os.fsencode(target),1):
            error=ctypes.get_errno(); raise OSError(error,os.strerror(error),str(target))
    else: raise ContractError('EXCLUSIVE_EXPORT_PUBLISH_UNSUPPORTED')

def proof(state,target,entries,**extra):
    atomic_json(state/'verification'/(target.name+'.json'),{'schema':'gc-ng04-owned-verification-v1',
        'inventory_sha256':sha(encode([entries[p] for p in sorted(entries)])),
        'custody':'OWNED_READ_ONLY_PAYLOADS_CONSUMPTION_HASH_REQUIRED',**extra})

def _freeze_input(root,state):
    packages=state/'verified-packages'; receipt=state/'verification'/(root.name+'.json')
    if root.parent==packages and receipt.is_file():
        value=read_json(receipt); entries,_=previous_inventory(root)
        if value.get('schema') not in {'gc-ng03-owned-verification-v1','gc-ng04-owned-verification-v1'} or value.get('inventory_sha256')!=sha(encode([entries[p] for p in sorted(entries)])): raise ContractError('VERIFIED_CUSTODY_METADATA_CHANGED')
        return root,{'mode':'verified_owned_immutable_inventory','novel_payload_reads':0,'novel_payload_hashes':0,'os_copied_files':0,
                     'integrity_scope':'inherited inventory; touched payloads and runtime lazy consumption hash actual bytes'}
    for p in [root,*root.rglob('*')]:
        if p.is_symlink() or (hasattr(p,'is_junction') and p.is_junction()): raise ContractError('IMPORT_LINK_NOT_ALLOWED')
    packages.mkdir(parents=True,exist_ok=True); temp=packages/('.owned-import-'+uuid.uuid4().hex); target=None
    try:
        shutil.copytree(root,temp,copy_function=shutil.copyfile); evidence=verify(temp); entries,_=previous_inventory(temp)
        identity=sha(encode([entries[p] for p in sorted(entries)])); target=packages/(identity+'-'+uuid.uuid4().hex)
        immutable(temp); os.replace(temp,target); proof(state,target,entries,initial_verification=evidence)
    except BaseException:
        if target is not None: discard(target)
        raise
    finally: discard(temp)
    novels=sum(p.startswith('novels/') for p in entries)
    return target,{'mode':'cold_external_import','novel_payload_reads':novels,'novel_payload_hashes':novels,'os_copied_files':len(entries)+2,'integrity_scope':'captured actual bytes; whole staging verification'}

def freeze_input(root,state_root):
    root,state=Path(root).resolve(),Path(state_root).resolve()
    if state.is_relative_to(root): raise ContractError('OUTPUT_OR_STATE_CANNOT_MUTATE_INPUT_PACKAGE')
    with ProcessFileLock(state/'authoring.writer.lock','AUTHORING_STATE_WRITER_BUSY'): return _freeze_input(root,state)


def apply_review(package,review,current_source,output,state_root,*,fault=None,expected_base=None):
    validate_review(review); package,state=Path(package).resolve(),Path(state_root).resolve(); output=Path(output).resolve() if output is not None else None
    if state.is_relative_to(package) or (output and (output.is_relative_to(package) or output.is_relative_to(state))): raise ContractError('OUTPUT_OR_STATE_CANNOT_MUTATE_INPUT_PACKAGE')
    if output and output.exists(): raise ContractError('OUTPUT_MUST_BE_NEW')
    if any(Path(value).resolve()!=package for value in (expected_base,review.get('expected_base')) if value is not None): raise ContractError('EXPECTED_BASE_PACKAGE_MISMATCH')
    with ProcessFileLock(state/'authoring.writer.lock','AUTHORING_STATE_WRITER_BUSY'):
        return _apply_locked(package,review,current_source,output,state,fault)


def _apply_locked(package,review,current_source,output,state,fault):
    owned,import_metrics=_freeze_input(package,state); current=state/'authoring-current.json'
    if current.exists() and Path(read_json(current)['next_package']).resolve()!=owned: raise ContractError('AUTHORING_EXPECTED_BASE_CONFLICT')
    baseline,_=previous_inventory(owned); staging=state/'verified-packages'/('.owned-apply-'+uuid.uuid4().hex)
    generation=None; committed=False; export_temporary=None;commit_token=uuid.uuid4().hex
    changed,docs,outcomes=set(),{},{}; payload_reads=[]; payload_hashes=[]; master_approved=[]
    requested=[(f'{g}:{i}',g,op) for g in ARRAYS for i,op in enumerate(review.get(g,[]))]
    def result(key,group,op,status,reason,**extra):
        if key in outcomes: raise ContractError('REVIEW_OPERATION_CONSUMED_TWICE')
        outcomes[key]={'request_id':key,'review_id':op.get('review_id') if isinstance(op,dict) else group+':'+op,'operation':group,'status':status,'reason':reason,**extra}
    def payload_hash(file,raw):
        payload_hashes.append(file); return sha(raw)
    def document(file):
        if file not in docs:
            raw=safe_path(staging,file).read_bytes(); payload_reads.append(file); desc=baseline.get(file)
            if desc is None or len(raw)!=desc['size'] or payload_hash(file,raw)!=desc['sha256']: raise CustodyIntegrityError('OWNED_CONSUMPTION_IDENTITY_MISMATCH:'+file)
            docs[file]=read_json(staging/file)
            payload_reads.append(file)  # actual read_json reads the bytes again
        return docs[file]
    try:
        # Missing optional source blocks only operations that consume it. A known
        # package-target KO edit and already-held work require no fresh source.
        # This never supplies a fake source snapshot.
        if current_source is None:
            for key,group,op in requested:
                if not isinstance(op,dict) or op.get('decision') in HELD:
                    continue
                observed = group in {'master_key_rows','resource_rows','binding_rows'} or (group=='rows' and 'source_basis' not in op)
                if observed:
                    result(key,group,op,'HELD','CURRENT_OBSERVED_SOURCE_REQUIRED')
        master_requests={}
        for key,group,op in requested:
            if key in outcomes: continue
            if group in {'rows','master_key_rows'} and op.get('target',{}).get('file')=='master.json' and op.get('decision')=='APPROVE': master_requests.setdefault(tuple(op['target'].get('keys',[])),[]).append((key,group,op))
        for uses in master_requests.values():
            if len(uses)>1 and any(group=='master_key_rows' for key,group,op in uses):
                for key,group,op in uses: result(key,group,op,'HELD','MULTIPLE_MASTER_KEY_DECISIONS_REQUIRE_ONE_SCOPE_REVIEW')
        shutil.copytree(owned,staging,copy_function=shutil.copyfile)
        # These bytes are consumed by the builder/structural QA even for empty
        # changes. Verify their inherited identities before metadata can be rebuilt.
        for file in ('ui/resources.json','ui/bindings.json',*[p for p in baseline if p.startswith('layout/')]): document(file)
        for key,group,op in requested:
            if key in outcomes or group in {'register','unregister'}: continue
            if op['decision'] in HELD: result(key,group,op,'HELD',op['decision']); continue
            try:
                if group=='master_key_rows':
                    from update.operator_review import apply_master_key
                    file='master.json';doc=document(file);before=copy.deepcopy(doc);applied=apply_master_key(doc,current_source,op)
                    if applied:
                        keys=op['target']['keys'];master_approved.append({'table':keys[0],'field':keys[1],'exact_jp':keys[2],'old_ko':before[keys[0]][keys[1]][keys[2]],'new_ko':op['new_ko']})
                elif group=='rows':
                    file=op['target']['file']; safe_path(staging,file)
                    if file not in {'master.json','names.json','ui/resources.json'}: raise ReviewConflict('SUPPORTED_REVIEW_TARGET_REQUIRED')
                    doc=document(file); before=copy.deepcopy(doc)
                    if 'source_basis' in op:
                        from update.operator_review import apply_package_ui
                        applied=apply_package_ui(doc,document('ui/bindings.json'),op)
                    else: applied=apply_row(doc,op,current_source)
                    if applied and file=='master.json':
                        keys=op['target']['keys']; parent=before
                        for part in keys[:-1]: parent=parent[part]
                        approval={'table':keys[0],'field':keys[1],'exact_jp':keys[-1],'old_ko':parent.get(keys[-1]),'new_ko':op['new_ko']}
                        previous=next((p for p in master_approved if (p['table'],p['field'],p['exact_jp'])==(keys[0],keys[1],keys[-1])),None)
                        if previous is None: master_approved.append(approval)
                        else: previous['new_ko']=op['new_ko']  # each row's old-KO pin was independently checked
                elif group=='resource_rows':
                    source=find_source(current_source,op); proposal=copy.deepcopy(op['proposal'])
                    if op['lane']!='ui' or proposal['id']!=source['locator'].get('resource') or proposal['jp']!=source['jp'] or proposal['role']!=source['locator'].get('role') or proposal['kind']!=source.get('structure',{}).get('kind'): raise ReviewConflict('RESOURCE_SOURCE_ROLE_MISMATCH')
                    file='ui/resources.json'; doc=document(file); before=copy.deepcopy(doc); matches=[r for r in doc['resources'] if r['id']==proposal['id']]
                    if len(matches)>1 or fingerprint(matches[0] if matches else None)!=op['old_resource_sha256']: raise ReviewConflict('CONCURRENT_RESOURCE_CHANGE')
                    if matches and matches[0]['disposition'] in {'hold','keep_source','known_residual'} and proposal['disposition']=='translate': raise ReviewConflict('PROTECTED_RESOURCE_NOT_AUTOMATICALLY_ACTIVATED')
                    if not matches or matches[0]!=proposal:
                        if matches: doc['resources'].remove(matches[0])
                        doc['resources'].append(proposal)
                    applied=doc!=before
                elif group=='binding_rows':
                    source=find_source(current_source,op); proposal=copy.deepcopy(op['proposal'])
                    if op['lane']!='ui' or proposal['resource_id']!=source['locator'].get('resource') or proposal['source_identity']['exact_jp']!=source['jp']: raise ReviewConflict('BINDING_SOURCE_TARGET_MISMATCH')
                    resources=[r for r in document('ui/resources.json')['resources'] if r['id']==proposal['resource_id']]
                    if len(resources)!=1: raise ReviewConflict('RESOURCE_TARGET_REQUIRED')
                    validate_binding_provenance(source,proposal,resources[0])
                    if resources[0]['disposition']!='translate' and proposal['enabled']: raise ReviewConflict('PROTECTED_RESOURCE_NOT_AUTOMATICALLY_ACTIVATED')
                    file='ui/bindings.json'; doc=document(file); before=copy.deepcopy(doc); matches=[r for r in doc['bindings'] if r['id']==proposal['id']]
                    if len(matches)>1 or fingerprint(matches[0] if matches else None)!=op['old_binding_sha256']: raise ReviewConflict('CONCURRENT_BINDING_CHANGE')
                    if matches and not matches[0]['enabled'] and proposal['enabled']: raise ReviewConflict('PROTECTED_BINDING_NOT_AUTOMATICALLY_ACTIVATED')
                    if op.get('operation')=='remove':
                        if not matches: raise ReviewConflict('REMOVE_BINDING_TARGET_REQUIRED')
                        doc['bindings'].remove(matches[0])
                    elif not matches or matches[0]!=proposal:
                        if matches: doc['bindings'].remove(matches[0])
                        doc['bindings'].append(proposal)
                    applied=doc!=before
                else:
                    file=op['target']['file']
                    if file!='layout/episode-title.json' or op['target']['policy_id']!='episode-title.fit.v1': raise ReviewConflict('COMPILED_TYPED_LAYOUT_REQUIRED')
                    doc=document(file)
                    if fingerprint(doc)!=op['old_layout_sha256']: raise ReviewConflict('CONCURRENT_LAYOUT_CHANGE')
                    validate_shape(read_json(CONTRACTS/'episode-title-fit.schema.json'),op['proposal'])
                    # Existing schema fixes every value. Only valid byte formatting
                    # may be canonicalized; no native/property permission is added.
                    applied=safe_path(staging,file).read_bytes()!=encode(op['proposal']); docs[file]=copy.deepcopy(op['proposal'])
                if applied: changed.add(file)
                result(key,group,op,'APPLIED' if applied else 'NO_CHANGE','REVIEWED_CHANGE_APPLIED' if applied else 'ALREADY_SAME_VALUE')
            except (ReviewConflict,ContractError,KeyError,TypeError) as error:
                reason=str(error);narrow=group=='master_key_rows' or (group=='rows' and 'source_basis' in op)
                status='HELD' if narrow and reason in {'STALE_PACKAGE_TARGET_SOURCE_OR_BINDING','CONCURRENT_KO_CHANGE','STALE_MASTER_KEY_PEERS_OR_CONTEXT','MASTER_KEY_SOURCE_COLLISION','MASTER_KEY_OBSERVED_PEERS_REQUIRED'} else 'REJECTED'
                result(key,group,op,status,reason)
        for file in changed&set(docs): safe_path(staging,file).write_bytes(encode(docs[file]))
        if 'master.json' in changed:
            from reconcile_master import verify_master_delta
            master_guard=verify_master_delta(read_json(owned/'master.json'),docs['master.json'],approved_changes=master_approved,resources=document('ui/resources.json'),bindings=document('ui/bindings.json'))
        else: master_guard={'status':'NOT_RUN_MASTER_UNCHANGED'}
        semantic=None; register=[]; unregister=[]
        for key,g,op in requested:
            if g not in {'register','unregister'}: continue
            if semantic is None: semantic=document('manifest.json')
            if g=='unregister' and op not in semantic['novels']: result(key,g,op,'REJECTED','UNREGISTER_UNKNOWN_NOVEL'); continue
            if g=='register' and 'novels/'+op+'.json' not in baseline and 'novels/'+op+'.json' not in changed: result(key,g,op,'REJECTED','REGISTER_REQUIRES_STORED_NOVEL'); continue
            if g=='register' and op in semantic['novels']: result(key,g,op,'NO_CHANGE','ALREADY_REGISTERED'); continue
            if g=='register': document('novels/'+op+'.json')  # explicit semantic consumption must match verified bytes
            (register if g=='register' else unregister).append(op); result(key,g,op,'APPLIED','EXPLICIT_REGISTRATION_CHANGE')
        if fault: fault('after_payload')
        build(staging,modified=sorted(changed&set(baseline)),added=sorted(changed-set(baseline)),register=register,unregister=unregister)
        if fault: fault('after_metadata')
        qa=validate_package(staging,CONTRACTS); entries,release=previous_inventory(staging)
        delta=sorted(p for p in entries if p not in baseline or entries[p]!=baseline[p])
        for file in delta:
            raw=safe_path(staging,file).read_bytes(); payload_reads.append(file)
            if len(raw)!=entries[file]['size'] or payload_hash(file,raw)!=entries[file]['sha256']: raise ContractError('TOUCHED_QA_HASH_MISMATCH')
        if {p.relative_to(staging).as_posix() for p in staging.rglob('*') if p.is_file()}!=set(entries)|{'ui/manifest.json','release-manifest.json'}: raise ContractError('COMPLETE_CANDIDATE_MEMBERSHIP_MISMATCH')
        if set(outcomes)!={key for key,g,op in requested}: raise ContractError('REVIEW_OPERATION_NOT_CONSUMED')
        if fault: fault('before_publish')
        if delta:
            generation=state/'verified-packages'/('generation-'+uuid.uuid4().hex); immutable(staging); os.replace(staging,generation)
            proof(state,generation,entries,previous_package=str(owned),validated_payloads=delta,target_qa=qa); next_package=generation
        else: next_package=owned
        if fault: fault('after_generation')
        if output:
            output.parent.mkdir(parents=True,exist_ok=True)
            export_temporary=output.with_name('.owned-export-'+uuid.uuid4().hex)
            export_temporary.mkdir()  # exclusive allocation establishes our cleanup ownership
            shutil.copytree(next_package,export_temporary,copy_function=shutil.copyfile,dirs_exist_ok=True)
            if fault: fault('before_export_publish')
            exclusive_directory_publish(export_temporary,output); export_temporary=None
        if fault: fault('before_authoring_select')
        selection={'schema':'gc-ng04-authoring-reference-v1','next_package':str(next_package),'previous_package':str(owned),'revision':release['revision'],'commit_token':commit_token,
                   'previous_commit_token':read_json(current).get('commit_token') if current.exists() else None,'runtime_selection':False}
        atomic_json(state/'authoring-selections'/(commit_token+'.json'),selection)
        atomic_json(current,selection); committed=True
        ordered=[outcomes[key] for key,g,op in requested]; rejected=[o for o in ordered if o['status']=='REJECTED']; held=[o for o in ordered if o['status']=='HELD']
        status='PARTIAL_CONFLICTS' if rejected else ('PARTIAL_HOLDS' if held and delta else 'HELD' if held else 'PASS' if delta else 'NO_CHANGE')
        return {'schema':'gc-ng04-apply-result-v1','status':status,'output':str(output) if output else None,'next_package':str(next_package),'expected_base':str(owned),'verified_input':str(owned),
                'changed_payloads':sorted(changed),'generated_and_payload_delta':delta,'request_outcomes':ordered,'requested_count':len(requested),'consumed_count':len(outcomes),
                'rejected_rows':rejected,'held_rows':[o['review_id'] for o in held],'held_operations':held,'revision':release['revision'],'target_qa':'PASS','master_guard':master_guard,'import_metrics':import_metrics,
                'apply_metrics':{'novel_payload_reads':sum(p.startswith('novels/') for p in payload_reads),'novel_payload_hashes':sum(p.startswith('novels/') for p in payload_hashes),
                                 'unchanged_novel_payload_reads':sum(p.startswith('novels/') and p not in changed for p in payload_reads),'unchanged_novel_payload_hashes':sum(p.startswith('novels/') and p not in changed for p in payload_hashes),'os_copied_files':len(baseline)+2,'os_export_copied_files':len(entries)+2 if output else 0,
                                 'os_copy_payload_bytes':sum(e['size'] for e in baseline.values()),'os_export_copy_payload_bytes':sum(e['size'] for e in entries.values()) if output else 0,
                                 'os_copy_metadata_files':2,'os_hardlinks_created':0,'os_stat_calls':'NOT_INSTRUMENTED','os_copy_counter_scope':'completed copytree payload descriptors; two selection metadata files separate; not OS syscall counts',
                                 'scope':'actual direct apply callsites; builder semantic I/O separately scoped; explicit registration reads are counted as unchanged byte payload consumption'},
                'native':'NOT_RUN','game':'NOT_RUN','semantic_approval':'ONLY_EXPLICIT_REVIEW_DECISIONS'}
    finally:
        discard(staging)
        if export_temporary is not None: discard(export_temporary)
        if not committed:
            if generation is not None: discard(generation); (state/'verification'/(generation.name+'.json')).unlink(missing_ok=True)
            # A successfully published complete export is externally visible.
            # Never delete it by path after publication: another actor may have
            # changed custody. A failed head selection leaves it unselected.
