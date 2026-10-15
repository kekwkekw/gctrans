"""Disposable NG05 command-boundary reproductions; no semantic approval or game APIs."""
import pathlib,sys,tempfile,subprocess,json,copy,hashlib
ROOT=pathlib.Path(sys.argv[1]).resolve();OUT=pathlib.Path(sys.argv[2]).resolve();OUT.mkdir(parents=True,exist_ok=True)
sys.path[:0]=[str(ROOT/'tools'),str(ROOT/'tests')]
from ng04_apply_tests import small_package,ui_inputs,source,correction
from update.apply_review import freeze_input,discard
from update.operator_review import plan_ui_edit
from build_manifests import encode
from contract_validation import read_json
results={}
with tempfile.TemporaryDirectory(prefix='ng05-boundary-',dir=OUT) as td:
 t=pathlib.Path(td)
 def command(args):
  r=subprocess.run([sys.executable,'-X','utf8',str(ROOT/'tools/update/cli.py'),*args],cwd=t,capture_output=True,text=True,timeout=25)
  try:body=json.loads(r.stdout)
  except ValueError:body=None
  return r,body
 r,body=command(['scan','--platform','PC','--state-root',str(t/'source'),'--raw-root',str(t/'raw'),'--output',str(t/'scan.json')])
 results['G01_scan_without_PC_locator']={'exit':r.returncode,'json':body,'report_exists':(t/'scan.json').exists(),'stderr_tail':r.stderr[-1800:]}
 for decision in ('HOLD','APPROVE'):
  area=t/decision;area.mkdir();pkg=small_package(area/'package');state=area/'state';owned,_=freeze_input(pkg,state)
  resource,row=ui_inputs(owned);review=plan_ui_edit(owned,resource['id'],'CPU fixture correction')['review'];review['rows'][0]['decision']='APPROVE'
  if decision=='HOLD':review['master_key_rows']=[{'review_id':'leave-observed-work-pending','decision':'HOLD'}]
  else:
   # A syntactically valid but independent observed-source operation with no current source.
   other=next(v for v in read_json(owned/'ui/resources.json')['resources'] if v['id']!=resource['id'] and v['kind']=='literal' and v['disposition']=='translate' and v.get('ko'))
   otherrow=copy.deepcopy(row);otherrow['locator']['resource']=other['id'];otherrow['locator']['role']=other['role'];otherrow['jp']=other['jp'];otherrow['structure']['kind']=other['kind']
   op=correction(other,otherrow,other['ko'],'CPU requires observed source')['rows'][0];op['review_id']='observed-op';review['rows'].append(op)
  (area/'review.json').write_bytes(encode(review));r,body=command(['apply-review','--package',str(owned),'--review',str(area/'review.json'),'--state-root',str(state),'--report',str(area/'report.json')])
  nextpkg=body.get('next_package') if isinstance(body,dict) else None
  actual=None
  if nextpkg:actual=next(v['ko'] for v in read_json(pathlib.Path(nextpkg)/'ui/resources.json')['resources'] if v['id']==resource['id'])
  observed_preserved = None
  if decision=='APPROVE' and nextpkg:
   observed_preserved=next(v['ko'] for v in read_json(pathlib.Path(nextpkg)/'ui/resources.json')['resources'] if v['id']==other['id'])==other['ko']
  results['G02_mixed_ui_with_'+decision]={'observed_target_ko_preserved':observed_preserved,'exit':r.returncode,'status':body.get('status') if body else None,'code':body.get('code') if body else None,'next_package_created':bool(nextpkg),'ui_ko':actual,'outcomes':body.get('request_outcomes') if body else None,'stderr_tail':r.stderr[-1200:]}
 # preflight without required inputs is layout-only, not a native readiness check
 r,body=command(['preflight','--candidate-root',str(ROOT),'--report',str(t/'preflight.json')])
 results['G03_preflight_without_external_dependencies']={'exit':r.returncode,'body':body}
(OUT/'BOUNDARY_REPRODUCTIONS.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:{q:v for q,v in x.items() if q not in ('body','json','outcomes','stderr_tail')} for k,x in results.items()},ensure_ascii=False,indent=2))
