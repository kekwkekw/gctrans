"""Isolated signed, read-only extraction. Never imports/calls Updater.
Python 3.12 + UnityPy==1.10.18 + pycryptodome + httpx==0.27.2.
Repo functions are loaded from verified Git blobs into memory without pyc files.
No signing key, signed URL, story body or translated body is written to audit outputs.
"""
import sys
sys.dont_write_bytecode=True
import argparse, ast, collections, concurrent.futures, datetime, hashlib, json, re, subprocess, threading, time, types
from pathlib import Path

MAIN='6c9be9e795a6ce686f35a6ccaec4481066b240e1'
TREE='1a748fb02a4ab5db712a17d4dc5aca873efc15af'
MANIFEST_SHA='62355171145edc1a19d8e88e9f2def46fa0e9a23f46c8c295742c168ac0be44d'
def sha(b):return hashlib.sha256(b).hexdigest()
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def enc(x):return (json.dumps(x,ensure_ascii=False,indent=2)+'\n').encode('utf-8')
def write_json(path,obj):path.write_bytes(enc(obj))

def main():
    p=argparse.ArgumentParser()
    for name in ['repo','deps','manifest','cache','run-dir']:p.add_argument('--'+name,required=True)
    p.add_argument('--pilot',action='store_true');p.add_argument('--offline',action='store_true')
    p.add_argument('--workers',type=int,default=4)
    a=p.parse_args();assert 1<=a.workers<=8
    repo=Path(a.repo).resolve();cache=Path(a.cache).resolve();run=Path(a.run_dir).resolve()
    assert not cache.is_relative_to(repo) and not run.is_relative_to(repo)
    cache.mkdir(parents=True,exist_ok=True);run.mkdir(parents=True,exist_ok=True)
    sys.path.insert(0,str(Path(a.deps).resolve()))
    import UnityPy, httpx
    g=['git','-c','safe.directory='+repo.as_posix(),'-C',str(repo)]
    def git(*args):return subprocess.check_output(g+list(args))
    assert git('rev-parse','HEAD^{tree}').decode().strip()==TREE
    status=git('status','--porcelain=v1','-z','--untracked-files=all');refs=git('show-ref')
    source_ids={}
    def source(path):
        blob=git('rev-parse','HEAD:'+path).decode().strip();b=git('cat-file','blob',blob)
        source_ids[path]={'commit':MAIN,'blob':blob,'sha256':sha(b),'size_bytes':len(b)}
        return b
    crypto=types.ModuleType('gc_existing_crypto');exec(compile(source('scripts/crypto.py'),'verified_git/scripts/crypto.py','exec'),crypto.__dict__)
    parser=types.ModuleType('gc_existing_parse');exec(compile(source('scripts/parse.py'),'verified_git/scripts/parse.py','exec'),parser.__dict__)
    update_tree=ast.parse(source('scripts/update.py'))
    cls=next(n for n in update_tree.body if isinstance(n,ast.ClassDef) and n.name=='Updater')
    assignments={n.targets[0].id:n.value for n in cls.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name)}
    base=ast.literal_eval(assignments['BASE_URL']);assetprefix=ast.literal_eval(assignments['ASSET_PATH'])
    pattern=ast.literal_eval(assignments['NOVEL_PATTERN'].args[0]);pat=re.compile(pattern)
    assert base=='https://cdn-r18.gc.dmmgames.com' and assetprefix=='/secure/data/production/webgl/resources/'
    mb=Path(a.manifest).read_bytes();assert sha(mb)==MANIFEST_SHA
    manifest=json.loads(mb);assets=manifest['d'];assert len(assets)==len({x['n'] for x in assets})
    classed=[];candidates=[];families=collections.defaultdict(list)
    for asset in assets:
        path=asset['n'];existing=bool(pat.match(path));novel='novel_script' in path
        related=any(t in path.lower() for t in ['script','novel','scenario'])
        family='/'.join(path.split('/')[:-1]);families[family].append(asset)
        classification='EXISTING_PATTERN_MATCH' if existing else 'NOVEL_SCRIPT_RELATED_ADDITIONAL' if novel else 'OTHER_SCRIPT_NOVEL_NAMING' if related else 'UNRELATED'
        row={'asset':asset,'classification':classification,'existing_pattern_match':existing,'novel_script_related':novel,'candidate':existing or novel}
        classed.append(row)
        if existing or novel:candidates.append(row)
    assert len(candidates)==len({(x['asset']['n'],x['asset']['h']) for x in candidates})
    otherfamilies=[]
    for family,members in sorted(families.items()):
        outside=[x for x in classed if '/'.join(x['asset']['n'].split('/')[:-1])==family and x['classification']=='OTHER_SCRIPT_NOVEL_NAMING']
        if not outside:continue
        ps=[x['asset']['n'] for x in outside]
        if family.startswith('notinit/novelchara_') and all(x.endswith('_image.dmm') for x in ps):reason='Image companion suffix _image; scene script with same prefix/stem is independently selected when present.'
        elif 'image_' in family or 'texture' in family or 'sprite_atlas' in family:reason='Image/texture/atlas resource family naming; no scenario TextAsset naming evidence.'
        elif family=='sound/se':reason='Sound effect family naming.'
        elif family.startswith('popup') or family.startswith('scene'):reason='Unity UI popup/scene resource family naming, not scenario data file naming.'
        elif family in ['notinit/novel_emote','notinit/novel_prefab']:reason='Novel presentation emote/prefab family naming, not a scenario script family.'
        else:reason='UNRESOLVED_RELATED_FAMILY'
        otherfamilies.append({'family':family,'count':len(ps),'declared_bytes':sum(x['asset']['s'] for x in outside),'representatives':ps[:3],
            'classification':'EXCLUDED_PRESENTATION_RESOURCE_BY_NAMING' if reason!='UNRESOLVED_RELATED_FAMILY' else 'UNRESOLVED','reason':reason,
            'content_inspected':False,'source':'Pinned manifest path names; no semantic judgment of speaker keys'})
    family_audit={'manifest_sha256':MANIFEST_SHA,'total_assets':len(assets),'exclusive_classification_counts':dict(collections.Counter(x['classification'] for x in classed)),
        'novel_script_related_total':sum(x['novel_script_related'] for x in classed),'existing_pattern':pattern,'candidate_count':len(candidates),
        'candidate_declared_bytes':sum(x['asset']['s'] for x in candidates),'deduplication':'Exact (asset path, manifest h) identity',
        'duplicate_candidate_pairs':0,'duplicate_candidate_hashes':len(candidates)-len({x['asset']['h'] for x in candidates}),
        'additional_script_candidates_outside_union':[],'other_related_families':otherfamilies,
        'family_counts':[{'family':k,'count':len(v)} for k,v in sorted(families.items())]}
    write_json(run/'ASSET_FAMILY_AUDIT.json',family_audit)
    (run/'ASSET_CLASSIFICATION.jsonl').write_bytes(b''.join((json.dumps(x,ensure_ascii=False)+'\n').encode('utf-8') for x in classed))
    write_json(run/'SOURCE_CODE_IDENTITIES.json',source_ids)
    selected=candidates
    if a.pilot:
        predicates=[lambda n:'/btl_' in n,lambda n:'/dns_' in n,lambda n:n.endswith('/mas_10010101.dmm'),lambda n:n.endswith('_image.dmm'),lambda n:'/novelchara_main_r18/' in n]
        selected=[]
        for pred in predicates:
            found=min((x for x in candidates if pred(x['asset']['n'])),key=lambda x:x['asset']['s'])
            if found not in selected:selected.append(found)
    stop=threading.Event();lock=threading.Lock();counts=collections.Counter();out_records=[]
    client=httpx.Client(timeout=httpx.Timeout(60,connect=15,pool=30),limits=httpx.Limits(max_connections=a.workers,max_keepalive_connections=a.workers),follow_redirects=False)
    def fetch(row):
        asset=row['asset'];identity=sha((asset['n']+'\0'+asset['h']).encode());bp=cache/(identity+'.bundle');dp=cache/(identity+'.download.json')
        record={'asset_path':asset['n'],'manifest_h':asset['h'],'manifest_sha256':MANIFEST_SHA,'manifest_declared_size':asset['s'],'classification':row['classification'],'cache_id':identity}
        if bp.exists() and dp.exists():
            saved=json.loads(dp.read_bytes());data=bp.read_bytes();assert saved['bundle_sha256']==sha(data) and saved['asset_path']==asset['n'] and saved['manifest_h']==asset['h']
            record.update(saved);record['cache_reused']=True;return record,data
        if a.offline:record.update(status='CACHE_MISSING',http_status=None,attempts=[]);return record,None
        if stop.is_set():record.update(status='NOT_ATTEMPTED_AFTER_STOP',http_status=None,attempts=[]);return record,None
        attempts=[];forbidden=0
        for attempt in range(3):
            if stop.is_set():break
            assetpath=assetprefix+asset['n'];params=crypto.get_url_params(assetpath,asset['h']);started=now()
            try:
                with client.stream('GET',base+assetpath,params=params) as response:
                    code=response.status_code;attempts.append({'attempt':attempt+1,'utc':started,'http_status':code,'params_freshly_generated':True})
                    if code==200:
                        chunks=[];total=0
                        for part in response.iter_bytes():
                            total+=len(part)
                            if total>max(asset['s']*2,128*1024*1024):raise ValueError('BUNDLE_SIZE_LIMIT')
                            chunks.append(part)
                        data=b''.join(chunks)
                        record.update(status='DOWNLOADED',http_status=200,attempts=attempts,fetched_utc=started,size_bytes=len(data),bundle_sha256=sha(data),
                            bundle_md5=hashlib.md5(data).hexdigest(),manifest_size_match=len(data)==asset['s'],cache_reused=False,
                            response_headers={k:v for k,v in response.headers.items() if k.lower() in ['content-type','last-modified','etag','date']})
                        bp.write_bytes(data);write_json(dp,record);return record,data
                    if code==403:
                        forbidden+=1
                        if forbidden>=2:break
                        time.sleep(1);continue
                    if code==404:break
                    if code==429 or 500<=code<600:
                        if attempt<2:
                            retry=response.headers.get('retry-after','')
                            if retry.isdigit() and int(retry)>60:break
                            time.sleep(max(2**(attempt+1),int(retry) if retry.isdigit() else 0));continue
                    break
            except Exception as exc:
                # Never stringify HTTP exceptions: they can contain signed URLs.
                attempts.append({'attempt':attempt+1,'utc':started,'error_type':type(exc).__name__})
                if attempt<2:time.sleep(2**(attempt+1));continue
        lastcode=next((x['http_status'] for x in reversed(attempts) if 'http_status' in x),None)
        record.update(status='DOWNLOAD_FAILED',http_status=lastcode,attempts=attempts,forbidden_after_fresh_retry=forbidden>=2)
        with lock:
            if forbidden>=2:counts['terminal_403']+=1
            if counts['terminal_403']>=3:stop.set()
        write_json(dp,record);return record,None
    def process(row):
        rec,data=fetch(row);result={'download':rec,'scripts':[],'speaker_occurrences':[]}
        if data is None:return result
        try:
            env=UnityPy.load(data);objtypes=collections.Counter(o.type.name for o in env.objects)
            objects=[o for o in env.objects if o.type.name=='TextAsset'];rec['object_type_counts']=dict(objtypes);rec['textasset_count']=len(objects)
            if not objects:rec['content_status']='REJECTED_NO_TEXTASSET';return result
            rec['textasset_audit']=[]
            for obj in objects:
                ta={'textasset_path_id':obj.path_id}
                try:
                    asset=obj.read();name=asset.name;sb=bytes(asset.script);ta.update(script_name=name,script_sha256=sha(sb),script_bytes=len(sb))
                    try:text=sb.decode('utf-8')
                    except UnicodeDecodeError:
                        if name.lower().endswith('.usm') and sb.startswith(b'CRID'):
                            ta.update(status='REJECTED_BINARY_TEXTASSET',reason='Non-UTF8 .usm TextAsset with CRID header; not a textual scenario',binary_header_hex=sb[:4].hex())
                            rec['textasset_audit'].append(ta);continue
                        raise
                    rows=parser.parse_script(text)
                    textlines=text.split('\n')
                    recognized=[(n,line.split(',')[0]) for n,line in enumerate(textlines,1) if line.startswith(('title','message','msgvoicesync'))]
                    assert len(recognized)==len(rows)
                    if not rows:ta['status']='REJECTED_NO_RECOGNIZED_ROWS';rec['textasset_audit'].append(ta);continue
                    script_id=identity_for_script=sha((rec['asset_path']+'\0'+str(obj.path_id)+'\0'+sha(sb)).encode())
                    stats=collections.Counter();namechars=collections.Counter();occ=[]
                    for idx,((lineno,cmd),msg) in enumerate(zip(recognized,rows)):
                        key=msg['name'];assert isinstance(key,str)
                        source_line=textlines[lineno-1]
                        expected='' if source_line.startswith('title') else source_line.split(',')[1] if source_line.startswith('message') else source_line.split(',')[2]
                        assert key==expected
                        stats[cmd]+=1
                        if not key:continue
                        occ.append({'speaker_jp':key,'script_id':script_id,'script_name':name,'asset_path':rec['asset_path'],
                            'textasset_path_id':obj.path_id,'message_index_0based':idx,'raw_line_1based':lineno,'command':cmd,
                            'manifest_h':rec['manifest_h'],'bundle_sha256':rec['bundle_sha256'],'script_sha256':sha(sb)})
                    script={'script_id':script_id,**ta,'asset_path':rec['asset_path'],'manifest_h':rec['manifest_h'],'bundle_sha256':rec['bundle_sha256'],
                        'manifest_sha256':MANIFEST_SHA,'parsed_rows':len(rows),'blank_name_rows':len(rows)-len(occ),'nonblank_name_rows':len(occ),
                        'exact_speaker_keys':len({x['speaker_jp'] for x in occ}),'command_counts':dict(stats),'status':'CONFIRMED_NOVEL_SCRIPT','name_fields_match_unmodified_source':True}
                    result['scripts'].append(script);result['speaker_occurrences'].extend(occ);ta['status']='CONFIRMED_NOVEL_SCRIPT'
                except Exception as exc:
                    ta.update(status='TEXTASSET_PARSE_FAILED',error_type=type(exc).__name__)
                    stop.set()
                rec['textasset_audit'].append(ta)
            rec['content_status']='CONFIRMED_NOVEL_SCRIPT' if result['scripts'] else 'REJECTED_NON_SCRIPT_TEXTASSETS'
            if any(t['status']=='TEXTASSET_PARSE_FAILED' for t in rec['textasset_audit']):rec['content_status']='TEXTASSET_PARSE_FAILED'
            # Repository's first-TextAsset parse_bundle is checked against all-TextAsset equivalent extraction.
            if rec['textasset_audit'][0]['status']=='CONFIRMED_NOVEL_SCRIPT':
                first=parser.parse_bundle(data)
                rec['original_parse_bundle_first_textasset_verified']=bool(first and first[0]==rec['textasset_audit'][0].get('script_name') and sha(first[1].encode('utf-8'))==rec['textasset_audit'][0].get('script_sha256'))
            else:rec['original_parse_bundle_first_textasset_verified']=None
        except Exception as exc:
            rec.update(content_status='BUNDLE_PARSE_FAILED',error_type=type(exc).__name__)
            with lock:counts['bundle_parse_failures']+=1
            if counts['bundle_parse_failures']>=3:stop.set()
        return result
    started=now();suffix='PILOT' if a.pilot else 'FULL'
    outpath=run/(suffix+'_RESULTS.jsonl')
    with outpath.open('w',encoding='utf-8',newline='\n') as output,concurrent.futures.ThreadPoolExecutor(max_workers=a.workers) as pool:
        futures=[pool.submit(process,row) for row in selected]
        for future in concurrent.futures.as_completed(futures):
            res=future.result();output.write(json.dumps(res,ensure_ascii=False)+'\n');output.flush();out_records.append(res)
            if len(out_records)%100==0:
                print(json.dumps({'completed':len(out_records),'selected':len(selected),'confirmed':sum(bool(r['scripts']) for r in out_records),'stop':stop.is_set()}),flush=True)
    client.close()
    assert status==git('status','--porcelain=v1','-z','--untracked-files=all') and refs==git('show-ref')
    summary={'started_utc':started,'finished_utc':now(),'mode':suffix,'manifest_sha256':MANIFEST_SHA,'selected_assets':len(selected),
        'download_status_counts':dict(collections.Counter(x['download']['status'] for x in out_records)),
        'content_status_counts':dict(collections.Counter(x['download'].get('content_status','NOT_PARSED') for x in out_records)),
        'http_status_counts':dict(collections.Counter(str(x['download'].get('http_status')) for x in out_records)),
        'confirmed_script_assets':sum(bool(x['scripts']) for x in out_records),'confirmed_textassets':sum(len(x['scripts']) for x in out_records),
        'parsed_rows':sum(s['parsed_rows'] for x in out_records for s in x['scripts']),
        'blank_name_rows':sum(s['blank_name_rows'] for x in out_records for s in x['scripts']),
        'exact_keys':len({o['speaker_jp'] for x in out_records for o in x['speaker_occurrences']}),'stop':stop.is_set(),'repository_mutation':0,
        'dependency_versions':{'python':sys.version.split()[0],'UnityPy':UnityPy.__version__,'httpx':httpx.__version__},'source_code_identities':source_ids}
    write_json(run/(suffix+'_SUMMARY.json'),summary);print(json.dumps(summary),flush=True)

if __name__=='__main__':main()
