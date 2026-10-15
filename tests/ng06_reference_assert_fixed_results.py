"""Assert actual CLI outcomes produced by review_boundaries.py; no product stubs."""
import json
from pathlib import Path
import sys
if len(sys.argv) != 2:
    raise SystemExit('Usage: assert_fixed_results.py BOUNDARY_REPRODUCTIONS.json')
r=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
scan=r['G01_scan_without_PC_locator']
assert scan['exit']==2 and scan['report_exists'] and scan['json']['operation_status']=='OBSERVATION_UNAVAILABLE',scan
for name in ('G02_mixed_ui_with_HOLD','G02_mixed_ui_with_APPROVE'):
    case=r[name]
    assert case['exit']==2 and case['status']=='PARTIAL_HOLDS',case
    assert case['next_package_created'] and case['ui_ko']=='CPU fixture correction',case
    statuses=[o['status'] for o in case['outcomes']]
    assert statuses.count('APPLIED')==1 and statuses.count('HELD')==1,statuses
assert r['G02_mixed_ui_with_APPROVE']['observed_target_ko_preserved'] is True
# Preflight is intentionally not part of the reference patch. Its output only
# establishes layout/existence reporting, never native readiness.
print('PASS: scanner typed failure, independent UI commit, held source scope, original observed KO preserved; native/game NOT_RUN')
