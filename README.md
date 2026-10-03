# CANDU Refuelling Game

This repository is the source for the web-first CANDU on-power refuelling game
deployed from `web/candu-playtest` to Vercel.

The player keeps a deterministic practice reactor at useful power, manages RRS
reserve, spends a finite fresh-bundle inventory, refuels channels, and builds
score from stable operation and useful discharged burnup. Reactor Studio and Core
Designer share one authoritative `GameSession`; the browser never substitutes
a second simulator when the WASM bridge is unavailable.

In Reactor Studio, use **Highest burnup** (`N`) to inspect a candidate and the
map buttons to switch between power and burnup. Choose direction (`D`) and shift
size (4 or 8 bundles), then refuel (`R`). The response card compares local power, tilt,
reserve, fuel stock, and score; the zone strip shows all fourteen RRS fills.
Useful discharged burnup earns points, while throwing away fresh fuel costs
points. Stable operation earns up to one point per simulated hour, so a productive
fuel move materially affects the score. The [balance report](docs/gameplay/score-balance.md)
compares three seeds and four strategies. `Space` pauses/resumes; **New shift** resets the run and fuel budget.

The launcher has native **Begin shift**, seed and objective controls. Use Tab and
Enter/Space throughout play. Designer has native channel/position selectors,
readable cell measurements, fuel and boundary buttons, solve, zone geometry and
return controls over the same live session. Both support a 320px-wide layout;
continuous telemetry stays outside screen-reader announcements.

**Reactor Studio** is the primary game interface with a CRT phosphor terminal
layout, square channel cells, fuel watchlist, axial power/burnup line graphs, and native
keyboard-accessible controls. **Begin shift** opens Studio. Core Designer returns
to Studio while preserving history, selected channel, direction, and shift size.
Resume to apply power targets and pause to step time. The native launcher and
Studio start without Phaser; opening Designer loads its map on demand. The clock
shows requested speed and observed simulation minutes per real second, including
solver waits. History keeps all observations for inspection while reducing drawn
paths. [Browser measurements and budgets](docs/performance/browser-phase3.md)
record the startup/rendering improvements and their limits.

Studio shows the current objective, seed, time remaining and fuel budget. Choose
**One-day challenge** for a paused, 24-hour run: discharge at least eight bundles
at 6 MWd/kg or above and reach the end with RRS headroom to earn the **Efficient
refueller** badge. **Free practice** returns to the 30-day sandbox. No mandatory
scripted moves are added.

The shift report shows the ending reason, thermal energy and estimated electrical
energy delivered, fresh fuel consumed, useful discharged bundles and cumulative
operating/discharge/fuel-cost points. **Retry same seed** repeats the same objective
and aged core; **Try new seed** starts the next deterministic core. Reports and
badges belong to the current run and reset when a new run begins.

Before ordering, the fuel strip names the incoming/outgoing ends and marks the
positions that will leave, with their burnup. Confirmed moves retain bundle
identities, old/new positions, discharge burnup and score across Designer visits.
Highest burnup lists eligible channels only; a selected nonfuel channel explains
why it cannot be refuelled. RRS feedback names the controller decision and the
zone with least drain/fill headroom.

Inspection in Designer preserves a standard run. Accepted fuel, reflective-face
or zone-geometry edits mark the run **Modified sandbox** for the rest of that
run, including after restoring the geometry. Sandbox runs remain playable and
retain score/objective progress, but do not earn the standard challenge badge.
Reset recreates a standard run with the selected seed and objective.

The tabs show simulation-time line graphs for channel/bundle power peaks,
thermal/electrical output, discharged burnup, all fourteen zone levels and their
core mean, axial tilt, Keff/reactivity, reserve, fuel stock and score. Hover a graph
or use the sample slider to read values, filter its time window, and toggle traces.
History retains up to 4,096 observations in this browser session and clears on
**New shift**. Discharge readings come from confirmed shared-simulation fuel moves;
they remain unavailable until fuel has been discharged.

The **Iodine & xenon** tab shows core poison inventories and all fourteen
regional xenon traces. Poison follows bundle history, changes the solved power
shape, and affects zone fills. See the [gameplay model](docs/physics/iodine-xenon-gameplay.md)
for the analytic update and authored calibration.

In **Core Designer**, open **Zone geometry** (`Z`) to inspect all twelve axial
slices of the fourteen control regions and the homogenized absorber masks.
Paint region boundaries, set or clear absorber slopes, or move a compartment's
mask independently of its measured region. Draft edits have undo and JSON
export; **Apply layout & solve** commits through the shared simulation.
The [zone calibration](docs/physics/zone-calibration.md) documents the current
6.5 mk worth, positive absorption and balanced aged reference core.

The physics packs are project-authored approximations for plausible gameplay.
Formal source validation is not required to develop or play the game.

Reference output is 650 MW electrical at 2,064 MW thermal. Burnup and
channel/bundle power use thermal fission energy; Reactor Studio shows both
core totals. The electrical conversion is an authored presentation estimate.

Runs now start from a seeded aged-core snapshot. **New aged core** on the title
screen chooses the next seed; **New shift** recreates the current seed.
The [aged-core model notes](docs/physics/aged-core-starts.md) describe the
RFSP-inspired channel ages, eight-bundle history and burnup coverage.

## Product architecture

The active product path is intentionally narrow:

```text
Native DOM/SVG web client + optional Phaser Designer
        |
TypeScript protocol + Web Worker
        |
ReactorSim.BrowserHost (browser-wasm)
        |
ReactorSim.Browser
        |
ReactorSim.Game
        |
ReactorSim.Core
```

- `web/candu-playtest` owns the native DOM/SVG UI, optional Phaser Designer, browser protocol, worker transport,
  smoke test, and deployed reproduction benchmark.
- `src/ReactorSim.BrowserHost` publishes the .NET browser-WASM host.
- `src/ReactorSim.Browser` owns the versioned bridge contract.
- `src/ReactorSim.Game` owns the reusable run/session and presentation
  snapshots.
- `src/ReactorSim.Core` owns deterministic reactor state, refuelling,
  inventory, burnup, controls, and the spatial solver.
- `data`, `reference`, `docs`, and the retained solver benchmark/tooling
  support the shared simulation and its provenance; they are not alternate
  playable products.

The exact active two-group equations are documented in
[`docs/physics/active-two-group-solver.md`](docs/physics/active-two-group-solver.md).
The current architecture is [docs/architecture.md](docs/architecture.md), and
task status is [docs/REFACTORING_TASK_GUIDE.md](docs/REFACTORING_TASK_GUIDE.md).
The current product contract is
[`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md), and the active
work plan is [`docs/WEB_ROADMAP.md`](docs/WEB_ROADMAP.md).

Current benchmarks, canonical pack staging and archived reproduction tools are
indexed in [docs/maintenance/research-tools.md](docs/maintenance/research-tools.md).

## Local development

Stage the authoritative browser bridge from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
```

Then run the web app:

```powershell
cd web/candu-playtest
npm ci
npm run dev
```

For a production-shaped local build:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm test
npm run build
```

Generated WASM is staged into `web/candu-playtest/public/wasm` and is ignored
except for the directory placeholder.

## Tests

Run the shared .NET tests that protect the web runtime:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-DotNet.ps1
```

Run the browser-focused .NET, Vitest, and production-build checks:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-Browser.ps1
```

The browser package also exposes:

```text
npm run smoke
npm run benchmark -- --label=local --warm-samples=1
```

The deployed acceptance path is the stable Vercel alias. The production workflow
builds the .NET AOT WASM bridge, verifies the staged bridge, runs frontend
tests/builds, deploys the prebuilt Vercel output, then performs smoke and
reproduction-matrix checks against the stable deployment.

## Scope

The repository is for the web CANDU game and the shared simulation required to
run and validate it. Separate presentation clients, headless gameplay products,
and archived duplicate test trees are intentionally excluded.

Shutdown, scram, accident progression, operator-training scenarios, full plant
operations, and plant-grade safety claims remain out of scope.

The aged-core reference uses a 190-full-power-day channel refuelling interval
(16 fresh bundles/day, 285-day average bundle residence) at 2064 MW thermal.
See [power and fuel calibration](docs/physics/power-and-fuel-calibration.md).
