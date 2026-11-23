"""Rebuild GCMod 7.3.2 full/source bundles from verified baseline dependencies.

The update ZIP is produced by tools/gcmod_release.py. Loader, Utility, font,
and dependency sources are reused byte-for-byte from the verified baseline.
Notices are selected strictly by the current component allowlist.
"""
import argparse,hashlib,json,zipfile
from pathlib import Path,PurePosixPath

VERSION='7.3.2'
BASE_FULL='44a496317f8d4a1fb51b505e07fc7a2ab4f26a7e7f1e83e20c40b75145ad06cc'
BASE_SOURCES='a07dc14c6f10101db4932a52d9f75ab72ec8d36ea09c635bc3e4d0bdb674e277'
GCMOD_DLL_SHA='37402bf49a4d34821d1b7278148066f09fe6582504f3e7654454fe13102aeaf7'
P='BepInEx/plugins/GCMod/'
DEPS={P+'Utility.dll':'a842b1c9393285e687379101081d6a43a076da5f3440c754b2a56c0fe1bcf4f0',
      P+'fonts/RHR_Medium_GFSAFE_ADH.bundle':'b2087a14149d7d2de92c675e0ce6c04c722fcb53f227c5427e288d00eacffe80'}
BOOT={'.doorstop_version','doorstop_config.ini','winhttp.dll'}
NOTICE_NAMES=frozenset({
    'AsmResolver-LICENSE.txt','AssetRipper-CIL-LICENSE.txt','AssetRipper-Primitives-LICENSE.txt',
    'BepInEx-LICENSE.txt','Capstone-LICENSE.txt','Capstone-LLVM-LICENSE.txt','Capstone-NET-LICENSE.txt',
    'Cpp2IL-LICENSE.txt','Disarm-LICENSE.txt','Dobby-LICENSE.txt','GCMod-upstream-MIT.txt',
    'HarmonyX-LICENSE.txt','Iced-LICENSE.txt','Il2CppInterop-LICENSE.txt','MonoCecil-LICENSE.txt',
    'MonoMod-helpers-LICENSE.txt','MonoMod-legacy-LICENSE.txt','ResourceHanRounded-OFL-1.1.txt',
    'SemanticVersioning-LICENSE.txt','UnityDoorstop-LICENSE.txt','dotnet-LICENSE.txt',
    'dotnet-PATENTS.txt','dotnet-THIRD-PARTY-NOTICES.txt'})
UPSTREAM_MIT_SHA='606688d2ae1f70514f42b8e27b3b9a1201cce3263d1349754426c273ee4fb732'
APPROVED_ROOT_NOTICES=frozenset({'GCMod-upstream-LICENSE','THIRD_PARTY_NOTICES_GCMod.txt'})
APPROVED_COPYRIGHT_LINES=frozenset({
    'Original GCMod copyright (c) 2024 Jitsu; MIT. GCMod-upstream-MIT.txt.',
    'License copies including copyright notices are in GCMod-licenses/, named by component.',
    'Resource Han Rounded KR Medium 0.990, source copyright 2018\u20132019 Cyano Hao;',
    "Source Han Sans copyright 2014, 2015, 2018 Adobe, reserved name 'Source'.",
})
NOTICE_MARKERS=('license','mit','notice','copying','patent')

def approved_notice(path,prefix):
    return path.startswith(prefix) and path[len(prefix):] in NOTICE_NAMES

def verify_notices(files,prefix):
    if {n[len(prefix):] for n in files if n.startswith(prefix)} != NOTICE_NAMES:
        raise ValueError('DISTRIBUTION_NOTICE_MEMBERSHIP')
    if sha(files[prefix+'GCMod-upstream-MIT.txt'])!=UPSTREAM_MIT_SHA:
        raise ValueError('UPSTREAM_NOTICE_BYTES')
    for name in files:
        if name.startswith(prefix) or name in APPROVED_ROOT_NOTICES or name.startswith(('BepInEx/','dotnet/')):
            continue
        base=PurePosixPath(name).name.casefold()
        if any(m in base for m in NOTICE_MARKERS):
            raise ValueError('UNAPPROVED_DISTRIBUTION_NOTICE')
    raw=files.get('THIRD_PARTY_NOTICES_GCMod.txt')
    if not raw:
        raise ValueError('MISSING_DISTRIBUTION_ATTRIBUTION')
    lines=[line.strip() for line in raw.decode('utf-8').splitlines()]
    if 'Original GCMod copyright (c) 2024 Jitsu; MIT. GCMod-upstream-MIT.txt.' not in lines:
        raise ValueError('UPSTREAM_ATTRIBUTION_MISSING')
    if any('copyright' in line.casefold() and line not in APPROVED_COPYRIGHT_LINES for line in lines):
        raise ValueError('UNAPPROVED_DISTRIBUTION_ATTRIBUTION')
def sha(raw):return hashlib.sha256(raw).hexdigest()
def jb(value):return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf-8')
def safe(name):
    p=PurePosixPath(name)
    assert not p.is_absolute() and '..' not in p.parts and '\\' not in name
def archive(path,files):
    assert not path.exists(),'NEW_OUTPUT_REQUIRED'
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for name,raw in sorted(files.items()):
            safe(name);info=zipfile.ZipInfo(name,(2026,11,20,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
            info.external_attr=0o644<<16;z.writestr(info,raw)
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None and set(z.namelist())==set(files)
        assert all(z.read(name)==raw for name,raw in files.items())
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    for name in ['baseline-full','baseline-sources','update','gcmod-source','docs','output']:ap.add_argument('--'+name,type=Path,required=True)
    ap.add_argument('--commit',required=True)
    ap.add_argument('--packaging-commit')
    ap.add_argument('--update-sha256');a=ap.parse_args()
    assert sha(a.baseline_full.read_bytes())==BASE_FULL
    assert sha(a.baseline_sources.read_bytes())==BASE_SOURCES
    update_sha=sha(a.update.read_bytes())
    if a.update_sha256:
        assert update_sha==a.update_sha256
    assert len(a.commit)==40 and all(c in '0123456789abcdef' for c in a.commit)
    if a.packaging_commit:
        assert len(a.packaging_commit)==40 and all(c in '0123456789abcdef' for c in a.packaging_commit)
    files={};baseline_hashes={}
    with zipfile.ZipFile(a.baseline_full) as z:
        baseline=json.loads(z.read('GCMod-package.json'))
        assert set(z.namelist())==set(baseline['files'])|{'GCMod-package.json'}
        for name,row in baseline['files'].items():
            safe(name);raw=z.read(name);assert len(raw)==row['size'] and sha(raw)==row['sha256']
            if name.startswith(('BepInEx/core/','dotnet/')) or approved_notice(name,'GCMod-licenses/') or name in BOOT or name in DEPS:
                files[name]=raw;baseline_hashes[name]=sha(raw)
    for name,digest in DEPS.items():assert sha(files[name])==digest
    with zipfile.ZipFile(a.update) as z:
        update=json.loads(z.read('package.json'));assert update['kind']=='public-update'
        assert set(z.namelist())==set(update['files'])|{'package.json'}
        for name,row in update['files'].items():
            safe(name);raw=z.read(name);assert len(raw)==row['size'] and sha(raw)==row['sha256']
            if name=='README_KO.txt':continue
            assert name.startswith('BepInEx/') or name=='GCMod-upstream-LICENSE'
            files[name]=raw
    install=(a.docs/'INSTALL_KO.txt').read_bytes()
    files['INSTALL_KO.txt']=install;files['README_GCMod_KO.txt']=install
    for name in ['THIRD_PARTY_NOTICES_GCMod.txt','DATA_UPDATES_KO.txt','IMAGE_UPDATES_KO.txt']:
        files[name]=(a.docs/name).read_bytes()
    verify_notices(files,'GCMod-licenses/')
    assert all(sha(files[name])==digest for name,digest in baseline_hashes.items())
    assert not any(n.startswith(('BepInEx/interop/','BepInEx/cache/','BepInEx/unity-libs/','GC_Data/')) or
                   n in {'GC.exe','GameAssembly.dll','UnityPlayer.dll'} for n in files)
    assert sorted(n for n in files if n.startswith('BepInEx/plugins/') and n.endswith('.dll'))==[P+'GCMod.dll',P+'Utility.dll']
    assert sha(files[P+'GCMod.dll'])==GCMOD_DLL_SHA
    assert len(json.loads(files[P+'translations/ko/ui.json']))==14052
    assert len(json.loads(files[P+'translations/ko/images.json'])['entries'])==179
    assert len([n for n in files if n.startswith(P+'translations/ko/novels/')])==1939
    manifest=dict(schema='gc-public-full-package-v1',kind='public-full',version=VERSION,commit=a.commit,
        packaging_commit=a.packaging_commit,notice_policy='current-component-selection-v1',
        upgrade_from=['7.2.1'],baseline_full_sha256=BASE_FULL,unchanged_dependencies=DEPS,
        first_run_generates=['BepInEx/interop','BepInEx/unity-libs','BepInEx/cache'],
        files={n:dict(sha256=sha(raw),size=len(raw)) for n,raw in sorted(files.items())})
    files['GCMod-package.json']=jb(manifest)
    with zipfile.ZipFile(a.baseline_sources) as z:
        sources={n:z.read(n) for n in z.namelist() if approved_notice(n,'licenses/') or n in {
            'BepInEx-source.zip','Il2CppInterop-source.zip','UnityDoorstop-source.zip','official-downloads.json'}}
    for name in ['INSTALL_KO.txt','THIRD_PARTY_NOTICES_GCMod.txt','DATA_UPDATES_KO.txt','IMAGE_UPDATES_KO.txt','BUILD_INPUTS.txt']:
        sources[name]=(a.docs/name).read_bytes()
    sources['GCMod-source.zip']=a.gcmod_source.read_bytes()
    sources['build_full_release.py']=Path(__file__).read_bytes()
    verify_notices(sources,'licenses/')
    a.output.mkdir(parents=True,exist_ok=True)
    full=a.output/f'GCMod-{VERSION}-ko-full-win-x64.zip';src=a.output/f'GCMod-{VERSION}-sources-notices.zip'
    archive(full,files);archive(src,sources)
    report=dict(status='PASS',version=VERSION,source_commit=a.commit,packaging_commit=a.packaging_commit,full_files=len(files),
        unchanged_dependency_and_notice_files=len(baseline_hashes),direct_upgrade_from='7.2.1',
        baseline_full_sha256=BASE_FULL,update_sha256=update_sha,
        full=dict(name=full.name,size=full.stat().st_size,sha256=sha(full.read_bytes())),
        sources=dict(name=src.name,size=src.stat().st_size,sha256=sha(src.read_bytes())),
        game_executables_interop_cache_credentials='EXCLUDED',cold_start_native='NOT_RUN')
    (a.output/'assembly-provenance.json').write_bytes(jb(report))
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
