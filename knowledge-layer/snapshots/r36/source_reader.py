"""Read original nested archive for verification. Does not write or print story text."""
import csv,io,json,zipfile,hashlib
from pathlib import Path

def read_original(root, novel_ids):
 root=Path(root)
 with zipfile.ZipFile(root/'source/gc_pro_review_r4.zip') as z:
  r3=z.read(next(n for n in z.namelist() if n.endswith('/source/r3.zip')))
 with zipfile.ZipFile(io.BytesIO(r3)) as z:
  def raw(p):return z.read('gc_pro_review_r3/'+p)
  def jl(p):return [json.loads(x) for x in raw(p).decode('utf-8-sig').splitlines() if x.strip()]
  def csvr(p):return list(csv.DictReader(io.StringIO(raw(p).decode('utf-8-sig'))))
  hm={x['block_id']:x for x in jl('source/hand_blocks.jsonl')+jl('alignment/working_hand_blocks.jsonl')}
  gs=jl('alignment/groups_r3.jsonl');gi={(g['novel_id'],i):g for g in gs for i in g['source_raw_indices']}
  si={(s['novel_id'],s['source_raw_index']):s for s in jl('alignment/reviewed_key_spans.jsonl')}
  tm={x['novel_id']:x for x in csvr('alignment/title_rows_reviewed.csv')};mp=csvr('source/mapping_units.csv')
  rows={};hashes={}
  for n in novel_ids:
   b=raw('source/raw_ordered/'+n+'.json');hashes[n]=hashlib.sha256(b).hexdigest()
   for i,s in enumerate(json.loads(b)):
    title=i==0 and bool(tm[n]['jp_title']);r={'novel_id':n,'raw_index':i,'is_title':title,'jp':s['message'],'speaker_jp':s['name'],'speaker_ko':'','hand_spans':[],'group_id':None,'join_separator':''}
    if title:
     h=tm[n]['arca_title_ko'];a=h.index('[')+1;end=h.rindex(']');r.update(exact_ko=h,ko=h[a:end],title_extraction={'source_field':'arca_title_ko','start':a,'end':end,'before':h,'observed_title':h[a:end],'translation_changed':False})
    else:
     g=gi[n,i];r.update(group_id=g['group_id'],speaker_ko=g['hand_speaker_ko'])
     if (n,i) in si:
      sp=si[n,i];r.update(ko=sp['translation_ko_untagged'],exact_ko=sp['exact_ko_fragment'],hand_spans=sp['hand_spans'],join_separator=sp.get('join_separator',''))
     else:
      if not(g['source_count']==g['hand_count']==1):raise ValueError('Unresolved original alignment '+n+'/'+str(i))
      block=hm[g['hand_block_ids'][0]];r.update(ko=block['text_ko'],exact_ko=block['text_ko'],hand_spans=[{'block_id':block['block_id'],'start':0,'end':len(block['text_ko'])}])
     if r['exact_ko']!=r['join_separator'].join(hm[a['block_id']]['text_ko'][a['start']:a['end']] for a in r['hand_spans']):raise ValueError('Original span mismatch '+n+'/'+str(i))
    rows[n,i]=r
 overrides=[]
 for override_file in ['source/title_extraction_overrides_r25.json','source/title_extraction_overrides_r26.json','source/title_extraction_overrides_r27.json','source/title_extraction_overrides_r28.json','source/title_extraction_overrides_r30.json','source/title_extraction_overrides_r31.json','source/title_extraction_overrides_r32.json','source/title_extraction_overrides_r33.json','source/title_extraction_overrides_r34_redo.json','source/title_extraction_overrides_r35_v2.json']:
  overrides_path=root/override_file
  if overrides_path.is_file():overrides.extend(json.loads(overrides_path.read_text(encoding='utf-8')))
 if overrides:
  for override in overrides:
   n=override['novel_id'];i=override['raw_index']
   if (n,i) not in rows:continue
   r=rows[n,i];before=tm[n]['arca_title_ko']
   if not r['is_title'] or before!=override['before'] or hashlib.sha256(before.encode()).hexdigest()!=override['preimage_sha256']:raise ValueError('Title preimage mismatch '+n)
   a=override['start'];b=override['end']
   if before[a:b]!=override['observed_title']:raise ValueError('Title slice mismatch '+n)
   r['ko']=before[a:b];r['title_extraction']={k:v for k,v in override.items() if k not in ['novel_id','raw_index','legacy_extracted_title']}
 return {'rows':rows,'hand':hm,'titles':tm,'mapping':mp,'raw_hashes':hashes,'nested_source_sha256':hashlib.sha256(r3).hexdigest()}
