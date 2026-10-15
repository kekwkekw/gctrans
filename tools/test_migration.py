"""Offline metamorphic and negative tests; never loads game code."""
from __future__ import annotations
import copy,json,hashlib,tempfile,shutil
from pathlib import Path
from contract_validation import ContractError,read_json,validate_shape,check_models,validate_package
from export_ng01 import Exporter,encoded,write,sha,csharp_literals
from semantic_diff import diff
from build_manifests import build,safe_path

def run(out:Path):
    root=out/'candidate/translations/ko';contracts=out/'contracts';inputs=out.parent/'inputs'
    resources=read_json(root/'ui/resources.json');bindings=read_json(root/'ui/bindings.json')
    rschema=read_json(contracts/'ui-resources.schema.json');bschema=read_json(contracts/'ui-bindings.schema.json');checks=[]
    def ok(name,condition):
        if not condition:raise AssertionError(name)
        checks.append({'check':name,'status':'PASS'})
    def reject(name,fn):
        try:fn()
        except (ContractError,ValueError,json.JSONDecodeError) as e:
            checks.append({'check':name,'status':'PASS','rejection':str(e).split(':')[0]})
        else:raise AssertionError('unexpected accept '+name)
    def mutation(re=None,be=None):
        r=copy.deepcopy(resources);b=copy.deepcopy(bindings)
        if re:re(r)
        if be:be(b)
        validate_shape(rschema,r);validate_shape(bschema,b);return check_models(r,b)
    reject('duplicate_resource',lambda:mutation(re=lambda r:r['resources'].append(copy.deepcopy(r['resources'][0]))))
    reject('boolean_schema_version_is_not_integer_one',lambda:mutation(re=lambda r:r.update(schema_version=True)))
    reject('duplicate_binding',lambda:mutation(be=lambda b:b['bindings'].append(copy.deepcopy(b['bindings'][0]))))
    reject('dangling_resource',lambda:mutation(be=lambda b:b['bindings'][0].update(resource_id='missing.resource')))
    reject('exact_jp_disagreement',lambda:mutation(be=lambda b:b['bindings'][0]['source_identity'].update(exact_jp='changed')))
    reject('arbitrary_native_field',lambda:mutation(be=lambda b:b['bindings'][0]['selector'].update(native_field='_text')))
    reject('executable_rva',lambda:mutation(be=lambda b:b['bindings'][0].update(rva=32)))
    reject('selector_traversal',lambda:mutation(be=lambda b:b['bindings'][0]['selector'].update(segments=['..'])))
    reject('malformed_tag',lambda:mutation(re=lambda r:r['resources'][0].update(ko=r['resources'][0]['ko']+'<b')))
    reject('malformed_placeholder',lambda:mutation(re=lambda r:r['resources'][0].update(ko=r['resources'][0]['ko']+'{x')))
    reject('new_template_placeholder',lambda:mutation(re=lambda r:r['resources'][0].update(ko=r['resources'][0]['ko']+'{x}')))
    reject('newline_variant_damage',lambda:mutation(re=lambda r:next(x for x in r['resources'] if '\r\n' in x['jp']).update(ko='changed\nvalue')))
    reject('master_ref_hidden_ko',lambda:mutation(re=lambda r:next(x for x in r['resources'] if x['kind']=='master_ref').update(ko='hidden fallback')))
    masterid=next(x['id'] for x in resources['resources'] if x['kind']=='master_ref')
    keep=mutation(re=lambda r:next(x for x in r['resources'] if x['id']==masterid).update(disposition='keep_source'),
                  be=lambda b:[x.update(enabled=False) for x in b['bindings'] if x['resource_id']==masterid])
    ok('disabled_master_ref_keep_source_is_valid_no_ko_fallback',keep['resource_count']==len(resources['resources']))
    reject('hold_activation',lambda:mutation(re=lambda r:r['resources'][0].update(disposition='hold'),be=lambda b:b['bindings'][0].update(enabled=True)))
    reject('unsupported_markup_role',lambda:mutation(re=lambda r:r['resources'][0].update(markup_policy='omit-cspace-v1')))
    reject('source_id_without_callback',lambda:mutation(be=lambda b:b['bindings'][0]['selector'].update(source_id=1,lifetime='owner')))
    result=mutation(be=lambda b:b['bindings'][0].update(adapter_id='future.optional.v99'))
    ok('unknown_optional_adapter_format_valid_and_unavailable_warning',bool(result['warnings']))
    # Verify source row identities directly against independent input files.
    mapping=read_json(out/'migration/MIGRATION_MAP.json');cache={}
    for row in mapping:
        if 'source_index' not in row:continue
        file=row['source_file']
        if file not in cache:cache[file]=read_json(inputs/file)
        doc=cache[file];i=row['source_index']
        if file.endswith('dictionary_rule_ui10.json'):original=doc
        elif file.endswith('story_sources.json'):original=doc['keywords'][i]
        elif file.endswith('CANDIDATE_TITLE_CATALOG.json'):original=doc['entries'][i]
        else:
            collection='mappings' if file.endswith('UI3_DYNAMIC_TITLE_PROPOSALS.json') else 'bindings' if file.endswith('UI6_BINDING_PLAN.json') else 'rules'
            original=doc[collection][i]
        if sha(encoded(original))!=row['source_row_sha256']:raise AssertionError('source row identity '+file+'#'+str(i))
    ok('all_original_row_hashes_match_inputs',True)
    chrome=read_json(inputs/'ui21/canonical/payload/plugins/GCUI11Chrome_UI18_20261010/chrome_rules.json')['rules']
    x=Exporter(inputs,out);y=Exporter(inputs,out)
    for i,row in enumerate(chrome):x.resource('Chrome',row,i)
    for i,row in enumerate(reversed(chrome)):y.resource('Chrome',row,i)
    ok('source_reordering_does_not_rename_resource_ids',sorted(x.resources,key=lambda r:r['id'])==sorted(y.resources,key=lambda r:r['id']))
    z=copy.deepcopy(chrome[0]);z['ko']+=' 검수'
    ok('ko_correction_does_not_rename_resource_id',Exporter(inputs,out).resource('Chrome',z,999)['id']==x.resources[0]['id'])
    ok('mixed_crlf_variant_preserved',any(x['jp']=='系統\r\n傾向\n' and x['ko']=='계열\r\n성향\n' for x in resources['resources']))
    ok('fullwidth_segment_preserved',any('Text_IteｍType' in x['selector'].get('segments',[]) for x in bindings['bindings']))
    ok('dynamic_stays_disabled',all(not b['enabled'] for b in bindings['bindings'] if b['adapter_id']=='core.dynamic-caption.v1'))
    ok('ng02_stays_disabled',all(not b['enabled'] for b in bindings['bindings'] if b['adapter_id'].startswith(('story.','battle.'))))
    ko_only=copy.deepcopy(resources)
    x=next(x for x in ko_only['resources'] if x['kind']=='literal' and '<' not in x['jp'] and '\n' not in x['jp'])
    x['ko']+=' 검수';validate_shape(rschema,ko_only);check_models(ko_only,bindings)
    # Tiny separate fixture avoids repeating the one-time 1900-file import.
    with tempfile.TemporaryDirectory(dir=out/'migration') as td:
        td=Path(td);a=td/'before';b=td/'after'
        for rt in [a,b]:
            write(rt/'ui/resources.json',resources);write(rt/'ui/bindings.json',bindings)
            write(rt/'layout/episode-title.json',read_json(root/'layout/episode-title.json'))
            write(rt/'master.json',{});write(rt/'names.json',{'a':'1'});write(rt/'manifest.json',{'novels':{}});write(rt/'novels/123.json',{'a':'1'})
        receipt=td/'fixture-import.json';write(receipt,{'files':[{'path':n,'bytes':(a/n).stat().st_size,'sha256':sha((a/n).read_bytes())} for n in ['master.json','names.json','manifest.json','novels/123.json']]})
        baseline_legacy=(b/'manifest.json').read_bytes();oldm=build(a,receipt)
        write(b/'ui/resources.json',ko_only);newm=build(b,receipt)
        ok('ko_change_semantic_text_only',diff(a,b)['classification']=='text_only')
        ok('ko_change_regenerates_ui_manifest',oldm['revision']!=newm['revision'])
        ok('ui_manifest_never_registers_novel',(b/'manifest.json').read_bytes()==baseline_legacy)
        fixture_r=copy.deepcopy(resources);fixture_b=copy.deepcopy(bindings)
        added={'id':'fixture.supported.resource','role':'fixture_caption','kind':'literal','jp':'NEW_SOURCE','ko':'새 항목','disposition':'translate'}
        fixture_r['resources'].append(added);binding=copy.deepcopy(next(x for x in fixture_b['bindings'] if x['adapter_id']=='core.static-caption.v1' and not x.get('action_group')))
        binding.update(id='fixture.supported.binding',resource_id=added['id']);binding['source_identity']['identity']='fixture-new-identity';binding['source_identity']['exact_jp']=added['jp']
        fixture_b['bindings'].append(binding);write(b/'ui/resources.json',fixture_r);write(b/'ui/bindings.json',fixture_b)
        validate_package(b,contracts)
        ok('binding_addition_not_text_only',diff(a,b)['classification']=='structural')
        oldindex=read_json(b/'legacy-components.json');write(b/'names.json',{'a':'2'});write(b/'novels/123.json',{'a':'2'})
        changedm=build(b,receipt,['names.json','novels/123.json']);newindex=read_json(b/'legacy-components.json')
        oldids={x['path']:x for x in oldindex['files']};newids={x['path']:x for x in newindex['files']}
        ok('same_length_names_and_novel_change_updates_legacy_identity',all(oldids[n]['size']==newids[n]['size'] and oldids[n]['sha256']!=newids[n]['sha256'] for n in ['names.json','novels/123.json']))
        ok('legacy_index_does_not_register_or_deny_stored_novel',(b/'manifest.json').read_bytes()==baseline_legacy)
        nextm=build(b,receipt)
        ok('ui_only_rebuild_keeps_declared_legacy_corrections',changedm['revision']==nextm['revision'] and read_json(b/'legacy-components.json')==newindex)
        write(b/'layout/episode-title.json',{**read_json(root/'layout/episode-title.json'),'base_size':35})
        write(td/'malformed.json',{})
        (td/'malformed.json').write_text('{"a":1,"a":2}',encoding='utf-8')
        reject('duplicate_json_key',lambda:read_json(td/'malformed.json'))
        duplicate=copy.deepcopy(resources);duplicate['resources'].append(copy.deepcopy(duplicate['resources'][0]));write(b/'ui/resources.json',duplicate)
        reject('semantic_diff_rejects_duplicate_instead_of_no_change',lambda:diff(a,b))
        reject('manifest_path_traversal',lambda:safe_path(b,'../outside.json'))
        reject('manifest_absolute_path',lambda:safe_path(b,'C:/outside.json'))
    # Deterministic migration files include no wall-clock, session or pointer
    # values in runtime documents. Audit-only evidence remains separate.
    tracked=list((root/'ui').glob('*.json'))+list((root/'layout').glob('*.json'))+list((out/'contracts').glob('*.json'))+list((out/'migration').glob('*.json'))+[root/'legacy-components.json',root/'release-manifest.json',out/'DATA_MIGRATION_REPORT.json']
    before={p:sha(p.read_bytes()) for p in tracked if p.name!='MIGRATION_TESTS.json'}
    Exporter(inputs.resolve(),out.resolve()).run()
    ok('deterministic_export_identical_bytes',all(sha(p.read_bytes())==h for p,h in before.items()))
    lexer=list(csharp_literals('/* "ignored" */ var a="系統\\r\\n傾向\\n"; var b=@"x""y"; // "ignored"\n'))
    ok('literal_lexer_preserves_escape_newline_and_verbatim',len(lexer)==2 and lexer[0]['value']=='系統\r\n傾向\n' and lexer[1]['value']=='x"y')
    report={'status':'PASS','scope':'offline data/schema/exporter tests; native game NOT_RUN',
            'checks':checks,'passed':len(checks),'failed':0,'legacy_import_repeated':False,
            'game_operations':0,'dll_rebuild_performed_by_data_tests':False}
    write(out/'migration/MIGRATION_TESTS.json',report);return report

if __name__=='__main__':
    out=Path(__file__).resolve().parents[1]
    try:result=run(out)
    except Exception as e:
        write(out/'migration/MIGRATION_TESTS.json',{'status':'FAIL','failed':1,'reason':str(e),'game_operations':0})
        raise
    print(json.dumps({'status':result['status'],'passed':result['passed'],'failed':result['failed']}))
