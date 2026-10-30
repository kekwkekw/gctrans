"""Disposable corpus/Knowledge integration fixtures, never actual PRO acceptance."""
import json
from pathlib import Path
import os
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'tools'), str(ROOT/'tests')]
import corpus_consistency as corpus
import novel_workflow as novel
import translation_knowledge as knowledge
from local_parity_fixture import fixture, save
from test_translation_knowledge import record
from update.local_parity import make_plan, review_template, apply_review
from update.process_lock import ProcessFileLock


class CorpusTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.repo = Path(self.temp.name)
        self.package = fixture(self.repo)

    def tearDown(self):
        self.temp.cleanup()

    def review(self, terms):
        batch, review = corpus.prepare(self.repo, terms=terms)
        review.update(review_id='SYNTHETIC_NOT_PRO', pro_review_complete=True,
                      knowledge_review='Synthetic fixture: no new knowledge.')
        return batch, review

    def test_current_only_index_and_projection_ignore_poisoned_legacy(self):
        for path in ('knowledge-layer/provenance/old.jsonl', 'knowledge-layer/current.jsonl',
                     'novels/1/zh_Hans.json', 'names/zh_Hans.json'):
            target = self.repo/path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('INVALID LEGACY JSON', encoding='utf-8')
        rows, ledger = corpus.scan(self.repo)
        self.assertEqual(ledger['knowledge_records'], 0)
        self.assertEqual({r['ko_current'] for r in rows if r['kind'] == 'novel'}, {'본문'})
        batch, _ = corpus.prepare(self.repo, terms=['名前'])
        self.assertEqual(batch['context']['names'], {'名前': '이름'})
        self.assertEqual(batch['context']['knowledge']['records'], [])

    def test_exact_corrections_export_and_preserve_other_canonicals(self):
        master_raw = (self.package/'master.json').read_bytes().replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        (self.package/'master.json').write_bytes(master_raw)
        protected = {p: (self.package/p).read_bytes() for p in ('names.json', 'ui.templates.json')}
        before_knowledge = (self.repo/'translation/knowledge.jsonl').read_bytes()
        batch, review = self.review(['既存', '本文'])
        for row in review['rows']:
            original = next(r for r in batch['rows'] if r['id'] == row['id'])
            row.update(action='correct', ko_final='현행' if original['jp'] == '既存' else '새 본문', reason='fixture correction')
        result = corpus.apply(self.repo, batch, review)
        self.assertEqual(result['corrected_rows'], 2)
        self.assertEqual(json.loads((self.package/'ui.json').read_bytes())['既存'], '현행')
        self.assertEqual(json.loads((self.package/'novels/1.json').read_bytes()), {'本文': '새 본문'})
        self.assertEqual(protected, {p: (self.package/p).read_bytes() for p in protected})
        self.assertEqual(before_knowledge, (self.repo/'translation/knowledge.jsonl').read_bytes())
        self.assertEqual((self.package/'master.json').read_bytes(), master_raw.replace('기존'.encode(), '현행'.encode()))
        receipt = list((self.repo/'knowledge-layer/provenance/corpus-changes').glob('*.json'))
        self.assertEqual(len(receipt), 1)
        self.assertFalse(json.loads(receipt[0].read_bytes())['translation_input'])
        with self.assertRaisesRegex(ValueError, 'target changed'):
            corpus.apply(self.repo, batch, review)

    def test_stale_context_and_title_mismatch_do_not_publish(self):
        save(self.package/'master.json', {'mNovels': {'title': {'題': '제목'}, 'ml_title[]': {'題': '제목'}}})
        save(self.package/'novels/1.json', {'題': '제목', '本文': '본문'})
        batch, review = self.review(['題'])
        review['rows'][0].update(action='correct', ko_final='다른 제목', reason='fixture')
        before = (self.package/'master.json').read_bytes()
        with self.assertRaisesRegex(ValueError, 'matching mNovels/Novel'):
            corpus.apply(self.repo, batch, review)
        self.assertEqual((self.package/'master.json').read_bytes(), before)
        save(self.package/'master.json', {'mNovels': {'title': {'題': '독립 수정'}}})
        with self.assertRaisesRegex(ValueError, 'target changed'):
            corpus.apply(self.repo, batch, review)

    def test_title_width_alias_is_linked_without_rewriting_jp_keys(self):
        master = json.loads((self.package/'master.json').read_bytes())
        master['mNovels'] = {'title': {'題 1': '제목1'}, 'ml_title[]': {'題 1': '제목1'}}
        save(self.package/'master.json', master)
        save(self.package/'novels/1.json', {'題　１': '제목1', '本文': '본문'})
        batch, review = self.review(['題'])
        for target, decision in zip(batch['rows'], review['rows']):
            if target['kind'] == 'master':
                decision.update(action='correct', ko_final='새 제목1', reason='fixture')
        with self.assertRaisesRegex(ValueError, 'matching mNovels/Novel'):
            corpus.apply(self.repo, batch, review)
        for decision in review['rows']:
            decision.update(action='correct', ko_final='새 제목1', reason='fixture')
        corpus.apply(self.repo, batch, review)
        self.assertEqual(json.loads((self.package/'novels/1.json').read_bytes()), {'題　１':'새 제목1', '本文':'본문'})

    def test_six_reviewed_master_titles_follow_unique_final_novel(self):
        titles = {'二代目': ('10010802', '2대째'), '二代目との接触': ('11011102', '2대째와의 접촉'),
                  '３人の少女たち': ('10011006', '세 명의 소녀들'), '３人の出会い': ('11011001', '세 사람의 만남')}
        girls = '３人の少女たち'
        current = {jp: ('3명의 소녀들' if jp == girls else ko) for jp, (_, ko) in titles.items()}
        master = json.loads((self.package/'master.json').read_bytes())
        master.update(mNovels={'title': current, 'ml_title[]': current},
                      mQuestStages={'ml_name[]': dict.fromkeys(titles, '이전 표기')})
        save(self.package/'master.json', master)
        for jp, (source_id, _) in titles.items():
            save(self.package/'novels'/(source_id+'.json'), {jp: current[jp]})
        source_ids = [source_id for source_id, _ in titles.values()]
        corpus.build(self.package, added=['novels/'+s+'.json' for s in source_ids], register=source_ids,
                     modified=['master.json'])
        batch, review = self.review(list(titles))
        for row, d in zip(batch['rows'], review['rows']):
            ko = titles[row['jp']][1]
            if row['ko_current'] == ko:
                d.update(action='keep', reason='fixture')
            else:
                d.update(action='correct', ko_final=ko, reason='fixture')
        result = corpus.apply(self.repo, batch, review)
        self.assertEqual(result['corrected_rows'], 7)  # six Master fields plus the same-batch Novel title
        path, = (self.repo/'knowledge-layer/provenance/corpus-changes').glob('*.json')
        evidence = json.loads(path.read_bytes())['numeric_title_equivalences']
        expected = {(jp, 'mQuestStages', 'ml_name[]') for jp in titles} | {(girls, 'mNovels', f) for f in master['mNovels']}
        self.assertEqual({(e['jp'], e['table'], e['field']) for e in evidence}, expected)
        for e in evidence:
            self.assertEqual((e['novel'], e['ko']), titles[e['jp']])

    def test_numeric_title_requires_exact_meaning_unique_source_and_title_field(self):
        jp = '３人の出会い'
        ko = '세 사람의 만남'
        cases = [
            ('二代目', '3대째', ['3대째'], 'mQuestStages', 'ml_name[]'),
            ('３人の少女たち', '네 명의 소녀들', ['네 명의 소녀들'], 'mNovels', 'title'),
            (jp, '두 사람의 만남', ['두 사람의 만남'], 'mQuestStages', 'ml_name[]'),
            (jp, ko, ['다른 제목'], 'mQuestStages', 'ml_name[]'),
            (jp, ko, [], 'mQuestStages', 'ml_name[]'),
            (jp, ko, [ko, ko], 'mQuestStages', 'ml_name[]'),
            (jp, ko, [ko], 'mItems', 'quantity'),
        ]
        for source, proposed, novels, table, field in cases:
            with self.subTest(source=source, proposed=proposed, novels=novels, field=field):
                master = {'kind':'master', 'jp':source, 'table':table, 'field':field}
                first_id = corpus.REVIEWED_NUMERIC_TITLES[source][0]
                live = {str(i): {'kind':'novel', 'jp':source, 'ko_current':v, 'dialogue':False,
                                 'id':str(i), 'source_id':first_id if i == 0 else '999', 'held':False}
                        for i, v in enumerate(novels)}
                with self.assertRaisesRegex(ValueError, 'NUMBER_MISMATCH'):
                    corpus._numeric_title_authority(master, proposed, live, {})

    def test_knowledge_retirement_history_and_publication_rollback(self):
        old = record('term:old', jp='本文', status='approved', scope='global')
        path = self.repo/'translation/knowledge.jsonl'
        path.write_bytes(knowledge.dump_knowledge([old]))
        batch, review = self.review(['本文'])
        review['changes'] = [{'kind': 'knowledge', 'id': old['id'], 'operation': 'retire',
                              'old_sha256': knowledge.record_sha256(old), 'approved': True,
                              'reason': 'Synthetic obsolete record'}]
        review['rows'][0].update(action='keep', reason='Current wording retained')
        replace = os.replace
        def fail_receipt(src, dst):
            if 'corpus-changes' in Path(dst).parts:
                raise OSError('receipt publication failure after canonical write')
            return replace(src, dst)
        with patch('novel_workflow.os.replace', side_effect=fail_receipt):
            with self.assertRaises(OSError):
                corpus.apply(self.repo, batch, review)
        self.assertEqual(knowledge.load_current(self.repo), [old])
        corpus.apply(self.repo, batch, review)
        self.assertEqual(knowledge.load_current(self.repo), [])
        receipt = json.loads(next((self.repo/'knowledge-layer/provenance/corpus-changes').glob('*.json')).read_bytes())
        entry = receipt['knowledge']['entries'][0]
        self.assertEqual(entry['before'], old)
        self.assertIsNone(entry['after'])
        self.assertEqual(knowledge.translation_context(self.repo, jp_texts=['本文'])['knowledge']['records'], [])

    def test_ui_authoring_edit_regenerates_export_without_registering_authoring(self):
        save(self.package/'ui/general-exact.reviewed.json', {'表示': '표시'})
        batch, review = self.review(['表示'])
        review['rows'][0].update(action='correct', ko_final='화면 표시', reason='fixture')
        corpus.apply(self.repo, batch, review)
        self.assertEqual(json.loads((self.package/'ui.json').read_bytes())['表示'], '화면 표시')
        registered = json.loads((self.package/'release-manifest.json').read_bytes())['files']
        self.assertFalse(any(x['path'] == 'ui/general-exact.reviewed.json' for x in registered))

    def test_corpus_respects_both_publishers_and_releases_partial_lock(self):
        batch, review = self.review(['本文'])
        review['rows'][0].update(action='correct', ko_final='새 본문', reason='fixture')
        original = (self.package/'novels/1.json').read_bytes()
        for path, code in ((self.repo/'work/novels/.apply.lock', 'NOVEL_WRITER_BUSY'),
                           (self.repo/'translations/.local-parity-apply.lock', 'PACKAGE_WRITER_BUSY')):
            with ProcessFileLock(path):
                with self.assertRaisesRegex(ValueError, code):
                    corpus.apply(self.repo, batch, review)
            self.assertEqual((self.package/'novels/1.json').read_bytes(), original)
        corpus.apply(self.repo, batch, review)

    def test_corpus_does_not_overwrite_an_independent_update_during_staging(self):
        batch, review = self.review(['本文'])
        review['rows'][0].update(action='correct', ko_final='새 본문', reason='fixture')
        original = (self.package/'novels/1.json').read_bytes()
        actual_build = corpus.build
        def concurrent(*args, **kwargs):
            result = actual_build(*args, **kwargs)
            save(self.package/'names.json', {'名前': '독립 변경'})
            return result
        with patch.object(corpus, 'build', side_effect=concurrent):
            with self.assertRaisesRegex(ValueError, 'Corpus inputs changed during staging'):
                corpus.apply(self.repo, batch, review)
        self.assertEqual((self.package/'novels/1.json').read_bytes(), original)
        self.assertEqual(json.loads((self.package/'names.json').read_bytes()), {'名前': '독립 변경'})

    def test_old_master_ui_batches_remain_compatible(self):
        batch = make_plan(self.package, self.repo/'baseline.json', 'fixture')
        batch.pop('context')
        review = review_template(batch)
        self.assertNotIn('knowledge_review', review)
        review['rows'][0].update(action='translate', ko='추가', reviewer='fixture', evidence='Synthetic')
        self.assertEqual(apply_review(self.package, batch, review)['added_master'], 1)


class TextAndContextTests(unittest.TestCase):
    @staticmethod
    def text_guards(jp, ko):
        original = {'row_id': '99991:0', 'source_id': '99991', 'message_index_0based': 0,
                    'physical_line_1based': 1, 'command': 'message', 'speaker': '話者',
                    'jp': jp, 'raw_field_bytes': [0, len(jp.encode('utf-8'))]}
        document = {'text_rules': novel.TEXT_RULES,
                    'sources': [{'source_id': '99991', 'title': '제목', 'rows': [original]}]}
        return {
            'check_text': lambda: novel.check_text(jp, ko),
            'novel_final_rows': lambda: novel._checked_rows(document, [dict(original, hold=False, reason='', ko=ko)]),
            'corpus_text_guard': lambda: novel.check_text(jp, ko, previous_ko=jp),
        }

    def test_reflow_rejects_split_raw_tokens(self):
        cases = [
            ('{PLAYER}の話', '{PLA<br>YER}의 이야기'),
            ('${PLAYER}の話', '${PLA\nYER}의 이야기'),
            ('<color=red>本文</color>', '<co<br>lor=red>본문</color>'),
            ('<color=red>本文</color>', '<color=re<br>d>본문</color>'),
            ('<color=red>本文</color>', '<color=re\nd>본문</color>'),
            ('<color="red">本文</color>', '<color="re<br>d">본문</color>'),
            ('{PLAYER}の話', '{PLA\r\nYER}의 이야기'),
            ('${PLAYER}の話', '${PLA<BR/>YER}의 이야기'),
            ('$PLAYERの話', '$PLA<br>YER의 이야기'),
            ('%2$sの話', '%2<br>$s의 이야기'),
            ('■■の話', '■<br>■의 이야기'),
            # Filtering only the break tokens still permits this raw-span join.
            ('<b></b>{PLAYER}', '{PLA<b></b>YER}'),
        ]
        for jp, ko in cases:
            for name, guard in self.text_guards(jp, ko).items():
                with self.subTest(jp=jp, ko=ko, guard=name), self.assertRaises(ValueError):
                    guard()

    def test_reflow_outside_raw_tokens_is_allowed(self):
        for jp, ko in [
            ('長い文章<br>続き', '긴 문장입니다. 이어집니다.'),
            ('{PLAYER}の話', '{PLAYER}의<br>이야기'),
            ('${PLAYER}の話', '${PLAYER}의\n이야기'),
            ('<color=red>本文</color>', '<color=red>본<br>문</color>'),
            ('<b></b>{PLAYER}', '<b></b><br>{PLAYER}'),
        ]:
            for name, guard in self.text_guards(jp, ko).items():
                with self.subTest(jp=jp, ko=ko, guard=name):
                    guard()

    def test_reflow_counts_utf16_and_preserves_controls(self):
        self.assertEqual(novel.line_cells('가'*32+'😀<br>　끝'), [34, 2])
        novel.check_text('長い文章<br>続き', '긴 문장입니다. 이어집니다.')
        novel.check_text('見出し', '가'*80, dialogue=False)
        for text in ('가'*33+'😀', '가<br>나<br>다<br>라'):
            with self.assertRaisesRegex(ValueError, '34 UTF-16'):
                novel.check_text('原文', text)
        with self.assertRaisesRegex(ValueError, 'block split'):
            novel.check_text('<click=1>原文</click>', '<click=1>첫<br>둘</click>')
        with self.assertRaisesRegex(ValueError, 'block changed'):
            novel.check_text('<ruby=x>原文</ruby>', '<ruby=x>새 표기</ruby>', previous_ko='<ruby=x>기존 표기</ruby>')
        with self.assertRaisesRegex(ValueError, 'Markup'):
            novel.check_text('{PLAYER}<br>本文', '{USER} 본문')

    def test_layout_boundaries_narration_and_literal_escape_contract(self):
        for separator in ('<br>', '<BR/>', '\n', '\r\n', '\r'):
            text = separator.join(['가'*34]*3)
            self.assertEqual(novel.line_cells(text), [34, 34, 34])
            novel.check_text('原文', text)
        with self.assertRaisesRegex(ValueError, '34 UTF-16'):
            novel.check_text('原文', '가'*35)
        self.assertEqual(novel.line_cells('<ruby=x>가</ruby><click=1>나</click>'), [2])
        for command in ('message', 'msgvoicesync', None):
            self.assertTrue(novel.is_dialogue({'command': command, 'speaker': '', 'jp': '地の文'}, {}))
        self.assertFalse(novel.is_dialogue({'command': 'title', 'jp': '題'}, {}))
        novel.check_text(r'甲\n乙', r'가\n나')
        self.assertEqual(novel.line_cells(r'가\n나'), [4])
        for ko in ('가나', '가\n나', '가<br>나'):
            with self.assertRaisesRegex(ValueError, 'Markup'):
                novel.check_text(r'甲\n乙', ko)

    def test_retire_checks_preimage_and_leaves_other_scope_and_reference_intact(self):
        retired = record('term:retire', status='approved')
        remaining = record('term:remaining', status='approved', sources=['10011201'], scope={'source_ids':['10011201']})
        remaining['origin']['source_map_ref'] = 'knowledge-layer/provenance/historical-map.json'
        change = {'kind':'knowledge', 'id':retired['id'], 'operation':'retire',
                  'old_sha256':knowledge.record_sha256(retired), 'approved':True, 'reason':'Synthetic explicit retirement'}
        changed = dict(retired, value={'jp':'改訂', 'ko':'수정'})
        with self.assertRaisesRegex(ValueError, 'preimage'):
            knowledge.apply_reviewed_changes([changed, remaining], {}, [change], review_id='fixture-review')
        records, _ = knowledge.apply_reviewed_changes([retired, remaining], {}, [change], review_id='fixture-review')
        self.assertEqual(records, [remaining])
        retried, _ = knowledge.apply_reviewed_changes(records, {}, [change], review_id='fixture-review')
        self.assertEqual(retried, [remaining])

    def test_only_applicable_terms_bypass_budget_and_stale_terms_are_targeted(self):
        old = record('term:scoped', status='approved')
        outside = knowledge.project([old], jp_texts=['アルテ'], source_ids=['10011201'], max_chars=1)
        self.assertEqual(outside['required_term_ids'], [])
        self.assertEqual(outside['records'], [])
        active = knowledge.project([old], jp_texts=['アルテ'], source_ids=['10010101'], max_chars=1)
        self.assertEqual(active['required_term_ids'], [old['id']])
        self.assertTrue(active['budget_exceeded'])
        context = {'names': {}, 'knowledge': knowledge.project([], max_chars=1)}
        knowledge.check_context(context, [old], {}, jp_texts=['アルテ'], source_ids=['10011201'])
        with self.assertRaisesRegex(ValueError, 'New applicable terms'):
            knowledge.check_context(context, [old], {}, jp_texts=['アルテ'], source_ids=['10010101'])


if __name__ == '__main__':
    unittest.main()
