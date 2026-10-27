"""Sync mNovels exact keys from accepted per-ID Novel dictionaries; never edit Novel files."""
import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def translation_hash(value):
    def flatten(obj, prefix=""):
        for key in sorted(obj):
            item = obj[key]
            if isinstance(item, dict):
                yield from flatten(item, prefix + key + "\1")
            else:
                yield prefix + key + "\0" + item + "\0"
    return hashlib.md5("".join(flatten(value)).encode("utf-8")).hexdigest()


def sync(package, raw_path):
    master = read(package / "master.json")
    rows = read(raw_path)
    if not isinstance(rows, list):
        raise ValueError("Expected mNovels source rows")
    proposals = defaultdict(lambda: defaultdict(list))
    holds, matched, aliases = [], set(), []
    novel_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in sorted((package / "novels").glob("*.json"))}
    for row in rows:
        # novel_script_id is the actual script identity; do not infer from row order.
        script = str(row.get("novel_script_id", ""))
        novel_id = script.rsplit("_", 1)[-1]
        if not novel_id.isdigit():
            holds.append({"id": row.get("id"), "reason": "invalid_script_id"})
            continue
        file = package / "novels" / (novel_id + ".json")
        if not file.exists():
            holds.append({"id": novel_id, "reason": "accepted_novel_missing"})
            continue
        novel = read(file)
        keys = {row.get("title", "")}
        ml = row.get("ml_title")
        if isinstance(ml, list) and ml:
            keys.add(ml[0])  # local translates the JP slot, not every locale slot.
        for jp in sorted(keys - {""}):
            ko = novel.get(jp)
            if ko is None:
                # Master uses ASCII episode digits where Novel uses full-width digits.
                # Authoring-only, unique match within the same Novel; never runtime fuzzing.
                width = str.maketrans("０１２３４５６７８９　", "0123456789 ")
                candidates = [k for k in novel if k.translate(width) == jp.translate(width)]
                if len(candidates) == 1:
                    ko = novel[candidates[0]]
                    aliases.append({"id": novel_id, "master_jp": jp, "novel_jp": candidates[0]})
            if not isinstance(ko, str) or not ko or ko == jp:
                holds.append({"id": novel_id, "jp": jp, "reason": "exact_title_missing_in_novel"})
                continue
            proposals[jp][ko].append(novel_id)
            matched.add(novel_id)
    changes, synced = [], []
    for jp, values in sorted(proposals.items()):
        if len(values) != 1:
            holds.append({"jp": jp, "reason": "same_jp_conflicting_accepted_titles", "values": dict(values)})
            continue
        ko = next(iter(values))
        for field in ("title", "ml_title[]"):
            table = master.setdefault("mNovels", {}).setdefault(field, {})
            if table.get(jp) != ko:
                changes.append({"field": field, "jp": jp, "before": table.get(jp), "after": ko})
                table[jp] = ko
        synced.append({"jp": jp, "ko": ko, "ids": values[ko]})
    if changes:
        write(package / "master.json", master)
    manifest = read(package / "manifest.json")
    manifest["master"] = translation_hash(master)
    manifest["hash"] = translation_hash({k: v for k, v in manifest.items() if k != "hash"})
    if manifest != read(package / "manifest.json"):
        write(package / "manifest.json", manifest)
    assert all(hashlib.sha256((package / "novels" / name).read_bytes()).hexdigest() == digest
               for name, digest in novel_hashes.items()), "Novel bytes changed"
    return {"source_sha256": hashlib.sha256(raw_path.read_bytes()).hexdigest(),
            "source_rows": len(rows), "novels_preserved": len(novel_hashes),
            "matched_novel_ids": len(matched), "synced_unique_jp": len(synced),
            "synced_field_keys": len(synced) * 2, "changed_field_keys": len(changes),
            "changes": changes, "holds": holds, "synced": synced, "width_aliases": aliases}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()
    result = sync(args.package, args.source)
    write(args.report, result)
    print(json.dumps({k: v for k, v in result.items() if k not in ("changes", "holds", "synced", "width_aliases")}, ensure_ascii=False))
    print("HOLD records:", len(result["holds"]))
