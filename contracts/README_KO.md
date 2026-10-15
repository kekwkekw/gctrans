# NG02 데이터 운용 계약

현재 authoring 정본은 `translations/ko`다. KO, source, selector, layout 정책은 JSON으로 편집한다. resource/binding ID는 기존 값을 유지한다. `migration/MIGRATION_MAP.json`은 이관 근거이며 정상 빌드의 필수 입력이 아니다. 회수 source와 최초 exporter는 audit/bootstrap에 사용한다.

Story6/Battle108의 기존 legacy eligibility를 복원했으며 일반 UI172의 활성 결정은 유지했다. enabled는 지원된 writer의 적용 자격이다. 실제 hook 설치와 게임 화면 확인은 별도로 보고한다. Choice는 JSON의 group/script/branch/exact source를 사용하며 물리행이나 현재 option 개수를 영구 포맷으로 삼지 않는다.

`markup_policy`의 기본값은 `exact-tags`다. 기존 세 Caption의 `omit-cspace-v1`는 `ui.caption.v1`, caption selector 및 승인된 역할에만 허용한다. co-match는 adapter/selector/enabled/JP/KO뿐 아니라 role/kind/disposition/MasterRef/markup policy도 같아야 한다. Python과 C#은 `tests/shared-validator-cases.json`의 같은 문서를 검사한다. unknown/unsupported optional capability는 정상 package의 해당 binding만 unavailable로 처리한다.

정상 교정은 현재 release/index와 명시 변경 목록으로 수행한다. 과거 receipt, archive, 작업 폴더의 절대경로가 필요하지 않다.

```powershell
python tools/validate_ng01.py translations/ko
python tools/build_manifests.py translations/ko --modify ui/resources.json
python tools/build_manifests.py translations/ko --add novels/NEW_ID.json
python tools/build_manifests.py translations/ko --modify names.json
python tools/build_manifests.py translations/ko --remove novels/REMOVED_ID.json
python tests/data_tools_tests.py
```

`--add`, `--modify`, `--remove`는 서로 겹치면 거부한다. 삭제는 실제 source 파일 제거와 명시 remove를 함께 수행한다. 새 Novel은 다음 UI-only 빌드에도 inventory에 남으며 legacy `manifest.json`의 registration은 자동 수정하지 않는다. 최초 release/index가 없는 import에만 `--legacy-receipt`를 지정한다. 정상 후속 빌드는 이전 release/index의 상호 일치와 metadata identity를 확인한다. unchanged Novel payload는 읽지 않는다.

trusted diff는 검증한 immutable package의 완전한 descriptor와 정확한 add/modify/remove 변경 목록을 받는다. 새 SHA/size가 있는 실제 변경 파일만 hash한다. immutable 상태와 이력을 믿을 수 없는 외부 mutable 폴더는 명시 `cold_scan`으로 모든 payload bytes를 비교한다. 같은 길이와 같은 mtime도 변경 여부의 근거로 사용하지 않는다.

```powershell
python tools/snapshot_descriptor.py BEFORE --mode verified_immutable_inventory --trust-immutable --report before-descriptor.json
python tools/snapshot_descriptor.py AFTER --mode verified_immutable_inventory --trust-immutable --report after-descriptor.json
python tools/semantic_diff.py BEFORE AFTER --mode trusted --old-descriptor before-descriptor.json --new-descriptor after-descriptor.json --changeset changeset.json --report diff.json
python tools/semantic_diff.py EXTERNAL_BEFORE EXTERNAL_AFTER --mode cold_scan --report cold-diff.json
```

changeset 형식은 `{"add":[],"modify":["ui/resources.json"],"remove":[]}`다. `--trust-immutable`는 현재 package가 검증·고정되었다는 caller의 선언이며 publisher 인증을 뜻하지 않는다. descriptor는 source tree 안에 넣지 않는다. 전체 미설치 staging은 `verify_staged_release.py`로 한 번 별도 검증한다. UI-only reload는 기존 Master/Names/Novel session을 유지하며 authority 변경은 기본 `RESTART_REQUIRED`다.

Fit3 layout은 현재 승인된35와34→33→32→31을 유지한다. `contract_validation.py`는 필요한 JSON Schema subset만 지원한다. 일반 Draft2020-12 준수를 주장하지 않으며 모르는 validation keyword는 거부한다. audit 출력은 relative POSIX 경로의 ordinal key와 안정된 record key로 정렬한다.
