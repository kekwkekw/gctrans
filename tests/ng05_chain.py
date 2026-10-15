"""Full disposable candidate: package-target KO drafts -> owned generations -> supported binding/typed fixed layout."""
from ng04_apply_tests import *
from update.operator_review import plan_ui_edit
def generate_chain(destination,report_path,*,export=True):
    if destination.exists(): discard(destination)
    destination.mkdir(parents=True); external=destination/'external-baseline'
    shutil.copytree(ROOT/'translations/ko',external,copy_function=shutil.copyfile)
    policy=read_json(external/'layout/episode-title.json')
    # Disposable formatting-only layout correction: all const policy values stay.
    (external/'layout/episode-title.json').write_bytes(json.dumps(policy,ensure_ascii=False,separators=(',',':')).encode())
    fixture_bindings=read_json(external/'ui/bindings.json')
    original_binding=next(b for b in fixture_bindings['bindings'] if b['enabled'] and b['adapter_id']=='battle.caption.v1')
    binding_template=copy.deepcopy(original_binding)
    # Keep the compiled route/SourceIdentity intact. Only the disposable baseline
    # leaves this existing route unbound so the reviewed addition is executable.
    routes=[re.findall(r'"([^"]*)"',line) for line in (ROOT/'source/GCMod.Candidate/Native/BattleRoutes.cs').read_text(encoding='utf-8-sig').splitlines() if line.startswith('new BattleRoute(')]
    route_values=[r for r in routes if r[0]==binding_template['source_identity']['identity']]
    assert len(route_values)==1,'Binding fixture requires exactly one existing compiled BattleRoute'
    route_values=route_values[0]
    binding_route=dict(zip(('source_id','slot','owner','selector','callback','surface'),route_values[:6])); binding_route['segments']=route_values[6:]
    selector=binding_template['selector']
    assert selector['scope_key']=='battle-'+binding_route['surface'].lower() and selector['slot_key']==binding_route['slot'] and selector['segments']==binding_route['segments'] and selector['lifetime']=='owner'
    def matches_route(binding):
        return binding['enabled'] and binding['adapter_id']=='battle.caption.v1' and binding['source_identity']['identity']==binding_route['source_id'] and binding['selector']['scope_key']==selector['scope_key'] and binding['selector']['slot_key']==selector['slot_key'] and binding['selector'].get('segments',[])==selector['segments']
    assert sum(matches_route(b) for b in fixture_bindings['bindings'])==1,'Existing route fixture must not duplicate an enabled native binding'
    fixture_bindings['bindings'].remove(original_binding)
    (external/'ui/bindings.json').write_bytes(encode(fixture_bindings))
    build(external,modified=['layout/episode-title.json','ui/bindings.json'])
    raw_novels={p.read_bytes() for p in (external/'novels').glob('*.json')}
    state=destination/'state'
    with ActualIO(raw_novels) as cold_io: owned,cold=freeze_input(external,state)
    resource,row=ui_inputs(owned); first=resource['ko']+' 교정1'; second=resource['ko']+' 교정2'
    stages=[]; current=owned
    def stage(id,review,current_source,ko):
        nonlocal current
        with ActualIO(raw_novels) as io:
            result=apply_review(current,review,current_source,destination/('export-'+id) if export else None,state)
        assert result['status']=='PASS',result
        assert io.report()['novel_read_calls']==0 and io.report()['novel_hash_calls']==0,io.report()
        current=Path(result['next_package']); actual=next(r for r in read_json(current/'ui/resources.json')['resources'] if r['id']==resource['id'])
        assert actual['ko']==ko
        stages.append({'id':id,'root':str(current),'export_root':result['output'],'resource_id':resource['id'],'expected_ko':ko,'import_metrics':result['import_metrics'],'apply_metrics':result['apply_metrics'],'actual_io':io.report()})
    review=plan_ui_edit(current,resource['id'],first)['review'];review['rows'][0]['decision']='APPROVE'
    stage('correction1',review,None,first)
    review=plan_ui_edit(current,resource['id'],second)['review'];review['rows'][0]['decision']='APPROVE'
    stage('correction2',review,None,second)
    proposal=copy.deepcopy(binding_template); proposal['id']='binding.cpu-ng05-chain'
    bound_resource=next(r for r in read_json(current/'ui/resources.json')['resources'] if r['id']==proposal['resource_id'])
    binding_source={'locator':{'resource':bound_resource['id'],'binding':proposal['id'],'owner':proposal['selector']['scope_key'],'slot':proposal['selector']['slot_key'],'role':bound_resource['role']},'jp':bound_resource['jp'],
                    'context':{'scope':'CPU fixture only'},'structure':{'adapter_id':proposal['adapter_id'],'selector':proposal['selector'],'source_identity':proposal['source_identity']}}
    review={'schema':'gc-ng03-reviewed-change-set-v1','binding_rows':[{'review_id':'BINDING','decision':'APPROVE','lane':'ui','locator':binding_source['locator'],'source_fingerprint':source_fingerprint(PROFILE,'ui',binding_source),'proposal':proposal,'old_binding_sha256':fingerprint(None)}]}
    stage('binding',review,source(binding_source),second)
    stage('layout',layout_review(current),None,second)
    assert next(b for b in read_json(current/'ui/bindings.json')['bindings'] if b['id']==proposal['id'])==proposal
    assert sum(matches_route(b) for b in read_json(current/'ui/bindings.json')['bindings'])==1
    assert read_json(current/'layout/episode-title.json')==policy
    report={'schema':'gc-ng05-chain-fixtures-v1','fixture_only':True,'initial_root':str(owned),'stages':stages,'final_root':str(current),
            'resource_id':resource['id'],'final_ko':second,'binding_id':proposal['id'],'binding_route':binding_route,'fixture_removed_binding_id':binding_template['id'],'binding_source_identity':proposal['source_identity'],'layout_file':'layout/episode-title.json','layout_policy_id':'episode-title.fit.v1','layout':policy,'layout_sha256':fingerprint(policy),
            'cold_import_metrics':cold,'cold_actual_io':cold_io.report(),'unchanged_novel_count':len(raw_novels),'layout_change':'format canonicalization only; accepted 35/34/33/32/31 const values unchanged',
            'native':'NOT_RUN','game':'NOT_RUN'}
    report_path.write_bytes(encode(report)); return report

if __name__=='__main__':
    generate_chain(ROOT/'tests/fixtures-ng05-chain',ROOT/'tests/NG05_CHAIN_FIXTURES.json')
