"""Deterministic exact UI export. Authoring bindings are inputs, never runtime dependencies."""
import argparse
import calendar
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
    # Auto-deck and resonance prefabs also contain static copies of these labels.
    ('mVirtueEffects', 'ml_name[]'),
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
    # Complete, finite labels composed by the observed detail menus. Expand at
    # authoring time so the installed 7.1.1 exact consumer needs no new parser.
    detail = root / 'ui/detail-families.json'
    detail_pairs = {}
    if detail.exists():
        spec = read(detail)
        if spec.get('schema') != 'gc-ui-detail-families-v1':
            raise ValueError('Detail family schema')
        def add(jp, ko):
            if jp in result and result[jp] != ko:
                raise ValueError('Detail exact conflict: ' + jp)
            detail_pairs[jp] = ko
        for group in spec['memory_groups']:
            add(f'グループ{group}', f'그룹{group}')
            for slot in spec['memory_slots']:
                add(f'設定{group}-{slot}', f'세트{group}-{slot}')
        # Use the current Master stage names; no hardcoded chapter count or
        # translating arbitrary strings that merely contain a stage name.
        chapters = defaultdict(dict)
        for jp, ko in master.get('mQuestStages', {}).get('ml_name[]', {}).items():
            match = re.fullmatch(r'メインステージ([0-9]+)-([0-9]+)', jp)
            if match:
                chapters[int(match[1])][int(match[2])] = (jp, ko)
        for stages in chapters.values():
            first, last = min(stages), max(stages)
            original = spec['rank_jp'].replace('{0}', stages[first][0]).replace('{1}', str(last))
            korean = result.get(original, spec['rank_ko'].replace('{0}', stages[first][1]).replace('{1}', str(last)))
            for name in set(stages[first]):
                add(spec['rank_jp'].replace('{0}', name).replace('{1}', str(last)),
                    korean)
        for month in range(1, 13):
            for day in range(1, calendar.monthrange(2000, month)[1] + 1):
                add(spec['birthday_jp'].replace('{0:D2}', f'{month:02d}').replace('{1:D2}', f'{day:02d}'),
                    spec['birthday_ko'].replace('{0:D2}', f'{month:02d}').replace('{1:D2}', f'{day:02d}'))
        if spec.get('profile_percentages'):
            # Percentages are a finite domain; no runtime substring replacement.
            for number in range(101):
                add(f'上位{number}%', f'상위 {number}%')
                add(f'{number}%以下', f'{number}% 이하')
                add(f'上位{number}%以下', f'상위 {number}% 이하')
                add(f'全体上位{number}%', f'전체 상위 {number}%')
                add(f'Rank内上位{number}%', f'등급 내 상위 {number}%')
        # The Native ranking filter exposes ten classes, and the table explicitly
        # displays the top 100. Expand those observed domains, not arbitrary names.
        for rank in spec.get('ranking_classes', []):
            add(f'Rank{rank}級', f'랭크 {rank}등급')
            add(f'Rank{rank}級内順位', f'랭크 {rank}등급 내 순위')
            for number in range(101):
                add(f'Rank{rank}級/上位{number}%', f'랭크 {rank}등급/상위 {number}%')
                add(f'Rank{rank}級/上位{number}%以下', f'랭크 {rank}등급/상위 {number}% 이하')
        if spec.get('ranking_classes'):
            for number in range(1, 101):
                add(f'{number}位', f'{number}위')
        if spec.get('equipment_exclusive_names'):
            # The item/deck producer concatenates either the original or already
            # translated unit name with 専用 after the Master hook has finished.
            for jp, ko in master.get('mUnits', {}).get('ml_name[]', {}).items():
                for name in {jp, ko}:
                    add(name + '専用', ko + ' 전용')
        result.update(detail_pairs)
    # Review finite enhancement controls as families, including result/confirmation
    # states. Expand complete strings offline for the existing exact consumer.
    enhancement_pairs = {}
    enhancement = root / 'ui/enhancement-families.json'
    if enhancement.exists():
        spec = read(enhancement)
        if spec.get('schema') != 'gc-ui-enhancement-families-v1':
            raise ValueError('Enhancement family schema')
        for family in spec['families']:
            values = family['values']
            if (not values or len(values) > 1000 or len(set(values)) != len(values)
                or any(type(v) is not int or v < 0 or v > 999 for v in values)
                or family['jp'].count('{0}') != 1 or family['ko'].count('{0}') != 1):
                raise ValueError('Enhancement family domain')
            for value in values:
                jp = family['jp'].replace('{0}', str(value))
                ko = family['ko'].replace('{0}', str(value))
                if jp in result and result[jp] != ko:
                    raise ValueError('Enhancement exact conflict: ' + jp)
                if jp in enhancement_pairs and enhancement_pairs[jp] != ko:
                    raise ValueError('Enhancement family overlap: ' + jp)
                enhancement_pairs[jp] = ko
        result.update(enhancement_pairs)
    # Museum timers and AP recovery use a small, explicitly bounded exact set.
    # Reuse current Korean wording; the installed DLL needs no new parser.
    ap_pairs = {}
    ap = root / 'ui/ap-families.json'
    if ap.exists():
        spec = read(ap)
        def bounded_range(name, ceiling):
            bounds = spec.get(name)
            if (not isinstance(bounds, list) or len(bounds) != 2
                or any(type(n) is not int for n in bounds)
                or not 0 <= bounds[0] <= bounds[1] <= ceiling):
                raise ValueError('AP family domain: ' + name)
            return range(bounds[0], bounds[1] + 1)
        if spec.get('schema') != 'gc-ui-ap-families-v1':
            raise ValueError('AP family schema')
        minutes = bounded_range('timer_minutes', 24 * 60)
        amounts = bounded_range('ap_recovery_amounts', 999)
        update = [r for r in rules if r.get('jp') == '更新まで{value}' and r.get('value_kind') == 'clock']
        recovery_jp = '回数回復までhh:mm'
        amount_jp = 'APを{0:#,0}回復しました。'
        recovery_ko = reviewed.get(recovery_jp)
        amount_ko = master.get('mTextConfig', {}).get('ml_value[]', {}).get(amount_jp)
        if (len(update) != 1 or not isinstance(update[0].get('ko'), str)
            or update[0]['ko'].count('{value}') != 1
            or not isinstance(recovery_ko, str) or recovery_ko.count('hh:mm') != 1
            or not isinstance(amount_ko, str) or amount_ko.count('{0:#,0}') != 1):
            raise ValueError('AP family translation source')
        def add_ap(jp, ko):
            if jp in result and result[jp] != ko:
                raise ValueError('AP exact conflict: ' + jp)
            if jp in ap_pairs and ap_pairs[jp] != ko:
                raise ValueError('AP family overlap: ' + jp)
            ap_pairs[jp] = ko
        for minute in minutes:
            clock = f'{minute // 60:02d}:{minute % 60:02d}'
            add_ap(update[0]['jp'].replace('{value}', clock), update[0]['ko'].replace('{value}', clock))
            add_ap(recovery_jp.replace('hh:mm', clock), recovery_ko.replace('hh:mm', clock))
        for amount in amounts:
            add_ap(amount_jp.replace('{0:#,0}', str(amount)), amount_ko.replace('{0:#,0}', str(amount)))
        result.update(ap_pairs)
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
                     "bounded_detail_exacts": len(detail_pairs),
                     "bounded_enhancement_exacts": len(enhancement_pairs),
                     "bounded_ap_exacts": len(ap_pairs),
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
