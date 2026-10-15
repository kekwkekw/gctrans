from pathlib import Path
import argparse,json
from contract_validation import validate_package

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--contracts',type=Path,default=Path(__file__).resolve().parents[1]/'contracts')
    a=p.parse_args();print(json.dumps({'status':'PASS',**validate_package(a.root,a.contracts)},ensure_ascii=False))
