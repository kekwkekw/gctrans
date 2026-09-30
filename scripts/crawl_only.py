import hashlib
import json
import os
import re
import time
from datetime import datetime, timezone
from pathlib import Path

import httpx

from crypto import get_url_params
from parse import parse_bundle, parse_script


BASE_URL = "https://cdn-r18.gc.dmmgames.com"
ASSET_PATH = "/secure/data/production/webgl/resources/"
ASSETBUNDLE_MANIFEST = "/files/manifest/webgl/r18/assetbundle.json"
MASTER_MANIFEST = "/files/manifest/webgl/r18/master.json"
NOVEL_PATTERN = re.compile(r"notinit/[^/]+/\w{3}_(\d{8}|\d{5,6})\.dmm$")

ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / os.environ.get("CRAWL_OUTPUT_DIR", "crawl_out")


def write_json(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_json(data) -> str:
    payload = json.dumps(
        data,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=False,
    ).encode("utf-8")
    return sha256_bytes(payload)


def fetch_json(client: httpx.Client, url: str, attempts: int = 3):
    last_error = None
    for attempt in range(1, attempts + 1):
        try:
            response = client.get(url)
            response.raise_for_status()
            return response.json()
        except Exception as exc:
            last_error = exc
            if attempt == attempts:
                break
            time.sleep(attempt * 2)
    raise RuntimeError(f"Failed to fetch JSON after {attempts} attempts: {url}") from last_error


def fetch_bytes(
    client: httpx.Client,
    url: str,
    *,
    params: dict[str, str | int] | None = None,
    attempts: int = 3,
) -> bytes:
    last_error = None
    for attempt in range(1, attempts + 1):
        try:
            response = client.get(url, params=params)
            response.raise_for_status()
            return response.content
        except Exception as exc:
            last_error = exc
            if attempt == attempts:
                break
            time.sleep(attempt * 2)
    raise RuntimeError(f"Failed to fetch asset after {attempts} attempts: {url}") from last_error


def existing_novel_ids() -> tuple[set[str], set[str], set[str]]:
    legacy = set()
    legacy_root = ROOT / "novels"
    if legacy_root.exists():
        for path in legacy_root.glob("*/zh_Hans.json"):
            if path.parent.name.isdigit():
                legacy.add(path.parent.name)

    ko7 = set()
    ko7_root = ROOT / "translations" / "ko" / "novels"
    if ko7_root.exists():
        for path in ko7_root.glob("*.json"):
            if path.stem.isdigit():
                ko7.add(path.stem)

    return legacy, ko7, legacy | ko7


def parse_source_ids(raw: str) -> list[str]:
    if not raw.strip():
        return []
    ids = []
    seen = set()
    for item in re.split(r"[\\s,;]+", raw.strip()):
        if not item:
            continue
        if not item.isdigit():
            raise ValueError(f"Invalid source id: {item!r}")
        if item not in seen:
            seen.add(item)
            ids.append(item)
    return ids


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    forced_ids = parse_source_ids(os.environ.get("CRAWL_SOURCE_IDS", ""))
    max_candidates = int(os.environ.get("CRAWL_MAX_CANDIDATES", "200"))
    if max_candidates < 0:
        raise ValueError("CRAWL_MAX_CANDIDATES must be >= 0")

    legacy_ids, ko7_ids, existing_ids = existing_novel_ids()

    generated_at = datetime.now(timezone.utc).isoformat()
    run_context = {
        "repository": os.environ.get("GITHUB_REPOSITORY"),
        "repository_sha": os.environ.get("GITHUB_SHA"),
        "repository_ref": os.environ.get("GITHUB_REF"),
        "workflow": os.environ.get("GITHUB_WORKFLOW"),
        "run_id": os.environ.get("GITHUB_RUN_ID"),
        "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
        "actor": os.environ.get("GITHUB_ACTOR"),
    }

    with httpx.Client(
        timeout=httpx.Timeout(90.0, connect=30.0),
        follow_redirects=True,
        headers={"User-Agent": "gctrans-crawl-only/1.0"},
    ) as client:
        assetbundle = fetch_json(client, f"{BASE_URL}{ASSETBUNDLE_MANIFEST}")
        master_manifest = fetch_json(client, f"{BASE_URL}{MASTER_MANIFEST}")

        write_json(OUTPUT_DIR / "manifests" / "assetbundle.json", assetbundle)
        write_json(OUTPUT_DIR / "manifests" / "master.json", master_manifest)

        novel_assets: dict[str, dict] = {}
        duplicate_ids: dict[str, list[str]] = {}

        for asset in assetbundle.get("d", []):
            asset_name = asset.get("n", "")
            match = NOVEL_PATTERN.match(asset_name)
            if not match:
                continue

            novel_id = match.group(1)
            if novel_id in novel_assets:
                duplicate_ids.setdefault(novel_id, [novel_assets[novel_id]["n"]]).append(asset_name)
                continue
            novel_assets[novel_id] = asset

        if duplicate_ids:
            write_json(OUTPUT_DIR / "audit" / "duplicate_novel_ids.json", duplicate_ids)
            raise RuntimeError(
                "Duplicate novel IDs detected in official asset manifest; "
                "see crawl_out/audit/duplicate_novel_ids.json"
            )

        if assetbundle.get("d") and not novel_assets:
            write_json(
                OUTPUT_DIR / "audit" / "novel_pattern_no_matches.json",
                {
                    "assetbundle_entries": len(assetbundle.get("d", [])),
                    "novel_pattern": NOVEL_PATTERN.pattern,
                },
            )
            raise RuntimeError(
                "Official asset manifest was fetched but no novel assets matched the crawler pattern"
            )

        missing_from_manifest = [novel_id for novel_id in forced_ids if novel_id not in novel_assets]
        if missing_from_manifest:
            write_json(
                OUTPUT_DIR / "audit" / "forced_ids_missing_from_manifest.json",
                missing_from_manifest,
            )
            raise RuntimeError(
                "Requested source IDs were not found in the official asset manifest: "
                + ", ".join(missing_from_manifest)
            )

        if forced_ids:
            candidate_ids = forced_ids
            selection_mode = "forced_source_ids"
        else:
            candidate_ids = sorted(set(novel_assets) - existing_ids)
            selection_mode = "missing_from_repository"

        if max_candidates and len(candidate_ids) > max_candidates:
            write_json(
                OUTPUT_DIR / "audit" / "candidate_limit_exceeded.json",
                {
                    "candidate_count": len(candidate_ids),
                    "max_candidates": max_candidates,
                    "candidate_ids": candidate_ids,
                },
            )
            raise RuntimeError(
                f"Safety limit exceeded: {len(candidate_ids)} candidates > {max_candidates}. "
                "Increase max_candidates explicitly if this is expected."
            )

        items = []
        failed = []

        for index, novel_id in enumerate(candidate_ids, start=1):
            asset = novel_assets[novel_id]
            asset_name = asset["n"]
            asset_hash = asset["h"]
            asset_path = f"{ASSET_PATH}{asset_name}"
            params = get_url_params(asset_path, asset_hash)

            print(f"[{index}/{len(candidate_ids)}] Crawling {novel_id}: {asset_name}")

            try:
                bundle_bytes = fetch_bytes(
                    client,
                    f"{BASE_URL}{asset_path}",
                    params=params,
                )
                parsed = parse_bundle(bundle_bytes)
                if not parsed:
                    raise RuntimeError("No TextAsset found in bundle")

                script_name, script_text = parsed
                messages = parse_script(script_text)
                output_path = OUTPUT_DIR / "novels" / f"{novel_id}.json"
                write_json(output_path, messages)

                speakers = sorted(
                    {
                        str(message.get("name"))
                        for message in messages
                        if message.get("name")
                    }
                )
                title = None
                for message in messages:
                    if message.get("name") == "" and message.get("message"):
                        title = message["message"]
                        break

                items.append(
                    {
                        "novel_id": novel_id,
                        "selection_reason": (
                            "forced"
                            if forced_ids
                            else "missing_from_repository"
                        ),
                        "asset_name": asset_name,
                        "asset_manifest_hash": asset_hash,
                        "bundle_sha256": sha256_bytes(bundle_bytes),
                        "script_name": script_name,
                        "parsed_sha256": sha256_json(messages),
                        "message_count": len(messages),
                        "speaker_count": len(speakers),
                        "speakers": speakers,
                        "title": title,
                        "output_file": output_path.relative_to(OUTPUT_DIR).as_posix(),
                    }
                )
            except Exception as exc:
                failed.append(
                    {
                        "novel_id": novel_id,
                        "asset_name": asset_name,
                        "error": f"{type(exc).__name__}: {exc}",
                    }
                )

        manifest = {
            "schema": "gctrans-crawl-only-v1",
            "generated_at_utc": generated_at,
            "source": {
                "base_url": BASE_URL,
                "assetbundle_manifest": ASSETBUNDLE_MANIFEST,
                "master_manifest": MASTER_MANIFEST,
                "assetbundle_manifest_sha256": sha256_json(assetbundle),
                "master_manifest_sha256": sha256_json(master_manifest),
            },
            "run": run_context,
            "selection": {
                "mode": selection_mode,
                "forced_source_ids": forced_ids,
                "max_candidates": max_candidates,
            },
            "repository_inventory": {
                "legacy_6_1_novel_ids": len(legacy_ids),
                "gcmod7_ko_novel_ids": len(ko7_ids),
                "union_existing_novel_ids": len(existing_ids),
            },
            "official_inventory": {
                "assetbundle_entries": len(assetbundle.get("d", [])),
                "novel_assets": len(novel_assets),
            },
            "crawl_result": {
                "candidate_count": len(candidate_ids),
                "success_count": len(items),
                "failure_count": len(failed),
                "candidate_ids": candidate_ids,
                "successful_ids": [item["novel_id"] for item in items],
                "failed_ids": [item["novel_id"] for item in failed],
            },
            "items": items,
            "failures": failed,
        }
        write_json(OUTPUT_DIR / "CRAWL_MANIFEST.json", manifest)
        (OUTPUT_DIR / "NEW_IDS.txt").write_text(
            "\n".join(candidate_ids) + ("\n" if candidate_ids else ""),
            encoding="utf-8",
        )

        summary_lines = [
            "# gctrans crawl-only result",
            "",
            f"- Generated: {generated_at}",
            f"- Selection mode: `{selection_mode}`",
            f"- Official assetbundle entries: **{len(assetbundle.get('d', []))}**",
            f"- Official novel assets: **{len(novel_assets)}**",
            f"- Existing repository novel IDs: **{len(existing_ids)}**",
            f"- Crawl candidates: **{len(candidate_ids)}**",
            f"- Successful: **{len(items)}**",
            f"- Failed: **{len(failed)}**",
            "",
        ]

        if candidate_ids:
            summary_lines += [
                "## Candidate IDs",
                "",
                "`" + "`, `".join(candidate_ids) + "`",
                "",
            ]
        if failed:
            summary_lines += [
                "## Failures",
                "",
            ]
            for failure in failed:
                summary_lines.append(
                    f"- `{failure['novel_id']}`: {failure['error']}"
                )
            summary_lines.append("")

        summary = "\n".join(summary_lines)
        (OUTPUT_DIR / "SUMMARY.md").write_text(summary, encoding="utf-8")
        print(summary)

        if failed:
            raise RuntimeError(
                f"{len(failed)} of {len(candidate_ids)} candidate assets failed to crawl"
            )


if __name__ == "__main__":
    main()
