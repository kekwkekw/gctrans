"""Read-only, language-independent text census over owned extraction evidence.

The SQLite output is private source evidence, never a translation apply input.
No current dictionary, source state, raw file or runtime is modified.
"""
from __future__ import annotations

import hashlib
import json
import re
import sqlite3
from pathlib import Path

KANA = re.compile(r'[\u3040-\u30ff]')
HAN = re.compile(r'[\u3400-\u9fff]')
LATIN = re.compile(r'[A-Za-z]')
FORMAT = re.compile(r'\{[^{}]+\}|%(?:\d+\$)?[sdif]|(?:hh|HH|yyyy|mm:ss|HH:mm)')
MEDIA_TYPES = {'Texture2D', 'Sprite', 'AudioClip', 'Mesh', 'Cubemap', 'RenderTexture'}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def walk_text(value, path=()):
    """Include dictionary keys, nested values and every array position; no cap."""
    pending = [(path, value, False)]
    while pending:
        parts, node, is_key = pending.pop()
        if isinstance(node, str):
            yield parts, node, is_key
        elif isinstance(node, dict):
            for key, child in reversed(list(node.items())):
                if isinstance(child, (str,dict,list,tuple)):
                    pending.append((parts + (key,), child, False))
                if isinstance(key, str):
                    pending.append((parts + (key,), key, True))
        elif isinstance(node, (list, tuple)):
            pending.extend((parts + (index,), child, False) for index, child in reversed(list(enumerate(node))) if isinstance(child,(str,dict,list,tuple)))


def script_group(value):
    if not value.strip(): return 'empty_or_whitespace'
    if KANA.search(value): return 'kana'
    if HAN.search(value): return 'han_without_kana'
    if re.search(r'[\uac00-\ud7af]', value): return 'hangul'
    if LATIN.search(value): return 'latin_or_mixed'
    if any(c.isalpha() for c in value): return 'other_letters'
    return 'numbers_or_symbols'


def decode_textasset(raw, name=''):
    """Only exclude positively identified payloads; undecodable data is a gap."""
    head = raw[:16]
    signatures = [(b'@UTF', 'cri_audio_table'), (b'AFS2', 'cri_audio_wavebank'),
                  (b'CRID', 'cri_video'), (b'OggS', 'ogg_audio'), (b'FSB5', 'fsb_audio'),
                  (b'\x89PNG\r\n\x1a\n', 'png_image'), (b'\xff\xd8\xff', 'jpeg_image'),
                  (b'OTTO', 'opentype_font'), (b'ttcf', 'font_collection'),
                  (b'wOFF', 'woff_font'), (b'wOF2', 'woff2_font')]
    if raw.startswith(b'RIFF') and raw[8:12] == b'WAVE': return 'excluded', 'wave_audio', None
    if raw.startswith(b'\x00\x01\x00\x00') and name.lower().endswith(('.ttf','.otf')):
        return 'excluded', 'truetype_font', None
    for magic, category in signatures:
        if head.startswith(magic): return 'excluded', category, None
    if raw.startswith(b'GC Movie\x00') and name.lower().endswith('.usm'):
        return 'excluded', 'gc_wrapped_usm_video', None
    if name.lower().endswith('.skel'):
        # Spine's two leading length-prefixed strings: export hash and version.
        # Require both a named skeleton payload and a coherent version header.
        try:
            cursor=0;header=[]
            for _ in range(2):
                length=0;shift=0
                while True:
                    byte=raw[cursor];cursor+=1;length|=(byte&127)<<shift
                    if not byte&128:break
                    shift+=7
                    if shift>28:raise ValueError('invalid varint')
                if length<1 or cursor+length-1>len(raw):raise ValueError('invalid header length')
                header.append(raw[cursor:cursor+length-1].decode('ascii'));cursor+=length-1
            if re.fullmatch(r'[A-Za-z0-9+/=_-]+',header[0]) and re.fullmatch(r'[3-4]\.\d+(?:\.\d+)?(?:-beta)?',header[1]):
                return 'excluded','spine_binary_version_header',None
        except (ValueError,IndexError,UnicodeError):pass
    encodings = ['utf-8-sig']
    if raw.startswith((b'\xff\xfe', b'\xfe\xff')): encodings = ['utf-16']
    for encoding in encodings:
        try:
            text = raw.decode(encoding, errors='strict')
        except UnicodeError:
            continue
        if '\x00' in text: continue
        try: value = json.loads(text)
        except ValueError: value = text
        if isinstance(value, dict) and isinstance(value.get('bones'), list) and (isinstance(value.get('slots'), list) or isinstance(value.get('animations'), dict)):
            return 'excluded', 'spine_json_structure', None
        if name.lower().endswith('.atlas') and ('size:' in text or 'bounds:' in text) and ('format:' in text or 'filter:' in text):
            return 'excluded', 'spine_atlas_structure', None
        return 'text', encoding, value
    # A filename or enclosing bundle named Spine is not sufficient evidence.
    return 'gap', 'UNDECODED_TEXTASSET', None


def master_role(parts, is_key, table, known_fields, value):
    if is_key: return 'structural_key', None, None
    p = list(parts)
    if p and isinstance(p[0], int): p.pop(0)
    if not p or not isinstance(p[0], str): return 'master_nested', None, None
    field = p[0]
    if len(p) == 1:
        declared = field + '|' if field + '|' in known_fields else field
        return 'master_scalar', (table, declared), value.partition('|')[0] if declared.endswith('|') else value
    if len(p) == 2 and isinstance(p[1], int):
        if field.startswith('ml_'):
            return ('master_locale_primary', (table, field+'[]'), value) if p[1] == 0 else ('master_locale_tail', None, None)
        # Unknown arrays may be lists of display strings, not language arrays.
        return 'master_array_unresolved', None, None
    return 'master_nested', None, None


class Census:
    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(self.path)
        self.db.executescript('''
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS assets(id INTEGER PRIMARY KEY, platform TEXT, asset_key TEXT,
                identity TEXT, raw_sha256 TEXT, state_sha256 TEXT, status TEXT, details TEXT,
                UNIQUE(platform,asset_key,identity));
            CREATE TABLE IF NOT EXISTS texts(id INTEGER PRIMARY KEY, sha256 TEXT UNIQUE, value TEXT,
                script TEXT, has_format INTEGER);
            CREATE TABLE IF NOT EXISTS occurrences(asset_id INTEGER, text_id INTEGER, role TEXT,
                locator TEXT, target_table TEXT, target_field TEXT, query_value TEXT);
            CREATE TABLE IF NOT EXISTS files(asset_id INTEGER, path TEXT, sha256 TEXT, bytes INTEGER, purpose TEXT);
            CREATE TABLE IF NOT EXISTS dispositions(asset_id INTEGER, locator TEXT, status TEXT, reason TEXT, details TEXT);
            CREATE TABLE IF NOT EXISTS structural_occurrences(asset_id INTEGER, text_id INTEGER, role TEXT, first_locator TEXT, occurrences INTEGER);
        ''')
        self.text_ids = {h:i for i,h in self.db.execute('SELECT id,sha256 FROM texts')}
        self.value_ids = {value:i for i,value in self.db.execute('SELECT id,value FROM texts')}
        self.next_id = max(self.text_ids.values(), default=0)+1
        self.pending = []
        self.structural = {}

    def asset(self, platform, key, identity, raw_sha, state_sha='', details=None):
        row = self.db.execute('SELECT id,status,raw_sha256,state_sha256 FROM assets WHERE platform=? AND asset_key=? AND identity=?', (platform,key,identity)).fetchone()
        if row and (row[2]!=raw_sha or row[3]!=state_sha):raise ValueError('CENSUS_SOURCE_CHANGED_USE_NEW_OUTPUT')
        if row and row[1]=='COMPLETE': return row[0], True
        if row: raise ValueError('INCOMPLETE_ASSET_REQUIRES_NEW_OUTPUT_OR_EXPLICIT_RECOVERY')
        cur = self.db.execute('INSERT INTO assets(platform,asset_key,identity,raw_sha256,state_sha256,status,details) VALUES(?,?,?,?,?,?,?)',
            (platform,key,identity,raw_sha,state_sha,'PROCESSING',json.dumps(details or {},ensure_ascii=False)))
        return cur.lastrowid, False

    def add(self, asset_id, value, role, locator, target=None, query=None):
        tid=self.value_ids.get(value)
        if tid is None:
            digest=sha(value.encode('utf-8',errors='surrogatepass'))
            tid=self.next_id;self.next_id+=1;self.text_ids[digest]=tid
            self.value_ids[value]=tid
            self.db.execute('INSERT INTO texts VALUES(?,?,?,?,?)',(tid,digest,value,script_group(value),int(bool(FORMAT.search(value)))))
        if role=='structural_key' or not value.strip():
            key=(asset_id,tid,role)
            if key in self.structural:self.structural[key][1]+=1
            else:self.structural[key]=[json.dumps(locator,ensure_ascii=False,separators=(',',':')),1]
            return
        self.pending.append((asset_id,tid,role,json.dumps(locator,ensure_ascii=False,separators=(',',':')),
                             target[0] if target else None,target[1] if target else None,query))
        if len(self.pending)>=10000:self.flush()

    def flush(self):
        if self.pending:
            self.db.executemany('INSERT INTO occurrences VALUES(?,?,?,?,?,?,?)',self.pending);self.pending.clear()

    def disposition(self, asset_id, locator, status, reason, details=None):
        self.db.execute('INSERT INTO dispositions VALUES(?,?,?,?,?)',(asset_id,json.dumps(locator,ensure_ascii=False),status,reason,json.dumps(details or {},ensure_ascii=False)))

    def file(self, asset_id, path, digest, size, purpose):
        self.db.execute('INSERT INTO files VALUES(?,?,?,?,?)',(asset_id,str(path),digest,size,purpose))

    def complete(self, asset_id):
        self.flush()
        self.db.executemany('INSERT INTO structural_occurrences VALUES(?,?,?,?,?)',
            [(a,t,r,loc,count) for (a,t,r),(loc,count) in self.structural.items()])
        self.structural.clear()
        self.db.execute('UPDATE assets SET status=? WHERE id=?',('COMPLETE',asset_id));self.db.commit()

    def finish(self):
        self.flush();self.db.commit()
        self.db.executescript('CREATE INDEX IF NOT EXISTS occurrences_text ON occurrences(text_id); CREATE INDEX IF NOT EXISTS occurrences_role ON occurrences(role);')
        result={'assets':self.db.execute('SELECT COUNT(*) FROM assets').fetchone()[0],
                'texts':self.db.execute('SELECT COUNT(*) FROM texts').fetchone()[0],
                'occurrences':self.db.execute('SELECT COUNT(*) FROM occurrences').fetchone()[0],
                'structural_or_empty_occurrences':self.db.execute('SELECT COALESCE(SUM(occurrences),0) FROM structural_occurrences').fetchone()[0],
                'structural_storage':'All keys/empty strings retained, grouped by asset/value/role with count and first locator; original files retain every location',
                'scripts':dict(self.db.execute('SELECT script,COUNT(*) FROM texts GROUP BY script')),
                'roles':dict(self.db.execute('SELECT role,COUNT(*) FROM occurrences GROUP BY role')),
                'dispositions':[{'status':s,'reason':r,'count':n} for s,r,n in self.db.execute('SELECT status,reason,COUNT(*) FROM dispositions GROUP BY status,reason')],
                'files_hashed':self.db.execute('SELECT COUNT(*) FROM files').fetchone()[0]}
        self.db.execute('PRAGMA wal_checkpoint(TRUNCATE)');return result
