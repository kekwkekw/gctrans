"""Scoped DLL replacement, closed game and current trial only; no launch."""
from pathlib import Path
import argparse,sys,json
sys.path.insert(0,str(Path(__file__).resolve().parent))
from native_trial import Trial,lock,check,sha,atomic_bytes,game_closed
p=argparse.ArgumentParser(description=__doc__)
p.add_argument("--work-root",type=Path,required=True);p.add_argument("--implementation-root",type=Path,required=True)
p.add_argument("--game-root",type=Path,required=True);p.add_argument("--fixture",type=Path);p.add_argument("--execute",action="store_true")
a=p.parse_args();check(a.execute,"EXPLICIT_EXECUTE_REQUIRED");t=Trial(a)
with lock(t.base/".native-trial.lock"):
    t.ready();source=t.implementation/"candidate/GCMod.dll";old_sha=sha(t.plugin/"GCMod.dll");new_sha=sha(source)
    check(old_sha!=new_sha,"NO_CHANGE");backup=t.backup/("candidate-"+old_sha+".dll")
    if not backup.exists():atomic_bytes(backup,(t.plugin/"GCMod.dll").read_bytes())
    check(sha(backup)==old_sha,"OWNED_REVISION_BACKUP_MISMATCH")
    t.record["replacement"]={"old_sha":old_sha,"new_sha":new_sha}
    t.phase("REPLACE_STARTED","OWNED_DLL_REPLACE_STARTED");game_closed()
    check(sha(t.plugin/"GCMod.dll")==old_sha,"FOREIGN_TRIAL_DLL_CHANGE")
    atomic_bytes(t.plugin/"GCMod.dll",source.read_bytes())
    check(sha(t.plugin/"GCMod.dll")==new_sha,"REPLACEMENT_SOURCE_CHANGED")
    t.record["candidate_sha"]=new_sha;t.record["plugin_files"]["GCMod.dll"]=new_sha
    t.phase("STAGE_COMPLETE","COMPLETE");print(json.dumps(t.ready(),indent=2))
