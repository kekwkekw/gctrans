"""Offline discarded package_target fixture; no edits to source package or live store."""
from pathlib import Path
import argparse,subprocess,sys,json

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--implementation',required=True);p.add_argument('--scratch',required=True)
p.add_argument('--discarded-fixture-approved',action='store_true',required=True)
a=p.parse_args();impl=Path(a.implementation).resolve();scratch=Path(a.scratch).resolve()
assert not scratch.exists() and not scratch.is_relative_to(impl),'NEW_EXTERNAL_SCRATCH_REQUIRED'
scratch.mkdir(parents=True);cli=impl/'tools/update/cli.py';initial=impl/'translations/ko'
assert cli.is_file() and initial.is_dir()
def invoke(name,*args):
    r=subprocess.run([sys.executable,'-I','-S','-X','utf8',str(cli),*map(str,args)],cwd=impl,capture_output=True,text=True,encoding='utf-8')
    (scratch/(name+'.log')).write_text(r.stdout+'\n'+r.stderr,'utf-8');assert r.returncode==0,(name,r.returncode)
def edit(package,ko,name):
    draft=scratch/(name+'-draft.json');review=scratch/(name+'-reviewed.json');report=scratch/(name+'-apply.json')
    invoke(name+'-plan','plan-ui-edit','--package',package,'--resource-id','ui.core.UI4-S028.a26e468b7032574f','--new-ko',ko,'--draft',draft,'--report',scratch/(name+'-plan.json'))
    obj=json.loads(draft.read_text('utf-8'));assert len(obj['rows'])==1 and obj['rows'][0]['decision']=='HOLD'
    obj['rows'][0]['decision']='APPROVE';review.write_text(json.dumps(obj,ensure_ascii=False,indent=2),'utf-8')
    invoke(name+'-apply','apply-review','--package',package,'--review',review,'--state-root',scratch/'authoring','--report',report)
    result=json.loads(report.read_text('utf-8'));assert result['status']=='PASS' and result['consumed_count']==1
    assert result['apply_metrics']['unchanged_novel_payload_reads']==result['apply_metrics']['unchanged_novel_payload_hashes']==0
    return result['next_package']
changed=edit(initial,'닫기닫기','trial');restored=edit(changed,'닫기','restore')
obj={'initial_root':str(initial),'changed':changed,'restored':restored,'resource_id':'ui.core.UI4-S028.a26e468b7032574f','trial_only':True,'canonical_semantic_edits':0,'authority':'Explicit discarded CPU fixture flag; no game/store authorization inferred'}
(scratch/'FIXTURE.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2),'utf-8');print(json.dumps(obj,ensure_ascii=False,indent=2))
