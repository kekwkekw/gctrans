"""Source updates, Novel draft/review/apply, and existing UI/Master operations."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from contract_validation import read_json
from build_manifests import encode
from update.apply_review import apply_review, freeze_input
from update.diff_review import compare, source_snapshot_input

def main():
    if sys.argv[1:2]==['novel']:
        from novel_workflow import main as novel_main
        extra=sys.argv[2:];extra=extra[1:] if extra[:1]==['--'] else extra
        return novel_main(extra)
    if sys.argv[1:2]==['scan']:
        from update.source_scan import main as scan_main
        extra=sys.argv[2:];extra=extra[1:] if extra[:1]==['--'] else extra
        return scan_main(extra)
    parser=argparse.ArgumentParser(description=__doc__)
    actions=parser.add_subparsers(dest='command',required=True)
    novel=actions.add_parser('novel',help='Prepare drafts, hand off PRO review, and apply reviewed Novel batches')
    novel.add_argument('arguments',nargs=argparse.REMAINDER)
    apply=actions.add_parser('apply-review')
    for name in ('package','review','state-root'):apply.add_argument('--'+name,type=Path,required=True)
    apply.add_argument('--current-source',type=Path,help='Required for observed-source changes; existing package-target KO edits need none')
    apply.add_argument('--output',type=Path,help='Optional mutable export; use next_package for the next apply')
    apply.add_argument('--expected-base',type=Path,help='Expected owned authoring base, defaults to --package')
    apply.add_argument('--report',type=Path,required=True)
    difference=actions.add_parser('diff')
    difference.add_argument('--previous',type=Path,required=True);difference.add_argument('--current',type=Path,required=True);difference.add_argument('--report',type=Path,required=True)
    ui=actions.add_parser('plan-ui-edit')
    ui.add_argument('--package',type=Path,required=True);ui.add_argument('--resource-id',required=True);ui.add_argument('--new-ko',required=True);ui.add_argument('--report',type=Path,required=True)
    ui.add_argument('--draft',type=Path,help='Write a HOLD change-set for explicit review; no JSON extraction needed')
    master=actions.add_parser('plan-master-key')
    master.add_argument('--package',type=Path,required=True);master.add_argument('--current-source',type=Path,required=True)
    for field in ('table','runtime-field','jp','new-ko'):master.add_argument('--'+field,required=True)
    master.add_argument('--report',type=Path,required=True)
    master.add_argument('--draft',type=Path)
    resume=actions.add_parser('resume-intake-completion');resume.add_argument('--state-root',type=Path,required=True);resume.add_argument('--report',type=Path,required=True)
    bound=actions.add_parser('bind-intake-wb')
    for name in ('current-source','bound-scene','raw-root','report'):bound.add_argument('--'+name,type=Path,required=True)
    for name in ('scene-sha256','source-id','asset-key'):bound.add_argument('--'+name,required=True)
    failed=actions.add_parser('intake-failed')
    failed.add_argument('--state-root',type=Path,required=True);failed.add_argument('--current-source',type=Path,required=True);failed.add_argument('--work-id',required=True);failed.add_argument('--reason',required=True);failed.add_argument('--report',type=Path,required=True)
    scan=actions.add_parser('scan',help='Known WebGL source or explicit local PC profile; no URL guessing')
    scan.add_argument('arguments',nargs=argparse.REMAINDER)
    status=actions.add_parser('status');status.add_argument('--state-root',type=Path,required=True)
    capture=actions.add_parser('import');capture.add_argument('--package',type=Path,required=True);capture.add_argument('--state-root',type=Path,required=True)
    doctor=actions.add_parser('preflight');doctor.add_argument('--candidate-root',type=Path,required=True);doctor.add_argument('--dependency-root',type=Path);doctor.add_argument('--medium',type=Path);doctor.add_argument('--plugins',type=Path);doctor.add_argument('--report',type=Path,required=True)
    arguments=parser.parse_args()
    if arguments.command in {'apply-review','diff','plan-ui-edit','plan-master-key','intake-failed','resume-intake-completion','bind-intake-wb'}:
        try:
            if arguments.command=='apply-review':
                current=source_snapshot_input(read_json(arguments.current_source)) if arguments.current_source else None
                review=read_json(arguments.review)
                # Absence of optional observation is handled per operation by apply_review.
                # Malformed supplied snapshots still fail above before a candidate is selected.
                result=apply_review(arguments.package,review,current,arguments.output,arguments.state_root,expected_base=arguments.expected_base)
            elif arguments.command=='diff':result=compare(source_snapshot_input(read_json(arguments.previous)),source_snapshot_input(read_json(arguments.current)))
            elif arguments.command=='plan-ui-edit':
                from update.operator_review import plan_ui_edit
                result=plan_ui_edit(arguments.package,arguments.resource_id,arguments.new_ko)
            elif arguments.command=='plan-master-key':
                from update.operator_review import plan_master_key
                result=plan_master_key(arguments.package,source_snapshot_input(read_json(arguments.current_source)),[arguments.table,arguments.runtime_field,arguments.jp],arguments.new_ko)
            elif arguments.command=='resume-intake-completion':
                from update.apply_review import resume_intake_completion
                result=resume_intake_completion(arguments.state_root)
            elif arguments.command=='bind-intake-wb':
                from update.novel_bridge import bind_existing_wb_scene
                result=bind_existing_wb_scene(source_snapshot_input(read_json(arguments.current_source)),arguments.bound_scene,arguments.scene_sha256,arguments.source_id,arguments.asset_key,arguments.raw_root)
            else:
                from update.intake_work import mark_failed
                current=source_snapshot_input(read_json(arguments.current_source))
                mark_failed(arguments.state_root,current['profile'],arguments.work_id,arguments.reason)
                result={'status':'FAILED_WORK_RETAINED_FOR_RETRY','work_id':arguments.work_id}
        except (ValueError,OSError,KeyError,TypeError,AttributeError) as error:
            code=str(error)
            if not code.replace('_','').replace(':','').isalnum():code='SOURCE_INPUT_INVALID' if arguments.command=='diff' else 'OPERATOR_REQUEST_INVALID'
            result={'schema':'gc-ng05-operator-result-v1','status':'REJECTED_SOURCE_INPUT' if arguments.command=='diff' else 'REJECTED_REQUEST','code':code,'next_package':None,'native':'NOT_RUN','game':'NOT_RUN'}
    elif arguments.command=='preflight':
        from update.preflight import preflight
        result=preflight(arguments.candidate_root,arguments.dependency_root,arguments.medium,arguments.plugins)
    elif arguments.command=='import':
        owned,metrics=freeze_input(arguments.package,arguments.state_root);result={'verified_package':str(owned),'metrics':metrics}
    elif arguments.command=='scan':
        from update.source_scan import main as scan_main
        extra=arguments.arguments[1:] if arguments.arguments[:1]==['--'] else arguments.arguments
        return scan_main(extra)
    else:
        root=arguments.state_root.resolve()
        files=sorted(root.rglob('*.json')) if root.exists() else []
        authoring=read_json(root/'authoring-current.json') if (root/'authoring-current.json').exists() else None
        result={'schema':'gc-ng04-operator-status-v1','state_root':str(root),'authoring_current':authoring,
                'durable_state_documents':[str(path.relative_to(root)) for path in files if 'verified-packages' not in path.parts],
                'runtime_selection_authority':'GCMod RuntimeCoordinator current/previous/pending_restart; no CLI Active pointer',
                'next_action':'continue with authoring_current.next_package or import an external package into a new state root'}
    if hasattr(arguments,'report'):
        arguments.report.parent.mkdir(parents=True,exist_ok=True);arguments.report.write_bytes(encode(result))
    if getattr(arguments,'draft',None) and 'review' in result:
        arguments.draft.parent.mkdir(parents=True,exist_ok=True);arguments.draft.write_bytes(encode(result['review']))
    print(json.dumps(result,ensure_ascii=False,indent=2))
    return 2 if result.get('status') in {'REJECTED_REQUEST','REJECTED_SOURCE_INPUT','PARTIAL_CONFLICTS','PARTIAL_HOLDS','HELD','ARTIFACT_FAIL'} else 0

if __name__=='__main__':raise SystemExit(main())
