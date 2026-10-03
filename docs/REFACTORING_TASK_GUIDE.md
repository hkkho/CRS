# CANDU refuelling game: review and implementation tasks

Review date: 2026-10-02. Scope: the current working tree, including existing
uncommitted changes and experimental files. This review adds documentation only.
It does not remove code or change gameplay.

## Current delivery status

For the later plan assessment and temporary-artifact cleanup, see the
[implementation plan review](maintenance/implementation-plan-review.md) and
[knowledge library](maintenance/knowledge-library.md). This status table is the
current progress view. Original task checklists and dated implementation records
below are historical; unchecked boxes there do not reopen completed tasks.

The numbered findings below retain their original review text. This status table
and the implementation records at the end describe the current product.

| Task IDs | Status | Completed work or next requirement |
| --- | --- | --- |
| 01–04 | Complete | Reset response lifecycle, unified terminal state, no hidden practice events, per-bundle score breakdown/policy |
| 05 | Implemented; acceptance follow-up | Challenge/progress/report/seed retries pass automated playthroughs; human playtest measurements remain |
| 06 | Complete (initial balance pass) | Three seeds/four policies, operating scale corrected, physical outcomes retained; report in `gameplay/score-balance.md` |
| 07 | Complete | Shared position plans, outgoing burnup strip and immutable confirmed movement/identity summary |
| 08 | Complete | Request watchdogs, failed-session handling and explicit recovery |
| 09 | Complete | Constant-state versioned replay chain; streamed recording contract and solver-free scaling measurements |
| 10 | Complete | Highest-burnup wording, Game-owned geometry eligibility and power/zone headroom comparisons |
| 11 | Complete | Stable controller branch reasons, measured residuals and limiting-zone headroom |
| 12 | Complete | Immutable sandbox reasons, standard eligibility/reward exclusion and clean seeded reset |
| 13 | Complete | Native launcher/Designer actions, quiet announcements, keyboard play and 320px/zoom-emulated layouts |
| 14 | Complete | Native shell and on-demand Designer; cold normal/throttled measurements |
| 15 | Complete | Status notifications, cached derivations, stable SVG nodes and measured history reduction |
| 16 | Complete | Requested/observed pace, quiet solving state, bounded lifecycle measurement and worker latency budgets |
| 17–20 | Complete | Focused Game clock/candidates, direct equilibrium projection, instance bridge/shared wire fixtures and typed/injected frontend boundaries |
| 21 | Complete | 161-type consumer manifest; shared primitive extraction; 23 legacy files moved to research |
| 22–23 | Complete | Consumer-checked helper removal and explicit optional GPU research build |
| 24 | Implemented; acceptance follow-up | Release gates and local smoke pass; intentionally failing/green hosted CI runs remain |
| 25 | Complete | Empty Golden project removed; 31 unique fixtures retained with consumers |
| 26 | Complete | 58-tool/script and 101-artifact inventory; verified commands; canonical pack staging; indexed archives |
| 27 | Complete | Current architecture, direct-loop acceptance, research setup and historical index |
| 28 | Complete | Applied target agreement across Game/full/compact/UI |

After task 26: **26 completed tasks**, 2 implemented tasks with acceptance
follow-ups, and **no implementation tasks remaining**. Therefore **2 of 28 task
IDs remain open**: task 05 human playtest and task 24 hosted CI acceptance.
**Phase 5 is complete (21–23, 25–27).** No implementation slice is left partial.

### Implementation progress — first correctness slice

Implementation has begun on 01–04 and 28. This progress section describes changes
after the original review; the review findings below remain as the historical
task definitions.

- **01:** Studio now handles accepted sequence-reset responses and clears stale
  impact/result styling; regression covers sequence 20 → reset 1 → refuel 2.
  Transport remains serialized; no new run-generation protocol is introduced.
- **02:** Game guards all commands using scenario completion plus RRS exhaustion.
  Full/compact responses carry run status/reason; Studio and its clock stop for
  horizon completion while preserving the separate RRS state. The existing
  30-day browser horizon is retained. A richer final summary remains task 05.
- **03:** Removed the practice factory's obsolete scripted actions. Legacy
  scenario counters remain until the focused runtime refactor in task 17.
- **04:** Per-bundle scoring now publishes reward, fuel cost, net points and
  policy metadata through full/compact responses. Studio displays the breakdown;
  replay identity includes the policy ID. See the scoring slice record below.
- **28:** Full/compact targets use the applied physics target. Removed Studio's
  workaround. Contract tests cover paused rejection and queued/applied behavior.
- **08/24:** Worker recovery and CI gates implemented in the previous slice.
  Hosted failing/green CI acceptance runs remain unverified. No broad obsolete-code
  deletion yet.

Validation for this implementation slice is recorded at the end of this guide.

## Direction and review limits

Make refuelling the meaningful decision: inspect a channel, understand which
bundles will leave, spend scarce fresh fuel, observe the accepted response, and
finish a shift with an understandable result. Keep Core authoritative and retain
the browser → BrowserHost → Browser → Game → Core architecture. Keep shutdown,
scram, accidents, operator training and full plant operations out of scope.

This is a repository-wide architectural and targeted source review, not a claim
that every numerical equation or historical reference artifact was independently
validated. Inventory and dependency searches covered production projects, tests,
tools, benchmarks, data, references, documentation and deployment configuration.
The active gameplay, transaction, transport, rendering and scoring paths received
the closest inspection. Historical research implementations received structural
and consumer review; they require the deletion gates below before removal.
No deployed playthrough or fresh WASM publication was performed for this review.

Existing strengths to preserve:

- One authoritative simulation; worker failure does not activate a fake reactor.
- Candidate/commit handling for refuelling, layout edits and equilibrium solves.
- Explicit thermal/electrical units, seeded starts, bundle identities and poison
  history, full/compact protocol responses, and deterministic numerical tests.
- Clock backpressure and hidden-tab handling; bounded presentation history.
- Native Studio controls, keyboard map navigation and meaningful browser smoke
  scripts. OperationsScene is already deleted in this working tree.

Issue labels below distinguish **defect** (source establishes a mismatch),
**risk** (a missing guard or scaling problem), **design** (proposed improvement),
and **cleanup** (requires dependency verification). P1 means next correctness or
reliability work; P2 means the following playable/refactoring slices; P3 means
later consolidation. No P0 emergency was established.

## Ordered delivery plan

| Slice | Tasks | Playable exit condition |
| --- | --- | --- |
| 1. Trustworthy run | 01–04, 08, 24, 28 | Reset feedback, terminal state, targets and score agree across layers; checks gate changes. |
| 2. Understandable refuelling | 05–07, 10–12 | Player can explain a move, its cost, its result and the shift objective. |
| 3. Responsive and accessible | 09, 13–16 | Loading/failure recovery is clear; controls work at smaller sizes; measured latency is visible. |
| 4. Separate responsibilities | 17–20 | Session, bridge and solver concerns are independently testable without rule duplication. |
| 5. Remove obsolete branches | 21–23, 25–27 | Proven unused code is removed; valuable experiments/provenance have explicit homes. |

Tasks are separate implementation units, not instructions for one large rewrite.
Do not combine numerical changes with mechanical moves or scoring changes.

## 01 — Reset sequence numbers suppress new-run feedback

**P1 · defect.** `src/ReactorSim.Browser/PlaytestBridge.cs`, `Dispatch`, replaces
the runtime on reset and increments its new sequence from zero. In
`web/candu-playtest/src/studio/StudioView.ts`, `receive`, response handling is
guarded by `response.sequence > this.lastSequence`. After a run at sequence 20,
reset sequence 1 is ignored; subsequent moves are ignored until they exceed 20.
Snapshot rendering continues, making the stale impact card especially misleading.

- [ ] Add a regression for sequence 20 → accepted reset 1 → accepted refuel 2.
- [ ] Define response identity across runs: preferably a run generation plus
  sequence, or explicitly reset the response tracker on an accepted reset.
- [ ] Clear impact, result styling and stale errors exactly once on reset; retain
  the intended selection/draft policy explicitly.
- [ ] Audit other response sequence comparisons, including Core Designer, for
  the same lifecycle assumption. Reject late responses from an earlier run.

**Acceptance:** the first move after every reset shows the new move's deltas;
duplicate status notifications do not replay feedback. Run Studio/controller
tests and `tools/Test-Browser.ps1`. **Dependency:** none.

## 02 — Two different terminal-state authorities

**P1 · defect.** `Phase8ScenarioRuntimeContracts.cs` completes at the scenario
horizon (`SurvivedScenarioHorizon`) and refuses further actions. The factory sets
a 30-day browser horizon. `GameSession.CreateSnapshot` publishes that outcome
separately but derives `IsGameOver` only from `_practiceRrs`. Browser scheduling
and Studio use `snapshot.rrs.isGameOver`. A horizon-completed run can therefore
look running while its scenario clock no longer advances.

- [ ] Add a focused short-horizon Game fixture to reproduce completion without
  RRS exhaustion; avoid a costly real 30-day test.
- [ ] Make Game own one run status: running, paused, completed, or ended, with
  explicit reason and final summary. Preserve RRS diagnostics separately.
- [ ] Publish status in full and compact responses; stop the browser pump for
  every terminal outcome and consistently reject further gameplay mutations.
- [ ] Decide whether free practice is unlimited or horizon-limited; make the
  displayed objective match that decision.

**Acceptance:** horizon and both RRS exhaustion outcomes stop once, freeze final
score, display a reason and permit reset. **Checks:** Game + Browser + controller
tests. **Dependency:** none; precedes 05 and 17.

## 03 — Hidden scripted power changes remain in free practice

**P1 · defect relative to the player-controlled loop.**
`PracticeGameSessionFactory.CreateSession` installs power-target events at 120
seconds (95%) and 360 seconds (100%), plus a refuel-request event at 240 seconds.
`Phase8ScenarioRuntimeV1.ApplyScriptedEvent` applies these. The refuel request
only decrements a legacy counter; it does not move bundles. At accelerated
browser pacing these events occur near the beginning of play without a visible
tutorial contract.

- [ ] Use an empty event list for free practice; keep intentional scripted events
  only in named, visible challenge definitions.
- [ ] Add tests that a player's target survives crossing 120/360 seconds and
  that inventory/operation counts change only through accepted fuel moves.
- [ ] Remove the synthetic request counter from the active presentation if it
  has no player-facing purpose; do not confuse it with fresh-bundle inventory.

**Acceptance:** time alone cannot silently replace the player's target through
these legacy tutorial events. **Checks:** Game + Browser. **Dependency:** none.

## 04 — Implemented discharge scoring differs from the stated rule

**P1 · defect.** `GameSession.PracticeRefuellingScore` clamps the average burnup
and multiplies by shift size. `WEB_ROADMAP.md` specifies a per-bundle clamp.
These are not equivalent for mixed discharge above the cap. For four bundles
with burnups `[0, 0, 20, 20]`, current code gives 24 points; the documented rule
gives 9. High-burnup bundles currently offset fresh-fuel waste beyond their cap.

- [x] Extract a named scoring policy in Game and implement the documented sum
  of per-bundle rewards (or explicitly revise the design before implementation).
- [x] Test mixed above-cap/fresh discharge, all-fresh, all-at-cap, both directions,
  four/eight shifts, reversal and rejected operations.
- [x] Publish an operation score breakdown with reward and fresh-fuel cost;
  keep the authoritative formula out of TypeScript.
- [x] Version the scoring policy for run comparison and replay metadata.

**Acceptance:** the mixed example produces the chosen documented result and
reversing fresh fuel cannot farm score. **Checks:** Game + Browser. **Dependency:** none.

## 05 — Shifts need a visible goal and a useful ending

**P2 · design.** The factory supplies 128 bundles and a 30-day horizon, while
Studio offers a score and generic “Shift complete” text. There is no dedicated
result summary or objective progress view. These constants are authored defaults,
not evidence of a tuned game duration.

- [x] Define one short refuelling challenge in Game: seed, duration, fuel budget,
  completion rule and a visible reward. Keep free practice available.
- [x] Show the current objective and remaining simulated time/fuel before play.
- [x] Add an end card: outcome reason, energy delivered, fuel consumed, useful
  discharge, score components, and retry-same-seed/new-seed actions.
- [ ] Run human playtests; record time to first successful refuel, understood
  mistakes, completion rate and reason for restarting. Tune from observations.

**Acceptance:** a new player can state the objective and explain the outcome
without interpreting solver fields. **Checks:** deterministic Game outcome tests
and a browser playthrough. **Dependencies:** 02–04.

## 06 — Operating score overwhelms the refuelling reward scale

**P2 · design/balance risk.** `TryBuildPracticeAdvance` adds up to 0.5 points per
simulated second (`0.35 * powerQuality + 0.15 * tiltQuality`). An ideal hour is
1,800 points; a maximum-scoring eight-bundle move is 48. This arithmetic does not
prove a winning exploit, but it makes a fuel move's displayed reward tiny next
to ordinary time progression.

- [x] Compare deterministic same-seed policies: no refuels, oldest-channel,
  deliberate waste/reversal, and a considered player strategy.
- [x] Report energy, survival duration, fuel use and each score component;
  separate policy quality from machine-dependent wall-clock speed.
- [x] Tune authored scoring parameters and challenge length so useful fuel
  decisions materially affect the outcome without awarding button presses.
- [x] Show score components, not just the total, in the result card.

**Acceptance:** policy comparisons demonstrate the intended incentive; capture
the balance rationale and seed set. **Checks:** focused campaign tests and
playtesting. **Dependencies:** 04, 05. Do not change physics to force the score.

## 07 — Direction and shift size lack a concrete fuel-movement explanation

**P2 · design.** Studio draws axial power/burnup graphs and direction text, but
does not clearly mark which four/eight bundles will leave, which remain and
which are inserted. `GameRefuellingResultV1` already retains inserted/discharged
bundle records; the browser mostly gets the resulting state and text.

- [ ] Expose an immutable accepted-operation summary from Game: affected channel,
  direction, old/new bundle identities, discharged burnup and score breakdown.
- [ ] Visually mark incoming/outgoing ends and discharged positions. If a draft
  needs rule-dependent movement data, ask the shared layer; do not duplicate
  the Core shift algorithm or predict reactor response in JavaScript.
- [ ] Animate confirmed movement only; respect reduced motion and never add an
  artificial cooldown or require a preview/confirm workflow.
- [ ] Persist the last operation summary across Designer navigation.

**Acceptance:** both directions and both sizes are understandable visually and
through text; failed commands never animate a successful transfer.
**Checks:** movement contracts + bridge DTO + Studio tests. **Dependency:** 04.

## 08 — Worker requests can remain pending indefinitely

**P1 · reliability risk.** `WorkerProtocolBridge.ready` and `request` in
`bridge.ts` depend on messages/errors. Pending requests have no timeout, and
there is no `messageerror` handler in the worker interface. A silent startup or
lost response can leave controls indefinitely loading/solving.

- [x] Add separate configurable startup and command watchdogs, sized using
  measured cold/WASM solve times rather than an arbitrary short deadline.
- [x] On expiry, terminate the worker, reject all pending work and enter a clear
  failed state; prevent late replies from restoring the old session.
- [x] Offer explicit restart/reload recovery with an explanation of lost progress.
  Do not automatically resend a possibly committed refuel.
- [x] Test never-ready, missing result, malformed-message failure, worker error,
  dispose during a request and successful long-running work.
- [x] Catch the rejected reset promise in `TitleScene`'s new-seed callback and
  present the controller error; its current `.then(...)` has no rejection handler.

**Acceptance:** every request settles or enters explicit recoverable failure;
no double fuel movement. **Checks:** bridge/controller tests. **Dependency:** none.

## 09 — Long runs accumulate unbounded replay data and repeated hashing

**P2 · scaling risk.** `PlaytestBridge.BridgeRuntime` retains `CommandJson` and
`History` lists. `ComputeReplayDigest` joins the entire command list on each
response. This produces growing memory and O(n²) cumulative hashing work over
n commands; the browser history's 4,096 cap does not bound these C# lists.

- [ ] Measure retained bytes and command overhead at 100, 1,000 and 10,000 cheap
  deterministic commands, separately from solver time.
- [ ] Choose a replay contract: streamed/chunked command recording plus a bounded
  diagnostic ring, or an explicitly bounded session replay.
- [ ] Use incremental hashing where the current canonical byte stream can be
  preserved, or version a new chained digest. Do not silently change digest meaning.
- [ ] Remove duplicate command storage if a single record can serve both uses.
- [ ] Preserve rejected-command replay semantics and reset/initialization identity.

**Acceptance:** per-command bookkeeping stays approximately constant and bounded
diagnostics do not destroy required replay evidence. **Checks:** replay/digest
regressions + long-run measurement. **Dependency:** 01 lifecycle decision.

## 10 — “Oldest fuel” is burnup ranking, and includes unusable channels

**P2 · UX mismatch.** `oldestFuelChannel` and Studio's watchlist rank average
burnup, not `insertedAtSeconds`. They admit channels with any fuel, whereas
`GameSession.RefuelChannel` rejects a channel containing any configured nonfuel
cell. Designer can therefore leave a highly ranked candidate unrefuellable.

- [ ] Rename the helper/UI to “Highest burnup” or use actual residence age if age
  is the intended concept; distinguish these measurements in copy.
- [ ] Publish/refine authoritative refuelling eligibility and reason per channel.
- [ ] Filter or clearly label ineligible candidates; show why before the click.
- [ ] Let players compare observed burnup, local power and relevant zone reserve
  without presenting a sorted list as a prediction or mandatory strategy.

**Acceptance:** mixed fuel/nonfuel channels do not masquerade as executable
orders; ties remain deterministic. **Checks:** presentation + Game/bridge tests.
**Dependency:** none.

## 11 — RRS feedback reports movement without explaining the decision

**P2 · design.** Studio's zone note uses the largest `appliedFillCommand`, or
“No fill movement on this solve.” The controller has convergence, candidate and
correction information, but the player cannot tell whether retained fills are
adequate, constrained, or the best available candidate.

- [ ] Add a stable controller decision/reason code in Core with measured facts.
- [ ] Map it in Game to concise feedback: already balanced, corrected shape,
  correction retained, bounded by fill limits, or exhausted, as actually applicable.
- [ ] Highlight the limiting zone and both ends of available headroom.
- [ ] Explain accepted before/after changes without claiming uncomputed causality.

**Acceptance:** retained fills and exhausted reserve have distinct explanations;
UI text does not infer a controller reason from a small numerical delta.
**Checks:** controller branch tests + Browser/Studio tests. **Dependency:** none.

## 12 — Engineering edits and ordinary scores are indistinguishable

**P2 · design risk.** `ConfigureCell`/`ConfigureZoneLayout` deliberately preserve
score and fuel while changing physical geometry. Debug Game methods also grant
fuel or reset score. These are useful tools, but future personal bests/challenges
cannot compare edited and standard runs fairly.

- [ ] Introduce run provenance in Game: standard challenge versus modified
  sandbox, with immutable reasons once changed.
- [ ] Keep Designer inspection available; flag accepted physical edits, not mere
  navigation or rejected edits.
- [ ] Mark sandbox results clearly and exclude them from standard challenge bests.
- [ ] Keep developer grant/reset methods out of normal player command routing.

**Acceptance:** Designer remains useful and no geometry-modified run is presented
as a standard challenge result. **Checks:** Game + bridge tests.
**Dependencies:** 05; must precede any leaderboard/best-score feature.

## 13 — Launcher and Designer have a different accessibility standard

**P2 · accessibility risk.** Title/Designer use Phaser canvas controls and a fixed
1600×900 logical layout; Studio uses native DOM controls. Title has Enter/Space
for starting, but the canvas does not expose its individual buttons as native
controls. `main.ts` rewrites the hidden `aria-live` status mirror on every update,
including changing running score, which risks excessive announcements.

- [ ] Provide semantic DOM controls for launch, seed selection and essential
  Designer actions, with accessible names and visible focus.
- [ ] Announce accepted operations, failures and terminal transitions once;
  keep continuously changing telemetry outside the live region.
- [ ] Verify keyboard-only use, zoom and 1280×720 layout; define a minimum usable
  narrow-screen layout rather than relying only on canvas scaling.
- [ ] Review map keyboard scope so arrows on unrelated controls do not unexpectedly
  move focus to the reactor map.

**Acceptance:** all primary actions work without a pointer, announcements remain
usable during live running, and labels are readable. **Checks:** browser keyboard
playthrough and focused DOM tests. **Dependency:** none.

## 14 — Eager Phaser shell makes the initial web bundle large

**P2 · measured optimization opportunity.** The review build emitted a 1,330.13 kB
main JS chunk (367.63 kB gzip), with Vite's large-chunk warning. `main.ts` eagerly
imports Phaser and all scenes even though the main game UI is DOM/SVG. This is
not a measured time-to-interactive result and excludes WASM transfer costs.

- [ ] Measure cold load transfer, WASM startup and time to usable Begin Shift.
- [ ] Evaluate a small DOM launcher/Studio shell with Designer loaded on demand.
  Preserve one BridgeSessionController and history across transitions.
- [ ] Split by real feature boundary; do not merely raise the chunk warning limit.
- [ ] Compare startup on a throttled connection and a slower device before/after.

**Acceptance:** report measured startup improvement without losing Designer or
creating another simulation/session. **Checks:** production build and smoke.
**Dependencies:** 13; coordinate with 20.

## 15 — Frequent notifications rebuild presentation work

**P2 · performance risk.** `sessionController.dispatch` emits pending, response
and completion updates. `StudioView.render` revisits every channel, sorts the
watchlist and replaces axial SVG HTML on every emission. History charts generate
paths over the retained sample set. These are identifiable work sources, not yet
proven the dominant bottleneck.

- [ ] Profile a 380-channel run with a full history buffer and separate solver,
  transfer, parse/materialization, render and paint times.
- [ ] Separate command-status updates from changed snapshot/selection updates.
- [ ] Cache derived watchlists and patch existing SVG nodes when inputs change.
- [ ] If chart rendering is costly, downsample display paths while retaining full
  samples, extrema, discontinuities and inspection values.
- [ ] Preserve focus and avoid excessive allocation during pending notifications.

**Acceptance:** measured rendering improves; charts retain real observations and
no simulation rules migrate to TS. **Checks:** targeted rendering tests and
browser profiling. **Dependency:** baseline measurements before optimization.

## 16 — Speed labels describe requested acceleration, not delivered pace

**P2 · UX/performance design.** `LiveClockScheduler` intentionally discards time
spent solving and caps backlog at one quantum. Thus “1×” (authored as 30 simulated
minutes per real second) slows with solver latency. This is appropriate
backpressure, but the UI gives no achieved-rate indication.

- [ ] Retain bounded scheduling; never fix slow play with a catch-up spiral.
- [ ] Display requested pace and observed simulated-time/wall-time rate, with a
  quiet solving indicator when performance limits the rate.
- [ ] Benchmark full command latency at each pace and establish measured budgets
  for refuel, normal tick, shape solve and initial load.
- [ ] Use results to prioritize controller/transport work; do not relax numerical
  tolerances simply to make a speed label appear accurate.

**Acceptance:** users can understand slow progression and foreground pause/refuel
remains responsive. **Checks:** clock tests + production-shaped benchmarks.
**Dependency:** none.

## 17 — GameSession combines too many responsibilities

**P2 · refactor.** `GameSession.cs` is roughly 2,000 lines and contains command
orchestration, geometry edits, time integration, score calculation, transaction
construction, terminal-time search and detailed snapshot projection. It also
plans and then executes the legacy scored scenario runtime although the shown
score is `_syntheticScore`.

- [x] First characterize accepted/rejected state changes with current tests and
  a small deterministic command corpus.
- [x] Extract scoring, presentation projection and geometry transactions into
  named Game components; keep GameSession as the public command boundary.
- [x] Replace the active Phase8 scored runtime with a focused run clock/target/
  outcome model after 02/03; retain queued-target timing and pause semantics.
- [x] Keep an immutable candidate transaction containing fuel, poison, projection,
  RRS, clock and score; commit only after every validation succeeds.
- [x] Rename synthetic/phase-era identifiers only in a separate mechanical slice.

**Acceptance:** same seed/commands yield equivalent state, inventory, score and
failure behavior except separately approved fixes. **Checks:** Core/Game/Browser
suites. **Dependencies:** 02–04; preserves 12 provenance.

## 18 — Equilibrium presentation still depends on IQS compatibility types

**P2 · refactor/cleanup prerequisite.** `EquilibriumCoreProjectionV1` stores
`LegacyPresentationProjection` as `IqsSpatialCandidateV1`; the equilibrium solver
constructs compatibility reactivity and IQS objects. Game and Browser expose/cache
these types even though the active model is a steady-state equilibrium solver.
Deleting `IqsFullCoreSolver.cs` by name alone would be unsafe.

- [x] Inventory the exact projection fields consumed by Game/Browser and tests.
- [x] Introduce an equilibrium-specific read-only projection contract; map it once
  to presentation snapshots without constructing a pretend kinetics result.
- [x] Migrate identity/change detection and compact replacement logic carefully.
- [x] Move legacy IQS tests/solver to a retained research target or remove them
  only after references are gone and their useful invariants have active tests.

**Acceptance:** active gameplay has no IQS presentation dependency; fission
normalization, group ordering, poison coupling and failure diagnostics survive.
**Checks:** Core/Game/Browser and replay comparisons. **Dependency:** 17 projection extraction.

## 19 — The bridge is a global session plus serializer plus experiment host

**P2 · refactor.** `PlaytestBridge.cs` is roughly 2,300 lines, including DTOs,
static runtime/locking, dispatch, mapping, digest construction and GPU fixtures.
`protocol.ts` separately maintains a large matching TypeScript contract/parser.

- [x] Extract DTO declarations, snapshot mapping, protocol parsing, digest policy
  and instance-owned session dispatch into small cohesive modules.
- [x] Keep exported static methods as a thin adapter over one explicit runtime.
- [x] Add shared serialized fixtures covering full, compact, replacement, resync,
  failure and reset across C# and TS; consider generation for mechanical DTO
  shapes, while retaining strict runtime validation.
- [x] Keep serialization names, missing/null semantics, units and canonical digest
  bytes stable; version intentionally incompatible changes.

**Acceptance:** tests can instantiate independent runtimes and cross-language
fixtures catch contract drift. **Checks:** Browser/Vitest, protocol fixture tests.
**Dependencies:** 01/02 contract decisions; coordinate 09 and 23.

## 20 — Frontend state and view responsibilities need smaller boundaries

**P2 · refactor.** Studio currently owns static markup, event routing, map,
watchlist, graphs, target controls and response state in one class. Designer has
its own large presentation path and gets the session through a global singleton.

- [x] Extract map, order/impact and status/control views only where each has an
  independent update/lifecycle boundary; keep native DOM instead of adopting a
  framework just for file size.
- [x] Define typed navigation state shared by Studio/Designer rather than the
  Designer's generic `Record<string, unknown>` storage.
- [x] Inject the session into view adapters and explicitly own subscription disposal.
- [x] Preserve selection, draft, tab and history across navigation; retain response
  summaries in session presentation state where persistence is intended.

**Acceptance:** repeated navigation creates no duplicate listeners, loses no
chosen draft and preserves keyboard focus behavior. **Checks:** view/controller
tests + smoke. **Dependencies:** 01, 07; coordinate 14/15.

## 21 — Phase-era Core trees need a consumer-led retirement plan

**P3 · cleanup candidate, not blanket dead-code finding.** Core compiles large
Phase5 lifecycle/state, Phase6 queue/device/influence-map, Phase7 kinetics/xenon,
reduced-model and serialization trees. The active factory uses Practice RRS,
Practice Xenon and equilibrium solving, but shared types and remaining tests/tools
still connect these families. Folder names do not establish obsolescence.

- [x] Produce a type-level consumer manifest for each family: active runtime,
  active invariant test, retained research tool, or no consumer.
- [x] Extract genuinely shared primitives first, then separate optional research
  implementations from the gameplay dependency graph.
- [x] For each deletion, identify the replacement coverage for units, inventory
  conservation, canonical state, time ordering and rejected-command atomicity.
- [x] Remove unused serializers only after proving they are not called by retained
  tools; reevaluate the Newtonsoft dependency after this boundary is established.
- [x] Keep manifests/provenance for retained packs; archive historical material
  with an index rather than silently orphaning sources.

**Acceptance:** every removed public type has a checked consumer report and the
active browser path builds/tests. **Checks:** all affected projects, not only the
main solution. **Dependencies:** 17/18 and tool classification in 26.

## 22 — Small obsolete helpers and aliases can be removed first

**P3 · concrete cleanup candidates.** Repository searches found no callers for
`drawing.ts:drawMeter`; `commandState.ts:toggleShiftCount` and `adjustTarget` are
only used by their own tests. `KineticsDataPackVersion` is explicitly a legacy
browser/Unity alias, still consumed by Browser and a test. `liveClock.ts` retains
an ignored-but-validated `maxBacklogMs` compatibility option. The JSON compatibility
probe has no external repository call site in this review.

- [x] Recheck imports/usages at implementation time, including scripts, then
  remove proven unused helpers and tests that only preserve them.
- [x] Replace the active metadata alias with `DiffusionDataPackVersion` without
  changing the wire value.
- [x] Remove obsolete scheduler options after migrating any real callers/tests.
- [x] Delete or relocate `JsonSerializationCompatibilityProbe` if its only purpose
  was past platform admission; retain any still-useful serialization assertion.
- [x] Do not restore OperationsScene or its old preview/cancel flow.

**Acceptance:** no unresolved references; TS build and affected .NET suites pass.
**Dependency:** none for helpers; no broad Core deletion in this task.

## 23 — Experimental GPU/CPU hosting crosses the production boundary

**P3 · isolation, not automatic removal.** Core embeds WGSL fixtures, Browser
serves GPU experiments, and BrowserHost publishes experiment exports/modules.
The product documentation says GPU migration is paused. CPU parallelism is
already opt-in; preserve that explicit boundary.

- [x] Put fixture generation and GPU prototype hosting behind a separate research
  project/build option; keep ordinary browser exports focused on the game.
- [x] Keep one documented command for reproducing retained experiments and their
  numerical comparison budgets.
- [x] Verify normal builds exclude experiment resources/exports; measure resulting
  publish size rather than assuming managed trimming removes everything.
- [x] Retain tests for any shared row-operator code; do not change reduction order
  while moving experimental hosts.

**Acceptance:** default WASM remains single-threaded, works without GPU/shared
memory, and developer experiments still have an explicit runnable entry point.
**Checks:** default build/smoke plus opted-in experiment tests. **Dependencies:** 19.

## 24 — CI does not run the shared simulation suites before deployment

**P1 · validation gap.** The only workflow,
`.github/workflows/deploy-candu-playtest.yml`, runs on main push/manual dispatch,
publishes WASM and runs frontend tests/build, then deploys production before smoke
and benchmarks. It does not run the Core/Game/Browser test suites. A successful
compile cannot replace simulation regression tests.

- [x] Add pull-request CI using `tools/Test-DotNet.ps1` and
  `tools/Test-Browser.ps1`, with deliberate handling of their overlapping Browser
  suite rather than accidental duplicate runs.
- [x] Make deploy depend on the shared tests; retain frontend validation.
- [x] Run a production-shaped local or preview smoke before production promotion;
  keep stable-alias smoke/reproduction checks after deployment.
- [x] Upload useful failures/benchmark artifacts; ensure concurrent deployments
  cannot make stable-alias checks inspect an unrelated commit.

**Acceptance:** a failing shared invariant blocks release before users receive it.
**Checks:** workflow review and one intentionally failing test in a temporary
validation branch, then a green run. **Dependency:** none.

## 25 — An empty Golden test project gives a misleading validation surface

**P3 · confirmed obsolete project shell.** `tests/ReactorSim.Golden.Tests` has
the project file but no authored C# test files. Its csproj copies many historical
fixtures and is still listed in `ReactorSim.sln`. `Test-DotNet.ps1` does not run it.

- [x] Remove the empty project from the solution and repository, unless a specific
  retained golden test is deliberately restored now.
- [x] Trace copied fixture consumers before removing any data; an empty test
  project does not prove its referenced artifacts are unused everywhere.
- [x] Put necessary active golden checks in an actual executed suite with explicit
  comparison tolerances and authored-data labels.

**Acceptance:** solution/test listings match real executable coverage; no orphaned
links. **Checks:** solution build + standard scripts. **Dependency:** none.

## 26 — Research tools and duplicate data lack a clear supported inventory

**P3 · maintenance risk.** Numerous one-off authority/calibration tools coexist
with active AgedCore/SingleSolve/CPU/browser benchmarks. The older benchmark
program references `benchmarks/P9-T03-core-solver-profile-parameters-v1.json`,
which is absent from the repository inventory. The diffusion pack is duplicated
under `data/packs` and Core EmbeddedData; their hashes currently match.

- [x] Create a tool inventory: purpose, maintained command, inputs, outputs,
  active consumers and keep/archive/delete decision.
- [x] Reproduce retained commands; repair or retire the old benchmark profile mode
  that requires the missing P9 manifest. Do not label every mode broken.
- [x] Establish one canonical diffusion-pack source with deterministic staging or
  an equality check; keep provenance and embedded runtime independence.
- [x] Archive superseded mapping/deck versions and one-off authority generators
  only after tracing references and preserving useful reproduction instructions.
- [x] Keep current solver/browser benchmarks easy to find and distinct from
  external analysis programs, which remain development-only dependencies.

**Acceptance:** each retained tool has a verified invocation and each retained
data artifact has an owner/consumer. **Checks:** affected tool builds and pack audit.
**Dependencies:** supports 21; can start independently.

## 27 — Documentation still describes removed or misleading behavior

**P3 · confirmed documentation drift.** The implementation guide's launch checks
still require preview/cancel/confirm even though Studio refuels directly. It also
describes replay export/feedback companion tooling that is not exposed by the
current Studio. The roadmap's score formula differs from code (04), and Phase8
playback names include “real-time” despite the accelerated browser base rate.

- [x] Update acceptance instructions to the actual direct-refuel loop in both
  directions and both sizes; retain atomic rejection tests separately.
- [x] Mark internal digest/reproduction tooling as developer-only unless a working
  player export/import UI is actually added.
- [x] Link one active roadmap, this task guide, and one accurate architecture page;
  clearly label historical research specs and paused proposals.
- [x] Document requested versus achieved pace, final score formula, terminal rules,
  modified-run policy and current validation commands after their tasks land.

**Acceptance:** a contributor can follow the docs without looking for removed
screens, nonexistent commands or obsolete gameplay. **Checks:** walkthrough/link
review. **Dependencies:** update alongside 02/04/12/16, not months afterward.

## 28 — Top-level power target is hardcoded despite a live target

**P1 · contract defect.** `PlaytestBridge.CreateSnapshot` and
`CreateSnapshotPatch` assign `TargetPowerFraction = 1.0`. Studio works around
this with `physics.targetPowerWatts / physics.referencePowerWatts`, even passing
an overridden snapshot into status formatting. Consumers of the top-level field
can disagree with the actual applied target after an 80–120% command.

- [ ] Define separate requested/queued and applied target semantics in Game if
  both are needed; avoid one ambiguously named field for two values.
- [ ] Populate full and compact responses from the same authoritative applied
  target and retain explicit units for the watt-valued field.
- [ ] Remove Studio's compatibility override once the contract is consistent.
- [ ] Test queueing while paused, application after resume, rejected values and
  full/compact parity at 80%, 95%, 100% and 120%.

**Acceptance:** all target displays and status calculations agree with the shared
state, including while a target is pending. **Checks:** Game/Browser/protocol and
Studio tests. **Dependencies:** coordinate with 03 and 19.

## Coverage map

| Area inspected | Main findings/tasks | Important retained boundary |
| --- | --- | --- |
| Core inventory, burnup, topology, equilibrium, RRS, xenon | 04, 07, 10, 11, 18, 21 | Deterministic state and physical rules remain in Core. |
| Game factory, session, transactions, presentation | 02–07, 12, 17 | Game owns objectives, scoring, run status and immutable presentation. |
| Browser protocol, serialization, replay, host/worker | 01, 08, 09, 19, 23, 28 | One authoritative serialized command stream. |
| Studio, Designer, launcher, history, clock | 01, 05–16, 20, 22 | Presentation observes state; it does not solve reactor rules. |
| Unit tests, smoke, CI and publish scripts | 24, 25 and per-task checks | Browser deployment remains primary acceptance. |
| Benchmarks, calibration/authority tools, packs, references | 21, 23, 26 | Preserve useful fixtures and provenance; external programs stay offline. |
| README, implementation guide, roadmap, historical specs | 27 | Distinguish current product instructions from historical research. |

## Validation and deletion rules for every slice

1. Inspect the working diff first; this review started with extensive user changes.
   Keep each implementation commit focused and do not revert concurrent work.
2. Add a focused behavior regression for actual defects before changing them.
   Avoid tests that merely duplicate a helper's implementation.
3. Use `tools/Test-DotNet.ps1` for Core/Game/Browser changes and
   `tools/Test-Browser.ps1` for browser contract/presentation integration.
4. For deployment-sensitive changes, build the actual WASM configuration and
   exercise smoke plus the reproduction matrix. A frontend build alone cannot
   validate staged WASM or the deployed alias.
5. Before deletion, search references in code, project files, tests, scripts and
   docs. Build retained tools outside the solution. Record where useful invariants
   move. Remove one dependency family at a time.
6. Preserve units, group ordering, canonical identities/digests, deterministic
   iteration order, finite-value checks and transaction failure semantics.
7. Review the final diff and report the exact checks performed and omissions.

## Review validation record

- Frontend `npm test`: **81 tests passed across 13 files**.
- Frontend `npm run build`: **passed**, with the bundle-size warning described in 14.
- Initial standard .NET script attempts were blocked by sandbox access to the
  installed Windows SDK location; an approved retry outside that sandbox was used.
- `tools/Test-DotNet.ps1`: **162 tests passed** — Core 105, Game 31, Browser 26;
  no failed or skipped tests. The initial `Test-Browser.ps1` attempt stopped at
  the SDK access error; its Browser suite, Vitest and build stages subsequently
  passed through the separate invocations recorded here.
- No fresh WASM publish, deployed smoke, performance campaign or human playtest
  was run. Source findings and arithmetic examples are not presented as measured
  deployment failures or numerical calibration conclusions.

## Implementation validation — first correctness slice

- `tools/Test-DotNet.ps1 -Suite Game`: 34 passed; final terminal/debug-guard
  regression rerun: 2 passed.
- `tools/Test-Browser.ps1`: 27 Browser tests and 85 frontend tests passed;
  production frontend build passed, retaining the existing large-chunk warning.
- Fresh Release WASM publish passed on retry after a transient linker access
  violation. This local publish did not enable AOT; Newtonsoft trim warnings remain.
- Local browser smoke passed against that fresh WASM build: direct refuel,
  applied target, Designer/geometry flow, history reset, and first post-reset
  fuel impact. Screenshots captured at 1600, 1280 and 720 widths; the 1280 Studio
  screenshot was visually inspected. No console/page errors were reported.
- Final diff whitespace check passed. Existing unrelated working-tree changes
  were retained. No deployment or reproduction-matrix benchmark was performed.
- Paused after the correctness slice. The following requested slice addresses
  worker recovery (08) and CI gates (24); scoring breakdown/provenance remains
  pending under 04.


## Implementation — worker recovery and release gates (08, 24)

- Worker startup and initialization each have a configurable 120-second ceiling;
  dispatched commands and resync reads have a 180-second ceiling. These generous
  defaults account for the measured ~3.7-second 60x solve in
  `physics/runtime-profile.md`, cold loading, and slower devices. They are failure
  ceilings, not performance targets or measured mobile guarantees. Browser timer
  throttling can delay detection in background tabs.
- Timeout, malformed responses, deserialization errors and worker exceptions end
  the session, terminate the worker, clear timers and reject active/queued work.
  Late replies cannot revive it. Orders are never automatically retried because
  their result may already have committed.
- A native recovery action appears on every view, explains progress loss, and
  reloads only on explicit player action. Launcher reset errors are caught.
- Pull requests validate without deployment secrets. Core and Game tests run once;
  Test-Browser runs Browser tests, Vitest and the frontend build. Release AOT WASM
  is published and locally smoke-tested before uploading the validated artifact.
  The main-only deploy job requires successful validation and downloads that artifact.
- Main workflow runs are serialized through stable-alias smoke and the commit-SHA
  reproduction benchmark. Logs and browser captures are retained as artifacts.
  This serialization covers this workflow; external/manual deployments remain
  outside its control. The post-deploy benchmark still verifies the expected SHA.
- Hosted intentionally-failing/green workflow runs remain an acceptance follow-up;
  this local slice does not push a validation branch or deploy production.

### Validation for this slice

- `tools/Test-Browser.ps1`: 27 Browser .NET tests and 94 frontend tests passed;
  production build passed with the existing large-chunk warning.
- Workflow YAML parsed successfully; PR trigger, validation prerequisite,
  no validation-job secrets and non-cancelling concurrency were checked locally.
- JavaScript syntax checks passed for smoke and recovery scripts.
- No shared C# source changed in this slice. Local browser verification uses the
  previous slice's staged Release non-AOT WASM. The new CI AOT pipeline, hosted
  failing/green runs and production reproduction matrix are not locally verified.

- Local production-build smoke passed, including refuelling, target feedback,
  Designer return, history reset, responsive layouts and forced startup failure
  followed by explicit reload into a working authoritative session. Normal-flow
  console/page errors: none. Captures are in `tmp/reliability-slice-smoke`.
- Recovery screenshot visually checked; the progress-loss explanation and native
  reload button are visible and readable. Final diff whitespace check passed.
- Paused after this requested slice. No deployment, commit or later plan task
  was started. Task 24's hosted CI acceptance follow-up remains outstanding.


## Implementation — scoring explanation and policy metadata (04 follow-up)

- Added an immutable last-operation breakdown from actual discharged fuel in
  Game. Reward and positive fresh-fuel cost are separate; the net still uses the
  existing per-bundle sum. No scoring weights or physics changed.
- Full/compact snapshots publish the policy ID and nullable last breakdown.
  Accepted moves replace it; rejected commands and advances retain it; new runs
  start with null. Studio shows authoritative components with the move response.
- Replay digests now include the policy ID. Historical digest values therefore
  change intentionally; the protocol documentation records this compatibility
  boundary. Older v2 hosts without metadata remain readable.
- This is the only implementation unit in this turn. Task 05 (visible objective
  and shift ending) is next; challenges, balance tuning and fuel animation remain
  untouched.

Validation:

- `tools/Test-DotNet.ps1 -Suite Game`: 34 passed.
- `tools/Test-Browser.ps1`: 27 Browser tests, 96 frontend tests, and the production
  build passed. Existing large-chunk warning remains.
- Fresh Release non-AOT WASM publication passed. Existing Newtonsoft trim warning
  remains. No production deployment or AOT/reproduction benchmark was run.
- Regression checks cover mixed capped discharge, negative fresh-fuel reversal,
  rejected-operation retention, initial null state, full/compact agreement and
  frontend rendering of supplied components rather than a second scoring formula.


## Implementation — visible shift objective and ending (05)

- Added Game-owned immutable shift progress/results, serialized in full and compact
  v2 snapshots. Free practice keeps 30 days, 128 bundles and existing scoring.
- Added optional `useful-fuel-day-v1`: a paused 24-hour challenge, same fuel stock,
  eight actual discharged bundles at >= 6 MWd/kg, and horizon completion with
  nonterminal RRS. Reward is a run-local Efficient refueller badge; no score bonus,
  new weights, scripted target or mandatory fuel move was introduced.
- Game accumulates accepted fission-energy increments with burnup transactions;
  assumed startup exposure, paused time and rejected transactions contribute no
  delivered energy. Electrical output remains an estimate at the authored ratio.
  Actual accepted refuelling accumulates fuel consumed, useful discharge, reward
  and cost. Reports separate operating points from discharge/fuel-cost points.
- Studio displays the objective, seed, resource/time remaining and reward before
  challenge play. Countdown uses elapsed duration rather than day numbering.
  The ending card explains success/missed/early outcomes and shows cumulative
  energy, fuel and score components, with same-seed and next-seed retry controls.
- Reset retains the objective/seed when omitted; switching objective is explicit.
  Invalid IDs reject atomically. Older v2 hosts without objective metadata remain
  readable. Reports/badges are not persisted across new runs.
- Browser automation exercises real direction/shift choices, complete successful
  and empty challenge days, seed retries, practice return and responsive reports.
  Players should inspect both axial ends; average channel burnup does not predict
  discharge quality in a selected direction.

Validation:

- `tools/Test-DotNet.ps1 -Suite Game`: 40 passed.
- `tools/Test-Browser.ps1`: 28 Browser tests, 99 frontend tests and production build
  passed. Existing large-chunk warning remains.
- Fresh Release non-AOT WASM publication passed; existing Newtonsoft trim warning
  remains. No deployment or AOT/reproduction benchmark was performed.
- Human playtests and balance tuning remain follow-ups. No human-completion or
  restart-time measurements are claimed. Task 06 (balance) and fuel animation
  have not been started.

- Local production-shaped smoke passed with the fresh WASM: successful challenge
  (8 useful bundles, badge earned), empty retry marked missed, same/next seed
  controls, practice return, history/Designer/recovery flows and responsive layouts.
  Normal-flow console/page errors: none. Captures are in `tmp/shift-smoke`;
  desktop and mobile ending cards were visually inspected. Final whitespace and
  smoke-script syntax checks passed.


## Implementation — score balance (06)

- Compared four authoritative policies on seeds 1001, 1002 and 1013 over the
  same 24-hour target-100% challenge: no refuels, highest burnup/default direction,
  repeated fresh reversal, and considered discharge using Core inventory candidates.
  Recorded duration, energy, inventory/useful discharge, reserve, outcomes and all
  score components. Timing and strategy decisions are based on simulation time.
- Baseline showed idle seed 1002 beating the successful considered strategy.
  Reduced operating maximum from 1,800 to 1 point per simulated hour; preserved
  the existing 70/30 power/tilt quality share, quality thresholds, per-bundle
  discharge rule, fuel cost and all physics. Named policy is now
  `practice-fuel-and-operation-v2`; replay metadata includes the new identity.
- All three seeds now rank considered > default direction > idle > fresh reversal.
  Baseline/tuned checks retain exact recorded physical metrics and outcome equality;
  operating contribution matches the authored 1/1,800 conversion within 1e-9.
- Retained 24-hour duration and 128 stock after these bounded comparisons: considered
  policies reached the existing objective using 16 fresh bundles. No acceptance
  bonus, hidden target, altered physical tolerance or simulation rule in JS.
- Studio shows fractional points; the existing ending card already presents
  operating, discharge and fuel-cost components. Historical v1 wire metadata
  remains readable, but totals/replay results are not comparable across policies.
- Added a maintained `tools/GameplayBalanceBenchmark` and committed before/after
  results in `benchmarks/gameplay-balance-v2.json`. Rationale, exact seed/policy
  choices and reproduction commands: `docs/gameplay/score-balance.md`.

Validation:

- Game suite: 42 passed, including short same-seed policy ranking and operating
  time/quality/cap checks. Browser suite: 28 passed; frontend: 99 passed; production
  build passed. Existing bundle-size warning remains.
- Fresh Release non-AOT WASM publication passed with the existing Newtonsoft trim
  warning. No physics source changed in this slice; no deployment or AOT benchmark.
- Human completion/restart observations remain task 05's acceptance follow-up;
  this is an initial authored balance pass over the reported policies/seeds.
  Task 07 (fuel movement explanation) is next, not started here.

- Production-shaped browser smoke passed against the freshly published WASM:
  refuelling, history, Designer/layout return, successful and missed challenge
  days, same/new-seed retries, practice return and recovery. Normal-flow console
  and page errors: none. Reports at 1600 and 720 pixels were visually inspected;
  captures are in `tmp/balance-smoke`. Final diff whitespace checks passed.

### Task 07 — movement explanation completed (2026-10-02)

Core owns one immutable four/eight position map for both directions, now consumed
by execution and Game presentation. Studio marks outgoing positions/burnup and
names the incoming/outgoing ends before an order. Game retains confirmed bundle
identities, before/after positions, discharged burnup and authoritative score.
The confirmed summary survives Designer navigation; accepted moves animate only
once, and reduced-motion users receive the static summary. Rejections never
animate success. The bridge's omitted-null position encoding has a regression.

Validation: 105 Core tests, 42 existing Game tests plus four focused movement
cases, 29 Browser tests and 101 frontend tests passed; Release WASM publication
and production build passed. Production browser smoke passed refuelling,
Designer preservation, challenge success/miss/retries and recovery at desktop
and 720px widths. Existing chunk-size and Newtonsoft trimming warnings remain.
No deployment or human playtest is claimed. Captures: `tmp/movement-smoke`.

### Task 10 — useful candidate eligibility completed (2026-10-02)

Game publishes geometry eligibility and the same nonfuel reason used to reject
an order. Studio calls the inspection helper Highest burnup, excludes ineligible
channels with deterministic ties, and explains a selected nonfuel channel before
dispatch. Candidate rows show observed local power; selected orders and candidate
titles show the actual absorber zones' drain/fill headroom. Copy distinguishes
burnup from age and does not promise a reactor response or optimal strategy.
Stock and terminal-state constraints remain separate from geometry eligibility.

Validation: 47 Game, 29 Browser and 102 frontend tests passed, plus fresh Release
WASM and production build. Focused real-WASM smoke edited/restored fuel through
Designer, checked candidate removal and disabled orders, then confirmed an eight
bundle move at 1600px and 720px, with no browser errors. The harness waits for the
pending edit before returning; captures: `tmp/eligibility-smoke`.

### Task 11 — controller decision explanations completed (2026-10-02)

Core records branch-based decision codes for balanced/retained fills, accepted
first/second corrections, retaining the first move after testing a worse second
correction, reached physical/event bounds, and empty/full exhaustion. Metadata
survives clock-only copies; numerical candidate selection and physical digest
semantics are unchanged. Game maps reasons to concise measured explanations.
Studio highlights the zone with least headroom, exposes drain/fill room, and
reports accepted baseline/final controller residuals without claiming an
uncomputed response. Older hosts show explanation unavailable rather than
inventing a reason from a numerical fill delta.

Validation: 113 Core tests plus the final retained-correction case (nine reason
cases passed together), 47 Game, 29 Browser and 104 frontend tests passed.
Fresh Release WASM, production build and focused real-WASM browser smoke passed
at 1600px/720px with zero browser errors. Captures: `tmp/rrs-feedback-smoke`.

### Task 12 and Phase 2 — implementation completed (2026-10-02)

Game publishes immutable run provenance: standard challenge, free practice, or
modified sandbox. Accepted changes to fuel/reflective faces and zone mapping
retain readable reasons for the rest of the run, including after restoration.
Inspection, equilibrium solves, no-op edits and rejected edits do not mark it.
Accepted developer state changes also mark provenance, while developer grant/reset
commands remain unavailable in ordinary browser routing. Sandbox play retains
score and objective progress but cannot earn the standard badge; consumers have
an explicit standard-challenge eligibility flag for future score comparisons.
Studio labels live/report provenance and reset starts a clean run with the chosen
seed/objective. No engineering lock, cooldown or confirmation workflow was added.

Final validation: **299 automated tests passed** (114 Core, 49 Game, 30 Browser,
106 frontend), using the repository test scripts. One new Browser test initially
expected compact engineering responses; the corrected test verifies their existing
full response plus a subsequent ordinary compact response, and the full gate passed.
Release non-AOT and the exact deployment Release AOT / omitted-precompression
publish both passed, followed by a production frontend build. Full and focused
browser smoke passed on the final AOT artifacts: standard challenge success/badge,
modified challenge goal completion without a standard badge, empty retry/miss,
seed retries, Designer inspection/edit/restoration, eligibility filtering,
confirmed movement persistence, RRS reasons/headroom, history, and recovery.
Desktop 1600/1280 and 720px layouts passed overflow checks; final captures and
zero browser-error log are in `tmp/phase2-aot-smoke`. Existing large-chunk and
Newtonsoft trim warnings remain; no Vercel deployment or hosted CI run is claimed.

Phase 2 tasks 05–07 and 10–12 now have all planned implementation delivered.
Task 05's human measurements remain uncollected and are not replaced by automated
playthroughs. Overall remaining implementation: phase 3 = five tasks, phase 4 =
four tasks, phase 5 = six tasks (15 total), plus task 05/24 acceptance follow-ups.

Quota was checked after each completed task, using remaining percentages:

| Boundary | Five-hour remaining | Weekly remaining |
| --- | --- | --- |
| Start of this Phase 2 continuation | 69% | 65% |
| Task 07 complete | 57% | 63% |
| Task 10 complete | 49% | 61% |
| Task 11 complete | 43% | 61% |
| Task 12 complete, final AOT browser acceptance passed | 31% | 59% |

No reset credit was consumed. Work stopped at the completed Phase 2 boundary.


### Task 09 — Phase 3 started; replay slice completed (2026-10-02)

Removed the live bridge's duplicate, unexposed command/history collections.
`ReplayDigestChain` retains one digest and hashes only that digest plus the current
canonical command. Algorithm `sha256-chained-replay-v2` is explicitly published
in capabilities and every initialization/dispatch response; canonical v1 digests
are not comparable. State-digest algorithms and reactor behavior are unchanged.
There is no artificial command/session cap. Response streams remain the source
of replay evidence: record the successful initialization and every sequence-
advancing response, including dispatched rejections and resets. Chart history
is not replay storage; the digest cannot reconstruct a discarded command stream.
The bridge retains zero diagnostic-history records, rather than an unbounded ring.
The complete recording/reset/rejection contract is in `spec/browser-playtest-protocol-v2.md`.

The solver-free 100/1,000/10,000-command comparison is reproducible with
`tools/ReplayBookkeepingBenchmark`. At 10,000 commands, old bookkeeping took
2,054.809 ms and allocated 8,472,754,232 bytes cumulatively; chained bookkeeping
took 11.672 ms and allocated 19,502,440 bytes. Retained evidence payload fell from
1,038,890 bytes (excluding object/list overhead) to a constant 71 bytes.
Per-command chain time stayed approximately constant. See
`performance/replay-bookkeeping.md` for all measurements and their limits.

Validation: repository `Test-Browser.ps1` passed **32 Browser .NET tests and
106 Vitest tests**, plus the production frontend build. New regressions cover
10,000-command determinism/order, bounded storage, dispatched rejection, early
validation/resync rejection, initialization identity and reset. The exact Release
AOT / omitted-precompression WASM publish passed. Independent Web Crypto hashes
matched that published host's initialization, accepted/rejected commands and
reset, with zero browser errors (`scripts/replay-smoke.mjs`). Its initial harness
attempt assumed main-window exports; the corrected harness loads the published
module in an isolated test realm because the product host runs in its worker.
Full local gameplay smoke passed on the AOT artifacts, including refuelling,
Designer/session preservation, challenge success, sandbox reward exclusion,
seed retries and recovery. Captures: `tmp/phase3-replay-aot-smoke`. Final diff
inspection passed. Existing bundle-size/Newtonsoft trim warnings remain. No
Vercel deployment, human playtest or hosted CI run is claimed.

Phase 3 has four unstarted tasks: **13 accessibility, 14 shell/feature loading,
15 rendering cost, 16 achieved pace**. Across phases 3–5, 14 implementation tasks
remain, plus acceptance follow-ups 05 and 24. This continuation ends at the
completed task 09 boundary with no further implementation slice started.

Quota checked at completion: **94% five-hour remaining, 57% weekly remaining**.
The five-hour window renewed naturally during verification (it began at 30%
remaining); no reset credit was consumed.


### Task 13 — accessible launcher and Designer completed (2026-10-02)

The native launcher replaces canvas-only actions with Begin shift, seed input,
objective selection, Apply and New aged core. It remains a view over the same
session. Designer's native inspector exposes all channels/positions, readable
fuel/burnup/power, fuel and six reflective-face actions, Solve, Zone geometry
and Return. Its existing canvas remains the visual map, hidden from the accessible
control tree. Scene subscriptions/handlers and native views clean up on exit.
Focused native controls remain focused while pending and reject duplicate actions.

The session announcement region deduplicates response objects, errors and terminal
transitions. Accepted operations and dispatched rejections announce once; accepted
clock ticks and continuously changing telemetry do not. Recovery owns connection
failure announcements. The legacy telemetry mirror remains available to diagnostic
smoke tooling with aria-hidden, separate from the live region. Studio's feedback
card is readable without duplicating live announcements. Geometry draft feedback
has its own local region; the global result region moves inside that modal and
returns on close. Connection loss closes geometry and exposes recovery.

Zone geometry has a native channel selector and one roving map Tab stop, with
arrows for channel navigation and Enter/Space activation. Studio arrows are
scoped to channel controls; unrelated controls retain focus and native behavior.
All essential actions fit a minimum 320px viewport with vertical scrolling.
Narrow Studio cards now shrink/wrap and show zones in two rows of seven.

Validation: `Test-Browser.ps1` passed 32 Browser .NET tests, 113 Vitest tests and
production build; the final additional modal-recovery regression passed in the
complete **114-test Vitest run** (146 Browser/frontend tests in total). Existing
Release AOT WASM was reused because no shared simulation/host code changed.
Full local gameplay/recovery smoke passed, including Designer session/history,
standard challenge success, modified reward exclusion and seed retries. New
keyboard-only real-WASM smoke completed seed/objective selection, cell inspection,
fuel/face edits/restoration, solve, geometry draft/undo/apply, return and refuelling.
It confirmed focus retention, one accepted-edit announcement and quiet live ticks.

Layouts/captures were checked at 1280×720, 320px wide, and 200% desktop zoom
emulation (640×360 CSS pixels at DPR 2). This verifies layout/reflow and focus,
not screen-reader audio or a complete manual accessibility audit. The first
narrow check caught Studio overflow and passed after the card/zone-strip fix.
A repeated CDP zoom override required clearing its previous session metrics; the
corrected final zoom run passed. Final captures: `tmp/phase3-accessibility-smoke`;
full smoke captures: `tmp/phase3-accessibility-full-smoke`. Both final runs had
zero browser errors. The installed browser skill CLI is unavailable; repository
Playwright verification provided the browser acceptance path. Final diff check
passed; the existing large-bundle warning remains for task 14. No deployment or
hosted-CI/human acceptance follow-up is claimed.

Phase 3 now has tasks 09 and 13 complete, with **14, 15 and 16** unstarted.
Overall: 13 implementation tasks and acceptance follow-ups 05/24 remain.
This continuation ends at the completed task 13 boundary; no task 14 implementation
was started. No reset credit was consumed.

Quota checked after final task 13 acceptance: **85% five-hour remaining and 98% weekly remaining**. No reset credit was consumed.

### Task 14 — shell loading completed (2026-10-02)

Native AppShell loads Phaser/Designer only on demand and preserves the single controller, history and navigation. Retry/disposal tests, 116 Vitest tests, production build, full gameplay/recovery smoke and keyboard smoke passed. Cold three-run medians and payload distinctions are recorded in `performance/browser-phase3.md`. Main JS is 96.41kB; the deferred Designer remains 1.25MB and its warning is retained. Begin readiness remains dominated by WASM. No deployment claimed. Quota at task boundary: 76% five-hour and 96% weekly remaining; no reset consumed. Tasks 15/16 continue under the whole-phase request.

### Task 15 — rendering completed (2026-10-02)

Measured baseline before optimization with 380 production channels and synthetic 4096-sample history. Pending p50 fell 2.3→0.1ms; chart p50 14.4→7.7ms. Status updates avoid snapshot work, watchlist ordering is cached, SVG nodes patched, movement summaries retained and display-only chart reduction preserves extrema/gaps/step transitions. All observations remain inspectable. Focus/node and reducer tests passed; full production smoke passed including corrected visible Designer canvas. Measurement conditions and paint/solver timing limits are in `performance/browser-phase3.md`. Quota at boundary: 72% five-hour, 96% weekly remaining. No reset consumed. Task 16 continues.

### Task 16 and whole Phase 3 — completed (2026-10-02)

Studio separates requested speed from achieved simulation minutes/real second,
including solver/foreground waits. The presentation tracker is bounded, resets
across pause/speed/visibility/navigation/reset/terminal transitions and never
drives the simulation clock. Quiet solving status leaves pause available during
clock requests. Existing one-request/one-quantum scheduler behavior is retained.
No Core rules, numerical tolerances, solver cadence or WASM artifacts changed.

Production worker benchmark: 20 1x ticks, six each at 10x/60x, three refuels/shape
solves, plus real scheduler queued-pause/refuel checks. Final comparable run had
no concurrent build/test/benchmark workloads. Tick medians 11.0/191.4/847.6ms,
refuel 253.4ms, shape solve 1787.3ms; all commands accepted. Queued pause 806ms,
no overtaking tick, following refuel 211.5ms. The scheduler measured 198.3 sim
minutes/real second at requested 60x. Machine-specific review budgets and timing
limits are in `performance/browser-phase3.md` and
`../benchmarks/browser-latency-budgets.json`. They are not new gameplay gates.

`Test-Browser.ps1` passed **32 Browser .NET tests and 123 Vitest tests** plus
production build. The final added Designer-start recovery regression passed in
the complete **124-test frontend run** (156 Browser/frontend tests in total),
followed by a successful production build. Focused tracker/controller tests
cover solver-wait measurement, status updates and lifecycle resets. The frontend
uses the previously verified Release AOT deployment artifacts unchanged.

Full local production gameplay/recovery smoke passed: refuelling, visible
Designer canvas, geometry, session/history preservation, challenge success,
sandbox exclusion and seeded retries. Keyboard real-WASM playthrough passed,
including observed pace, quiet telemetry, focus, 320px and zoom-emulated layouts.
Visual inspection caught desktop flex-column wrapping of the added pace label;
the correction passed dedicated real-60x checks at 320/640/720/1280/1600px,
including card containment, enabled foreground pause and rate reset. Captures:
`tmp/phase3-final-smoke`, `tmp/phase3-final-keyboard`, `tmp/phase3-pace-layout`.
Final diff inspection passed. Deferred Designer still produces Vite's large-chunk
warning; the warning limit was not raised. No deployment, hosted-CI or human
acceptance follow-up is claimed.

Phase 3 is finished with no partial implementation slice left. Next phase is
17–20. Quota checked after task 16: **63% five-hour remaining, 94% weekly
remaining**; no reset credit consumed.

### Task 17 — Game boundaries implemented (2026-10-02)

PracticeRunClock replaces the active scored scenario engine, retaining queued-target
control ticks, pause, outcome IDs and wall-time semantics. Named scoring,
presentation and geometry components leave GameSession as the public command
boundary. Detached physical accumulation freezes into an immutable candidate with
fuel, poison, projection, RRS, clock and score before validation/commit. Legacy
identifier renaming is deliberately deferred to its mechanical cleanup slice.
The two-seed corpus preserved all 24 complete response byte hashes. Game checks
passed 52 tests including partial ticks, capacity, stale/foreign candidates and
terminal timing. Shared Core/Browser and rebuilt WASM acceptance follow at the
whole-phase boundary. Task 18 is next; no reset credit consumed.

### Task 18 — equilibrium boundary implemented (2026-10-02)

Game and Browser now expose/cache the existing immutable equilibrium projection;
the solver no longer allocates IQS/adjoint compatibility objects. The exact
consumer manifest and preserved units/group/caching invariants are recorded in
`physics/equilibrium-presentation-boundary.md`. IQS candidate/solver are retained
in `ReactorSim.Core.Research`, explicitly referenced by Core research tests and
excluded from the Game/Browser/Host dependency graph. Shared metadata pack APIs
remain until task 21 classifies their adjoint/diffusion consumers. Build passed
with zero warnings; all 24 baseline response hashes remain byte-identical.
The complete shared suites are running; whole-phase acceptance is pending.

### Task 19 — bridge modules implemented (2026-10-02)

PlaytestRuntime owns independent sessions, locks, replay/cache/counters and
experiment state; PlaytestBridgeV2 is a thin default-runtime export adapter.
DTOs, mapping, wire readers, digest policy and experiment hosting are separate
private partial implementation modules. Stateless helpers remain static; module
boundaries preserve the same dispatch order and generated JSON serialization.
Six shared compressed fixtures replay exact response bytes in C# and use the
strict production TS parser for full/compact/replacement/failure/resync/reset.
Independent-runtime tests cover state/cache/counter isolation. The Node fixture
test lives outside production TS sources, avoiding an unnecessary Node typing
runtime dependency. Frontend fixture/build passed; all 24 original corpus hashes
are unchanged. Browser suite acceptance is running at the phase boundary.

### Task 20 and whole Phase 4 — completed (2026-10-02)

Studio now composes native map, order/movement and status/control view boundaries.
Typed Studio/Designer navigation replaces generic record storage. The optional
Designer adapter receives the shared session explicitly; its active path no
longer reads the global singleton. The shell/session owns subscription lifetimes,
while passive DOM components have no independent listeners. The shared controller
retains a bounded presentation-only response/impact summary; accepted reset clears
it. Five-cycle DOM tests check exactly one Studio subscriber, zero while disposed,
chosen direction/size/map/tab, retained history/feedback and return focus.

Final checks: `Test-DotNet.ps1` passed **114 Core and 52 Game tests**, including the
retained research target and deterministic rollback/poison/normalization coverage.
The final `Test-Browser.ps1` passed **34 Browser .NET tests and 127 frontend tests**
plus production build (**327 Core/Game/Browser/frontend tests**). Two new bridge
tests replay six shared wire fixtures byte-for-byte and isolate independent
runtime state/cache/counters. All **24 original pre-refactor response hashes**
remain unchanged; the original baseline is saved under
`../benchmarks/phase4-contract-baseline.json`. No protocol version, canonical bytes,
units, numerical tolerances, authored packs or solver cadence were changed.

Release AOT WASM was rebuilt and staged into the runnable browser product.
Full local production smoke passed refuelling, geometry, challenge success,
sandbox reward exclusion, seeded retries and **three repeated Designer returns**
with retained impact/draft/tab/focus. Keyboard real-WASM smoke passed launcher,
seed/objective, inspection, fuel/faces/solve/zones/refuel, quiet announcements and
focus. Layouts passed at 320px and 200% zoom emulation, plus live 60x pace checks
at 320/640/720/1280/1600px. Browser error counts were zero. Captures are in
`tmp/phase4-full-smoke`, `tmp/phase4-keyboard` and `tmp/phase4-pace-layout`.
Desktop and narrow captures were inspected. Final diff/whitespace checks passed;
the solution registration was reduced to the research project entries without
unrelated platform configuration changes. The existing deferred-Phaser chunk
warning remains visible. No deployment, human playtest or hosted-CI acceptance
is claimed.

Quota checks during the phase: task 17 **49% five-hour / 92% weekly**, task 18
**47% / 92%**, task 19 **44% / 91%**. No reset credit consumed. Task 20 final quota
is recorded below. Phase 5 is left wholly unstarted.

Quota checked after final task 20 acceptance: **36% five-hour remaining and 90% weekly remaining**. No reset credit consumed.

### Phase 5 started — bounded cleanup (2026-10-02)

Task 22 removed only consumer-checked unused helpers/compatibility options and
metadata/probe aliases; active serialization fixtures remain. Its acceptance run
passed 34 Browser tests, 126 frontend tests and production build. Task 25 removed
the empty Golden project/solution entries; all 31 unique copied fixtures remain
with checked consumers in `maintenance/golden-fixture-consumers.json`. The solution
build passed with zero warnings/errors. Quota: task 25 boundary **32% five-hour /
89% weekly**; task 22 acceptance **28% / 89%**. No reset credit consumed.
Task 23 experiment isolation and task 27 current-document audit are complete.
Tasks 21/26 remain unstarted until their larger consumer/tool audit can be completed.

Task 23 now excludes GPU fixture types, WGSL, modules and exports from normal
builds using an explicit research option. Default Release AOT and optional
research Release publishes succeeded. Publish audit measured **178,666 fewer
bytes** in the combined default cleanup (precompressed copies excluded), with
both GPU JavaScript modules absent. Research non-AOT size is not comparable.
No shared numerical equations, budgets or reduction order changed.

Final default checks passed **109 Core + 52 Game + 33 Browser + 126 frontend =
320 tests**, plus production build. Research Core passed 114 tests, isolated
research Browser passed three checks, and GPU JavaScript passed five checks.
Actual WASM boundary smoke verified absent default GPU/profiling exports,
present optional fixture generation, 4,560 nodes and unchanged session state.
Full local production smoke passed play, Designer, zones, challenge/sandbox and
retry paths. Evidence: `benchmarks/phase5-publish-boundary.json` and
`docs/maintenance/phase5-cleanup.md`; captures: `tmp/phase5-full-smoke`.

Task 27 updated current architecture, acceptance, score, pace, terminal and
modified-run documentation, and indexed historical research. Current local
links and contributor production walkthrough passed. Task 21/26 remain larger
consumer/tool audits; they were not partially started. Task 23 boundary quota:
**23% five-hour / 88% weekly remaining**. No reset credit consumed.

Final task 27 boundary quota: **22% five-hour / 88% weekly remaining**. No reset credit consumed.

### Task 21 complete — consumer-led Core/research separation (2026-10-02)

The checked manifest covers **161 declarations** and their pre/current consumers,
including peer declarations in the same source file. Twenty-three legacy files
and 133 declarations (125 public) moved to `ReactorSim.Core.Research`, preserving
namespaces/signatures. Shared complete-state digest/codec and canonical numeric
validation were extracted intact into Core. Bundle state, spatial xenon and
metadata primitives retain their checked Core consumers. No public type, data
artifact or serializer was deleted; research archive APIs remain indexed and
optional. Newtonsoft remains required by the active diffusion parser and shared
IQS/adjoint metadata parser.

All **161 type bodies** match recorded HEAD after newline normalization; all
**24 original browser response hashes** remain unchanged. Exact evidence is in
`benchmarks/task21-type-preservation.json`, with the type-level manifest and
coverage decisions in `docs/maintenance/core-research-boundary.md` and
`core-family-consumers.json`. `python tools/Audit-CoreFamilies.py` refreshes
consumer paths, classifications and hashes.

Solution build passed with zero warnings/errors. All **16 tool/benchmark projects**
built successfully, including projects outside the solution. Standard checks
passed **109 Core, 52 Game, 33 Browser and 126 frontend tests** plus production
build; the new isolated architecture test passed (**321 distinct default checks**
including that test). Research-enabled Core passed **115 tests**. Existing SI,
identity/ownership conservation, canonical state, ordered groups, time/replay and
atomic rejection coverage remains executed through the retained research APIs.

Release AOT WASM rebuilt/staged successfully with threads/profiling/research
exports disabled and no research assembly in the publish. Full local browser
smoke passed direct refuelling, Designer state retention, geometry, challenge,
modified rewards and seeded retries; captures are `tmp/task21-full-smoke`.
Diff/whitespace and documentation links passed. No deployment or human/hosted-CI
acceptance is claimed. Task 26 remains the broader tool/data inventory, not an
unfinished part of this separation.

Quota after implementation boundary: **19% five-hour / 87% weekly**; final
acceptance boundary: **15% five-hour / 87% weekly remaining**. No reset consumed.

### Task 26 complete — supported tools and canonical pack (2026-10-02)

The refreshable inventory covers **58 tools/scripts and 101 data/reference/
embedded artifacts**, each with owner, purpose/inputs/outputs where applicable,
filename consumers, hashes and keep/archive decision. Seven maintained native
invocations ran successfully: aged-core, single-solve, CPU rows, gameplay report
comparison, replay bookkeeping, wire corpus and the legacy static solver.
Gameplay comparison now accepts the tracked combined baseline/tuned report,
preserving array-only report support. All 24 corpus response hashes remain in
the verified command output. The old P9 profile mode is explicitly retired with
a replacement diagnostic; the static mode passes with positive iteration counts.

Nine historical executables were built and their usage interface invoked with
expected exit 1/2. Thirty-three Python/JavaScript syntax checks and nine PowerShell
parses passed. Numerical reruns are distinguished from interface/syntax checks;
external DRAGON/DONJON workflows were not executed. Historical maps, decks,
authority generators and manifests are **archived in place**, with individual
consumer/owner records and preserved original paths/digest bindings. No artifact
was deleted merely because its version looked old, and no data bytes changed.

Canonical diffusion source is `data/packs`; `Sync-PhysicsPacks.ps1` checks or
stages its embedded mirror. Core builds now enforce equality. Isolated negative
and positive target checks proved stale bytes are rejected and equal bytes pass;
normal solution build passed with zero warnings/errors. This is copy consistency,
not source-validation admission; runtime remains embedded and independent.

`Test-DotNet.ps1 -Suite Core` passed **110 tests**. `Test-Browser.ps1` passed
**33 bridge and 126 frontend tests**, plus production build. Full browser smoke
and complete-command benchmark passed with no console/page errors. Game logic
was unchanged; its prior task 21 suite remains the latest Game run. Documentation
links, inventory ownership/paths and diff/whitespace checks passed.
Evidence: `docs/maintenance/research-tools.md`, `research-inventory.json` and
`benchmarks/task26-verification.json`; captures/logs are under `tmp/task26-*`.
No deployment, human-playtest or hosted-CI acceptance is claimed.

Final task 26 quota: **7% five-hour / 85% weekly remaining**. No reset consumed.
