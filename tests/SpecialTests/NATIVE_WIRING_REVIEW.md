# NG02 special native wiring review

Scope: compiled candidate Story, Battle and Fit3 providers plus managed transaction policies. This report records source and compile evidence. The game and all native callbacks are **NOT_RUN**.

## Actual subscriptions

`NativeHookHub` installs guarded, shared native hooks. These rows name the referenced original methods and the compiled handlers in the candidate. A rejected or unavailable signature produces no native invocation.

| Provider | Original method and parameter signature | Compiled handler | Callback timing |
| --- | --- | --- | --- |
| Story | `DMM.OLG.Unity.Extensions.Novel.EventSelect.AddSelectMessage(string, UnityEngine.Events.UnityAction)` | `ChoiceSourcePostAdd` | postfix |
| Story | `EventSelect.Open()` | `ChoiceOpen` | postfix |
| Story | `EventSelect.Release()` | `ChoiceRelease` | prefix |
| Story | `GcEventClickEvent.OnWordClickEvent(string)` | `ClickBefore`, `ClickAfter`, `ClickFinally` | prefix, postfix, finalizer |
| Story | `GcEventClickEvent.OpenKeywordPopup(Gc.Master.NovelKeyWordMaster)` | `RecordDelivered` | prefix |
| Story | `Gc.NovelKeyWordPopup.__c__DisplayClass2_0._Create_b__0(Gc.NovelKeyWordPopup)` | `AnnotationDeliveryPost` | postfix |
| Story | `Gc.Database.DatabaseTopController.UpdateKeyWordDetail(int)` | `DictionaryPost` | postfix |
| Battle | `Gc.Battle.UI.SkillInfo.Setup(Gc.Battle.Object.BattleUnit, Gc.Battle.Skill.ActiveSkill)` | `SkillSetup` | postfix |
| Battle | `Gc.Battle.UI.Detail.SharedEnemyInfoView.UpdateScrollView(Il2CppSystem.Collections.Generic.List<UnitInfoScrollCellData>)` | `Shared` | postfix |
| Battle | `Gc.Battle.UI.Detail.EnemyScrollCell.Setup(UnitInfoScrollCellData)` | `Cell` | postfix |
| Battle | `Gc.Battle.UI.Detail.ResistScrollCell.Setup(UnitInfoScrollCellData)` | `Cell` | postfix |
| Battle | `Gc.Battle.UI.Detail.ResistTextScrollCell.Setup(UnitInfoScrollCellData)` | `Cell` | postfix |
| Battle | `Gc.Battle.UI.Detail.EnemyInfoView.Show()` | `Enemy` | postfix |
| Battle | `Gc.Battle.UI.InstructionView.UpdateView()` | `Instruction` | postfix |
| Battle | `Gc.Battle.AutoView.UpdateView(bool)` | `AutoState` | postfix |
| Battle | `Gc.Battle.TimeScaleView.UpdateView(int)` | `SpeedState` | postfix |
| Battle | `Gc.Battle.TimelinePlayView.UpdateView(int)` | `EffectState` | postfix |
| Battle | `Gc.Battle.UI.BattleMenuPopup.OnSet()` | `Menu` | postfix |
| Battle | `Gc.Battle.UI.BattleMenuPopup.UpdateTabContents(int)` | `Menu` | postfix |
| Battle | `Gc.Battle.UI.BattleMenuSettingView.Init(UnityEngine.Events.UnityAction)` | `Setting` | postfix |
| Battle | `Gc.Quest.QuestSortieController.SetupUI()` | `Sortie` | postfix |
| Battle | `Gc.Result.MainView.Show()` | `Result` | postfix |
| Battle | `Gc.Battle.UI.CommandSet.Setup(Gc.Battle.UI.CommandView.SetParam)` | `CardCompleted` | postfix |
| Fit3 | `Gc.Novel.ContentScrollCell.Setup(Gc.Novel.ContentScrollCellData)` | `BeforeSetup`, `SetupPost` | prefix, postfix |
| Fit3 | `Gc.Novel.ContentScrollCell.UpdateView()` | `UpdatePost` | postfix |

Physical original method counts: Story 7, Battle 16, Fit3 2. Typed click finalizers return the original exception unchanged. There is no `__state` dependency.

## Runtime selectors, inputs and allowed writes

| Feature | Current native proof | Data authority | Allowed write / restoration |
| --- | --- | --- | --- |
| Choice | Current `EventSelect._selectList`; original closure/delegate chain identifies active ScriptObject and branch; exactly one matching current `selectif` source row; option order, button navigation/delegates, Letter references, component/TMP inventory, original rects and native raycasts stay equal before/after commit | Enabled JSON group/script/branch/exact source, complete one-to-one current group | New sibling TMP visuals and newly owned CanvasGroup masks only. Every visual prepared before any mask changes. All rollback paths restore own masks and remove own visuals. Original text, controls, Letter/Ruby and branches stay read only. |
| Annotation heading | Receiver-owned click token and generation; delivered original Master record pointer/id/title/body/ruby; delivered popup pointer/instance; exact compiled popup structure and direct detail/close references | JSON `辞典` caption; body/title/ruby supplied by active legacy Master | Shared `UiOwnershipService` writes only the distinct live heading TMP. Content fields are proof inputs, never UI rewrite targets. |
| Standalone dictionary heading | Current selected owned active list cell, same list model/index/reference; direct Master cache id/keyword; current controller scene/index and header reference | JSON `辞典` caption; independent from Annotation | Shared text ownership writes only `_subSceneTitleText`. |
| Battle captions B01–B07 | Natural completed callback; exact current scope class/owner/path; current source model pointer/scalars; enabled binding matches compiled source id/scope/slot/path; B05 requires exactly one active path at each segment | Active package resources and active legacy Master reference resolver | Shared text ownership writes only exact current TMP caption. All 106 current caption bindings have exactly one compiled route. |
| Battle cost title | Original completed `SetParam` pointer/skill/source; current `_button.Param`; unique current command slot 0 or 1; one active actor owner | JSON label plus original completed numeric suffix | Shared ownership changes the label only. Suffix including numbers, spaces, CRLF and tags is copied unchanged. No cost getter, parameter mutation or automatic battle action is called. |
| Fit3 | Active owned current cell checked before renderer lookup; current model/title plain Hangul equality; actual list ancestry; approved current Master output; fresh cached mesh/actual Medium glyph supplier/visible geometry; protected style and own scalar lease | Typed `episode-title.fit.v1` snapshot, 35 then 34/33/32/31 | Only fontSize plus measurement after a successful owned size write. Largest fitting size retained; minimum failure restores 35. External size/style/context changes yield. Setup reuse and retryable reads/writes keep a conditional restore lease. |

Every native target implements the shared managed main-thread access guard. All providers consume the immutable selected snapshot. None load layout JSON or translation files per callback. Story and Battle production resolvers receive the active Master view; a missing override defaults to empty Master authority.

All three constructors receive a managed `writesEnabled` delegate from the actual bridge configuration; absent authority defaults to false. The bridge uses `SpecialDisplayPolicy.AllowsWrites(Config.Translation.Value, Config.TranslationLanguage.Value)`, permitting only Translation ON with `ko` or `ko-ui3-common-poc`. Every new native callback/write/Tick owner lookup checks this authority first. Existing owned cleanup is a separate `ReconcileDisplay()` boundary: explicit OFF cancels Story wait/proof queues and removes Choice visuals/masks, cancels Battle pending parameters/captions, and conditionally restores all Fit3 leases including settled sizes. Unavailable current Master dependencies retract a complete Choice plan and restore affected Fit3 sizes. Temporary inactive/read/write failures preserve lease evidence; destroyed or rebound targets receive no stale restore write. UI-only reload continues to wait at the natural Choice boundary.

Finite work: Choice at most 32 current options and 512 source rows; Annotation click/record maps at most 32 and heading queue 16; Battle pending scopes 128 and card parameters 5; Fit3 active states 12. Waiting work expires or uses a finite frame schedule. Identity caches are limited to 4096 entries. The current rule counts are audit baselines, not runtime list-size gates.

## Managed verification

`SpecialTests` executes only managed source policies, mocks and JSON route coverage; no target/native assembly is referenced by that executable.

Latest result is recorded in `tests/SPECIAL_TEST_RESULTS.json`; the added configuration/Master availability and settled scalar restoration tests are part of that result.

Coverage: Translation OFF/language authorization before native work, retained Master plan invalidation with unrelated literal plan preservation, settled inactive/destroyed scalar restoration, non-fixed choice group and native order, partial/mismatched/duplicate source rejection, transaction preparation and mid-commit rollback, control invariance rollback, receiver token/generation/TTL and distinct record/popup proof, exact completed numeric suffix, finite Fit3 grid and external change yield, long-to-short reused scalar restoration, retryable restoration lease retention, main-thread rejection before target reads/writes, and all current 106 compiled Battle caption routes.

`SpecialNativeBuild` is a **compile-only** net6 project using readonly current game interop references. It is never executed. Owned source compiles with zero errors and zero owned-source warnings; remaining warnings, if reported, belong to the shared NativeHookHub.

Runtime limits: native signature/address guards, live object layouts, mesh suppliers, raycast equality and actual game behavior require a future authorized in-game check. Compile and managed tests do not claim those native checks have run.
