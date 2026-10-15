"""The existing TranslationHash.Compute compatibility contract, not transport security."""
import hashlib
from contract_validation import ContractError

def canonical(table, prefix=''):
    if not isinstance(table, dict):
        raise ContractError('LEGACY_OBJECT_REQUIRED')
    values = []
    for key, value in sorted(table.items()):
        if not isinstance(key, str):
            raise ContractError('LEGACY_STRING_KEY_REQUIRED')
        path = prefix + key
        if isinstance(value, dict):
            values.append(canonical(value, path + '\x01'))
        elif value is None or isinstance(value, str):
            values.append(path + '\0' + (value or '') + '\0')
        else:
            raise ContractError('LEGACY_STRING_OR_NULL_LEAF_REQUIRED')
    return ''.join(values)

def compute(table):
    return hashlib.md5(canonical(table).encode('utf-8')).hexdigest()

def root_hash(manifest):
    return compute({key: value for key, value in manifest.items() if key != 'hash'})
