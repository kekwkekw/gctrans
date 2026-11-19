"""Authoring policy: preserve complete Latin-script display text verbatim.

Tags and format arguments are ignored only for classification. Callers keep the
original spelling, case, spacing and punctuation; runtime code is unchanged.
"""
import re
import unicodedata

_TAGS = re.compile(r'</?[A-Za-z][^<>]*>')
_ARGUMENTS = re.compile(r'\{[^{}]*\}')


def preserve_english_original(source):
    if not isinstance(source, str):
        return False
    visible = _ARGUMENTS.sub('', _TAGS.sub('', source))
    visible = unicodedata.normalize('NFKC', visible)
    letters = [c for c in visible if unicodedata.category(c).startswith('L')]
    return bool(letters) and all('LATIN' in unicodedata.name(c, '') for c in letters)


def validate_master_originals(master):
    for table, fields in master.items():
        for field, pairs in fields.items():
            for source, target in pairs.items():
                if preserve_english_original(source) and source != target:
                    raise ValueError('ENGLISH_ORIGINAL_MUST_BE_PRESERVED:' + table + '.' + field + ':' + source)
