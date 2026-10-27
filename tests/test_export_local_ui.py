import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from export_local_ui import export


class ExactUiExportTests(unittest.TestCase):
    def package(self, root, resources, reviewed=None):
        (root / 'ui').mkdir()
        values = {
            'master.json': {},
            'ui/resources.json': {'resources': resources},
            'ui/bindings.json': {'bindings': [{'resource_id': r['id'], 'enabled': True} for r in resources]},
            'ui/general-exact.reviewed.json': reviewed or {},
            'ui.templates.json': {'rules': [{'jp': '工房{value}', 'ko': '공방{value}', 'value_kind': 'integer'}]},
        }
        for name, value in values.items():
            (root / name).write_text(json.dumps(value, ensure_ascii=False), encoding='utf-8')

    def row(self, identity, jp, ko, kind='literal'):
        return {'id': identity, 'jp': jp, 'ko': ko, 'kind': kind, 'disposition': 'translate'}

    def test_native_template_caption_is_exported_and_order_does_not_matter(self):
        rows = [self.row('gem', '藝晶石所持状況', '예정석 보유 현황', 'template_label'),
                self.row('campaign', 'キャンペーン', '캠페인', 'template_label')]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.package(root, rows)
            first, report = export(root)
            self.assertEqual(json.loads(first)['藝晶石所持状況'], '예정석 보유 현황')
            self.assertEqual(json.loads(first)['キャンペーン'], '캠페인')
            (root / 'ui/resources.json').write_text(json.dumps({'resources': rows[::-1]}), encoding='utf-8')
            self.assertEqual(export(root)[0], first)
            self.assertEqual(report['runtime_bindings'], 0)

    def test_conflicting_translation_fails_instead_of_silently_dropping_key(self):
        rows = [self.row('menu', 'オプション', '옵션'), self.row('title', 'オプション', '설정')]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.package(root, rows)
            with self.assertRaisesRegex(ValueError, 'Unresolved UI translation conflict.*オプション'):
                export(root)

    def test_explicit_review_can_resolve_conflict_without_runtime_selector(self):
        rows = [self.row('menu', 'オプション', '옵션'), self.row('title', 'オプション', '설정')]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.package(root, rows, {'オプション': '옵션'})
            payload, report = export(root)
            self.assertEqual(json.loads(payload)['オプション'], '옵션')
            self.assertEqual(report['review_resolved_authoring_jp'], ['オプション'])

    def test_all_48_reviewed_captions_survive_repository_export(self):
        rows = json.loads((ROOT / 'staging/ui-exact-recovery-20261028/reviewed-48.json').read_text(encoding='utf-8'))
        payload, report = export(ROOT / 'translations/ko')
        actual = json.loads(payload)
        self.assertEqual(len(rows), 48)
        for row in rows:
            self.assertEqual(actual.get(row['jp']), row['ko'], row['jp'])
        self.assertEqual(report['held_authoring_jp'], [])
        self.assertEqual(payload, (ROOT / 'translations/ko/ui.json').read_bytes())

    def test_disabled_and_unbound_resources_still_export(self):
        rows = [self.row('badge', '装備中', '장착 중', 'old_owner_only'), self.row('unused', 'メモリー', '메모리')]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); self.package(root, rows)
            (root/'ui/bindings.json').write_text(json.dumps({'bindings':[{'resource_id':'badge','enabled':False}]}), encoding='utf-8')
            payload,_ = export(root)
            self.assertEqual(json.loads(payload)['装備中'], '장착 중')
            self.assertEqual(json.loads(payload)['メモリー'], '메모리')

    def test_master_ui_fields_are_exported_but_arbitrary_master_is_not(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); self.package(root, [])
            (root/'master.json').write_text(json.dumps({'mTextConfig':{'ml_value[]':{'藝術家強化':'예술가 강화'}},
                'mTutorialPopupDescriptions':{'ml_title[]':{'すれ違いバフ':'스쳐 지나가기 버프'}},
                'mNovels':{'title':{'私':'나'}}}), encoding='utf-8')
            result=json.loads(export(root)[0])
            self.assertEqual(result['藝術家強化'],'예술가 강화')
            self.assertEqual(result['すれ違いバフ'],'스쳐 지나가기 버프')
            self.assertNotIn('私',result)

    def test_outer_tag_alias_and_explicit_conflict_resolution(self):
        rows=[self.row('title','<cspace=-0.15em>アテネスレコード</cspace>','<cspace=-0.15em>아테네스 레코드</cspace>')]
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);self.package(root,rows)
            self.assertEqual(json.loads(export(root)[0])['アテネスレコード'],'아테네스 레코드')
            (root/'ui/local-coverage.json').write_text(json.dumps({'schema':'gc-local-ui-coverage-v1','keys':['未登録'],'keep_source':[]}), encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'Local UI coverage missing'):export(root)
            (root/'ui/local-coverage.json').write_text(json.dumps({'schema':'gc-local-ui-coverage-v1','keys':['未登録'],'keep_source':['未登録']}), encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'Invalid local UI coverage contract'):export(root)

    def test_explicit_hold_is_not_silently_activated(self):
        rows=[self.row('held','保留','보류')|{'disposition':'hold'}]
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);self.package(root,rows)
            self.assertNotIn('保留',json.loads(export(root)[0]))


if __name__ == '__main__':
    unittest.main()
