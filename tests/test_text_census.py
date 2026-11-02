import json
from pathlib import Path
import tempfile
import unittest

from tools.update.text_census import Census, walk_text, decode_textasset, master_role


class TextCensusTests(unittest.TestCase):
    def test_nested_fields_keys_and_locale_tails_are_retained(self):
        data={'unknown':{'表示キー':['日本語','English','繁體中文','']},'caption':'Confirm purchase?'}
        rows=list(walk_text(data))
        self.assertIn((('unknown','表示キー'),'表示キー',True),rows)
        self.assertEqual([v for p,v,k in rows if not k],['日本語','English','繁體中文','','Confirm purchase?'])

    def test_master_unknown_arrays_are_not_silently_first_language(self):
        self.assertEqual(master_role((0,'ml_name',2),False,'mItems',{},'English')[0],'master_locale_tail')
        self.assertEqual(master_role((0,'ml_name',0),False,'mItems',{},'名前'),('master_locale_primary',('mItems','ml_name[]'),'名前'))
        self.assertEqual(master_role((0,'captions',1),False,'mItems',{},'二番目')[0],'master_array_unresolved')
        self.assertEqual(master_role((0,'nested','caption'),False,'mItems',{},'Nested text')[0],'master_nested')
        self.assertEqual(master_role((0,'name'),False,'mItems',{'name|':{}},'名前|なまえ')[2],'名前')

    def test_container_name_does_not_exclude_display_text(self):
        self.assertEqual(decode_textasset('回復しました'.encode(),'spine_notification')[0],'text')
        self.assertEqual(decode_textasset(b'{"caption":"Click here"}','skeleton_hint.json')[0],'text')
        self.assertEqual(decode_textasset(b'{"skeleton":"Click here"}','skeleton_hint.json')[0],'text')
        self.assertEqual(decode_textasset(b'{"bones":[],"slots":[]}','payload')[1],'spine_json_structure')
        self.assertEqual(decode_textasset(b'@UTF'+bytes(20),'voice')[1],'cri_audio_table')

    def test_binary_gap_remains_explicit_and_utf16_is_supported(self):
        self.assertEqual(decode_textasset(b'\xff\x00\x81','unknown')[0:2],('gap','UNDECODED_TEXTASSET'))
        self.assertEqual(decode_textasset('復帰します'.encode('utf-16'),'text')[2],'復帰します')
        binary=b'\x05hash\x064.2.1'+b'\x00\xff'
        self.assertEqual(decode_textasset(binary,'effect.skel')[1],'spine_binary_version_header')
        self.assertEqual(decode_textasset(binary,'unidentified')[0],'gap')
        self.assertEqual(decode_textasset(b'GC Movie\0'+b'\xff'*16,'scene.usm')[1],'gc_wrapped_usm_video')
        self.assertEqual(decode_textasset(b'GC Movie\0'+b'\xff'*16,'unknown')[0],'gap')

    def test_structural_aggregation_keeps_counts_and_content_rows(self):
        base=Path(__file__).parent/'scratch-text-census';base.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=base) as tmp:
            c=Census(Path(tmp)/'census.sqlite')
            aid,_=c.asset('synthetic','mixed_spine_bundle','v','raw')
            for index in range(100):c.add(aid,'m_Text','structural_key',{'index':index})
            c.add(aid,'日本語','ui_display',{'field':'m_Text'})
            c.add(aid,'English UI','serialized_string',{'field':'unknown'})
            c.complete(aid);result=c.finish()
            self.assertEqual(result['occurrences'],2)
            self.assertEqual(result['structural_or_empty_occurrences'],100)
            self.assertEqual(result['texts'],3)
            self.assertEqual(c.asset('synthetic','mixed_spine_bundle','v','raw'),(aid,True))
            with self.assertRaisesRegex(ValueError,'SOURCE_CHANGED'):
                c.asset('synthetic','mixed_spine_bundle','v','changed')
            c.db.close()


if __name__=='__main__':unittest.main()
