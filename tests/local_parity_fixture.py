from pathlib import Path
import json
import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
from update.master_coverage import save, sha
from export_local_ui import export
from build_manifests import build
from legacy_semantic import compute, root_hash


def fixture(repo):
    (repo/'translation').mkdir(parents=True)
    (repo/'translation/knowledge.jsonl').write_bytes(b'')
    package = repo/'translations/ko'
    values = {'master.json': {'mTextConfig': {'ml_value[]': {'既存': '기존'}}},
              'names.json': {'名前':'이름'}, 'novels/1.json': {'本文':'본문'},
              'ui/resources.json': {'resources':[]}, 'ui/bindings.json': {'bindings':[]},
              'ui/general-exact.reviewed.json': {},
              'ui/detail-families.json': json.loads((ROOT/'translations/ko/ui/detail-families.json').read_bytes()),
              'ui/local-coverage.json': {'schema':'gc-local-ui-coverage-v1','keys':['既存'],'keep_source':[]},
              'ui.templates.json': {'rules':[{'jp':'工房{value}','ko':'공방{value}','value_kind':'integer'}]}}
    for name,value in values.items(): save(package/name,value)
    manifest = {'master':compute(values['master.json']), 'names':compute(values['names.json']),
                'novels':{'1':compute(values['novels/1.json'])}}
    manifest['hash'] = root_hash(manifest);save(package/'manifest.json',manifest)
    (package/'ui.json').write_bytes(export(package)[0])
    components = ['master.json','names.json','novels/1.json','manifest.json','ui.json','ui.templates.json','ui/resources.json','ui/bindings.json']
    receipt = repo/'bootstrap.json'
    save(receipt, {'files':[{'path':p,'bytes':(package/p).stat().st_size,'sha256':sha(package/p)} for p in components]})
    build(package,receipt)
    (repo/'candidate').mkdir();(repo/'candidate/GCMod.cfg').write_bytes((ROOT/'candidate/GCMod.cfg').read_bytes())
    (repo/'candidate/GCMod.dll').write_bytes(b'MZ synthetic test DLL')
    (repo/'THIRD_PARTY_LICENSES').mkdir();(repo/'THIRD_PARTY_LICENSES/gc-mod-local-LICENSE.txt').write_text('test license')
    save(repo/'baseline.json', {'mTextConfig':{'ml_value[]':{'既存':'ignored','追加':'ignored'}}})
    return package
