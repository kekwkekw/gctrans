"""Deterministically migrate accepted addon data without changing any input.

NG02 writers remain disabled in the partial candidate. Migration evidence keeps
historical eligibility separately from candidate native adapter availability.
"""
from __future__ import annotations
import argparse,hashlib,json,re,shutil
from pathlib import Path
from collections import Counter,defaultdict
from contract_validation import read_json,validate_package,SUPPORTED

MODULES={
 'Core':'GCUI6_UI18_20261010','Consumers':'GCUI10Consumers_UI18_20261010',
 'Chrome':'GCUI11Chrome_UI18_20261010','Residuals':'GCUI12Residuals_UI18_20261010',
 'Captions':'GCUI13Captions_UI18_20261010','Story':'GCUI10Story_UI18_20261010',
 'Battle':'GCUI16Battle_UI18_20261010','Fit3':'GCEpisodeTitleFit3_UI18_20261010'}
CUSTODY='canonical/evidence/source_custody/gc_ui16_battle_ui_polish_terminology_codex_result_20261010/source'
STORY_PARTIAL='canonical/evidence/source_custody/gc_ui14_choice_guard_layout_scroll_codex_result_20261009/code/source_work/GCUI10Story'

def sha(raw):return hashlib.sha256(raw).hexdigest()
def encoded(value):return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf-8')
def write(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(encoded(value))
def relative_posix_key(path,root):return path.relative_to(root).as_posix()
def audit_order(records,path_key,*record_keys):
    # pathlib ordering follows platform path semantics. The published audit
    # contract instead uses explicit relative POSIX strings and ordinal keys.
    return sorted(records,key=lambda row:(row[path_key],*(row[k] for k in record_keys),encoded(row)))
def slug(s):return re.sub('[^a-z0-9._-]+','-',s.lower()).strip('-')[:128] or 'unspecified'
def jp(row):return row.get('jp',row.get('exact_jp'))
def legacy_id(row,index):return row.get('id',row.get('rule_id',row.get('decision_id','ROW-'+str(index))))
def source_key(row):
    # Source locator identity excludes KO and original array position. Reordering
    # the donor rows therefore cannot rename translation/binding identities.
    fields=['source_identity','context_key','source_id','owner','root','role','selector','callback','surface','branch',
            'source_path_segments','source_hierarchy_segments','segments','domain','domain_id','source_value','region']
    return {'jp':jp(row),**{k:row[k] for k in fields if k in row}}
def stable_suffix(row):return sha(encoded(source_key(row)))[:16]

def make_contracts(design:Path,out:Path):
    out.mkdir(parents=True,exist_ok=True)
    for name in ['ui-resources.schema.json','ui-bindings.schema.json','episode-title-fit.schema.json']:
        d=read_json(design/'contracts'/name)
        d['title']=d['title'].replace('DESIGNED_V1','NG01_V1')
        d['$id']=d.get('$id','urn:gc:'+name).replace('designed-v1','ng01-v1')
        if name=='ui-resources.schema.json':
            d['properties']['resources']['items']['properties']['markup_policy']={'enum':['exact-tags','omit-cspace-v1']}
        if name=='ui-bindings.schema.json':
            b=d['properties']['bindings']['items'];s=b['properties']['selector']['properties']
            s.update({'type':{'enum':['static','model','control','date','profile','exchange','caption']},
                      'domain':{'type':'integer','minimum':-1,'maximum':65535},
                      'source_id':{'type':'integer','minimum':-1,'maximum':65535},
                      'source_value':{'type':'integer','minimum':-1,'maximum':65535},
                      'selected':{'type':'boolean'},
                      'template_id':{'type':'string','minLength':1,'maxLength':128,'pattern':'^[a-z0-9][a-z0-9._-]*$'},
                      'lifetime':{'enum':['callback','owner','cell']},
                      'overflow_policy':{'enum':['intrinsic-only']}})
            b['allOf'][1]['then']['properties']['adapter_id']={'enum':['core.static-caption.v1','ui4.static-caption.v1']}
            d['description']='Bounded read-only selectors. Compiled adapters own scope/slot and native compatibility. Unknown optional adapters are unavailable, never executable declarations.'
        write(out/name,d)
    write(out/'VALIDATOR_CONTRACT.json',{'validator':'NG01 dependency-free contract subset',
          'general_draft_2020_12_conformance_claim':False,'supported_keywords':sorted(SUPPORTED),
          'unknown_validation_keyword':'reject','source_comparison':'ordinal exact; no Unicode or newline normalization',
          'adapter_policy':'compiled finite registry; unknown optional capability unavailable',
          'limits_are_safety_bounds_not_historical_exact_counts':True})

def csharp_literals(text):
    """Lex ordinary/verbatim strings, ignoring comments and char literals.

    Interpolated strings are retained as source template tokens (not claimed as
    resolved translations). C# escape sequences decode without normalizing JP.
    """
    i=0;n=len(text)
    while i<n:
        if text.startswith('//',i):
            end=text.find('\n',i);i=n if end<0 else end;continue
        if text.startswith('/*',i):
            end=text.find('*/',i+2);i=n if end<0 else end+2;continue
        if text[i]=="'":
            i+=1
            while i<n:
                if text[i]=='\\':i+=2;continue
                if text[i]=="'":i+=1;break
                i+=1
            continue
        start=i;verbatim=False;interpolated=False
        if text.startswith('$@"',i) or text.startswith('@$"',i):verbatim=True;interpolated=True;i+=2
        elif text.startswith('@"',i):verbatim=True;i+=1
        elif text.startswith('$"',i):interpolated=True;i+=1
        if i<n and text[i]=='"':
            i+=1;chars=[]
            while i<n:
                c=text[i]
                if c=='"':
                    if verbatim and i+1<n and text[i+1]=='"':chars.append('"');i+=2;continue
                    i+=1;break
                if c=='\\' and not verbatim:
                    i+=1
                    if i>=n:break
                    e=text[i];escape={'n':'\n','r':'\r','t':'\t','0':'\0','a':'\a','b':'\b','f':'\f','v':'\v','"':'"',"'":"'",'\\':'\\'}
                    if e in 'uUx':
                        maxlen={'u':4,'U':8,'x':4}[e];m=re.match('[0-9a-fA-F]{1,'+str(maxlen)+'}',text[i+1:])
                        if m:chars.append(chr(int(m.group(),16)));i+=len(m.group())+1;continue
                    chars.append(escape.get(e,'\\'+e));i+=1;continue
                chars.append(c);i+=1
            yield {'line':text.count('\n',0,start)+1,'offset':start,'token':text[start:i],
                   'value':''.join(chars),'interpolated':interpolated}
            continue
        i=start+1

class Exporter:
    def __init__(self,inputs:Path,out:Path):
        self.inputs=inputs;self.ui21=inputs/'ui21';self.ui18=inputs/'ui18';self.design=inputs/'design';self.out=out
        self.root=out/'candidate/translations/ko';self.resources=[];self.bindings=[];self.mapping=[];self.datafiles=[]
        self.stats=defaultdict(Counter);self.evidence=out/'migration';self.resource_index=defaultdict(list)
    def p(self,module,file):return self.ui21/'canonical/payload/plugins'/MODULES[module]/file
    def load(self,module,file):
        p=self.p(module,file);raw=p.read_bytes();d=read_json(p)
        self.datafiles.append({'module':module,'source_file':p.relative_to(self.inputs).as_posix(),'sha256':sha(raw),'bytes':len(raw)})
        return d
    def resource(self,module,row,index,kind='literal',role=None):
        lid=legacy_id(row,index);rid='ui.'+module.lower()+'.'+lid+'.'+stable_suffix(row)
        r={'id':rid,'role':slug(role or row.get('role','STATIC_CAPTION')),'kind':kind,'jp':jp(row),
           'disposition':'translate'}
        if kind=='master_ref':
            r['master_ref']={'table':row['reviewed_resource_table'],'field':row['reviewed_resource_field'],'exact_jp':jp(row)}
            # Audit literal references point to active-Master authority; a KO
            # value is never stored in the runtime master_ref resource.
            self.resource_index[row['ko']].append(rid)
        else:r['ko']=row['ko']
        self.resources.append(r);self.resource_index[r['jp']].append(rid)
        if 'ko' in r:self.resource_index[r['ko']].append(rid)
        self.stats[module]['resources']+=1
        return r
    def record(self,module,file,row,index,r,bs,legacy_enabled,reason,disposition=None,original_row=None):
        self.mapping.append({'module':module,'source_file':self.p(module,file).relative_to(self.inputs).as_posix(),
          'source_index':index,'legacy_id':legacy_id(row,index),'source_row_sha256':sha(encoded(original_row if original_row is not None else row)),
          'source_identity':row.get('source_identity',row.get('context_key',row.get('source_id'))),
          'jp':jp(row),'ko':row.get('ko'),'legacy_enabled':legacy_enabled,'activation_evidence':reason,
          'disposition':disposition or ('master_ref' if r and r['kind']=='master_ref' else 'ui_resource'),
          'resource_id':r['id'] if r else None,'binding_ids':[b['id'] for b in bs],
          'candidate_enabled':any(b['enabled'] for b in bs),'native_runtime_proven':False})
        self.stats[module]['source_rows']+=1
    def binding(self,module,row,index,r,adapter,selector,enabled,identity=None,kind='current-model',suffix='',group=None):
        b={'id':'binding.'+module.lower()+'.'+legacy_id(row,index)+'.'+stable_suffix(row)+suffix,
           'resource_id':r['id'],'adapter_id':adapter,'enabled':bool(enabled),
           'source_identity':{'kind':kind,'identity':identity or row.get('source_identity') or row.get('context_key') or legacy_id(row,index),
                              'exact_jp':r['jp']},'selector':selector}
        if group:b['action_group']=group
        self.bindings.append(b);self.stats[module]['bindings']+=1;self.stats[module]['enabled_bindings']+=int(b['enabled'])
        return b
    def static_core(self):
        module='Core';d=self.load(module,'ui.json');receipts=self.load(module,'runtime_bindings.json')['bindings']
        joined={sid:x for x in receipts for sid in x['source_rule_ids']}
        for i,row in enumerate(d['rules']):
            rid=legacy_id(row,i);r=self.resource(module,row,i)
            rec=joined.get(rid);segments=rec['owner_relative_segments'] if rec else row['source_hierarchy_segments'][1:]
            sel={'scope_key':slug(rec['owner_type'] if rec else row['allowed_popup_owner_type']),
                 'slot_key':slug(rec['binding_id'] if rec else rid),'segments':segments,'type':'static','lifetime':'owner'}
            group='core.'+rec['binding_id']+'.co-match' if rec and len(rec['source_rule_ids'])>1 else None
            b=self.binding(module,row,i,r,'core.static-caption.v1',sel,rec is not None,kind='static-asset',group=group)
            self.record(module,'ui.json',row,i,r,[b],bool(rec),'canonical runtime_bindings.json current join; 9 bindings / 11 source identities')
        d=self.load(module,'static.proposals.json');receipts=self.load(module,'ui4_receipts.json')['observations']
        joined={sid:x for x in receipts for sid in x['source_rule_ids']}
        for i,row in enumerate(d['rules']):
            rid=legacy_id(row,i);r=self.resource(module,row,i)
            rec=joined.get(rid);segments=rec['root_relative_segments'] if rec else row['source_path_segments'][1:]
            # Current receipts are the activation authority. Per-source identity
            # is retained even when two sources explicitly co-match one target.
            sel={'scope_key':slug(rec['owner_type'] if rec else row['root']),
                 'slot_key':slug(rec['binding_id'] if rec else rid),'type':'static','lifetime':'owner'}
            if segments:sel['segments']=segments
            group='ui4.'+rec['binding_id']+'.co-match' if rec and len(rec['source_rule_ids'])>1 else None
            b=self.binding(module,row,i,r,'ui4.static-caption.v1',sel,rec is not None,kind='static-asset',group=group)
            self.record(module,'static.proposals.json',row,i,r,[b],bool(rec),'canonical ui4_receipts current34 / source42; remaining36 disabled')
        d=self.load(module,'UI3_DYNAMIC_TITLE_PROPOSALS.json')
        for i,original in enumerate(d['mappings']):
            row=dict(original,id='UI3-DYNAMIC');r=self.resource(module,row,i)
            b=self.binding(module,row,i,r,'core.dynamic-caption.v1',{'scope_key':'mission-dynamic-title','slot_key':'mission-title','type':'model','lifetime':'owner'},False)
            self.record(module,'UI3_DYNAMIC_TITLE_PROPOSALS.json',row,i,r,[b],False,'AllowDynamic=false; receipt0; preserve disabled resource',original_row=original)
        d=self.load(module,'UI6_BINDING_PLAN.json');current=read_json(self.design/'audit/CORE_UI_INVENTORY.json')['activation_evidence']['ui6']['exact_raw_joins']
        for i,row in enumerate(d['bindings']):
            rec=next((x for x in current if x['plan_rule_id']==row['id']),None);r=self.resource(module,row,i)
            sort='SORT' in row['id'];sel={'scope_key':slug(row['owner']),'slot_key':slug(row['region']),
                'type':'model','lifetime':'callback' if sort else 'owner'}
            if rec:sel['segments']=rec['target_relative_segments']
            b=self.binding(module,row,i,r,'core.current-model.v1',sel,rec is not None)
            self.record(module,'UI6_BINDING_PLAN.json',row,i,r,[b],bool(rec),'current receipt1 exact plan join; plan9 is not activation authority')
    def consumers(self):
        module='Consumers'
        for file in ['consumer_rules.json','f1_rules_32.json']:
            d=self.load(module,file)
            for i,row in enumerate(d['rules']):
                element=row['role']=='ELEMENT_FILTER_OPTION';date=row['role']=='OPTION_LAST_UPDATE_EMPTY_FALLBACK';sort=row['role']=='EQUIPMENT_SORT_OPTION'
                r=self.resource(module,row,i,'master_ref' if element else 'literal')
                if date:self.resource_index[row['ko_prefix']].append(r['id'])
                sel={'scope_key':'option-date' if date else 'equipment-sort' if sort else 'element-filter' if element else 'current-filter',
                     'slot_key':'last-update-empty' if date else 'work-order-name' if sort else slug(row['role']),
                     'type':'date' if date else 'model','lifetime':'callback' if sort else 'owner'}
                for source,target in [('domain','domain'),('domain_id','domain'),('source_value','source_value'),('source_id','source_id')]:
                    if row.get(source) is not None:sel[target]=row[source]
                if date:sel['template_id']='last-update-empty-v1'
                b=self.binding(module,row,i,r,'common.date-caption.v1' if date else 'common.model-label.v1',sel,True,
                    identity=row.get('context_key',row.get('semantic_reference',row['id'] if 'id' in row else row['decision_id'])))
                self.record(module,file,row,i,r,[b],True,'current Consumers enabled module, contextual retained model/callback writer; F1 Expected32 externalized')
    def captions(self):
        for module,file,adapter in [('Chrome','chrome_rules.json','ui.chrome.v1'),('Residuals','residual_rules.json','ui.residual-caption.v1'),('Captions','caption_rules.json','ui.caption.v1')]:
            d=self.load(module,file)
            for i,row in enumerate(d['rules']):
                r=self.resource(module,row,i);role=row['role'];owner=row['owner']
                if module=='Captions' and role in {'character_palette','character_imaju_label','character_memory'} and '<cspace' in r['jp'] and '<cspace' not in r['ko']:
                    r['markup_policy']='omit-cspace-v1'
                type_='profile' if 'profile' in role else 'exchange' if 'exchange' in role else 'caption'
                sel={'scope_key':slug(owner),'slot_key':slug(role),'type':type_,'lifetime':'cell' if 'ScrollCell' in owner else 'owner'}
                if row.get('segments'):sel['segments']=row['segments']
                if type_=='exchange':sel['overflow_policy']='intrinsic-only'
                # Repeated semantic IDs have distinct original selectors and
                # unique binding/resource IDs; none are silently overwritten.
                identity=[row['id'],row.get('source_sha256',row.get('sourceHash','')),owner,role,jp(row)]
                b=self.binding(module,row,i,r,adapter,sel,row.get('enabled',False),identity=identity)
                self.record(module,file,row,i,r,[b],row.get('enabled',False),'per-source enabled flag + canonical module config; historical PENDING ignored')
    def ng02(self):
        module='Story';d=self.load(module,'choice_rules_ui10.json')
        for i,row in enumerate(d['rules']):
            r=self.resource(module,row,i);cs=row['choice_source']
            sel={'scope_key':'story-choice-list','slot_key':'option-label','group_id':'tutorial.questions.'+str(cs['novel_id']),
                 'script_id':str(cs['novel_id']),'branch_label':cs['raw_branch_target']}
            b=self.binding(module,row,i,r,'story.choice.visual.v1',sel,False,identity=row['context_key'],kind='choice-group')
            self.record(module,'choice_rules_ui10.json',row,i,r,[b],True,'accepted legacy visual projection; native writer deferred NG02')
        row=self.load(module,'dictionary_rule_ui10.json');r=self.resource(module,row,0);bs=[]
        for surface,adapter in [('standalone-dictionary','story.dictionary-heading.v1'),('annotation-popup','story.annotation-heading.v1')]:
            bs.append(self.binding(module,row,0,r,adapter,{'scope_key':surface,'slot_key':'dictionary-heading','type':'caption','lifetime':'owner'},False,
                identity=row['context_key'],suffix='.'+surface))
        self.record(module,'dictionary_rule_ui10.json',row,0,r,bs,True,'two independent headings; 용어 사전 literal captured; writers deferred NG02')
        d=self.load(module,'story_sources.json')
        for i,row in enumerate(d['keywords']):
            self.record(module,'story_sources.json',dict(row,id='KEYWORD-'+str(row['id']),jp=row['jp_keyword'],ko=row['ko_keyword']),i,None,[],False,
                'Master mNovelKeyWords diagnostic body copy; not new UI authority','audit_only_master_diagnostic',original_row=row)
        module='Battle';d=self.load(module,'battle_rules.json')
        for i,row in enumerate(d['rules']):
            r=self.resource(module,row,i,role='battle-caption-'+row['surface'])
            sel={'scope_key':'battle-'+slug(row['surface']),'slot_key':slug(row['id']),'type':'caption','lifetime':'owner'}
            if row.get('segments'):sel['segments']=row['segments']
            b=self.binding(module,row,i,r,'battle.caption.v1',sel,False,identity=row.get('source_id',row['id']))
            self.record(module,'battle_rules.json',row,i,r,[b],row.get('enabled',False),'accepted106 exact variants including mixed CRLF; native writer deferred NG02')
        row={'id':'REQUIRED-PASSION','jp':'必要熱情','ko':'필요 열정','role':'NECESSARY_PASSION_TITLE_ONLY'}
        r=self.resource(module,row,106,'template_label');bs=[]
        for slot in [0,1]:
            bs.append(self.binding(module,row,106,r,'battle.command-cost-title.v1',{'scope_key':'battle-command','slot_key':'skill-slot-'+str(slot)+'-unusable-title',
                     'type':'caption','lifetime':'owner','template_id':'preserve-native-numeric-suffix-v1'},False,
                     identity='UI17-TEMPLATE-SLOT-'+str(slot),kind='native-template',suffix='.'+str(slot)))
        self.mapping.append({'module':module,'source_file':'ui18/code/battle/Plugin.cs','line':63,'legacy_id':'REQUIRED-PASSION',
             'jp':r['jp'],'ko':r['ko'],'disposition':'ui_template_resource','resource_id':r['id'],'binding_ids':[b['id'] for b in bs],
             'legacy_enabled':True,'candidate_enabled':False,'activation_evidence':'finite numeric suffix preserve template; writer deferred NG02'})
        module='Fit3';d=self.load(module,'CANDIDATE_TITLE_CATALOG.json')
        for i,row in enumerate(d['entries']):
            self.record(module,'CANDIDATE_TITLE_CATALOG.json',dict(row,id='TITLE-'+str(row['record_id'])+'-'+row['selector']),i,None,[],False,
                  'title provenance/eligibility audit only; active Master mNovels owns title translations','audit_only_title_provenance',original_row=row)
        write(self.root/'layout/episode-title.json',read_json(self.design/'examples/episode-title-fit.example.json'))
    def scan_sources(self):
        roots={'Core':[self.ui21/'code/helper/src',self.ui21/'code/helper/tests'],
               'Consumers':[self.ui21/CUSTODY/'GCUI10Consumers'],
               'Chrome':[self.ui21/CUSTODY/'GCUI11Chrome'],
               'Residuals':[self.ui21/CUSTODY/'GCUI12Residuals'],
               'Captions':[self.ui21/CUSTODY/'GCUI13Captions'],
               'Story':[self.ui21/STORY_PARTIAL,self.ui21/'canonical/evidence/source_custody/UI14_CURRENT_STORY'],
               'Battle':[self.ui18/'code/battle'],'Fit3':[self.ui21/'code/fit3/src',self.ui21/'code/fit3/tests']}
        inventory=[];files=[]
        for module,rts in roots.items():
            for p in sorted({p for rt in rts for p in rt.rglob('*.cs')},key=lambda p:relative_posix_key(p,self.inputs)):
                raw=p.read_bytes();text=raw.decode('utf-8-sig');test='test' in p.name.lower() or 'tests' in p.parts
                path=p.relative_to(self.inputs).as_posix();lits=list(csharp_literals(text))
                files.append({'module':module,'path':path,'sha256':sha(raw),'bytes':len(raw),'literal_count':len(lits),'test_only':test,
                              'current_source_mapping_supplied':module in {'Core','Battle','Fit3'},
                              'rebuild_executed_by_ng01_data_exporter':False,
                              'build_evidence_scope':'inherited source/build artifacts; new native rebuild correspondence is not asserted by this inventory',
                              'source_status':'current helper / UI18 Battle / UI21 Fit3' if module in {'Core','Battle','Fit3'} else 'custody reference; not final rebuild proof'})
                for x in lits:
                    value=x['value'];refs=sorted(set(self.resource_index.get(value,[])))
                    if test:disp='test_only'
                    elif p.name=='AutoLease.cs':disp='audit_only_test_operator_excluded'
                    elif refs:disp='ui_resource_reference'
                    elif re.search('[\uac00-\ud7a3]',value):disp='audit_only_diagnostic_or_policy_literal'
                    elif re.search('[\u3040-\u30ff\u3400-\u9fff]',value):disp='audit_only_source_selector_or_diagnostic'
                    else:disp='audit_only_code_contract'
                    inventory.append({'module':module,'path':path,**x,'disposition':disp,'resource_ids':refs,
                                      'runtime_literal_retained':False})
        files=audit_order(files,'path');inventory=audit_order(inventory,'path','offset')
        write(self.evidence/'SOURCE_FILES.json',files);write(self.evidence/'SOURCE_LITERAL_DISPOSITIONS.json',inventory)
        # Any untranslated Hangul token is retained for review as diagnostic/
        # policy evidence, never silently treated as a new active translation.
        unmatched=[x for x in inventory if re.search('[\uac00-\ud7a3]',x['value']) and not x['resource_ids'] and x['disposition']!='test_only']
        write(self.evidence/'UNMAPPED_KO_LITERAL_REVIEW.json',unmatched)
        return files,inventory,unmatched
    def data_literals(self):
        files=[];literals=[]
        canonical_set={x['source_file'] for x in self.datafiles}
        for module,folder in MODULES.items():
            root=self.ui21/'canonical/payload/plugins'/folder
            for p in sorted(root.glob('*.json'),key=lambda p:relative_posix_key(p,self.inputs)):
                raw=p.read_bytes();d=read_json(p);rel=p.relative_to(self.inputs).as_posix();start=len(literals)
                def visit(v,path):
                    if isinstance(v,dict):
                        for k,x in v.items():visit(x,path+'/'+str(k))
                    elif isinstance(v,list):
                        for i,x in enumerate(v):visit(x,path+'/'+str(i))
                    elif isinstance(v,str):
                        refs=sorted(set(self.resource_index.get(v,[])))
                        disposition='ui_resource_reference' if refs and rel in canonical_set else 'audit_only'
                        if p.name=='CANDIDATE_TITLE_CATALOG.json':disposition='audit_only_title_provenance'
                        if p.name=='story_sources.json':disposition='audit_only_master_diagnostic'
                        literals.append({'module':module,'source_file':rel,'json_pointer':path,'value':v,'disposition':disposition,'resource_ids':refs})
                visit(d,'')
                files.append({'module':module,'source_file':rel,'sha256':sha(raw),'bytes':len(raw),'literal_count':len(literals)-start,
                              'disposition':'canonical_rows_exported_with_audit_fields_retained' if rel in canonical_set else 'audit_only_no_runtime_dependency'})
        files=audit_order(files,'source_file');literals=audit_order(literals,'source_file','json_pointer')
        write(self.evidence/'DATA_FILES.json',files);write(self.evidence/'DATA_LITERAL_DISPOSITIONS.json',literals)
        cfgs=[]
        for module,folder in MODULES.items():
            for p in sorted((self.ui21/'canonical/payload/plugins'/folder).glob('*.cfg'),key=lambda p:relative_posix_key(p,self.inputs)):
                raw=p.read_bytes();values={}
                for line in raw.decode('utf-8-sig').splitlines():
                    if '=' in line and not line.lstrip().startswith(('#',';')):
                        k,v=(s.strip() for s in line.split('=',1))
                        if k in {'Mode','Language','Enabled','AllowExperimental','AllowDynamic','AllowUI4','AllowUI6Experimental'} or k.startswith('Enable'):
                            values[k]=v
                cfgs.append({'module':module,'source_file':p.relative_to(self.inputs).as_posix(),'sha256':sha(raw),'bytes':len(raw),'activation_flags':values,
                             'disposition':'audit_only_activation_evidence_no_runtime_cfg_dependency'})
        write(self.evidence/'CONFIG_ACTIVATION.json',audit_order(cfgs,'source_file'))
        return files,literals
    def import_legacy(self):
        source=self.ui21/'canonical/payload/translations/ko';receipts=[]
        previous=self.evidence/'LEGACY_BYTE_COPY.json'
        if previous.exists():
            old=read_json(previous)
            if all((self.root/x['path']).is_file() for x in old['files']):
                # This export touches only UI data. Initial import was verified
                # once; runtime and explicit changed-file manifest updates own
                # later candidate component verification.
                return old['files']
        for p in sorted(source.rglob('*.json'),key=lambda p:relative_posix_key(p,source)):
            rel=p.relative_to(source);dest=self.root/rel;dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(p,dest);raw=p.read_bytes();copied=dest.read_bytes()
            if raw!=copied:raise ValueError('BYTE_PRESERVING_IMPORT_FAILED:'+str(rel))
            receipts.append({'path':rel.as_posix(),'bytes':len(raw),'sha256':sha(raw),'byte_equal':True})
        write(self.evidence/'LEGACY_BYTE_COPY.json',{'files':receipts,'file_count':len(receipts),
            'novels_stored':len(list((source/'novels').glob('*.json'))),'legacy_manifest_unchanged':True,
            'unregistered_stored_policy':'preserved; no automatic registration or deny created',
            'novel_pipeline_rewritten':False})
        return receipts
    def run(self):
        make_contracts(self.design,self.out/'contracts');self.evidence.mkdir(parents=True,exist_ok=True)
        receipts=self.import_legacy();self.static_core();self.consumers();self.captions();self.ng02()
        self.resources.sort(key=lambda r:r['id']);self.bindings.sort(key=lambda r:r['id'])
        write(self.root/'ui/resources.json',{'schema_version':1,'locale':'ko','resources':self.resources})
        write(self.root/'ui/bindings.json',{'schema_version':1,'bindings':self.bindings})
        write(self.evidence/'MIGRATION_MAP.json',self.mapping)
        sf,sl,unmatched=self.scan_sources();df,dl=self.data_literals()
        validation=validate_package(self.root,self.out/'contracts')
        write(self.evidence/'ADAPTER_ROLE_INVENTORY.json',[{'adapter_id':a,'roles':sorted({r['role'] for r in self.resources if any(b['resource_id']==r['id'] and b['adapter_id']==a for b in self.bindings)}),
              'bindings':sum(b['adapter_id']==a for b in self.bindings),'candidate_enabled':sum(b['adapter_id']==a and b['enabled'] for b in self.bindings)} for a in sorted({b['adapter_id'] for b in self.bindings})])
        report={'status':'PASS_DATA_EXPORT_AND_CONTRACTS','scope':'NG01 offline partial candidate; no native game execution',
          'module_counts':dict(self.stats),'validation':validation,'legacy_byte_copies':len(receipts),
          'source_files':len(sf),'source_literals':len(sl),'data_files':len(df),'data_literals':len(dl),
          'source_literal_disposition_complete':all(x['disposition'] for x in sl),
          'source_row_disposition_complete':all(x['disposition'] for x in self.mapping),
          'unmapped_ko_literal_review_count':len(unmatched),'review_note':'Remaining Korean literals are diagnostic/policy/test evidence, not silently activated resources; see explicit list.',
          'ui4':{'rules':78,'current_receipts':34,'current_source_identities':42,'disabled_source_identities':36},
          'ui6':{'plan_rows':9,'current_receipts':1,'candidate_enabled':1},
          'dynamic':{'resources':2,'enabled':False},'chrome':{'rows':59,'unique_semantic_ids':52,'binding_ids_unique':True},
          'consumers_expected32_runtime_array':'not imported; contextual JSON resources/bindings are authority',
          'master_ref_element_count':5,'story_keyword37':'audit-only Master diagnostic','fit3_title3846':'audit-only provenance, layout policy exported',
          'source_final_rebuild_proof_gaps':['Consumers','Chrome','Residuals','Captions','Story'],
          'native_game_validation':'NOT_RUN_NO_GAME_AUTHORIZATION','accepted_baseline':'ACCEPTED_LOCAL_FROZEN',
          'accepted_baseline_modified':False,'external_ledger_modified':False,'xunity':'OFF',
          'no_runtime_receipt_dependencies':True,'no_original_game_dll_font_credentials_copied':True,
          'legacy_index_runtime_validation':'Names/Novel/legacy manifest metadata identity only; source bytes were all verified on initial import; no eager Novel parser/register/deny added',
          'ng02_deferred':['Choice visual writer','Annotation','Dictionary','Battle','Fit3 native writer'],
          'raw_source_identity_preserved_in':'migration/MIGRATION_MAP.json; source_row_sha256 uses deterministic original object serialization, file SHA separately'}
        report['runtime_id_policy']='legacy semantic prefix plus stable source-key SHA16; excludes KO and array index; source_index is audit locator only'
        write(self.out/'DATA_MIGRATION_REPORT.json',report)
        from build_manifests import build
        build(self.root,self.out/'migration/LEGACY_BYTE_COPY.json')
        return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inputs',type=Path,default=Path(__file__).resolve().parents[2]/'inputs')
    parser.add_argument('--out',type=Path,default=Path(__file__).resolve().parents[1])
    a=parser.parse_args();r=Exporter(a.inputs.resolve(),a.out.resolve()).run()
    print(json.dumps({k:r[k] for k in ['status','module_counts','validation','legacy_byte_copies','source_literals','data_literals']},ensure_ascii=False))
