"""Read-only full-text census from existing asset extraction or explicit PC files.

All text values, nested Master strings, language tails and nonstandard scripts
remain evidence. Only identified media is excluded. Engine schema keys are
aggregated where trees are read; existing complete value indexes are reused.
The output is private and cannot be passed directly to translation apply.
"""
from pathlib import Path
import argparse, collections, hashlib, json, os, re, sys, time, traceback
import csv, io, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
sys.path[:0]=[str(ROOT),str(ROOT/'tools')]
from tools.update.text_census import Census, walk_text, decode_textasset, master_role, MEDIA_TYPES, sha
from tools.update.asset_source import file_hash, read_object_typetree, is_spine
from tools.update.source_scan import NOVEL_PATTERN, safe_key, normalize_inventory
from tools.novel_workflow import FIELDS
from tools.update.unity_schema_text import read_verified_schema, load_registry, harvest_registry, carve_unity_strings

OUT=RAW=STATE=PC_DATA=None
MANIFESTS=[]
MASTER={}
SCHEMAS={}
PC_SCOPE='all'
PC_PROFILE='PC_INSTALLED_OR_CACHE_OBSERVED'
DECODE_MASTER=None

def save(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def index_ref(c,aid,base,ref,purpose):
    path=(base/safe_key(ref['path'])).resolve()
    if not path.is_relative_to(base.resolve()):raise ValueError('EXTRACTION_REF_ESCAPES_ROOT')
    raw=path.read_bytes()
    if len(raw)!=ref['size'] or sha(raw)!=ref['sha256']:raise ValueError('EXTRACTION_REF_HASH_MISMATCH')
    c.file(aid,path,ref['sha256'],len(raw),purpose)
    return raw

def tree_text(c,aid,tree,loc,role='serialized_string',table=None):
    for parts,value,is_key in walk_text(tree):
        if table:
            row_role,target,query=master_role(parts,is_key,table,MASTER.get(table,{}),value)
        else:
            target=query=None
            row_role='structural_key' if is_key else 'ui_display' if parts and parts[-1] in ('m_text','m_Text') else 'object_name' if parts and parts[-1]=='m_Name' else role
        c.add(aid,value,row_role,{**loc,'field':list(parts),'dictionary_key':is_key},target,query)

def structured_text(c,aid,value,name,loc):
    """Retain the whole payload above, plus independently addressable cells."""
    first=value.partition('\n')[0]
    columns=next(csv.reader([first]),[])
    localization={'id','label','ja'}<=set(columns)
    if name.lower().endswith('.csv') or localization or columns==['Key','Value'] or name=='BaseMasterModelMapping':
        try:rows=list(csv.reader(io.StringIO(value),strict=True))
        except csv.Error as error:
            c.disposition(aid,loc,'gap','TEXT_CSV_PARSE_FAILED',{'exception':type(error).__name__});return
        for number,row in enumerate(rows):
            for column,cell in enumerate(row):
                field=columns[column] if column<len(columns) else None
                role='textasset_csv_cell'
                if localization:
                    role='localization_header' if number==0 else 'localization_jp' if field=='ja' else 'localization_key' if field in ('id','label') else 'localization_secondary_locale'
                c.add(aid,cell,role,{**loc,'csv_record':number,'csv_column':column,'csv_header':field,
                                   'localization_id':row[0] if row else None,'localization_label':row[1] if len(row)>1 else None})
    elif value.lstrip().startswith('<?xml') or name.lower().endswith('.xml'):
        try:root=ET.fromstring(value,parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True)))
        except ET.ParseError:
            c.disposition(aid,loc,'gap','TEXT_XML_PARSE_FAILED');return
        for number,element in enumerate(root.iter()):
            tag=element.tag if isinstance(element.tag,str) else '#comment'
            c.add(aid,tag,'structural_key',{**loc,'xml_node':number})
            for key,cell in element.attrib.items():
                c.add(aid,key,'structural_key',{**loc,'xml_node':number,'xml_attribute':key})
                c.add(aid,cell,'textasset_xml_value',{**loc,'xml_node':number,'xml_tag':tag,'xml_attribute':key})
            if element.text is not None:c.add(aid,element.text,'textasset_xml_comment' if tag=='#comment' else 'textasset_xml_value',{**loc,'xml_node':number,'xml_tag':tag})
            if element.tail is not None:c.add(aid,element.tail,'textasset_xml_value',{**loc,'xml_node':number,'xml_tag':tag,'tail':True})

def textasset(c,aid,raw,name,loc,key,decoded=None):
    if decoded is not None:
        tree_text(c,aid,decoded,loc,table=name);return
    if re.search(r'(?:^|/)master\.dmm(?:/|$)',key) and raw:
        # PC caches may retain several generations. Never merge their results
        # into the current WebGL source or treat ciphertext as readable text.
        if DECODE_MASTER is None:
            c.disposition(aid,loc,'gap','MASTER_PRIVATE_DECODER_NOT_CONFIGURED',{'payload_sha256':sha(raw),'bytes':len(raw)})
        else:
            try:
                decoded=DECODE_MASTER(raw)
                if not isinstance(decoded,(dict,list)):raise ValueError('MASTER_DECODE_NOT_STRUCTURED')
                tree_text(c,aid,decoded,{**loc,'payload_sha256':sha(raw)},table=name)
                return
            except Exception as error:
                c.disposition(aid,loc,'gap','MASTER_DECODE_FAILED',{'exception':type(error).__name__,'payload_sha256':sha(raw),'bytes':len(raw)})
    status,reason,value=decode_textasset(raw,name)
    if status!='text':
        c.disposition(aid,loc,status,reason,{'sha256':sha(raw),'bytes':len(raw),'name':name,'header_hex':raw[:24].hex()})
        return
    if isinstance(value,str):
        lines=value.splitlines()
        if any(line.split(',',1)[0] in FIELDS for line in lines):
            match=NOVEL_PATTERN.fullmatch(key)
            battle=re.search(r'btl_\d+_(\d+)',name+' '+key)
            identity=match[1] if match else battle[1] if battle else None
            # Retain all commands/arguments, including those not selected by Novel intake.
            for number,line in enumerate(lines,1):
                fields=line.split(',');command=fields[0]
                for index,field in enumerate(fields):
                    role='script_command' if index==0 else 'script_argument'
                    target=None
                    if command in FIELDS and index==FIELDS[command]:
                        role='novel_text';target=('novel',identity) if identity else None
                    elif command in FIELDS and command!='title' and index==FIELDS[command]-1:role='novel_speaker'
                    c.add(aid,field,role,{**loc,'physical_line':number,'command':command,'argument':index,'source_id':identity},target,field if target else None)
        else:
            c.add(aid,value,'textasset_plain',loc)
            structured_text(c,aid,value,name,loc)
    else:tree_text(c,aid,value,loc,role='textasset_json')

def webgl():
    c=Census(OUT/'webgl.sqlite');start=time.time()
    manifests=[json.loads(path.read_bytes()) for path in MANIFESTS]
    normalized=normalize_inventory({'d':[r for m in manifests for r in m['d']]},'All',max_entries=None)
    if normalized['collisions']:raise ValueError('SOURCE_INVENTORY_COLLISION')
    items=[{'n':r['asset_key'],'h':r['vendor_token']} for r in normalized['items']]
    for number,item in enumerate(items,1):
        key=item['n'];h=sha(key.encode());state_path=STATE/h[:2]/(h+'.json')
        state_raw=state_path.read_bytes();state=json.loads(state_raw)
        if state['vendor_token']!=item['h']:raise ValueError('STATE_TOKEN_STALE:'+key)
        raw=state.get('raw',{});aid,done=c.asset('WebGL_CURRENT',key,item['h'],raw.get('sha256',''),sha(state_raw),{'source_status':state['status'],'raw':raw})
        if done:continue
        c.file(aid,state_path,sha(state_raw),len(state_raw),'owned_state')
        if not raw or state['status']!='EXTRACTED':c.disposition(aid,{},'gap','ASSET_NOT_FULLY_EXTRACTED',{'status':state['status']})
        if raw:
            p=RAW/safe_key(raw['path'])
            if not p.is_file() or p.stat().st_size!=raw['size']:raise ValueError('RAW_MISSING_OR_SIZE_CHANGED:'+key)
        ex=state.get('extraction')
        if ex:
            # Index bytes are verified; raw ownership refers to the preserved prior acquisition hash.
            objects=index_ref(c,aid,RAW,ex['indexes']['objects'],'objects_index')
            indexed=index_ref(c,aid,RAW,ex['indexes']['strings'],'strings_index')
            failures=index_ref(c,aid,RAW,ex['indexes']['failures'],'failures_index')
            for line in failures.splitlines():
                failure=json.loads(line)
                c.disposition(aid,failure.get('locator',{}),'gap','PRIOR_EXTRACTION_FAILURE',failure)
            # The old extractor retained ALL string values in this index. Its
            # coverage report, not this index, applied the Japanese-only gate.
            # Reuse those values without a candidate/language/mapping selector.
            for line in indexed.splitlines():
                row=json.loads(line)
                if row['kind'] in ('master','textasset'):continue  # Expanded below, without their old field/locale omissions.
                loc=row['locator'];field=loc.get('field','')
                role='ui_display' if row['kind']=='ui' else 'object_name' if field.endswith('["m_Name"]') else 'serialized_string'
                c.add(aid,row['jp'],role,loc)
            base=RAW/safe_key(ex['path'])
            for line in objects.splitlines():
                obj=json.loads(line);loc=obj['locator'];kind=loc['type']
                if kind in MEDIA_TYPES:
                    c.disposition(aid,loc,'excluded','NON_TEXT_UNITY_TYPE',{'type':kind});continue
                if obj.get('status'):
                    c.disposition(aid,loc,'gap','OBJECT_EXTRACTION_STATUS',{'status':obj['status']})
                exports=obj.get('exports',[])
                if kind=='TextAsset':
                    data=next((e for e in exports if e['path'].endswith('.bytes')),None)
                    dec=next((e for e in exports if e['path'].endswith('.decoded.json')),None)
                    if dec:
                        body=index_ref(c,aid,base,dec,'decoded_master');textasset(c,aid,b'',loc.get('name',''),loc,key,json.loads(body))
                    elif data:
                        body=index_ref(c,aid,base,data,'textasset_payload');textasset(c,aid,body,loc.get('name',''),loc,key)
                    else:c.disposition(aid,loc,'gap','TEXTASSET_PAYLOAD_NOT_EXPORTED')
                elif is_spine(key) or NOVEL_PATTERN.fullmatch(key):
                    # These enclosing bundles suppressed their general strings
                    # in the old extractor, so inspect their retained trees.
                    trees=[e for e in exports if e['path'].endswith('.json')]
                    if not trees:c.disposition(aid,loc,'gap','NO_SERIALIZED_TREE_EXPORT')
                    for ref in trees:
                        body=index_ref(c,aid,base,ref,'typetree');tree_text(c,aid,json.loads(body),loc)
        c.complete(aid)
        if number%200==0:print(json.dumps({'phase':'webgl','assets':number,'total':len(items),'unique_strings':len(c.text_ids),'elapsed_seconds':round(time.time()-start)}),flush=True)
    result=c.finish();result.update(source='EXPLICIT_SAVED_INVENTORIES_WITH_CURRENT_OWNED_STATE',raw_verification='owned raw size/existence and prior acquisition hash; every consumed extraction file hashed',seconds=round(time.time()-start))
    save(OUT/'webgl-summary.json',result);c.db.close();print(json.dumps(result),flush=True)

def pc():
    import UnityPy
    game=PC_DATA
    c=Census(OUT/'pc.sqlite');start=time.time()
    files=[p for p in game.iterdir() if p.is_file() and (p.suffix=='.assets' or p.name=='globalgamemanagers' or re.fullmatch(r'level\d+',p.name))]
    files+=[p for p in (game/'Resources').glob('*') if p.is_file()]
    if PC_SCOPE=='cache':files=[]
    if PC_SCOPE!='core':files+=[p for p in (game/'Caches').rglob('__data') if p.is_file()]
    files=sorted(set(files))
    for number,p in enumerate(files,1):
        key=p.relative_to(game).as_posix();digest=file_hash(p);aid,done=c.asset(PC_PROFILE,key,digest,digest,details={'current_manifest_authority':False,'cache_profile':PC_PROFILE})
        if done:continue
        c.file(aid,p,digest,p.stat().st_size,'pc_unity_source')
        try:env=UnityPy.load(str(p));objects=list(env.objects)
        except Exception as error:
            c.disposition(aid,{},'gap','PC_UNITY_LOAD_FAILED',{'exception':type(error).__name__});c.complete(aid);continue
        if not objects:c.disposition(aid,{},'gap','PC_NO_UNITY_OBJECTS')
        for obj in objects:
            kind=obj.type.name;loc={'container':str(obj.assets_file.name),'path_id':obj.path_id,'type':kind}
            if kind in MEDIA_TYPES:
                c.disposition(aid,loc,'excluded','NON_TEXT_UNITY_TYPE',{'type':kind});continue
            try:
                if obj.serialized_type.nodes:
                    recovery={};tree=read_object_typetree(obj,recovery)
                else:
                    # Never invoke the library's guessed built-in schema for
                    # stripped files. Only exact, round-tripping cache schemas.
                    tree,recovery=read_verified_schema(obj,SCHEMAS)
                    c.disposition(aid,loc,'recovered','EXACT_CACHE_SCHEMA',recovery)
                if kind=='TextAsset':
                    body=tree['m_Script'];body=body.encode('utf8','surrogateescape') if isinstance(body,str) else bytes(body)
                    loc['name']=tree.get('m_Name','')
                    textasset(c,aid,body,loc['name'],loc,key)
                else:tree_text(c,aid,tree,loc)
            except Exception as error:
                body=obj.get_raw_data();container_hash=sha(loc['container'].encode())[:16]
                dest=OUT/'pc-failed-objects'/digest/container_hash/(str(obj.path_id)+'.bytes');dest.parent.mkdir(parents=True,exist_ok=True)
                if dest.exists() and file_hash(dest)!=sha(body):raise ValueError('FAILED_OBJECT_EVIDENCE_COLLISION')
                if not dest.exists():dest.write_bytes(body)
                c.file(aid,dest,sha(body),len(body),'unresolved_serialized_object')
                c.disposition(aid,loc,'gap','PC_OBJECT_READ_FAILED',{'exception':type(error).__name__,'reason':str(error)[:300],'serialized_sha256':sha(body),'serialized_bytes':len(body)})
                for hint in carve_unity_strings(body,obj.reader.endian):
                    c.add(aid,hint['value'],'unverified_binary_string',{**loc,**{k:v for k,v in hint.items() if k!='value'}})
        c.complete(aid)
        if number%100==0:print(json.dumps({'phase':'pc','assets':number,'total':len(files),'unique_strings':len(c.text_ids),'seconds':round(time.time()-start)}),flush=True)
    result=c.finish();result.update(source=PC_PROFILE,scope=PC_SCOPE,seconds=round(time.time()-start));save(OUT/'pc-summary.json',result);c.db.close();print(json.dumps(result),flush=True)

def main():
    global OUT,RAW,STATE,PC_DATA,MANIFESTS,MASTER,SCHEMAS,PC_SCOPE,DECODE_MASTER,PC_PROFILE
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('phase',choices=['webgl','pc'])
    p.add_argument('--output',type=Path,required=True,help='Private output directory; contains exact source strings')
    p.add_argument('--inventory',type=Path,action='append',default=[],help='Saved asset/master manifests; repeat for both')
    p.add_argument('--state',type=Path,default=ROOT/'source-state/all-assets/WebGL/assets')
    p.add_argument('--raw',type=Path,default=ROOT/'private-source-cache')
    p.add_argument('--package',type=Path,default=ROOT/'translations/ko')
    p.add_argument('--pc-game-data',type=Path,help='Explicit installed PC GC_Data directory')
    p.add_argument('--pc-scope',choices=['all','core','cache'],default='all')
    p.add_argument('--pc-provenance',choices=['installed','historical'],default='installed')
    p.add_argument('--decode-pc-master',action='store_true',help='Reuse configured NG03 private helper for cached Master generations')
    schema=p.add_mutually_exclusive_group()
    schema.add_argument('--schema-registry',type=Path,help='Existing exact cache schema registry; source hashes rechecked')
    schema.add_argument('--harvest-cache-schemas',action='store_true',help='Read matching schemas from the same installed cache')
    args=p.parse_args()
    if args.phase=='webgl' and not args.inventory:p.error('webgl requires explicit saved inventories')
    if args.phase=='pc' and not args.pc_game_data:p.error('pc requires --pc-game-data')
    if args.phase!='pc' and (args.schema_registry or args.harvest_cache_schemas or args.decode_pc_master):p.error('PC recovery options require phase pc')
    OUT=args.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
    RAW=args.raw.resolve();STATE=args.state.resolve();PC_DATA=args.pc_game_data.resolve() if args.pc_game_data else None
    MANIFESTS=args.inventory;MASTER=json.loads((args.package/'master.json').read_bytes())
    PC_SCOPE=args.pc_scope
    PC_PROFILE='PC_HISTORICAL_ARCHIVE' if args.pc_provenance=='historical' else 'PC_INSTALLED_OR_CACHE_OBSERVED'
    decoder_sha=None
    if args.decode_pc_master:
        from scripts.crypto import private_helper, decrypt_master_text
        decoder_sha=private_helper().ng03_helper_sha256;DECODE_MASTER=decrypt_master_text
    config={'schema':'full-text-census-v2','phase':args.phase,'raw':str(RAW),'state':str(STATE),
            'pc_game_data':str(PC_DATA),'pc_scope':PC_SCOPE,
            'pc_profile':PC_PROFILE,
            'master_sha256':file_hash(args.package/'master.json'),
            'inventories':[{ 'path':str(q.resolve()),'sha256':file_hash(q)} for q in MANIFESTS],
            'schema_registry_sha256':file_hash(args.schema_registry) if args.schema_registry else None,
            'harvest_cache_schemas':args.harvest_cache_schemas,
            'private_decoder_sha256':decoder_sha,
            'implementation':{q.name:file_hash(q) for q in [Path(__file__),ROOT/'tools/update/text_census.py',ROOT/'tools/update/unity_schema_text.py']}}
    config_path=OUT/(args.phase+'-configuration.json')
    if config_path.exists() and json.loads(config_path.read_bytes())!=config:raise ValueError('CENSUS_CONFIGURATION_CHANGED_USE_NEW_OUTPUT')
    if not config_path.exists():save(config_path,config)
    if args.schema_registry:SCHEMAS=load_registry(args.schema_registry,PC_DATA)
    elif args.harvest_cache_schemas:
        core=[q for q in PC_DATA.iterdir() if q.is_file() and (q.suffix=='.assets' or q.name=='globalgamemanagers' or re.fullmatch(r'level\d+',q.name))]
        core += [q for q in (PC_DATA/'Resources').glob('*') if q.is_file()]
        SCHEMAS=harvest_registry(PC_DATA,core,OUT/'pc-exact-schemas.json')
    try:(webgl if args.phase=='webgl' else pc)()
    except BaseException:
        (OUT/(args.phase+'-fatal.txt')).write_text(traceback.format_exc(),encoding='utf8');raise

if __name__=='__main__':main()
