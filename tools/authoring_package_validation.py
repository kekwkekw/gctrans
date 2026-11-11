"""Whole-package checks for review imports and staged Novel updates."""
from pathlib import Path, PurePosixPath
import hashlib
from contract_validation import read_json, validate_package

def verify(root:Path, *, source_tree=False):
    root=root.resolve();release=read_json(root/'release-manifest.json')
    if release.get('profile')!='NextgenCandidate':raise ValueError('PROFILE_NOT_CANDIDATE')
    entries={};total=0
    for entry in release['files']:
        path=entry['path'];parts=PurePosixPath(path)
        if not path or path.startswith('/') or '\\' in path or ':' in path or any(x in {'.','..'} for x in path.split('/')):
            raise ValueError('UNSAFE_RELEASE_PATH')
        if path in entries:raise ValueError('DUPLICATE_RELEASE_PATH')
        target=(root/parts).resolve()
        if not target.is_relative_to(root):raise ValueError('RELEASE_PATH_ESCAPE')
        raw=target.read_bytes();total+=len(raw)
        if len(raw)!=entry['size'] or hashlib.sha256(raw).hexdigest()!=entry['sha256']:
            raise ValueError('PAYLOAD_MISMATCH:'+path)
        entries[path]=entry
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    if source_tree:
        actual -= {'ui/general-exact.reviewed.json',
                   'ui/detail-families.json', 'ui/enhancement-families.json'}
    expected=set(entries)|{'ui/manifest.json','release-manifest.json'}
    if actual!=expected:raise ValueError('RELEASE_MEMBERSHIP_MISMATCH:'+str(sorted(actual^expected)))
    manifest=read_json(root/'ui/manifest.json')
    if release.get('schema_version') == 2:
        descriptor=release.get('ui_manifest');raw=(root/'ui/manifest.json').read_bytes()
        if not isinstance(descriptor,dict) or descriptor != {'path':'ui/manifest.json','size':len(raw),'sha256':hashlib.sha256(raw).hexdigest()}:
            raise ValueError('RELEASE_UI_DESCRIPTOR_MISMATCH')
    for entry in manifest['files']:
        counterpart=entries.get(entry['path'])
        if not counterpart or any(entry[k]!=counterpart[k] for k in ['path','size','sha256']):
            raise ValueError('UI_RELEASE_MANIFEST_DIVERGENCE')
    index=read_json(root/'legacy-components.json')
    for entry in index['files']:
        counterpart=entries.get(entry['path'])
        if not counterpart or any(entry[k]!=counterpart[k] for k in ['path','size','sha256']):
            raise ValueError('LEGACY_RELEASE_MANIFEST_DIVERGENCE')
    validate_package(root,Path(__file__).resolve().parents[1]/'contracts')
    return {'schema':'gc-ng01-offline-staging-gate-v1','status':'PASS','files_verified':len(entries),
            'bytes_verified':total,'membership':'EXACT_RUNTIME_WITH_AUTHORING' if source_tree else 'EXACT',
            'validation_scope':'all staged payload bytes; offline only',
            'runtime_names_novel_scope':'legacy-index metadata only; existing donor lazy behavior preserved',
            'native_runtime':'NOT_RUN_OFFLINE_STAGING_ONLY'}
