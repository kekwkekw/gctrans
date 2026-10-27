"""Local Master keys are coverage authority only; never import its translated values."""
import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from reconcile_master import flatten_master


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('DUPLICATE_JSON_KEY')
            result[key] = value
        return result
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def source_keys(snapshot):
    snapshot = snapshot.get('sourceSnapshot', snapshot)
    return {(row['translation_target']['table'], row['translation_target']['field'], row['jp'])
            for row in snapshot.get('master', []) if row.get('translation_target')}


def plan(baseline, package, snapshot, baseline_ref):
    # Values are deliberately discarded: Chinese translations are not draft inputs.
    authority = set(flatten_master(read(baseline)))
    current = flatten_master(read(package / 'master.json'))
    observed = source_keys(snapshot)
    missing = sorted(authority - current.keys())
    return {'schema': 'gc-local-master-coverage-v1', 'baseline_ref': baseline_ref,
            'baseline_sha256': sha(baseline), 'current_master_sha256': sha(package / 'master.json'),
            'baseline_keys': len(authority), 'current_keys': len(current),
            'covered_keys': len(authority & current.keys()), 'missing_count': len(missing),
            'current_only_keys_preserved': len(current.keys() - authority),
            'observed_missing_count': len(set(missing) & observed),
            'local_only_missing_count': len(set(missing) - observed),
            'new_source_candidates_count': len(observed - authority),
            'status': 'COVERAGE_GAP' if missing else 'BASELINE_KEYS_PRESENT',
            'semantic_review': 'NOT_CLAIMED',
            'missing': [{'table': t, 'field': f, 'jp': jp,
                         'source': 'CURRENT_SOURCE_AND_LOCAL' if (t, f, jp) in observed else 'LOCAL_COVERAGE_JP',
                         'ko': None} for t, f, jp in missing],
            'new_source_candidates': [{'table': t, 'field': f, 'jp': jp}
                                      for t, f, jp in sorted(observed - authority)]}


# [[variable]unit] permits a translated unit (e.g. ターン -> 턴), not a renamed variable.
TOKENS = re.compile(r'<[A-Za-z/][^>]*>|\[\[[A-Za-z0-9_]+\]|\{[^{}]+\}')
NUMBERS = re.compile(r'[0-9０-９]+(?:\.[0-9０-９]+)?')


def validate_draft(proposal, rows):
    wanted = {(r['table'], r['field'], r['jp']) for r in proposal['missing']}
    drafted = {}
    for row in rows:
        key = (row['table'], row['field'], row['jp'])
        ko = row.get('ko')
        if key not in wanted or key in drafted:
            raise ValueError('DRAFT_EXACT_IDENTITY_MISMATCH')
        keep = (row.get('disposition') == 'keep_source' and ko == key[2]
                and not re.search(r'[\u3040-\u30ff\u3400-\u9fff]', key[2].replace('・', '')))
        if not isinstance(ko, str) or not ko.strip() or (ko == key[2] and not keep):
            raise ValueError('DRAFT_TRANSLATION_MISSING')
        if Counter(TOKENS.findall(key[2])) != Counter(TOKENS.findall(ko)):
            raise ValueError('DRAFT_PROTECTED_TOKEN_MISMATCH: ' + str(key[:2]))
        if Counter(NUMBERS.findall(key[2])) != Counter(NUMBERS.findall(ko)):
            raise ValueError('DRAFT_NUMBER_MISMATCH: ' + str(key[:2]))
        if any(key[2].count(c) != ko.count(c) for c in ('[', ']', '\n')):
            raise ValueError('DRAFT_STRUCTURE_MISMATCH: ' + str(key[:2]))
        drafted[key] = ko
    if drafted.keys() != wanted:
        raise ValueError('DRAFT_COVERAGE_INCOMPLETE')
    return drafted


def apply_draft(package, proposal, rows):
    # Draft application is explicitly requested by the operator; this never invents PRO approval.
    if sha(package / 'master.json') != proposal['current_master_sha256']:
        raise ValueError('MASTER_CHANGED_SINCE_PLAN')
    drafted = validate_draft(proposal, rows)
    master = read(package / 'master.json')
    before = flatten_master(master)
    for (t, f, jp), ko in drafted.items():
        if (t, f, jp) in before:
            raise ValueError('EXISTING_TRANSLATION_OVERWRITE')
        master.setdefault(t, {}).setdefault(f, {})[jp] = ko
    after = flatten_master(master)
    assert all(after[k] == v for k, v in before.items())
    save(package / 'master.json', master)
    from build_manifests import build
    build(package, modified=['master.json'])
    return {'status': 'DRAFT_APPLIED', 'added': len(drafted), 'modified': 0, 'removed': 0,
            'master_sha256': sha(package / 'master.json'), 'semantic_review': 'CODEX_DRAFT_NOT_PRO_REVIEWED',
            'native': 'NOT_RUN_BY_THIS_TOOL'}


def reviewed_master(master, proposal, additions, reviews):
    """Validate the entire review transaction before changing any package file."""
    before = flatten_master(master)
    drafted = validate_draft(proposal, additions)
    if drafted.keys() & before.keys():
        raise ValueError('EXISTING_TRANSLATION_OVERWRITE')
    changes = {}
    for row in reviews:
        key = (row['table'], row['field'], row['jp'])
        if key in changes or key not in before or before[key] != row.get('before_ko'):
            raise ValueError('REVIEW_PREIMAGE_MISMATCH')
        status = row.get('review_status', '')
        if status.startswith('HOLD_'):
            if (row.get('ko') != key[2] or not row.get('hold_reason')
                    or not row.get('evidence', {}).get('records')):
                raise ValueError('HOLD_REQUIRES_SOURCE_EVIDENCE_AND_JP')
        elif status == 'CODEX_SEMANTIC_REVIEWED':
            validate_draft({'missing': [row]}, [row])
        else:
            raise ValueError('REVIEW_DISPOSITION_MISSING')
        changes[key] = row['ko']
    result = json.loads(json.dumps(master, ensure_ascii=False))
    for (t, f, jp), ko in {**drafted, **changes}.items():
        result.setdefault(t, {}).setdefault(f, {})[jp] = ko
    after = flatten_master(result)
    assert all(after[k] == v for k, v in before.items() if k not in changes)
    return result, {'added': len(drafted), 'reviewed_existing': len(changes),
                    'modified_existing': sum(before[k] != v for k, v in changes.items()),
                    'held_existing': sum(r.get('review_status', '').startswith('HOLD_') for r in reviews),
                    'removed': 0}


def apply_reviewed(package, proposal, additions, reviews):
    if sha(package / 'master.json') != proposal['current_master_sha256']:
        raise ValueError('MASTER_CHANGED_SINCE_PLAN')
    result, summary = reviewed_master(read(package / 'master.json'), proposal, additions, reviews)
    save(package / 'master.json', result)
    from build_manifests import build
    build(package, modified=['master.json'])
    return dict(summary, status='REVIEW_APPLIED', master_sha256=sha(package / 'master.json'),
                semantic_review='CODEX_REVIEW_NOT_PRO_REVIEW', native='NOT_RUN_BY_THIS_TOOL')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('plan')
    p.add_argument('--baseline', type=Path, required=True)
    p.add_argument('--baseline-ref', required=True)
    p.add_argument('--package', type=Path, required=True)
    p.add_argument('--source-snapshot', type=Path)
    p.add_argument('--output', type=Path, required=True)
    p = sub.add_parser('apply-draft')
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--draft', type=Path, required=True)
    p.add_argument('--package', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p = sub.add_parser('apply-reviewed')
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--draft', type=Path, required=True)
    p.add_argument('--reviews', type=Path, required=True)
    p.add_argument('--package', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.command == 'plan':
        result = plan(args.baseline, args.package, read(args.source_snapshot) if args.source_snapshot else {}, args.baseline_ref)
    elif args.command == 'apply-draft':
        result = apply_draft(args.package, read(args.plan), read(args.draft))
    else:
        result = apply_reviewed(args.package, read(args.plan), read(args.draft), read(args.reviews))
    save(args.output, result)
    print(json.dumps({k: v for k, v in result.items() if k not in ('missing', 'new_source_candidates')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
