"""Reuse an exact installed-cache type schema for read-only stripped-object audit.

Version, class, script identity and type hash must match. A full byte round trip
is mandatory; no global UnityPy settings or source object is modified.
"""
import hashlib
import json
from ctypes import c_uint32


def schema_key(obj):
    st=obj.serialized_type
    old=bytes(st.old_type_hash)
    if len(old)!=16 or not any(old):raise ValueError('SCHEMA_TYPE_HASH_REQUIRED')
    script=bytes(getattr(st,'script_id',b''))
    return (str(obj.assets_file.unity_version),int(st.class_id),old.hex(),script.hex())


def node_dicts(nodes):
    return [{key:getattr(n,key) for key in dir(n) if key.startswith('m_')} for n in nodes]


def nodes_sha(nodes):
    return hashlib.sha256(json.dumps(nodes,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def read_verified_schema(obj, schemas):
    from UnityPy.helpers import TypeTreeHelper
    from UnityPy.streams import EndianBinaryReader, EndianBinaryWriter
    key=schema_key(obj)
    entry=schemas.get(key)
    if not entry:raise ValueError('EXACT_SCHEMA_UNAVAILABLE_OR_AMBIGUOUS')
    if tuple(entry['key'])!=key:raise ValueError('SCHEMA_IDENTITY_MISMATCH')
    if nodes_sha(entry['nodes'])!=entry['nodes_sha256']:raise ValueError('SCHEMA_NODES_CHANGED')
    nodes=TypeTreeHelper.check_nodes(entry['nodes'])
    raw=obj.get_raw_data()
    reader=EndianBinaryReader(raw,endian=obj.reader.endian)
    tree=TypeTreeHelper.read_value(nodes,reader,c_uint32(0))
    if reader.Position!=len(raw):raise ValueError('SCHEMA_OBJECT_LENGTH_MISMATCH')
    written=TypeTreeHelper.write_typetree(tree,nodes,EndianBinaryWriter(endian=obj.reader.endian)).bytes
    if written!=raw:raise ValueError('SCHEMA_OBJECT_ROUNDTRIP_MISMATCH')
    # Reject malformed text even if the library's surrogate handling round trips.
    def strings(node, path=()):
        if isinstance(node,str):node.encode('utf8',errors='strict')
        elif isinstance(node,dict):
            for k,v in node.items():
                strings(k)
                # Unity TextAsset stores arbitrary bytes in this string slot.
                # The caller decodes/classifies the payload independently.
                if obj.type.name=='TextAsset' and not path and k=='m_Script':continue
                strings(v,path+(k,))
        elif isinstance(node,(list,tuple)):
            for v in node:
                if isinstance(v,(str,dict,list,tuple)):strings(v,path+(0,))
    strings(tree)
    return tree,{'policy':'exact-cache-schema-and-byte-roundtrip-v1','key':list(key),
        'nodes_sha256':entry['nodes_sha256'],'schema_source':entry['source'],
        'object_sha256':hashlib.sha256(raw).hexdigest(),'consumed_bytes':reader.Position,'roundtrip_equal':True}


def carve_unity_strings(raw, endian='<'):
    """Unverified field hints from unresolved objects, never translation input.

    Every aligned, length-prefixed, strictly decodable printable UTF-8 segment
    is retained. Offsets are evidence, not a claim about field semantics.
    """
    import struct
    for offset in range(0,len(raw)-4,4):
        length=struct.unpack_from(endian+'i',raw,offset)[0]
        if length<=0 or length>len(raw)-offset-4:continue
        end=offset+4+length
        aligned=(end+3)&~3
        if aligned>len(raw) or any(raw[end:aligned]):continue
        try:value=raw[offset+4:end].decode('utf8',errors='strict')
        except UnicodeError:continue
        if not all(c.isprintable() or c in '\r\n\t' for c in value):continue
        yield {'offset':offset,'byte_length':length,'value':value,
               'status':'HEURISTIC_LENGTH_PREFIXED_UTF8_NOT_VERIFIED_FIELD'}


def load_registry(path, allowed_root):
    from pathlib import Path
    from .asset_source import file_hash
    data=json.loads(Path(path).read_bytes());root=Path(allowed_root).resolve()
    for source,digest in data['sources'].items():
        p=Path(source).resolve()
        if not p.is_relative_to(root):raise ValueError('SCHEMA_SOURCE_OUTSIDE_PC_ROOT')
        if file_hash(p)!=digest:raise ValueError('SCHEMA_SOURCE_CHANGED')
    schemas={tuple(e['key']):e for e in data['schemas']}
    for entry in schemas.values():
        src=entry['source']
        if data['sources'].get(src['path'])!=src['sha256']:raise ValueError('SCHEMA_SOURCE_UNBOUND')
    for key in data['ambiguous_keys']:schemas[tuple(key)]=None
    return schemas


def harvest_registry(game, core, output):
    """Collect matching schemas only from this installation's existing cache."""
    import UnityPy
    from .asset_source import file_hash
    targets=set()
    for path in core:
        for obj in UnityPy.load(str(path)).objects:
            try:targets.add(schema_key(obj))
            except (ValueError,TypeError):pass
    schemas={};sources={};collisions=set();scanned=0
    for path in sorted((game/'Caches').rglob('__data')):
        env=UnityPy.load(str(path));visited=set();scanned+=1
        for obj in env.objects:
            st=obj.serialized_type
            if id(st) in visited or not st.nodes:continue
            visited.add(id(st))
            try:key=schema_key(obj)
            except (ValueError,TypeError):continue
            if key not in targets:continue
            nodes=node_dicts(st.nodes);digest=nodes_sha(nodes)
            if key in schemas:
                if schemas[key]['nodes_sha256']!=digest:collisions.add(key)
                continue
            if str(path) not in sources:sources[str(path)]=file_hash(path)
            schemas[key]={'key':list(key),'nodes':nodes,'nodes_sha256':digest,
                          'source':{'path':str(path),'sha256':sources[str(path)]}}
        if scanned%200==0:print(json.dumps({'cache_schemas_scanned':scanned,'matched_schemas':len(schemas)}),flush=True)
    data={'schemas':list(schemas.values()),'ambiguous_keys':[list(k) for k in sorted(collisions)],
          'sources':sources,'target_schema_count':len(targets),
          'unmatched_keys':[list(k) for k in sorted(targets-schemas.keys())]}
    output.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    return load_registry(output,game)
