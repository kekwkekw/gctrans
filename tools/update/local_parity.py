"""Offline Master/UI review batches. Crawler observations are candidates, never approval."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import subprocess
import unicodedata
from collections import Counter

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from update.master_coverage import read, save, sha, TOKENS, NUMBERS
from update.source_scan import source_item_eligible
from update.process_lock import ProcessFileLock
from reconcile_master import flatten_master
from export_local_ui import export
from build_manifests import build

PUBLISH = ('master.json', 'ui.json', 'ui/general-exact.reviewed.json', 'manifest.json', 'legacy-components.json', 'ui/manifest.json', 'release-manifest.json')
GUARDED = tuple(dict.fromkeys(PUBLISH + ('names.json', 'ui/resources.json', 'ui/bindings.json', 'ui.templates.json', 'ui/local-coverage.json', 'ui/detail-families.json')))


def validate_translation(jp, ko):
    if not isinstance(ko, str) or not ko.strip() or jp == ko:
        raise ValueError('TRANSLATION_REQUIRED')
    if Counter(TOKENS.findall(jp)) != Counter(TOKENS.findall(ko)):
        raise ValueError('PROTECTED_TOKEN_MISMATCH')
    numbers = lambda s: Counter(unicodedata.normalize('NFKC', n) for n in NUMBERS.findall(s))
    if numbers(jp) != numbers(ko):
        raise ValueError('NUMBER_MISMATCH')
    # Layout punctuation/newlines may change in Korean; report instead of vetoing.
    return ['LAYOUT_CHANGED_REVIEW_NATIVE'] if any(jp.count(c) != ko.count(c) for c in ('[', ']', '\n')) else []


def identity(row):
    return (row['kind'], row.get('table', ''), row.get('field', ''), row['jp'])


def row_id(row):
    return hashlib.sha256(json.dumps(identity(row), ensure_ascii=False).encode()).hexdigest()


def make_plan(package, baseline, baseline_ref, scans=(), observations=(), probe_runtime=False):
    master = flatten_master(read(package/'master.json'))
    ui = read(package/'ui.json')
    candidates, sources = {}, []
    ko_by_jp = {}
    for (_, _, jp), ko in master.items():
        if ko and ko != jp:
            ko_by_jp.setdefault(jp, set()).add(ko)
    def add(row, evidence):
        if not isinstance(row.get('jp'), str) or not row['jp'].strip():
            return
        key = identity(row)
        if row['kind'] == 'master' and key[1:] in master or row['kind'] == 'ui' and row['jp'] in ui:
            return
        # Latin/numeric-only UI fields need no Japanese translation. Master keeps all keys.
        if row['kind'] == 'ui' and not re.search(r'[\u3040-\u30ff\u3400-\u9fff]', row['jp']):
            return
        entry = candidates.setdefault(key, dict(row, id=row_id(row), evidence=[]))
        if row['jp'] in ko_by_jp:
            entry['existing_korean_suggestions'] = sorted(ko_by_jp[row['jp']])
        if evidence not in entry['evidence']:
            entry['evidence'].append(evidence)
    for t,f,jp in flatten_master(read(baseline)):
        add({'kind':'master','table':t,'field':f,'jp':jp}, {'source':'local_coverage','ref':baseline_ref})
    ui_baseline = read(package/'ui/local-coverage.json')
    for jp in ui_baseline['keys']:
        if jp not in ui_baseline['keep_source']:
            add({'kind':'ui','jp':jp}, {'source':'local_ui_coverage','ref':ui_baseline.get('upstream', baseline_ref)})
    for path in scans:
        document = read(path); snap = document.get('sourceSnapshot', document)
        profile = snap.get('profile', {})
        coverage = snap.get('coverage', {})
        eligible = {r['asset_key'] for r in snap.get('asset_materializations', []) if source_item_eligible(r)}
        available = snap.get('metadata', {}).get('status') != 'UNKNOWN' and bool(eligible)
        sources.append({'sha256':sha(path), 'snapshot_id':snap.get('snapshot_id'), 'profile':profile,
                        'coverage':coverage, 'status':'USABLE_ROWS' if available else 'NO_USABLE_ROWS',
                        'excluded_assets': [{'asset_key':r['asset_key'],'status':r['status'],'reason':r.get('error_code')}
                                            for r in snap.get('asset_materializations',[]) if r['asset_key'] not in eligible],
                        'ui_diagnostics': snap.get('ui_diagnostics', []),
                        'master_field_coverage':snap.get('master_field_coverage',[])})
        if not available:
            continue
        for row in snap.get('master', []):
            if row.get('translation_target'):
                add(dict(kind='master', jp=row['jp'], **row['translation_target']),
                    {'source':'crawler','profile':profile,'locator':row['locator'],'snapshot_id':snap.get('snapshot_id')})
        for row in snap.get('ui', []):
            if row.get('locator',{}).get('asset_key') not in eligible:
                continue
            add({'kind':'ui','jp':row['jp']}, {'source':'crawler','profile':profile,'locator':row['locator'],'snapshot_id':snap.get('snapshot_id')})
            entry = candidates.get(('ui', '', '', row['jp']))
            if entry is not None and row.get('context'):
                entry.setdefault('contexts', [])
                if row['context'] not in entry['contexts']: entry['contexts'].append(row['context'])
    for path in observations:
        data=read(path)
        if data.get('schema') != 'gc-native-ui-observations-v1':
            raise ValueError('NATIVE_OBSERVATION_SCHEMA')
        for row in data['rows']:
            if not row.get('evidence'):
                raise ValueError('NATIVE_OBSERVATION_EVIDENCE_REQUIRED')
            add({'kind':'ui','jp':row['jp']}, {'source':'native_observation','evidence':row['evidence']})
    rows=[candidates[k] for k in sorted(candidates)]
    runtime_covered=[]
    if probe_runtime and any(r['kind']=='ui' for r in rows):
        # Use the very same C# lookup implementation, not a second Python parser.
        with tempfile.TemporaryDirectory(prefix='ui-lookup-') as temp:
            source=Path(temp)/'input.json';result=Path(temp)/'output.json'
            save(source,[r['jp'] for r in rows if r['kind']=='ui'])
            project=Path(__file__).resolve().parents[2]/'tests/UiTextLookup'
            subprocess.run(['dotnet','run','--project',str(project),'--',str((package/'ui.json').resolve()),
                            '--probe',str(source),str(result)],check=True, stdout=sys.stderr)
            probe=read(result)
            if probe['ui_sha256'] != sha(package/'ui.json'):raise ValueError('UI_CHANGED_DURING_PROBE')
            runtime_covered=[r for r in probe['rows'] if r['matched']]
        covered={r['jp'] for r in runtime_covered}
        rows=[r for r in rows if r['kind']!='ui' or r['jp'] not in covered]
    return {'schema':'gc-local-parity-review-v1','baseline_ref':baseline_ref,'baseline_sha256':sha(baseline),
        'preimages':{p:sha(package/p) for p in GUARDED},'sources':sources,'rows':rows,
        'missing_master':sum(r['kind']=='master' for r in rows),'missing_ui':sum(r['kind']=='ui' for r in rows),
        'existing_master_source_equal':sum(jp==ko for (_,_,jp),ko in master.items()),
        'runtime_covered':runtime_covered,
        'automatic_deletes':0,'semantic_review':'REQUIRED',
        'note':'Scope is supplied source/field selectors. Key coverage is not proof of whole-game translation.'}


def review_template(batch):
    return {'schema':'gc-local-parity-decisions-v1', 'rows':[
        {'id':r['id'], 'jp':r['jp'], 'action':'pending', 'ko':None, 'reviewer':'', 'evidence':'',
         'suggestions':r.get('existing_korean_suggestions', [])} for r in batch['rows']]}


def apply_review(package, batch, decisions):
    with ProcessFileLock(package.parent/'.local-parity-apply.lock', 'PACKAGE_WRITER_BUSY'):
        return _apply_review(package, batch, decisions)


def _apply_review(package, batch, decisions):
    if batch.get('schema') != 'gc-local-parity-review-v1':
        raise ValueError('REVIEW_BATCH_SCHEMA')
    if set(batch.get('preimages', {})) != set(GUARDED):
        raise ValueError('PACKAGE_PREIMAGES_REQUIRED')
    if any(sha(package/p)!=h for p,h in batch['preimages'].items()):
        raise ValueError('PACKAGE_CHANGED_SINCE_PLAN')
    wanted={r['id']:r for r in batch['rows']}
    if len(wanted)!=len(batch['rows']) or any(row_id(r)!=r['id'] for r in wanted.values()):
        raise ValueError('BATCH_IDENTITY')
    if decisions.get('schema') != 'gc-local-parity-decisions-v1': raise ValueError('DECISIONS_SCHEMA')
    if len({r['id'] for r in decisions['rows']}) != len(decisions['rows']): raise ValueError('DUPLICATE_DECISION')
    decisions={r['id']:r for r in decisions['rows']}
    if not decisions.keys() <= wanted.keys(): raise ValueError('UNKNOWN_DECISION_ID')
    additions,held,pending,warnings=[],[],[],[]
    for key,row in wanted.items():
        if row['kind'] not in ('master','ui') or (row['kind']=='master' and (not row.get('table') or not row.get('field'))):
            raise ValueError('BATCH_TARGET')
        decision=decisions.get(key, {'action':'pending'})
        if decision.get('action') == 'pending':
            pending.append(row)
            continue
        if decision.get('action')=='hold':
            if not decision.get('reason') or not decision.get('evidence'):
                raise ValueError('HOLD_REASON_AND_EVIDENCE_REQUIRED')
            held.append(dict(row, reason=decision['reason'], review_evidence=decision['evidence']))
            continue
        if decision.get('action')!='translate' or not decision.get('reviewer') or not decision.get('evidence'):
            raise ValueError('EXPLICIT_SEMANTIC_REVIEW_REQUIRED')
        for warning in validate_translation(row['jp'], decision.get('ko')):
            warnings.append({'id':key,'warning':warning})
        additions.append(dict(row,ko=decision['ko']))
    # Validate/export/build in a disposable copy before touching the real package.
    with tempfile.TemporaryDirectory(prefix='local-parity-apply-',dir=package.parent) as temp:
        stage=Path(temp)/'ko';shutil.copytree(package,stage)
        master=read(stage/'master.json'); reviewed=read(stage/'ui/general-exact.reviewed.json')
        for row in additions:
            if row['kind']=='master':
                field = master.setdefault(row['table'],{}).setdefault(row['field'],{})
                if row['jp'] in field: raise ValueError('EXISTING_TRANSLATION_OVERWRITE')
                field[row['jp']]=row['ko']
            else:
                if row['jp'] in read(stage/'ui.json') or row['jp'] in reviewed: raise ValueError('EXISTING_TRANSLATION_OVERWRITE')
                reviewed[row['jp']]=row['ko']
        changed=[]
        if any(r['kind']=='master' for r in additions):save(stage/'master.json',master);changed.append('master.json')
        if any(r['kind']=='ui' for r in additions):save(stage/'ui/general-exact.reviewed.json',dict(sorted(reviewed.items())))
        payload,_=export(stage)
        if payload!=(stage/'ui.json').read_bytes():(stage/'ui.json').write_bytes(payload);changed.append('ui.json')
        if changed:build(stage,modified=changed)
        if any(sha(package/p)!=h for p,h in batch['preimages'].items()):raise ValueError('PACKAGE_CHANGED_DURING_REVIEW')
        backups={p:(package/p).read_bytes() for p in PUBLISH}
        try:
            for p in PUBLISH:
                if (stage/p).read_bytes()!=backups[p]:shutil.copyfile(stage/p,package/p)
        except OSError:
            for p,raw in backups.items():(package/p).write_bytes(raw)
            raise
    return {'status':'REVIEW_APPLIED','added_master':sum(r['kind']=='master' for r in additions),
        'added_ui':sum(r['kind']=='ui' for r in additions),'holds':held,'pending':pending,'warnings':warnings,'removed':0,
        'master_sha256':sha(package/'master.json'),'ui_sha256':sha(package/'ui.json'),
        'native':'NOT_RUN_BY_THIS_TOOL'}


def main():
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
    q=sub.add_parser('plan');q.add_argument('--baseline',type=Path,required=True);q.add_argument('--baseline-ref',required=True)
    q.add_argument('--scan',type=Path,action='append',default=[]);q.add_argument('--observations',type=Path,action='append',default=[])
    q.add_argument('--probe-runtime',action='store_true',help='Use actual C# lookup to exclude already supported dynamic labels')
    q=sub.add_parser('apply');q.add_argument('--batch',type=Path,required=True);q.add_argument('--review',type=Path,required=True)
    q=sub.add_parser('review-template');q.add_argument('--batch',type=Path,required=True)
    for q in sub.choices.values():
        q.add_argument('--package',type=Path,default=Path(__file__).resolve().parents[2]/'translations/ko');q.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    try:
        result=make_plan(a.package,a.baseline,a.baseline_ref,a.scan,a.observations,a.probe_runtime) if a.command=='plan' else review_template(read(a.batch)) if a.command=='review-template' else apply_review(a.package,read(a.batch),read(a.review))
        save(a.output,result);print(json.dumps({k:v for k,v in result.items() if k in ('status','missing_master','missing_ui','added_master','added_ui','removed')},ensure_ascii=False))
    except (ValueError,OSError,KeyError,subprocess.CalledProcessError) as e:
        print(json.dumps({'status':'FAILED','reason':str(e)},ensure_ascii=False));return 2
    return 0

if __name__=='__main__':raise SystemExit(main())
