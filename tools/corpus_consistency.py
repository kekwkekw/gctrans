"""Current-only corpus index and explicitly reviewed exact corrections. No model or Git writes."""
from collections import Counter, defaultdict
from contextlib import ExitStack
import argparse
import hashlib
import json
import re
from pathlib import Path
import shutil
import subprocess
import tempfile

from build_manifests import build, encode
from contract_validation import read_json
from export_local_ui import export
from novel_workflow import (META_FILES, _checked_changes, _publish, check_text,
                            encode_novel_payload, file_hash, require)
from translation_knowledge import (change_history, check_context, dump_knowledge,
                                   load_current, translation_context)
from update.process_lock import ProcessFileLock
from update.local_parity import GUARDED, validate_translation

ROOT = Path(__file__).resolve().parents[1]
# Same bounded title alias as sync_novel_titles.py; never modify the exact JP key.
TITLE_WIDTH = str.maketrans('０１２３４５６７８９　', '0123456789 ')
# Exact reviewed numeral renderings, tied to the source Novel. These are only
# consulted after the ordinary validator rejects the literal digits.
REVIEWED_NUMERIC_TITLES = {
    '二代目': ('10010802', '2대째'),
    '二代目との接触': ('11011102', '2대째와의 접촉'),
    '３人の出会い': ('11011001', '세 사람의 만남'),
    '３人の少女たち': ('10011006', '세 명의 소녀들'),
}


def _numeric_title_authority(master, ko, live, decisions):
    jp = master['jp']
    expected = REVIEWED_NUMERIC_TITLES.get(jp)
    field = (master.get('table'), master.get('field'))
    allowed = field == ('mQuestStages', 'ml_name[]') or (
        jp == '３人の少女たち' and field in {('mNovels', 'title'), ('mNovels', 'ml_title[]')})
    require(master['kind'] == 'master' and allowed and expected and ko == expected[1], 'NUMBER_MISMATCH')
    matches = [r for r in live.values() if r['kind'] == 'novel' and not r['dialogue'] and r['jp'] == jp]
    require(len(matches) == 1, 'NUMBER_MISMATCH: missing or ambiguous Novel title')
    novel = matches[0]
    decision = decisions.get(novel['id'], {})
    action = decision.get('action', 'keep')
    approved = decision.get('ko_final') if action == 'correct' else novel['ko_current']
    require(novel['source_id'] == expected[0] and not novel['held'] and action in {'correct', 'keep'}
            and approved == ko, 'NUMBER_MISMATCH: Novel title not approved or differs')
    return novel


def sha(value):
    return hashlib.sha256(encode(value)).hexdigest()


def occurrences(repo):
    """Explicit current inputs only. Neither legacy nor provenance is traversed."""
    package = Path(repo) / 'translations/ko'
    rows = []
    hashes = {}

    def add(kind, path, pointer, jp, ko, **extra):
        if not isinstance(jp, str) or not isinstance(ko, str):
            return
        target = {'path': path, 'pointer': pointer, 'jp': jp}
        if path not in hashes:
            hashes[path] = file_hash(package/path)
        rows.append(dict(target, id=sha(target), kind=kind, ko_current=ko,
                         file_sha256=hashes[path], **extra))

    names = read_json(package/'names.json')
    for jp, ko in names.items():
        add('name', 'names.json', [jp], jp, ko)
    master = read_json(package/'master.json')
    title_keys = set(master.get('mNovels', {}).get('title', {})) | set(master.get('mNovels', {}).get('ml_title[]', {}))
    title_keys = {jp.translate(TITLE_WIDTH) for jp in title_keys}
    for table, fields in master.items():
        for field, pairs in fields.items():
            for jp, ko in pairs.items():
                add('master', 'master.json', [table, field, jp], jp, ko, table=table, field=field)
    for path in sorted((package/'novels').glob('*.json')):
        for jp, ko in read_json(path).items():
            add('novel', 'novels/'+path.name, [jp], jp, ko,
                source_id=path.stem, dialogue=jp.translate(TITLE_WIDTH) not in title_keys,
                held=bool(re.search(r'\[[^\]]*\bHOLD\]', ko)))
    for jp, ko in read_json(package/'ui/general-exact.reviewed.json').items():
        add('ui', 'ui/general-exact.reviewed.json', [jp], jp, ko)
    for index, row in enumerate(read_json(package/'ui/resources.json')['resources']):
        if row.get('disposition') == 'translate':
            add('ui_resource', 'ui/resources.json', ['resources', index, 'ko'], row.get('jp'), row.get('ko'),
                resource_id=row['id'])
    for index, row in enumerate(read_json(package/'ui.templates.json')['rules']):
        add('ui_template', 'ui.templates.json', ['rules', index, 'ko'], row.get('jp'), row.get('ko'))
    for path in ('ui/detail-families.json', 'ui/enhancement-families.json'):
        if not (package/path).exists():
            continue
        spec = read_json(package/path)
        for key in ('rank', 'birthday'):
            if key+'_jp' in spec:
                add('ui_family', path, [key+'_ko'], spec[key+'_jp'], spec[key+'_ko'])
        for index, row in enumerate(spec.get('families', [])):
            add('ui_family', path, ['families', index, 'ko'], row.get('jp'), row.get('ko'))
    authors = defaultdict(list)
    for row in rows:
        if row['kind'] in {'ui', 'ui_resource', 'master'}:
            authors[row['jp']].append(row['id'])
    for jp, ko in read_json(package/'ui.json').items():
        add('ui_export', 'ui.json', [jp], jp, ko, writable=False,
            exact_source_candidates=authors[jp])
    return rows


def scan(repo, terms=()):
    rows = occurrences(repo)
    groups = defaultdict(lambda: defaultdict(list))
    for row in rows:
        if not row.get('held'):
            groups[row['jp']][row['ko_current']].append(row['id'])
    variants = [{'jp': jp, 'variants': dict(values), 'classification': 'PENDING_CONTEXT_REVIEW'}
                for jp, values in sorted(groups.items()) if len(values) > 1]
    probes = [{'jp_term': term, 'occurrence_ids': [r['id'] for r in rows if term in r['jp'] and not r.get('held')],
               'canonical_decision': 'NOT_ASSIGNED'} for term in sorted(set(terms))]
    return rows, {'schema': 'gc-corpus-consistency-v1', 'counts': dict(Counter(r['kind'] for r in rows)),
                  'exact_jp_variant_groups': variants, 'term_occurrences': probes,
                  'knowledge_records': len(load_current(repo)), 'translation_changes': 0,
                  'note': 'Candidates, not error counts. UI export is derived; same JP can have contextual variants.'}


def prepare(repo, *, terms=(), ids=()):
    require(terms or ids, 'Select exact occurrence ids or JP terms')
    all_rows = occurrences(repo)
    known = {r['id'] for r in all_rows}
    require(set(ids) <= known, 'Unknown occurrence id; scan the current corpus')
    rows = [r for r in all_rows if r.get('writable', True) and not r.get('held')
            and (r['id'] in ids or any(term in r['jp'] for term in terms))]
    require(rows, 'No writable current occurrences selected')
    head = subprocess.run(['git', '-C', str(repo), 'rev-parse', 'HEAD'], capture_output=True, text=True)
    novel_rows = defaultdict(list)
    for row in all_rows:
        if row['kind'] == 'novel':
            novel_rows[row['source_id']].append(row)
    selected = {r['id'] for r in rows}
    neighbors = {}
    for source_rows in novel_rows.values():
        for index, row in enumerate(source_rows):
            if row['id'] in selected:
                neighbors[row['id']] = [{k: item[k] for k in ('id', 'jp', 'ko_current')}
                                        for item in source_rows[max(0, index-2):index+3]]
    document = {'schema': 'gc-corpus-review-v1', 'base_commit': head.stdout.strip(), 'rows': rows,
                'context': translation_context(repo, jp_texts=[r['jp'] for r in rows]),
                'novel_context': neighbors,
                'note': 'Cross-corpus context has no Novel source scope. Scoped observations remain references; no promotion.'}
    review = {'schema': 'gc-corpus-decisions-v1', 'batch_sha256': sha(document),
              'review_id': '', 'pro_review_complete': False, 'knowledge_review': '', 'changes': [],
              'rows': [{'id': r['id'], 'action': 'pending', 'ko_final': None, 'reason': ''} for r in rows]}
    return document, review


def _put(document, pointer, value):
    for part in pointer[:-1]:
        document = document[part]
    document[pointer[-1]] = value


def apply(repo, batch, review):
    repo = Path(repo)
    with ExitStack() as locks:
        # Cooperate with both existing publishers instead of adding another runtime/lock service.
        locks.enter_context(ProcessFileLock(repo/'work/novels/.apply.lock', 'NOVEL_WRITER_BUSY'))
        locks.enter_context(ProcessFileLock(repo/'translations/.local-parity-apply.lock', 'PACKAGE_WRITER_BUSY'))
        return _apply(repo, batch, review)


def _apply(repo, batch, review):
    require(batch.get('schema') == 'gc-corpus-review-v1' and review.get('schema') == 'gc-corpus-decisions-v1', 'Corpus review schema')
    require(review.get('batch_sha256') == sha(batch), 'Corpus batch changed')
    require(review.get('pro_review_complete') is True and isinstance(review.get('review_id'), str)
            and review['review_id'].strip(), 'Explicit PRO final review required')
    require(isinstance(review.get('knowledge_review'), str) and review['knowledge_review'].strip(), 'Knowledge review findings required')
    live = {r['id']: r for r in occurrences(repo)}
    wanted = {r['id']: r for r in batch['rows']}
    decisions = {r['id']: r for r in review['rows']}
    require(len(wanted) == len(batch['rows']) and len(decisions) == len(review['rows']) and wanted.keys() == decisions.keys(), 'Corpus row coverage differs')
    corrections, held, pending = [], [], []
    numeric_title_evidence = []
    title_preimages = {}
    for key, old in wanted.items():
        require(live.get(key) == old and old.get('writable', True) and not old.get('held'), 'Corpus target changed or not writable: '+key)
        decision = decisions[key]
        action = decision.get('action')
        require(action in {'correct', 'keep', 'hold', 'pending'}, 'Unknown corpus decision')
        if action == 'pending':
            pending.append(key)
            continue
        require(isinstance(decision.get('reason'), str) and decision['reason'].strip(), 'Corpus decision needs context/reason')
        if action == 'hold':
            held.append(key)
        if action != 'correct':
            require(decision.get('ko_final') is None, 'Only correct decisions supply ko_final')
            continue
        ko = decision.get('ko_final')
        require(isinstance(ko, str) and ko.strip() and '\x00' not in ko, 'Invalid corrected KO')
        if old['kind'] == 'novel':
            check_text(old['jp'], ko, dialogue=old['dialogue'], previous_ko=old['ko_current'])
        elif old['kind'] != 'name':
            try:
                validate_translation(old['jp'], ko)
            except ValueError as error:
                if str(error) != 'NUMBER_MISMATCH':
                    raise
                novel = _numeric_title_authority(old, ko, live, decisions)
                title_preimages[novel['path']] = novel['file_sha256']
                numeric_title_evidence.append({'rule': 'TITLE_NUMERIC_EQUIVALENCE_VIA_APPROVED_NOVEL',
                                               'id': key, 'jp': old['jp'], 'ko': ko, 'novel': novel['source_id'],
                                               'table': old['table'], 'field': old['field']})
        corrections.append((old, ko))
    package = repo/'translations/ko'
    names, records = read_json(package/'names.json'), load_current(repo)
    changes = list(review.get('changes', []))
    changes.extend({'kind': 'name', 'jp': row['jp'], 'old_ko': row['ko_current'], 'ko': ko, 'approved': True}
                   for row, ko in corrections if row['kind'] == 'name')
    check_context(batch['context'], records, names, jp_texts=[r['jp'] for r in batch['rows']], changes=changes)
    next_records, next_names = _checked_changes(records, names, changes, review['review_id'])
    guarded = {package/p for p in GUARDED} | {package/r['path'] for r, _ in corrections}
    guarded.update(package/p for p in title_preimages)
    guarded.add(repo/'translation/knowledge.jsonl')
    guarded.add(package/'ui/enhancement-families.json')
    before = {p: p.read_bytes() if p.exists() else None for p in guarded}
    for relative, digest in title_preimages.items():
        require(hashlib.sha256(before[package/relative] or b'').hexdigest() == digest, 'Novel title preimage changed')
    with tempfile.TemporaryDirectory(prefix='corpus-stage-', dir=repo/'work') as temporary:
        stage = Path(temporary)/'ko'
        shutil.copytree(package, stage)
        documents = {}
        for row, ko in corrections:
            if row['kind'] == 'name':
                continue
            path = row['path']
            if path not in documents:
                documents[path] = read_json(stage/path)
            document = documents[path]
            _put(document, row['pointer'], ko)
        if names != next_names:
            documents['names.json'] = next_names
        for relative, document in documents.items():
            original = (stage/relative).read_bytes()
            raw = encode_novel_payload(document)
            # Keep the current file's line endings; an exact-value correction
            # must not turn the whole Master/authoring file into a format diff.
            if b'\r\n' in original and b'\n' not in original.replace(b'\r\n', b''):
                raw = raw.replace(b'\n', b'\r\n')
            (stage/relative).write_bytes(raw)
        # Menu-title corrections must be reviewed together with their Novel title.
        master = read_json(stage/'master.json')
        title_changes = {row['jp'].translate(TITLE_WIDTH) for row, _ in corrections
                         if (row['kind'] == 'novel' and not row['dialogue'])
                         or (row.get('table') == 'mNovels' and row.get('field') in ('title', 'ml_title[]'))}
        title_values = defaultdict(set)
        for row in live.values():
            title_key = row['jp'].translate(TITLE_WIDTH)
            if row['kind'] == 'novel' and title_key in title_changes:
                document = documents.get(row['path'])
                title_values[title_key].add(document[row['jp']] if document is not None else row['ko_current'])
        for jp in title_changes:
            for field in ('title', 'ml_title[]'):
                for key, value in master.get('mNovels', {}).get(field, {}).items():
                    if key.translate(TITLE_WIDTH) == jp:
                        title_values[jp].add(value)
            require(len(title_values[jp]) <= 1, 'Review the matching mNovels/Novel titles in the same batch: '+jp)
        payload, _ = export(stage)
        if payload != (stage/'ui.json').read_bytes():
            (stage/'ui.json').write_bytes(payload)
            documents['ui.json'] = None
        modified = [p for p in documents if (stage/p).read_bytes() != (package/p).read_bytes()]
        if modified:
            # Reviewed exact is authoring input, not a downloadable runtime component.
            build(stage, modified=[p for p in modified if p not in
                                   {'ui/general-exact.reviewed.json', 'ui/detail-families.json', 'ui/enhancement-families.json'}])
        updates = {package/p: (stage/p).read_bytes() for p in set(modified) | META_FILES
                   if (stage/p).read_bytes() != (package/p).read_bytes()}
        knowledge_raw = dump_knowledge(next_records)
        if knowledge_raw != before[repo/'translation/knowledge.jsonl']:
            updates[repo/'translation/knowledge.jsonl'] = knowledge_raw
        if updates:
            receipt = {'schema': 'gc-corpus-applied-v1', 'review_id': review['review_id'],
                       'batch_sha256': sha(batch), 'knowledge_review': review['knowledge_review'],
                       'rows': [dict(row, ko_final=ko, reason=decisions[row['id']]['reason']) for row, ko in corrections],
                       'knowledge': change_history(records, names, next_records, next_names,
                                                   review_id=review['review_id'], changes=changes),
                       'translation_input': False}
            if numeric_title_evidence:
                receipt['numeric_title_equivalences'] = numeric_title_evidence
            path = repo/'knowledge-layer/provenance/corpus-changes'/(sha(receipt)+'.json')
            raw = encode(receipt)
            before[path] = path.read_bytes() if path.exists() else None
            require(before[path] in (None, raw), 'Corpus history is immutable')
            updates[path] = raw
        require(all((p.read_bytes() if p.exists() else None) == raw for p, raw in before.items()), 'Corpus inputs changed during staging')
        check_context(batch['context'], load_current(repo), read_json(package/'names.json'),
                      jp_texts=[r['jp'] for r in batch['rows']], changes=changes)
        _publish(updates, {p: before[p] for p in updates})
    return {'status': 'APPLIED_LOCAL', 'corrected_rows': len(corrections), 'held': held, 'pending': pending,
            'changed_files': [p.relative_to(repo).as_posix() for p in updates], 'git': 'NOT_RUN', 'native': 'NOT_RUN'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['scan', 'prepare', 'apply'])
    parser.add_argument('--repo', type=Path, default=ROOT)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--term', action='append', default=[])
    parser.add_argument('--id', action='append', default=[])
    parser.add_argument('--batch', type=Path)
    parser.add_argument('--review', type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    require(not any(output.is_relative_to(args.repo.resolve()/p) for p in ('translations', 'translation', 'knowledge-layer', '.git')), 'Output must be a work directory')
    require(not output.exists(), 'Use a new output directory; preserve existing reviews')
    output.mkdir(parents=True)
    if args.command == 'scan':
        rows, ledger = scan(args.repo, args.term)
        with (output/'index.jsonl').open('w', encoding='utf-8', newline='\n') as stream:
            for row in rows:
                stream.write(json.dumps(row, ensure_ascii=False, separators=(',', ':'))+'\n')
        (output/'corpus-consistency-ledger.json').write_bytes(encode(ledger))
        print(json.dumps({'counts': ledger['counts'], 'variant_groups': len(ledger['exact_jp_variant_groups'])}))
    elif args.command == 'prepare':
        batch, review = prepare(args.repo, terms=args.term, ids=args.id)
        (output/'batch.json').write_bytes(encode(batch))
        (output/'review.json').write_bytes(encode(review))
        print(json.dumps({'rows': len(batch['rows']), 'semantic_review': 'NOT_RUN'}))
    else:
        require(args.batch and args.review, 'Supply --batch and --review')
        result = apply(args.repo, read_json(args.batch), read_json(args.review))
        (output/'result.json').write_bytes(encode(result))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
