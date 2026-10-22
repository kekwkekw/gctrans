"""Novel workspace: prepare -> Codex draft -> PRO review -> local checkout apply.

This module does not run a model, commit Git changes, or select a game package.
Content decisions are supplied by the reviewer using HOLD_POLICY.md verbatim.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import uuid

from build_manifests import build, encode
from contract_validation import read_json
from update.process_lock import ProcessFileLock

POLICY_SHA256 = "3aca17a2f12c76ca9c17d939a8f68e769b1eeefe54c8b52f6912d5f73f77922e"
SCHEMA = "gc-novel-batch-v1"
FIELDS = {"title": 1, "message": 2, "msgvoicesync": 3}
ROW_FIELDS = ("row_id", "source_id", "message_index_0based", "physical_line_1based",
              "command", "speaker", "jp", "raw_field_bytes")
META_FILES = {"manifest.json", "legacy-components.json", "ui/manifest.json", "release-manifest.json"}


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def file_hash(path):
    return digest(path.read_bytes()) if path.is_file() else None


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encode(value))


def source_id(value):
    require(isinstance(value, str) and re.fullmatch(r"[0-9]{1,16}", value), "Invalid Novel source id")
    return value


def parse_script(raw, identity):
    """Read exact comma-position fields; only BOM and physical EOL are framing.

    JP fields are never trimmed, unquoted, normalized or converted between <br>
    and literal backslash-n. Raw byte spans make the framing removal explicit.
    No historical raw_index is invented.
    """
    identity = source_id(identity)
    require(isinstance(raw, bytes), "Script must be bytes")
    bom = 3 if raw.startswith(b"\xef\xbb\xbf") else 0
    require(not raw[bom:].startswith(b"\xef\xbb\xbf"), "Repeated leading UTF-8 BOM")
    raw[bom:].decode("utf-8", errors="strict")
    rows, cursor = [], bom
    for number, framed in enumerate(raw[bom:].splitlines(keepends=True), 1):
        line = framed[:-2] if framed.endswith(b"\r\n") else framed[:-1] if framed.endswith((b"\n", b"\r")) else framed
        fields = line.split(b",")
        command = fields[0].decode("utf-8")
        if command in FIELDS:
            offset = FIELDS[command]
            require(len(fields) > offset, f"Malformed {command} at physical line {number}")
            jp = fields[offset].decode("utf-8")
            require(jp != "" and "\x00" not in jp, f"Empty or NUL JP at physical line {number}")
            speaker = "" if command == "title" else fields[offset - 1].decode("utf-8")
            start = cursor + sum(len(field) + 1 for field in fields[:offset])
            rows.append({"row_id": f"{identity}:{len(rows)}", "source_id": identity,
                         "message_index_0based": len(rows), "physical_line_1based": number,
                         "command": command, "speaker": speaker, "jp": jp,
                         "raw_field_bytes": [start, start + len(fields[offset])]})
        cursor += len(framed)
    require(rows, f"No supported Novel rows in {identity}")
    return rows


def _policy(repo):
    raw = (repo / "translation/HOLD_POLICY.md").read_bytes()
    require(digest(raw) == POLICY_SHA256, "Frozen HOLD_POLICY.md bytes changed")
    return raw


def _jsonl(path):
    if not path.exists():
        return []
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line]


def _jsonl_bytes(rows):
    return b"".join((json.dumps(row, ensure_ascii=False, sort_keys=True, allow_nan=False) + "\n").encode("utf-8") for row in rows)


def _collect(input_spec):
    """Read selected original files again at every boundary, never a saved proof."""
    selected = input_spec["source_ids"]
    require(selected and len(selected) == len(set(selected)), "Select unique source ids")
    for item in selected:
        source_id(item)
    if input_spec["kind"] == "saved_textassets":
        path = Path(input_spec["path"])
        document = read_json(path)
        require(document.get("schema") == "gc-novel-sources-v1" and isinstance(document.get("sources"), list), "Invalid saved source manifest")
        sources = document["sources"]
        output = []
        for identity in selected:
            matches = [item for item in sources if item.get("source_id") == identity]
            require(len(matches) == 1, f"Missing or ambiguous source {identity}")
            item = matches[0]
            original = Path(item["path"])
            original = original if original.is_absolute() else path.parent / original
            original = original.resolve()
            raw = original.read_bytes()
            if "sha256" in item:
                require(digest(raw) == item["sha256"], f"Saved source hash changed: {identity}")
            rows = parse_script(raw, identity)
            title = item.get("title", next((r["jp"] for r in rows if r["command"] == "title"), identity))
            require(isinstance(title, str) and title, f"Missing scenario title: {identity}")
            output.append(({"source_id": identity, "title": title,
                            "sha256": digest(raw), "size": len(raw), "original_path": str(original),
                            "asset_key": item.get("asset_key"), "extractor": item.get("extractor", "saved-textasset"),
                            "textasset_name": item.get("textasset_name"), "rows": rows}, raw))
        return output
    require(input_spec["kind"] == "source_snapshot", "Unsupported source input")
    from update.diff_review import source_snapshot_input
    from update.source_scan import source_item_eligible, verified_blob
    snapshot = source_snapshot_input(read_json(Path(input_spec["path"])))
    require(snapshot.get("metadata", {}).get("status") != "UNKNOWN", "Current source observation is unavailable")
    output = []
    for identity in selected:
        matches = [item for item in snapshot.get("novel", []) if item.get("source_id") == identity]
        require(len(matches) == 1, f"Missing or ambiguous source {identity}")
        item = matches[0]
        require(source_item_eligible(item) and isinstance(item.get("selected_textasset"), dict), f"Source bytes are not current: {identity}")
        asset = item["selected_textasset"]
        raw_root = Path(input_spec["raw_root"])
        verified_blob(raw_root, item["raw"])
        raw = verified_blob(raw_root, asset)
        rows = parse_script(raw, identity)
        title = next((r["jp"] for r in rows if r["command"] == "title"), identity)
        output.append(({"source_id": identity, "title": title, "sha256": digest(raw), "size": len(raw),
                        "asset_key": item["asset_key"], "vendor_token": item["vendor_token"],
                        "extractor": item["extractor"], "textasset": asset,
                        "profile": snapshot["profile"], "rows": rows}, raw))
    return output


def prepare_batch(repo, batch, *, source_manifest=None, source_snapshot=None, raw_root=None,
                  source_ids=(), register=(), state_root=None, reason="source_update", knowledge_max_chars=40000):
    """Create a local draft workspace; no translation or semantic verdict occurs."""
    from translation_knowledge import load_current, project
    repo, batch = Path(repo).resolve(), Path(batch).resolve()
    require((source_manifest is None) != (source_snapshot is None), "Choose one source input")
    require(not batch.exists(), "Batch folder already exists")
    require(not any(batch.is_relative_to(repo / p) for p in ("translations", "translation", ".git")), "Batch must be outside production files")
    policy = _policy(repo)
    if source_manifest is not None:
        path = Path(source_manifest).resolve()
        if not source_ids:
            source_ids = [row["source_id"] for row in read_json(path)["sources"]]
        spec = {"kind": "saved_textassets", "path": str(path), "source_ids": list(source_ids)}
        require(state_root is None, "State completion is available only for scanner sources")
    else:
        require(raw_root is not None, "Scanner sources require raw-root")
        spec = {"kind": "source_snapshot", "path": str(Path(source_snapshot).resolve()),
                "raw_root": str(Path(raw_root).resolve()), "source_ids": list(source_ids),
                "state_root": str(Path(state_root).resolve()) if state_root else None}
    captured = _collect(spec)
    require(set(register) <= set(spec["source_ids"]), "Register only explicitly selected Novel ids")
    require(reason in {"source_update", "legacy_retranslation", "translation_correction"}, "Invalid work reason")
    require(type(knowledge_max_chars) is int and knowledge_max_chars > 0, "Knowledge context size must be positive")
    package = repo / "translations/ko"
    manifest = read_json(package / "manifest.json")
    names = read_json(package / "names.json")
    rows = [row for info, _ in captured for row in info["rows"]]
    speakers = sorted({row["speaker"] for row in rows if row["speaker"]})
    context = {"names": {name: names.get(name) for name in speakers},
               "knowledge": project(load_current(repo), speakers=speakers,
                                    source_ids=spec["source_ids"], jp_texts=[row["jp"] for row in rows], max_chars=knowledge_max_chars)}
    head = subprocess.run(["git", "-C", str(repo), "rev-parse", "HEAD"], capture_output=True, text=True)
    document = {"schema": SCHEMA, "reason": reason, "base_commit": head.stdout.strip() if head.returncode == 0 else None,
                "policy_sha256": POLICY_SHA256, "source_input": spec,
                "sources": [info for info, _ in captured], "register": sorted(set(register)),
                "target_preimages": {identity: file_hash(package / f"novels/{identity}.json") for identity in spec["source_ids"]},
                "previous_registration": {identity: identity in manifest["novels"] for identity in spec["source_ids"]},
                "context_sha256": digest(encode(context))}
    document_hash = digest(encode(document))
    draft = {"schema": "gc-novel-draft-v1", "batch_sha256": document_hash,
             "rows": [dict(row, hold=None, reason="", ko="") for row in rows], "changes": []}
    batch.mkdir(parents=True)
    try:
        for info, raw in captured:
            path = batch / "source" / (info["source_id"] + ".txt")
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(raw)
        (batch / "HOLD_POLICY.md").write_bytes(policy)
        write_json(batch / "context.json", context)
        write_json(batch / "batch.json", document)
        write_json(batch / "draft.json", draft)
        (batch / "TASK.md").write_text(
            "# Novel 번역 작업\n\n"
            "1. `HOLD_POLICY.md` 원본을 읽고 그 파일만 HOLD 정책으로 사용합니다.\n"
            "2. `source/` 전체 장면과 `draft.json`의 JP/speaker를 읽습니다. JP와 행 식별 필드는 바꾸지 않습니다.\n"
            "3. `context.json`의 적용 범위와 상태를 확인합니다. 현재 JP가 우선이며 Names는 exact speaker 표시값입니다.\n"
            "4. Codex는 각 행의 `hold`를 true/false로 판정합니다. HOLD 행은 `reason`을 적고 `ko`를 비워 둬도 됩니다. "
            "나머지 행의 `ko`를 번역하고 태그·변수·줄바꿈을 보존합니다. 지식 변경은 `changes`에 제안합니다.\n"
            "5. `novel review`가 구조를 검사하고 HOLD 표시문을 넣은 `review.json`을 생성합니다. "
            "ChatGPT PRO는 이 파일과 원문·context·정책을 함께 검수합니다.\n"
            "6. PRO는 최종 KO와 HOLD 판정을 검수하고, 반영할 지식 변경만 승인합니다. "
            "검수가 끝나면 `review_id`를 기입하고 `pro_review_complete`를 true로 설정합니다.\n"
            "7. `novel apply`는 검수본을 검사해 현재 checkout에 반영합니다. Git commit/push와 게임 실행은 수행하지 않습니다.\n\n"
            "이 작업 폴더는 초벌과 검수용입니다. 모델 실행이나 실제 PRO 검수 완료를 코드가 자동 주장하지 않습니다.\n\n"
            "## 선택적인 Names/Knowledge 변경안\n\n"
            "`changes`에 넣는 형식 예시입니다. 실제 대상의 JP·KO·ID·source_ids·근거로 바꿉니다. "
            "초벌은 `approved:false`로 제안하고, PRO가 검수한 항목만 true로 바꿉니다. "
            "새 항목의 preimage는 null, 기존 Names는 실제 `old_ko`, 기존 Knowledge는 "
            "`translation_knowledge.record_sha256(기존 레코드)` 값을 `old_sha256`에 넣습니다.\n\n"
            '```json\n{"kind":"name","jp":"新しい話者","old_ko":null,"ko":"새 화자","approved":false}\n```\n\n'
            '```json\n{"kind":"knowledge","id":"term:example","old_sha256":null,"approved":false,'
            '"value":{"id":"term:example","kind":"term","status":"approved",'
            '"scope":{"source_ids":["99999999"]},"source_ids":["99999999"],"content_available":true,'
            '"value":{"jp":"原語","ko":"대응 표기"},'
            '"origin":{"review_id":"BATCH-REVIEW-ID","source":"source/99999999.txt의 해당 행"}}}\n```\n\n'
            "Knowledge의 `origin.review_id`는 최종 `review.json`의 `review_id`와 같아야 합니다. "
            "실제 규칙 내용과 적용 범위, 별도의 원문 근거를 함께 기록합니다.\n",
            encoding="utf-8")
    except BaseException:
        shutil.rmtree(batch)
        raise
    projection = context["knowledge"]
    return {"status": "DRAFT_WORKSPACE_READY", "batch": str(batch), "sources": len(captured), "rows": len(rows),
            "policy_sha256": POLICY_SHA256, "semantic_review": "NOT_RUN",
            "knowledge_projection": {"selected_records": len(projection["records"]), "matched_records": projection.get("matched_records"),
                                     "characters": projection.get("characters"), "omitted_ids": projection.get("omitted_ids", []),
                                     "missing_value_ids": projection.get("missing_value_ids", [])}}


def _load_batch(repo, batch):
    policy = _policy(repo)
    require((batch / "HOLD_POLICY.md").read_bytes() == policy, "Batch HOLD_POLICY.md differs from frozen file")
    document = read_json(batch / "batch.json")
    require(document.get("schema") == SCHEMA and document.get("policy_sha256") == POLICY_SHA256, "Invalid batch or policy binding")
    require(file_hash(batch / "context.json") == document["context_sha256"], "Batch Knowledge context changed")
    captured = _collect(document["source_input"])
    require([info for info, _ in captured] == document["sources"], "Selected source changed since draft preparation")
    for info, raw in captured:
        require((batch / "source" / (info["source_id"] + ".txt")).read_bytes() == raw, "Batch source copy changed")
    return document


def _structure(value):
    tokens, plain, index = [], [], 0
    start_pattern = re.compile(r"</?[A-Za-z][A-Za-z0-9_:-]*(?=[\s=/>]|$)")
    break_pattern = re.compile(r"\r\n|\r|\n|\\n")
    while index < len(value):
        if start_pattern.match(value, index):
            end, quote = index + 1, None
            while end < len(value):
                char = value[end]
                if quote:
                    if char == quote:
                        quote = None
                elif char in ("'", '"'):
                    quote = char
                elif char in "<>":
                    break
                end += 1
            require(end < len(value) and value[end] == ">" and quote is None, "Malformed markup")
            tokens.append(("tag", value[index:end + 1]))
            index = end + 1
        else:
            line_break = break_pattern.match(value, index)
            if line_break:
                tokens.append(("break", line_break.group()))
                plain.append(line_break.group())
                index = line_break.end()
            else:
                plain.append(value[index])
                index += 1
    visible = "".join(plain)
    return (tokens,
            re.findall(r"\$\{[^{}\r\n]+\}|\{[A-Za-z_0-9][^{}\r\n]*\}|%(?:[0-9]+\$)?[sdif]|\$[A-Za-z_][A-Za-z_0-9]*|■+", visible))


def hold_placeholder(title, row):
    return f"[{title} - {row['message_index_0based'] + 1}행 HOLD]"


def _checked_rows(document, supplied, *, draft=False):
    expected = [row for info in document["sources"] for row in info["rows"]]
    require(isinstance(supplied, list) and len(supplied) == len(expected), "Row coverage differs from source")
    titles = {info["source_id"]: info["title"] for info in document["sources"]}
    output = []
    for original, row in zip(expected, supplied):
        require(isinstance(row, dict) and set(row) == set(ROW_FIELDS) | {"hold", "reason", "ko"}, "Invalid draft/review row fields")
        require({key: row[key] for key in ROW_FIELDS} == original, "JP/speaker/row identity changed")
        require(type(row["hold"]) is bool and isinstance(row["reason"], str), f"Row decision missing: {row['row_id']}")
        require(isinstance(row["ko"], str) and "\x00" not in row["ko"], "Invalid KO string")
        checked = copy.deepcopy(row)
        if row["hold"]:
            require(row["reason"].strip(), f"HOLD reason missing: {row['row_id']}")
            placeholder = hold_placeholder(titles[row["source_id"]], row)
            require(row["ko"] == placeholder or (draft and row["ko"] == ""), f"HOLD display mismatch: {row['row_id']}")
            checked["ko"] = placeholder
        else:
            require(row["ko"].strip(), f"Translation missing: {row['row_id']}")
            require(_structure(row["jp"]) == _structure(row["ko"]), f"Markup/variable/break mismatch: {row['row_id']}")
        output.append(checked)
    return output


def prepare_review(repo, batch):
    repo, batch = Path(repo).resolve(), Path(batch).resolve()
    document = _load_batch(repo, batch)
    draft = read_json(batch / "draft.json")
    require(draft.get("schema") == "gc-novel-draft-v1" and draft.get("batch_sha256") == file_hash(batch / "batch.json"), "Draft does not match batch")
    rows = _checked_rows(document, draft["rows"], draft=True)
    require(isinstance(draft.get("changes"), list), "Knowledge proposals must be a list")
    path = batch / "review.json"
    require(not path.exists(), "Review file already exists; preserve completed review")
    review = {"schema": "gc-novel-pro-review-v1", "batch_sha256": file_hash(batch / "batch.json"),
              "draft_sha256": file_hash(batch / "draft.json"), "policy_sha256": POLICY_SHA256,
              "review_id": "", "pro_review_complete": False, "rows": rows,
              "changes": copy.deepcopy(draft["changes"]), "notes": ""}
    write_json(path, review)
    return {"status": "PRO_REVIEW_READY", "review": str(path), "rows": len(rows), "hold_rows": sum(r["hold"] for r in rows), "semantic_review": "NOT_RUN"}


def _checked_changes(records, names, changes, review_id):
    """Already-applied exact proposals are idempotent; all others keep preimages."""
    from translation_knowledge import apply_reviewed_changes, record_sha256
    current = {record["id"]: record for record in records}
    require(isinstance(changes, list), "Knowledge proposals must be a list")
    pending, seen = [], set()
    for change in changes:
        require(isinstance(change, dict), "Invalid Knowledge proposal")
        if change.get("approved") is not True:
            continue
        key = (change.get("kind"), change.get("jp") if change.get("kind") == "name" else change.get("id"))
        require(key not in seen, "Duplicate approved Knowledge proposal")
        seen.add(key)
        require(change.get("kind") in {"name", "knowledge"}, "Unsupported Knowledge proposal")
        before_key = "old_ko" if change["kind"] == "name" else "old_sha256"
        require(before_key in change, "Knowledge proposal preimage is missing")
        existing = names.get(change.get("jp")) if change["kind"] == "name" else current.get(change.get("id"))
        proposed = change.get("ko") if change["kind"] == "name" else change.get("value")
        validated = copy.deepcopy(change)
        if existing is not None and existing == proposed:
            validated[before_key] = existing if change["kind"] == "name" else record_sha256(existing)
        pending.append(validated)
    return apply_reviewed_changes(records, names, pending, review_id=review_id)


def _context_current(repo, batch, records, names, changes):
    context = read_json(batch / "context.json")
    replacement_names = {change.get("jp"): change.get("ko") for change in changes if change.get("kind") == "name" and change.get("approved") is True}
    replacement_records = {change.get("id"): change.get("value") for change in changes if change.get("kind") == "knowledge" and change.get("approved") is True}
    for jp, old in context["names"].items():
        require(names.get(jp) == old or (jp in replacement_names and names.get(jp) == replacement_names[jp]), f"Used Names entry changed: {jp}")
    current = {record["id"]: record for record in records}
    for record in context["knowledge"]["records"]:
        identity = record["id"]
        require(current.get(identity) == record or (identity in replacement_records and current.get(identity) == replacement_records[identity]), f"Used Knowledge entry changed: {identity}")


def _reviewed(repo, batch, review_path):
    from translation_knowledge import load_current
    document = _load_batch(repo, batch)
    review = read_json(review_path)
    require(review.get("schema") == "gc-novel-pro-review-v1", "Invalid PRO review document")
    require(review.get("batch_sha256") == file_hash(batch / "batch.json") and review.get("draft_sha256") == file_hash(batch / "draft.json"), "Review no longer matches batch/draft")
    require(review.get("policy_sha256") == POLICY_SHA256, "Review policy binding changed")
    require(review.get("pro_review_complete") is True and isinstance(review.get("review_id"), str) and review["review_id"].strip(), "PRO final review is not complete")
    rows = _checked_rows(document, review["rows"])
    package = repo / "translations/ko"
    records = load_current(repo)
    names = read_json(package / "names.json")
    changes = review.get("changes", [])
    require(isinstance(changes, list) and all(isinstance(item, dict) for item in changes), "Invalid Knowledge proposals")
    _context_current(repo, batch, records, names, changes)
    next_records, next_names = _checked_changes(records, names, changes, review["review_id"])
    payloads = {info["source_id"]: {} for info in document["sources"]}
    for row in rows:
        mapping = payloads[row["source_id"]]
        require(row["jp"] not in mapping or mapping[row["jp"]] == row["ko"], f"Same exact JP key has conflicting KO: {row['row_id']}")
        mapping[row["jp"]] = row["ko"]
    manifest = read_json(package / "manifest.json")
    for identity, payload in payloads.items():
        current_hash = file_hash(package / f"novels/{identity}.json")
        require(current_hash in (document["target_preimages"][identity], digest(encode(payload))), f"Target translation changed: {identity}")
        current_registration = identity in manifest["novels"]
        desired_registration = document["previous_registration"][identity] or identity in document["register"]
        require(current_registration in (document["previous_registration"][identity], desired_registration), f"Target registration changed: {identity}")
    return document, review, rows, payloads, next_records, next_names


def check_review(repo, batch, review):
    repo, batch, review = Path(repo).resolve(), Path(batch).resolve(), Path(review).resolve()
    document, reviewed, rows, payloads, _, _ = _reviewed(repo, batch, review)
    return {"status": "READY_TO_APPLY", "review_id": reviewed["review_id"], "sources": len(payloads), "rows": len(rows),
            "hold_rows": sum(row["hold"] for row in rows), "unique_jp_keys": sum(len(value) for value in payloads.values()),
            "register": document["register"], "policy_sha256": POLICY_SHA256,
            "review_basis": "Explicit supplied PRO final review; no identity authentication", "game": "NOT_RUN"}


def _publish(updates, previous):
    """Replace only owned outputs and roll back ordinary write failures."""
    temporary, committed = {}, []
    operation = uuid.uuid4().hex
    try:
        for path, raw in updates.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            temp = path.with_name(f".novel-{operation}-{path.name}")
            with temp.open("xb") as stream:
                # Register as soon as exclusive creation succeeds, before any
                # write/flush/fsync can fail. Never remove an existing file.
                temporary[path] = temp
                stream.write(raw)
                stream.flush()
                os.fsync(stream.fileno())
        for path in updates:
            require((path.read_bytes() if path.exists() else None) == previous[path], f"Concurrent file update: {path.name}")
        for path in updates:
            require((path.read_bytes() if path.exists() else None) == previous[path], f"Concurrent file update: {path.name}")
            os.replace(temporary[path], path)
            committed.append(path)
    except BaseException:
        for path in reversed(committed):
            # Preserve a non-cooperating writer's change rather than clobber it.
            if path.read_bytes() != updates[path]:
                continue
            if previous[path] is None:
                path.unlink()
            else:
                recovery = path.with_name(f".novel-{operation}-rollback-{path.name}")
                recovery.write_bytes(previous[path])
                os.replace(recovery, path)
        raise
    finally:
        for path in temporary.values():
            path.unlink(missing_ok=True)


def apply_review(repo, batch, review):
    """Apply explicitly PRO-reviewed data to a local checkout, without Git writes."""
    from translation_knowledge import dump_knowledge
    from verify_staged_release import verify
    repo, batch, review = Path(repo).resolve(), Path(batch).resolve(), Path(review).resolve()
    with ProcessFileLock(repo / "work/novels/.apply.lock", "Novel apply is already running"):
        document, reviewed, rows, payloads, records, names = _reviewed(repo, batch, review)
        package = repo / "translations/ko"
        package_members = {path.relative_to(package).as_posix() for path in package.rglob("*") if path.is_file()}
        guarded = {package / relative for relative in META_FILES | {"names.json"}}
        guarded.update(package / f"novels/{identity}.json" for identity in payloads)
        guarded.update(repo / relative for relative in ("translation/knowledge.jsonl", "translation/holds.jsonl"))
        owned_paths = {part for path in guarded for part in (path, *path.parents) if part.is_relative_to(repo)}
        require(not any(path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction()) for path in owned_paths),
                "Output contains a filesystem link")
        preimages = {path: path.read_bytes() if path.exists() else None for path in guarded}
        require(not any(path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction())
                        for path in package.rglob("*")), "Package contains a filesystem link")
        with tempfile.TemporaryDirectory(prefix=".novel-stage-", dir=batch) as staging:
            stage = Path(staging) / "ko"
            shutil.copytree(package, stage)
            added, modified = [], []
            for identity, payload in payloads.items():
                relative = f"novels/{identity}.json"
                raw = encode(payload)
                if not (stage / relative).exists():
                    added.append(relative)
                elif (stage / relative).read_bytes() != raw:
                    modified.append(relative)
                (stage / relative).parent.mkdir(exist_ok=True)
                (stage / relative).write_bytes(raw)
            names_raw = encode(names)
            if read_json(stage / "names.json") != names:
                (stage / "names.json").write_bytes(names_raw)
                modified.append("names.json")
            build(stage, added=added, modified=modified, register=document["register"])
            verification = verify(stage)
            allowed = {f"novels/{identity}.json" for identity in payloads} | META_FILES | {"names.json"}
            updates = {}
            for path in stage.rglob("*"):
                if not path.is_file():
                    continue
                relative = path.relative_to(stage).as_posix()
                target, raw = package / relative, path.read_bytes()
                if not target.exists() or target.read_bytes() != raw:
                    require(relative in allowed, f"Unexpected package change: {relative}")
                    updates[target] = raw
            knowledge_path = repo / "translation/knowledge.jsonl"
            current_records_raw = dump_knowledge(records)
            if knowledge_path.read_bytes() != current_records_raw:
                updates[knowledge_path] = current_records_raw
            holds_path = repo / "translation/holds.jsonl"
            holds = [item for item in _jsonl(holds_path) if item.get("source_id") not in payloads]
            source_hashes = {item["source_id"]: item["sha256"] for item in document["sources"]}
            holds.extend({"source_id": row["source_id"], "row_id": row["row_id"],
                          "message_index_0based": row["message_index_0based"], "physical_line_1based": row["physical_line_1based"],
                          "source_sha256": source_hashes[row["source_id"]], "jp_sha256": digest(row["jp"].encode("utf-8")),
                          "placeholder": row["ko"], "reason": row["reason"], "policy_sha256": POLICY_SHA256,
                          "review_id": reviewed["review_id"]} for row in rows if row["hold"])
            holds_raw = _jsonl_bytes(sorted(holds, key=lambda item: (item["source_id"], item["row_id"])))
            if (holds_path.read_bytes() if holds_path.exists() else b"") != holds_raw:
                updates[holds_path] = holds_raw
            previous = {path: preimages[path] for path in updates}
            # Re-read current source, targets and context after staging before publishing.
            refreshed = _reviewed(repo, batch, review)
            require(refreshed == (document, reviewed, rows, payloads, records, names),
                    "Review or inputs changed while staging; retry apply")
            require(all((path.read_bytes() if path.exists() else None) == old for path, old in preimages.items()),
                    "Package or Knowledge changed while staging; retry apply")
            require({path.relative_to(package).as_posix() for path in package.rglob("*") if path.is_file()} == package_members,
                    "Package membership changed while staging; retry apply")
            _publish(updates, previous)
        result = {"status": "APPLIED_LOCAL", "review_id": reviewed["review_id"], "sources": len(payloads),
                  "rows": len(rows), "hold_rows": sum(row["hold"] for row in rows), "policy_sha256": POLICY_SHA256,
                  "changed_files": [{"path": path.relative_to(repo).as_posix(), "sha256": digest(raw), "bytes": len(raw)} for path, raw in sorted(updates.items())],
                  "package_check": verification["status"], "git_commit": "NOT_RUN", "game": "NOT_RUN"}
        spec = document["source_input"]
        if spec.get("state_root"):
            from update.diff_review import source_snapshot_input
            from update.intake_work import complete_novel_batch
            try:
                snapshot = source_snapshot_input(read_json(Path(spec["path"])))
                reviewed_sources = [{"source_id": item["source_id"], "asset_key": item["asset_key"],
                                     "textasset_sha256": item["sha256"], "extractor": item["extractor"]} for item in document["sources"]]
                result["intake_completion"] = complete_novel_batch(Path(spec["state_root"]), snapshot, reviewed_sources,
                    review_id=reviewed["review_id"], payloads={f"novels/{identity}.json": digest(encode(value)) for identity, value in payloads.items()})
            except (ValueError, OSError, KeyError) as error:
                result["status"] = "APPLIED_LOCAL_COMPLETION_PENDING"
                result["intake_completion"] = {"error": str(error), "retry": "Repeat the same apply; current reviewed payloads are checked again"}
        try:
            write_json(batch / "result.json", result)
        except OSError as error:
            # The canonical writes have already succeeded. A diagnostic report
            # failure must not misreport the operation as an unapplied ERROR.
            result["report_write_error"] = str(error)
        return result


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_subparsers(dest="command", required=True)
    prepare = actions.add_parser("prepare")
    for name in ("prepare", "review", "check", "apply"):
        action = prepare if name == "prepare" else actions.add_parser(name)
        action.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
        action.add_argument("--batch", type=Path, required=True)
        if name in {"check", "apply"}:
            action.add_argument("--review", type=Path)
    source = prepare.add_mutually_exclusive_group(required=True)
    source.add_argument("--source-manifest", type=Path)
    source.add_argument("--source-snapshot", type=Path)
    prepare.add_argument("--raw-root", type=Path)
    prepare.add_argument("--state-root", type=Path)
    prepare.add_argument("--source-id", action="append", default=[])
    prepare.add_argument("--register", action="append", default=[])
    prepare.add_argument("--reason", choices=("source_update", "legacy_retranslation", "translation_correction"), default="source_update")
    prepare.add_argument("--knowledge-max-chars", type=int, default=40000)
    args = parser.parse_args(arguments)
    try:
        if args.command == "prepare":
            result = prepare_batch(args.repo, args.batch, source_manifest=args.source_manifest,
                                   source_snapshot=args.source_snapshot, raw_root=args.raw_root, source_ids=args.source_id,
                                   register=args.register, state_root=args.state_root, reason=args.reason, knowledge_max_chars=args.knowledge_max_chars)
        elif args.command == "review":
            result = prepare_review(args.repo, args.batch)
        else:
            review_path = args.review or args.batch / "review.json"
            result = (check_review if args.command == "check" else apply_review)(args.repo, args.batch, review_path)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 2 if result["status"] == "APPLIED_LOCAL_COMPLETION_PENDING" else 0
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(json.dumps({"status": "ERROR", "reason": str(error)}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
