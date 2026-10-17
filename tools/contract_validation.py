"""Dependency-free validator for the NG01 contracts' explicitly used JSON Schema subset.

Not a general Draft 2020-12 implementation. Unknown validation keywords fail closed.
The checked subset is documented in contracts/VALIDATOR_CONTRACT.json.
"""
from __future__ import annotations
import json
import re
from pathlib import Path

MAX_JSON_BYTES = 8 * 1024 * 1024
MAX_STRING_BYTES = 65536

class ContractError(ValueError):
    pass

def unique_object(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise ContractError('DUPLICATE_JSON_PROPERTY:' + key)
        out[key] = value
    return out

def read_json(path: Path):
    raw = path.read_bytes()
    if len(raw) > MAX_JSON_BYTES:
        raise ContractError('JSON_BYTE_LIMIT:' + path.name)
    value = json.loads(raw.decode('utf-8-sig'), object_pairs_hook=unique_object,
                       parse_constant=lambda s: (_ for _ in ()).throw(ContractError('NONFINITE_NUMBER')))
    def strings(x):
        if isinstance(x, str) and (len(x.encode('utf-8')) > MAX_STRING_BYTES or '\x00' in x):
            raise ContractError('STRING_BYTE_LIMIT_OR_NUL')
        if isinstance(x, dict):
            for k, v in x.items(): strings(k); strings(v)
        if isinstance(x, list):
            for v in x: strings(v)
    strings(value)
    return value

SUPPORTED = {'$schema','$id','title','description','$comment','type','properties','required',
             'additionalProperties','items','minItems','maxItems','uniqueItems','minLength',
             'maxLength','pattern','minimum','maximum','exclusiveMinimum','exclusiveMaximum',
             'const','enum','allOf','anyOf','oneOf','not','if','then','else'}

def validate_shape(schema, value, path='$'):
    unknown = set(schema) - SUPPORTED
    if unknown:
        raise ContractError('UNSUPPORTED_SCHEMA_KEYWORD:' + ','.join(sorted(unknown)))
    def fail(msg): raise ContractError('SCHEMA:' + path + ':' + msg)
    types = {'object':lambda x:isinstance(x,dict),'array':lambda x:isinstance(x,list),
             'string':lambda x:isinstance(x,str),'boolean':lambda x:isinstance(x,bool),
             'integer':lambda x:type(x) is int,'number':lambda x:type(x) in (int,float),
             'null':lambda x:x is None}
    if 'type' in schema:
        ts=schema['type'] if isinstance(schema['type'],list) else [schema['type']]
        if not any(types[t](value) for t in ts):fail('TYPE')
    def json_equal(a,b):
        # JSON Schema does not equate true with 1 as Python normally does.
        if isinstance(a,bool)!=isinstance(b,bool):return False
        return a==b
    if 'const' in schema and not json_equal(value,schema['const']):fail('CONST')
    if 'enum' in schema and not any(json_equal(value,x) for x in schema['enum']):fail('ENUM')
    if isinstance(value,dict):
        for k in schema.get('required',[]):
            if k not in value:fail('MISSING:' + k)
        if schema.get('additionalProperties') is False:
            if set(value) - set(schema.get('properties',{})):fail('UNKNOWN_PROPERTY')
        for k,s in schema.get('properties',{}).items():
            if k in value:validate_shape(s,value[k],path+'.'+k)
    if isinstance(value,list):
        if len(value)<schema.get('minItems',0) or len(value)>schema.get('maxItems',10**12):fail('ITEM_BOUND')
        if schema.get('uniqueItems') and len({json.dumps(x,sort_keys=True) for x in value})!=len(value):fail('UNIQUE_ITEMS')
        if 'items' in schema:
            for i,x in enumerate(value):validate_shape(schema['items'],x,path+'['+str(i)+']')
    if isinstance(value,str):
        if len(value)<schema.get('minLength',0) or len(value)>schema.get('maxLength',10**12):fail('STRING_BOUND')
        if 'pattern' in schema and not re.search(schema['pattern'],value):fail('PATTERN')
    if type(value) in (int,float):
        if value<schema.get('minimum',-float('inf')) or value>schema.get('maximum',float('inf')):fail('NUMBER_BOUND')
        if 'exclusiveMinimum' in schema and value<=schema['exclusiveMinimum']:fail('NUMBER_BOUND')
        if 'exclusiveMaximum' in schema and value>=schema['exclusiveMaximum']:fail('NUMBER_BOUND')
    def matches(s):
        try:validate_shape(s,value,path);return True
        except ContractError:return False
    for s in schema.get('allOf',[]):validate_shape(s,value,path)
    if 'anyOf' in schema and not any(matches(s) for s in schema['anyOf']):fail('ANY_OF')
    if 'oneOf' in schema and sum(matches(s) for s in schema['oneOf'])!=1:fail('ONE_OF')
    if 'not' in schema and matches(schema['not']):fail('NOT')
    if 'if' in schema:
        target='then' if matches(schema['if']) else 'else'
        if target in schema:validate_shape(schema[target],value,path)

KNOWN_ADAPTERS = {
 'core.static-caption.v1','ui4.static-caption.v1','core.current-model.v1',
 'common.model-label.v1','common.date-caption.v1','ui.chrome.v1',
 'ui.residual-caption.v1','ui.caption.v1','story.choice.visual.v1',
 'story.dictionary-heading.v1','story.annotation-heading.v1','battle.caption.v1',
 'battle.command-cost-title.v1','core.dynamic-caption.v1'}

def check_models(resources,bindings):
    rmap={}
    for r in resources['resources']:
        if r['id'] in rmap:raise ContractError('DUPLICATE_RESOURCE_ID:'+r['id'])
        rmap[r['id']]=r
        if r['kind']=='master_ref' and r['jp']!=r['master_ref']['exact_jp']:
            raise ContractError('MASTER_REF_SOURCE_MISMATCH')
        if r['kind']=='master_ref' and r.get('markup_policy','exact-tags')!='exact-tags':
            raise ContractError('MASTER_REF_MARKUP_POLICY')
        if r['kind']!='master_ref' and r['disposition']=='keep_source' and r.get('ko')!=r['jp']:
            raise ContractError('KEEP_SOURCE_CHANGED')
        # Only a finite, explicit policy permits the three accepted caption
        # spacing transforms. Other markup and placeholders stay identical.
        markup={};placeholders={};newlines={}
        for field in ['jp','ko']:
            if field not in r:continue
            stack=[];tokens=[]
            for m in re.finditer(r'<(/?)([A-Za-z][A-Za-z0-9]*)([^>]*)>',r[field]):
                closing,tag,tail=m.groups()
                tokens.append(m.group())
                if tag.lower() in {'br','sprite','space','page','pos','voffset','alpha'}:continue
                if closing:
                    if not stack or stack.pop()!=tag:raise ContractError('TAG_BALANCE:'+r['id'])
                elif not tail.endswith('/'):stack.append(tag)
            if stack:raise ContractError('TAG_BALANCE:'+r['id'])
            plain=re.sub(r'<(/?)([A-Za-z][A-Za-z0-9]*)([^>]*)>','',r[field])
            if '<' in plain or '>' in plain:raise ContractError('MALFORMED_TAG:'+r['id'])
            braces=re.findall(r'\{[^{}]*\}',plain)
            if '{' in re.sub(r'\{[^{}]*\}','',plain) or '}' in re.sub(r'\{[^{}]*\}','',plain):raise ContractError('MALFORMED_TEMPLATE:'+r['id'])
            markup[field]=tokens;placeholders[field]=braces
            newlines[field]=re.findall(r'\r\n|\r|\n',plain)
        if 'ko' in r:
            a,b=markup['jp'],markup['ko']
            if r.get('markup_policy')=='omit-cspace-v1':
                if r['role'] not in {'character_palette','character_imaju_label','character_memory'}:raise ContractError('UNSUPPORTED_MARKUP_ROLE')
                a=[t for t in a if not re.fullmatch(r'</?cspace(?:=[^<>]*)?>',t)]
                if any(re.fullmatch(r'</?cspace(?:=[^<>]*)?>',t) for t in b):raise ContractError('CSPACE_POLICY_REQUIRES_OMISSION')
            if a!=b:raise ContractError('TAG_PARITY:'+r['id'])
            if placeholders['jp']!=placeholders['ko']:raise ContractError('TEMPLATE_PARITY:'+r['id'])
            if newlines['jp']!=newlines['ko']:raise ContractError('NEWLINE_PARITY:'+r['id'])
    ids=set();groups={};collisions={};warnings=[]
    for b in bindings['bindings']:
        if b['id'] in ids:raise ContractError('DUPLICATE_BINDING_ID:'+b['id'])
        ids.add(b['id'])
        if b['resource_id'] not in rmap:raise ContractError('DANGLING_RESOURCE:'+b['id'])
        r=rmap[b['resource_id']]
        if b['source_identity']['exact_jp']!=r['jp']:raise ContractError('EXACT_JP_MISMATCH:'+b['id'])
        if r['disposition']!='translate' and b['enabled']:raise ContractError('HOLD_OR_KEEP_SOURCE_ENABLED')
        if (b['source_identity']['kind']=='native-template')!=(r['kind']=='template_label'):
            raise ContractError('TEMPLATE_KIND_MISMATCH')
        s=b['selector']
        producer=b.get('producer')
        if producer is not None:
            if (b['adapter_id'] not in {'core.static-caption.v1','ui4.static-caption.v1'}
                    or b['source_identity']['kind']!='static-asset'
                    or producer.get('family')!='core-literal-v1'):
                raise ContractError('CORE_PRODUCER_ADAPTER')
            if type(producer.get('localizer_type')) is not int or producer['localizer_type']!=0:
                raise ContractError('CORE_LITERAL_TYPE')
            key=producer.get('text_config_key')
            if not isinstance(key,str) or len(key)>128 or any(ord(c)<32 or 127<=ord(c)<=159 for c in key):
                raise ContractError('CORE_LITERAL_METADATA_KEY')
            if 'option_area' in producer:
                area=producer['option_area']
                if s['scope_key']!='gc.option.optionpopup' or area.get('field') not in {'soundAndTextOptionArea','screenSizeOptionArea','downloadOptionArea'}:
                    raise ContractError('OPTION_AREA_TYPED_FIELD')
                if len(area['segments'])!=len(area['indices']):raise ContractError('OPTION_AREA_PATH_BOUND')
        if r.get('markup_policy')=='omit-cspace-v1' and (b['adapter_id']!='ui.caption.v1' or s.get('type')!='caption'):
            raise ContractError('UNSUPPORTED_MARKUP_ADAPTER')
        if 'source_id' in s and s.get('lifetime')!='callback':raise ContractError('SORT_SOURCE_REQUIRES_CALLBACK')
        if b['adapter_id'] not in KNOWN_ADAPTERS:warnings.append('OPTIONAL_ADAPTER_UNAVAILABLE:'+b['adapter_id'])
        if 'action_group' in b:groups.setdefault(b['action_group'],[]).append(b)
        if b['enabled']:
            # A physical slot is resolved later by the adapter; identical
            # logical selectors already identify an ambiguous data declaration.
            key=json.dumps([b['adapter_id'],s,b['source_identity']['exact_jp']],sort_keys=True,ensure_ascii=False)
            collisions.setdefault(key,[]).append(b)
    for gid,members in groups.items():
        if not 2<=len(members)<=16:raise ContractError('COMATCH_MEMBER_BOUND:'+gid)
        shapes=set();identity=set()
        for b in members:
            r=rmap[b['resource_id']]
            if b['adapter_id'] not in {'core.static-caption.v1','ui4.static-caption.v1'} or b['source_identity']['kind']!='static-asset':
                raise ContractError('COMATCH_ADAPTER:'+gid)
            shapes.add(json.dumps([b['adapter_id'],b['selector'],b.get('producer'),r['jp'],r.get('ko'),b['enabled'],
                                   r['role'],r['kind'],r['disposition'],r.get('master_ref'),
                                   r.get('markup_policy','exact-tags')],sort_keys=True,ensure_ascii=False))
            identity.add(json.dumps(b['source_identity']['identity'],ensure_ascii=False))
        if len(shapes)!=1 or len(identity)!=len(members):raise ContractError('COMATCH_CONFLICT:'+gid)
    for members in collisions.values():
        if len(members)>1 and (len({b.get('action_group') for b in members})!=1 or not members[0].get('action_group')):
            raise ContractError('AMBIGUOUS_DUPLICATE:'+members[0]['id'])
    return {'resource_count':len(rmap),'binding_count':len(ids),
            'enabled_count':sum(b['enabled'] for b in bindings['bindings']),
            'comatch_group_count':len(groups),'warnings':sorted(set(warnings))}

def validate_package(root:Path,contracts:Path):
    resources=read_json(root/'ui/resources.json');bindings=read_json(root/'ui/bindings.json')
    validate_shape(read_json(contracts/'ui-resources.schema.json'),resources)
    validate_shape(read_json(contracts/'ui-bindings.schema.json'),bindings)
    for p in (root/'layout').glob('*.json'):
        validate_shape(read_json(contracts/'episode-title-fit.schema.json'),read_json(p))
    return check_models(resources,bindings)
