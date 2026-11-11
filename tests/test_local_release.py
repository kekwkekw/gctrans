import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile
import hashlib
from translation_update_fixture import fixture
import local_release as release


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);fixture(self.root)
        self.archive=self.root/'update.zip';release.pack(self.root,self.archive)
        self.game=self.root/'game';(self.game/'BepInEx/core').mkdir(parents=True);(self.game/'GC.exe').write_bytes(b'test')
        for p in (release.PLUGIN+'Utility.dll',release.PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle'):
            t=self.game/p;t.parent.mkdir(parents=True,exist_ok=True);t.write_bytes(b'local dependency')
        self.backup=self.root/'backup'
    def tearDown(self):self.temp.cleanup()

    def test_pack_deterministic_and_install_rollback_preserves_preferences(self):
        again=self.root/'again.zip';release.pack(self.root,again);self.assertEqual(self.archive.read_bytes(),again.read_bytes())
        cfg=self.game/release.CFG;cfg.parent.mkdir(parents=True,exist_ok=True)
        old=('\ufeff# 사용자 주석\r\n[General]\r\nFrameRate = 144\r\nCheckUpdate = false\r\n'
             '[Message.Window]\r\nNormalAlpha = 0.19\r\nCgModeAlpha = 0.4\r\n'
             '[Message.Text]\r\nModified = false\r\nCharacterSpacing = 0.7\r\n'
             '[Translation]\r\nEnabled = false\r\nLanguage = custom\r\nCDN = offline\r\n'
             'CustomPath = D:/review translations\r\nAsyncMode = true\r\n'
             '[Translation.MasterData]\r\nEnabledTables = mItems\r\n'
             '[Unknown]\r\nKeep = future user setting\r\n').encode('utf-8')
        cfg.write_bytes(old)
        with patch.object(release,'game_closed'):
            result=release.install(self.archive,self.game,self.backup)
            self.assertGreater(result['changed_files'],0)
            self.assertEqual(result['config_policy'],'preserve-existing')
            self.assertEqual(cfg.read_bytes(),old)
            installed=release.cfg_read(cfg.read_bytes());self.assertEqual(installed['General']['FrameRate'],'144')
            self.assertEqual(installed['Translation.MasterData']['EnabledTables'],'mItems')
            journal=json.loads((self.backup/'install.json').read_bytes())
            self.assertNotIn(release.CFG,[r['path'] for r in journal['files']])
            release.rollback(self.game,self.backup)
        self.assertEqual(cfg.read_bytes(),old);self.assertFalse((self.game/release.PLUGIN/'GCMod.dll').exists())

    def test_new_install_uses_package_defaults_and_rollback_removes_new_config(self):
        with patch.object(release,'game_closed'):
            result=release.install(self.archive,self.game,self.backup)
            self.assertEqual(result['config_policy'],'new-defaults')
            raw=(self.game/release.CFG).read_bytes()
            self.assertEqual(raw,(self.root/'candidate/GCMod.cfg').read_bytes())
            cfg=release.cfg_read(raw)
            self.assertEqual(cfg['Message.Text']['CharacterSpacing'],'0')
            self.assertFalse(cfg.has_option('General','CheckUpdate'))
            self.assertFalse(cfg.has_option('Battle','IsSkipCutin'))
            self.assertTrue(release.required_cfg(cfg))
            release.rollback(self.game,self.backup)
        self.assertFalse((self.game/release.CFG).exists())

    def test_explicit_korean_profile_changes_translation_only_and_rolls_back(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        old=(b'# preserve in backup\r\n[General]\r\nFrameRate = 144\r\n'
             b'[Message.Window]\r\nNormalAlpha = 0.43\r\n'
             b'[Translation]\r\nEnabled = false\r\nLanguage = custom\r\nCDN = offline\r\n'
             b'CustomPath = D:/review\r\nAsyncMode = true\r\n'
             b'[Translation.MasterData]\r\nEnabledTables = mItems\r\n'
             b'[Translation.Font]\r\nAssetBundlePath = missing-custom.bundle\r\n'
             b'[Unknown]\r\nKeep = user value\r\n')
        target.write_bytes(old)
        with patch.object(release,'game_closed'):
            result=release.install(self.archive,self.game,self.backup,apply_korean_profile=True)
            self.assertEqual(result['config_policy'],'apply-korean-profile')
            cfg=release.cfg_read(target.read_bytes());defaults=release.cfg_read((self.root/'candidate/GCMod.cfg').read_bytes())
            for section in ('Translation','Translation.MasterData','Translation.Font'):
                self.assertEqual(dict(cfg[section]),dict(defaults[section]))
            self.assertEqual(cfg['General']['FrameRate'],'144')
            self.assertEqual(cfg['Message.Window']['NormalAlpha'],'0.43')
            self.assertEqual(cfg['Unknown']['Keep'],'user value')
            self.assertEqual((self.backup/release.CFG).read_bytes(),old)
            release.rollback(self.game,self.backup)
        self.assertEqual(target.read_bytes(),old)

    def test_existing_config_edits_after_update_survive_dll_rollback(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(b'[General]\nFrameRate = 60\n')
        edited=b'[General]\nFrameRate = 30\n# edited after installation\n'
        with patch.object(release,'game_closed'):
            release.install(self.archive,self.game,self.backup)
            target.write_bytes(edited)
            release.rollback(self.game,self.backup)
        self.assertEqual(target.read_bytes(),edited)

    def test_config_edit_during_update_is_not_overwritten_by_old_snapshot(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(b'[General]\nFrameRate = 60\n')
        edited=b'[General]\nFrameRate = 30\n# changed during preflight\n'
        original=release.safe_target
        def edit_during_dependency_check(root,relative):
            if relative==release.PLUGIN+'Utility.dll':target.write_bytes(edited)
            return original(root,relative)
        with patch.object(release,'game_closed'),patch.object(release,'safe_target',edit_during_dependency_check):
            release.install(self.archive,self.game,self.backup)
        self.assertEqual(target.read_bytes(),edited)
        self.assertNotIn(release.CFG,[r['path'] for r in json.loads((self.backup/'install.json').read_bytes())['files']])

    def test_configured_font_relative_or_absolute_is_preserved_and_checked(self):
        (self.game/release.PLUGIN/'fonts/RHR_Medium_GFSAFE_ADH.bundle').unlink()
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        for index,relative in enumerate((True,False)):
            with self.subTest(relative=relative):
                font=self.game/'BepInEx/plugins/custom/font.bundle' if relative else self.root/'external-font.bundle'
                font.parent.mkdir(parents=True,exist_ok=True);font.write_bytes(b'user font')
                value='custom/font.bundle' if relative else str(font)
                original=('[Translation.Font]\nAssetBundlePath = '+value+'\n').encode('utf-8')
                target.write_bytes(original)
                with patch.object(release,'game_closed'):
                    backup=self.root/('backup-font-'+str(index))
                    release.install(self.archive,self.game,backup)
                    self.assertEqual(target.read_bytes(),original)
                    release.rollback(self.game,backup)
                self.assertEqual(font.read_bytes(),b'user font')

    def test_missing_configured_font_blocks_before_file_changes(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        original=b'[Translation.Font]\nAssetBundlePath = missing.bundle\n';target.write_bytes(original)
        with patch.object(release,'game_closed'):
            with self.assertRaisesRegex(ValueError,'LOCAL_DEPENDENCY_REQUIRED:configured font'):
                release.install(self.archive,self.game,self.backup)
        self.assertFalse(self.backup.exists())
        self.assertEqual(target.read_bytes(),original)
        self.assertFalse((self.game/release.PLUGIN/'GCMod.dll').exists())

    def test_local_complete_supplies_missing_default_dependencies(self):
        font=self.root/'bundled-font';font.write_bytes(b'bundled font')
        utility=self.root/'bundled-utility';utility.write_bytes(b'bundled utility')
        archive=self.root/'complete.zip';release.pack(self.root,archive,font,utility)
        for relative in (release.PLUGIN+'Utility.dll',release.PLUGIN+'fonts/RHR_Medium_GFSAFE_ADH.bundle'):
            (self.game/relative).unlink()
        with patch.object(release,'game_closed'):
            release.install(archive,self.game,self.backup)
            self.assertEqual((self.game/release.PLUGIN/'fonts/RHR_Medium_GFSAFE_ADH.bundle').read_bytes(),b'bundled font')
            self.assertEqual((self.game/release.PLUGIN/'Utility.dll').read_bytes(),b'bundled utility')
            release.rollback(self.game,self.backup)
        self.assertFalse((self.game/release.PLUGIN/'Utility.dll').exists())

    def test_profile_install_failure_restores_original_config(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        old=b'# original\n[General]\nFrameRate = 144\n';target.write_bytes(old)
        original=Path.write_bytes
        def fail(path,raw):
            if path==self.game/release.PLUGIN/'GCMod.dll':raise OSError('injected disk failure')
            return original(path,raw)
        with patch.object(release,'game_closed'),patch.object(Path,'write_bytes',fail):
            with self.assertRaises(OSError):release.install(self.archive,self.game,self.backup,apply_korean_profile=True)
        self.assertEqual(target.read_bytes(),old)
        self.assertEqual(json.loads((self.backup/'install.json').read_bytes())['status'],'rolled_back')

    def test_invalid_existing_config_is_not_replaced_with_defaults(self):
        target=self.game/release.CFG;target.parent.mkdir(parents=True,exist_ok=True)
        old=b'not a config section\n';target.write_bytes(old)
        with patch.object(release,'game_closed'):
            with self.assertRaises(release.configparser.Error):release.install(self.archive,self.game,self.backup)
        self.assertEqual(target.read_bytes(),old)
        self.assertFalse(self.backup.exists())

    def test_changed_install_refuses_rollback(self):
        with patch.object(release,'game_closed'):
            release.install(self.archive,self.game,self.backup)
            dll=self.game/release.PLUGIN/'GCMod.dll';dll.write_bytes(b'user changed')
            with self.assertRaisesRegex(ValueError,'INSTALL_CHANGED'):release.rollback(self.game,self.backup)
            self.assertEqual(dll.read_bytes(),b'user changed')

    def test_image_catalog_payload_and_novel_manifest_preserved(self):
        from translation_update_fixture import ROOT, build
        package=self.root/'translations/ko'
        old=json.loads((package/'manifest.json').read_bytes())
        source=ROOT/'translations/ko'
        catalog=json.loads((source/'images.json').read_bytes())
        # Use a real decodable PNG, including its exact content-addressed path.
        catalog['entries']=catalog['entries'][:1]
        row=catalog['entries'][0]
        (package/'images').mkdir()
        (package/row['file']).write_bytes((source/row['file']).read_bytes())
        (package/'images.json').write_text(json.dumps(catalog),encoding='utf8')
        build(package,added=['images.json',row['file']])
        updated=json.loads((package/'manifest.json').read_bytes())
        self.assertEqual(updated['names'],old['names'])
        self.assertEqual(updated['novels'],old['novels'])
        self.assertEqual(updated['images'],hashlib.sha256((package/'images.json').read_bytes()).hexdigest())
        archive=self.root/'images.zip';release.pack(self.root,archive)
        _,files=release.verify_archive(archive)
        self.assertEqual(files[release.PLUGIN+'translations/ko/'+row['file']],(source/row['file']).read_bytes())
        (package/row['file']).write_bytes(b'corrupt')
        with self.assertRaisesRegex(ValueError,'IMAGE_PAYLOAD'):release.verify_repo(self.root)

    def test_partial_install_failure_restores_preimages(self):
        original=Path.write_bytes
        def fail(target,raw):
            if target==self.game/release.PLUGIN/'GCMod.dll':raise OSError('injected disk failure')
            return original(target,raw)
        with patch.object(release,'game_closed'),patch.object(Path,'write_bytes',fail):
            with self.assertRaises(OSError):release.install(self.archive,self.game,self.backup)
        self.assertFalse((self.game/release.CFG).exists());self.assertFalse((self.game/release.PLUGIN/'GCMod.dll').exists())
        self.assertEqual(json.loads((self.backup/'install.json').read_bytes())['status'],'rolled_back')

    def test_archive_and_path_rejection_before_game_write(self):
        with self.assertRaisesRegex(ValueError,'PATH_ESCAPE'):release.safe_target(self.game,'../outside')
        with zipfile.ZipFile(self.archive,'a') as z:z.writestr('../escape',b'bad')
        with patch.object(release,'game_closed'):
            with self.assertRaisesRegex(ValueError,'ARCHIVE_MEMBERSHIP'):release.install(self.archive,self.game,self.backup)
        self.assertFalse(self.backup.exists())

    def test_stale_manifest_and_active_game_block(self):
        (self.root/'translations/ko/master.json').write_text('{}')
        with self.assertRaises(ValueError):release.verify_repo(self.root)
        with patch.object(release,'game_closed',side_effect=ValueError('CLOSE_GAME_FIRST')):
            with self.assertRaisesRegex(ValueError,'CLOSE_GAME_FIRST'):release.install(self.archive,self.game,self.backup)
        self.assertFalse(self.backup.exists())

    def test_failed_rollback_recovers_installed_state(self):
        with patch.object(release,'game_closed'):
            release.install(self.archive,self.game,self.backup)
        journal=json.loads((self.backup/'install.json').read_bytes())
        before={r['path']:(self.game/r['path']).read_bytes() for r in journal['files']}
        original=Path.unlink;calls=0
        def fail_once(target,*a,**kw):
            nonlocal calls
            calls+=1
            if calls==2:raise OSError('injected restore failure')
            return original(target,*a,**kw)
        with patch.object(release,'game_closed'),patch.object(Path,'unlink',fail_once):
            with self.assertRaises(OSError):release.rollback(self.game,self.backup)
        self.assertEqual(before,{p:(self.game/p).read_bytes() for p in before})
        self.assertEqual(json.loads((self.backup/'install.json').read_bytes())['status'],'installed')

if __name__=='__main__':unittest.main()
