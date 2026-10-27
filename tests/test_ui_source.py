from pathlib import Path
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from tools.update.ui_source import UiPrefabExtractor, select_inventory
from tools.update.source_scan import SourceProfile, scan_inventory


def obj(kind, identity, tree=None, error=False):
    def read():
        if error: raise ValueError('decode fail')
        return tree
    return SimpleNamespace(type=SimpleNamespace(name=kind),path_id=identity,assets_file=SimpleNamespace(name='CAB'),read_typetree=read)


class UiSourceTests(unittest.TestCase):
    def test_default_scope_selects_multiple_scenes_and_common_not_voice_images(self):
        scope={'schema':'gc-ui-source-scope-v1','include':['scene/*.dmm','common*prefab.dmm']}
        inv={'d':[{'n':n} for n in ('scene/a.dmm','scene/b.dmm','common_prefab.dmm','notinit/voice/a.dmm','image/a.dmm')]}
        self.assertEqual(len(select_inventory(inv,scope)['d']),3)
        self.assertEqual(select_inventory(inv,scope,'scene/b.dmm')['d'],[{'n':'scene/b.dmm'}])

    def test_large_bundle_and_partial_component_failure_keep_other_labels(self):
        objects=[obj('Transform',n) for n in range(19213)]
        objects += [obj('MonoBehaviour',20001,{'m_text':'実際の表示'}),obj('MonoBehaviour',20002,error=True),obj('Sprite',20003)]
        with patch('UnityPy.load',return_value=SimpleNamespace(objects=objects)):
            result=UiPrefabExtractor()(b'UnityFS\0test',{'asset_key':'scene/sub.dmm'})
        self.assertEqual(result['ui'][0]['jp'],'実際の表示')
        self.assertEqual(len(result['ui_diagnostics']['failed_components']),1)
        self.assertEqual(result['ui_diagnostics']['text_status'],'PARTIAL')
        self.assertEqual(len(result['ui_diagnostics']['image_objects']),1)

    def test_production_scanner_stores_prefab_without_textasset_and_warm_reuses(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);extractor=UiPrefabExtractor();meta={'d':[{'n':'scene/ui.dmm','h':'token','s':8}]}
            profile=SourceProfile('WebGL','R18','UI','vendor-d-n-h-s-v1')
            options=dict(state_root=root/'state',raw_root=root/'raw',fetch_asset=lambda _:b'UnityFS\0test',extractor=extractor,complete=False)
            with patch('UnityPy.load',return_value=SimpleNamespace(objects=[obj('MonoBehaviour',1,{'m_Text':'確認'})])):
                first=scan_inventory(profile,meta,**options)
                warm=scan_inventory(profile,meta,**options)
            self.assertEqual(first['sourceSnapshot']['ui'][0]['jp'],'確認')
            self.assertEqual(first['sourceSnapshot']['ui_diagnostics'][0]['decoded_components'],1)
            self.assertEqual(warm['metrics']['payload_network'],0)
            self.assertEqual(warm['metrics']['materialized'],0)

    def test_metadata_first_then_bounded_acquire_resumes_remaining_assets(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);profile=SourceProfile('WebGL','R18','UI','vendor-d-n-h-s-v1')
            meta={'d':[{'n':'scene/'+n+'.dmm','h':'v','s':8} for n in ('a','b')]}
            paths=dict(state_root=root/'state',raw_root=root/'raw',complete=False,max_candidates=1)
            observed=scan_inventory(profile,meta,existing_ids={'scene/a.dmm','scene/b.dmm'},**paths)
            self.assertEqual(observed['metrics']['baseline_only'],2)
            with patch('UnityPy.load',return_value=SimpleNamespace(objects=[obj('MonoBehaviour',1,{'m_Text':'確認'})])):
                first=scan_inventory(profile,meta,fetch_asset=lambda _:b'UnityFS\0test',extractor=UiPrefabExtractor(),**paths)
                second=scan_inventory(profile,meta,fetch_asset=lambda _:b'UnityFS\0test',extractor=UiPrefabExtractor(),**paths)
            self.assertEqual(first['metrics']['deferred'],1)
            self.assertEqual(first['sourceSnapshot']['coverage']['pending_assets'],1)
            self.assertEqual(second['sourceSnapshot']['coverage']['successful_assets'],2)
            self.assertEqual(second['metrics']['payload_network'],1)

if __name__=='__main__':unittest.main()
