"""Finite restoration report correction. Read-only inputs; never a runtime/acceptance gate.

The prior report/log is retained. Exact physical signatures use their SHA-derived
identity; abbreviated console symbols never identify a hook. Native display and
required/alternative/optional availability remain separate.
"""
from pathlib import Path
import argparse, collections, hashlib, json, re

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def sid(owner, member, parameters):
    return hashlib.sha256((owner+'::'+member+'('+','.join(parameters)+')').encode()).hexdigest()[:12].upper()

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--candidate',type=Path,required=True)
    ap.add_argument('--prior',type=Path,required=True,help='Preserved Dev r1 task root containing repository/accounting/audit')
    ap.add_argument('--out',type=Path,required=True)
    a=ap.parse_args();candidate=a.candidate.resolve();prior=a.prior.resolve();out=a.out.resolve()
    assert out!=prior and not out.is_relative_to(prior),'DO_NOT_OVERWRITE_PRIOR_EVIDENCE'
    out.mkdir(parents=True,exist_ok=True)
    emit=lambda name,value:(out/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n','utf-8')
    original=[json.loads(x) for x in (prior/'accounting/ALL_BINDINGS_FINAL.jsonl').read_text('utf-8-sig').splitlines()]
    bindings={x['id']:x for x in read(candidate/'translations/ko/ui/bindings.json')['bindings']}
    assert set(bindings)=={x['id'] for x in original},'EXACT_SCOPE_ID_DIFFERENCE'
    log=prior/'audit/native/RESTORE14_FINAL.log';requests={};outcomes={}
    for line,txt in enumerate(log.read_text('utf-8-sig').splitlines(),1):
        m=re.search(r'HOOK_REQ_([A-F0-9]{12})_(.+)_A(\d+)$',txt)
        if m:requests[m[1]]={'line':line,'symbol':m[2],'arity':int(m[3])}
        m=re.search(r'HOOK_RESULT_([A-F0-9]{12})_(INSTALLED|UNAVAILABLE|ISOLATED)_(.+)$',txt)
        if m:outcomes[m[1]]={'line':line,'state':m[2],'reason':m[3]}
    specs=[('Gc.Battle.UI.BattleMenuPopup','OnSet',[],'alternative_entry'),
        ('Gc.Battle.UI.BattleMenuPopup','UpdateTabContents',['System.Int32'],'alternative_rebind'),
        ('DMM.OLG.Unity.Extensions.Novel.EventSelect','AddSelectMessage',['System.String','UnityEngine.Events.UnityAction'],'required'),
        ('DMM.OLG.Unity.Extensions.Novel.EventSelect','Release',[],'required_prefix'),
        ('DMM.OLG.Unity.Extensions.Novel.EventSelect','Open',[],'optional'),
        ('Gc.Mission.MissionPopup','OnLoad',[],'alternative_entry'),
        ('Gc.Mission.MissionPopup','UpdateView',['System.Int32','System.Int32'],'alternative_rebind'),
        ('Gc.Mission.ContentScrollCell','Setup',['Gc.Mission.ContentScrollCellData'],'alternative_typed_parent'),
        ('Gc.PresentPopup','OnLoad',[],'alternative_entry'),
        ('Gc.PresentPopup','OnUpdateDataSet',[],'alternative_rebind'),
        ('Gc.PresentScrollCell','Setup',['Gc.IPresent'],'alternative_typed_parent')]
    # Verify the relationship against the actual submitted implementation, rather than
    # turning a logical dispatch name into an executable method.
    battle=(candidate/'source/GCMod.Candidate/Native/BattleNativeProvider.cs').read_text('utf-8-sig')
    story=(candidate/'source/GCMod.Candidate/Native/StoryNativeProvider.cs').read_text('utf-8-sig')
    general=(candidate/'source/GCMod.Candidate/Native/GeneralUiProvider.cs').read_text('utf-8-sig')
    assert 'Sub(typeof(Gc.Battle.UI.BattleMenuPopup),"OnSet",Type.EmptyTypes,Menu)' in battle
    assert 'Sub(typeof(Gc.Battle.UI.BattleMenuPopup),"UpdateTabContents",new[]{typeof(int)},Menu)' in battle
    assert '"Gc.Battle.UI.BattleMenuPopup.completed_binding"' in battle
    assert 'RequiredLifecycleAvailable(add,close)' in story and 'STORY_CHOICE_ADD_RELEASE_ACTIVE_ROOT_OPEN_UNAVAILABLE' in story
    assert 'Hook(typeof(Gc.Mission.MissionPopup),"UpdateView",2' in general
    assert 'Hook(typeof(Gc.PresentPopup),"OnUpdateDataSet",0' in general
    catalog={}
    for owner,member,params,role in specs:
        key=sid(owner,member,params)
        catalog[key]={'owner':owner,'member':member,'parameter_types':params,'signature_id':key,'role':role,
            'request':requests.get(key),'outcome':outcomes.get(key),'capture':'audit/native/RESTORE14_FINAL.log',
            'capture_sha256':sha(log),'observed_DLL_sha256':'255edc5005ea172789165aa29666dc82624ada222a10127a3c75893f8c0b5b37',
            'display_PASS_inferred':False}
    keys=lambda owner:[k for k,v in catalog.items() if v['owner']==owner]
    corrected=[];corrected_ids=[]
    for old in original:
        b=bindings[old['id']];scope=b['selector']['scope_key'];row=dict(old)
        row['prior_report_native_stages']=row.pop('stages');row['prior_callback_declaration']=row.pop('required_producer_callbacks')
        contract={'logical_declaration':row['prior_callback_declaration'],'physical_signature_resolution':'PRIOR_DECLARATION_ONLY_NOT_ABBREVIATED_SYMBOL_JOIN'}
        if scope=='battle-b06':
            contract={'logical_producers':row['prior_callback_declaration'],
                'dispatch':'BattleNativeProvider.Menu -> completed_binding/typed fragment selector',
                'entry_any':keys('Gc.Battle.UI.BattleMenuPopup'),'required_all':[],'optional':[]}
        elif scope=='story-choice-list':
            ks=keys('DMM.OLG.Unity.Extensions.Novel.EventSelect')
            contract={'required_all':[k for k in ks if catalog[k]['role'].startswith('required')],
                'optional':[k for k in ks if catalog[k]['role']=='optional'],
                'active_root_alternative':'Existing StoryNativeProvider.Tick natural active-root; no forced Open',
                'branch_selection_and_Release_native':'SEPARATE_HELD_IF_PROGRESS_IMPACT_UNCERTAIN'}
        elif scope in ('gc.mission.missionpopup','gc.presentpopup'):
            owners=['Gc.Mission.MissionPopup','Gc.Mission.ContentScrollCell'] if scope=='gc.mission.missionpopup' else ['Gc.PresentPopup','Gc.PresentScrollCell']
            contract={'entry_any':[k for owner in owners for k in keys(owner)],'required_all':[],'optional':[],
                'typed_cell_route':'Natural completed Setup -> current typed popup ancestor; not a synthetic owner callback'}
        if 'entry_any' in contract or 'required_all' in contract:corrected_ids.append(b['id'])
        row['physical_callback_contract']=contract
        row['no_abbreviated_or_fuzzy_hook_join']=True
        row['evidence_level']='EXPECTED_DISABLED_OR_KEEP_HOLD' if not b['enabled'] else 'EVIDENCE_GAP_PENDING_SCOPED_JOIN'
        row['closure_scope']='REPORT_CORRECTION_NOT_NEW_NATIVE_ACCEPTANCE'
        corrected.append(row)
    (out/'ALL_BINDINGS_CLOSEOUT.jsonl').write_text(''.join(json.dumps(x,ensure_ascii=False)+'\n' for x in corrected),'utf-8')
    changed=[];same=[]
    for p in (candidate/'source').rglob('*.cs'):
        if any(x in ('bin','obj') for x in p.parts):continue
        rel=p.relative_to(candidate);before=prior/'repository'/rel
        (same if before.is_file() and sha(p)==sha(before) else changed).append(rel.as_posix())
    emit('SOURCE_LINEAGE_FROM_R1.json',{'source_bytes_equal':not changed,'unchanged_source_files':same,'changed_or_added_source_files':changed,
        'prior_DLL_sha256':sha(prior/'repository/candidate/GCMod.dll'),'current_DLL_sha256':sha(candidate/'candidate/GCMod.dll'),
        'dependency_note':'Native renderer plus shared font/ownership/geometry files are included. Equal prior evidence is reused under its original session/DLL/time, not renamed as a new execution.'})
    emit('EXACT_PHYSICAL_CALLBACK_CORRECTION.json',{'physical_signatures':catalog,'corrected_binding_ids':corrected_ids,
        'unique_capture_signature_outcomes':dict(collections.Counter(x['state'] for x in outcomes.values())),
        'unique_physical_signatures_in_this_supplement':len(catalog),'binding_expansion_is_not_hook_install_count':True,
        'missing_exact_supplement_requests':[k for k,v in catalog.items() if v['request'] is None],
        'runtime_hooks_added':0,'new_native_PASS_created':0,'old_original_evidence_edited':False})
    print(json.dumps({'bindings':len(corrected),'corrected_declarations':len(corrected_ids),'signature_supplement':len(catalog),'source_equal':not changed,'missing_exact_requests':[k for k,v in catalog.items() if v['request'] is None]}))
if __name__=='__main__':main()
