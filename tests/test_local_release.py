import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile
import hashlib
from local_parity_fixture import fixture
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
        old=b'[General]\nFrameRate = 144\n[Translation.MasterData]\nEnabledTables = mItems\n'
        cfg.write_bytes(old)
        with patch.object(release,'game_closed'):
            self.assertGreater(release.install(self.archive,self.game,self.backup)['changed_files'],0)
            installed=release.cfg_read(cfg.read_bytes());self.assertEqual(installed['General']['FrameRate'],'144')
            self.assertEqual(installed['Translation.MasterData']['EnabledTables'],'*')
            release.rollback(self.game,self.backup)
        self.assertEqual(cfg.read_bytes(),old);self.assertFalse((self.game/release.PLUGIN/'GCMod.dll').exists())

    def test_changed_install_refuses_rollback(self):
        with patch.object(release,'game_closed'):
            release.install(self.archive,self.game,self.backup)
            dll=self.game/release.PLUGIN/'GCMod.dll';dll.write_bytes(b'user changed')
            with self.assertRaisesRegex(ValueError,'INSTALL_CHANGED'):release.rollback(self.game,self.backup)
            self.assertEqual(dll.read_bytes(),b'user changed')

    def test_image_catalog_payload_and_novel_manifest_preserved(self):
        from local_parity_fixture import ROOT, build
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
