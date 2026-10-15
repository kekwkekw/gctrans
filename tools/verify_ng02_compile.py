"""Inspect actual MSBuild Compile items and PE metadata, without executing native code."""
from pathlib import Path
import argparse
import json
import re

def verify(items_path, types_path, report_path):
    items = json.loads(items_path.read_text(encoding='utf-8-sig'))['Items']['Compile']
    types = types_path.read_text(encoding='utf-8-sig')
    source_items = [x for x in items if '/obj/' not in x['FullPath'].replace('\\','/')]
    violations = []
    inspected = []
    forbidden = re.compile(r'\b(?:InitAutoLease|AutoTick|compiledDataSha|HostOnlyProof|ValidateLocalPackageOnLoad)\b')
    for item in source_items:
        path = Path(item['FullPath'])
        text = path.read_text(encoding='utf-8-sig')
        if 'AutoLease' in path.name or path.name == 'NextgenPackageBootstrap.cs':
            violations.append('excluded source compiled: '+path.name)
        if forbidden.search(text): violations.append('forbidden operational path: '+path.name)
        if '/Native/' in path.as_posix() and re.search(r'\b(?:MemoryTextTarget|HostOnly)\b',text):
            violations.append('fixture provider in native source: '+path.name)
        inspected.append(item['Identity'].replace('\\','/'))
    required = ['GCMod.Plugin','GCMod.Candidate.NativeRuntimeBridge','GCMod.Nextgen.RuntimeCoordinator',
                'GCMod.Candidate.LegacyTranslationRuntime','GCMod.Candidate.Native.NativeHookHub',
                'GCMod.Candidate.Native.GeneralUiProvider','GCMod.Candidate.Native.StoryNativeProvider',
                'GCMod.Candidate.Native.BattleNativeProvider','GCMod.Candidate.Native.Fit3NativeProvider']
    for name in required:
        if 'TYPE '+name+' BASE=' not in types: violations.append('missing compiled type: '+name)
    if re.search(r'TYPE .*AutoLease\b',types): violations.append('AutoLease compiled type')
    result = dict(schema='gc-ng02-compile-metadata-audit-v1',status='PASS' if not violations else 'FAIL',
                  compile_items=len(items),source_items=len(source_items),required_actual_types=required,
                  inspected_sources=inspected,violations=violations,fixture_native_providers=0,
                  original_game_assembly_execution=False,native_hooks='NOT_RUN',game='NOT_RUN')
    report_path.parent.mkdir(parents=True,exist_ok=True)
    report_path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status=result['status'],compile_items=len(items),violations=violations)))
    return not violations

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('items',type=Path);p.add_argument('types',type=Path);p.add_argument('report',type=Path)
    a=p.parse_args();raise SystemExit(0 if verify(a.items,a.types,a.report) else 1)
