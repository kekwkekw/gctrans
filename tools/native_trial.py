"""Reentrant recovery of this trial's four recorded paths; no launcher/installer.
Backups and phases live outside the game. Individual replaces are atomic;
the multi-path operation is not. Unknown bytes/paths never become owned.
"""
from pathlib import Path
import argparse, configparser, contextlib, hashlib, json, os, shutil, subprocess, uuid

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(ok, reason):
    if not ok: raise AssertionError(reason)
def no_reparse(path):
    for p in (path, *path.parents):
        if p.exists() or p.is_symlink():
            check(not p.is_symlink() and not getattr(p.lstat(), 'st_file_attributes', 0) & 1024,
                  'REPARSE_REFUSED:'+str(p))
def inventory(root):
    no_reparse(root); files={}
    if root.exists():
        check(root.is_dir(), 'EXPECTED_DIRECTORY:'+str(root))
        for p in root.rglob('*'):
            no_reparse(p)
            if p.is_file(): files[p.relative_to(root).as_posix()]=sha(p)
            else: check(p.is_dir(), 'UNKNOWN_FILE_TYPE:'+str(p))
    return files
def owned_subset(root, expected, full=False):
    actual=inventory(root)
    check(all(k in expected and v==expected[k] for k,v in actual.items()), 'UNKNOWN_CHANGED:'+str(root))
    if root.exists():
        dirs={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_dir()}
        permitted={parent.as_posix() for k in expected for parent in Path(k).parents if parent!=Path('.')}
        check(dirs<=permitted, 'UNKNOWN_DIRECTORY:'+str(root))
    if full: check(actual==expected, 'INCOMPLETE_OWNED_CANDIDATE')
    return actual
def atomic_bytes(path, raw):
    no_reparse(path); path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_name(path.name+'.ng10-'+uuid.uuid4().hex+'.tmp')
    try:
        with temporary.open('xb') as stream:
            stream.write(raw); stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary,path)
    finally:
        if temporary.exists(): temporary.unlink()
def atomic_json(path, value): atomic_bytes(path,json.dumps(value,ensure_ascii=False,indent=2).encode('utf-8'))
@contextlib.contextmanager
def lock(path):
    no_reparse(path)
    path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('a+b') as stream:
        if stream.tell()==0: stream.write(b'0'); stream.flush()
        stream.seek(0)
        try:
            if os.name=='nt':
                import msvcrt
                msvcrt.locking(stream.fileno(),msvcrt.LK_NBLCK,1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
        except OSError: raise AssertionError('LIVE_TRIAL_WRITER_BUSY')
        try: yield
        finally:
            stream.seek(0)
            if os.name=='nt': msvcrt.locking(stream.fileno(),msvcrt.LK_UNLCK,1)
            else: fcntl.flock(stream.fileno(),fcntl.LOCK_UN)
def game_closed():
    r=subprocess.run(['powershell','-NoProfile','-Command','@(Get-Process -Name GC -ErrorAction SilentlyContinue).Count'],capture_output=True,text=True,check=True)
    check(int(r.stdout.strip())==0,'GAME_RUNNING_NORMAL_CLOSE_REQUIRED')
def settings(path):
    c=configparser.ConfigParser(interpolation=None); c.optionxform=str; c.read(path,encoding='utf-8-sig')
    return {(s,k):v for s in c.sections() for k,v in c.items(s)}

class Trial:
    def __init__(self,args):
        self.args=args; self.base=args.work_root.absolute(); self.game=args.game_root.absolute()
        self.implementation=(args.implementation_root or self.base).absolute()
        for p in (self.base,self.game,self.implementation): no_reparse(p)
        check(not self.base.is_relative_to(self.game),'BACKUP_OUTSIDE_GAME_REQUIRED')
        self.audit=self.base/'audit/native'; self.record_path=self.audit/'INSTALL_RECEIPT.json'
        self.old=self.game/'BepInEx/plugins/GCMod/GCMod.dll'; self.cfg=self.game/'BepInEx/config/GCMod.cfg'
        self.plugin=self.game/'BepInEx/plugins/GCMod.Nextgen.Candidate'; self.store=self.game/'BepInEx/config/GCMod.Nextgen.local'
        self.backup=self.base/'private_backup'; self.prepared=self.base/'trial-staging'; self.record=None
    def paths(self): return {k:str(getattr(self,k)) for k in ('old','cfg','plugin','store','backup','prepared')}
    def inputs(self):
        data=json.loads((self.audit/'CURRENT_INPUTS.json').read_text('utf-8'))
        for row,p in [('config',self.cfg),('store',self.store),('trial_plugin',self.plugin)]:
            check(Path(data[row]['path']).absolute()==p,'INPUT_PATH_MISMATCH')
        check(not data['store']['exists'] and not data['trial_plugin']['exists'],'EXISTING_STORE_REQUIRES_PRESERVATION_PLAN')
        self.original_inputs=data; return data
    def load(self):
        self.record=json.loads(self.record_path.read_text('utf-8'))
        check(self.record['schema']=='ng10-current-trial-recovery-v1','CURRENT_NG10_RECORD_REQUIRED')
        check(self.record['paths']==self.paths(),'TRIAL_WRITE_SET_PATH_MISMATCH'); self.inputs()
        check(self.record['original_cfg_sha']==self.original_inputs['config']['sha256'],'BACKUP_INPUT_CFG_MISMATCH')
        original=next(x for x in self.original_inputs['plugins'] if Path(x['path']).absolute()==self.old)
        check(self.record['original_body_sha']==original['sha256'],'BACKUP_INPUT_BODY_MISMATCH')
        for name,field in [('GCMod.cfg','original_cfg_sha'),('GCMod.dll','original_body_sha')]:
            no_reparse(self.backup/name); check(sha(self.backup/name)==self.record[field],'BACKUP_INTEGRITY_FAILED')
    def phase(self,value,step):
        self.record.update(phase=value,mutation=step); atomic_json(self.record_path,self.record)
    def other_plugins(self):
        expected={Path(x['path']).absolute():x['sha256'] for x in self.original_inputs['plugins'] if Path(x['path']).absolute()!=self.old}
        actual={p.absolute():sha(p) for p in (self.game/'BepInEx/plugins').rglob('*.dll') if p!=self.old and not p.is_relative_to(self.plugin)}
        for p in actual: no_reparse(p)
        check(actual==expected,'OTHER_PLUGIN_SET_OR_BYTES_CHANGED')
    def cfg_state(self):
        no_reparse(self.cfg)
        if not self.cfg.is_file(): return 'UNKNOWN_CHANGED'
        if sha(self.cfg)==self.record['original_cfg_sha']: return 'ORIGINAL'
        check(sha(self.base/'GCMod.trial.cfg.proposal')==self.record['trial_cfg_sha'],'TRIAL_PROPOSAL_CHANGED')
        current=settings(self.cfg); proposed=settings(self.base/'GCMod.trial.cfg.proposal')
        for key,value in {('Nextgen','UpdateRepository'):'kekwkekw/gctrans',('Nextgen','UpdateChannel'):'main'}.items():
            if key in current: proposed[key]=value
        return 'OWNED_TRIAL' if current==proposed else 'UNKNOWN_CHANGED'
    def state(self):
        no_reparse(self.old)
        old='ABSENT_AS_EXPECTED' if not self.old.exists() else 'ORIGINAL' if self.old.is_file() and sha(self.old)==self.record['original_body_sha'] else 'UNKNOWN_CHANGED'
        plugin='ABSENT_AS_EXPECTED'
        if self.plugin.exists():
            try: owned_subset(self.plugin,self.plugin_expected()); plugin='OWNED_TRIAL'
            except (AssertionError,OSError): plugin='UNKNOWN_CHANGED'
        store='ABSENT_AS_EXPECTED'
        if self.store.exists():
            try: self.validate_store(); store='OWNED_TRIAL'
            except (AssertionError,OSError,ValueError,KeyError): store='UNKNOWN_CHANGED'
        return {'old':old,'cfg':self.cfg_state(),'plugin':plugin,'store':store}
    def plugin_expected(self):
        expected=dict(self.record['plugin_files'])
        replacement=self.record.get('replacement')
        if replacement and self.record['phase']=='REPLACE_STARTED' and (self.plugin/'GCMod.dll').is_file():
            actual=sha(self.plugin/'GCMod.dll')
            if actual in (replacement['old_sha'],replacement['new_sha']): expected['GCMod.dll']=actual
        return expected
    def stage(self):
        game_closed(); check(not self.record_path.exists(),'CURRENT_TRIAL_ALREADY_RECORDED_USE_RESTORE')
        inputs=self.inputs(); self.other_plugins()
        check(not self.plugin.exists() and not self.store.exists(),'EXISTING_PATH_NO_OVERWRITE')
        check(sha(self.cfg)==inputs['config']['sha256'],'CFG_CONCURRENT_CHANGE')
        original=next(x for x in inputs['plugins'] if Path(x['path']).absolute()==self.old)
        check(sha(self.old)==original['sha256'],'OLD_BODY_CONCURRENT_CHANGE')
        source=self.implementation/'candidate/GCMod.dll'; check(sha(source)==inputs['candidate']['sha256'],'CANDIDATE_CHANGED')
        no_reparse(Path(inputs['font']['path'])); check(sha(Path(inputs['font']['path']))==inputs['font']['sha256'],'FONT_CUSTODY_CHANGED')
        check(not self.backup.exists() and not self.prepared.exists(),'EXISTING_BACKUP_OR_STAGING')
        self.backup.mkdir(); atomic_bytes(self.backup/'GCMod.cfg',self.cfg.read_bytes()); atomic_bytes(self.backup/'GCMod.dll',self.old.read_bytes())
        check(sha(self.backup/'GCMod.cfg')==sha(self.cfg) and sha(self.backup/'GCMod.dll')==sha(self.old),'BACKUP_INTEGRITY_FAILED')
        expected={'GCMod.dll':sha(source)}; expected.update({'translations/'+k:v for k,v in inventory(self.implementation/'translations').items()})
        self.record={'schema':'ng10-current-trial-recovery-v1','trial_id':uuid.uuid4().hex,'paths':self.paths(),
            'original_cfg_sha':sha(self.cfg),'original_body_sha':sha(self.old),'trial_cfg_sha':sha(self.base/'GCMod.trial.cfg.proposal'),
            'candidate_sha':sha(source),'plugin_files':expected,'store_existed':False,'installed':False,
            'phase':'BACKUP_VERIFIED','mutation':'NONE','atomicity':'INDIVIDUAL_FILE_REPLACE_ONLY_NOT_MULTI_PATH'}
        self.phase('PREPARING_OUTSIDE_PLUGINS','COPY_CANDIDATE_STARTED'); self.prepared.mkdir()
        for relative in expected:
            raw=source.read_bytes() if relative=='GCMod.dll' else (self.implementation/relative).read_bytes()
            check(hashlib.sha256(raw).hexdigest()==expected[relative],'SOURCE_CHANGED_DURING_COPY')
            atomic_bytes(self.prepared/relative,raw)
        owned_subset(self.prepared,expected,full=True)
        self.phase('CANDIDATE_READY_OUTSIDE_PLUGINS','PUBLISH_CANDIDATE_STARTED'); game_closed()
        os.replace(self.prepared,self.plugin)
        self.phase('CANDIDATE_PUBLISHED','REMOVE_OLD_STARTED'); game_closed()
        check(sha(self.old)==self.record['original_body_sha'],'OLD_BODY_CONCURRENT_CHANGE'); self.old.unlink()
        self.phase('OLD_BODY_REMOVED','APPLY_CFG_STARTED'); game_closed()
        check(sha(self.cfg)==self.record['original_cfg_sha'],'CFG_CONCURRENT_CHANGE')
        atomic_bytes(self.cfg,(self.base/'GCMod.trial.cfg.proposal').read_bytes())
        self.record['installed']=True; self.phase('STAGE_COMPLETE','COMPLETE'); return self.ready()
    def validate_store(self):
        if not self.store.exists(): return {}
        actual=inventory(self.store)
        if 'cleanup_store_files' in self.record:
            owned_subset(self.store,self.record['cleanup_store_files']); return actual
        fixture=json.loads(self.args.fixture.read_text('utf-8')) if self.args.fixture and self.args.fixture.is_file() else {}
        sources=[self.implementation/'translations/ko']+[Path(fixture[k]) for k in ('changed','restored') if k in fixture]
        known=set(); packages=self.store/'packages'; check(packages.is_dir(),'STORE_PACKAGES_REQUIRED')
        sources=[{k:v for k,v in inventory(s).items() if not Path(k).name.startswith('.')} for s in sources]
        for folder in packages.iterdir():
            check(folder.is_dir(),'STORE_UNEXPECTED_PACKAGE_FILE'); files=inventory(folder); complete=folder/'complete.json'
            check(complete.is_file() and json.loads(complete.read_text('utf-8'))=={'Schema':1,'ContentId':folder.name},'UNEXPECTED_COMPLETE_RECORD')
            files.pop('complete.json'); check(files in sources,'UNOWNED_OR_CHANGED_STORE_GENERATION'); known.add(folder.name)
        allowed={'packages','current.json','previous.json','pending_restart.json','ui-request.json','ui-request.claim.json','ui-request-result.json','.ui-request.lock'}
        check({p.name for p in self.store.iterdir()}<=allowed,'UNEXPECTED_STORE_FILE')
        for name,key in [('current.json','ContentId'),('previous.json','ContentId'),('pending_restart.json','ContentId'),
                         ('ui-request.json','CandidateContentId'),('ui-request.claim.json','CandidateContentId'),('ui-request-result.json','CandidateContentId')]:
            p=self.store/name
            if p.exists(): check(json.loads(p.read_text('utf-8'))[key] in known,'FOREIGN_SELECTION_OR_REQUEST')
        return actual
    def ready(self):
        self.load(); game_closed(); self.other_plugins(); state=self.state()
        check(self.record['phase']=='STAGE_COMPLETE' and self.record['installed'],'STAGE_NOT_COMPLETE_NO_LAUNCH')
        check(state['old']=='ABSENT_AS_EXPECTED','OLD_AND_CANDIDATE_MIX_NO_LAUNCH')
        check(state['cfg']=='OWNED_TRIAL','TRIAL_CFG_NOT_ACTIVE_NO_LAUNCH')
        owned_subset(self.plugin,self.plugin_expected(),full=True); self.validate_store()
        return {'status':'STAGE_COMPLETE_SINGLE_CANDIDATE','launch_allowed':True,'states':state,'candidate_sha':self.record['candidate_sha'],'game_launches':0}
    def restore(self):
        self.load(); game_closed(); self.other_plugins(); state=self.state()
        check(state['old'] in {'ORIGINAL','ABSENT_AS_EXPECTED'},'UNKNOWN_CHANGED_ORIGINAL_BODY')
        check(state['cfg'] in {'ORIGINAL','OWNED_TRIAL'},'UNKNOWN_CHANGED_CFG')
        expected_plugin=self.plugin_expected()
        owned_subset(self.plugin,expected_plugin); owned_subset(self.prepared,self.record['plugin_files'])
        store_files=self.validate_store()
        complete=state['old']==state['cfg']=='ORIGINAL' and not any(p.exists() for p in (self.plugin,self.store,self.prepared))
        summary={'schema':'ng10-exact-restore-v1','status':'ALREADY_RESTORED' if complete else 'READY','states_before':state,
                 'game_processes':0,'targets':list(self.record['paths'].values()),'unrelated_plugins_unchanged':True,'original_store_absent':True}
        if self.args.execute and not complete:
            self.record['cleanup_store_files']=store_files; self.phase('RESTORE_STARTED','RESTORE_ORIGINAL_FILES_STARTED')
            if state['old']!='ORIGINAL': game_closed(); atomic_bytes(self.old,(self.backup/'GCMod.dll').read_bytes())
            if state['cfg']!='ORIGINAL': game_closed(); atomic_bytes(self.cfg,(self.backup/'GCMod.cfg').read_bytes())
            self.phase('ORIGINAL_FILES_RESTORED','REMOVE_OWNED_CANDIDATE_STARTED')
            for path,expected in ((self.plugin,expected_plugin),(self.store,store_files),(self.prepared,self.record['plugin_files'])):
                if path.exists(): game_closed(); owned_subset(path,expected); shutil.rmtree(path)
            check(sha(self.cfg)==self.record['original_cfg_sha'] and sha(self.old)==self.record['original_body_sha'],'RESTORED_BYTES_MISMATCH')
            self.record['installed']=False; self.phase('RESTORED','COMPLETE')
            summary.update(status='PASS',cfg_sha=sha(self.cfg),body_sha=sha(self.old),trial_plugin_absent=True,trial_store_absent=True)
        elif complete:
            summary.update(cfg_sha=sha(self.cfg),body_sha=sha(self.old),trial_plugin_absent=True,trial_store_absent=True)
        atomic_json(self.audit/('RESTORE_RESULT.json' if self.args.execute else 'RESTORE_READY.json'),summary); return summary

def main(operation):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--work-root',type=Path,required=True); p.add_argument('--implementation-root',type=Path)
    p.add_argument('--game-root',type=Path,required=True)
    p.add_argument('--fixture',type=Path); p.add_argument('--execute',action='store_true'); args=p.parse_args()
    try:
        check(operation!='stage' or args.execute,'EXPLICIT_EXECUTE_REQUIRED'); trial=Trial(args)
        with lock(trial.base/'.native-trial.lock'):
            value=trial.stage() if operation=='stage' else trial.ready() if operation=='ready' else trial.restore()
        print(json.dumps(value,ensure_ascii=False,indent=2)); return 0
    except (AssertionError,OSError,ValueError,KeyError) as error:
        value={'status':'REJECTED' if isinstance(error,AssertionError) else 'IO_OR_INPUT_FAILURE','reason':str(error),
               'operation':operation,'launch_allowed':False,'recovery':'Preserve files/backup; same restore may resume known states.'}
        if 'trial' in locals() and trial.record is not None:
            value['phase']=trial.record['phase']
            try: value['states']=trial.state()
            except Exception: value['states']='READ_FAILED'
        print(json.dumps(value,ensure_ascii=False,indent=2)); return 2
