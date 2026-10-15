"""One-time import custody verification; never a runtime dependency."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument('--inputs', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--handoff-zip', type=Path, required=True)
    p.add_argument('--design-zip', type=Path, required=True)
    a = p.parse_args()
    inputs = a.inputs.resolve()
    baseline = json.loads((inputs / 'design/BASELINE_REFERENCE.json').read_text(encoding='utf-8-sig'))
    result = {'schema': 'gc-ng01-import-verification-v1', 'runtime_dependency': False,
              'accepted_state': baseline['status'], 'content_id': baseline['content_id'],
              'archives': {}, 'payload': {}, 'canonical': {}, 'ledger': {}, 'expected_private_exclusions': {}, 'failures': []}
    archive_paths = [a.handoff_zip, a.design_zip] + list((inputs / 'handoff/attachments').glob('*.zip'))
    archive_paths.append(inputs / 'ui21/canonical/evidence/UI18_PARENT_EXACT.zip')
    for path in archive_paths:
        data = path.read_bytes()
        expected = baseline['source_archives'].get(path.name)
        with ZipFile(path) as archive:
            bad = archive.testzip()
            if bad:
                result['failures'].append(f'CRC:{path.name}:{bad}')
            names = archive.namelist()
            if len(names) != len(set(names)):
                result['failures'].append(f'duplicate archive member:{path.name}')
            for name in names:
                normalized = name.replace('\\', '/')
                if normalized.startswith('/') or ':' in normalized or '..' in normalized.split('/'):
                    result['failures'].append(f'unsafe archive member:{path.name}:{name}')
        identity = {'bytes': len(data), 'sha256': digest(data), 'crc': 'PASS' if bad is None else 'FAIL',
                    'members': len(names)}
        if expected and (identity['bytes'] != expected['bytes'] or identity['sha256'] != expected['sha256']):
            result['failures'].append(f'archive baseline mismatch:{path.name}')
        result['archives'][path.name] = identity
    for group, manifest_path, root in [
        ('payload', inputs/'ui21/CONTENT_MANIFEST.json', inputs/'ui21/canonical/payload'),
        ('canonical', inputs/'ui21/canonical/LOCAL_CANONICAL_MANIFEST.json', inputs/'ui21/canonical')]:
        records = json.loads(manifest_path.read_text(encoding='utf-8-sig'))['files']
        checked = 0
        for relative, record in records.items():
            path = root / relative
            if not path.is_file():
                if group == 'canonical' and relative in ('private_local/fonts/RHR_Medium_GFSAFE_ADH.bundle',
                                                        'private_local/launch_existing_gc.ps1'):
                    result['expected_private_exclusions'][relative] = record
                    continue
                result['failures'].append(f'missing:{group}:{relative}')
                continue
            data = path.read_bytes()
            if len(data) != record['bytes'] or digest(data) != record['sha256']:
                result['failures'].append(f'bytes/hash mismatch:{group}:{relative}')
            checked += 1
        result[group] = {'manifest_entries': len(records), 'verified': checked,
                         'membership_exact': {x.relative_to(root).as_posix() for x in root.rglob('*') if x.is_file()}
                         == set(records)}
        # The canonical manifest excludes itself by design, unlike the payload manifest.
        if group == 'payload' and not result[group]['membership_exact']:
            result['failures'].append('payload membership mismatch')
    for path in sorted((inputs/'ui22/ledger').glob('*.json')):
        data = path.read_bytes()
        result['ledger'][path.name] = {'bytes': len(data), 'sha256': digest(data)}
        corresponding = inputs/'design/reference/accepted/UI22/ledger'/path.name
        if not corresponding.is_file() or corresponding.read_bytes() != data:
            result['failures'].append(f'ledger reference mismatch:{path.name}')
    result['status'] = 'PASS' if not result['failures'] else 'FAIL'
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'status': result['status'], 'payload': result['payload'], 'canonical': result['canonical'],
                      'ledger_records': len(result['ledger']), 'failures': result['failures']}, ensure_ascii=False))
    if result['failures']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
