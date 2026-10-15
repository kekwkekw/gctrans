"""Finite Master leaf reconciliation and changed-Master preservation gate.

Authority is scoped to (table, field grammar, exact Japanese source). No
translation counts, historical hashes, or file-wide main/old priority are used
by the reusable product gate. The CLI's hash is a one-time restoration input.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import uuid


class MasterReconciliationError(ValueError):
    pass


def flatten_master(master):
    if not isinstance(master, dict):
        raise MasterReconciliationError('MASTER_OBJECT_REQUIRED')
    out = {}
    for table, fields in master.items():
        if not isinstance(table, str) or not table or not isinstance(fields, dict):
            raise MasterReconciliationError('MASTER_TABLE_CONTRACT')
        for field, entries in fields.items():
            if not isinstance(field, str) or not field or not isinstance(entries, dict):
                raise MasterReconciliationError('MASTER_FIELD_CONTRACT')
            for jp, ko in entries.items():
                if not isinstance(jp, str) or not jp or not isinstance(ko, str):
                    raise MasterReconciliationError('MASTER_LEAF_CONTRACT')
                out[(table, field, jp)] = ko
    return out


def observed_scope(master):
    return {'tables': len(master), 'fields': sum(len(fields) for fields in master.values()),
            'entries': len(flatten_master(master))}


def _record(key, old, new):
    return {'table': key[0], 'field': key[1], 'exact_jp': key[2],
            'old_ko': old, 'new_ko': new}


def master_delta(before, after):
    left, right = flatten_master(before), flatten_master(after)
    return {'removed': [_record(k, left[k], None) for k in sorted(left.keys()-right.keys())],
            'modified': [_record(k, left[k], right[k]) for k in sorted(left.keys() & right.keys()) if left[k] != right[k]],
            'added': [_record(k, None, right[k]) for k in sorted(right.keys()-left.keys())]}


def active_master_refs(resources, bindings):
    """Only current enabled translation bindings impose dependencies."""
    catalog = {r['id']: r for r in resources.get('resources', [])}
    refs = {}
    for binding in bindings.get('bindings', []):
        if not binding.get('enabled'):
            continue
        resource = catalog.get(binding.get('resource_id'))
        if resource and resource.get('kind') == 'master_ref' and resource.get('disposition') == 'translate':
            r = resource['master_ref']
            key = (r['table'], r['field'], r['exact_jp'])
            refs.setdefault(key, []).append(resource['id'])
    return refs


def verify_master_delta(before, after, *, approved_changes=(), resources=None, bindings=None):
    """Reject unreviewed removal/term changes; allow exact explicitly reviewed edits.

    The caller passes only accepted operations from its existing review path,
    then invokes this once when Master actually changed. UI-only applies do not
    invoke or scan this gate. approved_changes records carry exact old/new KO.
    Additions remain available to the existing source-qualified review checks.
    """
    delta = master_delta(before, after)
    approvals = {}
    for item in approved_changes:
        if not isinstance(item, dict) or set(item) != {'table', 'field', 'exact_jp', 'old_ko', 'new_ko'}:
            raise MasterReconciliationError('MASTER_APPROVAL_CONTRACT')
        key = (item['table'], item['field'], item['exact_jp'])
        if key in approvals:
            raise MasterReconciliationError('MASTER_DUPLICATE_APPROVAL')
        approvals[key] = item
    unauthorized = []
    for item in delta['removed'] + delta['modified']:
        key = (item['table'], item['field'], item['exact_jp'])
        if approvals.get(key) != item:
            unauthorized.append(item)
    if unauthorized:
        error = MasterReconciliationError('MASTER_UNREVIEWED_REMOVAL_OR_MODIFICATION')
        error.details = unauthorized
        raise error
    refs = active_master_refs(resources or {}, bindings or {})
    before_leaves, after_leaves = flatten_master(before), flatten_master(after)
    unresolved = [_record(k, before_leaves.get(k), after_leaves.get(k)) | {'resource_ids': ids}
                  for k, ids in refs.items() if not after_leaves.get(k)]
    if unresolved:
        error = MasterReconciliationError('MASTER_ACTIVE_REF_UNRESOLVED')
        error.details = unresolved
        raise error
    return {'schema': 'gc-ng04-master-change-gate-v1', 'status': 'PASS',
            'removed': len(delta['removed']), 'modified': len(delta['modified']),
            'added': len(delta['added']), 'active_master_refs_checked': len(refs),
            'gate_scope': 'changed Master only; exact leaf delta and current enabled refs',
            'permanent_cardinality_gate': False}


def reconcile_master(accepted, current, *, baseline=None, accepted_authority=()):
    """Merge independent leaf work and report only real conflicting leaf keys.

    Without a known common baseline, differing common values need explicit
    accepted authority. Missing accepted leaves are retained; current-only
    additions are retained. A missing current key never revokes acceptance.
    With a baseline, current-only and accepted-only changes are merged. A
    current deletion of an accepted leaf is reported, never silently applied.
    Conflicts retain the accepted value until a scoped decision is available.
    """
    a, c = flatten_master(accepted), flatten_master(current)
    b = flatten_master(baseline) if baseline is not None else None
    authority = set(tuple(k) for k in accepted_authority)
    merged = dict(a)
    conflicts, kept_current, restored = [], [], []
    missing = object()
    for key in sorted(a.keys() | c.keys()):
        av, cv = a.get(key, missing), c.get(key, missing)
        if av == cv:
            continue
        if key in authority and av is not missing:
            restored.append(_record(key, None if cv is missing else cv, av))
            continue
        if av is missing:
            merged[key] = cv
            kept_current.append(_record(key, None, cv))
            continue
        if cv is missing:
            if b is not None and key in b:
                reason = 'CURRENT_DELETION_OF_ACCEPTED_LEAF' if av == b[key] else 'BOTH_CHANGED'
                conflicts.append(_record(key, av, None) | {'reason': reason})
            else:
                restored.append(_record(key, None, av))
            continue
        if b is not None:
            bv = b.get(key, missing)
            if av == bv:
                merged[key] = cv
                kept_current.append(_record(key, av, cv))
                continue
            if cv == bv:
                restored.append(_record(key, cv, av))
                continue
        conflicts.append(_record(key, av, cv) | {'reason': 'BOTH_CHANGED' if b is not None else 'COMMON_ANCESTOR_REQUIRED'})
    result = copy.deepcopy(accepted)
    for (table, field, jp), ko in merged.items():
        result.setdefault(table, {}).setdefault(field, {})[jp] = ko
    return result, {'schema': 'gc-ng04-master-leaf-reconciliation-v1',
                    'status': 'PARTIAL_CONFLICTS' if conflicts else 'PASS',
                    'authority': 'lane/table/field/exact-JP; no blanket file priority',
                    'accepted_observed_scope': observed_scope(accepted),
                    'current_observed_scope': observed_scope(current),
                    'result_observed_scope': observed_scope(result),
                    'current_changes_retained': kept_current, 'accepted_leaves_restored': restored,
                    'conflicts': conflicts, 'new_semantic_acceptance_created': False}


def reconcile_candidate_lanes(accepted_master, current_root: Path, output: Path, *, baseline_master=None, accepted_authority=()):
    """First-integration candidate tool: merge Master leaves, carry other lanes.

    The output is a new uninstalled candidate. Novel/Names/UI/layout are copied
    from current repository bytes and their inventory/registration is retained.
    Only a completed disposable staging directory is published locally.
    """
    from build_manifests import build, encode, previous_inventory
    from contract_validation import read_json
    current_root, output = current_root.resolve(), output.resolve()
    if output.exists() or output.is_relative_to(current_root) or current_root.is_relative_to(output):
        raise MasterReconciliationError('RECONCILIATION_OUTPUT_MUST_BE_NEW_AND_SEPARATE')
    before_entries, _ = previous_inventory(current_root)
    original_manifest = read_json(current_root/'manifest.json')
    current_master = read_json(current_root/'master.json')
    merged, report = reconcile_master(accepted_master, current_master, baseline=baseline_master, accepted_authority=accepted_authority)
    output.parent.mkdir(parents=True, exist_ok=True)
    staging = output.with_name('.ng04-reconcile-'+uuid.uuid4().hex)
    try:
        shutil.copytree(current_root, staging)
        if merged != current_master:
            (staging/'master.json').write_bytes(encode(merged))
            build(staging, modified=['master.json'])
        after_entries, _ = previous_inventory(staging)
        unexpected = [p for p in before_entries if p not in {'master.json','manifest.json','legacy-components.json'} and before_entries[p] != after_entries.get(p)]
        if unexpected or read_json(staging/'manifest.json')['novels'] != original_manifest['novels']:
            raise MasterReconciliationError('RECONCILIATION_NON_MASTER_LANE_CHANGED')
        # Initial integration inspects current refs even if Master leaf conflicts
        # are held. The existing UI contract remains separately validated.
        merged_leaves = flatten_master(merged)
        unresolved = [list(key) for key in active_master_refs(read_json(staging/'ui/resources.json'),read_json(staging/'ui/bindings.json')) if not merged_leaves.get(key)]
        if unresolved:
            raise MasterReconciliationError('RECONCILIATION_ACTIVE_REF_UNRESOLVED')
        os.replace(staging, output)
        report.update({'output':str(output),'non_master_lane_inventory_changes':unexpected,
                       'novel_registration':'UNCHANGED','native':'NOT_RUN','game':'NOT_RUN',
                       'install':'NOT_PERFORMED'})
        return report
    finally:
        if staging.exists():
            shutil.rmtree(staging)


def restore_candidate(candidate: Path, accepted_path: Path, plan_path: Path, report_path: Path):
    """One-time exact restoration to the new candidate only; metadata via builder."""
    from build_manifests import build, previous_inventory, legacy_hash
    from contract_validation import read_json
    candidate = candidate.resolve()
    accepted_path = accepted_path.resolve()
    if accepted_path.is_relative_to(candidate):
        raise MasterReconciliationError('RESTORATION_SOURCE_MUST_BE_SEPARATE')
    plan = read_json(plan_path)
    raw = accepted_path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != plan['equivalent_available_source']['sha256'] or digest != plan['source']['sha256']:
        raise MasterReconciliationError('RESTORATION_SOURCE_IDENTITY')
    before_entries, _ = previous_inventory(candidate)
    before = read_json(candidate/'master.json')
    accepted = read_json(accepted_path)
    # An interrupted payload copy may already have accepted bytes with stale
    # descriptors. Only a fully coherent restoration is a no-op.
    if ((candidate/'master.json').read_bytes() == raw
            and before_entries['master.json']['sha256'] == digest
            and read_json(candidate/'manifest.json').get('master') == legacy_hash(accepted)):
        return {'schema':'gc-ng04-accepted-master-restoration-v1','status':'NO_CHANGE',
                'candidate_master_sha256':digest,'reason':'accepted exact bytes already restored; original restoration evidence preserved',
                'native':'NOT_RUN','game':'NOT_RUN'}
    # Plan explicitly authorizes restoration of the two documented term regressions.
    # Any new candidate-only leaf/conflict beyond the submitted plan is held.
    delta = master_delta(before, accepted)
    if delta['removed']:
        raise MasterReconciliationError('NEW_CURRENT_MASTER_LEAVES_REQUIRE_RECONCILIATION')
    if hashlib.sha256((candidate/'master.json').read_bytes()).hexdigest() != plan['current_candidate_master_sha256']:
        if before != accepted:
            raise MasterReconciliationError('CANDIDATE_CHANGED_RECONCILE_LEAVES_FIRST')
    preserved = {p: (candidate/p).read_bytes() for p in ['names.json','ui/resources.json','ui/bindings.json','layout/episode-title.json']}
    registered = read_json(candidate/'manifest.json')['novels']
    (candidate/'master.json').write_bytes(raw)
    build(candidate, modified=['master.json'])
    after_entries, _ = previous_inventory(candidate)
    unexpected = [p for p in before_entries if p not in {'master.json','manifest.json','legacy-components.json'} and before_entries[p] != after_entries.get(p)]
    if unexpected or any((candidate/p).read_bytes() != value for p, value in preserved.items()) or read_json(candidate/'manifest.json')['novels'] != registered:
        raise MasterReconciliationError('NON_TARGET_LANE_CHANGED')
    resources, bindings = read_json(candidate/'ui/resources.json'), read_json(candidate/'ui/bindings.json')
    gate = verify_master_delta(before, accepted, approved_changes=delta['modified'], resources=resources, bindings=bindings)
    report = {'schema':'gc-ng04-accepted-master-restoration-v1', 'status':'PASS',
              'accepted_source_sha256':digest, 'candidate_master_sha256':hashlib.sha256((candidate/'master.json').read_bytes()).hexdigest(),
              'before_scope':observed_scope(before), 'after_scope':observed_scope(accepted),
              'restored_entry_count':len(delta['added']), 'restored_term_changes':delta['modified'],
              'novel_inventory_and_registration':'UNCHANGED', 'names_ui_layout_bytes':'UNCHANGED',
              'non_target_inventory_changes':unexpected, 'master_change_gate':gate,
              'archive_preservation':'SEPARATE_ROOT_AUDIT_REQUIRED', 'new_semantic_acceptance_created':False,
              'counts_are_observations_not_runtime_gate':True, 'native':'NOT_RUN', 'game':'NOT_RUN'}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('candidate', type=Path)
    p.add_argument('--accepted', type=Path, required=True)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--report', type=Path, required=True)
    a = p.parse_args()
    print(json.dumps(restore_candidate(a.candidate, a.accepted, a.plan, a.report), ensure_ascii=False))
