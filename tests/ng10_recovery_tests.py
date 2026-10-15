"""Actual helper file-IO faults in NEW synthetic custody. No game/DLL execution."""
from pathlib import Path
import argparse, hashlib, json, subprocess, sys

def main():
    p=argparse.ArgumentParser(description=__doc__); p.add_argument('--root',type=Path,required=True);p.add_argument('--scratch',type=Path,required=True)
    args=p.parse_args(); root=args.root.resolve(); scratch=args.scratch.resolve()
    assert not scratch.exists() and not any(p.name.casefold()=='bepinex' or (p/'GC.exe').is_file() for p in (scratch,*scratch.parents)), 'NEW_OUTSIDE_GAME_SCRATCH_REQUIRED'
    scratch.mkdir(parents=True)
    wrapper=scratch/'fault_child.py'
    wrapper.write_text('''from pathlib import Path
import runpy,sys,subprocess,shutil,os,json
mode,target,script,*args=sys.argv[1:];target=Path(target).absolute(); marker=Path(__file__).with_name('fault-triggered.json')
def hit():marker.write_text(json.dumps({'mode':mode,'target':str(target),'primitive':'actual os.replace/unlink/rmtree boundary'}),'utf-8')
def run(cmd,*a,**k):
 if isinstance(cmd,list) and cmd and str(cmd[0]).lower()=='powershell' and len(cmd)==4 and 'Get-Process -Name GC' in cmd[3]:return subprocess.CompletedProcess(cmd,0,'1\\n' if mode=='running' else '0\\n','')
 raise RuntimeError('UNEXPECTED_SUBPROCESS_IN_FILE_ONLY_TEST')
subprocess.run=run
original_replace=os.replace
def replace(src,dst,*a,**k):
 if Path(dst).absolute()==target and mode in ('atomic_replace','record_after_cfg'):
  if mode=='atomic_replace' or json.loads(Path(src).read_text('utf-8')).get('phase')=='STAGE_COMPLETE':
   hit();raise PermissionError('SYNTHETIC_ATOMIC_REPLACE_DENIED')
 return original_replace(src,dst,*a,**k)
os.replace=replace
original_unlink=Path.unlink
def unlink(self,*a,**k):
 if mode=='unlink_old' and self.absolute()==target:hit();raise PermissionError('SYNTHETIC_OLD_UNLINK_DENIED')
 return original_unlink(self,*a,**k)
Path.unlink=unlink
original_delete=shutil.rmtree
def delete(path,*a,**k):
 if Path(path).absolute()==target and mode in ('delete','partial_delete'):
  if mode=='partial_delete':
   first=next((x for x in Path(path).rglob('*') if x.is_file()),None)
   if first:first.unlink()
  hit();raise PermissionError('SYNTHETIC_OWNED_DELETE_DENIED')
 return original_delete(path,*a,**k)
shutil.rmtree=delete
sys.argv=[script,*args];runpy.run_path(script,run_name='__main__')
''','utf-8')
    records=[]
    def setup(name):
        b=scratch/name;game=b/'synthetic-game';impl=b/'implementation';work=b/'work'
        cfg=game/'BepInEx/config/GCMod.cfg';old=game/'BepInEx/plugins/GCMod/GCMod.dll';font=b/'fake-medium'
        original=b'[Translation]\nEnabled = true\nLanguage = ko\n[Translation.Font]\nAssetBundlePath = test\n[Translation.MasterData]\nEnabledTables = mUnitWords, mSubunitWords\n[Personal]\nValue = preserve\n'
        files={cfg:original,old:b'NOT_EXECUTABLE_ORIGINAL',old.with_name('Utility.dll'):b'NOT_EXECUTABLE_UTILITY',
               impl/'candidate/GCMod.dll':b'NOT_EXECUTABLE_CANDIDATE',impl/'translations/ko/release-manifest.json':b'{"synthetic":true}',font:b'NOT_A_FONT'}
        for path,raw in files.items():path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
        return b,game,impl,work,cfg,old,font,files
    def execute(context, tool, extra=(), mode='none', target=None, expected=0):
        b,game,impl,work,cfg,old,font,files=context
        marker=scratch/'fault-triggered.json';marker.unlink(missing_ok=True)
        cmd=[sys.executable,'-I','-S','-X','utf8',str(wrapper),mode,str(target or b),str(root/'tools'/tool),
             '--work-root',str(work),'--implementation-root',str(impl),'--game-root',str(game),*map(str,extra)]
        r=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8',timeout=60)
        value=json.loads(r.stdout) if r.stdout.strip().startswith('{') else {'stderr':r.stderr[-1000:]}
        assert r.returncode==expected,(tool,mode,r.returncode,r.stdout,r.stderr)
        return {'command':tool,'fault':mode,'fault_observed':marker.exists(),'exit':r.returncode,'result':value}
    def prepare(c): return execute(c,'prepare_native.py',('--medium',c[6]))
    def stage(c,**kw):return execute(c,'stage_native.py',('--execute',),**kw)
    def restore(c,**kw):return execute(c,'restore_native.py',('--execute',),**kw)
    def intact(c):
        b,game,impl,work,cfg,old,font,files=c
        return {'cfg_original':cfg.read_bytes()==files[cfg],'body_original':old.read_bytes()==files[old],
          'backup_cfg':(work/'private_backup/GCMod.cfg').read_bytes()==files[cfg],
          'backup_body':(work/'private_backup/GCMod.dll').read_bytes()==files[old],
          'candidate_absent':not (game/'BepInEx/plugins/GCMod.Nextgen.Candidate').exists(),
          'store_absent':not (game/'BepInEx/config/GCMod.Nextgen.local').exists()}
    # Inject into corrected primitives, not obsolete Path.write_bytes wrappers.
    for name,point in [('copy_candidate','prepared'),('publish_candidate','plugin'),('old_remove','old'),('stage_cfg','cfg'),('record_after_cfg','record'),('restore_cfg','cfg_restore'),('restore_delete','delete'),('restore_partial_delete','partial_delete'),('restore_store_partial_delete','store_delete')]:
        c=setup(name);b,game,impl,work,cfg,old,font,files=c;calls=[prepare(c)]
        plugin=game/'BepInEx/plugins/GCMod.Nextgen.Candidate';store=game/'BepInEx/config/GCMod.Nextgen.local'
        if point in ('cfg_restore','delete','partial_delete','store_delete'):
            calls.append(stage(c))
            if point=='store_delete':
                folder=store/'packages/synthetic';folder.mkdir(parents=True)
                (folder/'release-manifest.json').write_bytes(files[impl/'translations/ko/release-manifest.json'])
                (folder/'complete.json').write_text('{"Schema":1,"ContentId":"synthetic"}','utf-8')
                (store/'current.json').write_text('{"ContentId":"synthetic"}','utf-8')
            mode='atomic_replace' if point=='cfg_restore' else 'partial_delete' if point in ('partial_delete','store_delete') else 'delete'
            target=cfg if point=='cfg_restore' else store if point=='store_delete' else plugin
            fault=restore(c,mode=mode,target=target,expected=2);calls.append(fault)
        else:
            target={'prepared':work/'trial-staging/GCMod.dll','plugin':plugin,'old':old,'cfg':cfg,'record':work/'audit/native/INSTALL_RECEIPT.json'}[point]
            fault=stage(c,mode='unlink_old' if point=='old' else 'record_after_cfg' if point=='record' else 'atomic_replace',target=target,expected=2);calls.append(fault)
        assert fault['fault_observed'],'FAULT_DID_NOT_TRIGGER:'+name
        ready=execute(c,'verify_native_ready.py',expected=2);calls.append(ready)
        assert ready['result']['launch_allowed'] is False
        calls.append(restore(c));state=intact(c);assert all(state.values()),state
        repeated=restore(c);assert repeated['result']['status']=='ALREADY_RESTORED';calls.append(repeated)
        records.append({'case':name,'status':'PASS','calls':calls,'state':state})
    for name in ('external_cfg','external_body','unknown_plugin','unknown_empty_dir','running','duplicate_body','reparse'):
        c=setup(name);b,game,impl,work,cfg,old,font,files=c;calls=[prepare(c),stage(c)];plugin=game/'BepInEx/plugins/GCMod.Nextgen.Candidate'
        if name=='external_cfg':cfg.write_bytes(cfg.read_bytes()+b'\n[External]\nValue = leave\n')
        elif name=='external_body':old.write_bytes(b'EXTERNAL_BODY')
        elif name=='unknown_plugin':(plugin/'external.txt').write_text('external','utf-8')
        elif name=='unknown_empty_dir':(plugin/'foreign-dir').mkdir()
        elif name=='duplicate_body':old.write_bytes(files[old])
        elif name=='reparse':
            target=b/'external-tree';target.mkdir();link=plugin/'foreign-link'
            result=subprocess.run(['cmd','/c','mklink','/J',str(link),str(target)],capture_output=True)
            assert result.returncode==0,'JUNCTION_FIXTURE_SETUP_FAILED'
        before={str(x):hashlib.sha256(x.read_bytes()).hexdigest() for x in [cfg,old] if x.exists()}
        if name=='duplicate_body':
            calls.append(execute(c,'verify_native_ready.py',expected=2));calls.append(restore(c));assert all(intact(c).values())
        else:
            reject=restore(c,mode='running' if name=='running' else 'none',expected=2);calls.append(reject)
            after={str(x):hashlib.sha256(x.read_bytes()).hexdigest() for x in [cfg,old] if x.exists()};assert before==after,'REJECTION_MUTATED_ORIGINAL'
        records.append({'case':name,'status':'PASS','calls':calls,'external_preserved':True})
    # Existing absent lock inode is not BUSY; OS lock lives only with owner handle.
    c=setup('normal_and_empty_lock');calls=[prepare(c)];(c[3]/'.native-trial.lock').write_bytes(b'')
    calls.extend([stage(c),execute(c,'verify_native_ready.py'),restore(c),restore(c)])
    assert calls[-1]['result']['status']=='ALREADY_RESTORED' and all(intact(c).values())
    records.append({'case':'normal_and_empty_lock','status':'PASS','calls':calls})
    result={'schema':'ng10-product-helper-fault-results-v1','status':'PASS','cases':records,'native_calls':0,'game_execution':0,
      'basis':'Real current Python helper entrypoints and Windows file IO; only GC process query and specified failing primitive mocked. Dummy DLL/font bytes are non-executable.',
      'injection_required_for_fault_pass':True,'power_loss_or_native_fault':'NOT_RUN',
      'source_sha256':{x:hashlib.sha256((root/'tools'/x).read_bytes()).hexdigest() for x in ('native_trial.py','stage_native.py','restore_native.py','verify_native_ready.py')}}
    (scratch/'RESULT.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8');print(json.dumps({'status':'PASS','cases':len(records),'result':str(scratch/'RESULT.json')}))
if __name__=='__main__':main()
