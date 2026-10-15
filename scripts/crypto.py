"""Credential-free boundary to the operator's existing private source helper.

NG03_PRIVATE_CRYPTO_HELPER points to the existing upstream crypto.py in private
custody. No signing/decryption literals are copied into this product or reports.
The optional NG03_PRIVATE_CRYPTO_SHA256 pin detects private helper drift.
"""
import hashlib
import importlib.util
import os
from pathlib import Path

_private = None


def private_helper():
    global _private
    if _private is not None:
        return _private
    value = os.environ.get("NG03_PRIVATE_CRYPTO_HELPER")
    if not value:
        raise ValueError("SOURCE_PRIVATE_HELPER_REQUIRED")
    path = Path(value)
    if not path.is_file() or path.is_symlink() or path.resolve() == Path(__file__).resolve():
        raise ValueError("SOURCE_PRIVATE_HELPER_REQUIRED")
    raw = path.read_bytes()
    expected = os.environ.get("NG03_PRIVATE_CRYPTO_SHA256")
    if expected and hashlib.sha256(raw).hexdigest() != expected:
        raise ValueError("SOURCE_PRIVATE_HELPER_CHANGED")
    spec = importlib.util.spec_from_file_location("_ng03_private_source_crypto", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if not callable(getattr(module, "get_url_params", None)) or not callable(getattr(module, "decrypt_master_text", None)):
        raise ValueError("SOURCE_PRIVATE_HELPER_CONTRACT")
    module.ng03_helper_sha256 = hashlib.sha256(raw).hexdigest()
    _private = module
    return module


def get_url_params(path, vendor_token):
    return private_helper().get_url_params(path, vendor_token)


def decrypt_master_text(raw):
    return private_helper().decrypt_master_text(raw)
