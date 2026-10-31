import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from export_local_ui import export


class ExactUiExportTests(unittest.TestCase):
    def test_ap_exact_domains_reuse_current_wording_and_stop_at_boundaries(self):
        actual = json.loads(export(ROOT / 'translations/ko')[0])
        expected = {
            '更新まで00:00': '갱신까지00:00',
            '更新まで08:47': '갱신까지08:47',
            '更新まで12:00': '갱신까지12:00',
            '回数回復まで00:00': '횟수 회복까지 00:00',
            '回数回復まで10:26': '횟수 회복까지 10:26',
            '回数回復まで12:00': '횟수 회복까지 12:00',
            'APを0回復しました。': 'AP를 0 회복했습니다.',
            'APを107回復しました。': 'AP를 107 회복했습니다.',
            'APを150回復しました。': 'AP를 150 회복했습니다.',
        }
        for jp, ko in expected.items():
            self.assertEqual(actual[jp], ko)
        for jp in ['更新まで12:01', '回数回復まで12:01', '回数回復まで00:60',
                   '更新まで8:47', 'APを151回復しました。', 'APを-1回復しました。',
                   'APを007回復しました。', 'BPを107回復しました。']:
            self.assertNotIn(jp, actual)

    def test_ap_export_rejects_conflict_without_overwriting_reviewed_text(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.package(root, [], {'回数回復までhh:mm': '횟수 회복까지 hh:mm',
                                    '更新まで08:47': '기존 번역'})
            (root/'master.json').write_text(json.dumps({'mTextConfig': {'ml_value[]': {
                'APを{0:#,0}回復しました。': 'AP를 {0:#,0} 회복했습니다.'}}}), encoding='utf-8')
            (root/'ui.templates.json').write_bytes((ROOT/'translations/ko/ui.templates.json').read_bytes())
            (root/'ui/ap-families.json').write_bytes((ROOT/'translations/ko/ui/ap-families.json').read_bytes())
            with self.assertRaisesRegex(ValueError, 'AP exact conflict'):
                export(root)

    def test_enhancement_controls_include_results_and_static_master_labels(self):
        ui = json.loads(export(ROOT / 'translations/ko')[0])
        master = json.loads((ROOT / 'translations/ko/master.json').read_bytes())
        for jp, ko in master['mVirtueEffects']['ml_name[]'].items():
            self.assertEqual(ui[jp], ko)
        self.assertEqual(ui['レベル2'], '레벨2')
        self.assertEqual(ui['レベル20'], '레벨20')
        self.assertEqual(ui['パッシブ3の効果ランクのみ抽選します'], '패시브3의 효과 랭크만 추첨합니다')
        self.assertEqual(ui['限界突破2で解放'], '돌파 2회에 해제')
        self.assertNotIn('パッシブ4の効果ランクのみ抽選します', ui)
        self.assertNotIn('限界突破6で解放', ui)
        self.assertEqual(ui['炎'], '염')

    def test_detail_labels_cover_mixed_master_output_and_finite_defaults(self):
        payload, report = export(ROOT / 'translations/ko')
        actual = json.loads(payload)
        self.assertEqual(actual['グループ4'], '그룹4')
        self.assertEqual(actual['設定4-5'], '세트4-5')
        self.assertNotIn('設定5-1', actual)
        self.assertEqual(actual['上位65%'], '상위 65%')
        self.assertEqual(actual['76%以下'], '76% 이하')
        self.assertEqual(actual['Rank10級/上位66%以下'], '랭크 10등급/상위 66% 이하')
        self.assertEqual(actual['100位'], '100위')
        self.assertNotIn('Rank110級/上位66%以下', actual)
        self.assertNotIn('上位101%', actual)
        self.assertEqual(actual['모차르트専用'], '모차르트 전용')
        self.assertEqual(actual['モーツァルト専用'], '모차르트 전용')
        self.assertEqual(actual['獲得数は메인 스테이지10-1〜10のクリアランクが対象です'],
                         '획득 수량은 메인 스테이지10-1〜10의 클리어 랭크를 기준으로 합니다')
        self.assertIn('02月29日で\n誕生日を設定します。\nよろしいですか？\n', actual)
        self.assertNotIn('02月30日で\n誕生日を設定します。\nよろしいですか？\n', actual)
        self.assertGreater(report['bounded_detail_exacts'], 390)
        for row in json.loads((ROOT/'staging/ui-details-20261028/review.json').read_bytes())['rows']:
            self.assertEqual(actual[row['jp']], row['ko'])

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
