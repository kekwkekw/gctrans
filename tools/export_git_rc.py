"""Export the committed safe operating paths exactly; no overlays/private history.
Output must be a NEW folder outside the game. The commit/tree are provenance,
not runtime conditions, and no commit SHA is embedded into translation manifests.
"""
from pathlib import Path, PurePosixPath
import argparse, hashlib, io, json, subprocess, tarfile

PREFIXES=('source/','tools/','tests/','contracts/','donor_recovery/','staging/','translations/ko/','translation/','candidate/','docs/')
ROOT_FILES={'archive/display/20261017/WRAP_SCOPE.json','.gitattributes','NuGet.Config',
 'README.md','.gitignore','.github/workflows/crawl_only.yml',
 '.github/workflows/update_translation.yml'}
SCRIPTS={'scripts/__init__.py','scripts/crawl_only.py','scripts/crypto.py'}
def run(repo,*args):return subprocess.check_output(['git','-C',str(repo),*args])
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--ref',default='HEAD');a=p.parse_args()
 repo=a.repo.resolve();out=a.output.absolute();assert not out.exists(),'NEW_EXPORT_REQUIRED'
 assert not any(p.name.casefold()=='bepinex' or (p/'GC.exe').is_file() for p in (out,*out.parents)), 'OUTSIDE_GAME_EXPORT_REQUIRED'
 commit=run(repo,'rev-parse',a.ref+'^{commit}').decode().strip();tree=run(repo,'rev-parse',commit+'^{tree}').decode().strip()
 paths=[x for x in run(repo,'ls-tree','-r','--name-only','-z',commit).decode().split('\0') if x]
 selected=[x for x in paths if x.startswith(PREFIXES) or x in ROOT_FILES or x in SCRIPTS]
 for x in selected:
  assert not any(k.lower() in {'audit','private_backup','bin','obj','__pycache__','.git'} for k in PurePosixPath(x).parts)
  assert Path(x).suffix.lower() not in {'.bundle','.raw','.assetbundle','.zip','.exe','.key','.pem','.pdb'}
  assert not x.endswith('.dll') or x=='candidate/GCMod.dll'
 out.mkdir(parents=True);members=[]
 # Git archive handles the recorded blobs; working-tree CRLF/untracked files cannot enter.
 # Directory pathspecs keep the Windows command below its length limit.
 # Membership is still checked against every prevalidated selected leaf below.
 selectors=[prefix.rstrip('/') for prefix in PREFIXES if any(x.startswith(prefix) for x in selected)]
 selectors+=sorted(x for x in selected if x in ROOT_FILES or x in SCRIPTS)
 raw=run(repo,'archive','--format=tar',commit,'--',*selectors)
 with tarfile.open(fileobj=io.BytesIO(raw),mode='r:') as archive:
  for m in archive.getmembers():
   assert not m.issym() and not m.islnk() and '..' not in PurePosixPath(m.name).parts
   if m.isdir():continue
   assert m.isfile() and m.name in selected,'UNEXPECTED_ARCHIVE_MEMBER'
   target=out/m.name;target.parent.mkdir(parents=True,exist_ok=True);payload=archive.extractfile(m).read();target.write_bytes(payload)
   members.append({'path':m.name,'size':len(payload),'sha256':hashlib.sha256(payload).hexdigest()})
 assert len(members)==len(selected),'EXPORT_MEMBERSHIP_MISMATCH'
 result={'schema':'ng11-exact-Git-operating-export-v1','commit':commit,'tree':tree,'files':members,
  'exported_files':len(members),'excluded_tracked_paths':len(paths)-len(selected),
  'rules':'Current operating paths and translation policy/knowledge only; historical legacy/WB/Knowledge retained in Git.',
  'publication':'NOT_PERFORMED','working_tree_bytes_used':False}
 (out/'GIT_EXPORT.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
 print(json.dumps({'status':'PASS','commit':commit,'tree':tree,'files':len(members),'export':str(out)},ensure_ascii=False))
if __name__=='__main__':main()
