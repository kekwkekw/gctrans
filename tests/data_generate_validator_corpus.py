"""Author the self-contained declarative corpus consumed by Python and C#."""
from pathlib import Path
import copy, json

def generate():
    r={'schema_version':1,'locale':'ko','resources':[
       {'id':'case.resource.'+str(i),'role':'menu.caption','kind':'literal','jp':'一括ダウンロード','ko':'일괄 다운로드','disposition':'translate'} for i in range(2)]}
    b={'schema_version':1,'bindings':[
       {'id':'case.binding.'+str(i),'resource_id':'case.resource.'+str(i),'adapter_id':'core.static-caption.v1','enabled':True,
        'action_group':'case.comatch','source_identity':{'kind':'static-asset','identity':'case.asset.'+str(i),'exact_jp':'一括ダウンロード'},
        'selector':{'scope_key':'main-menu','slot_key':'download-label','type':'static','segments':['Group','Text (TMP)']}} for i in range(2)]}
    cases=[]
    def case(id,accept,mutate=lambda r,b:None,capability='available',note=''):
        rr,bb=copy.deepcopy(r),copy.deepcopy(b);mutate(rr,bb)
        cases.append({'id':id,'resources_document':rr,'bindings_document':bb,
                      'expected':{'accept':accept,'capability':capability if accept else 'package_rejected'},'note':note})
    case('comatch_equal',True)
    case('comatch_explicit_default_markup',True,lambda r,b:r['resources'][1].update(markup_policy='exact-tags'))
    case('comatch_role_difference',False,lambda r,b:r['resources'][1].update(role='other.caption'))
    def kind_change(r,b):
        r['resources'][1].update(kind='master_ref',master_ref={'table':'mTextConfig','field':'ml_value[]','exact_jp':r['resources'][1]['jp']});r['resources'][1].pop('ko')
    case('comatch_kind_difference',False,kind_change)
    def disposition_change(r,b):
        for x in b['bindings']:x['enabled']=False
        r['resources'][1]['disposition']='hold'
    case('comatch_disposition_difference',False,disposition_change)
    def master_refs(r,b,change=False):
        for x in r['resources']:
            x.update(kind='master_ref',master_ref={'table':'mTextConfig','field':'ml_value[]','exact_jp':x['jp']});x.pop('ko')
        if change:r['resources'][1]['master_ref']['table']='mOtherText'
    case('comatch_equal_master_refs',True,master_refs)
    case('comatch_master_ref_difference',False,lambda r,b:master_refs(r,b,True))
    case('comatch_markup_policy_difference',False,lambda r,b:r['resources'][1].update(markup_policy='omit-cspace-v1'),note='Unsupported markup adapter also rejects this structural mismatch.')
    case('comatch_selector_difference',False,lambda r,b:b['bindings'][1]['selector'].update(slot_key='new-label'))
    case('comatch_enabled_difference',False,lambda r,b:b['bindings'][1].update(enabled=False))
    case('comatch_duplicate_identity',False,lambda r,b:b['bindings'][1]['source_identity'].update(identity='case.asset.0'))
    case('comatch_adapter_difference',False,lambda r,b:b['bindings'][1].update(adapter_id='ui4.static-caption.v1'))
    case('comatch_ko_difference',False,lambda r,b:r['resources'][1].update(ko='모두 다운로드'))
    case('unsafe_selector_segment',False,lambda r,b:b['bindings'][1]['selector'].update(segments=['..']))
    case('dangling_resource',False,lambda r,b:b['bindings'][1].update(resource_id='missing.resource'))
    case('source_disagreement',False,lambda r,b:b['bindings'][1]['source_identity'].update(exact_jp='別の原文'))
    def optional(r,b,adapter='future.optional.v99',enabled=True):
        r['resources']=r['resources'][:1];b['bindings']=b['bindings'][:1];b['bindings'][0].pop('action_group')
        b['bindings'][0].update(adapter_id=adapter,enabled=enabled)
        b['bindings'][0]['selector']['slot_key']='unregistered-optional-slot'
    case('unknown_optional_enabled',True,optional,'optional_unavailable')
    case('unknown_optional_disabled',True,lambda r,b:optional(r,b,enabled=False),'optional_unavailable')
    case('known_optional_unsupported_selector',True,lambda r,b:optional(r,b,'core.static-caption.v1'),'optional_unavailable')
    def master_markup(r,b):master_refs(r,b);r['resources'][1]['markup_policy']='omit-cspace-v1'
    case('master_ref_unsupported_markup',False,master_markup)
    return {'schema':'gc-ng02-shared-validator-cases-v1','classification_contract':'Structural accept/reject is shared; capability is separate from package validity.',
            'host_registry':{'adapter_id':'core.static-caption.v1','scope_keys':['main-menu'],'slot_keys':['download-label','new-label'],'supports_comatch':True},
            'cases':cases}

if __name__=='__main__':
    root=Path(__file__).resolve().parents[1]
    (root/'tests/shared-validator-cases.json').write_text(json.dumps(generate(),ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
