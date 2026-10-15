"""Create disposable full-package changes from a normal prior release/index.

No import receipt, archive or historical absolute custody path is required.
"""
from pathlib import Path
import argparse
import json
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from build_manifests import build, encode
from snapshot_descriptor import descriptor
from semantic_diff import diff


def prepare(candidate: Path, output: Path, report: Path):
    candidate, output = candidate.resolve(), output.resolve()
    if candidate == output or candidate in output.parents or output.exists():
        raise ValueError('DISPOSABLE_DESTINATION_MUST_BE_NEW_AND_OUTSIDE_CANDIDATE')
    correction, addition = output / 'full-ko-correction', output / 'full-binding-addition'
    output.mkdir(parents=True)
    shutil.copytree(candidate, correction)
    resources = json.loads((correction / 'ui/resources.json').read_text(encoding='utf-8-sig'))
    bindings = json.loads((correction / 'ui/bindings.json').read_text(encoding='utf-8-sig'))
    resource = next(r for r in resources['resources'] if 'UI13-K001' in r['id'])
    before = resource['ko']
    resource['ko'] = before + ' [검증]'
    (correction / 'ui/resources.json').write_bytes(encode(resources))
    build(correction)
    prior = descriptor(candidate, mode='verified_immutable_inventory')
    corrected = descriptor(correction, mode='verified_immutable_inventory')
    delta = diff(candidate, correction, mode='trusted', old_descriptor=prior,
                 new_descriptor=corrected, changeset={'add': [], 'modify': ['ui/resources.json'], 'remove': []})
    if delta['classification'] != 'text_only' or delta.get('novel_payload_reads', 0) != 0:
        raise ValueError('UNEXPECTED_CORRECTION_SEMANTICS')
    shutil.copytree(correction, addition)
    added_bindings = json.loads((addition / 'ui/bindings.json').read_text(encoding='utf-8-sig'))
    old = next(b for b in added_bindings['bindings'] if b['resource_id'] == resource['id'] and b['enabled'])
    new = dict(old)
    old['enabled'] = False
    new['id'] = 'binding.ng02.supported-caption.addition'
    added_bindings['bindings'].append(new)
    (addition / 'ui/bindings.json').write_bytes(encode(added_bindings))
    build(addition)
    result = dict(schema='gc-ng02-full-candidate-json-fixtures-v1', status='PASS_PREPARATION',
                  resource_id=resource['id'], binding_id=old['id'], before=before, after=resource['ko'],
                  addition_binding_id=new['id'], supported_shape_identical_to=old['id'],
                  addition_scope_expansion=False, source_root=str(candidate), correction_root=str(correction),
                  addition_root=str(addition), diff=delta, native='NOT_RUN', game='NOT_RUN')
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_bytes(encode(result))
    print(json.dumps(dict(status=result['status'], correction=str(correction), addition=str(addition))))


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('candidate', type=Path)
    p.add_argument('output', type=Path)
    p.add_argument('--report', type=Path, required=True)
    a = p.parse_args()
    prepare(a.candidate, a.output, a.report)
