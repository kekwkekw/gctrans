"""Collect selected or all Japanese PC code literals for the existing UI review lane.

This is source evidence, not proof of display or semantic approval. Never reads
process memory, account data, or the metadata type/name tables.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct


def collect(raw, contains=(), *, all_japanese=False):
    if (not contains and not all_japanese
            or any(not isinstance(s, str) or not s.strip() for s in contains)):
        raise ValueError('EXPLICIT_LITERAL_SCOPE_REQUIRED')
    if contains and all_japanese:
        raise ValueError('AMBIGUOUS_LITERAL_SCOPE')
    if len(raw) < 32:
        raise ValueError('METADATA_HEADER')
    magic, version, offset, size, count, start, length, strings = struct.unpack_from('<8I', raw)
    # Current PC v39 uses count+1 byte offsets. Reject other formats rather than
    # interpreting arbitrary metadata bytes as translatable text.
    if (magic != 0xFAB11BAF or version != 39 or count != strings + 1 or size != count * 4
            or offset < 32 or offset + size > len(raw) or start < offset + size
            or start + length > len(raw) or count < 1):
        raise ValueError('UNSUPPORTED_LITERAL_TABLE')
    indexes = struct.unpack_from(f'<{count}I', raw, offset)
    if indexes[0] != 0 or indexes[-1] != length or any(a > b for a, b in zip(indexes, indexes[1:])):
        raise ValueError('LITERAL_OFFSETS')
    found = {}
    japanese = re.compile(r'[\u3041-\u3096\u30a1-\u30fa\u3400-\u4dbf\u4e00-\u9fff]')
    for i, (a, b) in enumerate(zip(indexes, indexes[1:])):
        text = raw[start+a:start+b].decode('utf-8')
        if (all_japanese and japanese.search(text)) or any(term in text for term in contains):
            found.setdefault(text, []).append(i)
    digest = hashlib.sha256(raw).hexdigest()
    return {'schema': 'gc-native-ui-observations-v1', 'source_kind': 'pc_metadata_literals',
            'metadata_version': version, 'source_sha256': digest, 'contains': sorted(set(contains)),
            'selection': 'all_japanese_literals' if all_japanese else 'selected_substrings',
            'total_literal_entries': strings,
            'display_status': 'SOURCE_ONLY_REQUIRES_MENU_CONTEXT_REVIEW',
            'rows': [{'jp': text, 'evidence': f'PC v39 code literal; metadata sha256={digest}; '
                     f'indexes={found[text]}; source only, not a Native display observation'} for text in sorted(found)]}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--metadata', type=Path, required=True)
    scope = p.add_mutually_exclusive_group(required=True)
    scope.add_argument('--contains', action='append', default=[], help='Explicit menu/family text; repeat as needed')
    scope.add_argument('--all-japanese', action='store_true', help='Collect all Japanese-looking literals, including diagnostics and fragments, for later review')
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = collect(a.metadata.read_bytes(), a.contains, all_japanese=a.all_japanese)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'source_kind': result['source_kind'], 'candidates': len(result['rows']),
                      'display_status': result['display_status']}))


if __name__ == '__main__':
    main()
