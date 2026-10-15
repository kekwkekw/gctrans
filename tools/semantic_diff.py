"""Compare trusted immutable descriptors, or explicitly scan untrusted folders.

Trusted mode requires a complete verified inventory and an explicit change set.
It hashes only changed payloads. A mutable external directory must use cold_scan;
mtime and length are never evidence that its bytes are unchanged.
"""
from __future__ import annotations
import argparse, copy, json
from pathlib import Path
from contract_validation import read_json, validate_package, ContractError
from build_manifests import encode, sha, checked_entries, component_kind, safe_path, previous_inventory

def descriptor(root: Path, *, mode: str):
    root = root.resolve(); reads = []
    if mode == 'verified_immutable_inventory':
        inventory, _ = previous_inventory(root)
        entries = [e for p,e in inventory.items() if p != 'legacy-components.json']
    elif mode == 'cold_scan':
        entries = []
        for path in sorted(p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()):
            if path in {'ui/manifest.json','release-manifest.json','legacy-components.json'}: continue
            kind = component_kind(path); raw = safe_path(root,path).read_bytes(); reads.append(path)
            entries.append({'path':path,'size':len(raw),'sha256':sha(raw),'kind':kind})
    else: raise ContractError('EXPLICIT_SNAPSHOT_MODE_REQUIRED')
    entries = sorted(entries,key=lambda x:x['path'])
    return {'schema':'gc-ng02-snapshot-v1','complete_inventory':True,
            'trust_basis':mode,'inventory_sha256':sha(encode(entries)),'files':entries,
            'payload_reads':reads}

def checked_descriptor(root, value):
    if value.get('schema') != 'gc-ng02-snapshot-v1' or value.get('complete_inventory') is not True or value.get('trust_basis') not in {'verified_immutable_inventory','cold_scan'}:
        raise ContractError('UNTRUSTED_OR_INCOMPLETE_DESCRIPTOR')
    entries = checked_entries(root, {'schema_version':1,'files':value['files']})
    normalized = [entries[p] for p in sorted(entries)]
    if value.get('inventory_sha256') != sha(encode(normalized)): raise ContractError('DESCRIPTOR_INVENTORY_MISMATCH')
    return entries

def inventory_changes(a,b):
    return {'add':sorted(set(b)-set(a)), 'remove':sorted(set(a)-set(b)),
            'modify':sorted(p for p in set(a)&set(b) if a[p] != b[p])}

def diff(old: Path, new: Path, *, mode='cold_scan', old_descriptor=None, new_descriptor=None, changeset=None):
    old = old.resolve(); new = new.resolve(); payload_reads = {'old':[],'new':[]}
    if mode == 'cold_scan':
        old_descriptor = descriptor(old,mode='cold_scan'); new_descriptor = descriptor(new,mode='cold_scan')
        payload_reads = {'old':old_descriptor['payload_reads'],'new':new_descriptor['payload_reads']}
    elif mode == 'trusted':
        if old_descriptor is None or new_descriptor is None or changeset is None: raise ContractError('TRUSTED_DESCRIPTORS_AND_CHANGESET_REQUIRED')
    else: raise ContractError('EXPLICIT_COMPARISON_MODE_REQUIRED')
    av = checked_descriptor(old,old_descriptor); bv = checked_descriptor(new,new_descriptor)
    delta = inventory_changes(av,bv)
    if mode == 'trusted':
        if not isinstance(changeset,dict) or set(changeset) != {'add','modify','remove'}: raise ContractError('EXPLICIT_CHANGESET_OPERATIONS_REQUIRED')
        declared = {}
        for op, paths in changeset.items():
            if not isinstance(paths,list) or len(set(paths)) != len(paths): raise ContractError('INVALID_CHANGESET')
            for path in paths: safe_path(new,path)
            declared[op] = sorted(paths)
        if declared != delta: raise ContractError('CHANGESET_INVENTORY_DISAGREEMENT')
        for path in delta['add']+delta['modify']:
            raw = safe_path(new,path).read_bytes(); payload_reads['new'].append(path)
            if len(raw) != bv[path]['size'] or sha(raw) != bv[path]['sha256']: raise ContractError('CHANGED_PAYLOAD_DESCRIPTOR_MISMATCH:'+path)
        for path in delta['remove']:
            if safe_path(new,path).exists(): raise ContractError('EXPLICIT_REMOVED_PATH_STILL_PRESENT:'+path)
    contracts = Path(__file__).resolve().parents[1]/'contracts'
    if contracts.exists(): validate_package(old,contracts); validate_package(new,contracts)
    changes = []; ko = []
    for file, collection in [('ui/resources.json','resources'),('ui/bindings.json','bindings')]:
        a = read_json(old/file); b = read_json(new/file)
        am = {x['id']:x for x in a[collection]}; bm = {x['id']:x for x in b[collection]}
        for key in sorted(set(am)|set(bm)):
            if key not in am: changes.append({'kind':'added','file':file,'id':key}); continue
            if key not in bm: changes.append({'kind':'removed','file':file,'id':key}); continue
            x,y = copy.deepcopy(am[key]),copy.deepcopy(bm[key])
            if x == y: continue
            if collection == 'resources':
                oldko = x.pop('ko',None); newko = y.pop('ko',None)
                if x == y and oldko != newko: ko.append({'id':key,'before':oldko,'after':newko}); continue
            changes.append({'kind':'structural','file':file,'id':key,'before':am[key],'after':bm[key]})
        if {k:v for k,v in a.items() if k != collection} != {k:v for k,v in b.items() if k != collection}:
            changes.append({'kind':'document_metadata','file':file})
    # Descriptor comparisons preserve byte identity, even equal-length changes.
    for op, paths in delta.items():
        for path in paths:
            if path.startswith('layout/'): changes.append({'kind':'layout','file':path,'operation':op})
            elif path in {'master.json','names.json','manifest.json'} or path.startswith('novels/'):
                changes.append({'kind':'legacy_component','file':path,'operation':op})
    novel_reads = sum(path.startswith('novels/') for paths in payload_reads.values() for path in paths)
    authority_change = any(x['kind'] == 'legacy_component' for x in changes)
    return {'classification':'no_change' if not changes and not ko else 'text_only' if not changes else 'structural',
            'ko_changes':ko,'structural_changes':changes,'dll_rebuild_required_for_supported_adapter':False,
            'activation_change':any(x.get('file') == 'ui/bindings.json' for x in changes),
            'runtime_action':'RESTART_REQUIRED' if authority_change else 'UI_RELOAD_OR_NOOP',
            'comparison_mode':mode,'trust_condition':'complete verified immutable inventories plus exact declared changes' if mode == 'trusted' else 'explicit full payload scan of external mutable input',
            'inventory_delta':delta,'payload_reads':payload_reads,'novel_payload_reads':novel_reads,
            'read_count_scope':'comparison payload hashes; small UI semantic validation and inventory metadata reads are separate'}

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__); p.add_argument('old',type=Path); p.add_argument('new',type=Path)
    p.add_argument('--mode',choices=['trusted','cold_scan'],required=True)
    p.add_argument('--old-descriptor',type=Path); p.add_argument('--new-descriptor',type=Path); p.add_argument('--changeset',type=Path); p.add_argument('--report',type=Path)
    a = p.parse_args(); r = diff(a.old,a.new,mode=a.mode,
        old_descriptor=read_json(a.old_descriptor) if a.old_descriptor else None,
        new_descriptor=read_json(a.new_descriptor) if a.new_descriptor else None,
        changeset=read_json(a.changeset) if a.changeset else None)
    out = json.dumps(r,ensure_ascii=False,indent=2)+'\n'
    if a.report: a.report.parent.mkdir(parents=True,exist_ok=True); a.report.write_text(out,encoding='utf-8')
    print(out)
