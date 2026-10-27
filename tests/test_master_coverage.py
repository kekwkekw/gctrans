import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from update.master_coverage import plan, validate_draft, reviewed_master


class CoverageTests(unittest.TestCase):
    def test_identity_is_table_field_and_exact_jp_and_ignores_zh_value(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'master.json').write_text(json.dumps({'t': {'f': {'JP': '한국어'}}}), encoding='utf-8')
            baseline = root/'baseline.json'
            baseline.write_text(json.dumps({'t': {'f': {'JP': '中文'}, 'g': {'JP': '中文'}}}), encoding='utf-8')
            result = plan(baseline, root, {}, 'fixed-ref')
            self.assertEqual(result['missing_count'], 1)
            self.assertEqual(result['missing'][0]['field'], 'g')
            self.assertIsNone(result['missing'][0]['ko'])
            self.assertNotIn('中文', json.dumps(result, ensure_ascii=False))

    def row(self, jp='威力[[power]%]＜[[turn]ターン]＞', ko='위력[[power]%]＜[[turn]턴]＞'):
        return {'table': 't', 'field': 'f', 'jp': jp, 'ko': ko}

    def test_variable_unit_translation(self):
        row = self.row()
        self.assertEqual(len(validate_draft({'missing':[row]}, [row])), 1)

    def test_no_incomplete_or_duplicate_or_foreign_keys(self):
        row = self.row()
        for rows in ([], [row,row], [row | {'field':'other'}]):
            with self.assertRaises(ValueError):validate_draft({'missing':[row]}, rows)

    def test_no_variable_tag_number_or_line_loss(self):
        row = self.row('威力[[power]%]/<help=x>攻撃</help>10\n回', '위력[[power]%]/<help=x>공격</help>10\n회')
        for ko in (row['ko'].replace('power','other'), row['ko'].replace('help=x','help=y'),
                   row['ko'].replace('10','11'), row['ko'].replace('\n','')):
            with self.assertRaises(ValueError):validate_draft({'missing':[row]}, [row|{'ko':ko}])

    def test_neutral_keep_requires_explicit_disposition(self):
        row = self.row('AP・BP1/2','AP・BP1/2')
        with self.assertRaises(ValueError):validate_draft({'missing':[row]}, [row])
        validate_draft({'missing':[row]}, [row|{'disposition':'keep_source'}])
        row = self.row('日本語','日本語') | {'disposition':'keep_source'}
        with self.assertRaises(ValueError):validate_draft({'missing':[row]}, [row])

    def test_angle_display_label_is_not_a_markup_tag(self):
        row = self.row('<解除>', '<해제>')
        validate_draft({'missing':[row]}, [row])

    def test_review_requires_expected_old_value_and_preserves_other_keys(self):
        original = {'t': {'f': {'日本': '옛 번역', '他': '보존'}}}
        review = self.row('日本', '일본') | {'before_ko':'옛 번역','review_status':'CODEX_SEMANTIC_REVIEWED'}
        result, info = reviewed_master(original, {'missing':[]}, [], [review])
        self.assertEqual(result['t']['f'], {'日本':'일본','他':'보존'})
        self.assertEqual(original['t']['f']['日本'], '옛 번역')
        self.assertEqual(info['modified_existing'], 1)
        for rows in ([review | {'before_ko':'stale'}], [review, review]):
            with self.assertRaises(ValueError): reviewed_master(original, {'missing':[]}, [], rows)

    def test_hold_is_explicit_and_requires_record_evidence(self):
        original = {'t': {'f': {'日本': '추정 번역'}}}
        hold = self.row('日本','日本') | {'before_ko':'추정 번역', 'review_status':'HOLD_SOURCE_CONTRADICTION',
                                       'hold_reason':'원문 필드 충돌'}
        with self.assertRaises(ValueError): reviewed_master(original, {'missing':[]}, [], [hold])
        result, info = reviewed_master(original, {'missing':[]}, [], [hold | {'evidence':{'records':[{'id':1}]}}])
        self.assertEqual(result['t']['f']['日本'], '日本')
        self.assertEqual(info['held_existing'], 1)

    def test_failed_combined_review_does_not_mutate_input(self):
        original = {'t': {'f': {'日本': '일본'}}}
        addition = self.row('追加1','추가1')
        bad = self.row('日本','일본') | {'before_ko':'stale','review_status':'CODEX_SEMANTIC_REVIEWED'}
        with self.assertRaises(ValueError): reviewed_master(original, {'missing':[addition]}, [addition], [bad])
        self.assertNotIn('追加1', original['t']['f'])


if __name__ == '__main__':
    unittest.main()
