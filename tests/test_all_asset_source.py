"""All-lane boundary and failure tests; synthetic counts are not live coverage."""
import json
from pathlib import Path
import tempfile
import os
import threading
import time
import struct
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import httpx
from tools.update.asset_source import (extract_asset, master_candidates, preserve_file,
                                      process_asset, read_json, coverage_report, OwnedDependencies,
                                      dependencies_current, read_object_typetree)
from tools.update.source_http import fetch_file
from tools.update.source_scan import SourceProfile, normalize_inventory, digest, atomic_json
from scripts.crawl_only import parser, run


class Stream(httpx.SyncByteStream):
    def __init__(self, count=18, fail=False):
        self.count, self.fail = count, fail

    def __iter__(self):
        for _ in range(self.count):
            yield b'x' * (1024 * 1024)
        if self.fail:
            raise httpx.ReadError('signed secret URL must not be recorded')


class AllAssetsTests(unittest.TestCase):
    def setUp(self):
        scratch = Path(__file__).parent / 'scratch-all-assets'
        scratch.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=scratch)
        self.root = Path(self.temp.name).resolve()

    def tearDown(self):
        self.temp.cleanup()

    def all_args(self, *extra):
        return parser().parse_args(['--lane', 'All', '--state-root', str(self.root / 'state'),
            '--raw-root', str(self.root / 'raw'), '--output', str(self.root / 'out/scan.json'), *extra])

    @staticmethod
    def catalogs():
        return {name: {'d': [{'n': key, 'h': 'synthetic-token', 's': 12}]}
                for name, key in [('assetbundle', 'notinit/novel_script/sts_00000001.dmm'),
                                  ('master', 'master.dmm'),
                                  ('advvoice', 'notinit/novelchara_main/hmn_00000001_voice.dmm')]}

    def saved_catalog_args(self):
        args = []
        for name, flag in [('assetbundle', '--inventory'), ('master', '--all-master-inventory'),
                           ('advvoice', '--all-advvoice-inventory')]:
            path = self.root / (name + '.json')
            atomic_json(path, self.catalogs()[name])
            args.extend([flag, str(path)])
        return args

    def test_live_all_observes_three_catalogs_and_counts_voice_without_acquisition(self):
        calls, catalogs = [], self.catalogs()
        client_type = httpx.Client
        def handler(request):
            calls.append(request.url.path)
            return httpx.Response(200, json=catalogs[Path(request.url.path).stem])
        with patch('httpx.Client', side_effect=lambda **kw: client_type(transport=httpx.MockTransport(handler), **kw)):
            result = run(self.all_args())
        self.assertEqual(calls, ['/files/manifest/webgl/r18/' + name + '.json' for name in catalogs])
        self.assertEqual(result['inventory']['entries'], 3)
        self.assertTrue(result['inventory']['complete_selection'])
        self.assertEqual(result['inventory']['manifest_coverage']['missing'], [])
        self.assertEqual(result['manifest_results']['advvoice']['statuses'], {'PENDING': 1})
        self.assertEqual(result['status'], 'METADATA_ONLY')
        self.assertFalse(result['full_extraction_complete'])
        self.assertFalse((self.root / 'out/novel-intake.json').exists())

    def test_missing_or_invalid_voice_manifest_replaces_old_success_without_partial_fallback(self):
        client_type = httpx.Client
        for response in (httpx.Response(404), httpx.Response(200, content=b'not-json'),
                         httpx.Response(200, json={'d': 'not-an-array'})):
            with self.subTest(response=response.status_code, content=response.content[:15]):
                atomic_json(self.root / 'out/scan.json', {'status': 'EXTRACTED', 'full_extraction_complete': True})
                def handler(request):
                    name = Path(request.url.path).stem
                    return response if name == 'advvoice' else httpx.Response(200, json=self.catalogs()[name])
                with patch('httpx.Client', side_effect=lambda **kw: client_type(transport=httpx.MockTransport(handler), **kw)):
                    with self.assertRaises(ValueError):
                        run(self.all_args())
                report = read_json(self.root / 'out/scan.json')
                self.assertEqual(report['status'], 'SOURCE_METADATA_OBSERVATION_FAILED')
                self.assertFalse(report['full_extraction_complete'])
                self.assertFalse((self.root / 'state/all-assets/WebGL/assets').exists())

    def test_saved_three_catalog_replay_is_offline_and_filtered_selection_is_not_full(self):
        with patch('tools.update.asset_source.fetch_file', side_effect=AssertionError('unexpected network')):
            result = run(self.all_args(*self.saved_catalog_args(), '--asset-pattern', '*_voice.dmm'))
        self.assertEqual(result['inventory']['entries'], 1)
        self.assertTrue(result['inventory']['manifest_coverage']['complete'])
        self.assertFalse(result['inventory']['complete_selection'])
        self.assertFalse(result['full_extraction_complete'])
        self.assertEqual(result['manifest_results']['assetbundle']['selected'], 0)
        self.assertEqual(result['manifest_results']['advvoice']['selected'], 1)

    def test_all_completion_requires_every_selected_asset_to_be_extracted(self):
        def process(item, **kw):
            return {**item, 'profile': {'platform': 'WebGL'},
                    'status': 'EXTRACTED' if kw['acquire'] else 'PENDING'}
        saved = self.saved_catalog_args()
        with patch('scripts.crypto.private_helper', return_value=SimpleNamespace(ng03_helper_sha256='fixture')):
            with patch('tools.update.asset_source.process_asset', side_effect=process):
                limited = run(self.all_args(*saved, '--acquire', '--max-candidates', '1'))
                complete = run(self.all_args(*saved, '--acquire'))
        self.assertEqual(limited['status'], 'PARTIAL')
        self.assertFalse(limited['full_extraction_complete'])
        self.assertEqual(limited['manifest_results']['advvoice']['statuses'], {'PENDING': 1})
        self.assertTrue(complete['full_extraction_complete'])
        self.assertEqual(complete['manifest_results']['advvoice']['statuses'], {'EXTRACTED': 1})

    def test_conflicting_catalog_keys_are_rejected_and_supplement_is_not_silently_ignored(self):
        saved = self.saved_catalog_args()
        atomic_json(self.root / 'advvoice.json', {'d': [{'n': 'master.dmm', 'h': 'different'}]})
        with self.assertRaisesRegex(ValueError, 'SOURCE_INVENTORY_COLLISION'):
            run(self.all_args(*saved))
        with self.assertRaisesRegex(ValueError, 'SOURCE_ALL_MANIFEST_OPTIONS'):
            run(self.all_args('--all-advvoice-inventory', str(self.root / 'advvoice.json')))

    def test_textasset_media_payloads_and_serialized_fallback_are_quarantined(self):
        samples = [('audio', 'voice.acb', b'@UTF' + 'synthetic 日本語'.encode()),
                   ('audio', 'waves.bytes', b'AFS2' + bytes(20)),
                   ('video', 'movie.usm', b'GC Movie\0' + bytes(20)),
                   ('video', 'movie.bytes', b'CRID' + bytes(20)),
                   ('font', 'font.ttf', b'\0\1\0\0' + bytes(20)),
                   ('image', 'image.bytes', b'\x89PNG\r\n\x1a\n' + bytes(20)),
                   ('spine', 'data.json', b'{"bones":[],"slots":[]}')]
        for number, (category, name, body) in enumerate(samples):
            with self.subTest(category=category, name=name):
                obj = SimpleNamespace(type=SimpleNamespace(name='TextAsset'), path_id=1,
                    assets_file=SimpleNamespace(name='synthetic-CAB'),
                    read=lambda: SimpleNamespace(m_Name=name, m_Script=body), get_raw_data=lambda: b'serialized')
                output = self.root / str(number)
                with patch('UnityPy.load', return_value=SimpleNamespace(objects=[obj])):
                    result = extract_asset(self.root / 'unused', {'asset_key': 'media.dmm', 'vendor_token': 'v'}, output)
                self.assertEqual(result['status'], 'EXTRACTED')
                record = json.loads((output / 'objects.jsonl').read_text())
                self.assertEqual(record['category'], category)
                self.assertEqual(record['content_classification']['disposition'], 'excluded')
                self.assertEqual(len(record['exports']), 2)
                self.assertTrue(all(ref['path'].startswith(category + '/') for ref in record['exports']))
                self.assertEqual((output / 'strings.jsonl').read_text(), '')
                self.assertEqual(next(output.rglob('*.bytes')).read_bytes(), body)

    def test_textasset_extension_alone_does_not_quarantine_text_and_master_still_decodes(self):
        for key, name, body in [('text.dmm', 'dialogue.acb', '表示する文章'.encode()),
                                ('master.dmm', 'mText', b'@UTFsynthetic-encrypted-master')]:
            with self.subTest(key=key):
                obj = SimpleNamespace(type=SimpleNamespace(name='TextAsset'), path_id=1,
                    assets_file=SimpleNamespace(name='synthetic-CAB'),
                    read=lambda: SimpleNamespace(m_Name=name, m_Script=body), get_raw_data=lambda: b'serialized')
                output = self.root / key
                with patch('UnityPy.load', return_value=SimpleNamespace(objects=[obj])):
                    result = extract_asset(self.root / 'unused', {'asset_key': key, 'vendor_token': 'v'}, output,
                        decode_master=lambda raw: [{'id': 1, 'ml_name': ['表示する文章']}], known_master={'mText': {'ml_name[]'}})
                self.assertEqual(result['status'], 'EXTRACTED')
                record = json.loads((output / 'objects.jsonl').read_text())
                self.assertEqual(record['category'], 'text')
                self.assertIn('text/', record['exports'][0]['path'])
                self.assertTrue((output / 'strings.jsonl').read_text())

    def playable_object(self, body, kind='PlayableDirector'):
        from UnityPy.helpers import TypeTreeHelper
        schema = read_json(Path(__file__).parent / 'fixtures/playable_director_unity6000_typetree.json')
        nodes = TypeTreeHelper.check_nodes(schema)
        def native_failure():
            raise ValueError(f"Can't read 4 bytes at position {len(body)} of {len(body)}\n"
                             'Error occured at UnityPyBoost/TypeTreeHelper.c:165:read_SInt32')
        return SimpleNamespace(type=SimpleNamespace(name=kind), path_id=1,
            assets_file=SimpleNamespace(name='CAB-synthetic'), serialized_type=SimpleNamespace(nodes=nodes),
            reader=SimpleNamespace(endian='<'), read_typetree=native_failure, get_raw_data=lambda: body)

    @staticmethod
    def playable_bytes():
        # Entirely synthetic pointers, flags and empty collections; no game bytes.
        return (struct.pack('<iqB3x', 0, 101, 1) + struct.pack('<iq', 0, 202)
                + struct.pack('<iiidii', 0, 0, 1, 0.0, 0, 0))

    def test_playable_native_regression_recovers_exact_bytes_without_global_switch(self):
        from UnityPy.helpers import TypeTreeHelper
        native = TypeTreeHelper.read_typetree_c
        if native is None:
            self.skipTest('UnityPyBoost is unavailable; synthetic fallback guards are tested separately')
        body = self.playable_bytes()
        obj = self.playable_object(body)
        obj.read_typetree = lambda: native(obj.serialized_type.nodes, body, '<')
        with self.assertRaisesRegex(ValueError, "Can't read 4 bytes"):
            obj.read_typetree()
        output = self.root / 'playable'
        with patch('UnityPy.load', return_value=SimpleNamespace(objects=[obj])):
            result = extract_asset(self.root / 'raw', {'asset_key': 'synthetic.dmm', 'vendor_token': 'v'}, output)
        self.assertEqual(result['status'], 'EXTRACTED')
        self.assertEqual(result['counts']['recovered_playable_directors'], 1)
        self.assertEqual(result['counts'].get('failures', 0), 0)
        self.assertIs(TypeTreeHelper.read_typetree_c, native)
        record = json.loads((output / 'objects.jsonl').read_text())
        receipt = record['typetree_recovery']
        self.assertEqual(receipt['status'], 'RECOVERED')
        self.assertEqual(receipt['consumed_bytes'], len(body))
        self.assertTrue(receipt['roundtrip_equal'])
        self.assertEqual(receipt['original_failure']['code'], 'UNITYPYBOOST_OBJECT_END_OVERRUN')
        self.assertEqual(next(output.rglob('*.serialized')).read_bytes(), body)
        self.assertEqual((output / 'failures.jsonl').read_text(), '')
        self.assertEqual((output / 'strings.jsonl').read_text(), '')

    def test_playable_fallback_rejects_other_types_schemas_and_error_signatures(self):
        for variant in ('type', 'schema', 'no_embedded_schema', 'error'):
            with self.subTest(variant=variant):
                obj = self.playable_object(self.playable_bytes())
                if variant == 'type':
                    obj.type.name = 'MonoBehaviour'
                elif variant == 'schema':
                    obj.serialized_type.nodes[0].m_Version += 1
                elif variant == 'no_embedded_schema':
                    obj.serialized_type.nodes = []
                else:
                    obj.read_typetree = lambda: (_ for _ in ()).throw(ValueError('different error'))
                receipt = {}
                with self.assertRaises(ValueError):
                    read_object_typetree(obj, receipt)
                self.assertEqual(receipt, {})

    def test_playable_fallback_rejects_truncation_trailing_bytes_padding_and_nonempty_map(self):
        body = self.playable_bytes()
        padding = bytearray(body)
        padding[13] = 7
        populated = body[:-4] + struct.pack('<ii', 1, 1) + b'x\0\0\0' + struct.pack('<iq', 0, 303)
        for candidate, code in [(body[:-1], 'READ_FAILED'), (body + bytes(4), 'LENGTH'),
                                (bytes(padding), 'ROUNDTRIP'), (populated, 'SCOPE')]:
            with self.subTest(code=code):
                receipt = {}
                with self.assertRaisesRegex(ValueError, 'SOURCE_PLAYABLE_PYTHON_' + code):
                    read_object_typetree(self.playable_object(candidate), receipt)
                self.assertEqual(receipt['status'], 'REJECTED')
                self.assertIn('original_failure', receipt)

    @unittest.skipUnless(os.name == 'nt', 'Windows file-sharing regression')
    def test_atomic_state_write_survives_a_concurrent_status_reader(self):
        path = self.root / 'state.json'
        atomic_json(path, {'version': 'old'})
        errors = []
        def writer():
            try:
                atomic_json(path, {'version': 'new'})
            except Exception as error:
                errors.append(error)
        with path.open('rb') as reader:
            thread = threading.Thread(target=writer)
            thread.start()
            time.sleep(0.12)
            self.assertEqual(json.loads(reader.read()), {'version': 'old'})
        thread.join(timeout=3)
        self.assertFalse(thread.is_alive())
        self.assertEqual(errors, [])
        self.assertEqual(read_json(path), {'version': 'new'})

    def test_unlimited_inventory_keeps_all_paths_and_full_path_identity(self):
        rows = [{'n': f'folder/{i}.dmm', 'h': 'v', 's': 1} for i in range(100001)]
        rows += [{'n': f'notinit/{folder}/sts_10011201.dmm', 'h': 'v'} for folder in ('a', 'b')]
        with self.assertRaisesRegex(ValueError, 'SOURCE_INVENTORY_SCHEMA'):
            normalize_inventory({'d': rows}, 'UI')
        result = normalize_inventory({'d': rows}, 'All', max_entries=None)
        self.assertEqual(len(result['items']), len(rows))
        self.assertFalse(result['collisions'])
        with self.assertRaisesRegex(ValueError, 'SOURCE_ASSET_PATH'):
            normalize_inventory({'d': [{'n': '../escape', 'h': 'v'}]}, 'All', max_entries=None)

    def test_stream_above_16mib_and_failed_attempt_preserved_without_secrets(self):
        calls = []
        def handler(request):
            calls.append(request)
            return httpx.Response(200, stream=Stream(2, True) if len(calls) == 1 else Stream())
        target = self.root / 'large.download'
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            result = fetch_file(client, 'https://cdn-r18.gc.dmmgames.com/data', target, sleeper=lambda _: None)
        self.assertEqual(result['size'], 18 * 1024 * 1024)
        self.assertEqual(target.with_name(target.name + '.attempt-0.partial').stat().st_size, 2 * 1024 * 1024)
        receipt = read_json(target.with_name(target.name + '.attempt-0.json'))
        self.assertNotIn('secret', json.dumps(receipt))
        ref = preserve_file(self.root, target)
        self.assertEqual(ref['size'], result['size'])
        self.assertEqual(ref['sha256'], result['sha256'])

    def test_redirect_size_and_cancel_do_not_publish_complete_file(self):
        for code, response, extra in [
            ('SOURCE_REDIRECT_REJECTED', httpx.Response(302), {}),
            ('SOURCE_HTTP_SIZE', httpx.Response(200, content=b'large'), {'max_bytes': 2}),
            ('SOURCE_CANCELLED', httpx.Response(200, content=b'a'), {'cancelled': lambda: True})]:
            target = self.root / code
            with httpx.Client(transport=httpx.MockTransport(lambda _: response)) as client:
                with self.assertRaisesRegex(ValueError, code):
                    fetch_file(client, 'https://cdn-r18.gc.dmmgames.com/data', target, **extra)
            self.assertFalse(target.exists())

    def test_spine_is_quarantined_ui_and_unreviewed_strings_are_distinct(self):
        def obj(kind, identity, tree=None, body=None, fail=False):
            def read_tree():
                if fail:
                    raise ValueError('private payload should not enter receipt')
                return tree
            return SimpleNamespace(type=SimpleNamespace(name=kind), path_id=identity,
                assets_file=SimpleNamespace(name='CAB'), read_typetree=read_tree,
                read=lambda: SimpleNamespace(m_Name='skeleton', m_Script=body), get_raw_data=lambda: b'raw-object')
        objects = [obj('TextAsset', 1, body=b'{"bones":[],"slots":[],"name":"Spine"}'),
                   obj('MonoBehaviour', 2, tree={'m_text': '確認', 'internal': '未承認'}),
                   obj('MonoBehaviour', 3, fail=True)]
        with patch('UnityPy.load', return_value=SimpleNamespace(objects=objects)):
            report = extract_asset(self.root / 'raw', {'asset_key': 'unselected/new.dmm', 'vendor_token': 'v'}, self.root / 'extract')
        self.assertEqual(report['status'], 'PARTIAL')
        rows = [json.loads(line) for line in (self.root / 'extract/strings.jsonl').read_text('utf-8').splitlines()]
        self.assertEqual({r['kind'] for r in rows}, {'ui', 'typetree'})
        self.assertTrue(list((self.root / 'extract/spine').rglob('*.bytes')))
        self.assertNotIn('private payload', (self.root / 'extract/failures.jsonl').read_text())
        self.assertEqual(report['counts']['objects'], 3)

    def test_master_keeps_unknown_fields_separate_and_uses_only_first_locale(self):
        source = [{'id': 1, 'ml_name': ['名前', 'legacy translation'], 'new_label': '未知'}]
        rows = list(master_candidates('mNew', source, {}, {'ml_name[]': {}}))
        self.assertEqual([r['jp'] for r in rows], ['名前', '未知'])
        self.assertEqual(rows[0]['mapping'], 'existing_runtime_field')
        self.assertEqual(rows[1]['mapping'], 'unreviewed_field')

    def test_empty_dynamic_font_texture_preserves_raw_without_inventing_pixels(self):
        data = SimpleNamespace(m_Width=0, m_Height=0, m_ImageData=b'',
                               m_StreamData=SimpleNamespace(size=0, path=''))
        obj = SimpleNamespace(type=SimpleNamespace(name='Texture2D'), path_id=1,
            assets_file=SimpleNamespace(name='CAB'), read=lambda: data, get_raw_data=lambda: b'empty-atlas-object')
        output = self.root / 'empty-image'
        with patch('UnityPy.load', return_value=SimpleNamespace(objects=[obj])):
            result = extract_asset(self.root / 'raw', {'asset_key': 'fonts.dmm', 'vendor_token': 'v'}, output)
        self.assertEqual(result['status'], 'EXTRACTED')
        self.assertEqual(result['counts']['empty_image_objects'], 1)
        self.assertFalse(list(output.rglob('*.png')))
        self.assertEqual(next(output.rglob('*.serialized')).read_bytes(), b'empty-atlas-object')

    def test_external_sprite_uses_current_owned_raw_and_preserves_missing_attempt(self):
        profile = SourceProfile('WebGL', 'R18', 'All', 'vendor-d-n-h-s-v1')
        state = self.root / 'state'
        (state / 'assets/00').mkdir(parents=True)
        raw = self.root / 'raw'
        raw.mkdir()
        (raw / 'dependency.bytes').write_bytes(b'owned texture')
        index = raw / 'objects.jsonl'
        index.write_text(json.dumps({'locator': {'container': 'CAB-dependency'}}) + '\n')
        record = {'asset_key': 'texture.dmm', 'vendor_token': 'current',
                  'profile': profile.__dict__, 'raw': {'path': 'dependency.bytes', 'size': 13,
                  'sha256': digest(b'owned texture')}, 'extraction': {'indexes': {'objects':
                  {'path': index.name, 'size': index.stat().st_size, 'sha256': digest(index.read_bytes())}}}}
        atomic_json(state / 'assets/00/stale.json', {**record, 'vendor_token': 'stale'})
        atomic_json(state / 'assets/00/other-profile.json', {**record, 'profile': {**profile.__dict__, 'platform': 'PC'}})
        env = SimpleNamespace(objects=[], loaded=False, loads=[])
        env.get_cab = lambda name: SimpleNamespace(externals=[SimpleNamespace(name='unrelated-missing-cab')]) if env.loaded and name.lower() == 'cab-dependency' else None
        def load(path):
            self.assertEqual(Path(path).read_bytes(), b'owned texture')
            env.loaded = True
            env.loads.append(path)
            env.objects.append(SimpleNamespace(type=SimpleNamespace(name='DependencyOnly')))
        env.load_file = load
        class Sprite:
            @property
            def image(self):
                if not env.loaded:
                    raise AttributeError('external texture is absent')
                return SimpleNamespace(save=lambda path: path.write_bytes(b'png'))
        obj = SimpleNamespace(type=SimpleNamespace(name='Sprite'), path_id=1,
            assets_file=SimpleNamespace(name='CAB-sprite', externals=[SimpleNamespace(name='CAB-dependency')]),
            read=lambda: Sprite(), get_raw_data=lambda: b'serialized sprite')
        env.objects.append(obj)
        loader = OwnedDependencies(state, raw, profile, {'texture.dmm': 'current'})
        item = {'asset_key': 'sprite.dmm', 'vendor_token': 's'}
        with patch('UnityPy.load', return_value=env):
            first = extract_asset(raw / 'sprite', item, self.root / 'first', dependency_loader=loader)
            self.assertEqual(first['status'], 'PARTIAL')
            self.assertEqual(first['counts']['missing_dependency_objects'], 1)
            atomic_json(state / 'assets/00/current.json', record)
            loader.refresh()
            second = extract_asset(raw / 'sprite', item, self.root / 'second', dependency_loader=loader)
        self.assertEqual(second['status'], 'EXTRACTED')
        self.assertEqual(second['objects'], 1)  # Dependency objects are not exported or counted twice.
        self.assertEqual(len(env.loads), 1)
        self.assertEqual(second['dependencies'][0]['asset_key'], 'texture.dmm')
        self.assertIn('SOURCE_DEPENDENCY_UNAVAILABLE', (self.root / 'first/failures.jsonl').read_text())
        self.assertTrue(dependencies_current({'extraction': second}, {'texture.dmm': 'current'}))
        self.assertFalse(dependencies_current({'extraction': second}, {'texture.dmm': 'next'}))

    def test_metadata_all_does_not_bootstrap_skip_or_publish_novel_intake(self):
        inventory = self.root / 'inventory.json'
        inventory.write_text(json.dumps({'d': [{'n': 'outside/any.dmm', 'h': 'v', 's': 20000000}]}))
        args = parser().parse_args(['--lane', 'All', '--inventory', str(inventory),
            '--state-root', str(self.root / 'state'), '--raw-root', str(self.root / 'raw'),
            '--output', str(self.root / 'out' / 'scan.json')])
        result = run(args)
        self.assertEqual(args.max_candidates, 0)
        self.assertEqual(result['status'], 'METADATA_ONLY')
        self.assertEqual(result['coverage']['asset_statuses'], {'PENDING': 1})
        self.assertEqual(result['raw_assets'], 0)
        self.assertFalse(result['inventory']['complete_selection'])
        self.assertEqual(result['inventory']['manifest_coverage']['missing'], ['master', 'advvoice'])
        self.assertFalse(result['full_extraction_complete'])
        self.assertFalse((self.root / 'out' / 'novel-intake.json').exists())
        self.assertEqual(parser().parse_args([]).lane, 'Novel')
        args.integrity = True
        with self.assertRaisesRegex(ValueError, 'SOURCE_ALL_INTEGRITY_REQUIRES_ACQUIRE'):
            run(args)

    def test_coverage_counts_unique_display_and_japanese_separately(self):
        package = self.root / 'package'
        package.mkdir()
        (package / 'master.json').write_text(json.dumps({'mText': {'label': {'MENU': '메뉴'}}}), encoding='utf-8')
        (package / 'ui.json').write_text(json.dumps({'確認': '확인'}), encoding='utf-8')
        records = []
        for number, (asset, values) in enumerate([('a/scene.dmm', ['確認', '99']), ('outside/b.dmm', ['確認', '新規', '新規'])]):
            path = self.root / f'{number}.jsonl'
            path.write_text(''.join(json.dumps({'jp': value, 'candidate': True, 'kind': 'ui', 'locator': {}, 'mapping': 'serialized_display_field'}) + '\n' for value in values))
            records.append({'asset_key': asset, 'status': 'EXTRACTED', 'profile': {'platform': 'WebGL'},
                'extraction': {'counts': {}, 'indexes': {'strings': {'path': path.name, 'size': path.stat().st_size, 'sha256': ''}}}})
        path = self.root / 'master.jsonl'
        path.write_text(''.join(json.dumps({'jp': value, 'candidate': False, 'kind': 'master',
            'locator': {}, 'mapping': mapping, 'translation_target': {'table': 'mText', 'field': 'label'}}) + '\n'
            for value, mapping in [('MENU', 'existing_runtime_field'), ('internal/path', 'unreviewed_field')]))
        records.append({'asset_key': 'master.dmm', 'status': 'EXTRACTED', 'profile': {'platform': 'WebGL'},
            'extraction': {'counts': {}, 'indexes': {'strings': {'path': path.name, 'size': path.stat().st_size, 'sha256': ''}}}})
        report = coverage_report(records, self.root, package, self.root / 'scan.json', {'include': ['a/*']})
        self.assertEqual(report['unique_candidates']['ui'], 3)
        self.assertEqual(report['comparison']['ui_present'], 1)
        self.assertEqual(report['comparison']['ui_japanese_missing'], 1)
        self.assertEqual(report['comparison']['ui_non_japanese_missing'], 1)
        self.assertEqual(report['comparison']['ui_outside_previous_scope_present'], 1)
        self.assertEqual(report['expanded_ui_scope']['additional_japanese_missing_current'], 1)
        self.assertEqual(report['unique_candidates']['master'], 1)
        self.assertEqual(report['comparison']['master_non_japanese_present'], 1)

    def test_failed_extraction_retries_owned_raw_and_changed_token_keeps_history(self):
        source = self.root / 'local.dmm'
        source.write_bytes(b'first')
        item = {'asset_key': 'local.dmm', 'vendor_token': digest(b'first'), 'vendor_size': 5, 'source_id': 'local.dmm'}
        options = dict(profile=SourceProfile('PC', 'R18', 'All', 'pc-explicit-local-cache-v1'),
            state_root=self.root / 'state', raw_root=self.root / 'raw', client=None, acquire=True,
            integrity=False, version='fixture-v1', decode_master=None, known_master={}, max_objects=0, local_asset=source)
        with patch('tools.update.asset_source.extract_asset', side_effect=RuntimeError('private')):
            first = process_asset(item, **options)
        self.assertEqual(first['status'], 'FAILED')
        self.assertTrue((self.root / 'raw' / first['raw']['path']).exists())
        source.unlink()
        def success(raw, item, output, **kwargs):
            output.mkdir(parents=True)
            for name in ('strings', 'failures', 'objects'):
                (output / (name + '.jsonl')).write_text('')
            return {'status': 'EXTRACTED', 'counts': {}, 'objects': 0}
        with patch('tools.update.asset_source.extract_asset', side_effect=success):
            second = process_asset(item, **options)
        self.assertEqual(second['status'], 'EXTRACTED')
        self.assertTrue(second['attempts'])
        source.write_bytes(b'new')
        changed = {**item, 'vendor_token': digest(b'new'), 'vendor_size': 3}
        with patch('tools.update.asset_source.extract_asset', side_effect=success):
            process_asset(changed, **options)
        self.assertEqual(len(list((self.root / 'state/history').glob('*/*.json'))), 1)
        self.assertTrue((self.root / 'raw' / first['raw']['path']).exists())


if __name__ == '__main__':
    unittest.main()
