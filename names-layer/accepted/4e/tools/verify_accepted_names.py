"""Read-only standalone integrity and deterministic acceptance verifier (Python 3.10+).
Usage: python -B tools/verify_accepted_names.py [checkpoint_directory]
Optional: --input-zip <path> (repeat for the three original source ZIPs).
No network, game access, repository mutation, normalization, or output writes.
"""
import sys
sys.dont_write_bytecode=True
import argparse, collections, hashlib, json, re, unicodedata, zipfile
from pathlib import Path, PurePosixPath

PINS={
 '4D0':('gc_names_layer_4D0_workbench.zip','96c5a464eb11a2373d1d5ee9ad4776e95754c22ada0c739fab842dbbb3e47ca3','14a2204e59b82ded64edf1cca9d9e5e53551a70320a43d18deb6fb526b93b0ae'),
 'BATCH09':('gc_names_layer_4D1_semantic_batch09.zip','075b1556272a65bf8b436465fd778011f124e287104fd28a713652452bd804aa','04bbd61437a2a3f950e2231961d6a1652095f306eb1b059bf692934468938bbc'),
 'QA01':('gc_names_layer_4D1_final_qa01.zip','75506f9d9d2aeb4d30e958760500eccdc8d050438b14b29c418a9f57e2bb0a71','869199ca8e32f7e7f06411eff752ba0daed950fcc894b48bf29266bce573719d')}
AMEND={'誘拐犯':('유괴범','납치범'),'怪しげな男Ａ':('수상한 남자A','수상쩍은 남자A'),'怪しげな男Ｂ':('수상한 남자B','수상쩍은 남자B'),'義妹':('양가의 여동생','양부모 집의 여동생'),'不審な男改め死の芸術家':('수상한 남자 바꿔서 죽음의 예술가','죽음의 예술가가 된 수상한 남자')}
LIMITED={'少年A','少年B','おばさん','お姉さん','画材屋店主','若者'}
FROZEN='62355171145edc1a19d8e88e9f2def46fa0e9a23f46c8c295742c168ac0be44d'
KEYSET='d1eaf05bc068195c4f678097d080fd5b4db7aca36c3159b81bfc7b52d594cae7'
sha=lambda b:hashlib.sha256(b).hexdigest()
def require(ok,label):
    if not ok:raise ValueError(label)
def pairs(ps):
    d={}
    for k,v in ps:
        require(k not in d,'duplicate JSON object key: '+repr(k));d[k]=v
    return d
def loads(b):return json.loads(b.decode('utf-8') if isinstance(b,bytes) else b,object_pairs_hook=pairs)
def read(p):return loads(p.read_bytes())
def lines(p):return [loads(b) for b in p.read_bytes().splitlines()]
def index(rs):
    d={}
    for r in rs:
        k=r['speaker_jp'];require(isinstance(k,str) and k and k not in d,'duplicate/empty exact speaker key');d[k]=r
    return d
def keyhash(keys):return sha(json.dumps(sorted(keys,key=lambda k:k.encode('utf-8')),ensure_ascii=False,separators=(',',':')).encode())
def safe_path(s):
    p=PurePosixPath(s)
    require(not p.is_absolute() and '..' not in p.parts and '\\' not in s and ':' not in s,'unsafe manifest path')
    return p
def verify(root,input_zips=()):
    root=Path(root).resolve()
    om=read(root/'OUTPUT_MANIFEST.json');files=om['files'];names=[f['path'] for f in files]
    require(len(names)==len(set(names))==len({p.casefold() for p in names}),'duplicate output manifest paths')
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    require(not any(p.is_symlink() for p in root.rglob('*')),'output symlink')
    require(set(names)==actual-{'OUTPUT_MANIFEST.json'},'output manifest missing/extra files')
    parse_counts=collections.Counter()
    for f in files:
        p=root/safe_path(f['path']);b=p.read_bytes()
        require(len(b)==f['size_bytes'] and sha(b)==f['sha256'],'output hash/size mismatch: '+f['path'])
        if p.suffix=='.json':loads(b);parse_counts['json']+=1
        if p.suffix=='.jsonl':
            for l in b.splitlines():loads(l);parse_counts['jsonl_rows']+=1
            parse_counts['jsonl']+=1
        if p.suffix=='.md':b.decode('utf-8');require(b'\x00' not in b,'NUL in Markdown')
    sm=read(root/'SOURCE_MANIFEST.json');inputs={x['label']:x for x in sm['inputs']}
    for label,(fn,h,mh) in PINS.items():
        require(inputs[label]['sha256']==h and inputs[label]['manifest_sha256']==mh,'source identity changed')
        require(sha((root/'sources'/label/'MANIFEST.json').read_bytes())==mh,'pinned original manifest changed')
    manifests={k:{e['path']:e for e in read(root/'sources'/k/'MANIFEST.json')['files']} for k in PINS}
    for s in sm['included_immutable_source_files']:
        b=(root/safe_path(s['path'])).read_bytes();require(sha(b)==s['sha256'] and len(b)==s['size_bytes'],'included source mismatch')
        if s['member']!='MANIFEST.json':
            e=manifests[s['package_label']][s['member']];require(e['sha256']==sha(b) and e['size_bytes']==len(b),'included source differs from pinned ZIP manifest')
    checked_zips=[]
    for zpath in input_zips:
        p=Path(zpath);h=sha(p.read_bytes());labels=[k for k,v in PINS.items() if v[1]==h];require(len(labels)==1,'input ZIP SHA mismatch')
        label=labels[0]
        with zipfile.ZipFile(p) as z:
            require(z.testzip() is None,'ZIP CRC failure');ns=[i.filename for i in z.infolist() if not i.is_dir()]
            require(len(ns)==len(set(ns)),'ZIP duplicate member');prefix=ns[0].split('/')[0]+'/'
            for n in ns:safe_path(n)
            manifest=loads(z.read(prefix+'MANIFEST.json'));require(sha(z.read(prefix+'MANIFEST.json'))==PINS[label][2],'ZIP manifest identity')
            require({prefix+e['path'] for e in manifest['files']}==set(ns)-{prefix+'MANIFEST.json'},'ZIP manifest scope')
            for e in manifest['files']:
                b=z.read(prefix+e['path']);require(sha(b)==e['sha256'] and len(b)==e['size_bytes'],'ZIP member integrity')
        checked_zips.append(label)
    bp=root/'sources/BATCH09/CUMULATIVE_RECOMMENDATIONS_1070.jsonl';mp=root/'sources/4D0/NAMES_REVIEW_MASTER_1070.jsonl'
    require(bp.read_bytes()==(root/'sources/QA01/CUMULATIVE_RECOMMENDATIONS_1070.jsonl').read_bytes(),'QA baseline ledger changed')
    baseline=lines(bp);bindex=index(baseline);masters=lines(mp);mi=index(masters);accepted=lines(root/'NAMES_ACCEPTED_1070.jsonl');ai=index(accepted)
    require(len(baseline)==len(masters)==len(accepted)==1070,'target count not 1070')
    require(set(mi)==set(bindex)==set(ai),'exact target coverage mismatch');require(keyhash(ai)==KEYSET,'frozen exact UTF8 keyset mismatch')
    require(list(ai)==list(bindex),'original ledger order changed')
    lock=read(root/'sources/4D0/TARGET_UNIVERSE_LOCK.json');require(lock['manifest_sha256']==FROZEN and lock['exact_keyset_sha256']==KEYSET and lock['frozen'],'target lock')
    qa=index(lines(root/'sources/QA01/QA_AMENDMENTS_5.jsonl'));li=index(read(root/'sources/QA01/CONTEXT_LIMITATIONS.json')['limited_keys'])
    require(set(qa)==set(AMEND) and set(li)==LIMITED,'amendment/limitation key mismatch')
    plan=read(root/'sources/QA01/ACCEPTANCE_PLAN.json');groups={k:g['group_id'] for g in plan['groups'] for k in g['speaker_keys']}
    require(set(groups)==set(ai) and {g['group_id']:len(g['speaker_keys']) for g in plan['groups']}=={'A':1059,'B':5,'C':6},'acceptance group coverage')
    event=read(root/'ACCEPTANCE_EVENT.json');require(event['all_accepted'] and event['target_count']==1070 and event['authority']=='CURRENT_USER_REQUEST_EXPLICIT_ACCEPTANCE','user acceptance event')
    require(event['amendments']=={k:v[1] for k,v in AMEND.items()} and set(event['context_limited_keys'])==LIMITED,'acceptance scope')
    require(event['reported_user_statement']=='모두 수락' and not event['evidence_completeness_promoted'] and not event['knowledge_approval_promoted'],'approval boundary')
    require(not event['runtime_verified'] and not event['production_deployed'] and not event['merge_authorized'],'publication gate')
    bl=bp.read_bytes().splitlines(keepends=True);ml=mp.read_bytes().splitlines(keepends=True)
    expected={}
    for i,a in enumerate(accepted):
        k=a['speaker_jp'];b=bindex[k];m=mi[k];expected[k]=AMEND[k][1] if k in AMEND else b['recommended_ko']
        require(a['accepted_ko']==a['final_ko']==expected[k] and isinstance(expected[k],str) and expected[k],'accepted value disagrees with authority')
        require(a['speaker_jp_utf8_sha256']==sha(k.encode())==b['speaker_jp_utf8_sha256'],'key bytes changed')
        require(a['historical_semantic_record']==b and a['historical_workbench_record']==m,'old provenance silently rewritten')
        require(a['acceptance_status']=='USER_ACCEPTED' and a['acceptance_event_id']==event['event_id'] and a['acceptance_group']==groups[k],'acceptance not connected')
        require(a['baseline_provenance']['line_1based']==i+1 and a['baseline_provenance']['record_sha256_including_eol']==sha(bl[i]),'baseline record hash/order')
        require(b['source_master_record_sha256_including_eol']==sha(ml[b['source_master_line_1based']-1]) and masters[b['source_master_line_1based']-1]['speaker_jp']==k,'workbench lineage')
        require(a['historical_qa_limitation']==li.get(k) and a['qa01_amendment_source']==qa.get(k),'QA provenance changed')
        require(a['context_limitation_flags']==([li[k]['category']] if k in li else []),'context limitation flags removed/changed')
        require(not a['evidence_limitations_resolved_by_acceptance'] and not a['knowledge_status_promoted'] and not a['original_observation_mutated'],'evidence/state promotion')
        require(a['production_state']=='PRODUCTION_CANDIDATE' and not a['runtime_verified'] and not a['production_deployed'] and not a['merged'],'candidate mislabeled published')
        if k in qa:
            q=qa[k];require((q['before_recommended_ko'],q['proposed_recommended_ko'])==AMEND[k] and b['recommended_ko']==AMEND[k][0],'amendment value')
            require(q['baseline_record_sha256_including_eol']==sha(bl[q['baseline_line_1based']-1]),'amendment source line')
        if k in li:require(li[k]['context_complete'] is False,'historical context falsely completed')
    r34=ai['不審な男改め死の芸術家'];require(r34['historical_semantic_record']['inherited_speaker_approval']['effective_ko']==AMEND[r34['speaker_jp']][0],'R34 historical approval changed')
    require(r34['accepted_value_source']=='QA01_AMENDMENT_WITH_NEW_USER_ACCEPTANCE','R34 new approval missing')
    applied=read(root/'QA01_AMENDMENTS_APPLIED.json');applied_index=index(applied['rows'])
    require(applied['count']==5 and set(applied_index)==set(AMEND),'applied amendment report scope')
    for k,a in applied_index.items():
        require(a['accepted_ko']==expected[k] and a['previous_recommended_ko']==AMEND[k][0] and a['original_qa_record']==qa[k] and a['new_acceptance']==event['event_id'],'applied amendment report mismatch')
    limitations=read(root/'CONTEXT_LIMITATIONS_ACCEPTED_WITH_FLAGS.json');limited_index=index(limitations['rows'])
    require(limitations['count']==6 and set(limited_index)==LIMITED and not limitations['context_limitations_resolved'],'limitation report scope')
    for k,l in limited_index.items():
        require(l['accepted_ko']==expected[k] and not l['context_complete'] and l['limitation_flag']==li[k]['category'] and l['historical_qa_limitation']==li[k] and l['historical_semantic_record']==bindex[k],'limitation report mismatch')
    candidate=read(root/'names/zh_Hans.json');require(candidate==expected and list(candidate)==list(expected),'candidate deterministic roundtrip failed')
    require((root/'names/zh_Hans.json').read_bytes()==json.dumps(expected,ensure_ascii=False,indent=4).encode(),'candidate serialization mismatch')
    controls=[k for k,v in candidate.items() if any(unicodedata.category(c) in ['Cc','Cf','Cs'] for c in k+v)];require(not controls,'control-character anomaly')
    fixture=read(root/'ROUNDTRIP_VALIDATION.json')['fixtures']
    for pair in fixture['exact_distinct_pairs']:require(len(set(pair))==2 and all(k in candidate for k in pair),'distinct pair regression')
    for group,keys in fixture.items():
        if group!='exact_distinct_pairs':require(keys and all(k in candidate for k in keys),'regression fixture missing: '+group)
    require(set(fixture['qa01_amendments'])==set(AMEND) and set(fixture['context_limited'])==LIMITED,'regression scope')
    # Dictionary identity only. No normalizer/alias lookup is used.
    for k in candidate:
        probe=' '+k
        if probe not in expected:require(candidate.get(probe,probe)==probe,'unmapped fallback regression')
    delta=read(root/'LIVE_DELTA_AUDIT.json');live=lines(root/'live_delta/CURRENT_EXACT_KEYS.jsonl');liveindex=index(live)
    require(sha((root/'live_delta/MANIFEST_SNAPSHOT.json').read_bytes())==FROZEN==delta['current_manifest_sha256'],'live snapshot identity')
    require(set(liveindex)==set(ai) and delta['status']=='DELTA_CLEAR' and not delta['needs_pro_semantic_review'],'delta not clear')
    require(not lines(root/'live_delta/ADDED_CURRENT_KEYS.jsonl') and not lines(root/'live_delta/REMOVED_CURRENT_KEYS.jsonl'),'nonzero exact delta')
    require(set(index(lines(root/'live_delta/UNCHANGED_KEYS.jsonl')))==set(ai),'unchanged key coverage')
    ds=lines(root/'live_delta/ASSET_CACHE_REPARSE_AUDIT.jsonl');scripts=lines(root/'live_delta/CONFIRMED_SCRIPTS.jsonl');si={s['script_id']:s for s in scripts}
    require(len(ds)==1972 and len({d['asset_path'] for d in ds})==1972 and len(si)==len(scripts)==1864,'candidate/script coverage')
    assets={a['n']:a for a in read(root/'live_delta/MANIFEST_SNAPSHOT.json')['d']};require(len(assets)==22684,'manifest asset count')
    for d in ds:
        a=assets[d['asset_path']];require(d['manifest_h']==a['h'] and d['size_bytes']==a['s'] and d['cache_reused'] and d['content_status'] in ['CONFIRMED_NOVEL_SCRIPT','REJECTED_NO_TEXTASSET','REJECTED_NON_SCRIPT_TEXTASSETS'],'bundle provenance')
    di={d['asset_path']:d for d in ds}
    for s in scripts:require(s['bundle_sha256']==di[s['asset_path']]['bundle_sha256'] and s['name_fields_match_unmodified_source'],'script provenance')
    for k,r in liveindex.items():
        require(r['speaker_jp_utf8_sha256']==sha(k.encode()) and all(s in si for s in r['script_ids']),'live key provenance')
        require(r['occurrence_count']>0 and r['script_count']==len(set(r['script_ids'])),'live key count')
        for loc in r['representative_locators']:
            s=si[loc['script_id']];require(loc['speaker_jp']==k and loc['script_sha256']==s['script_sha256'] and loc['bundle_sha256']==s['bundle_sha256'],'locator identity')
    require(sum(s['parsed_rows'] for s in scripts)==138822 and sum(s['blank_name_rows'] for s in scripts)==33752 and sum(r['occurrence_count'] for r in live)==105070,'parsed count consistency')
    return {'status':'PASS','target':1070,'accepted':1070,'amendments':5,'limitations':6,'duplicate_missing_extra':0,'roundtrip':1070,'immutable_source_members':len(sm['included_immutable_source_files']),'output_manifest_files':len(files),'parse':dict(parse_counts),'input_zips_rechecked':checked_zips,'delta':'DELTA_CLEAR','runtime_verified':False}
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('checkpoint',nargs='?',default=str(Path(__file__).resolve().parents[1]));p.add_argument('--input-zip',action='append',default=[]);a=p.parse_args()
    try:result=verify(a.checkpoint,a.input_zip)
    except Exception as e:print(json.dumps({'status':'FAIL','error_type':type(e).__name__,'reason':str(e)},ensure_ascii=True));return 1
    print(json.dumps(result,ensure_ascii=True));return 0
if __name__=='__main__':raise SystemExit(main())
