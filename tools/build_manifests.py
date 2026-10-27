"""Build from current release/index and explicit add/modify/remove declarations.

The historical receipt bootstraps only the first import. Follow-up builds never
read unchanged Novel payloads or edit the legacy Novel registration manifest.
"""
from __future__ import annotations
import argparse, hashlib, json, re, os, uuid
from pathlib import Path, PurePosixPath
from contract_validation import read_json, ContractError
from legacy_semantic import compute as legacy_hash, root_hash

def sha(raw): return hashlib.sha256(raw).hexdigest()
def encode(x): return (json.dumps(x, ensure_ascii=False, sort_keys=True, indent=2)+'\n').encode('utf-8')

def canonical_path(value: str):
    if not isinstance(value, str) or not value or value.startswith('/') or '\\' in value or ':' in value or any(x in {'', '.', '..'} for x in value.split('/')):
        raise ContractError('PATH_TRAVERSAL')
    p = PurePosixPath(value)
    if p.is_absolute(): raise ContractError('PATH_TRAVERSAL')
    return p

def safe_path(root: Path, value: str):
    p = canonical_path(value)
    target = (root/p).resolve()
    if not target.is_relative_to(root.resolve()): raise ContractError('PATH_TRAVERSAL')
    return target

def component_kind(path):
    if path == 'ui.json': return 'general_ui'
    if path == 'ui.templates.json': return 'general_ui_templates'
    if path == 'master.json': return 'master'
    if path == 'names.json': return 'names'
    if path == 'manifest.json': return 'legacy_manifest'
    if re.fullmatch(r'novels/[0-9]+\.json', path): return 'novel'
    if path == 'ui/resources.json': return 'resources'
    if path == 'ui/bindings.json': return 'bindings'
    if re.fullmatch(r'layout/[^/]+\.json', path): return 'layout'
    if path == 'legacy-components.json': return 'legacy_index'
    raise ContractError('UNSUPPORTED_COMPONENT_PATH:'+path)

def checked_entries(root, document):
    if type(document.get('schema_version')) is not int or document['schema_version'] not in (1, 2) or not isinstance(document.get('files'), list):
        raise ContractError('INVALID_INVENTORY_DOCUMENT')
    entries = {}
    for item in document['files']:
        if not isinstance(item, dict) or set(item) != {'path','size','sha256','kind'}: raise ContractError('INVALID_INVENTORY_ENTRY')
        # Metadata declarations need lexical validation. Resolve a filesystem
        # target only when payload bytes are actually read or written.
        path = item['path']; canonical_path(path); kind = component_kind(path)
        if path in entries: raise ContractError('DUPLICATE_INVENTORY_PATH:'+path)
        if type(item['size']) is not int or item['size'] < 0 or not isinstance(item['sha256'], str) or not re.fullmatch('[0-9a-f]{64}', item['sha256']):
            raise ContractError('INVALID_INVENTORY_IDENTITY:'+path)
        if item['kind'] != kind and not (item['kind'] == 'legacy' and kind in {'master','names','legacy_manifest','novel'}):
            raise ContractError('INVALID_INVENTORY_KIND:'+path)
        entries[path] = {**item, 'kind':kind}
    return entries

def previous_inventory(root):
    """Check metadata correspondence without rereading lazy legacy payloads."""
    release = read_json(root/'release-manifest.json')
    if release.get('profile') != 'NextgenCandidate' or release.get('locale') != 'ko': raise ContractError('INVALID_PREVIOUS_RELEASE')
    entries = checked_entries(root, release)
    raw = (root/'legacy-components.json').read_bytes()
    descriptor = entries.get('legacy-components.json')
    if descriptor is None or descriptor['sha256'] != sha(raw) or descriptor['size'] != len(raw): raise ContractError('PREVIOUS_INDEX_IDENTITY_MISMATCH')
    index = checked_entries(root, read_json(root/'legacy-components.json'))
    expected = {p:e for p,e in entries.items() if e['kind'] in {'names','legacy_manifest','novel'}}
    if index != expected: raise ContractError('PREVIOUS_INDEX_RELEASE_DIVERGENCE')
    manifest = read_json(root/'ui/manifest.json'); ui = checked_entries(root, manifest)
    if release.get('schema_version') == 2:
        binding = release.get('ui_manifest')
        raw_ui = (root/'ui/manifest.json').read_bytes()
        if not isinstance(binding, dict) or set(binding) != {'path','size','sha256'} or binding['path'] != 'ui/manifest.json' or binding['size'] != len(raw_ui) or binding['sha256'] != sha(raw_ui):
            raise ContractError('PREVIOUS_UI_DESCRIPTOR_MISMATCH')
    expected_ui = {p:e for p,e in entries.items() if e['kind'] in {'general_ui_templates','general_ui','resources','bindings','layout','master','legacy_index'}}
    if manifest.get('revision') != release.get('revision') or ui != expected_ui: raise ContractError('PREVIOUS_UI_RELEASE_DIVERGENCE')
    return entries, release

def publish_metadata(root, values):
    """Owned metadata temporaries; rollback a failed local authoring update.

    Reviewed apply calls this inside a disposable, unselected staging tree, then
    publishes the entire completed directory. No runtime selection occurs here.
    """
    operation = uuid.uuid4().hex
    old = {name: safe_path(root, name).read_bytes() if safe_path(root, name).exists() else None for name in values}
    temps, committed = {}, []
    try:
        for name, raw in values.items():
            target = safe_path(root, name)
            temporary = target.with_name('.ng03-meta-' + operation + '-' + target.name)
            temps[name] = temporary
            with temporary.open('xb') as handle:
                handle.write(raw); handle.flush(); os.fsync(handle.fileno())
        for name in values:
            os.replace(temps[name], safe_path(root, name)); committed.append(name)
    except BaseException:
        for name in reversed(committed):
            target = safe_path(root, name)
            if old[name] is None:
                target.unlink(missing_ok=True)
            else:
                recovery = target.with_name('.ng03-meta-' + operation + '-rollback-' + target.name)
                recovery.write_bytes(old[name]); os.replace(recovery, target)
        raise
    finally:
        for temporary in temps.values():
            temporary.unlink(missing_ok=True)

def build(root: Path, legacy_receipt: Path | None = None, changed=(), *, added=(), modified=(), removed=(), register=(), unregister=()):
    root = root.resolve()
    if (root/'release-manifest.json').exists():
        entries, release_metadata = previous_inventory(root)
    else:
        if legacy_receipt is None: raise ContractError('BOOTSTRAP_RECEIPT_REQUIRED_WITHOUT_PREVIOUS_RELEASE')
        receipt = read_json(legacy_receipt); entries = {}
        for item in receipt['files']:
            path = item['path']; canonical_path(path)
            if path in entries: raise ContractError('DUPLICATE_BOOTSTRAP_PATH:'+path)
            entries[path] = {'path':path, 'size':item['bytes'], 'sha256':item['sha256'], 'kind':component_kind(path)}
        entries = checked_entries(root, {'schema_version':1,'files':list(entries.values())}); release_metadata = {}
    add, modify, remove = list(added), list(modified), list(removed)
    # Compatibility option --changed explicitly declares add or modify.
    for path in changed: (modify if path in entries else add).append(path)
    operations = add+modify+remove
    if len(set(operations)) != len(operations): raise ContractError('CONFLICTING_CHANGE_OPERATIONS')
    for path in operations:
        safe_path(root, path)
        if component_kind(path) == 'legacy_index': raise ContractError('GENERATED_INDEX_CANNOT_BE_CHANGED')
    for path in remove:
        if path not in entries: raise ContractError('REMOVE_UNKNOWN_PATH:'+path)
        if path in {'master.json','names.json','manifest.json','ui/resources.json','ui/bindings.json'}: raise ContractError('REMOVE_REQUIRED_COMPONENT:'+path)
        if safe_path(root,path).exists(): raise ContractError('REMOVED_COMPONENT_STILL_PRESENT:'+path)
        entries.pop(path)
    for path in add:
        if path in entries: raise ContractError('ADD_EXISTING_PATH:'+path)
    for path in modify:
        if path not in entries: raise ContractError('MODIFY_UNKNOWN_PATH:'+path)
    register, unregister = list(register), list(unregister)
    if len(set(register+unregister)) != len(register+unregister) or any(not re.fullmatch(r'[0-9]{1,16}', item) for item in register+unregister):
        raise ContractError('INVALID_REGISTRATION_OPERATION')
    semantic = read_json(root/'manifest.json')
    if not isinstance(semantic.get('novels'), dict) or semantic.get('hash') != root_hash(semantic):
        raise ContractError('LEGACY_ROOT_HASH_MISMATCH')
    semantic_changed = False
    for item in unregister:
        if item not in semantic['novels']: raise ContractError('UNREGISTER_UNKNOWN_NOVEL')
        semantic['novels'].pop(item); semantic_changed = True
    for path in remove:
        if path.startswith('novels/') and Path(path).stem in semantic['novels']:
            raise ContractError('REGISTERED_NOVEL_REMOVE_REQUIRES_UNREGISTER')
    for path in add+modify:
        if path in {'master.json','names.json'}:
            value = legacy_hash(read_json(safe_path(root, path)))
            if semantic.get(path[:-5]) != value: semantic[path[:-5]] = value; semantic_changed = True
        elif path.startswith('novels/') and Path(path).stem in semantic['novels']:
            value = legacy_hash(read_json(safe_path(root, path))); identity = Path(path).stem
            if semantic['novels'][identity] != value: semantic['novels'][identity] = value; semantic_changed = True
    for item in register:
        path = 'novels/' + item + '.json'
        if path not in entries and path not in add: raise ContractError('REGISTER_REQUIRES_STORED_NOVEL')
        value = legacy_hash(read_json(safe_path(root, path)))
        if semantic['novels'].get(item) != value: semantic['novels'][item] = value; semantic_changed = True
    semantic_raw = None
    if semantic_changed:
        semantic['hash'] = root_hash(semantic)
        semantic_raw = encode(semantic)
        entries['manifest.json'] = {'path':'manifest.json','size':len(semantic_raw),'sha256':sha(semantic_raw),'kind':'legacy_manifest'}
    for path in add+modify:
        if path == 'manifest.json' and semantic_raw is not None: continue
        raw = safe_path(root, path).read_bytes()
        entries[path] = {'path':path,'size':len(raw),'sha256':sha(raw),'kind':component_kind(path)}
    current_ui = ['ui/resources.json','ui/bindings.json']
    current_ui += sorted(p.relative_to(root).as_posix() for p in (root/'layout').glob('*.json'))
    for path in current_ui:
        if path in remove: raise ContractError('REMOVED_COMPONENT_STILL_PRESENT:'+path)
        raw = safe_path(root, path).read_bytes()
        entries[path] = {'path':path,'size':len(raw),'sha256':sha(raw),'kind':component_kind(path)}
    for path in entries:
        if path != 'legacy-components.json' and not (root/canonical_path(path)).is_file(): raise ContractError('MISSING_COMPONENT_REQUIRES_EXPLICIT_REMOVE:'+path)
    indexed = [entries[p] for p in sorted(entries) if entries[p]['kind'] in {'names','legacy_manifest','novel'}]
    index_raw = encode({'schema_version':1,'files':indexed})
    entries['legacy-components.json'] = {'path':'legacy-components.json','kind':'legacy_index','sha256':sha(index_raw),'size':len(index_raw)}
    files = [entries[p] for p in sorted(entries) if entries[p]['kind'] in {'general_ui_templates','general_ui','resources','bindings','layout','master','legacy_index'}]
    revision = 'ng03-'+sha(encode(files))[:24]
    previous_ui = read_json(root/'ui/manifest.json') if (root/'ui/manifest.json').exists() else {}
    manifest = {'schema_version':1,'locale':'ko','origin':previous_ui.get('origin','local'),'revision':revision,'files':files}
    ui_raw = encode(manifest)
    release = {'schema_version':2,'profile':'NextgenCandidate',
               'status':release_metadata.get('status','OFFLINE_PARTIAL_NOT_ACCEPTED_REPLACEMENT'),
               'locale':'ko','revision':revision,'legacy_manifest_builder_input':'current release/index; explicit add/modify/remove; optional initial bootstrap receipt',
               'legacy_index_validation_scope':'descriptor identity; unchanged lazy payload bytes carried from verified previous inventory',
               'ui_manifest':{'path':'ui/manifest.json','size':len(ui_raw),'sha256':sha(ui_raw)},'files':[entries[p] for p in sorted(entries)]}
    values = {'legacy-components.json':index_raw, 'ui/manifest.json':ui_raw, 'release-manifest.json':encode(release)}
    if semantic_raw is not None: values = {'manifest.json':semantic_raw, **values}
    publish_metadata(root, values)
    return manifest

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__); p.add_argument('root',type=Path)
    p.add_argument('--legacy-receipt',type=Path,help='first bootstrap only; ignored with a previous release/index')
    for name in ['changed','add','modify','remove']: p.add_argument('--'+name,action='append',default=[])
    for name in ['register','unregister']: p.add_argument('--'+name,action='append',default=[])
    a = p.parse_args(); m = build(a.root,a.legacy_receipt,a.changed,added=a.add,modified=a.modify,removed=a.remove,register=a.register,unregister=a.unregister)
    print(json.dumps({'revision':m['revision'],'ui_components':len(m['files'])}))
