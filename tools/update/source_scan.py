"""Durable incremental acquisition. Source observation is not materialization.

Raw cache is private operator state, outside translations/ko. This module never
deletes or edits translations and does not infer platform equivalence.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import tempfile
import uuid
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

from .process_lock import ProcessFileLock

MAX_ASSET_BYTES = 16 * 1024 * 1024
MAX_INVENTORY_ENTRIES = 100000
NOVEL_PATTERN = re.compile(r"notinit/[^/]+/\w{3}_(\d{8}|\d{5,6})\.dmm$")
MASTER_ML_WORD_MAPPING_CONTRACT = "upstream-master-ml-word-zero-to-runtime-array-v1"
MASTER_TRANSLATION_FIELDS = {
    ("mUnitWords", "ml_word"): "ml_word[]",
    ("mSubunitWords", "ml_word"): "ml_word[]",
}
# Confirmed physical keys in the captured PC table bodies. All other tables
# require their actual id; there is no ordinal/hash-of-text identity fallback.
MASTER_RECORD_ID_FIELDS = {"mCurrencies": "item_type", "mIntermissionNovels": "series_no"}


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def atomic_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".ng03-owned-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(json_bytes(value))
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def safe_key(value: str) -> str:
    if not isinstance(value, str) or not value or "\\" in value or ":" in value:
        raise ValueError("SOURCE_ASSET_PATH")
    parts = value.split("/")
    if any(p in ("", ".", "..") or p != p.rstrip(" .") for p in parts):
        raise ValueError("SOURCE_ASSET_PATH")
    if any(re.match(r"(?i)^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)", p) for p in parts):
        raise ValueError("SOURCE_ASSET_PATH")
    return value


def check_private_root(path: Path) -> Path:
    path = Path(path).absolute()
    if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (path, *path.parents)):
        raise ValueError("SOURCE_REPARSE_PATH")
    if "translations" in [p.lower() for p in path.parts]:
        raise ValueError("SOURCE_STATE_IN_DEPLOYMENT_TREE")
    path.mkdir(parents=True, exist_ok=True)
    return path


@dataclass(frozen=True)
class SourceProfile:
    platform: str
    channel: str
    partition: str
    inventory_contract: str

    def __post_init__(self):
        for value in asdict(self).values():
            if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9_.:-]{1,96}", value):
                raise ValueError("SOURCE_PROFILE")
        if self.platform not in ("PC", "WebGL"):
            raise ValueError("SOURCE_PLATFORM")

    @property
    def key(self) -> str:
        return digest(json_bytes(asdict(self)))[:24]


def parse_source_ids(raw: str) -> list[str]:
    values = []
    for item in re.split(r"[\s,;]+", raw.strip()):
        if not item:
            continue
        if not item.isdigit():
            raise ValueError("SOURCE_ID")
        if item not in values:
            values.append(item)
    return values


def normalize_inventory(manifest: dict, partition: str = "Novel") -> dict:
    """Known d/n/h/s vendor contract; h is a token, never called raw SHA."""
    entries = manifest.get("d") if isinstance(manifest, dict) else None
    if not isinstance(entries, list) or len(entries) > MAX_INVENTORY_ENTRIES:
        raise ValueError("SOURCE_INVENTORY_SCHEMA")
    result, collisions = [], {}
    keys = set()
    for value in entries:
        if not isinstance(value, dict) or not isinstance(value.get("n"), str):
            raise ValueError("SOURCE_INVENTORY_SCHEMA")
        key = safe_key(value["n"])
        match = NOVEL_PATTERN.fullmatch(key)
        if partition == "Novel" and not match:
            continue
        if partition == "Master" and key != "master.dmm":
            continue
        token = value.get("h")
        if not isinstance(token, str) or not token or len(token) > 256:
            raise ValueError("SOURCE_VENDOR_TOKEN")
        size = value.get("s")
        if size is not None and (type(size) is not int or size < 0):
            raise ValueError("SOURCE_SIZE")
        source_id = match.group(1) if match else key
        if key in keys:
            collisions.setdefault(source_id, []).append(key)
        keys.add(key)
        result.append({"asset_key": key, "source_id": source_id, "vendor_token": token, "vendor_size": size})
    by_id = {}
    for item in result:
        by_id.setdefault(item["source_id"], []).append(item["asset_key"])
    for source_id, paths in by_id.items():
        if len(paths) > 1:
            collisions[source_id] = paths
    if partition == "Novel" and entries and not result:
        raise ValueError("SOURCE_INVENTORY_FILTER_NO_MATCHES")
    return {"items": result, "collisions": collisions, "sha256": digest(json_bytes(manifest))}


def _count_hash(metrics, raw):
    if metrics is not None:
        metrics["raw_hash_calls"] = metrics.get("raw_hash_calls", 0) + 1
        metrics["raw_hash_bytes"] = metrics.get("raw_hash_bytes", 0) + len(raw)
    return digest(raw)


def _cache_read(path, metrics):
    with path.open("rb") as stream:
        raw = stream.read(MAX_ASSET_BYTES + 1)
    if metrics is not None:
        metrics["raw_cache_reads"] = metrics.get("raw_cache_reads", 0) + 1
        metrics["raw_cache_read_bytes"] = metrics.get("raw_cache_read_bytes", 0) + len(raw)
    return raw


def immutable_blob(raw_root: Path, raw: bytes, kind: str, metrics=None) -> dict:
    if not isinstance(raw, bytes) or len(raw) > MAX_ASSET_BYTES or (kind == "bundles" and not raw):
        raise ValueError("SOURCE_RAW_SIZE")
    if kind not in ("bundles", "textassets", "decoded-master"):
        raise ValueError("SOURCE_RAW_KIND")
    sha = _count_hash(metrics, raw)
    relative = f"{kind}/{sha[:2]}/{sha}.bytes"
    path = raw_root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (path, *path.parents)):
        raise ValueError("SOURCE_REPARSE_PATH")
    with ProcessFileLock(raw_root / "blob-locks" / (sha + ".lock"), "SOURCE_RAW_WRITER_BUSY"):
        if path.exists():
            existing = _cache_read(path, metrics)
            if len(existing) == len(raw) and _count_hash(metrics, existing) == sha:
                return {"path": relative, "size": len(raw), "sha256": sha}
            # Rename this owned directory entry; old hardlinks keep their bytes.
            # Publish the new inode atomically, never overwrite linked bytes.
            quarantine = raw_root / "quarantine" / kind
            if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (quarantine, *quarantine.parents)):
                raise ValueError("SOURCE_REPARSE_PATH")
            quarantine.mkdir(parents=True, exist_ok=True)
            os.replace(path, quarantine / (sha + "." + uuid.uuid4().hex + ".bytes"))
            if metrics is not None:
                metrics["quarantined_blobs"] = metrics.get("quarantined_blobs", 0) + 1
        fd, owned = tempfile.mkstemp(prefix=".ng03-owned-", dir=path.parent)
        try:
            with os.fdopen(fd, "wb") as stream:
                stream.write(raw)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(owned, path)
        finally:
            Path(owned).unlink(missing_ok=True)
    return {"path": relative, "size": len(raw), "sha256": sha}


def verified_blob(raw_root: Path, ref: dict, metrics=None) -> bytes:
    path = raw_root / safe_key(ref["path"])
    if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction()) for p in (path, *path.parents)):
        raise ValueError("SOURCE_REPARSE_PATH")
    if not path.resolve().is_relative_to(raw_root.resolve()) or path.stat().st_size != ref["size"]:
        raise ValueError("SOURCE_RAW_IDENTITY")
    raw = _cache_read(path, metrics)
    if len(raw) != ref["size"] or _count_hash(metrics, raw) != ref["sha256"]:
        raise ValueError("SOURCE_RAW_IDENTITY")
    return raw


class UnityTextExtractor:
    """All TextAssets, lossless inverse of UnityPy's script bytes conversion.

    No decode/projection is used to select a script. A single TextAsset is unique;
    multiple TextAssets require an explicit exact name from an existing adapter.
    Master remains body-decode PARTIAL unless an established table adapter is supplied.
    """
    def __init__(self, selected_names: dict[str, str] | None = None):
        import UnityPy
        self.selected_names = selected_names or {}
        self.version = f"ng03-all-textasset-v1:UnityPy-{UnityPy.__version__}:" + digest(json_bytes(self.selected_names))

    def __call__(self, raw: bytes, asset: dict) -> dict:
        import UnityPy
        if not raw.startswith(b"UnityFS\0"):
            raise ValueError("SOURCE_NOT_UNITYFS")
        env = UnityPy.load(raw)
        if len(env.objects) > 10000:
            raise ValueError("SOURCE_OBJECT_LIMIT")
        values = []
        for obj in env.objects:
            if obj.type.name != "TextAsset":
                continue
            data = obj.read()
            body = data.m_Script
            if isinstance(body, str):
                body = body.encode("utf-8", "surrogateescape")
            elif isinstance(body, (bytes, bytearray, memoryview)):
                body = bytes(body)
            else:
                raise ValueError("SOURCE_TEXTASSET_BYTES")
            values.append({"name": data.m_Name, "path_id": obj.path_id,
                           "container": str(obj.assets_file.name), "bytes": body})
        if not values:
            raise ValueError("SOURCE_TEXTASSET_REQUIRED")
        names = [v["name"] for v in values]
        requested = self.selected_names.get(asset["asset_key"])
        candidates = [i for i, v in enumerate(values) if v["name"] == requested] if requested else list(range(len(values)))
        selected = candidates[0] if len(candidates) == 1 else None
        return {"textassets": values, "selected": selected, "master": [], "ui": [],
                "selection_contract": "EXACT_DECLARED_NAME" if requested else "UNIQUE_TEXTASSET_ONLY"}


class MasterTableExtractor(UnityTextExtractor):
    """Established upstream decode boundary, explicit table/field contracts.

    A finite table/runtime-field selector contains no dictionary JP or KO.
    Scalar, [] index zero and | first segment match MasterDataTranslator.
    Manifest-only observation never reaches this class. Unsupported records
    hold only their field; the lossless TextAssets/decoded bodies remain private.
    """
    def __init__(self, decode_master, decoder_sha256: str, tables=None):
        super().__init__()
        if not re.fullmatch(r"[0-9a-f]{64}", decoder_sha256):
            raise ValueError("MASTER_DECODER_IDENTITY")
        self.decode_master = decode_master
        self.strict_legacy = tables is None
        self.tables = tables or {"mUnitWords": ["ml_word"], "mSubunitWords": ["ml_word"]}
        for table, fields in self.tables.items():
            if not re.fullmatch(r"[A-Za-z0-9_]{1,96}", table) or not isinstance(fields, list) or not fields:
                raise ValueError("MASTER_FIELD_SELECTOR")
            if len(set(fields)) != len(fields) or any(not re.fullmatch(r"[A-Za-z0-9_]+(?:\[\]|\|)?", f) for f in fields):
                raise ValueError("MASTER_FIELD_SELECTOR")
        self.version += ":master-fields-v4:" + decoder_sha256 + ":" + digest(json_bytes({"tables": self.tables, "record_keys": MASTER_RECORD_ID_FIELDS}))

    def __call__(self, raw, asset):
        result = super().__call__(raw, asset)
        rows, decoded, coverage = [], [], []
        names = [x["name"] for x in result["textassets"]]
        for table, fields in sorted(self.tables.items()):
            identity_field = MASTER_RECORD_ID_FIELDS.get(table, "id")
            field_reports = [{"table": table, "runtime_field": f, "status": "TABLE_TEXTASSET_MISSING",
                              "rows": 0, "empty": 0, "missing": 0, "unsupported_type": 0,
                              "record_identity_rejected": 0, "record_identity_field": identity_field,
                              "actual_field_types": {}} for f in fields]
            coverage.extend(field_reports)
            if names.count(table) != 1:
                if names.count(table) > 1:
                    for report in field_reports:
                        report["status"] = "TABLE_TEXTASSET_COLLISION"
                continue
            body = next(x for x in result["textassets"] if x["name"] == table)
            try:
                source = self.decode_master(body["bytes"])
                records = [source] if isinstance(source, dict) else source
                if not isinstance(records, list) or len(records) > MAX_INVENTORY_ENTRIES:
                    raise ValueError("MASTER_TABLE_SCHEMA")
            except (ValueError, TypeError, RuntimeError):
                if self.strict_legacy:
                    raise
                for report in field_reports:
                    report["status"] = "TABLE_DECODE_UNSUPPORTED"
                continue
            decoded.append({"name": table, "bytes": json_bytes(source)})
            counts = {}
            for record in records:
                if isinstance(record, dict) and type(record.get(identity_field)) in (str, int) and str(record[identity_field]):
                    identity = str(record[identity_field])
                    counts[identity] = counts.get(identity, 0) + 1
            duplicates = {identity for identity, count in counts.items() if count > 1}
            for report in field_reports:
                report.update(status="FIELD_SOURCE_OBSERVED", records=len(records),
                              record_container="single-record" if isinstance(source, dict) else "record-array")
            for record in records:
                if (not isinstance(record, dict) or type(record.get(identity_field)) not in (str, int)
                        or not str(record.get(identity_field, "")) or str(record[identity_field]) in duplicates):
                    if self.strict_legacy and (not isinstance(record, dict) or type(record.get(identity_field)) not in (str, int)):
                        raise ValueError("MASTER_RECORD_ID")
                    for report in field_reports:
                        report["record_identity_rejected"] += 1
                    continue
                identity = str(record[identity_field])
                if identity_field != "id":
                    identity = identity_field + ":" + identity
                for declared, report in zip(fields, field_reports):
                    array = declared.endswith("[]") or self.strict_legacy
                    delimited = declared.endswith("|")
                    field = declared[:-2] if declared.endswith("[]") else declared[:-1] if delimited else declared
                    localized = record.get(field)
                    if field not in record:
                        report["missing"] += 1
                        continue
                    field_type = "array" if isinstance(localized, list) else "string" if isinstance(localized, str) else type(localized).__name__
                    report["actual_field_types"][field_type] = report["actual_field_types"].get(field_type, 0) + 1
                    selected = localized[0] if array and isinstance(localized, list) and localized else localized if not array else None
                    if not isinstance(selected, str):
                        if self.strict_legacy:
                            raise ValueError("MASTER_JP_FIELD_SCHEMA")
                        report["unsupported_type"] += 1
                        continue
                    jp, separator, suffix = selected.partition("|") if delimited else (selected, "", "")
                    if not jp:
                        report["empty"] += 1
                        continue
                    locator = {"table": table, "record": identity, "field": field}
                    context = {k: v for k, v in record.items() if k != field and
                               (self.strict_legacy or k == identity_field or k.endswith("_id") or k in ("role", "type", "category", "word_type"))}
                    row = {"locator": locator, "jp": jp, "context": context,
                           "structure": {"field_type": "ml-array" if array else "delimited-string" if delimited else "string",
                                         "physical_field": field + "[0]" if array else field,
                                         "record_identity_field": identity_field,
                                         "role": "Master_word" if self.strict_legacy else "Master_body", "textasset": table}}
                    if array:
                        row["structure"].update(jp_index=0, field_arity=len(localized), language_tail=localized[1:])
                    if delimited:
                        row["structure"].update(delimiter="|", suffix=separator + suffix)
                    translation_field = MASTER_TRANSLATION_FIELDS.get((table, field)) if self.strict_legacy else declared
                    if translation_field:
                        row["translation_target"] = {"table": table, "field": translation_field}
                        row["structure"]["mapping_contract"] = MASTER_ML_WORD_MAPPING_CONTRACT if self.strict_legacy else "MasterDataTranslator-field-grammar-v1"
                    rows.append(row)
                    report["rows"] += 1
            for report in field_reports:
                if report["missing"] or report["unsupported_type"] or report["record_identity_rejected"]:
                    report["status"] = "FIELD_SOURCE_PARTIAL" if report["rows"] else "FIELD_SOURCE_UNSUPPORTED"
        result.update(master=rows, decoded_bodies=decoded, master_field_coverage=coverage,
                      selection_contract="EXPLICIT_MASTER_TABLE_FIELD_CONTRACT")
        return result


def scan_inventory(profile: SourceProfile, inventory: dict | None, *, state_root: Path,
                   raw_root: Path, fetch_asset: Callable[[dict], bytes] | None = None,
                   extractor=None, existing_ids=(), forced_ids=(), complete=True,
                   max_candidates=200, cancelled=lambda: False, observation_error=None,
                   integrity_scan=False) -> dict:
    """Persist each success independently, then save observation and snapshot.

    bootstrap records old translated IDs as baseline_only. A failed or pending
    asset can never become baseline_only. A metadata-only scan leaves needed
    acquisition pending. max_candidates=0 retains upstream explicit unlimited semantics.
    """
    state_root, raw_root = check_private_root(state_root), check_private_root(raw_root)
    state_path = state_root / (profile.key + ".json")
    lock_path = state_root / (profile.key + ".writer.lock")
    with ProcessFileLock(lock_path):
        return _scan(profile, inventory, state_path, raw_root, fetch_asset, extractor,
                     set(existing_ids), list(forced_ids), complete, max_candidates, cancelled, observation_error, integrity_scan)


def _scan(profile, inventory, state_path, raw_root, fetch_asset, extractor, existing_ids,
          forced_ids, complete, max_candidates, cancelled, observation_error, integrity_scan):
    if type(max_candidates) is not int or max_candidates < 0:
        raise ValueError("SOURCE_CANDIDATE_LIMIT")
    snapshot_path = state_path.with_name(profile.key + ".snapshot.json")
    previous_snapshot = json.loads(snapshot_path.read_bytes()) if snapshot_path.exists() else None
    if previous_snapshot is not None:
        previous_id = previous_snapshot.get("snapshot_id")
        unsigned = {k: v for k, v in previous_snapshot.items() if k != "snapshot_id"}
        if previous_snapshot.get("profile") != asdict(profile) or digest(json_bytes(unsigned)) != previous_id:
            raise ValueError("SOURCE_PREVIOUS_SNAPSHOT_IDENTITY")
    state = json.loads(state_path.read_bytes()) if state_path.exists() else {
        "schema": "gc-ng03-source-state-v1", "profile": asdict(profile), "assets": {},
        "last_complete_inventory": None, "historical_before_bootstrap": "UNKNOWN"}
    if state.get("schema") != "gc-ng03-source-state-v1" or state.get("profile") != asdict(profile):
        raise ValueError("SOURCE_STATE_PROFILE")
    metrics = {"payload_network": 0, "payload_network_bytes": 0, "source_payloads_acquired": 0, "source_payload_bytes": 0,
               "raw_cache_reads": 0, "raw_cache_read_bytes": 0,
               "raw_hash_calls": 0, "raw_hash_bytes": 0, "quarantined_blobs": 0,
               "reused": 0, "metadata_only_reuse": 0, "integrity_verified_assets": 0,
               "reextracted": 0, "materialized": 0, "baseline_only": 0, "failed": 0, "collision": 0}
    if observation_error or inventory is None:
        state["observation"] = {"status": "UNKNOWN", "complete": False, "observed_at": now(),
                                "reason": observation_error or "METADATA_NOT_AVAILABLE"}
        atomic_json(state_path, state)
        return {"state": state, "metrics": metrics, "plan": [], "deletion_candidates": [],
                "sourceSnapshot": _snapshot(profile, state, metrics)}
    try:
        normalized = normalize_inventory(inventory, profile.partition)
    except ValueError as error:
        state["observation"] = {"status": "UNKNOWN", "complete": False, "observed_at": now(), "reason": str(error)}
        atomic_json(state_path, state)
        return {"state": state, "metrics": metrics, "plan": [], "deletion_candidates": [],
                "sourceSnapshot": _snapshot(profile, state, metrics),
                "lane_diff": {"schema": "gc-ng03-lane-diff-v1", "changes": [], "automatic_ko_delete": False,
                              "automatic_semantic_approval": False, "historical_baseline": "UNKNOWN_CURRENT_INVENTORY"}}
    items = normalized["items"]
    by_id = {i["source_id"]: i for i in items}
    missing = set(forced_ids) - set(by_id)
    if missing:
        raise ValueError("FORCED_SOURCE_IDS_MISSING:" + ",".join(sorted(missing)))
    version = getattr(extractor, "version", "EXTRACTOR_NOT_AVAILABLE")
    plan = []
    bootstrap = state["last_complete_inventory"] is None and not state["assets"]
    # Current ambiguity revokes every historic variant before cache reuse.
    # Raw custody survives; no old successful variant can retain current intake.
    for old in state["assets"].values():
        if old["source_id"] in normalized["collisions"]:
            old["source_collision"] = True
            old["status"] = "collision"
            if old.get("materialization"):
                mat = old["materialization"]
                old.setdefault("collision_variants", {})[mat["vendor_token"]] = mat
    for item in items:
        key = item["asset_key"]
        old = state["assets"].get(key)
        collision = item["source_id"] in normalized["collisions"]
        if old is None:
            old = {"source_id": item["source_id"], "status": "baseline_only" if bootstrap and item["source_id"] in existing_ids else "pending",
                   "observed_token": item["vendor_token"], "materialization": None}
            state["assets"][key] = old
        previous_token = old["observed_token"]
        old["observed_token"] = item["vendor_token"]
        collision_resolved = old.get("source_collision", False) and not collision
        old["source_collision"] = collision
        mat = old.get("materialization")
        reason = None
        if item["source_id"] in forced_ids:
            reason = "forced"
        elif collision_resolved:
            reason = "source_collision_resolved"
        elif old["status"] in ("failed", "pending") or (old["status"] == "collision" and not mat):
            reason = "failed_retry" if old["status"] == "failed" else "new_or_pending"
        elif previous_token != item["vendor_token"] or (mat and mat["vendor_token"] != item["vendor_token"]):
            reason = "source_token_changed"
        elif mat and extractor is not None and mat["extractor"] != version:
            reason = "extractor_changed"
        elif mat:
            if integrity_scan:
                try:
                    for ref in [mat["raw"], *mat["textassets"], *mat.get("decoded_bodies", [])]:
                        verified_blob(raw_root, ref, metrics)
                    metrics["integrity_verified_assets"] += 1
                    old["cache_validation"] = "RAW_VERIFIED_THIS_SCAN"
                except (OSError, ValueError):
                    _revoke_fetched(asset=old, reason="SOURCE_RAW_IDENTITY")
                    reason = "cache_invalid"
            else:
                old["cache_validation"] = "METADATA_ONLY_OWNED_PROVENANCE"
                metrics["metadata_only_reuse"] += 1
            if reason is None:
                metrics["reused"] += 1
        if reason:
            if old["status"] != "failed":
                old["status"] = "pending"
            plan.append({**item, "reason": reason, "source_collision": collision})
        elif old["status"] == "baseline_only":
            metrics["baseline_only"] += 1
        if collision and reason is None:
            metrics["collision"] += 1
        elif old["status"] == "collision" and reason is None:
            metrics["collision"] += 1
    if max_candidates and len(plan) > max_candidates:
        # No acquisition and no advanced metadata on an exceeded operator limit.
        raise ValueError(f"SOURCE_CANDIDATE_LIMIT:{len(plan)}>{max_candidates}")
    previous_complete = state["last_complete_inventory"]
    current_keys = sorted({i["asset_key"] for i in items})
    deletions = sorted(set(previous_complete["asset_keys"]) - set(current_keys)) if complete and previous_complete else []
    state["observation"] = {"status": "OBSERVED" if complete else "PARTIAL", "complete": bool(complete),
                            "sha256": normalized["sha256"], "observed_at": now(), "asset_keys": current_keys}
    if complete:
        state["last_complete_inventory"] = {"sha256": normalized["sha256"], "asset_keys": current_keys}
    state["deletion_candidates"] = deletions
    # Save pending obligations before I/O, including interruption after metadata advance.
    atomic_json(state_path, state)
    for item in plan:
        key, token = item["asset_key"], item["vendor_token"]
        asset = state["assets"][key]
        if cancelled():
            asset["status"] = "failed"
            asset["error_code"] = "SOURCE_CANCELLED"
            metrics["failed"] += 1
            atomic_json(state_path, state)
            continue
        try:
            fetched = asset.get("fetched_raw")
            if fetched and fetched["vendor_token"] == token and item["reason"] not in ("forced", "cache_invalid"):
                try:
                    raw = verified_blob(raw_root, fetched["raw"], metrics)
                    ref = fetched["raw"]
                    metrics["reextracted"] += int(item["reason"] == "extractor_changed")
                except (OSError, ValueError):
                    _revoke_fetched(asset, "SOURCE_RAW_IDENTITY")
                    fetched = None
                    atomic_json(state_path, state)
            else:
                fetched = None
            if fetched is None:
                if fetch_asset is None:
                    asset["error_code"] = "SOURCE_ACQUISITION_PENDING"
                    atomic_json(state_path, state)
                    continue
                raw = fetch_asset(item)
                metrics["payload_network"] += int(profile.platform == "WebGL")
                metrics["payload_network_bytes"] += len(raw) if profile.platform == "WebGL" else 0
                metrics["source_payloads_acquired"] += 1
                metrics["source_payload_bytes"] += len(raw)
                if cancelled():
                    raise ValueError("SOURCE_CANCELLED")
                ref = immutable_blob(raw_root, raw, "bundles", metrics)
                asset["fetched_raw"] = {"vendor_token": token, "raw": ref}
                # Raw is safely persisted before extraction/decoding.
                atomic_json(state_path, state)
            if extractor is None:
                raise ValueError("SOURCE_EXTRACTOR_REQUIRED")
            result = extractor(raw, item)
            all_refs = []
            for textasset in result["textassets"]:
                script_ref = immutable_blob(raw_root, textasset["bytes"], "textassets", metrics)
                all_refs.append({**script_ref, "name": textasset["name"], "path_id": textasset["path_id"], "container": textasset["container"]})
            if not all_refs:
                raise ValueError("SOURCE_TEXTASSET_REQUIRED")
            selected = result.get("selected")
            if selected is not None and (type(selected) is not int or not 0 <= selected < len(all_refs)):
                raise ValueError("SOURCE_SELECTION_IDENTITY")
            names = [r["name"] for r in all_refs]
            if (selected is not None and names.count(names[selected]) != 1) or item["source_collision"]:
                selected = None
            mat = {"vendor_token": token, "extractor": version, "raw": ref,
                   "textassets": all_refs, "selected_textasset": all_refs[selected] if selected is not None else None,
                   "selection_contract": result.get("selection_contract", "EXPLICIT_EXTRACTOR_SELECTION"),
                   "master": result.get("master", []), "ui": result.get("ui", []),
                   "decoded_bodies": [{"table": b["name"], **immutable_blob(raw_root, b["bytes"], "decoded-master", metrics)}
                                      for b in result.get("decoded_bodies", [])],
                   "master_field_coverage": result.get("master_field_coverage", []), "completed_at": now()}
            asset["materialization"] = mat
            asset.pop("raw_invalid", None)
            asset["cache_validation"] = "RAW_ACQUIRED_OR_VERIFIED_AND_EXTRACTED"
            if item["source_collision"]:
                asset.setdefault("collision_variants", {})[token] = mat
            asset["status"] = "materialized" if not item["source_collision"] and (selected is not None or profile.partition == "Master") else "collision"
            asset.pop("error_code", None)
            metrics[asset["status"] if asset["status"] == "collision" else "materialized"] += 1
        except (OSError, ValueError, KeyError, TypeError, RuntimeError) as error:
            # Never copy exception URLs, source text or credential values into state.
            code = str(error)
            asset["error_code"] = code if re.fullmatch(r"[A-Z][A-Z0-9_]{1,95}", code) else "SOURCE_ACQUISITION_OR_EXTRACTION_FAILED"
            asset["status"] = "failed"
            metrics["failed"] += 1
        atomic_json(state_path, state)
    snapshot = _snapshot(profile, state, metrics)
    from .diff_review import compare
    comparison = compare(previous_snapshot, snapshot) if previous_snapshot else {
        "schema": "gc-ng03-lane-diff-v1", "changes": [],
        "novel_bridge_refs": [r["bridge_ref"] for r in snapshot["novel"] if r["bridge_ref"]],
        "automatic_ko_delete": False, "automatic_semantic_approval": False}
    comparison["historical_baseline"] = "KNOWN_SAVED_SNAPSHOT" if previous_snapshot else "UNKNOWN_BASELINE_ONLY"
    atomic_json(snapshot_path, snapshot)
    return {"state": state, "metrics": metrics, "plan": plan, "deletion_candidates": deletions,
            "sourceSnapshot": snapshot, "lane_diff": comparison}


def _revoke_fetched(asset, reason):
    fetched = asset.pop("fetched_raw", None)
    if fetched:
        asset.setdefault("revoked_raw_refs", []).append({**fetched, "reason": reason, "revoked_at": now()})
    asset["cache_validation"] = "RAW_REF_REVOKED"
    asset["raw_invalid"] = True


def source_item_eligible(item):
    """Fail closed for ambiguity/token mismatch, including older snapshots."""
    return (item.get("status") == "MATERIALIZED" and item.get("current_eligible", True)
            and not item.get("source_collision", False)
            and item.get("raw") is not None and item.get("extractor") is not None
            and item.get("materialized_token", item.get("vendor_token")) == item.get("vendor_token"))


def _snapshot(profile, state, metrics):
    rows, master, ui, field_coverage = [], [], [], []
    current_keys = set(state.get("observation", {}).get("asset_keys", []))
    for key, value in sorted(state["assets"].items()):
        if key not in current_keys:
            continue
        previous_mat = value.get("materialization")
        mat = previous_mat if previous_mat and previous_mat["vendor_token"] == value["observed_token"] and not value.get("raw_invalid") else None
        eligible = value["status"] == "materialized" and not value.get("source_collision", False) and mat is not None
        fetched = value.get("fetched_raw")
        current_raw = mat["raw"] if mat else fetched["raw"] if fetched and fetched["vendor_token"] == value["observed_token"] else None
        row = {"source_id": value["source_id"], "asset_key": key, "vendor_token": value["observed_token"],
               "status": value["status"].upper(), "raw": current_raw,
               "textassets": mat["textassets"] if mat else [], "selected_textasset": mat["selected_textasset"] if eligible else None,
               "extractor": mat["extractor"] if mat else None,
               "materialized_token": mat["vendor_token"] if mat else None,
               "source_collision": value.get("source_collision", False), "current_eligible": eligible,
               "cache_validation": value.get("cache_validation", "PREVIOUS_OWNED_PROVENANCE"),
               "bridge_ref": {"file": "novel-intake.json", "source_id": value["source_id"]}
                             if profile.partition == "Novel" and eligible else None,
               "collision_variants": value.get("collision_variants", {}),
               "old_source_before_bootstrap": "UNKNOWN", "error_code": value.get("error_code")}
        rows.append(row)
        if eligible:
            master.extend(mat["master"])
            ui.extend(mat["ui"])
            field_coverage.extend(mat.get("master_field_coverage", []))
    observation = state.get("observation", {})
    snapshot = {"schema": "gc-ng03-source-snapshot-v1", "profile": asdict(profile),
                "metadata": observation, "master": master, "ui": ui, "master_field_coverage": field_coverage,
                "qualified_sources": {},
                "novel": rows if profile.partition == "Novel" else [],
                "asset_materializations": rows, "coverage": {
                    "metadata": observation.get("status", "UNKNOWN"),
                    "raw_materialized_assets": sum(r["raw"] is not None for r in rows),
                    "successful_assets": sum(r["status"] == "MATERIALIZED" for r in rows),
                    "baseline_only_assets": sum(r["status"] == "BASELINE_ONLY" for r in rows),
                    "pending_assets": sum(r["status"] == "PENDING" for r in rows),
                    "failed_assets": sum(r["status"] == "FAILED" for r in rows),
                    "collision_assets": sum(r["status"] == "COLLISION" for r in rows),
                    "master": "TABLE_BODY_EXTRACTED" if master else "MASTER_BODY_DECODER_REQUIRED",
                    "ui": "SUPPORTED_SOURCE_ROWS_EXTRACTED" if ui else "UI_SOURCE_ADAPTER_REQUIRED",
                    "historical_before_bootstrap": "UNKNOWN", "automatic_translation_deletes": 0},
                "metrics": metrics}
    snapshot["coverage"]["lane_status"] = "PARTIAL" if not observation.get("complete") or any(
        r["status"] != "MATERIALIZED" for r in rows) else "SELECTED_PROFILE_RAW_EXTRACTED"
    snapshot["snapshot_id"] = digest(json_bytes(snapshot))
    return snapshot


def main(arguments=None):
    """One scanner command and exit contract for direct/CLI operator use."""
    import sys
    sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
    from scripts.crawl_only import main as scanner_main
    return scanner_main(arguments)

if __name__ == "__main__":
    raise SystemExit(main())
