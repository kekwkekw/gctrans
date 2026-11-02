import hashlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace as NS
import unittest
from unittest.mock import patch

from tools import audit_text_sources as audit
from tools.update.text_census import Census


class AuditSourceTests(unittest.TestCase):
    def setUp(self):
        scratch=Path(__file__).parent/'scratch-text-census';scratch.mkdir(exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(dir=scratch)
        self.root=Path(self.temp.name).resolve();self.c=Census(self.root/'census.sqlite')
        self.aid,_=self.c.asset('synthetic','spine_mixed','v','sha')

    def tearDown(self):
        self.c.db.close();self.temp.cleanup()

    def test_known_novel_fields_do_not_hide_extra_commands(self):
        raw='message,0,Speaker,本文\nselectif,1,English choice,日本語選択肢\nunknown,Other text'.encode()
        audit.textasset(self.c,self.aid,raw,'btl_123_10001',{},'btl_123_10001')
        self.c.complete(self.aid)
        values={r[0] for r in self.c.db.execute('select value from texts')}
        self.assertTrue({'English choice','日本語選択肢','Other text','selectif'}<=values)

    def test_master_expands_all_locales_unknown_fields_and_nested_arrays(self):
        audit.textasset(self.c,self.aid,b'', 'mSynthetic',{},'master',
                        [{'ml_name':['名前','English name'],'unknown':{'captions':['first','second']}}])
        self.c.complete(self.aid)
        rows=list(self.c.db.execute('select t.value,o.role from occurrences o join texts t on t.id=o.text_id'))
        self.assertIn(('English name','master_locale_tail'),rows)
        self.assertIn(('second','master_nested'),rows)

    def test_unknown_payload_remains_a_gap(self):
        audit.textasset(self.c,self.aid,b'\xff\x00\x81','unknown',{},'ordinary')
        self.assertEqual(self.c.db.execute('select reason from dispositions').fetchone()[0],'UNDECODED_TEXTASSET')

    def test_embedded_localization_csv_keeps_multiline_and_all_languages(self):
        raw='id,label,ja,en,tw\n1,notice,"日本語\n続き",English,中文\n'.encode()
        audit.textasset(self.c,self.aid,raw,'localization',{},'core')
        self.c.complete(self.aid)
        rows=list(self.c.db.execute('select t.value,o.role from occurrences o join texts t on t.id=o.text_id'))
        self.assertIn(('日本語\n続き','localization_jp'),rows)
        self.assertIn(('English','localization_secondary_locale'),rows)
        self.assertIn(('中文','localization_secondary_locale'),rows)
        self.assertIn((raw.decode(),'textasset_plain'),rows)

    def test_xml_attribute_text_and_comment_remain_separate_evidence(self):
        audit.textasset(self.c,self.aid,b'<?xml version="1.0"?><root label="English"><!-- note --><value>Text</value></root>','config',{},'core')
        self.c.complete(self.aid)
        rows=list(self.c.db.execute('select t.value,o.role from occurrences o join texts t on t.id=o.text_id'))
        self.assertIn(('English','textasset_xml_value'),rows)
        self.assertIn(('Text','textasset_xml_value'),rows)
        self.assertIn((' note ','textasset_xml_comment'),rows)

    def test_cached_master_ciphertext_requires_existing_decoder(self):
        with patch.object(audit,'DECODE_MASTER',None):
            audit.textasset(self.c,self.aid,b'ciphertext','mTable',{},'Caches/master.dmm/version/__data')
        self.assertEqual(self.c.db.execute('select reason from dispositions').fetchone()[0],'MASTER_PRIVATE_DECODER_NOT_CONFIGURED')
        with patch.object(audit,'DECODE_MASTER',lambda raw:[{'ml_name':['表示','English']} ]):
            audit.textasset(self.c,self.aid,b'ciphertext','mTable',{},'Caches/master.dmm/other/__data')
        self.c.complete(self.aid)
        rows=list(self.c.db.execute('select t.value,o.role from occurrences o join texts t on t.id=o.text_id'))
        self.assertIn(('表示','master_locale_primary'),rows)
        self.assertIn(('English','master_locale_tail'),rows)

    def test_saved_extraction_hash_must_match(self):
        (self.root/'index.jsonl').write_bytes(b'original')
        ref={'path':'index.jsonl','size':8,'sha256':hashlib.sha256(b'original').hexdigest()}
        self.assertEqual(audit.index_ref(self.c,self.aid,self.root,ref,'test'),b'original')
        (self.root/'index.jsonl').write_bytes(b'changed!')
        with self.assertRaisesRegex(ValueError,'HASH_MISMATCH'):audit.index_ref(self.c,self.aid,self.root,ref,'test')

    def test_stripped_objects_never_use_guessed_default_schema(self):
        game=self.root/'game';game.mkdir();(game/'resources.assets').write_bytes(b'synthetic')
        out=self.root/'out';out.mkdir()
        obj=NS(type=NS(name='MonoBehaviour'),path_id=1,reader=NS(endian='<'),
               assets_file=NS(name='core',unity_version='6000.3.8f1'),
               serialized_type=NS(nodes=[],class_id=114,old_type_hash=b'x'*16,script_id=b'y'*16),
               get_raw_data=lambda:b'\x04\x00\x00\x00Text')
        other=NS(**{**vars(obj),'assets_file':NS(name='other-core',unity_version='6000.3.8f1'),
                    'get_raw_data':lambda:b'\x04\x00\x00\x00More'})
        with patch.multiple(audit,PC_DATA=game,OUT=out,PC_SCOPE='core',SCHEMAS={}), \
             patch('UnityPy.load',return_value=NS(objects=[obj,other])), \
             patch.object(audit,'read_object_typetree',side_effect=AssertionError('guessed schema used')):
            audit.pc()
        import sqlite3
        db=sqlite3.connect(out/'pc.sqlite')
        self.assertEqual(db.execute("select count(*) from dispositions where status='gap'").fetchone()[0],2)
        self.assertEqual(db.execute("select count(*) from occurrences where role='unverified_binary_string'").fetchone()[0],2)
        self.assertEqual(len(list((out/'pc-failed-objects').rglob('1.bytes'))),2)
        db.close()


if __name__=='__main__':unittest.main()
