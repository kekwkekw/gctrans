"""Replay the returned operational ZIP in new scratch; no game, installation or network action.

Uses only source/data in the specified archive, stdlib Python and an installed .NET SDK.
The explicit fixture flag approves only disposable close-label correction and restoration.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import os
import shutil
import stat
import subprocess
import sys
import time
import zipfile


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--scratch', type=Path, required=True)
    parser.add_argument('--dotnet', help='Installed dotnet executable; otherwise PATH lookup')
    parser.add_argument('--discarded-fixture-approved', action='store_true', required=True)
    args = parser.parse_args()
    archive = args.archive.resolve()
    scratch = args.scratch.resolve()
    assert archive.is_file() and not scratch.exists(), 'ARCHIVE_AND_NEW_SCRATCH_REQUIRED'
    assert not any(p.name.casefold()=='bepinex' or (p/'GC.exe').is_file() for p in (scratch,*scratch.parents)), 'OUTSIDE_GAME_SCRATCH_REQUIRED'
    dotnet = args.dotnet or shutil.which('dotnet')
    assert dotnet, 'INSTALLED_DOTNET_REQUIRED'
    scratch.mkdir(parents=True)
    extraction = scratch/'extracted'
    extraction.mkdir()
    report = {
        'schema': 'ng09-extracted-operational-replay-v1', 'status': 'RUNNING',
        'archive': str(archive), 'archive_sha256': digest(archive),
        'scratch': str(scratch), 'commands': [],
        'source': 'ONLY_SEPARATELY_EXTRACTED_RETURN',
        'fixture': 'EXPLICIT_DISCARDED_PACKAGE_TARGET_ONLY',
        'network': 'NOT_PERFORMED; NuGet sources cleared for SDK-only UpdateCli build',
        'native_initialize': 'NOT_RUN', 'game': 'NOT_RUN', 'installation': 'NOT_PERFORMED',
    }
    report_path = scratch/'REPLAY_RESULTS.json'
    try:
        with zipfile.ZipFile(archive) as bundle:
            entries = bundle.infolist()
            assert len(entries) <= 20000 and sum(x.file_size for x in entries) < 512 * 1024 * 1024, 'BOUNDED_RETURN_REQUIRED'
            seen = set()
            for entry in entries:
                name = entry.filename
                part = PurePosixPath(name)
                assert not part.is_absolute() and not name.startswith('/') and '\\' not in name and ':' not in name, 'UNSAFE_ARCHIVE_PATH'
                assert all(x not in ('', '.', '..') for x in part.parts), 'UNSAFE_ARCHIVE_PATH'
                reserved = {'CON','PRN','AUX','NUL'} | {f'{prefix}{n}' for prefix in ('COM','LPT') for n in range(1,10)}
                assert all(not x.endswith((' ', '.')) and x.split('.')[0].upper() not in reserved for x in part.parts), 'WINDOWS_DEVICE_OR_ALIAS_PATH_REFUSED'
                key = name.rstrip('/').casefold()
                assert key not in seen, 'DUPLICATE_CASEFOLDED_ARCHIVE_PATH'
                seen.add(key)
                assert not stat.S_ISLNK(entry.external_attr >> 16) and not entry.flag_bits & 1, 'LINK_OR_ENCRYPTED_MEMBER_REFUSED'
                destination = extraction.joinpath(*part.parts)
                assert destination.resolve().is_relative_to(extraction), 'ARCHIVE_PATH_ESCAPE'
                if entry.is_dir():
                    destination.mkdir(parents=True, exist_ok=True)
                else:
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    with bundle.open(entry) as source, destination.open('xb') as target:
                        shutil.copyfileobj(source, target)
        root = extraction
        if not (root/'candidate/GCMod.dll').is_file():
            children = [x for x in root.iterdir() if x.is_dir()]
            assert len(children) == 1, 'SINGLE_OPERATIONAL_ROOT_REQUIRED'
            root = children[0]
        required = (
            'candidate/GCMod.dll', 'translations/ko/release-manifest.json',
            'tools/update/cli.py', 'tools/verify_staged_release.py',
            'tools/make_trial_fixture.py', 'tools/restore_native.py',
            'tools/UpdateCli/UpdateCli.csproj', 'scripts/crawl_only.py', 'scripts/crypto.py',
        )
        assert all((root/x).is_file() for x in required), 'RETURN_ENTRYPOINT_MISSING'
        report['extracted_root'] = str(root)
        report['candidate_sha256'] = digest(root/'candidate/GCMod.dll')
        environment = os.environ.copy()
        for key in ('PYTHONPATH', 'PYTHONHOME', 'NG03_PRIVATE_CRYPTO_HELPER', 'NG03_PRIVATE_CRYPTO_SHA256'):
            environment.pop(key, None)
        environment.update(PYTHONNOUSERSITE='1', PYTHONSAFEPATH='1', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
        logs = scratch/'logs'
        logs.mkdir()

        def run(name, command, expected=0, parse=False):
            started = time.monotonic()
            result = subprocess.run(list(map(str, command)), cwd=root, env=environment,
                                    capture_output=True, text=True, encoding='utf-8', timeout=900)
            (logs/(name+'.log')).write_text(result.stdout+'\n'+result.stderr, 'utf-8')
            report['commands'].append({'name': name, 'command': list(map(str, command)),
                                       'exit': result.returncode, 'expected_exit': expected,
                                       'elapsed_seconds': round(time.monotonic()-started, 3),
                                       'status': 'PASS' if result.returncode == expected else 'FAIL'})
            assert result.returncode == expected, name+'_UNEXPECTED_EXIT'
            return json.loads(result.stdout) if parse else result.stdout

        py = [sys.executable, '-I', '-S', '-X', 'utf8']
        cli = root/'tools/update/cli.py'
        run('operator-help', [*py, cli, '--help'])
        run('restore-help', [*py, root/'tools/restore_native.py', '--help'])
        run('safe-imports', [*py, '-c', 'import sys;sys.path.insert(0,'+repr(str(root))+');import scripts.crawl_only,scripts.crypto,tools.update.source_scan;print("safe imports PASS")'])
        preflight = run('artifact-preflight', [*py, cli, 'preflight', '--candidate-root', root, '--report', scratch/'PREFLIGHT.json'], parse=True)
        assert preflight['artifact']['status'] == 'PASS' and preflight['native_trial']['status'] == 'NOT_EXECUTED_BY_PREFLIGHT'
        staging = run('whole-return-staging', [*py, root/'tools/verify_staged_release.py', root/'translations/ko', '--report', scratch/'STAGING.json'], parse=True)
        assert staging['status'] == 'PASS'
        run('discarded-ui-next-package-chain', [*py, root/'tools/make_trial_fixture.py', '--implementation', root,
                                                '--scratch', scratch/'fixture', '--discarded-fixture-approved'])
        fixture = json.loads((scratch/'fixture/FIXTURE.json').read_text('utf-8'))
        assert Path(fixture['initial_root']).resolve() == (root/'translations/ko').resolve()
        for name in ('trial', 'restore'):
            planned = json.loads((scratch/f'fixture/{name}-draft.json').read_text('utf-8'))
            reviewed = json.loads((scratch/f'fixture/{name}-reviewed.json').read_text('utf-8'))
            applied = json.loads((scratch/f'fixture/{name}-apply.json').read_text('utf-8'))
            assert planned['rows'][0]['decision'] == 'HOLD' and reviewed['rows'][0]['decision'] == 'APPROVE'
            assert applied['status'] == 'PASS' and applied['consumed_count'] == 1
            assert applied['apply_metrics']['unchanged_novel_payload_reads'] == applied['apply_metrics']['unchanged_novel_payload_hashes'] == 0
        original = json.loads((root/'translations/ko/ui/resources.json').read_text('utf-8'))
        restored = json.loads((Path(fixture['restored'])/'ui/resources.json').read_text('utf-8'))
        assert original == restored, 'DISCARDED_FIXTURE_SEMANTIC_RESTORE_REQUIRED'
        nuget = scratch/'NuGet.Config'
        nuget.write_text('<configuration><packageSources><clear /></packageSources></configuration>', 'utf-8')
        run('update-cli-build', [dotnet, 'build', root/'tools/UpdateCli/UpdateCli.csproj', '-c', 'Release',
                                 '-p:NuGetAudit=false', '-p:RestoreConfigFile='+str(nuget), '-m:1', '-v:quiet'])
        runner = root/'tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll'
        assert runner.is_file(), 'UPDATE_CLI_BUILD_OUTPUT_REQUIRED'
        assert digest(runner.with_name('GCMod.dll')) == report['candidate_sha256'], 'ACTUAL_CANDIDATE_REFERENCE_REQUIRED'
        store = scratch/'isolated-store'
        prepared = run('isolated-prepare-local', [dotnet, runner, 'prepare-local', '--store', store,
                                                  '--package', fixture['restored'], '--based-on', 'NONE'], parse=True)
        assert prepared['reason'] == 'LOCAL_COMPLETE_PENDING_SAVED' and prepared['pending_saved']
        assert prepared['based_on'] is None and prepared['current'] is None and prepared['previous'] is None
        assert not prepared['current_changed'] and not prepared['previous_changed']
        assert prepared['native_initialize'] == 'NOT_RUN' and not (store/'current.json').exists() and not (store/'previous.json').exists()
        assert (store/'pending_restart.json').is_file() and digest(root/'candidate/GCMod.dll') == report['candidate_sha256']
        (scratch/'PREPARE_LOCAL.json').write_text(json.dumps(prepared, indent=2), 'utf-8')
        report.update(status='PASS', staging=staging, fixture=fixture,
                      unchanged_novel_app_read_hash_per_apply=0,
                      os_copy_cost='See fixture trial-apply.json and restore-apply.json; not asserted zero',
                      prepare_local=prepared,
                      environment={'PYTHONPATH': 'REMOVED', 'PYTHONHOME': 'REMOVED', 'private_helper': 'NOT_SUPPLIED',
                                   'entrypoints': 'ISOLATED_STDLIB_PYTHON', 'original_workspace_imports': 'NOT_USED'})
    except Exception as failure:
        report.update(status='FAIL', reason=str(failure) if isinstance(failure, AssertionError) else type(failure).__name__)
        raise
    finally:
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
        print(json.dumps({'status': report['status'], 'report': str(report_path)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
