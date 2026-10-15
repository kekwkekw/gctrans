"""Lane-qualified source comparison and optimistic reviewed changes.

Novel is represented only by references to the existing WB intake/return. No
parser ordinal, universal Novel row, semantic approval or translation API lives here.
"""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

class ReviewConflict(ValueError):
    pass

def source_snapshot_input(value):
    """Accept only the existing bare snapshot or the scanner's explicit envelope.

    This is shape normalization, never new source observation or acceptance.
    """
    if not isinstance(value, dict):
        raise ReviewConflict('SOURCE_SNAPSHOT_CONTRACT_REQUIRED')
    if value.get('schema') == 'gc-ng03-source-snapshot-v1':
        if 'sourceSnapshot' in value:
            raise ReviewConflict('AMBIGUOUS_SOURCE_SNAPSHOT_INPUT')
        snapshot = value
    elif 'sourceSnapshot' in value and isinstance(value.get('state'), dict) and isinstance(value.get('metrics'), dict):
        if 'schema' in value or any(key in value for key in ('profile', 'master', 'ui', 'novel')):
            raise ReviewConflict('AMBIGUOUS_SOURCE_SNAPSHOT_INPUT')
        snapshot = value['sourceSnapshot']
        if not isinstance(snapshot, dict) or value['state'].get('profile') != snapshot.get('profile'):
            raise ReviewConflict('SOURCE_ENVELOPE_PROFILE_MISMATCH')
    else:
        raise ReviewConflict('SOURCE_SNAPSHOT_CONTRACT_REQUIRED')
    index(snapshot)
    return snapshot

def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')

def fingerprint(value):
    return hashlib.sha256(encoded(value)).hexdigest()

def row_key(profile, lane, row):
    return fingerprint({'profile':profile, 'lane':lane, 'locator':row['locator']})

def source_fingerprint(profile, lane, row):
    return fingerprint({'profile':profile, 'lane':lane, 'locator':row['locator'],
                        'jp':row['jp'], 'context':row.get('context'), 'structure':row.get('structure'),
                        'translation_target':row.get('translation_target')})

def index(snapshot):
    if snapshot.get('schema') != 'gc-ng03-source-snapshot-v1':
        raise ReviewConflict('SOURCE_SNAPSHOT_CONTRACT_REQUIRED')
    profile = snapshot['profile']
    for field in ('platform','channel','partition','inventory_contract'):
        if not isinstance(profile.get(field), str) or not profile[field]:
            raise ReviewConflict('SOURCE_PROFILE_REQUIRED')
    records, collisions = {}, set()
    for lane in ('master','ui'):
        for row in snapshot.get(lane, []):
            if not isinstance(row.get('jp'), str) or not isinstance(row.get('locator'), dict):
                raise ReviewConflict('SOURCE_ROW_REQUIRED')
            key = row_key(profile, lane, row)
            if key in records:
                collisions.add(key)
            else:
                records[key] = (lane, row)
    for key in collisions: records.pop(key)
    return records, collisions

def compare(previous, current):
    before, old_collisions = index(previous)
    after, new_collisions = index(current)
    if previous['profile']!=current['profile']: raise ReviewConflict('SOURCE_DIFF_PROFILE_MISMATCH')
    changes = []
    for key in sorted(set(before) | set(after) | old_collisions | new_collisions):
        if key in old_collisions | new_collisions:
            changes.append({'key':key,'classification':'INPUT_UNKNOWN_FAILED','reason':'SOURCE_ID_COLLISION'}); continue
        if key not in after:
            # A missing row cannot remove a translation. Even complete inventories
            # produce a review candidate, never a KO deletion operation.
            changes.append({'key':key,'classification':'INPUT_UNKNOWN_FAILED','reason':'SOURCE_ROW_MISSING','automatic_remove':False}); continue
        lane, row = after[key]
        if key not in before:
            classification = 'REVIEW_SOURCE_CONTEXT'
        elif source_fingerprint(previous['profile'],lane,before[key][1]) != source_fingerprint(current['profile'],lane,row):
            classification = 'REVIEW_SOURCE_CONTEXT'
        elif before[key][1] != row:
            classification = 'REPACK_ONLY'
        else:
            classification = 'NO_CHANGE'
        if row.get('adapter_supported') is False:
            classification = 'ADAPTER_REQUIRED'
        changes.append({'key':key, 'lane':lane, 'locator':row['locator'], 'classification':classification,
                        'source_fingerprint':source_fingerprint(current['profile'],lane,row),
                        'automatic_semantic_approval':False})
    return {'schema':'gc-ng03-lane-diff-v1','changes':changes,
            'novel_bridge_refs':[row.get('bridge_ref') for row in current.get('novel',[]) if row.get('bridge_ref')],
            'automatic_ko_delete':False,'automatic_semantic_approval':False}

def find_source(snapshot, reviewed):
    if snapshot.get('metadata',{}).get('status')=='UNKNOWN': raise ReviewConflict('CURRENT_SOURCE_OBSERVATION_UNKNOWN')
    records, collisions = index(snapshot)
    key = row_key(snapshot['profile'], reviewed['lane'], {'locator':reviewed['locator']})
    if key in collisions: raise ReviewConflict('SOURCE_ID_COLLISION')
    if key not in records: raise ReviewConflict('CURRENT_SOURCE_REQUIRED')
    lane, row = records[key]
    if source_fingerprint(snapshot['profile'], lane, row) != reviewed['source_fingerprint']:
        raise ReviewConflict('STALE_SOURCE_OR_CONTEXT')
    return row

def resolve_target(doc, target):
    if target['file'] == 'ui/resources.json':
        matches = [row for row in doc['resources'] if row['id'] == target['resource_id']]
        if len(matches) != 1: raise ReviewConflict('RESOURCE_TARGET_REQUIRED')
        return matches[0], 'ko'
    keys = target.get('keys')
    if not isinstance(keys,list) or not keys or any(not isinstance(key,str) for key in keys):
        raise ReviewConflict('EXACT_LEGACY_TARGET_KEYS_REQUIRED')
    parent = doc
    for key in keys[:-1]:
        if not isinstance(parent,dict) or key not in parent: raise ReviewConflict('TARGET_MISSING')
        parent = parent[key]
    if not isinstance(parent,dict): raise ReviewConflict('TARGET_MISSING')
    return parent, keys[-1]

def validate_master_target(source,target):
    if target['file'] not in {'master.json','names.json'}: raise ReviewConflict('MASTER_TARGET_ROLE_MISMATCH')
    # Names is a speaker/person catalog, not an interchangeable JP dictionary.
    # Current Master extractors establish table/body fields only. A qualified
    # Names adapter must be supplied before this lane can write that catalog.
    if target['file'] == 'names.json':
        raise ReviewConflict('NAMES_QUALIFIED_SOURCE_ADAPTER_REQUIRED')
    keys = target.get('keys', [])
    if not keys or keys[-1] != source['jp']: raise ReviewConflict('MASTER_EXACT_JP_TARGET_REQUIRED')
    table,field=source['locator'].get('table'),source['locator'].get('field')
    mapped=source.get('translation_target')
    if mapped is not None:
        structure=source.get('structure',{})
        if structure.get('mapping_contract')=='MasterDataTranslator-field-grammar-v1':
            from contract_validation import read_json
            selectors=read_json(Path(__file__).resolve().parents[2]/'contracts/master-source-fields-v1.json')
            allowed={(item['table'],item['runtime_field']) for item in selectors['fields']}
            runtime=mapped.get('field') if isinstance(mapped,dict) else None
            if not isinstance(runtime,str) or mapped!={'table':table,'field':runtime} or (table,runtime) not in allowed:
                raise ReviewConflict('UNSUPPORTED_MASTER_FIELD_MAPPING')
            array,delimited=runtime.endswith('[]'),runtime.endswith('|')
            physical=runtime[:-2] if array else runtime[:-1] if delimited else runtime
            if field!=physical or structure.get('physical_field')!=(physical+'[0]' if array else physical) or structure.get('field_type')!=('ml-array' if array else 'delimited-string' if delimited else 'string'):
                raise ReviewConflict('UNSUPPORTED_MASTER_FIELD_MAPPING')
            if array and (type(structure.get('jp_index')) is not int or structure['jp_index']!=0):
                raise ReviewConflict('UNSUPPORTED_MASTER_FIELD_MAPPING')
            if delimited and (structure.get('delimiter')!='|' or not isinstance(structure.get('suffix'),str)):
                raise ReviewConflict('UNSUPPORTED_MASTER_FIELD_MAPPING')
        elif (table not in {'mUnitWords','mSubunitWords'} or field!='ml_word' or
              mapped!={'table':table,'field':'ml_word[]'} or structure.get('physical_field')!='ml_word[0]' or
              structure.get('mapping_contract')!='upstream-master-ml-word-zero-to-runtime-array-v1'):
            raise ReviewConflict('UNSUPPORTED_MASTER_FIELD_MAPPING')
        field=mapped['field']
    if target['file'] == 'master.json' and keys[:2] != [table,field]:
        raise ReviewConflict('MASTER_TABLE_FIELD_TARGET_MISMATCH')
    return table,field

def apply_row(doc, reviewed, snapshot):
    decision = reviewed.get('decision')
    if decision in {'HOLD','KEEP_SOURCE','KNOWN_RESIDUAL'}:
        return False
    if decision != 'APPROVE': raise ReviewConflict('EXPLICIT_REVIEW_DECISION_REQUIRED')
    source = find_source(snapshot, reviewed)
    target = reviewed['target']
    if reviewed['lane'] == 'master':
        validate_master_target(source,target)
        peers=[item for item in snapshot.get('master',[]) if item.get('jp')==source['jp'] and item.get('locator',{}).get('table')==source['locator'].get('table') and item.get('locator',{}).get('field')==source['locator'].get('field')]
        if len({fingerprint({'context':item.get('context'),'structure':item.get('structure')}) for item in peers})>1:
            raise ReviewConflict('SHARED_MASTER_JP_CONTEXT_SCOPE_REVIEW_REQUIRED')
    elif reviewed['lane'] == 'ui':
        if target['file'] != 'ui/resources.json' or target.get('resource_id') != source['locator'].get('resource'):
            raise ReviewConflict('UI_RESOURCE_TARGET_MISMATCH')
    else: raise ReviewConflict('NOVEL_REQUIRES_EXISTING_ACCEPTED_RETURN_BRIDGE')
    parent, key = resolve_target(doc, target)
    if target['file'] == 'ui/resources.json':
        if parent.get('jp') != source['jp'] or parent.get('disposition') != 'translate' or parent.get('kind') == 'master_ref':
            raise ReviewConflict('PROTECTED_OR_MASTER_REF_RESOURCE')
        if parent.get('role') != source['locator'].get('role') or parent.get('kind') != source.get('structure',{}).get('kind'):
            raise ReviewConflict('UI_CURRENT_TARGET_ROLE_STRUCTURE_CHANGED')
        if 'old_resource_sha256' in reviewed and fingerprint(parent) != reviewed['old_resource_sha256']:
            raise ReviewConflict('CONCURRENT_RESOURCE_CHANGE')
    if fingerprint(parent.get(key)) != reviewed['old_ko_sha256']:
        raise ReviewConflict('CONCURRENT_KO_CHANGE')
    if not isinstance(reviewed.get('new_ko'),str): raise ReviewConflict('REVIEWED_KO_STRING_REQUIRED')
    if parent.get(key) == reviewed['new_ko']:
        return False
    parent[key] = reviewed['new_ko']
    return True

def validate_binding_provenance(source, proposal, resource):
    locator, structure = source['locator'], source.get('structure',{})
    selector = proposal.get('selector',{})
    identity = proposal.get('source_identity',{})
    if (proposal.get('id') != locator.get('binding') or
        proposal.get('resource_id') != locator.get('resource') or
        selector.get('scope_key') != locator.get('owner') or
        selector.get('slot_key') != locator.get('slot') or
        resource.get('role') != locator.get('role') or resource.get('jp') != source['jp'] or
        identity.get('exact_jp') != source['jp']):
        raise ReviewConflict('BINDING_SOURCE_OWNER_SLOT_ROLE_MISMATCH')
    # Selector and identity are executable provenance. A JP match alone cannot
    # authorize changing a callback, ancestry, owner, slot or current-model token.
    if (not structure.get('adapter_id') or structure.get('adapter_id') != proposal.get('adapter_id') or
        structure.get('selector') != selector or structure.get('source_identity') != identity):
        raise ReviewConflict('QUALIFIED_BINDING_STRUCTURE_REQUIRED')
