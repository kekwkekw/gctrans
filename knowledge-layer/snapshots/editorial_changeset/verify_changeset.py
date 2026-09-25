import json,hashlib,sys,re,pathlib
from collections import defaultdict,Counter
R=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.')
def jl(p): return [json.loads(x) for x in (R/p).read_text(encoding='utf-8').splitlines() if x.strip()]
def j(p): return json.loads((R/p).read_text(encoding='utf-8'))
def sh(s): return hashlib.sha256(s.encode()).hexdigest()
ops=jl('records/EDITORIAL_OPERATIONS_11486.jsonl')
efile=next((R/'records').glob('ENTRY_COMPOSITIONS_*.jsonl'))
entries=jl(efile.relative_to(R))
prot=j('records/PROTECTED_LOCATORS_INHERITED.json')
holds={(x['novel_id'],int(x['raw_index'])) for x in prot['holds']}; an={(x['novel_id'],int(x['raw_index'])) for x in prot['source_anomalies']}
checks=[]
def c(n,x):
 if not x: raise SystemExit('FAIL '+n)
 checks.append(n)
c('operation_count',len(ops)==11486)
c('entry_count',len(entries)==9056)
c('entry_ids_unique',len(entries)==len({x['source_entry_id'] for x in entries}))
c('operation_ids_unique',len(ops)==len({x['operation_id'] for x in ops}))
c('holds_untouched',all((o['novel_id'],int(o['raw_index'])) not in holds for o in ops))
c('anomalies_untouched',all((o['novel_id'],int(o['raw_index'])) not in an for o in ops))
expected={'NORMALIZE_EXISTING_ELLIPSIS_GLYPH':10489,'RESTORE_INTERRUPTION_DASH':2,'NORMALIZE_STRUCTURAL_DASH_GLYPH':963,'ADD_NARRATION_TERMINAL_PERIOD':19,'ADD_NARRATOR_QUESTION_MARK':1,'REMOVE_LOCAL_ATTACHMENT_SPACE':10,'ADD_INTERNAL_NARRATION_PERIOD':1,'RECONNECT_REPORTED_CLAUSE_BOUNDARY':1}
c('action_counts',dict(Counter(o['action'] for o in ops))==expected)
byops=defaultdict(list)
for o in ops: byops[o['source_entry_id']].append(o)
by={x['source_entry_id']:x for x in entries}
c('entry_operation_partition',set(byops)==set(by))
tag_re=re.compile(r'<[^>]+>')
for eid,e in by.items():
 oo=byops[eid]
 c('baseline_'+eid,all(o['effective_baseline_sha256']==e['effective_before_sha256'] for o in oo))
 c('tokens_'+eid,all(e['effective_before'][o['start']:o['end']]==o['before'] for o in oo))
 t=e['effective_before']
 for o in sorted(oo,key=lambda x:(x['start'],x['end'],x['operation_id']),reverse=True): t=t[:o['start']]+o['after']+t[o['end']:]
 c('after_'+eid,sh(t)==e['effective_after_sha256'] and t==e['effective_after'])
 c('tags_'+eid,tag_re.findall(e['effective_before'])==tag_re.findall(t))
 c('binding_'+eid,e.get('target_binding',{}).get('kind') in {'PUBLISHED_MAIN_M1_RC1','HAND_SPANS','TITLE_EXTRACTION','BATTLE_HAND_SPAN'})
c('not_export',all(o['export_authorized'] is False for o in ops))
print(json.dumps({'status':'PASS','check_count':len(checks),'operations':len(ops),'entries':len(entries)},ensure_ascii=False))
