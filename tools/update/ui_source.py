"""Collect display text broadly; preserve component failures and image scope separately."""
from fnmatch import fnmatchcase


def select_inventory(inventory, scope, asset_key=None):
    if scope.get('schema') != 'gc-ui-source-scope-v1' or not isinstance(scope.get('include'), list) or not scope['include']:
        raise ValueError('UI_SCOPE_SCHEMA')
    if not isinstance(inventory.get('d'), list) or any(not isinstance(r, dict) or not isinstance(r.get('n'), str) for r in inventory['d']):
        raise ValueError('UI_INVENTORY_SCHEMA')
    patterns = scope['include']
    if any(not isinstance(p, str) or '..' in p or ':' in p or '\\' in p for p in patterns):
        raise ValueError('UI_SCOPE_PATTERN')
    selected = [r for r in inventory['d'] if r['n'] == asset_key] if asset_key else [
        r for r in inventory['d'] if any(fnmatchcase(r['n'], p) for p in patterns)]
    if not selected:
        raise ValueError('UI_SOURCE_SELECTION_EMPTY')
    return {'d': selected}


def text_rows(objects, asset_key):
    rows = []
    for container, path_id, tree in objects:
        # These serialized display fields are not arbitrary string properties.
        for field in ('m_text', 'm_Text'):
            value = tree.get(field)
            if isinstance(value, str) and value.strip():
                rows.append({'jp': value, 'locator': {'asset_key': asset_key,
                    'container': container, 'path_id': path_id, 'field': field},
                    'context': {'object_name': tree.get('_display_object_name', tree.get('m_Name', ''))},
                    'structure': {'role': 'serialized_ui_label', 'mapping_contract': 'TMP-or-legacy-text-field-v1'}})
    return sorted(rows, key=lambda r: (r['locator']['container'], r['locator']['path_id'], r['locator']['field']))


class UiPrefabExtractor:
    def __init__(self, max_objects=250000):
        import UnityPy
        if not 1 <= max_objects <= 1000000:
            raise ValueError('UI_OBJECT_LIMIT_RANGE')
        self.max_objects = max_objects
        self.version = f'local-parity-ui-prefab-v2:UnityPy-{UnityPy.__version__}:limit-{max_objects}'

    def __call__(self, raw, asset):
        import UnityPy
        if not raw.startswith(b'UnityFS\0'):
            raise ValueError('SOURCE_NOT_UNITYFS')
        env = UnityPy.load(raw)
        if len(env.objects) > self.max_objects:
            raise ValueError('SOURCE_OBJECT_LIMIT')
        objects, failures, images, names = [], [], [], {}
        for obj in env.objects:
            if obj.type.name == 'GameObject':
                try:
                    names[(str(obj.assets_file.name), obj.path_id)] = obj.read().m_Name
                except Exception:
                    pass  # Optional context; text decode status is recorded below.
        for obj in env.objects:
            if obj.type.name in ('Sprite', 'Texture2D'):
                images.append({'container': str(obj.assets_file.name), 'path_id': obj.path_id, 'type': obj.type.name})
            if obj.type.name != 'MonoBehaviour':
                continue
            try:
                tree = obj.read_typetree()
            except Exception:
                failures.append({'container': str(obj.assets_file.name), 'path_id': obj.path_id,
                                 'reason': 'UI_TYPETREE_UNAVAILABLE'})
                continue
            pointer = tree.get('m_GameObject')
            tree['_display_object_name'] = names.get((str(obj.assets_file.name), pointer.get('m_PathID') if isinstance(pointer,dict) else None), '')
            objects.append((str(obj.assets_file.name), obj.path_id, tree))
        return {'textassets': [], 'selected': None, 'master': [],
                'ui': text_rows(objects, asset['asset_key']),
                'ui_diagnostics': {'objects': len(env.objects), 'decoded_components': len(objects),
                    'failed_components': failures, 'image_objects': images,
                    'text_status': 'PARTIAL' if failures else 'SELECTED_TEXT_FIELDS_EXTRACTED',
                    'image_status': 'PIXELS_NOT_OCR_OR_TRANSLATED'},
                'selection_contract': 'UI_PREFAB_TEXT_FIELDS_V1'}
