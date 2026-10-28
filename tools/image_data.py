"""Validate the bounded runtime PNG catalog; no game or network access."""
import hashlib
import json
import re
import struct

def validate_images(catalog_raw, read):
    catalog = json.loads(catalog_raw)
    if catalog.get('schema') != 'gc-images-v1' or not isinstance(catalog.get('entries'),list) or len(catalog['entries']) > 128:
        raise ValueError('IMAGE_CATALOG_SCHEMA')
    targets=set();paths=set();total=0
    for e in catalog['entries']:
        if (set(e) != {'kind','name','width','height','file','sha256','bytes'}
            or e['kind'] not in ('sprite','texture') or not isinstance(e['name'],str) or not 1 <= len(e['name']) <= 160
            or any(type(e[n]) is not int or not 1 <= e[n] <= 4096 for n in ('width','height'))
            or type(e['bytes']) is not int or not 33 <= e['bytes'] <= 8*1024*1024
            or not isinstance(e['sha256'],str) or not re.fullmatch('[0-9a-f]{64}',e['sha256'])
            or e['file'] != 'images/'+e['sha256']+'.png'):
            raise ValueError('IMAGE_ENTRY')
        key=(e['kind'],e['name'],e['width'],e['height'])
        if key in targets: raise ValueError('IMAGE_DUPLICATE_TARGET')
        targets.add(key);paths.add(e['file']);total+=e['bytes']
        raw=read(e['file'])
        if (len(raw)!=e['bytes'] or hashlib.sha256(raw).hexdigest()!=e['sha256']
            or raw[:8]!=b'\x89PNG\r\n\x1a\n' or raw[12:16]!=b'IHDR'
            or struct.unpack('>II',raw[16:24])!=(e['width'],e['height'])):
            raise ValueError('IMAGE_PAYLOAD:'+e['name'])
    if total>64*1024*1024:raise ValueError('IMAGE_TOTAL_SIZE')
    return paths
