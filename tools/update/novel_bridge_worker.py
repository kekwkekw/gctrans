"""Offline small bridge using the existing WB20/M4 functions unchanged.

Return selected runtime files only; NG03's apply transaction builds the complete
current package. Acceptance proof remains an in-process object from this call.
"""
import argparse
import contextlib
import io
import json
import pathlib
import sys


def main(args):
    sys.path.insert(0, str(args.workbench_root))
    from wb.checker_core import need, sha, strict, GateError
    from wb.local_release_cli import plan_metadata, pinned
    from wb.local_acceptance import validate_acceptance
    from wb.local_candidate import _selected, _baseline, encoded
    from wb.source_pilot_cli import run as wb20_run
    from wb.safety import network_denied
    with network_denied():
        try:
            raw = args.plan.read_bytes()
            need(sha(raw) == args.plan_sha256 and len(raw) <= 1024 * 1024, "M4_PLAN_PIN")
            plan, jobs, paths, roots = plan_metadata(raw)
            out = args.output.resolve()
            need(not out.exists(), "M4_OUTPUT_ALREADY_EXISTS")
            for root in (*roots, args.workbench_root, args.current_package):
                need(not out.is_relative_to(pathlib.Path(root).resolve()) and not pathlib.Path(root).resolve().is_relative_to(out),
                     "M4_OUTPUT_OVERLAPS_INPUT")
            checks, scenes, results = {}, {}, []
            for sid, job in jobs:
                job.output_dir = out / "replay" / sid
                captured = io.StringIO()
                with contextlib.redirect_stdout(captured):
                    code = wb20_run(job)
                result = strict(captured.getvalue().encode("utf-8"))
                need(code == 0 and result["status"] == "RETURN_MECHANICAL_PASS_AUTHORITY_PENDING", "M4_WB20_REPLAY_REJECTED")
                checks[sid] = pathlib.Path(result["artifacts"]["source-return-check"]).read_bytes()
                scenes[sid] = pinned({"path": str(job.bound_scene), "sha256": job.bound_scene_sha256})
                results.append({"script_id": sid, "status": result["status"], "return_check_sha256": sha(checks[sid])})
            def record(value):
                return {"ledger_raw": pinned(value["ledger"]), "event_raw": pinned(value["event"])}
            acceptance = plan["acceptance"]
            proof = validate_acceptance(current_checks=checks, scenes=scenes,
                baseline_records=[record(r) for r in acceptance["baseline_records"]],
                overlay_record=record(acceptance["overlay_record"]) if acceptance["overlay_record"] is not None else None,
                trusted_event_sha256=acceptance["trusted_event_sha256"])
            selected, references = _selected(proof)
            # Same original M4 baseline and runtime key/registration contracts.
            original, objects = _baseline(pathlib.Path(plan["baseline"]["root"]), strict(pinned(plan["baseline"]["identity"])))
            manifest = objects["translations/ko/manifest.json"]
            payloads = {}
            for sid, mapping in selected.items():
                relative = "novels/" + sid + ".json"
                baseline_relative = "translations/ko/" + relative
                need(sid in manifest["novels"] and baseline_relative in original, "LOCAL_CANDIDATE_TARGET_NOT_REGISTERED")
                need(set(mapping) == set(objects[baseline_relative]), "LOCAL_CANDIDATE_RUNTIME_KEYSET_CHANGED")
                current = strict((args.current_package / relative).read_bytes())
                need(set(mapping) == set(current), "LOCAL_CANDIDATE_RUNTIME_KEYSET_CHANGED")
                payloads[relative] = encoded(mapping)
            out.mkdir(parents=True, exist_ok=True)
            for relative, body in payloads.items():
                target = out / "runtime-return" / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open("xb") as stream:
                    stream.write(body)
            report = {"status": "EXISTING_WB20_M4_ACCEPTED_RUNTIME_RETURN_VERIFIED",
                      "current_input_replays": len(results), "accepted_occurrences": len(references),
                      "payloads": {name: {"size": len(body), "sha256": sha(body)} for name, body in payloads.items()},
                      "proof_digest": proof["proof_digest"], "existing_baseline_verified": True,
                      "full_candidate_copy": "DEFERRED_TO_NG03_APPLY_TRANSACTION", "automatic_registration": False,
                      "new_semantic_approval": False, "public_release_authorized": False,
                      "executions": results}
            (out / "COMPLETE_RETURN.json").write_bytes(encoded(report))
            print(json.dumps(report))
            return 0
        except (GateError, OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            print(json.dumps({"status": "REJECTED", "code": str(error) if isinstance(error, GateError) else "INVALID_INPUT_OR_IO"}))
            return 2


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--workbench-root", type=pathlib.Path, required=True)
    p.add_argument("--current-package", type=pathlib.Path, required=True)
    p.add_argument("--plan", type=pathlib.Path, required=True)
    p.add_argument("--plan-sha256", required=True)
    p.add_argument("--output", type=pathlib.Path, required=True)
    raise SystemExit(main(p.parse_args()))
