# R36 production merge receipt v1.0

## 완료
- PR #13 merged
- main: `9e4bfa9691fe4251096a4aa12155794a182305cd`
- previous main: `340a3d2f5bcc793501887f15237b1095a1b21b7a`
- merge tree = final PR head tree: `819cc44e402a8030d911cf42b7694e0277e70a85`
- regular novel changed files: 338
- hold placeholders: 12/12
- two translator-note trailing `<br>` cleanups preserved

## GitHub Pages / production CDN
Pages run `35974594420` completed with conclusion `success`.
Downloaded Pages artifact `10797372255` was inspected.

Against sealed v1.2 regular-novel candidate:
- 336/338 files byte-identical
- exactly 2 expected differences
- those 2 differences are exactly the approved trailing `<br>` removals
- missing candidate files: 0

Therefore the Pages deployment artifact contains the merged regular-novel release as expected.

## Rollback
Created:
`rollback/pre-r36-translation-20260924`
→ `340a3d2f5bcc793501887f15237b1095a1b21b7a`

## GCMod decision
The current 6.1-based Korean custom GCMod reads regular novel JSON from the configured CDN.
This release changes translation JSON only and does not require a GCMod DLL/version update for the 338 regular novel files.

A GCMod/new-CDN migration remains a separate future project. Do not combine it with this completed translation release.

## Still deferred
- battle 13-row sidecar: runtime Prefix/Id not verified
- speaker 1-row sidecar: global names layer, current Korean production repo has no `names/zh_Hans.json`
