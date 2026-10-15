"""Synthetic file-only scope/restore checks; no native/runtime proof or real game writes."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import sys

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--tools-root', type=Path, required=True)
p.add_argument('--scratch', type=Path, required=True)
a = p.parse_args()
tools = a.tools_root.resolve()
scratch = a.scratch.resolve()
assert not scratch.exists(), 'NEW_SCRATCH_REQUIRED'
scratch.mkdir(parents=True)
game = scratch/'synthetic-game'
impl = scratch/'synthetic-implementation'
work = scratch/'trial'
medium = scratch/'not-a-real-font.bundle'
cfg = game/'BepInEx/config/GCMod.cfg'
body = game/'BepInEx/plugins/GCMod/GCMod.dll'
utility = body.with_name('Utility.dll')
for f, value in {
    cfg: b'[Translation]\nEnabled = true\nLanguage = ko\n[Translation.Font]\nAssetBundlePath = test-only\n[Translation.MasterData]\nEnabledTables = mUnitWords, mSubunitWords\n[Personal]\nPreserve = unchanged\n',
    body: b'CPU fixture original body, not a DLL',
    utility: b'CPU fixture utility, not a DLL',
    impl/'candidate/GCMod.dll': b'CPU fixture candidate, not a DLL',
    impl/'translations/ko/release-manifest.json': b'{"synthetic_file_only":true}',
    medium: b'CPU fixture custody existence, not font proof'
}.items():
    f.parent.mkdir(parents=True, exist_ok=True)
    f.write_bytes(value)
original = {str(x): x.read_bytes() for x in (cfg, body, utility)}
results = []

def call(name, extra=(), expected=0):
    cmd = [sys.executable, '-X', 'utf8', str(tools/name), '--work-root', str(work),
           '--implementation-root', str(impl), '--game-root', str(game), *map(str, extra)]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8')
    assert (r.returncode == 0) == (expected == 0), (name, r.returncode, r.stdout, r.stderr)
    results.append({'command': name, 'expected': 'PASS' if expected == 0 else 'GUARD_REJECTION',
                    'exit': r.returncode})
    return r

call('prepare_native.py', ['--medium', medium])
assert cfg.read_bytes() == original[str(cfg)] and body.exists()
call('stage_native.py', ['--execute'])
assert not body.exists() and utility.read_bytes() == original[str(utility)]
call('stage_native.py', ['--execute'], expected=1)
trial_cfg = cfg.read_bytes()
cfg.write_bytes(trial_cfg+b'\n[ConcurrentUser]\nUnrelated = preserved\n')
concurrent = cfg.read_bytes()
call('restore_native.py', expected=1)
assert cfg.read_bytes() == concurrent and not body.exists(), 'FAILED_GUARD_MUST_NOT_WRITE'
cfg.write_bytes(trial_cfg)
call('restore_native.py')
assert not body.exists(), 'DRY_RESTORE_MUST_NOT_WRITE'
call('restore_native.py', ['--execute'])
assert all(Path(k).read_bytes() == v for k,v in original.items())
assert not (game/'BepInEx/plugins/GCMod.Nextgen.Candidate').exists()
assert not (game/'BepInEx/config/GCMod.Nextgen.local').exists()
report = {'schema': 'ng09-native-helper-file-only-v1', 'status': 'PASS', 'checks': results,
          'control_scope': 'Synthetic local file stage/CAS/external-writer guard/dry-run/exact restore',
          'candidate_or_native_proof': 'NONE; fixture .dll and font bytes are not executable',
          'native': 'NOT_RUN', 'actual_game_writes': 0,
          'original_files_restored': {Path(k).name: hashlib.sha256(v).hexdigest() for k,v in original.items()}}
(scratch/'RESULT.json').write_text(json.dumps(report, indent=2), 'utf-8')
print(json.dumps(report, indent=2))
