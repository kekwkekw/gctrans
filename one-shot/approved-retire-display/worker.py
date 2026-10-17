"""One-shot exact reviewed repository application. No translator, game, installer or main push.
Only the known approved file-selection is replayed; all final Git trees are fixed externally.
"""
from pathlib import Path
import base64, hashlib, json, os, shutil, subprocess, sys, tempfile

def sha(b):return hashlib.sha256(b).hexdigest()
def write(root,rel,data):
    p=root/rel
    if not rel or rel.startswith('/') or '..' in rel.split('/') or '\\' in rel or rel.split('/')[0]=='.git':raise ValueError('UNSAFE_PATH')
    if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('SYMLINK_PATH')
    p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
def apply_files(root,phase):
    for rel in phase['delete']:(root/rel).unlink()
    patch=phase['patch'].encode('utf-8')
    subprocess.run(['git','apply','--check','--whitespace=nowarn','-'],input=patch,cwd=root,check=True)
    subprocess.run(['git','apply','--whitespace=nowarn','-'],input=patch,cwd=root,check=True)
def parse_pairs(raw):
    s=raw.decode('utf-8');dec=json.JSONDecoder();i=1 if s.startswith('\ufeff') else 0
    def ws(n):
        while n<len(s) and s[n] in ' \r\n\t':n+=1
        return n
    i=ws(i)
    if s[i]!='{':raise ValueError('OBJECT_REQUIRED')
    i=ws(i+1);rows=[];seen=set()
    if s[i]=='}':return s,rows
    while True:
        k,i=dec.raw_decode(s,i)
        if not isinstance(k,str) or k in seen:raise ValueError('INVALID_SOURCE_KEY')
        seen.add(k);i=ws(i)
        if s[i]!=':':raise ValueError('COLON_REQUIRED')
        start=ws(i+1);v,end=dec.raw_decode(s,start)
        if not isinstance(v,str):raise ValueError('STRING_VALUE_REQUIRED')
        rows.append((k,v,start,end));i=ws(end)
        if s[i]=='}':
            if ws(i+1)!=len(s):raise ValueError('TRAILING_DATA')
            return s,rows
        if s[i]!=',':raise ValueError('COMMA_REQUIRED')
        i=ws(i+1)
def apply_scenario(root,payload):
    count=kept=total=delta=0;modified=[]
    for ident,n,encoded in payload['decision_records']:
        if not ident.isascii() or not ident.isdigit():raise ValueError('ID_REQUIRED')
        rel='translations/ko/novels/'+ident+'.json';p=root/rel;raw=p.read_bytes();s,rows=parse_pairs(raw)
        if len(rows)!=n:raise ValueError('ROW_COUNT')
        bits=base64.b64decode(encoded,validate=True)
        if len(bits)!=(n+7)//8 or (n%8 and bits[-1]>>(n%8)):raise ValueError('BITSET_BOUND')
        edits=[];chosen=set()
        for i,(jp,ko,start,end) in enumerate(rows):
            if not (bits[i//8]>>(i%8))&1:continue
            if ko.startswith('「') and ko.endswith('」'):raise ValueError('ALREADY_WRAPPED')
            if ko.startswith('[') and ko.endswith(']') and '보류' in ko:raise ValueError('HOLD_PROTECTED')
            token=s[start:end]
            if token[0]!='"' or token[-1]!='"':raise ValueError('VALUE_TOKEN')
            edits.append((start,end,'"「'+token[1:-1]+'」"'));chosen.add(i)
        for start,end,new in reversed(edits):s=s[:start]+new+s[end:]
        out=s.encode('utf8');_,again=parse_pairs(out)
        if len(again)!=len(rows):raise ValueError('COVERAGE')
        for i,(a,b) in enumerate(zip(rows,again)):
            if a[0]!=b[0] or b[1]!=('「'+a[1]+'」' if i in chosen else a[1]):raise ValueError('UNEXPECTED_CHANGE')
        if len(out)-len(raw)!=len(edits)*6:raise ValueError('BYTE_DELTA')
        write(root,rel,out);count+=len(edits);total+=n;kept+=n-len(edits);delta+=len(out)-len(raw);modified.append('novels/'+ident+'.json')
    if (len(modified),total,count,kept,delta)!=(338,22760,18366,4394,110196):raise ValueError('EXACT_PLAN_TOTALS')
    sys.path.insert(0,str(root/'tools'))
    from build_manifests import build
    build(root/'translations/ko',modified=modified)
    apply_files(root,payload['scenario'])
    from verify_staged_release import verify
    result=verify(root/'translations/ko')
    if sha((root/'translations/ko/ui/manifest.json').read_bytes())!=payload['content_id']:raise ValueError('CONTENT_ID')
    return result

def run_git(root,*args):return subprocess.check_output(['git','-C',str(root),*args]).decode().strip()
def publish(root,payload):
    if run_git(root,'remote','get-url','origin') not in ('https://github.com/kekwkekw/gctrans','https://github.com/kekwkekw/gctrans.git'):
        raise ValueError('REPOSITORY_SCOPE')
    if os.environ.get('GITHUB_REF')!='refs/heads/'+payload['helper_branch']:raise ValueError('HELPER_BRANCH_SCOPE')
    run_git(root,'checkout','--detach',payload['base'])
    if run_git(root,'rev-parse','HEAD^{tree}')!=payload['base_tree']:raise ValueError('BASE_TREE')
    run_git(root,'config','user.name','GitHub Actions')
    run_git(root,'config','user.email','41898282+github-actions[bot]@users.noreply.github.com')
    run_git(root,'config','core.autocrlf','false')
    apply_files(root,payload['retirement'])
    for pattern in ('retirement_scope_tests.py','ng11_*tests.py'):
        subprocess.run([sys.executable,'-B','-m','unittest','discover','-s','tests','-p',pattern],cwd=root,check=True)
    first_paths=payload['retirement']['delete']+payload['retirement']['write_paths']
    run_git(root,'add','--',*first_paths)
    if run_git(root,'write-tree')!=payload['retirement_tree']:raise ValueError('RETIREMENT_TREE_MISMATCH')
    run_git(root,'commit','-m','Retire unused legacy translation pipelines; preserve current UI/source/CDN')
    retirement_commit=run_git(root,'rev-parse','HEAD')
    result=apply_scenario(root,payload)
    changed=['translations/ko/novels/'+x[0]+'.json' for x in payload['decision_records']]
    changed += ['translations/ko/manifest.json','translations/ko/legacy-components.json','translations/ko/ui/manifest.json','translations/ko/release-manifest.json']
    changed += payload['scenario']['write_paths']
    run_git(root,'add','--',*changed)
    if run_git(root,'write-tree')!=payload['final_tree']:raise ValueError('FINAL_TREE_MISMATCH')
    # Exact source is unchanged. Where already installed, use the pure .NET loader
    # with a fresh isolated store, not a game runtime or the user's Stable store.
    dotnet={'status':'NOT_RUN_SDK9_UNAVAILABLE'}
    tool=shutil.which('dotnet')
    if tool:
        sdks=subprocess.check_output([tool,'--list-sdks'],text=True)
        if any(x.startswith('9.') for x in sdks.splitlines()):
            env=os.environ.copy();env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1')
            subprocess.run([tool,'build','tools/UpdateCli/UpdateCli.csproj','-c','Release','-v','quiet'],cwd=root,env=env,check=True)
            store=Path(os.environ['RUNNER_TEMP'])/'gc-reviewed-display-store'
            if store.exists():raise ValueError('NEW_STORE_REQUIRED')
            output=subprocess.check_output([tool,'tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll','prepare-local','--store',str(store),'--package',str(root/'translations/ko'),'--based-on','NONE'],cwd=root,env=env,text=True)
            d=json.loads(output)
            if d['reason']!='LOCAL_COMPLETE_PENDING_SAVED' or d['candidate']!=payload['content_id'] or d['current_changed'] or d['previous_changed'] or (store/'current.json').exists() or (store/'previous.json').exists():raise ValueError('LOADER_PREPARATION_CONTRACT')
            dotnet={'status':'PASS_SDK9_BUILD_AND_COMPLETE_PREPARE_LOCAL','candidate':d['candidate'],'current_and_previous_absent':True,'native_initialize':'NOT_RUN'}
    run_git(root,'commit','-m','Apply existing PRO04 display-only outer quotes and rebuild current manifests')
    final_commit=run_git(root,'rev-parse','HEAD')
    if run_git(root,'rev-parse','HEAD^{tree}')!=payload['final_tree'] or run_git(root,'diff','--name-only') or run_git(root,'diff','--cached','--name-only'):raise ValueError('DIRTY_OR_WRONG_FINAL_COMMIT')
    remote=run_git(root,'ls-remote','origin','refs/heads/main').split()[0]
    if remote!=payload['base']:raise ValueError('REMOTE_MAIN_MOVED_NO_PUBLICATION')
    existing=run_git(root,'ls-remote','origin','refs/heads/'+payload['target_branch'])
    if existing:raise ValueError('TARGET_BRANCH_EXISTS_NO_OVERWRITE')
    run_git(root,'push','origin','HEAD:refs/heads/'+payload['target_branch'])
    record={'status':'VERIFIED_REVIEW_BRANCH_PUSHED_NOT_MERGED','base':payload['base'],'retirement_commit':retirement_commit,'retirement_tree':payload['retirement_tree'],'candidate_commit':final_commit,'candidate_tree':payload['final_tree'],'target_branch':payload['target_branch'],'package':result,'dotnet':dotnet,'main_write':False,'stable_install':False,'new_semantic_acceptance_receipt':False}
    print('RESULT_JSON='+json.dumps(record,separators=(',',':')))
    with open(os.environ['GITHUB_STEP_SUMMARY'],'a',encoding='utf8') as f:f.write('```json\n'+json.dumps(record,indent=2)+'\n```\n')
