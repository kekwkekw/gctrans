"""Small read-only artifact/custody summary; never loads DLLs or calls Unity."""
from pathlib import Path
import configparser
from build_manifests import sha

PREFIX = 'GCMod.Nextgen.Candidate'
FONT_DEFAULT = 'GCMod/fonts/tsukuardgothic-std-medium'
ENTRYPOINTS = ('tools/update/cli.py', 'scripts/crawl_only.py', 'scripts/__init__.py',
               'scripts/crypto.py', 'tools/UpdateCli/Program.cs', 'tools/UpdateCli/UpdateCli.csproj')

def custody(role, path=None):
    row = {'role': role, 'path': str(path) if path is not None else None,
           'status': 'NOT_CHECKED', 'scope': 'READ_ONLY_FILE_METADATA_NO_ABI_OR_GLYPH_PROOF'}
    if path is not None:
        try:
            p = Path(path)
            row['status'] = 'FOUND' if p.is_file() else 'MISSING'
            if row['status'] == 'FOUND': row['size'] = p.stat().st_size
        except OSError: row['status'] = 'UNREADABLE'
    row['exists'] = row['status'] == 'FOUND'
    return row

def plugin_inventory(path):
    result = {'status': 'NOT_CHECKED', 'root': str(path) if path else None, 'risks': [],
              'scope': 'EXPLICIT_PLUGIN_TREE_ONLY; no DLL load, move, disable or replacement'}
    if path is None: return result
    root = Path(path)
    if not root.is_dir(): result['status'] = 'MISSING'; return result
    # Known addon naming from recovered custody. Byte markers are metadata risks,
    # not a decoded BepInPlugin attribute or an ABI/native observation.
    try:
        files = sorted(root.rglob('*.dll'))
        for file in files:
            if file.is_symlink() or not file.resolve().is_relative_to(root.resolve()): continue
            rel = file.relative_to(root).as_posix()
            name = file.stem.lower()
            flags = []
            if name.startswith(('gcui', 'gcuiko', 'gcmodui')):
                flags.append('ORIGINAL_UI_ADDON_MIXTURE_RISK')
            if name == 'gcmod': flags.append('GCMOD_BODY_DUPLICATE_LOAD_RISK_GUID_GCMOD')
            if file.stat().st_size <= 16 * 1024 * 1024:
                raw = file.read_bytes()
                if raw.startswith(b'MZ') and b'\x01\x00\x05GCMod' in raw:
                    flags.append('GCMOD_CUSTOM_ATTRIBUTE_BYTE_MARKER_DUPLICATE_RISK')
            if flags: result['risks'].append({'path': rel, 'reasons': flags,
                                              'guid_confirmation': 'NATIVE_NOT_RUN_METADATA_RISK_ONLY'})
        result['status'] = 'RISKS_FOUND' if result['risks'] else 'NO_KNOWN_METADATA_RISK_FOUND'
        result['dll_files_observed'] = len(files)
    except OSError: result['status'] = 'PARTIAL_UNREADABLE'
    return result

def preflight(candidate_root, dependency_root=None, medium=None, plugins=None):
    root = Path(candidate_root).resolve()
    config = configparser.ConfigParser(); config.optionxform = str
    config.read(root/'staging/GCMod.cfg.template', encoding='utf-8')
    relative = config.get('Nextgen', 'PackageRoot', fallback='')
    manifest = root/'translations/ko/ui/manifest.json'
    font_setting = config.get('Translation.Font', 'AssetBundlePath', fallback=FONT_DEFAULT)
    checks = {'package_root_layout': relative == PREFIX+'/translations/ko' and manifest.is_file(),
              'entrypoints_present': all((root/p).is_file() for p in ENTRYPOINTS),
              'opt_in_off': config.get('Nextgen', 'Enabled', fallback='').lower() == 'false',
              'ui_request_opt_in_off': config.get('Nextgen', 'UiRequestsEnabled', fallback='false').lower() == 'false',
              'check_update_off': config.get('Nextgen', 'CheckUpdate', fallback='').lower() == 'false',
              'locale_ko': config.get('Translation', 'Language', fallback='') == 'ko',
              'xunity_off': config.get('Staging', 'XUnityEnabled', fallback='').lower() == 'false',
              'font_setting_matches_code_default': font_setting == FONT_DEFAULT}
    dlls = sorted(p.relative_to(root).as_posix() for p in (root/'candidate').glob('*.dll'))
    checks['single_candidate_no_addon_mixture'] = dlls == ['candidate/GCMod.dll']
    roles = [('game interop', 'BepInEx/interop/Assembly-CSharp.dll'),
             ('BepInEx IL2CPP core', 'BepInEx/core/BepInEx.Unity.IL2CPP.dll'),
             ('Utility AssetBundleLoader', 'BepInEx/plugins/GCMod/Utility.dll')]
    dependencies = [custody(role, Path(dependency_root)/rel if dependency_root else None) for role, rel in roles]
    font = custody('approved Medium bundle publishing RHR_Medium_GFSAFE_ADH', medium)
    plugins_report = plugin_inventory(plugins)
    artifact_status = 'PASS' if all(checks.values()) else 'FAIL'
    missing = [d['role'] for d in dependencies+[font] if d['status'] != 'FOUND']
    return {'schema': 'gc-ng06-read-only-preflight-v1',
            'status': 'OFFLINE_ARTIFACT_PASS' if artifact_status == 'PASS' else 'ARTIFACT_FAIL',
            'artifact': {'status': artifact_status, 'checks': checks, 'entrypoints': list(ENTRYPOINTS),
                         'scope': 'layout and required source presence; full payload staging is separate'},
            'checks': checks, 'projected_plugins_directory': PREFIX, 'PackageRoot': relative,
            'manifest_source': 'translations/ko/ui/manifest.json',
            'manifest_sha256': sha(manifest.read_bytes()) if manifest.is_file() else None,
            'dependencies': dependencies, 'dependency_axis': 'READ_ONLY_FILE_METADATA_ONLY',
            'font': font, 'font_setting': {'section': 'Translation.Font', 'key': 'AssetBundlePath',
                                         'value': font_setting, 'relative_to': 'BepInEx plugins',
                                         'bundled': False, 'glyph_validation': 'NOT_RUN'},
            'plugins': plugins_report,
            'native_trial': {'status': 'NOT_EXECUTED_BY_PREFLIGHT', 'execution': 'NOT_RUN',
                             'authorization': 'CURRENT_USER_POLICY_SEPARATE_FROM_ARTIFACT_CHECK',
                             'missing_or_unchecked_roles': missing, 'abi': 'NOT_RUN',
                             'glyph': 'NOT_RUN', 'hook': 'NOT_RUN'},
            'source_coverage': 'PARTIAL; latest PC inventory/UI/Novel locator and Names authority separate',
            'native_abi': 'NOT_RUN', 'native_hook': 'NOT_RUN', 'game': 'NOT_RUN',
            'installation': 'NOT_PERFORMED', 'operational_config_writes': 0,
            'xunity_note': 'Staging.XUnityEnabled is an operator assertion, not an XUnity setting writer'}
