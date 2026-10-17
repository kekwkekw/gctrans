"""The same finite producer corpus as the candidate-library CPU executable; no native proof."""
from pathlib import Path
import copy
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from contract_validation import ContractError, read_json, validate_shape, check_models

schema = read_json(ROOT / 'contracts/ui-bindings.schema.json')
resources = read_json(ROOT / 'translations/ko/ui/resources.json')
bindings = read_json(ROOT / 'translations/ko/ui/bindings.json')
validate_shape(schema, bindings)
real_result = check_models(resources, bindings)
base = next(b for b in bindings['bindings'] if b['id'].startswith('binding.core.UI4-S001.'))
resource = next(r for r in resources['resources'] if r['id'] == base['resource_id'])
results = []
for case in read_json(ROOT / 'tests/ComprehensiveCoreTests/producer_cases.json'):
    b = copy.deepcopy(base)
    b['adapter_id'] = case['adapter']
    b['source_identity']['kind'] = case['source_kind']
    b['selector']['scope_key'] = case['scope']
    b['producer'] = case['producer']
    b.pop('action_group', None)
    envelope = {'schema_version': 1, 'bindings': [b]}
    try:
        validate_shape(schema, envelope)
        check_models({'resources': [resource]}, envelope)
        valid, reason = True, None
    except ContractError as error:
        valid, reason = False, str(error)
    results.append({'name': case['name'], 'status': 'PASS' if valid == case['valid'] else 'FAIL', 'accepted': valid, 'reason': reason})
print(json.dumps({'fixture': 'shared_producer_schema_semantic_corpus_not_native', 'real_data': real_result,
    'count': len(results), 'failed': sum(r['status'] != 'PASS' for r in results), 'tests': results}, indent=2))
raise SystemExit(1 if any(r['status'] != 'PASS' for r in results) else 0)
