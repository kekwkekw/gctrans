"""Explicit once-per-staging whole-package gate, including lazy legacy payload bytes.

This is an offline packaging check, not an eager runtime Novel loader. Normal UI
updates can continue to use build_manifests.py with changed-file scope.
"""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, sys
# This standalone entrypoint also supports isolated Python (-I -S). Resolve its
# sibling imports from the returned source, without workspace/PYTHONPATH trust.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from contract_validation import read_json, validate_package

def verify(root:Path):
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
            'bytes_verified':total,'membership':'EXACT','validation_scope':'all staged payload bytes; offline only',
            'runtime_names_novel_scope':'legacy-index metadata only; existing donor lazy behavior preserved',
            'native_runtime':'NOT_RUN_OFFLINE_STAGING_ONLY'}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--report',type=Path,required=True)
    a=p.parse_args()
    try:r=verify(a.root)
    except Exception as e:r={'schema':'gc-ng01-offline-staging-gate-v1','status':'FAIL','reason':str(e)}
    a.report.parent.mkdir(parents=True,exist_ok=True)
    a.report.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(r,ensure_ascii=False));raise SystemExit(0 if r['status']=='PASS' else 1)
