import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from collect_ui_literals import collect


def metadata(texts):
    values = [t.encode('utf8') for t in texts]
    indexes = [0]
    for value in values:
        indexes.append(indexes[-1] + len(value))
    count = len(indexes)
    return (struct.pack('<8I', 0xFAB11BAF, 39, 32, count*4, count, 32+count*4, indexes[-1], count-1)
            + struct.pack(f'<{count}I', *indexes) + b''.join(values))


class LiteralCollectionTests(unittest.TestCase):
    def test_all_literals_has_no_language_or_empty_string_gate(self):
        raw = metadata(['AP', 'Confirm purchase?', '123', '', '回復', 'AP'])
        result = collect(raw, all_literals=True)
        self.assertEqual(result['selection'], 'all_literals')
        self.assertEqual(result['total_literal_entries'], 6)
        self.assertEqual({r['jp'] for r in result['rows']}, {'AP', 'Confirm purchase?', '123', '', '回復'})
        self.assertIn('indexes=[0, 5]', next(r['evidence'] for r in result['rows'] if r['jp'] == 'AP'))
        for kwargs in ({'all_literals': True, 'all_japanese': True}, {'all_literals': True, 'contains': ['AP']}):
            with self.assertRaisesRegex(ValueError, 'AMBIGUOUS_LITERAL_SCOPE'):
                collect(raw, **kwargs)

    def test_explicit_menu_scope_preserves_exact_source_and_deduplicates(self):
        raw = metadata(['メモリーセット一覧', '本文\n本文', 'メモリーセット一覧', 'スター付き'])
        result = collect(raw, ['メモリー', 'スター'])
        self.assertEqual([r['jp'] for r in result['rows']], ['スター付き', 'メモリーセット一覧'])
        self.assertIn('indexes=[0, 2]', result['rows'][1]['evidence'])
        self.assertEqual(result['display_status'], 'SOURCE_ONLY_REQUIRES_MENU_CONTEXT_REVIEW')

    def test_corrupt_or_unknown_format_and_unbounded_scope_fail(self):
        raw = metadata(['スター付き'])
        for broken in (raw[:31], raw[:-1], raw[:4]+struct.pack('<I', 38)+raw[8:],
                       raw[:32]+struct.pack('<I', 1)+raw[36:]):
            with self.assertRaises(ValueError):
                collect(broken, ['スター'])
        for scope in ([], ['']):
            with self.assertRaises(ValueError):
                collect(raw, scope)

    def test_explicit_full_inventory_retains_diagnostics_fragments_and_source_indexes(self):
        raw = metadata(['AP回復を実行しました', 'の', '[Debug]内部ログ', 'AP', '123',
                        'AP回復を実行しました', '回数回復まで', 'English only'])
        result = collect(raw, all_japanese=True)
        self.assertEqual(result['selection'], 'all_japanese_literals')
        self.assertEqual(result['total_literal_entries'], 8)
        self.assertEqual({r['jp'] for r in result['rows']},
                         {'AP回復を実行しました', 'の', '[Debug]内部ログ', '回数回復まで'})
        ap = next(r for r in result['rows'] if r['jp'] == 'AP回復を実行しました')
        self.assertIn('indexes=[0, 5]', ap['evidence'])
        self.assertEqual(result['display_status'], 'SOURCE_ONLY_REQUIRES_MENU_CONTEXT_REVIEW')

    def test_full_inventory_keeps_format_guards_and_rejects_mixed_selection(self):
        raw = metadata(['AP回復を実行しました'])
        with self.assertRaisesRegex(ValueError, 'AMBIGUOUS_LITERAL_SCOPE'):
            collect(raw, ['AP'], all_japanese=True)
        for broken in (raw[:31], raw[:-1], raw[:4]+struct.pack('<I', 38)+raw[8:]):
            with self.assertRaises(ValueError):
                collect(broken, all_japanese=True)
