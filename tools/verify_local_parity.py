"""One local/CI check command. Does not launch a game or publish anything."""
from pathlib import Path
import argparse
import json
import os
import subprocess
import sys
import tempfile
from local_release import verify_repo


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[1])
    p.add_argument('--output',type=Path)
    a=p.parse_args();repo=a.repo.resolve()
    scratch=repo/'work/local-parity';scratch.mkdir(parents=True,exist_ok=True)
    cases=['test_local_parity.py','test_local_release.py','test_ui_source.py',
           'test_export_local_ui.py','test_master_coverage.py','test_ng04_source.py']
    with tempfile.TemporaryDirectory(dir=scratch) as temp:
        env={**os.environ,'TEMP':temp,'TMP':temp,'PYTHONUTF8':'1'}
        for case in cases:
            subprocess.run([sys.executable,'-m','unittest','discover','-s','tests','-p',case],cwd=repo,env=env,check=True)
        subprocess.run(['dotnet','run','--project','tests/UiTextLookup','--','translations/ko/ui.json'],cwd=repo,env=env,check=True)
    report={'status':'PASS','test_suites':cases,'package':verify_repo(repo),'native':'NOT_RUN_BY_THIS_TOOL','publication':'NOT_PERFORMED'}
    if a.output:
        a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
