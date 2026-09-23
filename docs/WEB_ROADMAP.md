# CANDU web playtest roadmap

Status: sole active work plan. This roadmap supersedes the historical planning
and review documents removed in this cleanup. `README.md` and
`docs/IMPLEMENTATION_GUIDE.md` remain product and architecture references;
they point here for ordered work.

## Product readout

`web/candu-playtest` is a real Phaser 3 client over the authoritative
worker-hosted C# WASM bridge. The deployed site reached `CANDU play mode
online. 380 channels available.` and accepted a refuelling command. The
underlying 380-channel × 12-bundle deterministic equilibrium path mutates
inventory, re-solves spatial shape, and exposes RRS data. It is not yet a
highly functional player product: the current acceptance gap is between
implemented Core/Game/Browser state and what the deployed Phaser surface makes
visible and behaviorally meaningful.

The compact production reproduction from the 2026-09-13 triage used channel
210, `toward-end-a`, four `NAT-U-SYNTHETIC` bundles. It reported 5.99 MWd/kg HM
average discharged burnup:

| Observable | Before | After | Assessment |
|---|---:|---:|---|
| Fresh bundles | 128 | 124 | Authoritative inventory mutation works. |
| Refuelling operations | 0 | 1 | The command is accepted once. |
| Headline power | 100.0% | 100.0% | Whole-core equilibrium is normalized to the requested amplitude. |
| Headline tilt | +0.0% | +0.0% | Old runtime scalar; not the spatially derived signed tilt. |
| Channel 210 local power | ~67.8% | ~72.4% | Local shape response is present. |
| Channel 210 local tilt | ~0% | ~5.13% | The spatial response exists, but direction is discarded today. |
| All 14 zone fills | 58% | 58% | The Phaser view has no 14-zone display. |
| Applied RRS commands | prior state | 0 in every zone | Verification rejected the candidate and retained baseline fills. |

The largest absolute zonal shape error rose from about 0.0288% to 0.0888% of
total power, yet the controller still applied no fill movement. The existing
Game test intentionally accepts this fallback. Tests currently prove
determinism, atomicity, bridge parity, payload shape, and state mutation; they
do not prove signed tilt, sensible RRS effort, causal before/after feedback,
responsive accessibility, or deployed visual usability. The stable deployment
also reports commit `ba1362e5925dfa75a922012e190114b83074a832`, which was not
resolvable in the audited local refs. This checkout currently has no Git
remote configured, so deployment traceability cannot be repaired from local
state alone.

The current local non-AOT representative refuel takes about 1.88 s and returns
about 1.42 MB of payload. The browser build warns about an approximately
1.28 MB minified main chunk. The client is a fixed 1600×900 Phaser canvas using
`FIT`; visuals are procedural primitives and text, with no product-authored
image, animation, or audio assets. Observed presentation defects include tiny
labels, bundle labels that round to `0.0k`, result copy overlapping controls,
and no visible RRS/impact surface. These are product evidence, not reasons to
invent a second simulation.

## Gameplay and physics stance

Regulated whole-core power staying at 100% is physically honest for the
current steady-state equilibrium model, but it is poor feedback for a game.
The solver normalizes the shape to the requested amplitude, so refuelling can
change local shape, reactivity, tilt, and control demand without creating a
whole-core power excursion. The game must not fake a transient with a
Phaser-only animation or invented number. It should instead make the
authoritative response legible through:

- actual RRS control effort: per-zone applied fill command, bounds/saturation,
  and movement cost;
- a signed spatial tilt derived from authoritative node power, with End A/End B
  direction preserved;
- shape error and two-sided RRS reserve (distance from the 0%/100% fill
  boundaries); and
- explicit before/after deltas for whole-core power, signed whole-core and
  local tilt, local shape/power, reactivity, RRS state/effort, discharged
  burnup, fresh inventory, and score.

If a response is an equilibrium delta, label it as such. A zero control
command is valid only when the authoritative controller explains it as a
dead-band, uncontrollable response, or rejected candidate; it must not be
silently presented as success.

The project-authored
`candu6-two-group-diffusion-v1-infinite-cell-calibrated` pack remains the
active simulation and acceptance path. DRAGON5/DONJON5 material remains
optional offline reference context and does not gate implementation, testing,
or acceptance. Core owns all rules and state transitions;
Game owns session orchestration and immutable presentation snapshots; the
browser bridge serializes those snapshots; Phaser consumes them without
reimplementing physics.

Target desktop readability at 1600×900 and 1280×720 first. Make no mobile
promise until a separately resourced responsive design exists. Unity remains
frozen for feature, input, gameplay, and polish work; no slice below includes
Unity files.

## Completion stages

Every feature slice is complete only after this chain, even when an earlier
stage needs no change:

1. **Core** — define the deterministic rule/contract, units, ordering,
   provenance, bounds, and failure behavior; add the smallest focused Core
   test.
2. **Game** — orchestrate the rule in `GameSession` and publish an immutable
   snapshot/result; prove atomicity and replay behavior in Game tests.
3. **Browser serialization** — carry the same value through the versioned
   bridge and TypeScript protocol, including compact responses and null/finiteness
   rules; prove parity and malformed-input behavior.
4. **Phaser consumption** — render and dispatch the authoritative value in the
   live scene; keep formatting and interaction logic presentation-only.
5. **Deployed visible/behavioral validation** — production-shaped/AOT build and
   stable deployment show the value and the expected cause/effect at both target
   desktop sizes, with no console errors and a reproducible command trace.

Each numbered slice is independently committable. Do not combine a later slice
to hide a failed earlier stage.

## Ordered slices

### 1. Reconcile deployment provenance and build a reproduction matrix

**Scope.** Resolve the deployed SHA against the authoritative remote branch;
verify that the deployed bridge, frontend, protocol, data-pack, solver, and
topology identities match checked-in source. Extend the existing benchmark and
smoke path to emit a compact matrix for cold/warm initialization and refuelling:
channel 210 plus central/peripheral channels, both directions, four/eight
bundles, paused/live time, before/after snapshots, digests, console errors,
WASM time, parse time, and UTF-8 payload bytes. Keep results in existing CI or
benchmark output, not a new task/gate report.

**Likely files.** `.github/workflows/deploy-candu-playtest.yml`,
`web/candu-playtest/vercel.json`, `tools/Build-BrowserWasm.ps1`,
`tools/Test-Browser.ps1`, `web/candu-playtest/scripts/benchmark-wasm.mjs`,
`web/candu-playtest/scripts/smoke.mjs`,
`web/candu-playtest/src/transportMetrics.test.ts`, and
`tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs`.

**Acceptance.** The `ba1362e5...` deployment identity resolves to a reachable
source commit or is explicitly held from acceptance. Local production-shaped
and stable deployment runs reproduce the command matrix, `380` channels,
protocol/data/solver identities, deterministic digests, and the observed
ch210 deltas within documented tolerances. A clock freeze is either reproduced
and isolated or cleared with evidence.

**Tests.** Existing bridge tests, `npm test`, `npm run build`,
`npm run smoke`, and `node scripts/benchmark-wasm.mjs` against local and stable
URLs; inspect console and page errors.

**Stop conditions.** Stop if the deployed source cannot be reconciled, if
production differs from the checked-in authority, or if measurement requires a
browser-side simulator. Do not tune physics or UI against an unknown build.

### 2. Define and publish the signed spatial tilt contract

**Scope.** Make one documented surrogate authoritative. Proposed v1: for bundle
position `p` in `0..11`, `z(p) = 1 - 2p/11` with End A positive and End B negative;
compute `sum(power_i*z(p_i))/sum(power_i)` from authoritative spatial node
power for the full core and selected channel. Publish dimensionless fraction and
formatted percent, preserve sign, and never apply `Math.Abs`. State clearly
that this is a spatial first moment, not a plant detector measurement. Any tilt
target must name and use this same metric or remain explicitly legacy-only.

**Likely files.**
`src/ReactorSim.Core/Domain/EquilibriumCoreSolverContracts.cs`,
`src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs`,
`src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs`,
`src/ReactorSim.Game/GameSession.cs`,
`src/ReactorSim.Game/CorePresentationContracts.cs`,
`src/ReactorSim.Browser/PlaytestProtocol.cs`,
`web/candu-playtest/src/protocol.ts`,
`web/candu-playtest/src/projection.ts`, and
`web/candu-playtest/src/scenes/OperationsScene.ts`.

**Acceptance.** Symmetric manufactured power is zero; reversing axial power
  reverses the sign; the result is finite and bounded; ch210 End A/End B traces
  have the documented direction; full-core and local tilt no longer read the
  old absolute runtime scalar; browser and Phaser retain the sign.

**Tests.** `tests/ReactorSim.Core.Tests/SpatialSolveTests.cs`,
`tests/ReactorSim.Core.Tests/BurnupAndPowerTests.cs`, focused Game tilt/replay
tests beside `PracticeRefuellingCampaignTests.cs`, and bridge/TypeScript parity
tests.

**Stop conditions.** Stop if the sign cannot be demonstrated with a
manufactured fixture, if a second tilt authority is required, or if the only
way to show a response is an invented transient.

### 3. Diagnose and bound the RRS controller

**Scope.** Use the reproduction matrix to explain why the 14-variable Jacobian
candidate is rejected: inspect sign, scaling, target definition, residual
weighting, fill bounds, and full-core verification. Then make one deterministic
bounded sensitivity/Jacobian least-squares pass, one verification solve, and at
most one correction explicit. Preserve the accepted baseline atomically on
failure. Expose fill, average/min/max, shape error, applied command,
saturation, and reserve as authoritative diagnostics.

**Likely files.**
`src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs`,
`src/ReactorSim.Core/Domain/StaticAbsorptionOverlayContracts.cs`,
`src/ReactorSim.Core/Domain/EquilibriumCoreSolverContracts.cs`,
`src/ReactorSim.Game/GameSession.cs`,
`src/ReactorSim.Game/CorePresentationContracts.cs`,
`tests/ReactorSim.Core.Tests/PracticeLiquidZoneRrsTests.cs`,
`tests/ReactorSim.Game.Tests/LiquidZoneRrsGameSessionTests.cs`, and
`tests/ReactorSim.Game.Tests/PracticeRefuellingCampaignTests.cs`.

**Acceptance.** The ch210 zero-command result has an actionable rejection or
dead-band reason. A controllable perturbation produces bounded nonzero effort;
zero remains possible only with an explicit reason. No fill leaves `[0,1]`, no
unbounded retry occurs, and failed/nonconverged candidates retain the previous
state. Replay and partitioned time remain deterministic.

**Tests.** Extend `PracticeLiquidZoneRrsTests`, the existing rejected-
verification Game test, and a central/peripheral, both-direction, 4/8-bundle
matrix fixture. Run focused Core and Game suites plus bridge parity after the
snapshot changes.

**Stop conditions.** Stop if verification still cannot be explained, if the
fix needs transient physics/plant calibration, or if the proposed controller
would become an unconstrained or hidden solver loop.

### 4. Add the authoritative Game impact result

**Scope.** At the Game boundary, publish one immutable accepted-operation impact
with before/after values and delta for power, signed tilt, local shape, `k`/rho,
RRS reserve and effort, shape error, discharged burnup, inventory, operation
count, and score. Keep regulated `100.0% -> 100.0%` explicit when it occurs;
the result must explain the meaningful local/control response without changing
physics for presentation. Rejected operations expose result metadata only and
leave the accepted snapshot unchanged.

**Likely files.** `src/ReactorSim.Game/GameSession.cs`,
`src/ReactorSim.Game/CorePresentationContracts.cs`,
`src/ReactorSim.Game/PracticeGameSessionFactory.cs`,
`tests/ReactorSim.Game.Tests/PracticeRefuellingCampaignTests.cs`,
`tests/ReactorSim.Game.Tests/LiquidZoneRrsGameSessionTests.cs`, and a focused
new `tests/ReactorSim.Game.Tests/RefuellingImpactPresentationTests.cs` if the
existing campaign tests cannot isolate the contract.

**Acceptance.** One accepted refuel yields one deterministic before/after
impact; all values come from the accepted Game state; rejected/stale solves are
atomic; a replay and equivalent time partition produce the same impact and
digest. No field claims transient kinetics.

**Tests.** Focused Game campaign, RRS, atomicity, and replay tests; include the
ch210 reproduction as a trend/invariant, not an undocumented golden.

**Stop conditions.** Stop if the impact needs a Phaser calculation, a guessed
future state, or an additional simulation authority.

### 5. Serialize the full response through the browser boundary

**Scope.** Carry signed tilt, all 14 zone snapshots, RRS diagnostics, impact
before/after deltas, and provenance/solve identity through full and compact
responses. Preserve sequence/patch/resync behavior, finite/null JSON rules,
fail-closed initialization, and WASM-only authority.

**Likely files.** `src/ReactorSim.Browser/PlaytestProtocol.cs`,
`src/ReactorSim.Browser/PlaytestBridge.cs`,
`web/candu-playtest/src/protocol.ts`, `bridge.ts`, `wasmWorker.ts`,
`sessionController.ts`, and `projection.ts`.

**Acceptance.** The browser receives exactly 14 stably ordered zones and the
same signed and impact values as Game; compact responses patch the same state;
malformed/stale/nonfinite payloads fail closed; no compatibility fixture is
reported as authoritative.

**Tests.** `tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs`,
`web/candu-playtest/src/projection.test.ts`, `sessionController.test.ts`,
`transportMetrics.test.ts`, and focused protocol/replay tests.

**Stop conditions.** Stop if serialization drops a field, rounds away its sign
or causal delta, changes digest parity, or requires browser-side rules.

### 6. Make RRS and impact visible in Phaser

**Scope.** Render the 14-zone strip/status, average/min/max fill and reserve,
selected-zone fill/target/measured/error, applied effort and reason, and a
time-bounded impact card. Keep the direct authoritative refuel order visible;
do not resurrect a predictor or modal-only confirmation flow. Render clear
before/after values and equilibrium labels, including flat whole-core power,
local ch210 response, signed tilt, burnup, inventory, and score. Reserve space
so result copy never overlaps controls.

**Likely files.** `web/candu-playtest/src/scenes/OperationsScene.ts` (split into
bounded map, RRS/status, channel-dossier, impact, and control modules),
`web/candu-playtest/src/drawing.ts`, `web/candu-playtest/src/visuals.ts`,
`web/candu-playtest/src/projection.ts`, `web/candu-playtest/src/sessionController.ts`,
and `web/candu-playtest/src/main.ts` only if status/accessibility wiring is
needed.

**Acceptance.** On the deployed ch210 trace an owner can see all 14 zones,
selected-zone diagnostics, control effort/reason, and the local before/after
response; the headline may remain `100%` but is not the only feedback. All
displayed numbers match the bridge snapshot, signed values show direction, and
zero effort is explained. No controls or result copy overlap at 1600×900 or
1280×720.

**Tests.** `projection.test.ts`, `visuals.test.ts`, `sessionController.test.ts`,
`npm test`, and the real-browser `web/candu-playtest/scripts/smoke.mjs` extended
to exercise initialize, advance, refuel, and visible response.

**Stop conditions.** Stop if Phaser must derive physics, hides an unavailable
diagnostic, or needs a new value absent from the Game snapshot.

### 7. Set and protect latency and payload budgets

**Scope.** Measure cold/warm bridge startup, WASM call, parse/materialization,
worker transfer, and refuel response separately on local non-AOT and deployed
AOT. Treat the observed 1.88 s refuel and 1.42 MB response as the current
baseline; use provisional guardrails of 2.0 s local non-AOT and 1.5 MB per full
response, with deployed p95 caps set from Slice 1 measurements. Optimize compact
patches, serialization, and worker scheduling only after measurement.

**Likely files.** `web/candu-playtest/scripts/benchmark-wasm.mjs`,
`web/candu-playtest/scripts/smoke.mjs`, `web/candu-playtest/src/bridge.ts`,
`web/candu-playtest/src/wasmWorker.ts`, `web/candu-playtest/src/transportMetrics.test.ts`,
`src/ReactorSim.Browser/PlaytestBridge.cs`,
`src/ReactorSim.Browser/PlaytestProtocol.cs`, and
`.github/workflows/deploy-candu-playtest.yml`.

**Acceptance.** Baseline and p50/p95 numbers are repeatable on both build
forms; the deployed response stays within ratified budgets; the UI remains
responsive and authoritative fields/digests are unchanged. A budget exception
names its cause and is visible in CI output.

**Tests.** Benchmark trace, transport metrics tests, bridge tests, production
build, and deployed smoke with no console/page errors.

**Stop conditions.** Stop if performance work requires dropping authoritative
state, truncating deltas, changing digest semantics, or adding a client
simulator.

### 8. Tune scoring and pacing around the real bargain

**Scope.** Make score reward stable regulated power without pretending it must
move, signed tilt/shape quality, RRS reserve, economical control effort,
useful discharged burnup, and deliberate inventory use. Show each accepted
operation's score delta and the next actionable goal. Keep pause/resume,
bounded advance, restart, and RRS terminal state deterministic; target a
readable 10–20 minute run at intended acceleration.

**Likely files.** `src/ReactorSim.Core/Domain/Phase8ScoringContracts.cs`,
`src/ReactorSim.Core/Domain/Phase8ScenarioRuntimeContracts.cs`,
`src/ReactorSim.Game/GameSession.cs`,
`src/ReactorSim.Game/PracticeGameSessionFactory.cs`,
`src/ReactorSim.Game/CorePresentationContracts.cs`,
`web/candu-playtest/src/scenes/OperationsScene.ts`,
`web/candu-playtest/src/projection.ts`, and focused Game/browser tests.

**Acceptance.** Replaying the same command/time trace gives the same goals,
score, impact, and digest; a regulated 100% power run can score well; RRS
effort/reserve and useful burnup make refuelling choices matter; RRS game over
is the only run-ending boundary in scope.

**Tests.** Focused Game scoring/pacing and `PracticeRefuellingCampaignTests`,
`web/candu-playtest/src/liveClock.test.ts`, session-controller tests, and a
deployed short-run smoke.

**Stop conditions.** Stop if scoring needs an unmodeled transient, hidden
failure system, or training narrative, or if pacing hides the clock defect.

### 9. Finish desktop accessibility and visual/audio polish

**Scope.** At the two target desktop sizes, establish keyboard focus/selection,
high contrast, text/symbol alternatives to color, readable units and labels,
status-mirror announcements, reduced-motion behavior, and explicit mute/audio
behavior. Then refine the procedural tactical-console hierarchy, selection and
transfer feedback, RRS warnings, and authored sound cues. Any new asset must be
project-authored or lawfully licensed; no copied IP, characters, story, or
narrative scope. Do not promise mobile support.

**Likely files.** `web/candu-playtest/index.html`,
`web/candu-playtest/src/main.ts`, `web/candu-playtest/src/scenes/OperationsScene.ts`,
`web/candu-playtest/src/drawing.ts`, `web/candu-playtest/src/visuals.ts`,
`web/candu-playtest/src/projection.ts`, `web/candu-playtest/src/visuals.test.ts`,
and `web/candu-playtest/public/` only for documented authored audio/media.

**Acceptance.** 1600×900 and 1280×720 captures and playthroughs have no clipped
or overlapping controls, tiny unreadable labels, or misleading `0.0k` bundle
values; all critical state remains understandable without color or sound;
keyboard/reduced-motion paths work; audio is distinct, optional, and within the
latency budget. The deployed page remains a coherent procedural tactical UI.

**Tests.** Extend Vitest helpers, run production build, and run Playwright smoke
at both desktop viewports with console/page-error checks; perform owner-visible
deployed validation for layout, motion, and audio.

**Stop conditions.** Stop if polish hides an authoritative value, exceeds the
latency/payload budget, requires unlicensed assets, or turns mobile into an
unstated acceptance promise.

## Final release gate and exclusions

The web roadmap is complete only when the deployed stable alias passes the
provenance/reproduction matrix, the same accepted command trace matches
Core → Game → Browser serialization → Phaser consumption, and an owner can
visibly inspect the 14-zone RRS/resource state, signed tilt, control effort,
shape/RRS reserve, before/after impact, score, and pacing at both desktop
targets without console errors. Passing unit tests or seeing a changed digest
alone is not completion.

Unity feature development stays frozen. Shutdown, scram, accident progression,
operator-training scenarios, full plant operations, plant-grade safety claims,
and a transient-capable true IQS/photoneutron/Krylov research expansion are not
on this gameplay path. Any future DRAGON5/DONJON5 work remains optional offline
reference work and is outside this roadmap; it is not required for the
project-authored model or web acceptance.
