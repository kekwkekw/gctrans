"""Build/verify LocalParity archives and install or roll back with the game closed."""
import argparse
import configparser
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

sys.path.insert(0,str(Path(__file__).resolve().parent))
from export_local_ui import export, read
from build_manifests import previous_inventory
from update.process_lock import ProcessFileLock
from update.source_scan import atomic_json

PLUGIN='BepInEx/plugins/GCMod/'
CFG='BepInEx/config/GCMod.cfg'
CORE=('manifest.json','master.json','names.json','ui.json')
def digest(raw):return hashlib.sha256(raw).hexdigest()
def json_bytes(value):return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf-8')
def cfg_read(raw):
    cfg=configparser.ConfigParser(interpolation=None);cfg.optionxform=str;cfg.read_string(raw.decode('utf-8-sig'));return cfg
def required_cfg(cfg):
    return cfg.get('Translation','Language')=='ko' and cfg.get('Translation','CDN')=='offline' and cfg.get('Translation','Enabled').lower()=='true' and cfg.get('Translation.MasterData','EnabledTables')=='*'


def verify_repo(repo):
    package=repo/'translations/ko';payload,info=export(package)
    if payload!=(package/'ui.json').read_bytes():raise ValueError('UI_EXPORT_STALE')
    previous_inventory(package)
    manifest=read(package/'release-manifest.json')
    for row in manifest['files']+[manifest['ui_manifest']]:
        target=(package/row['path']).resolve()
        if not target.is_relative_to(package.resolve()):raise ValueError('MANIFEST_PATH_ESCAPE')
        raw=target.read_bytes()
        if digest(raw)!=row['sha256'] or len(raw)!=row['size']:raise ValueError('MANIFEST_MISMATCH:'+row['path'])
        if target.suffix=='.json':read(target)
    if not required_cfg(cfg_read((repo/'candidate/GCMod.cfg').read_bytes())):raise ValueError('RUNTIME_CFG_REQUIRES_KO_OFFLINE_MASTER_ALL')
    if not (repo/'candidate/GCMod.dll').read_bytes().startswith(b'MZ'):raise ValueError('RUNTIME_DLL_REQUIRED')
    return dict(info,status='PASS',master_keys=sum(len(v) for fs in read(package/'master.json').values() for v in fs.values()),
                novels=len(list((package/'novels').glob('*.json'))),native='NOT_RUN_OFFLINE')


def build_runtime(repo, game):
    subprocess.run(['dotnet', 'build', str(repo/'source/GCMod.Candidate/GCMod.Candidate.csproj'),
                    '-c', 'Release', '-p:GameDir='+str(game.resolve()), '--nologo'], check=True)
    return {'status':'BUILT', 'dll_sha256':digest((repo/'candidate/GCMod.dll').read_bytes()),
            'verification':verify_repo(repo)}


def allowed(path):
    if path in (CFG,PLUGIN+'GCMod.dll',PLUGIN+'Utility.dll',PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle','README_KO.txt','gc-mod-local-LICENSE.txt'):return True
    return bool(re.fullmatch(re.escape(PLUGIN)+r'translations/ko/(?:manifest|master|names|ui)\.json',path) or re.fullmatch(re.escape(PLUGIN)+r'translations/ko/novels/[0-9]+\.json',path))


def pack(repo, output, font=None, utility=None):
    report=verify_repo(repo)
    if output.exists():raise ValueError('NEW_ARCHIVE_PATH_REQUIRED')
    if bool(font)!=bool(utility):raise ValueError('LOCAL_DEPENDENCIES_REQUIRE_FONT_AND_UTILITY')
    files={CFG:(repo/'candidate/GCMod.cfg').read_bytes(),PLUGIN+'GCMod.dll':(repo/'candidate/GCMod.dll').read_bytes(),
           'gc-mod-local-LICENSE.txt':(repo/'THIRD_PARTY_LICENSES/gc-mod-local-LICENSE.txt').read_bytes()}
    for p in [*(repo/'translations/ko'/n for n in CORE),*sorted((repo/'translations/ko/novels').glob('*.json'))]:
        files[PLUGIN+'translations/ko/'+p.relative_to(repo/'translations/ko').as_posix()]=p.read_bytes()
    if font:
        files[PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle']=font.read_bytes();files[PLUGIN+'Utility.dll']=utility.read_bytes()
    kind='local-complete' if font else 'public-update'
    files['README_KO.txt']=('GCMod Korean LocalParity\n게임 종료 후 적용하세요. Language=ko / CDN=offline / EnabledTables=*\n'
        +'기존 BepInEx IL2CPP 설치가 필요합니다.\n'
        +('로컬 폰트/Utility를 포함한 개인 설치본입니다. 공개 배포용으로 업로드하지 마세요.\n' if font else
          '공개 업데이트에는 게임/interop/Utility/폰트를 포함하지 않습니다. 기존 승인된 Utility.dll 및 한국어 폰트가 필요합니다.\n')
        +'설치: python tools/local_release.py install --archive <이 ZIP> --game <게임 경로> --backup <새 백업 폴더>\n').encode('utf-8')
    receipt={'schema':'gc-local-parity-package-v1','kind':kind,'upstream':'0cdcd9e84fffa438dfdac63176685a6c47a07318',
        'files':{p:{'size':len(raw),'sha256':digest(raw)} for p,raw in sorted(files.items())},'verification':report}
    files['package.json']=json_bytes(receipt);output.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        for path,raw in sorted(files.items()):
            info=zipfile.ZipInfo(path,(2026,10,28,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;archive.writestr(info,raw)
    verify_archive(output)
    return {'status':'PACKAGED','kind':kind,'archive':str(output),'sha256':digest(output.read_bytes()),'files':len(files)}


def verify_archive(path):
    with zipfile.ZipFile(path) as archive:
        names=archive.namelist()
        if len(set(names))!=len(names) or sum(i.file_size for i in archive.infolist())>512*1024*1024:raise ValueError('ARCHIVE_MEMBERSHIP_OR_SIZE')
        receipt=json.loads(archive.read('package.json'))
        if receipt.get('schema')!='gc-local-parity-package-v1' or receipt.get('kind') not in ('public-update','local-complete'):raise ValueError('PACKAGE_SCHEMA')
        if set(names)!=set(receipt['files'])|{'package.json'}:raise ValueError('ARCHIVE_MEMBERSHIP')
        files={}
        for p,entry in receipt['files'].items():
            if not allowed(p):raise ValueError('ARCHIVE_PATH:'+p)
            raw=archive.read(p)
            if len(raw)!=entry['size'] or digest(raw)!=entry['sha256']:raise ValueError('ARCHIVE_HASH:'+p)
            if p.endswith('.json'):json.loads(raw)
            files[p]=raw
        for p in (CFG,PLUGIN+'GCMod.dll',*(PLUGIN+'translations/ko/'+n for n in CORE)):
            if p not in files:raise ValueError('RUNTIME_FILE_MISSING:'+p)
        dependencies = {PLUGIN+'Utility.dll', PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle'}
        if (receipt['kind']=='public-update' and dependencies.intersection(files)
            or receipt['kind']=='local-complete' and not dependencies.issubset(files)):
            raise ValueError('PACKAGE_DEPENDENCY_KIND')
        if not files[PLUGIN+'GCMod.dll'].startswith(b'MZ'): raise ValueError('RUNTIME_DLL_REQUIRED')
        registrations=json.loads(files[PLUGIN+'translations/ko/manifest.json']).get('novels',{})
        for identity in registrations:
            if not str(identity).isdigit() or PLUGIN+'translations/ko/novels/'+identity+'.json' not in files:
                raise ValueError('REGISTERED_NOVEL_MISSING')
        if not required_cfg(cfg_read(files[CFG])):raise ValueError('ARCHIVE_CFG')
        return receipt,files


def game_closed():
    if os.name!='nt':raise ValueError('INSTALL_WINDOWS_ONLY')
    result=subprocess.run(['tasklist','/FI','IMAGENAME eq GC.exe','/FO','CSV','/NH'],capture_output=True,text=True,check=True)
    if '"GC.exe"' in result.stdout:raise ValueError('CLOSE_GAME_FIRST')


def safe_target(root,relative):
    path=root/relative
    if not path.resolve().is_relative_to(root.resolve()):raise ValueError('INSTALL_PATH_ESCAPE')
    for p in (path,*path.parents):
        if p.is_symlink() or (hasattr(p,'is_junction') and p.is_junction()):raise ValueError('INSTALL_REPARSE_PATH')
        if p==root:break
    return path


def install(archive,game,backup):
    with ProcessFileLock(safe_target(game, 'BepInEx/.gcmod-install.lock'), 'INSTALL_WRITER_BUSY'):
        return _install(archive,game,backup)


def _install(archive,game,backup):
    safe_target(game, CFG)
    game_closed();game=game.resolve();backup=backup.resolve()
    if not (game/'GC.exe').is_file() or not (game/'BepInEx/core').is_dir():raise ValueError('EXISTING_GAME_BEPINEX_REQUIRED')
    if backup.exists() or backup.is_relative_to(game):raise ValueError('NEW_BACKUP_OUTSIDE_GAME_REQUIRED')
    receipt,files=verify_archive(archive)
    for dep in (PLUGIN+'Utility.dll',PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle'):
        if dep not in files and not safe_target(game,dep).is_file():raise ValueError('LOCAL_DEPENDENCY_REQUIRED:'+dep)
    # Preserve unrelated user preferences; explicitly enable the complete Master profile.
    target=safe_target(game,CFG)
    if target.exists():
        cfg=cfg_read(target.read_bytes());required=cfg_read(files[CFG])
        for section in ('Translation','Translation.MasterData','Translation.Font'):
            if not cfg.has_section(section):cfg.add_section(section)
            for k,v in required[section].items():cfg[section][k]=v
        out=io.StringIO();cfg.write(out);files[CFG]=out.getvalue().encode('utf-8')
    changes=[]
    for p,raw in sorted(files.items()):
        if not p.startswith('BepInEx/'):continue
        target=safe_target(game,p);old=target.read_bytes() if target.exists() else None
        if old==raw:continue
        changes.append((p,raw,old))
    backup.mkdir(parents=True)
    journal={'schema':'gc-local-parity-install-v1','game':str(game),'archive_sha256':digest(archive.read_bytes()),'status':'prepared','files':[]}
    for p,raw,old in changes:
        if old is not None:
            dest=safe_target(backup,p);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(old)
        journal['files'].append({'path':p,'before':digest(old) if old is not None else None,'after':digest(raw)})
    atomic_json(backup/'install.json', journal)
    written=[]
    try:
        game_closed()
        for p,raw,old in changes:
            target=safe_target(game,p)
            if (target.read_bytes() if target.exists() else None)!=old:raise ValueError('INSTALL_PREIMAGE_CHANGED')
            target.parent.mkdir(parents=True,exist_ok=True);written.append((target,old));target.write_bytes(raw)
            if digest(target.read_bytes())!=digest(raw):raise ValueError('INSTALL_VERIFY_FAILED')
    except Exception:
        for target,old in reversed(written):
            if old is None:target.unlink(missing_ok=True)
            else:target.write_bytes(old)
        journal['status']='rolled_back';atomic_json(backup/'install.json', journal);raise
    journal['status']='installed';atomic_json(backup/'install.json', journal)
    return {'status':'INSTALLED','changed_files':len(changes),'backup':str(backup),'native':'RESTART_AND_SMOKE_REQUIRED'}


def rollback(game,backup):
    with ProcessFileLock(safe_target(game, 'BepInEx/.gcmod-install.lock'), 'INSTALL_WRITER_BUSY'):
        return _rollback(game,backup)


def _rollback(game,backup):
    safe_target(game, CFG)
    game_closed();game=game.resolve();backup=backup.resolve();j=read(backup/'install.json')
    if j.get('status')!='installed' or j.get('schema')!='gc-local-parity-install-v1' or Path(j['game']).resolve()!=game:raise ValueError('ROLLBACK_RECEIPT')
    for row in j['files']:
        if not allowed(row['path']) or not row['path'].startswith('BepInEx/'):raise ValueError('ROLLBACK_PATH')
        if digest(safe_target(game,row['path']).read_bytes())!=row['after']:raise ValueError('ROLLBACK_INSTALL_CHANGED')
        if row['before'] and digest(safe_target(backup,row['path']).read_bytes())!=row['before']:raise ValueError('ROLLBACK_BACKUP_CHANGED')
    restored=[]
    try:
        for row in reversed(j['files']):
            target=safe_target(game,row['path']);installed=target.read_bytes()
            restored.append((target,installed))
            if row['before']:target.write_bytes(safe_target(backup,row['path']).read_bytes())
            else:target.unlink()
    except OSError:
        for target,installed in reversed(restored):target.write_bytes(installed)
        raise
    j['status']='rolled_back';atomic_json(backup/'install.json', j);return {'status':'ROLLED_BACK','files':len(j['files'])}


def main():
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
    q=sub.add_parser('verify');q.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[1])
    q=sub.add_parser('build');q.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[1]);q.add_argument('--game',type=Path,required=True)
    q=sub.add_parser('pack');q.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[1]);q.add_argument('--output',type=Path,required=True);q.add_argument('--font',type=Path);q.add_argument('--utility',type=Path)
    q=sub.add_parser('verify-archive');q.add_argument('--archive',type=Path,required=True)
    q=sub.add_parser('install');q.add_argument('--archive',type=Path,required=True);q.add_argument('--game',type=Path,required=True);q.add_argument('--backup',type=Path,required=True)
    q=sub.add_parser('rollback');q.add_argument('--game',type=Path,required=True);q.add_argument('--backup',type=Path,required=True)
    a=p.parse_args()
    try:
        if a.command=='verify':result=verify_repo(a.repo)
        elif a.command=='build':result=build_runtime(a.repo,a.game)
        elif a.command=='pack':result=pack(a.repo,a.output,a.font,a.utility)
        elif a.command=='verify-archive':receipt,_=verify_archive(a.archive);result={'status':'PASS','kind':receipt['kind'],'files':len(receipt['files'])}
        elif a.command=='install':result=install(a.archive,a.game,a.backup)
        else:result=rollback(a.game,a.backup)
        print(json.dumps(result,ensure_ascii=False))
    except (ValueError,OSError,KeyError,configparser.Error,zipfile.BadZipFile,subprocess.CalledProcessError) as e:
        print(json.dumps({'status':'FAILED','reason':str(e)},ensure_ascii=False));return 2
    return 0

if __name__=='__main__':raise SystemExit(main())
