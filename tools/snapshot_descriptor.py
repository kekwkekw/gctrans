"""Export a complete descriptor for an explicit cold scan or trusted inventory.

--trust-immutable declares that the caller has already validated and frozen the
current package. It is a trust precondition, not publisher authentication. Use
cold_scan for an external mutable directory or absent change history.
"""
from pathlib import Path
import argparse, json
from semantic_diff import descriptor
from build_manifests import encode

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('root',type=Path)
    p.add_argument('--mode',choices=['cold_scan','verified_immutable_inventory'],required=True)
    p.add_argument('--trust-immutable',action='store_true');p.add_argument('--report',type=Path,required=True)
    a=p.parse_args()
    if a.mode=='verified_immutable_inventory' and not a.trust_immutable:p.error('verified inventory requires explicit --trust-immutable; otherwise use cold_scan')
    result=descriptor(a.root,mode=a.mode);a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_bytes(encode(result))
    print(json.dumps({'files':len(result['files']),'trust_basis':result['trust_basis'],'payload_reads':len(result['payload_reads'])}))
