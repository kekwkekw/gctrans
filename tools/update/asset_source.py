"""Opt-in all-asset acquisition, private extraction and current coverage census.

Uses the existing source identity, HTTP, crypto and custody contracts. This lane
does not publish Novel intake, translations, Knowledge or runtime artifacts.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import asdict
from fnmatch import fnmatchcase
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import uuid
from concurrent.futures import ThreadPoolExecutor, wait, FIRST_COMPLETED
import threading

from .process_lock import ProcessFileLock
from .source_scan import (SourceProfile, atomic_json, check_private_root, digest,
                          json_bytes, normalize_inventory, now, NOVEL_PATTERN, safe_key)
from .source_http import fetch_file
from .ui_source import text_rows

JAPANESE = re.compile(r'[\u3040-\u30ff\u3400-\u9fff]')
SPINE = re.compile(r'(?:spine|skeleton|\.skel|\.atlas)', re.I)
VERSION = 'all-assets-v1'
PLAYABLE_SCHEMA_SHA256 = 'b541d09cbbcf3dcfb069f13d5c5cc4ba3b1fff92e801c28b8479b919c2ca9d0d'
PLAYABLE_RECOVERY = 'unitypy-playable-python-v1'


def file_hash(path):
    sha = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(chunk)
    return sha.hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write_row(stream, value):
    stream.write(json.dumps(value, ensure_ascii=True, separators=(',', ':')) + '\n')


def strings(value, path='$'):
    """Iterative walk: no object/string count cap and no implicit depth truncation."""
    pending = [(path, value)]
    while pending:
        field, node = pending.pop()
        if isinstance(node, str):
            if node.strip():
                yield field, node
        elif isinstance(node, dict):
            pending.extend((field + '[' + json.dumps(str(k), ensure_ascii=True) + ']', v) for k, v in node.items())
        elif isinstance(node, (list, tuple)):
            pending.extend((field + f'[{i}]', v) for i, v in enumerate(node))


def is_spine(name, body=None):
    return bool(SPINE.search(name) or (isinstance(body, dict) and
                ('skeleton' in body or ('bones' in body and ('slots' in body or 'animations' in body)))))


def safe_error(error, fallback='SOURCE_EXTRACTION_FAILED'):
    code = str(error)
    return code if re.fullmatch(r'(SOURCE|MASTER|UI)_[A-Z0-9_]{1,80}', code) else fallback


def owned_file(root, ref, *, verify=True):
    path = Path(root) / safe_key(ref['path'])
    check_private_root(path.parent)
    if path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction()):
        raise ValueError('SOURCE_REPARSE_PATH')
    if path.stat().st_size != ref['size'] or (verify and file_hash(path) != ref['sha256']):
        raise ValueError('SOURCE_RAW_IDENTITY')
    return path


def preserve_file(root, path):
    """Publish a content-addressed file without loading its bytes into memory."""
    sha, size = file_hash(path), path.stat().st_size
    relative = f'bundles/{sha[:2]}/{sha}.bytes'
    target = Path(root) / relative
    check_private_root(target.parent)
    with ProcessFileLock(Path(root) / 'blob-locks' / (sha + '.lock')):
        if target.exists():
            if target.is_symlink():
                raise ValueError('SOURCE_REPARSE_PATH')
            if target.stat().st_size == size and file_hash(target) == sha:
                path.unlink()
                return {'path': relative, 'sha256': sha, 'size': size}
            quarantine = check_private_root(Path(root) / 'quarantine' / 'bundles')
            os.replace(target, quarantine / (sha + '.' + uuid.uuid4().hex + '.bytes'))
        os.replace(path, target)
    return {'path': relative, 'sha256': sha, 'size': size}


def dependencies_current(record, tokens):
    return all((tokens or {}).get(dep['asset_key']) == dep['vendor_token']
               for dep in record.get('extraction', {}).get('dependencies', []))


class OwnedDependencies:
    """Resolve external CABs only from owned raw in this current profile/inventory."""
    def __init__(self, state_root, raw_root, profile, tokens):
        self.state_root, self.raw_root = state_root, raw_root
        self.profile, self.tokens = asdict(profile), tokens
        self.index, self.lock = None, threading.Lock()

    def refresh(self):
        self.index = None

    def __call__(self, env, names):
        with self.lock:
            if self.index is None:
                index = {}
                for path in (self.state_root / 'assets').glob('*/*.json'):
                    state = read_json(path)
                    if state.get('profile') != self.profile or self.tokens.get(state['asset_key']) != state['vendor_token']:
                        continue
                    ref = state.get('extraction', {}).get('indexes', {}).get('objects')
                    if not ref or not state.get('raw'):
                        continue
                    with owned_file(self.raw_root, ref).open(encoding='utf-8') as stream:
                        containers = {json.loads(line)['locator']['container'].lower() for line in stream}
                    dependency = {k: state[k] for k in ('asset_key', 'vendor_token', 'raw')}
                    for container in containers:
                        if container in index and index[container] != dependency:
                            index[container] = None  # An ambiguous CAB must not pick arbitrary bytes.
                        else:
                            index[container] = dependency
                self.index = index
        loaded = getattr(env, '_gc_owned_dependencies', {})
        env._gc_owned_dependencies = loaded
        pending, visited = list(names), set()
        while pending:
            name = pending.pop().replace('\\', '/').rsplit('/', 1)[-1].lower()
            if name in visited or env.get_cab(name) is not None:
                continue
            visited.add(name)
            if name not in self.index:
                raise ValueError('SOURCE_DEPENDENCY_UNAVAILABLE')
            dependency = self.index[name]
            if dependency is None:
                raise ValueError('SOURCE_DEPENDENCY_AMBIGUOUS')
            env.load_file(str(owned_file(self.raw_root, dependency['raw'])))
            loaded[dependency['asset_key']] = dependency
            cab = env.get_cab(name)
            if cab is None:
                raise ValueError('SOURCE_DEPENDENCY_UNAVAILABLE')
            # A referenced CAB may also contain unrelated scene dependencies.
            # Loading those preemptively can reject an already resolved texture.
        return list(loaded.values())


def master_candidates(table, source, locator, known_fields):
    """Keep every string separately; only first-language scalar/array values
    can contribute prospective runtime keys. Unknown mappings stay unapproved.
    """
    records = [source] if isinstance(source, dict) else source
    if not isinstance(records, list):
        return
    for index, record in enumerate(records):
        if not isinstance(record, dict):
            continue
        for field, value in record.items():
            declared = field + '[]' if isinstance(value, list) else field
            jp = value[0] if isinstance(value, list) and value else value
            if not isinstance(jp, str) or not jp.strip():
                continue
            # Preserve the established runtime delimiter mapping where declared.
            if field + '|' in known_fields:
                declared, jp = field + '|', jp.partition('|')[0]
            yield {'jp': jp, 'kind': 'master', 'locator': {**locator, 'table': table,
                   'record_index': index, 'record': record.get('id'), 'field': field},
                   'translation_target': {'table': table, 'field': declared},
                   'mapping': 'existing_runtime_field' if declared in known_fields else 'unreviewed_field',
                   'candidate': bool(JAPANESE.search(jp))}


def read_object_typetree(obj, recovery):
    """Keep the native path; narrowly recover the audited empty-reference map.

    Use UnityPy's existing Python reader/writer on an isolated object buffer.
    Never disable the shared C reader, patch a schema, or accept unread bytes.
    """
    try:
        return obj.read_typetree()
    except ValueError as original:
        if obj.type.name != 'PlayableDirector':
            raise
        nodes = getattr(getattr(obj, 'serialized_type', None), 'nodes', None)
        if not nodes:
            raise
        signature = [{k: getattr(n, k) for k in
                      ('m_Level', 'm_Type', 'm_Name', 'm_ByteSize', 'm_MetaFlag', 'm_Version')} for n in nodes]
        schema_sha = digest(json.dumps(signature, sort_keys=True).encode())
        error = re.fullmatch(r"Can't read 4 bytes at position (\d+) of (\d+)\n"
                             r'Error occured at UnityPyBoost/TypeTreeHelper\.c:\d+:read_SInt32', str(original))
        if schema_sha != PLAYABLE_SCHEMA_SHA256 or not error:
            raise
        raw = obj.get_raw_data()
        if int(error[1]) != len(raw) or int(error[2]) != len(raw):
            raise

    from ctypes import c_uint32
    from UnityPy.helpers import TypeTreeHelper
    from UnityPy.streams import EndianBinaryReader, EndianBinaryWriter
    recovery.update(policy=PLAYABLE_RECOVERY, status='REJECTED', schema_sha256=schema_sha,
        raw_object_sha256=digest(raw), object_bytes=len(raw),
        original_failure={'exception_type': 'ValueError', 'code': 'UNITYPYBOOST_OBJECT_END_OVERRUN',
                          'read_bytes': 4, 'offset': len(raw), 'object_bytes': len(raw)})
    reader = EndianBinaryReader(raw, endian=obj.reader.endian)
    try:
        tree = TypeTreeHelper.read_value(nodes, reader, c_uint32(0))
    except Exception as error:
        raise ValueError('SOURCE_PLAYABLE_PYTHON_READ_FAILED') from error
    finally:
        recovery['consumed_bytes'] = reader.Position
    if reader.Position != len(raw):
        raise ValueError('SOURCE_PLAYABLE_PYTHON_LENGTH')
    if tree.get('m_ExposedReferences', {}).get('m_References') != []:
        raise ValueError('SOURCE_PLAYABLE_PYTHON_SCOPE')
    try:
        encoded = TypeTreeHelper.write_typetree(tree, nodes, EndianBinaryWriter(endian=obj.reader.endian)).bytes
    except Exception as error:
        raise ValueError('SOURCE_PLAYABLE_PYTHON_WRITE_FAILED') from error
    if encoded != raw:
        raise ValueError('SOURCE_PLAYABLE_PYTHON_ROUNDTRIP')
    recovery.update(status='RECOVERED', roundtrip_equal=True)
    return tree


def extract_asset(raw_path, item, output, *, decode_master=None, known_master=None, max_objects=0, cancelled=lambda: False,
                  dependency_loader=None):
    """Best effort per object; every failure retains bundle, object and stage.

    Native TextAsset bytes, image PNG, decoded audio, typetrees and raw fallback
    objects are exported. No OCR or code/dynamic sentence reconstruction.
    """
    import UnityPy
    output = check_private_root(output)
    folders = {}
    def folder_for(category):
        if category not in folders:
            folders[category] = check_private_root(output / category)
        return folders[category]
    counts = Counter()
    dependencies = {}
    candidate_path = output / 'strings.jsonl'
    failure_path = output / 'failures.jsonl'
    object_path = output / 'objects.jsonl'
    with candidate_path.open('w', encoding='utf-8') as candidates, failure_path.open('w', encoding='utf-8') as failures, object_path.open('w', encoding='utf-8') as objects:
        def failure(locator, stage, error):
            row = {'locator': locator, 'stage': stage, 'code': safe_error(error), 'exception_type': type(error).__name__}
            write_row(failures, row)
            counts['failures'] += 1

        def emit(row):
            row.update(asset_key=item['asset_key'], vendor_token=item['vendor_token'])
            write_row(candidates, row)
            counts['strings'] += 1
            counts[row['kind'] + '_strings'] += 1

        try:
            env = UnityPy.load(str(raw_path))
            all_objects = list(env.objects)
            if not all_objects:
                raise ValueError('SOURCE_UNITY_OBJECTS_UNAVAILABLE')
            if max_objects and len(all_objects) > max_objects:
                raise ValueError('SOURCE_OBJECT_LIMIT')
        except Exception as error:
            failure({'asset_key': item['asset_key']}, 'bundle', error)
            return {'status': 'UNSUPPORTED', 'counts': dict(counts), 'objects': 0}
        novel = bool(NOVEL_PATTERN.fullmatch(item['asset_key']))
        bundle_spine = is_spine(item['asset_key'])
        names = {}
        for obj in all_objects:
            if obj.type.name == 'GameObject':
                try:
                    names[(str(obj.assets_file.name), obj.path_id)] = obj.read().m_Name
                except Exception:
                    pass
        for number, obj in enumerate(all_objects):
            if cancelled():
                raise ValueError('SOURCE_CANCELLED')
            kind, identity = obj.type.name, obj.path_id
            locator = {'container': str(obj.assets_file.name), 'path_id': identity, 'type': kind}
            stem = f'{number:07d}_{identity}'
            category = 'spine' if bundle_spine else 'image' if kind in ('Sprite', 'Texture2D') else 'audio' if kind == 'AudioClip' else 'text' if kind == 'TextAsset' else 'other'
            folder = folder_for(category)
            record = {'locator': locator, 'category': category, 'exports': []}

            def export_bytes(body, suffix, target_folder=folder):
                path = target_folder / (stem + suffix)
                path.write_bytes(body)
                record['exports'].append({'path': path.relative_to(output).as_posix(), 'size': len(body), 'sha256': digest(body)})

            def export_tree(tree, suffix='.json', target_folder=folder):
                # Unity typetrees can contain opaque bytes. The raw fallback below
                # preserves these, rather than coercing them to invented strings.
                export_bytes((json.dumps(tree, ensure_ascii=True, default=lambda b: {'binary_bytes': len(b)} if isinstance(b, (bytes, bytearray)) else str(type(b))) + '\n').encode(), suffix, target_folder)

            try:
                if kind == 'TextAsset':
                    data = obj.read()
                    body = data.m_Script
                    body = body.encode('utf-8', 'surrogateescape') if isinstance(body, str) else bytes(body)
                    name = data.m_Name
                    locator['name'] = name
                    decoded = None
                    try:
                        decoded = json.loads(body)
                    except (ValueError, UnicodeError):
                        pass
                    spine = bundle_spine or is_spine(name, decoded)
                    if spine:
                        category = record['category'] = 'spine'
                        folder = folder_for(category)
                    export_bytes(body, '.bytes', folder)
                    if item['asset_key'] == 'master.dmm':
                        if decode_master is None:
                            raise ValueError('SOURCE_PRIVATE_HELPER_REQUIRED')
                        decoded = decode_master(body)
                        export_tree(decoded, '.decoded.json', folder)
                        for row in master_candidates(name, decoded, locator, (known_master or {}).get(name, {})):
                            emit(row)
                        # Full decoded table is retained, including unknown fields,
                        # nested strings and language tails, outside coverage input.
                        counts['decoded_master_tables'] += 1
                    elif spine:
                        counts['quarantined_spine'] += 1
                    elif novel:
                        counts['novel_textassets'] += 1
                    else:
                        if decoded is None:
                            try:
                                decoded = body.decode('utf-8-sig')
                            except UnicodeError:
                                counts['binary_textassets'] += 1
                        for field, value in strings(decoded):
                            emit({'jp': value, 'kind': 'textasset', 'locator': {**locator, 'field': field},
                                  'candidate': bool(JAPANESE.search(value)), 'mapping': 'unreviewed_text'})
                elif kind in ('Sprite', 'Texture2D'):
                    data = obj.read()
                    stream = getattr(data, 'm_StreamData', None)
                    empty = (kind == 'Texture2D' and data.m_Width == 0 and data.m_Height == 0
                             and not getattr(data, 'm_ImageData', b'') and not getattr(stream, 'size', 0))
                    if empty:
                        # Dynamic font atlases legitimately contain no pixels.
                        # UnityPy otherwise tries to open the empty resource path
                        # as a directory and reports a misleading PermissionError.
                        record['image_status'] = 'EMPTY_TEXTURE_NO_PIXELS'
                        counts['empty_image_objects'] += 1
                    else:
                        path = folder / (stem + '.png')
                        try:
                            bitmap = data.image
                        except Exception:
                            externals = getattr(obj.assets_file, 'externals', [])
                            if kind != 'Sprite' or not externals or dependency_loader is None:
                                raise
                            for dependency in dependency_loader(env, [e.name for e in externals]):
                                dependencies[dependency['asset_key']] = dependency
                            bitmap = data.image
                        bitmap.save(path)
                        record['exports'].append({'path': path.relative_to(output).as_posix(), 'size': path.stat().st_size, 'sha256': file_hash(path)})
                        counts['exported_images'] += 1
                elif kind == 'AudioClip':
                    samples = obj.read().samples
                    if not samples:
                        raise ValueError('SOURCE_AUDIO_DECODER_UNAVAILABLE')
                    for index, (name, body) in enumerate(samples.items()):
                        export_bytes(body, f'.{index}.audio')
                    counts['exported_audio_clips'] += 1
                else:
                    recovery = {}
                    try:
                        tree = read_object_typetree(obj, recovery)
                    finally:
                        if recovery:
                            record['typetree_recovery'] = recovery
                    if recovery.get('status') == 'RECOVERED':
                        counts['recovered_playable_directors'] += 1
                    export_tree(tree)
                    if not bundle_spine and not novel:
                        pointer = tree.get('m_GameObject') if isinstance(tree, dict) else None
                        if kind == 'MonoBehaviour' and isinstance(pointer, dict):
                            tree['_display_object_name'] = names.get((locator['container'], pointer.get('m_PathID')), '')
                        ui = text_rows([(locator['container'], identity, tree)], item['asset_key']) if kind == 'MonoBehaviour' else []
                        display = {r['locator']['field'] for r in ui}
                        for row in ui:
                            emit({**row, 'kind': 'ui', 'candidate': True, 'mapping': 'serialized_display_field'})
                        for field, value in strings(tree):
                            if field in {'$[' + json.dumps(f) + ']' for f in display}:
                                continue
                            emit({'jp': value, 'kind': 'typetree', 'locator': {**locator, 'field': field},
                                  'candidate': bool(JAPANESE.search(value)), 'mapping': 'unreviewed_string_field'})
                counts['decoded_objects'] += 1
            except Exception as error:
                failure(locator, 'object_export', error)
                if safe_error(error) in ('SOURCE_DEPENDENCY_UNAVAILABLE', 'SOURCE_DEPENDENCY_AMBIGUOUS'):
                    counts['missing_dependency_objects'] += 1
                record['status'] = 'PARTIAL_OR_UNSUPPORTED'
            # Lossless serialized object fallback also preserves fields absent
            # from PNG/audio/typetree exports. It is not a translated artifact.
            try:
                export_bytes(obj.get_raw_data(), '.serialized', folder)
            except Exception as error:
                failure(locator, 'serialized_object', error)
            write_row(objects, record)
            counts['objects'] += 1
            counts['category_' + category] += 1
        dependencies.update(getattr(env, '_gc_owned_dependencies', {}))
        return {'status': 'PARTIAL' if counts['failures'] else 'EXTRACTED', 'counts': dict(counts),
                'objects': len(all_objects), 'dependencies': list(dependencies.values()),
                'recovery_policy': PLAYABLE_RECOVERY}


def extractor_version(decoder_sha, max_objects, known_master):
    import UnityPy
    mapping = digest(json_bytes({table: sorted(fields) for table, fields in known_master.items()}))
    return f'{VERSION}:UnityPy-{UnityPy.__version__}:decoder-{decoder_sha}:objects-{max_objects}:fields-{mapping}'


def inventory_report(items, raw_root, complete):
    sizes = [i['vendor_size'] for i in items]
    return {'entries': len(items), 'vendor_bytes': sum(s or 0 for s in sizes),
            'unknown_sizes': sum(s is None for s in sizes), 'complete_selection': complete,
            'disk_free_bytes': shutil.disk_usage(raw_root).free,
            'largest_asset_bytes': max((s or 0 for s in sizes), default=0),
            'prefixes': dict(Counter(i['asset_key'].split('/')[0] for i in items)),
            'expanded_bytes': 'UNKNOWN_UNTIL_EXTRACTED'}


def process_asset(item, *, profile, state_root, raw_root, client, acquire, integrity,
                  version, decode_master, known_master, max_objects, local_asset=None, cancelled=lambda: False,
                  retry_extraction_failures=False, dependency_loader=None, current_tokens=None):
    from scripts.crypto import get_url_params
    asset_id = digest(item['asset_key'].encode())
    state_path = state_root / 'assets' / asset_id[:2] / (asset_id + '.json')
    check_private_root(state_path.parent)
    with ProcessFileLock(state_path.with_suffix('.lock')):
        old = read_json(state_path) if state_path.exists() else {}
        if old and (old.get('profile') != asdict(profile) or old.get('asset_key') != item['asset_key']):
            raise ValueError('SOURCE_STATE_PROFILE')
        matching = old.get('vendor_token') == item['vendor_token']
        if old and not matching:
            history = check_private_root(state_root / 'history' / asset_id)
            atomic_json(history / (uuid.uuid4().hex + '.json'), old)
        record = old if matching else {'profile': asdict(profile), **item, 'status': 'PENDING', 'attempts': []}
        record['observed_at'] = now()
        if not acquire:
            # Inventory records the fresh observation; unchanged deferred assets
            # do not need thousands of identical state rewrites on every batch.
            if not old or not matching:
                atomic_json(state_path, record)
            return record
        reusable_statuses = ('EXTRACTED',) if retry_extraction_failures else ('EXTRACTED', 'PARTIAL', 'UNSUPPORTED')
        if (record.get('extraction', {}).get('version') == version and record.get('status') in reusable_statuses
                and dependencies_current(record, current_tokens)):
            try:
                owned_file(raw_root, record['raw'], verify=integrity)
                for ref in record['extraction']['indexes'].values():
                    owned_file(raw_root, ref, verify=integrity)
                for dependency in record['extraction'].get('dependencies', []):
                    owned_file(raw_root, dependency['raw'], verify=integrity)
                return record
            except (OSError, ValueError):
                record['attempts'].append({'time': now(), 'code': 'SOURCE_OWNED_FILE_INVALID'})
        try:
            if record.get('active_extraction_path'):
                record['attempts'].append({'time': now(), 'code': 'SOURCE_PREVIOUS_ATTEMPT_INCOMPLETE',
                                           'extraction_path': record.pop('active_extraction_path')})
            raw_path = None
            if record.get('raw'):
                try:
                    raw_path = owned_file(raw_root, record['raw'])
                except (OSError, ValueError):
                    record['attempts'].append({'time': now(), 'code': 'SOURCE_RAW_IDENTITY'})
            if raw_path is None:
                staging = check_private_root(raw_root / 'partial' / asset_id)
                target = staging / (uuid.uuid4().hex + '.download')
                if local_asset:
                    if local_asset.is_symlink():
                        raise ValueError('SOURCE_REPARSE_PATH')
                    shutil.copyfile(local_asset, target)
                    if file_hash(target) != item['vendor_token']:
                        raise ValueError('SOURCE_PC_IDENTITY_CHANGED')
                else:
                    path = '/secure/data/production/webgl/resources/' + item['asset_key']
                    fetch_file(client, 'https://cdn-r18.gc.dmmgames.com' + path, target,
                               params=get_url_params(path, item['vendor_token']), cancelled=cancelled)
                record['raw'] = preserve_file(raw_root, target)
                raw_path = raw_root / record['raw']['path']
                record['vendor_size_matches'] = item['vendor_size'] is None or item['vendor_size'] == record['raw']['size']
                record['status'] = 'ACQUIRED'
                atomic_json(state_path, record)
            if cancelled():
                raise ValueError('SOURCE_CANCELLED')
            # New directory for every attempt keeps earlier partial results.
            output = raw_root / 'extracted' / asset_id / uuid.uuid4().hex
            if record.get('extraction'):
                record.setdefault('extraction_history', []).append(record.pop('extraction'))
            record['active_extraction_path'] = output.relative_to(raw_root).as_posix()
            record['status'] = 'EXTRACTING'
            atomic_json(state_path, record)
            result = extract_asset(raw_path, item, output, decode_master=decode_master,
                                   known_master=known_master, max_objects=max_objects, cancelled=cancelled,
                                   dependency_loader=dependency_loader)
            indexes = {}
            for name in ('strings', 'failures', 'objects'):
                path = output / (name + '.jsonl')
                indexes[name] = {'path': path.relative_to(raw_root).as_posix(), 'sha256': file_hash(path), 'size': path.stat().st_size}
            record['extraction'] = {**result, 'version': version, 'indexes': indexes,
                                    'path': output.relative_to(raw_root).as_posix()}
            record['status'] = result['status']
            record.pop('active_extraction_path', None)
            record.pop('error_code', None)
        except (KeyboardInterrupt, SystemExit):
            record.update(status='FAILED', error_code='SOURCE_CANCELLED')
            record['attempts'].append({'time': now(), 'code': 'SOURCE_CANCELLED'})
            atomic_json(state_path, record)
            raise
        except Exception as error:
            record.update(status='FAILED', error_code=safe_error(error, 'SOURCE_ACQUISITION_OR_EXTRACTION_FAILED'))
            record['attempts'].append({'time': now(), 'code': record['error_code'], 'exception_type': type(error).__name__})
        atomic_json(state_path, record)
        return record


def coverage_report(records, raw_root, package, output, ui_scope):
    master_path, ui_path = package / 'master.json', package / 'ui.json'
    master, ui = read_json(master_path), read_json(ui_path)
    master_keys = {(t, f, jp): ko for t, fs in master.items() for f, values in fs.items() for jp, ko in values.items()}
    seen = {'master': set(), 'ui': set(), 'unreviewed_text': set()}
    old_ui, expanded_ui = set(), set()
    totals, categories = Counter(), Counter()
    statuses = Counter(r['status'] for r in records)
    missing_path = output.with_name('coverage-candidates.jsonl')
    with missing_path.open('w', encoding='utf-8') as missing:
        for record in sorted(records, key=lambda r: r['asset_key']):
            extraction = record.get('extraction')
            if not extraction or record['status'] not in ('EXTRACTED', 'PARTIAL', 'UNSUPPORTED'):
                continue
            totals.update(extraction['counts'])
            path = owned_file(raw_root, extraction['indexes']['strings'], verify=False)
            in_old_ui = any(fnmatchcase(record['asset_key'], pattern) for pattern in ui_scope['include'])
            with path.open(encoding='utf-8') as stream:
                for line in stream:
                    row = json.loads(line)
                    declared_master = row['kind'] == 'master' and row.get('mapping') == 'existing_runtime_field'
                    if not row['candidate'] and not declared_master:
                        continue
                    kind = row['kind'] if row['kind'] in ('master', 'ui') else 'unreviewed_text'
                    key = (row['translation_target']['table'], row['translation_target']['field'], row['jp']) if kind == 'master' else row['jp']
                    if kind == 'ui':
                        (old_ui if in_old_ui else expanded_ui).add(key)
                    if key in seen[kind]:
                        continue
                    seen[kind].add(key)
                    current = master_keys.get(key) if kind == 'master' else ui.get(key)
                    state = 'missing' if current is None else 'source_equal' if current == row['jp'] else 'present'
                    categories[kind + '_' + state] += 1
                    if kind in ('ui', 'master'):
                        categories[kind + '_' + ('japanese_' if JAPANESE.search(row['jp']) else 'non_japanese_') + state] += 1
                    if kind == 'master' and row['mapping'] == 'unreviewed_field':
                        categories['master_unreviewed_field_' + state] += 1
                    if state != 'present':
                        write_row(missing, {**row, 'coverage_status': state, 'platform': record['profile']['platform'],
                                           'raw': record.get('raw'), 'review_status': 'UNREVIEWED',
                                           'coverage_basis': 'declared_runtime_field' if declared_master else
                                               'serialized_display_field' if kind == 'ui' else 'unreviewed_Japanese_script'})
    added_ui = expanded_ui - old_ui
    for jp in expanded_ui:
        current = ui.get(jp)
        state = 'missing' if current is None else 'source_equal' if current == jp else 'present'
        categories['ui_outside_previous_scope_' + state] += 1
    return {'status': 'MEASURED', 'asset_statuses': dict(statuses), 'extraction_counts': dict(totals),
            'unique_candidates': {k: len(v) for k, v in seen.items()}, 'comparison': dict(categories),
            'expanded_ui_scope': {'distinct_display_values': len(expanded_ui),
                'additional_vs_previous_scope': len(added_ui),
                'additional_missing_current': len(added_ui - ui.keys()),
                'additional_japanese_missing_current': sum(bool(JAPANESE.search(v)) for v in added_ui - ui.keys())},
            'current_master_sha256': file_hash(master_path), 'current_ui_sha256': file_hash(ui_path),
            'current_master_keys': len(master_keys), 'current_ui_keys': len(ui),
            'candidates': missing_path.name, 'native_verified': 0, 'automatically_applied': 0,
            'semantic_approval': False, 'limitations': ['unreviewed strings are not display approval',
            'no code literal/dynamic sentence reconstruction or image OCR', 'WebGL and PC are separate profiles',
            'present exact key is not a claim of semantic correctness']}


def run(args):
    import httpx
    from scripts.crypto import private_helper, decrypt_master_text
    if args.source_ids:
        raise ValueError('SOURCE_ALL_USES_ASSET_KEY_OR_PATTERN')
    if args.max_candidates < 0 or args.all_max_objects < 0 or args.all_workers < 1:
        raise ValueError('SOURCE_CANDIDATE_LIMIT')
    if args.integrity and not args.acquire:
        raise ValueError('SOURCE_ALL_INTEGRITY_REQUIRES_ACQUIRE')
    raw_root, state_root = check_private_root(args.raw_root), check_private_root(args.state_root / 'all-assets' / args.platform)
    check_private_root(args.output.parent)
    profile = SourceProfile(args.platform, 'R18', 'All', 'vendor-d-n-h-s-v1' if args.platform == 'WebGL' else 'pc-explicit-local-cache-v1')
    records = []
    with ProcessFileLock(state_root / 'scan.lock'), httpx.Client(timeout=httpx.Timeout(90, connect=30), follow_redirects=False,
            headers={'User-Agent': 'gctrans-crawl-only/all-assets-v1'}) as client:
        manifests = []
        if args.platform == 'PC':
            if not args.local_asset or not args.asset_key or not args.local_asset.is_file() or args.local_asset.is_symlink():
                raise ValueError('SOURCE_PC_LOCATOR_REQUIRED')
            manifests = [{'d': [{'n': args.asset_key, 'h': file_hash(args.local_asset), 's': args.local_asset.stat().st_size}]}]
        elif args.inventory:
            manifests.append(read_json(args.inventory))
            if args.all_master_inventory:
                manifests.append(read_json(args.all_master_inventory))
        else:
            for name in ('assetbundle', 'master'):
                path = state_root / 'inventories' / (name + '.' + uuid.uuid4().hex + '.json')
                fetch_file(client, 'https://cdn-r18.gc.dmmgames.com/files/manifest/webgl/r18/' + name + '.json', path)
                manifests.append(read_json(path))
        combined = {'d': [row for manifest in manifests for row in manifest['d']]}
        normalized = normalize_inventory(combined, 'All', max_entries=None)
        if normalized['collisions']:
            atomic_json(args.output, {'status': 'SOURCE_INVENTORY_COLLISION', 'collisions': normalized['collisions']})
            raise ValueError('SOURCE_INVENTORY_COLLISION')
        items = normalized['items']
        current_tokens = {item['asset_key']: item['vendor_token'] for item in items}
        dependency_loader = OwnedDependencies(state_root, raw_root, profile, current_tokens)
        if any(re.search(r'[?#%\x00-\x1f]', item['asset_key']) for item in items):
            raise ValueError('SOURCE_ASSET_PATH')
        complete = not (args.asset_key or args.asset_pattern or args.partial or args.platform == 'PC')
        if args.asset_key:
            items = [i for i in items if i['asset_key'] == args.asset_key]
        if args.asset_pattern:
            items = [i for i in items if any(fnmatchcase(i['asset_key'], p) for p in args.asset_pattern)]
        if not items:
            raise ValueError('SOURCE_SELECTION_EMPTY')
        observation = {**inventory_report(items, raw_root, complete), 'sha256': normalized['sha256'],
                       'observed_at': now(), 'source': 'SAVED_INVENTORY' if args.inventory else 'LIVE_INVENTORY',
                       'manifests': len(manifests)}
        atomic_json(state_root / 'inventory.json', combined)
        atomic_json(args.output, {'status': 'OBSERVED', 'inventory': observation})
        atomic_json(args.output.with_name('progress.json'), {'processed': 0, 'selected': len(items), 'at': now()})
        known_master = {t: set(fs) for t, fs in read_json(args.master_package / 'master.json').items()}
        field_scope = read_json(args.master_field_scope)
        if field_scope.get('schema') != 'gc-master-source-field-selectors-v1':
            raise ValueError('MASTER_FIELD_SELECTOR')
        for selector in field_scope['fields']:
            known_master.setdefault(selector['table'], set()).add(selector['runtime_field'])
        scope = read_json(args.ui_scope)
        helper = private_helper() if args.acquire else None
        version = extractor_version(helper.ng03_helper_sha256 if helper else 'none', args.all_max_objects, known_master) if args.acquire else None
        # Coverage-bearing assets first; all remaining paths remain selected.
        items.sort(key=lambda i: (0 if i['asset_key'] == 'master.dmm' else 1 if any(fnmatchcase(i['asset_key'], p) for p in scope['include']) else 2, i['asset_key']))
        attempted, completed = 0, 0
        stop = threading.Event()
        def cancelled():
            return stop.is_set() or bool(args.cancel_file and args.cancel_file.exists())

        def work(item, acquire, retry=None):
            return process_asset(item, profile=profile, state_root=state_root, raw_root=raw_root, client=client,
                acquire=acquire, integrity=args.integrity, version=version, decode_master=decrypt_master_text if helper else None,
                known_master=known_master, max_objects=args.all_max_objects,
                local_asset=args.local_asset if args.platform == 'PC' else None, cancelled=cancelled,
                retry_extraction_failures=args.retry_extraction_failures if retry is None else retry,
                dependency_loader=dependency_loader, current_tokens=current_tokens)

        def needs_work(item):
            asset_id = digest(item['asset_key'].encode())
            state_path = state_root / 'assets' / asset_id[:2] / (asset_id + '.json')
            previous = read_json(state_path) if state_path.exists() else {}
            reusable_statuses = ('EXTRACTED',) if args.retry_extraction_failures else ('EXTRACTED', 'PARTIAL', 'UNSUPPORTED')
            reusable = (previous.get('vendor_token') == item['vendor_token'] and previous.get('extraction', {}).get('version') == version
                        and previous.get('status') in reusable_statuses and dependencies_current(previous, current_tokens))
            return not reusable

        iterator = iter(items)
        with ThreadPoolExecutor(max_workers=args.all_workers) as pool:
            pending = {}
            exhausted = False
            try:
                while pending or not exhausted:
                    while not exhausted and not cancelled() and len(pending) < args.all_workers:
                        item = next(iterator, None)
                        if item is None:
                            exhausted = True
                            break
                        needed = needs_work(item)
                        acquire = args.acquire and (not needed or not args.max_candidates or attempted < args.max_candidates)
                        attempted += int(acquire and needed)
                        pending[pool.submit(work, item, acquire)] = item
                    if cancelled():
                        exhausted = True
                    if not pending:
                        break
                    done, _ = wait(pending, return_when=FIRST_COMPLETED)
                    for future in done:
                        pending.pop(future)
                        records.append(future.result())
                        completed += 1
                    if completed % 100 < len(done):
                        atomic_json(args.output.with_name('progress.json'), {'processed': completed, 'selected': len(items),
                            'attempted': attempted, 'statuses': dict(Counter(r['status'] for r in records)), 'at': now()})
                        print(json.dumps({'all_assets_processed': completed, 'selected': len(items), 'attempted': attempted}), flush=True)
            except KeyboardInterrupt:
                stop.set()
                for future in pending:
                    records.append(future.result())
            if cancelled():
                for item in iterator:
                    asset_id = digest(item['asset_key'].encode())
                    path = state_root / 'assets' / asset_id[:2] / (asset_id + '.json')
                    previous = read_json(path) if path.exists() else {}
                    records.append(previous if previous.get('vendor_token') == item['vendor_token'] else
                                   {'profile': asdict(profile), **item, 'status': 'PENDING'})
        # Dependencies may be acquired after their referencing bundle. Revisit
        # only those explicit failures once the selected acquisition has ended.
        if args.acquire and not cancelled():
            dependency_loader.refresh()
            by_key = {item['asset_key']: item for item in items}
            for index, record in enumerate(records):
                if cancelled():
                    break
                if record.get('extraction', {}).get('counts', {}).get('missing_dependency_objects'):
                    records[index] = work(by_key[record['asset_key']], True, retry=True)
                    attempted += 1
        coverage = coverage_report(records, raw_root, args.master_package, args.output, scope)
        atomic_json(args.output.with_name('coverage.json'), coverage)
        result = {'status': 'CANCELLED' if cancelled() else 'METADATA_ONLY' if not args.acquire else 'PARTIAL' if any(r['status'] != 'EXTRACTED' for r in records) else 'EXTRACTED',
                  'profile': asdict(profile), 'inventory': observation, 'coverage': coverage,
                  'raw_assets': sum('raw' in r for r in records),
                  'raw_bytes': sum(r.get('raw', {}).get('size', 0) for r in records),
                  'attempted_this_run': attempted, 'state_root': str(state_root), 'raw_root': str(raw_root)}
        atomic_json(args.output, result)
        return result
