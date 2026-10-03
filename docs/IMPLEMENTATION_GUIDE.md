# CANDU Refuelling Game implementation guide

## Product direction

Build a responsive web game about keeping a CANDU reactor at useful steady
power through on-power refuelling. The player inspects channels and bundles,
chooses a refuelling operation, watches the authoritative reactor state update,
and learns the relationship between fuel history, power shape, RRS reserve, and
score.

The Vercel-deployed `web/candu-playtest` is the product and primary acceptance
path. The current game is intentionally a deterministic practice model: maintain
RRS reserve away from both 0% and 100%, conserve finite fresh bundles, and earn
score through stable power and useful discharged burnup.

Shutdown, scram, accident progression, operator-training scenarios, full plant
simulation, and plant-grade safety claims are out of scope.

### Understandable refuelling contracts

Core's `GameRefuellingPlanV1` supplies the four/eight position maps used by both
execution and the browser draft strip. Game retains the last immutable
`RefuellingMovement` with bundle identities, nullable old/new positions, discharged
burnup and scoring; no reactor response preview or confirmation gate is added.
The bridge omits null positions, and frontend validation accepts this encoding.
Channel geometry eligibility and nonfuel reasons use the same Game predicate as
the refuelling command. Stock and terminal state remain separate constraints.

Core emits controller branch decision codes without changing candidate selection
or its physical state digest. Game maps those codes to text and selects the zone
with least headroom. Full and compact browser snapshots carry the reason and
measured controller residuals; UI does not infer a reason from a small fill delta.

`RunProvenance` copies immutable modification reasons into every Game snapshot.
Only accepted changes to fuel/faces or zone mapping mark engineering edits;
rejected and no-op edits, inspection and equilibrium solves preserve eligibility.
Developer grants/score resets/actions cleared mark modifications when they change
state, and remain unavailable in normal browser routing. A modified run can meet
the objective but cannot earn the standard badge. Consumers must use
`EligibleForStandardChallenge` for any future standard-challenge bests.

## Shift objectives and endings

Game publishes immutable `ShiftProgress` in full and compact browser snapshots.
Free practice retains its 30-day horizon and existing inventory/scoring. The
optional one-day challenge starts paused, requires eight useful discharged
bundles (>= 6 MWd/kg), and awards a run-local badge only on horizon completion
without terminal RRS exhaustion on an unmodified run. Studio displays this contract rather than
calculating goal eligibility.

Cumulative energy is committed with accepted burnup transactions. Startup fuel
exposure is excluded; rejected/paused advances add no delivered energy. Fuel
consumption, useful discharge and score components update on accepted refuelling.
The ending card uses these totals and supports retaining or incrementing the
seed while preserving the selected objective. Operating reward is capped at one point per simulated hour; discharge rewards
and fuel costs retain the per-bundle rule. See `gameplay/score-balance.md`.

## Current implementation

The browser uses the native DOM/SVG Reactor Studio game interface, with a
native DOM launcher and Designer inspector over a Phaser core map, all using the public
`ReactorSim.Game` session through the versioned `candu-playtest-v2` browser
bridge. The bridge runs in a worker and the client fails closed when the
authoritative WASM module is unavailable.

Starting a run initializes one live 380-channel by 12-position `GameSession`
and opens Reactor Studio. Studio owns pacing, channel inspection, refuelling,
and the power/RRS/score/burnup/inventory feedback from the full-core snapshot.
Core Designer opens from Studio as an engineering view of that same live
session. Its `configure-cell` and `solve` commands pass through the bridge;
successful edits replace the live equilibrium atomically, and returning to
Studio preserves the run clock, inventory, score, and accepted refuelling
state.

Core Designer's **Zone geometry** (`Z`) view consumes authoritative per-node
region IDs, independent absorber compartment IDs, and two absorption slopes.
Its draft can repaint regions, clear absorber cells, and translate a mask across
the lattice and axial positions. `configure-zone-layout` validates the complete
mapping and solves a candidate before committing; rejection preserves physical
state. Accepted edits retain fuel, time, inventory and score, rebuild spatial
references, and use existing zone fills as the regulation starting point.
The browser does not estimate the resulting reactivity.

Reactor Studio opens from **Begin shift** on the title screen as the only gameplay
presentation. It provides a round channel map,
fuel-age shortlist, axial power and burnup line graphs, direct refuelling, pacing, power targets,
operation impact, and all fourteen zone fills. Designer returns to Studio with
selection, refuelling draft, map mode and active chart tab preserved.
Studio subscriptions and DOM event handlers are removed
on scene shutdown, and native focus remains stable through snapshot updates.

Studio's eight tabs include the reactor and seven history views: power peaks,
discharge burnup, zone levels, tilt, reactivity, iodine/xenon, and fuel/score. The session
controller retains at most 4,096 observation samples across scene transitions;
it clears them on an accepted reset or new initialization. Samples use simulation
time, preserve instantaneous changes, and ignore duplicate status notifications.
History is presentation data, not a second simulation. Charts support per-trace
toggles, time windows, pointer inspection, keyboard tabs and a sample slider.
Game publishes the last discharge maximum and the shift's running discharge
maximum in MWd/kg HM through full and compact Browser responses. Null means
no accepted discharge yet; failed refuels leave these measurements unchanged.

The last accepted fuel move includes an immutable Game-owned scoring summary:
useful-discharge reward, positive fresh-fuel cost, signed net points and a policy
ID. Full and compact responses retain it through advances and rejected commands,
and reset clears it. Studio shows these components without recalculating the
scoring formula. This operation summary excludes ordinary operating score.

The finite fuel budget is `FreshBundlesAvailable`.
`RefuelRequestsRemaining` is a scenario/runtime counter and must not be
presented as fuel inventory.

Free practice has no scripted target changes or synthetic refuel requests.
Browser runs finish at the 30-day horizon or earlier RRS exhaustion. Game owns
the terminal decision; the browser receives `runStatus` and `runEndReason`
separately from physical RRS exhaustion. Terminal runs retain their final score
and inventory until reset. The top-level `targetPowerFraction` is the applied
target, consistent with the physics watt-valued target; queued commands do not
change it until time advances. Targets remain unavailable while paused.

## Gameplay bargain and player loop

The player repeatedly:

- runs or pauses the session and reads RRS reserve, actual power, score,
  inventory, and operation count;
- selects a channel and inspects bundle positions, burnup, local power, and
  tilt;
- chooses a direction and four- or eight-bundle shift and issues the order directly;
- observes the authoritative bundle movement, power/RRS response, discharged
  burnup, score, and event history;
- opens Core Designer when an engineering inspection is useful, edits a live
  cell or reflective boundary, solves the full core, then returns to the same
  run; and
- restarts after terminal RRS exhaustion and attempts a better run.

The UI may sort observed fuel age, highlight inspection candidates, and compare
accepted snapshots. These are presentation aids, not a second simulation.
Project-authored synthetic data and plausible approximations are sufficient for
gameplay; formal source validation is not a development gate.

## Runtime architecture and boundaries

Keep the browser a consumer of the shared application contract:

```text
Vercel-deployed DOM/SVG client + optional Phaser Designer
              |
TypeScript protocol + Web Worker
              |
      ReactorSim.BrowserHost
              |
        ReactorSim.Browser
              |
         ReactorSim.Game
              |
         ReactorSim.Core
              |
project-authored deterministic two-group pack
```

`ReactorSim.Core` owns deterministic state transitions, bundle inventory,
burnup, topology, regulating-system contracts, and spatial solving.
`ReactorSim.Game` owns session orchestration, commands, and immutable
presentation snapshots. `ReactorSim.Browser` serializes that contract.
`ReactorSim.BrowserHost` publishes it for `browser-wasm`. Native views and Designer consume
snapshots and send commands without reimplementing reactor physics.

The active runtime never invokes external analysis tools. Project-authored
physics packs required at runtime are embedded into `ReactorSim.Core`.
Repository `data`, `reference`, solver benchmarks, and research tooling are
development/validation inputs only.

GPU migration is paused in favor of the [CPU parallel experiment](physics/cpu-parallel-experiment.md).
Gameplay uses the shared CPU solver; the default browser publish remains single-threaded.
An opt-in threaded publish partitions independent operator rows while keeping
row arithmetic, convergence reductions, group ordering, and controller sequencing unchanged.
The [alternate host experiment](physics/cpu-main-thread-hosting.md) bootstraps
threaded WASM in the browser window and queues simulation work on managed
background threads; it is tested through developer tooling and is not the
default product transport.

The retained GPU experiments are read-only. Core owns the embedded WGSL and packed
numerical fixtures; BrowserHost executes fixed-step operator/Jacobi experiments
and a complete two-group source/eigen iteration experiment. C# verifies the
returned successive states with its existing f64 equations, power normalization,
and per-group/node/regional comparison budgets.
An explicit research build exposes read-only fixtures to developer tooling;
normal publishing excludes the GPU code, shader resources, modules and export.
See [research setup](maintenance/research-builds.md).
GPU results cannot commit session state, and gameplay continues on the CPU.
See [the staged migration plan](physics/gpu-migration-plan.md) for accuracy,
transaction, replay, fallback and rollout gates.

## Current physics and provenance

The practice session uses the project-authored
`candu6-two-group-diffusion-v1-cycle190-650mwe-innerrel1e7` path. It is a
regulated steady-state practice model rather than a sub-second transient claim.

The shared full-core adapter publishes explicit SI watts, normalized power,
eigenvalue `k`, and `rho = (k - 1) / k`, together with solve identity and
diagnostics. Short operation intervals reuse the retained equilibrium projection
for deterministic burnup integration. The browser displays signed axial tilt
from the solved thermal flux and two-sided RRS reserve from the live liquid-zone
fill limits. The practice score uses the spatial tilt and power projection,
without the legacy scenario tilt/control-margin score. Burnup can change the
equilibrium reactivity at the half-hour full-core solve. Iodine/xenon history
evolves analytically between those solves; prompt neutron kinetics remain out
of scope.

The fourteen liquid zones use the traditional CANDU 6 arrangement: seven
regions in each axial half, with two left, three centre, and two right regions.
The bounded RRS regulates net criticality independently of fuel burnup, then
adjusts spatial shape within the criticality band. It measures the diffusion
response and uses a secant correction for an inaccurate initial estimate.
See [independent regulation](physics/rrs-independent-regulation.md) for the
acceptance policy and refuelling regression. Browser 1x advances 30 simulated
minutes per real second; each base second recomputes Keff and zone fills.
See [liquid-zone RRS](physics/liquid-zone-rrs.md) for the region numbering,
source diagrams, controller limits, and approximation details.

The exact active finite-volume operator, boundary handling, source iteration,
and normalization are documented in
[`physics/active-two-group-solver.md`](physics/active-two-group-solver.md).

The practice game integrates spatial I-135/Xe-135 with an analytic frozen-flux
update and feeds the poison field into every diffusion/RRS trial. The original
aged-core balance is preserved by a fixed authored reference calibration.
See [iodine/xenon gameplay](physics/iodine-xenon-gameplay.md) for data, units,
calibration, fuel movement and coupling cadence. Studio charts core and regional
poison history through the shared simulation contract.

## Browser playtest notes

Studio, the launcher and the Designer inspector use native controls. Designer's
visual core map still uses a 1600 × 900 Phaser viewport with `Phaser.Scale.FIT`;
the canvas is hidden from the accessible tree. All essential controls and cell
readings remain available in the native inspector at a minimum 320px viewport.
Zone geometry adds a native channel selector and one roving map Tab stop.
The shared session live region announces operation results and terminal changes
once, stays quiet through ticks, and moves inside the active geometry modal.
The hidden telemetry mirror is for automation and is not a live region.
`AppShell` starts the native launcher and Studio; it imports `designerRuntime`
only when Designer opens, retains one controller/history, and sleeps the Phaser
loop on return. Session notifications identify status versus snapshot changes.
Studio caches watchlist/graph/movement inputs and patches stable SVG nodes;
history display reduction preserves extrema, gaps and step changes while keeping
all 4,096 observations for exact inspection. Requested pace and observed sim
minutes per real second are separate. The bounded presentation measurement
includes solver waits and resets across paused/hidden/navigation lifecycles;
it does not feed the simulation clock. See `performance/browser-phase3.md` for
cold/throttled startup, rendering traces and production-worker latency budgets.
The client sends only protocol commands to the worker-hosted
bridge. State/replay digests and reproduction archives are developer-only
contract tooling. Studio currently has no player replay export/import or feedback
companion screen. The real zone-layout draft JSON export remains available.

For local development:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm run dev
```

For a production-shaped browser build:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm test
npm run build
```

## Active work plan

The [repository knowledge library](maintenance/knowledge-library.md) indexes
retained decisions, measurements, reproduction commands and cleanup policy.

Follow [`WEB_ROADMAP.md`](WEB_ROADMAP.md) for the ordered web roadmap,
completion stages, deployed acceptance path, and exclusions. Current task status
is [REFACTORING_TASK_GUIDE.md](REFACTORING_TASK_GUIDE.md), and the single active
architecture page is [architecture.md](architecture.md).

## Testing and launch checks

Install a .NET 10.0.3xx SDK and Node 20.19+.

Run the shared .NET suites:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-DotNet.ps1
```

Run the browser-focused .NET suite, Vitest suite, and production frontend build:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-Browser.ps1
```

The production GitHub workflow additionally publishes the Release AOT
`browser-wasm` bridge, verifies the Vercel build output, deploys the prebuilt
artifact, then runs the browser smoke test and reproduction matrix against the
stable alias.

Functional acceptance uses the same smoke/reproduction scripts against a local
production preview or the stable deployed alias. Begin shift, pause and inspect
channels. Exercise four- and eight-bundle direct orders in both directions;
compare power, tilt, RRS, stock and per-bundle score. There is no preview/cancel/
confirm flow. Rejected geometry, invalid orders and stock/terminal guards are
checked separately for atomicity. Visit Designer repeatedly and retain draft,
selection, tab, history and last response. Verify keyboard focus, 320px/zoom
reflow, quiet announcements and zero console/bridge errors.

The score is `0.75 * clamp(discharged MWd/kg, 0, 10) - 1.5` per bundle, plus
at most `hours * (0.7 * powerQuality + 0.3 * tiltQuality)` operating points.
Use authoritative ending/objective/provenance fields rather than recomputing
challenge eligibility in the client. See the active architecture for terminal
and requested/observed pace contracts.

```powershell
cd web/candu-playtest
npm run build
npm run preview -- --port 4173
# In another terminal:
node scripts/smoke.mjs http://127.0.0.1:4173
node scripts/accessibility-smoke.mjs http://127.0.0.1:4173
node scripts/pace-layout-smoke.mjs http://127.0.0.1:4173
```

The empty Golden test shell was removed. The executed Core/Game/Browser suites
and shared C#/TS serialized fixtures describe actual coverage. Historical specs
and paused proposals are indexed in [historical research](maintenance/historical-research.md);
they are not acceptance requirements for new gameplay.

Current benchmark invocations and archived reproduction levels are in
[the tool/data index](maintenance/research-tools.md). Edit the canonical diffusion
pack under `data/packs`, then stage it with `tools/Sync-PhysicsPacks.ps1 -Stage`.
Core builds check that its embedded mirror matches; runtime still consumes only
embedded data and requires no external analysis programs.

## Phase 4 responsibility boundaries

The active Game session orchestrates commands over a focused practice run clock,
immutable physical/clock candidates, named scoring and presentation projectors,
and detached geometry transactions. It consumes the immutable equilibrium
projection directly. The retained IQS solver/candidate live in
`src/ReactorSim.Core.Research`; Core invariant/research tests reference that
project, while the browser product does not. The exact consumer boundary is in
[the equilibrium presentation note](physics/equilibrium-presentation-boundary.md).

Legacy complete-state transitions, device queues/maps, delayed-neutron kinetics,
reduced models and state archives also live in research. Shared bundle state,
spatial xenon and canonical digest/validation primitives remain in Core. The
[type-level audit](maintenance/core-research-boundary.md) records all retained
consumers, coverage and project-reference requirements. Newtonsoft remains the
active diffusion-pack parser; no data-source verification gate was added.

The static browser exports forward to one explicit `PlaytestRuntime`. Tools and
tests can create independent runtimes. Wire DTOs, mapping, digest policy, readers
and experiment handling have separate implementation files; JSON names and
canonical bytes retain protocol v2. Shared compressed C#/TS fixtures are under
`tests/Fixtures` and the original two-seed byte-hash baseline is under
`benchmarks/phase4-contract-baseline.json`.

The native shell injects one session into Studio and its optional Designer.
Typed navigation retains selected channel, order draft, map mode and history tab.
Map, order/movement, and pending/control updates have focused DOM boundaries;
Studio owns event routing and disposal. The controller owns a bounded view-only
last-response summary so accepted fuel impact survives navigation. Reset clears
that summary through the accepted response; reactor rules remain in shared C#.
