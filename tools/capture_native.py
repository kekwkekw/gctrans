"""Bounded symbolic GCMod evidence; never exports full game/auth logs."""
from pathlib import Path
import hashlib,json,re,sys,datetime
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('name');parser.add_argument('--work-root',type=Path,default=Path(__file__).resolve().parents[1]);parser.add_argument('--game-root',type=Path,required=True)
args=parser.parse_args();base=args.work_root.resolve();game=args.game_root.resolve();out=base/'audit/native'
assert not base.is_relative_to(game),'AUDIT_MUST_BE_OUTSIDE_GAME'
out.mkdir(parents=True,exist_ok=True)
name=args.name; assert re.fullmatch(r'[A-Z0-9_-]{1,80}',name)
log=game/'BepInEx/LogOutput.log';raw=log.read_bytes();text=raw.decode('utf-8',errors='replace')
safe=[]
for line in text.splitlines():
    if 'GCMod' not in line:continue
    if any(x in line.lower() for x in ('token=','authorization','password','credential')):continue
    line=re.sub(r'0x[0-9a-fA-F]+','<address>',line)
    line=re.sub(r'https?://\S+','<url>',line)
    if 'Fetching translation:' in line:continue
    safe.append(line)
(out/(name+'.log')).write_text('\n'.join(safe)+'\n','utf-8')
store=game/'BepInEx/config/GCMod.Nextgen.local'
state={}
for key in ('current.json','previous.json','pending_restart.json','ui-request.json','ui-request.claim.json','ui-request-result.json'):
    p=store/key
    if p.exists():
        b=p.read_bytes();assert len(b)<10000
        state[key]=json.loads(b.decode('utf-8-sig'))
report={'time_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'log_sha256_reference_only':hashlib.sha256(raw).hexdigest(),'symbolic_GCMod_lines':len(safe),'store':state,'game_process_not_forced':True}
(out/(name+'.json')).write_text(json.dumps(report,indent=2),'utf-8')
print(json.dumps(report,indent=2))
