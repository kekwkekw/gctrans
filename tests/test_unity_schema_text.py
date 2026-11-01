import json
from pathlib import Path
import struct
from types import SimpleNamespace as NS
import unittest

from tools.update.unity_schema_text import schema_key, nodes_sha, read_verified_schema, carve_unity_strings


class ExactSchemaTests(unittest.TestCase):
    def fixture(self, raw=None):
        body=(struct.pack('<iqB3x',0,101,1)+struct.pack('<iq',0,202)
              +struct.pack('<iiidii',0,0,1,0.0,0,0)) if raw is None else raw
        obj=NS(type=NS(name='PlayableDirector'),reader=NS(endian='<'),
               assets_file=NS(unity_version='6000.3.8f1'),
               serialized_type=NS(class_id=320,old_type_hash=b'x'*16,script_id=b''),
               get_raw_data=lambda:body)
        nodes=json.loads((Path(__file__).parent/'fixtures/playable_director_unity6000_typetree.json').read_bytes())
        key=schema_key(obj)
        registry={key:{'key':list(key),'nodes':nodes,'nodes_sha256':nodes_sha(nodes),'source':{'path':'synthetic','sha256':'synthetic'}}}
        return obj,registry

    def test_exact_schema_consumes_and_roundtrips_without_global_switch(self):
        from UnityPy.helpers import TypeTreeHelper
        native=TypeTreeHelper.read_typetree_c
        obj,schemas=self.fixture();tree,receipt=read_verified_schema(obj,schemas)
        self.assertTrue(receipt['roundtrip_equal'])
        self.assertEqual(receipt['consumed_bytes'],len(obj.get_raw_data()))
        self.assertIs(native,TypeTreeHelper.read_typetree_c)
        self.assertIn('m_Enabled',tree)

    def test_unavailable_or_changed_identity_is_rejected(self):
        for field in ('version','class','hash','script','ambiguous','nodes'):
            with self.subTest(field=field):
                obj,schemas=self.fixture()
                if field=='version':obj.assets_file.unity_version='6000.3.9f1'
                elif field=='class':obj.serialized_type.class_id=1
                elif field=='hash':obj.serialized_type.old_type_hash=b'y'*16
                elif field=='script':obj.serialized_type.script_id=b'z'*16
                elif field=='ambiguous':schemas[schema_key(obj)]=None
                else:schemas[schema_key(obj)]['nodes_sha256']='changed'
                with self.assertRaises(ValueError):read_verified_schema(obj,schemas)

    def test_trailing_bytes_and_changed_padding_fail_closed(self):
        obj,_=self.fixture();body=obj.get_raw_data();bad=bytearray(body);bad[13]=7
        for raw in (body+b'\x00'*4,bytes(bad)):
            obj,schemas=self.fixture(raw)
            with self.assertRaises(ValueError):read_verified_schema(obj,schemas)

    def test_unresolved_hints_are_not_asserted_as_fields(self):
        def segment(s):
            raw=s.encode();return struct.pack('<i',len(raw))+raw+b'\0'*((-len(raw))%4)
        raw=segment('English UI')+segment('表示')+b'\xff'*8
        hints=list(carve_unity_strings(raw))
        self.assertEqual([h['value'] for h in hints],['English UI','表示'])
        self.assertTrue(all(h['status'].startswith('HEURISTIC_') for h in hints))


if __name__=='__main__':unittest.main()
