"""Reuse the NG05 full disposable chain with normal next_package authoring.

Mutable exports are optional and unnecessary for the subsequent authoring input.
No candidate semantic changes or new runtime/native proof are created.
"""
from pathlib import Path
from ng05_chain import generate_chain
ROOT = Path(__file__).resolve().parents[1]
if __name__ == '__main__':
    report = generate_chain(ROOT/'tests/fixtures-ng06-chain', ROOT/'tests/NG06_CHAIN_FIXTURES.json', export=False)
    print('NG06 full chain PASS; unchanged Novel app read/hash 0; OS copies reported separately')
