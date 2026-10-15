"""Read-only preparation; all files written outside the game installation."""
from pathlib import Path
import json, hashlib, configparser, subprocess, datetime
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--work-root', type=Path, default=Path(__file__).resolve().parents[1], help='Outside-game trial audit/backup directory')
parser.add_argument('--implementation-root', type=Path, help='Assembled source/data/candidate root; no historical ZIPs')
parser.add_argument('--game-root', type=Path, required=True)
parser.add_argument('--execute', action='store_true', help='Perform the scoped stage/replace/restore; authorization comes from the current user policy')
parser.add_argument('--medium', type=Path, required=True, help='Existing approved Medium custody; no download or generation')
args=parser.parse_args()
base=args.work_root.resolve()
game=args.game_root.resolve()
assert base!=game and not base.is_relative_to(game), 'BACKUP_AND_AUDIT_MUST_BE_OUTSIDE_GAME'
implementation=(args.implementation_root or (base if (base/'source').is_dir() else base/'implementation')).resolve()
audit=base/'audit/native'
audit.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def meta(p,hashed=False):
    r={'path':str(p),'exists':p.exists()}
    if p.is_file():
        r['size']=p.stat().st_size
        if hashed:r['sha256']=sha(p)
    return r
cfg=game/'BepInEx/config/GCMod.cfg'; old=game/'BepInEx/plugins/GCMod/GCMod.dll'
store=game/'BepInEx/config/GCMod.Nextgen.local'; plugin=game/'BepInEx/plugins/GCMod.Nextgen.Candidate'
font=args.medium.resolve()
c=configparser.ConfigParser(interpolation=None); c.optionxform=str; c.read(cfg,encoding='utf-8-sig')
keys={'Nextgen':['Enabled','PackageRoot','ReloadPackage','CheckUpdate','UiRequestsEnabled'],'Translation':['Enabled','Language'],'Translation.Font':['AssetBundlePath'],'Translation.MasterData':['EnabledTables']}
relevant={s:{k:c.get(s,k) for k in ks if c.has_option(s,k)} for s,ks in keys.items()}
plugins=[dict(meta(p,True),relative=str(p.relative_to(game/'BepInEx/plugins'))) for p in sorted((game/'BepInEx/plugins').rglob('*.dll'))]
proc=subprocess.run(['powershell','-NoProfile','-Command','@(Get-Process -Name GC -ErrorAction SilentlyContinue).Count'],capture_output=True,text=True,check=True)
assert int(proc.stdout.strip())==0,'EXISTING_GAME_RUNNING_NO_CHANGES'
assert not plugin.exists() and not store.exists(),'EXISTING_STORE_OR_CANDIDATE_REQUIRES_EXPLICIT_PRESERVATION_PLAN'
assert old.is_file() and cfg.is_file() and font.is_file(),'REQUIRED_INPUT_MISSING'
candidate=implementation/'candidate/GCMod.dll'
report={'schema':'ng09-current-native-inputs-v1','time':datetime.datetime.now(datetime.timezone.utc).isoformat(),'game_processes':0,'config':meta(cfg,True),'relevant_config':relevant,'plugins':plugins,'store':meta(store),'trial_plugin':meta(plugin),'font':meta(font,True),'dependencies':[meta(game/p) for p in ['BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/plugins/GCMod/Utility.dll','BepInEx/interop/Assembly-CSharp.dll','BepInEx/interop/MethodAddressToToken.db']],'candidate':meta(candidate,True),'xunity_translator_enabled':any(Path(p['path']).name=='XUnity.AutoTranslator.Plugin.BepInEx-IL2CPP.dll' for p in plugins),'metadata_claim':'FOUND is not ABI/glyph/hook PASS'}
assert not report['xunity_translator_enabled'],'XUNITY_MUST_REMAIN_OFF'
assert not any(Path(p['path']).stem.lower().startswith(('gcui','gcuiko','gcmodui')) for p in plugins), 'OLD_ADDON_MIXTURE_REQUIRES_EXPLICIT_WRITE_SET'
assert [Path(p['path']) for p in plugins if Path(p['path']).stem.lower()=='gcmod']==[old], 'DUPLICATE_GCMOD_BODY'
assert candidate.is_file() and (implementation/'translations/ko/release-manifest.json').is_file(), 'ASSEMBLED_CANDIDATE_REQUIRED'
(audit/'CURRENT_INPUTS.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
original=cfg.read_bytes(); text=original.decode('utf-8-sig'); nl='\r\n' if '\r\n' in text else '\n'; lines=text.splitlines(); section=''
tables=[x.strip() for x in relevant['Translation.MasterData']['EnabledTables'].split(',') if x.strip()]
for t in ['mTextConfig','mNovelKeyWords','mNovels']:
    if t not in tables:tables.append(t)
# Never introduce '*'; an existing personal setting is preserved as observed.
for i,line in enumerate(lines):
    if line.strip().startswith('['):section=line.strip()[1:-1]
    if section=='Translation' and line.split('=',1)[0].strip()=='Language':lines[i]='Language = ko'
    if section=='Translation.Font' and line.split('=',1)[0].strip()=='AssetBundlePath':lines[i]='AssetBundlePath = '+str(font)
    if section=='Translation.MasterData' and line.split('=',1)[0].strip()=='EnabledTables':lines[i]='EnabledTables = '+', '.join(tables)
assert '[Nextgen]' not in text,'EXISTING_NEXTGEN_REQUIRES_RECONCILE'
assert not (audit/'INSTALL_RECEIPT.json').exists(), 'CURRENT_TRIAL_RECORD_EXISTS_USE_RESTORE_OR_NEW_SCRATCH'
trial=(nl.join(lines)+nl+nl+'[Nextgen]'+nl+'Enabled = true'+nl+'PackageRoot = GCMod.Nextgen.Candidate/translations/ko'+nl+'ReloadPackage = false'+nl+'CheckUpdate = false'+nl+'UiRequestsEnabled = true'+nl).encode('utf-8')
(base/'GCMod.trial.cfg.proposal').write_bytes(trial)
auth={'schema':'current-trial-operator-note-v1','authorized':True,'basis':'Current human project policy applies; this prepared note does not itself grant authorization','scope':['DMM launcher','external backup','single candidate/cfg/store temporary changes','Annotation/Dictionary/Fit3 and Sort/Profile/Core/Choice display/live request','normal exit/exact restore'],'choice_selection':'HELD_UNPROVEN_ACCOUNT_SAVE_EFFECT','prohibited':['AP/BP/currency spending','sort apply changing persisted preferences','battle sortie','reward claim','gacha','account/save/lock operations','public actions'],'model_actual':'NOT_EXPOSED','reasoning_actual':'NOT_EXPOSED'}
(audit/'AUTHORIZATION.json').write_text(json.dumps(auth,ensure_ascii=False,indent=2),'utf-8')
plan={'schema':'ng09-exact-native-write-plan-v1','game':str(game),'backup':str(base/'private_backup'),'changes':[str(old),str(cfg),str(plugin),str(store)],'cfg_changes':{'font':str(font),'Master_tables':tables,'Nextgen':{'Enabled':True,'CheckUpdate':False,'UiRequestsEnabled':True,'PackageRoot':'GCMod.Nextgen.Candidate/translations/ko'}},'original_store_exists':False,'preserve':['Utility.dll and approved font','other plugins','unrelated personal settings','game/core/interop','account/save/cache'],'restore':'Normal close; compare original unrelated settings and plugin hashes; validate all trial generations against recorded source; restore original cfg bytes and body; remove only checked owned plugin/store targets','live_test':'trial-only close KO change and restore, same DLL, receiver and commit plus pixel rebind required'}
(audit/'TRIAL_WRITE_PLAN.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps({'current_cfg_sha':sha(cfg),'candidate_sha':sha(candidate),'active_DLLs':len(plugins),'store_exists':store.exists(),'font_found':font.is_file(),'trial_Master_tables':tables,'prepared':True},ensure_ascii=False,indent=2))
