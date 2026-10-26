"""Current translation knowledge, scoped lookup, and reviewed small changes.

Translation reads only translation/knowledge.jsonl and runtime Names.
This module does not classify source content or define any HOLD conditions.
"""
from __future__ import annotations

from collections import Counter
from copy import deepcopy
import argparse
import hashlib
import json
from pathlib import Path
import re


KNOWLEDGE_KINDS = {
    "entity", "term", "profile", "profile_observation", "relationship", "speech",
    "claim_guard", "claim_development", "reference", "evidence_link",
    "preference_evidence", "editorial_preference", "conflict", "contextual_term",
    "narration", "display_alignment", "candidate_gap",
}
STATES = {"approved", "reviewing", "conflict"}
SURFACE_FIELDS = {
    "source_text", "surface_source", "canonical_source_name", "source_name",
    "source_name_observed", "full_source_name_observed", "address_source_forms",
    "jp", "jp_terms", "speakers", "speaker_jp",
}
ENTITY_FIELDS = {
    "entity_id", "entity_ref", "target_entity_id", "speaker_entity_id",
    "subject_entity_id", "object_entity_id", "target_ref", "profile_ref",
    "profile_id", "target", "target_id", "variant_of",
}


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def _json(raw):
    return json.loads(raw, object_pairs_hook=_pairs)


def record_sha256(record):
    raw = json.dumps(record, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def _strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for child in value:
            yield from _strings(child)


def _fields(value, keys):
    result = set()
    if isinstance(value, dict):
        for key, child in value.items():
            if key in keys:
                result.update(_strings(child))
            if isinstance(child, (dict, list)):
                result.update(_fields(child, keys))
    elif isinstance(value, list):
        for child in value:
            result.update(_fields(child, keys))
    return result






def _validate_record(record):
    if not isinstance(record, dict) or not isinstance(record.get("id"), str) or not record["id"]:
        raise ValueError("Knowledge record needs an id")
    if record.get("kind") not in KNOWLEDGE_KINDS or record.get("status") not in STATES:
        raise ValueError("Invalid knowledge kind/status: " + record["id"])
    if not isinstance(record.get("value"), dict) or "scope" not in record:
        raise ValueError("Knowledge record needs its actual value and scope: " + record["id"])
    if type(record.get("content_available")) is not bool or not isinstance(record.get("origin"), dict):
        raise ValueError("Knowledge record needs availability and provenance: " + record["id"])
    ids = record.get("source_ids")
    if not isinstance(ids, list) or any(not isinstance(x, str) or not re.fullmatch(r"[0-9]+", x) for x in ids) or len(ids) != len(set(ids)):
        raise ValueError("Invalid knowledge source ids: " + record["id"])


def _meaningful_scope(scope):
    if isinstance(scope, str):
        return bool(scope.strip())
    if isinstance(scope, list):
        return bool(scope) and all(_meaningful_scope(value) for value in scope)
    if isinstance(scope, dict):
        return bool(scope) and all(isinstance(key, str) and key.strip() and _meaningful_scope(value)
                                   for key, value in scope.items())
    return False


def _has_text(value):
    if isinstance(value, str):
        return bool(value.strip())
    if isinstance(value, dict):
        return any(_has_text(child) for child in value.values())
    if isinstance(value, list):
        return any(_has_text(child) for child in value)
    return False


def _has_actual_value(value):
    metadata = {"id", "status", "scope", "confidence", "origin", "provenance", "sources",
                "source_tiers", "source_ids", "context_groups", "evidence_ids", "evidence",
                "review_id", "content_available", "global_promoted"}
    return any(key not in metadata and not key.endswith(("_id", "_ids")) and
               (isinstance(child, (bool, int, float)) or _has_text(child))
               for key, child in value.items())


def load_knowledge(path):
    records, seen = [], set()
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        record = _json(line)
        _validate_record(record)
        if record["id"] in seen:
            raise ValueError("Duplicate knowledge id: " + record["id"])
        seen.add(record["id"])
        records.append(record)
    return records


def _validate_current_record(record):
    _validate_record(record)
    identity = record["id"]
    if record["status"] != "approved" or record["content_available"] is not True:
        raise ValueError("Current Knowledge needs approved, available content: " + identity)
    if not _has_actual_value(record["value"]):
        raise ValueError("Current Knowledge needs actual content: " + identity)
    scope = record["scope"]
    scoped = (isinstance(scope, dict) and set(scope) == {"source_ids"}
              and isinstance(scope["source_ids"], list) and bool(scope["source_ids"])
              and all(isinstance(s, str) and re.fullmatch(r"[0-9]+", s) for s in scope["source_ids"])
              and len(scope["source_ids"]) == len(set(scope["source_ids"])))
    if scope != "global" and not scoped:
        raise ValueError("Current Knowledge needs an explicit current scope: " + identity)
    review = record["origin"].get("review_id")
    if not isinstance(review, str) or not review.strip():
        raise ValueError("Current Knowledge needs review provenance: " + identity)


def validate_current(records):
    for record in records:
        _validate_current_record(record)
    return records


def load_current(repo_root):
    return validate_current(load_knowledge(Path(repo_root) / "translation/knowledge.jsonl"))


def dump_knowledge(records):
    seen = set()
    for record in records:
        _validate_record(record)
        if record["id"] in seen:
            raise ValueError("Duplicate knowledge id: " + record["id"])
        seen.add(record["id"])
    return ("".join(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n"
                    for record in sorted(records, key=lambda record: record["id"]))).encode("utf-8")




def project(records, *, speakers=(), source_ids=(), jp_texts=(), max_chars=40000):
    """Select relevant data without normalizing JP or imposing a source row cap.

    The size limit is a soft prompt budget. Omitted ids remain visible and can be
    selected from the current file; it never blocks or splits a translation batch.
    Historical context membership is not a current occurrence identity mapping.
    """
    if not isinstance(max_chars, int) or max_chars < 1:
        raise ValueError("max_chars must be positive")
    speakers, source_ids, texts = set(speakers), set(source_ids), list(jp_texts)
    exact_surfaces = set(speakers)

    def mentioned(surface):
        return bool(surface) and (surface in exact_surfaces or any(surface in text for text in texts))

    entities = set()
    for record in records:
        value = record["value"]
        if record["kind"] in {"entity", "profile", "reference"} and any(mentioned(s) for s in _fields(value, SURFACE_FIELDS)):
            entities.update(_fields(value, ENTITY_FIELDS))
            entities.add(record["id"])
    ranked, gaps = [], []
    for record in records:
        surfaces = _fields(record["value"], SURFACE_FIELDS)
        scope = record["scope"]
        # source_ids records observed evidence membership. Only the explicit,
        # simple declared scope below identifies applicability to a new batch.
        declared = (isinstance(scope, dict) and set(scope) == {"source_ids"}
                    and isinstance(scope["source_ids"], list)
                    and all(isinstance(identity, str) for identity in scope["source_ids"])
                    and bool(source_ids.intersection(scope["source_ids"])))
        context = declared or bool(source_ids.intersection(record["source_ids"]))
        surface = any(mentioned(s) for s in surfaces)
        speaker = bool(speakers.intersection(surfaces))
        entity_refs = _fields(record["value"], ENTITY_FIELDS)
        entity = bool(entities.intersection(entity_refs))
        if record["kind"] in {"relationship", "speech"}:
            # A frequent protagonist must not pull every relationship in the
            # corpus into a new scene. Cross-scene references need all named
            # participants; an actual source/literal match still qualifies.
            entity = bool(entity_refs) and entity_refs.issubset(entities)
            if record["kind"] == "speech":
                # Address forms name the recipient, not the speaker whose
                # speech/register was observed in the historical scene.
                observed_speaker = record["value"].get("speaker_entity_id")
                surface = surface and observed_speaker in entities
                speaker = False
        elif record["kind"] not in {"entity", "profile", "profile_observation", "reference", "display_alignment", "candidate_gap"}:
            entity = False
        if not (context or surface or entity):
            continue
        if not record["content_available"]:
            gaps.append(record["id"])
            continue
        core = record["kind"] in {"entity", "profile", "term", "contextual_term", "editorial_preference"}
        score = 200 * context + 60 * speaker + 40 * surface + 20 * entity + 30 * core
        ranked.append((-score, record["id"], record, declared))
    selected, omitted, authority, used = [], [], {}, 0
    for _, identity, record, declared in sorted(ranked):
        size = len(json.dumps(record, ensure_ascii=False, separators=(",", ":")))
        if used + size > max_chars:
            omitted.append(identity)
            continue
        selected.append(deepcopy(record))
        used += size
        # Original approved data remains approved. Scope is displayed beside it;
        # historical row acceptance is never synthesized into a new global rule.
        global_scope = isinstance(record["scope"], str) and record["scope"].lower() == "global"
        current_review = isinstance(record["origin"].get("review_id"), str) and bool(record["origin"]["review_id"])
        authority[identity] = "approved_within_declared_scope" if record["status"] == "approved" and current_review and (declared or global_scope) else "reference"
    return {
        "records": selected,
        "effective_authority": authority,
        "omitted_ids": omitted,
        "missing_value_ids": sorted(gaps),
        "matched_records": len(ranked),
        "selected_records": len(selected),
        "characters": used,
        "max_chars": max_chars,
    }


def apply_reviewed_changes(records, names, changes, *, review_id=None):
    """Return new values only after validating the complete reviewed changeset.

    approved is a recorded reviewer decision, not identity authentication. The
    caller binds this list to the actual PRO review and stages the returned data.
    """
    if not isinstance(changes, list):
        raise ValueError("Reviewed changes must be a list")
    current = {record["id"]: deepcopy(record) for record in records}
    if len(current) != len(records):
        raise ValueError("Duplicate current knowledge ids")
    updated_names = deepcopy(names)
    seen = set()
    for change in changes:
        if not isinstance(change, dict) or type(change.get("approved")) is not bool:
            raise ValueError("Every proposed change needs an explicit review decision")
        if not change["approved"]:
            continue
        if not isinstance(review_id, str) or not review_id:
            raise ValueError("Approved changes need their review id")
        kind = change.get("kind")
        key = change.get("jp") if kind == "name" else change.get("id")
        if not isinstance(key, str) or not key or (kind, key) in seen:
            raise ValueError("Invalid or duplicate reviewed change target")
        seen.add((kind, key))
        if kind == "name":
            if "old_ko" not in change or updated_names.get(key) != change["old_ko"]:
                raise ValueError("Names preimage changed: " + key)
            if not isinstance(change.get("ko"), str) or not change["ko"]:
                raise ValueError("Name needs a nonempty Korean display value")
            updated_names[key] = change["ko"]
        elif kind == "knowledge":
            old = current.get(key)
            old_hash = record_sha256(old) if old is not None else None
            if "old_sha256" not in change or change["old_sha256"] != old_hash:
                raise ValueError("Knowledge preimage changed: " + key)
            value = deepcopy(change.get("value"))
            _validate_current_record(value)
            if value["id"] != key or not value["content_available"] or not _meaningful_scope(value["scope"]) or not _has_actual_value(value["value"]):
                raise ValueError("Reviewed knowledge needs actual content and explicit scope")
            if value["origin"].get("review_id") != review_id:
                raise ValueError("Knowledge provenance must identify this PRO review")
            if not any(key != "review_id" and _has_text(item) for key, item in value["origin"].items()):
                raise ValueError("Knowledge provenance needs source/evidence beyond the review id")
            # Store exactly the reviewed record. Its review linkage belongs to
            # the enclosing batch, avoiding hidden edits to the approved value.
            current[key] = value
        else:
            raise ValueError("Unsupported reviewed change kind")
    return sorted(current.values(), key=lambda record: record["id"]), updated_names




def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    path = args.repo / "translation/knowledge.jsonl"
    records = load_current(args.repo)
    print(json.dumps({"path": str(path), "records": len(records),
        "available_values": sum(record["content_available"] for record in records),
        "kinds": dict(sorted(Counter(record["kind"] for record in records).items())),
        "states": dict(sorted(Counter(record["status"] for record in records).items())),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
