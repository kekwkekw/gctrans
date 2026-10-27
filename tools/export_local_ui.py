"""Deterministic exact UI export. Authoring bindings are inputs, never runtime dependencies."""
import argparse
import json
import re
from collections import defaultdict
from pathlib import Path

# These fields supply UI labels. This is an authoring-time export, not a runtime
# Master lookup/reservation and not a scan of arbitrary Master descriptions.
MASTER_UI_FIELDS = (
    ('mTextConfig', 'ml_value[]'),
    ('mTutorialPopupDescriptions', 'ml_title[]'),
    ('mAthenesRecordAreas', 'name'),
)


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique)


def export(root):
    reviewed = read(root / "ui/general-exact.reviewed.json")
    resources = read(root / "ui/resources.json")["resources"]
    bindings = read(root / "ui/bindings.json")["bindings"]
    live = {b["resource_id"] for b in bindings if b.get("enabled")}
    groups = defaultdict(list)
    master_pairs = 0
    master = read(root / 'master.json')
    for table, field in MASTER_UI_FIELDS:
        for jp, ko in master.get(table, {}).get(field, {}).items():
            if isinstance(ko, str) and ko.strip() and jp != ko:
                groups[jp].append({'id': table + '.' + field, 'jp': jp, 'ko': ko,
                                   'disposition': 'translate', 'source': 'master_ui'})
                master_pairs += 1
    for r in resources:
        if r.get("jp"):
            groups[r["jp"]].append(r)
    result, resolved = {}, []
    for jp, rows in sorted(groups.items()):
        # Runtime binding activation/kind/role were legacy routing decisions.
        # A reviewed JP/KO text pair is useful without any such runtime binding.
        eligible = [r for r in rows if r.get("disposition") == "translate"
                    and isinstance(r.get("ko"), str) and r["ko"].strip() and r["ko"] != jp]
        if not eligible:
            continue
        variants = {r["ko"] for r in eligible}
        if len(variants) > 1 or any(r.get("disposition") not in ("translate", "keep_jp") for r in rows):
            if jp not in reviewed:
                raise ValueError("Unresolved UI translation conflict (normalize authoring or explicitly review): " + jp)
            resolved.append(jp)
        else:
            result[jp] = eligible[0]["ko"]
    for jp, ko in sorted(reviewed.items()):
        if not isinstance(jp, str) or not jp or not isinstance(ko, str) or not ko.strip() or jp == ko:
            raise ValueError("Invalid reviewed UI pair: " + repr(jp))
        if jp in result and result[jp] != ko:
            resolved.append(jp)
        # Explicit semantic review is authoritative even when an old binding was held.
        result[jp] = ko
    # Outer TMP decoration sometimes differs between copies of the same prefab.
    # Export a plain alias only when both sides have identical wrapper tags.
    # Preserve the exact decorated pair and reject ambiguous aliases.
    wrapper = re.compile(r'^(<([A-Za-z][A-Za-z0-9]*)(?:=[^>]*)?>)(.+)(</\2>)$', re.S)
    aliases = defaultdict(set)
    for jp, ko in list(result.items()):
        while True:
            a, b = wrapper.fullmatch(jp), wrapper.fullmatch(ko)
            if not a or not b or a[1] != b[1] or a[4] != b[4]: break
            jp, ko = a[3], b[3]
            aliases[jp].add(ko)
    for jp, variants in sorted(aliases.items()):
        if jp in reviewed: continue
        if jp in result: variants.add(result[jp])
        if len(variants) != 1: raise ValueError('Unresolved plain UI alias: ' + jp)
        result[jp] = next(iter(variants))
    # Native smoke showed exactly three numbered workshop slots. Expand this finite,
    # reviewed family offline instead of adding a runtime parser or template engine.
    rules = read(root / "ui.templates.json")["rules"]
    workshop = [r for r in rules if r.get("jp") == "工房{value}" and r.get("value_kind") == "integer"]
    if len(workshop) != 1 or workshop[0].get("ko") != "공방{value}":
        raise ValueError("Reviewed workshop template changed")
    for number in range(1, 4):
        jp = workshop[0]["jp"].replace("{value}", str(number))
        ko = workshop[0]["ko"].replace("{value}", str(number))
        if jp in result and result[jp] != ko:
            raise ValueError("Workshop exact conflict: " + jp)
        result[jp] = ko
    coverage = read(root / 'ui/local-coverage.json') if (root / 'ui/local-coverage.json').exists() else None
    if coverage:
        if (coverage.get('schema') != 'gc-local-ui-coverage-v1'
            or len(set(coverage['keys'])) != len(coverage['keys'])
            or not set(coverage['keep_source']).issubset(coverage['keys'])
            or any(re.search(r'[\u3041-\u3096\u30a1-\u30fa\u3400-\u9fff]', k) for k in coverage['keep_source'])):
            raise ValueError('Invalid local UI coverage contract')
        missing = [jp for jp in coverage['keys'] if jp not in result and jp not in coverage['keep_source']]
        if missing: raise ValueError('Local UI coverage missing: ' + repr(missing))
    payload = (json.dumps(result, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    return payload, {"exact_pairs": len(result), "authoring_bindings_preserved": len(bindings),
                     "held_authoring_jp": sorted({r['jp'] for r in resources if r.get('disposition') not in ('translate','keep_jp') and r['jp'] not in result}), "review_resolved_authoring_jp": resolved,
                     "bounded_workshop_exacts": 3, "runtime_bindings": 0,
                     "master_ui_source_pairs": master_pairs,
                     "authoring_pairs_without_live_binding": len({r['jp'] for r in resources if r['id'] not in live and r.get('disposition') == 'translate' and r.get('ko') and r['jp'] != r['ko']}),
                     "local_coverage_keys": len(coverage['keys']) if coverage else 0,
                     "local_coverage_keep_source": len(coverage['keep_source']) if coverage else 0,
                     "local_coverage_missing": [] if coverage else None}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, default=Path(__file__).resolve().parents[1] / "translations/ko")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    payload, report = export(args.package)
    target = args.package / "ui.json"
    if args.check:
        if target.read_bytes() != payload:
            raise SystemExit("ui.json differs; rerun tools/export_local_ui.py")
    else:
        target.write_bytes(payload)
    print(json.dumps(report, ensure_ascii=False))
