# CANDU Refuelling Game

This repository is the source for the web-first CANDU on-power refuelling game
deployed from `web/candu-playtest` to [GitHub Pages](https://hkkho.github.io/CRS/).

Pushes to `main` deploy the validated shared WASM simulation;
see [hosting and verification](docs/maintenance/hosting.md).

The player keeps a deterministic practice reactor at useful power, manages average
LZC level between 10% and 90%, spends a finite fresh-bundle inventory, and builds
score by keeping channel powers close to their time-average reference. Reactor Studio consumes
one authoritative `GameSession`; the browser never substitutes
a second simulator when the WASM bridge is unavailable.

In Reactor Studio, use **Highest burnup** (`N`) to inspect a candidate and the
map buttons to switch between power and burnup. Refuel (`R`) automatically inserts
eight bundles with the selected channel’s flow. Adjacent channels have opposite
flow directions. The power map runs from blue (low) through yellow to red (high).
A shift ends
when average LZC level falls below 10% or exceeds 90%, or absolute global tilt
exceeds 20%, any channel exceeds 7,300 kW thermal, or any bundle exceeds
935 kW thermal. Exact power-limit equality is allowed. The response card compares local power, tilt,
average LZC level, fuel stock, and score; the zone strip shows all fourteen RRS fills.
Score measures RMS deviation from the fixed channel targets, earning up to one
point per simulated hour. Refuelling affects later score through its power response.
The [scoring policy](docs/gameplay/score-balance.md) and
[reference derivation](docs/physics/channel-power-reference.md) explain the 2,064 MW
thermal profile with 21 nominal adjusters. `Space` pauses/resumes; **New shift** resets the run.

The launcher has native **Begin shift**, seed and objective controls. Use Tab and
Enter/Space throughout play. The native interface supports a 320px-wide layout;
continuous telemetry stays outside screen-reader announcements.

**Reactor Studio** is the primary game interface with a CRT phosphor terminal
layout, square channel cells, fuel watchlist, axial power/burnup line graphs, and native
keyboard-accessible controls. **Begin shift** opens Studio with an automatic eight-bundle order.
Resume to apply power targets and pause to step time. The native launcher and
Studio use native DOM/SVG presentation. The clock
shows requested speed and observed simulation minutes per real second, including
solver waits. History keeps all observations for inspection while reducing drawn
paths. [Browser measurements and budgets](docs/performance/browser-phase3.md)
record the startup/rendering improvements and their limits.

The main **Free practice** game runs endlessly with unlimited fresh fuel. Studio
tracks elapsed days, bundles consumed, energy and score. Choose
**One-day challenge** for a paused, 24-hour run: discharge at least eight bundles
at 6 MWd/kg or above and reach the end with average LZC level between 10% and 90%
and global tilt within ±20% to earn the **Efficient refueller** badge. **Free practice** returns to the endless main game. No mandatory
scripted moves are added.

The shift report shows the ending reason, thermal energy and estimated electrical
energy delivered, fresh fuel consumed, useful discharged bundles and cumulative
operating/discharge/fuel-cost points. **Retry same seed** repeats the same objective
and aged core; **Try new seed** draws a random new core. Reports and
badges belong to the current run and reset when a new run begins.

Before ordering, the fuel strip names the incoming/outgoing ends and marks the
positions that will leave, with their burnup. Confirmed moves retain bundle
identities, old/new positions, discharge burnup and score throughout the shift.
Highest burnup lists eligible channels only; a selected nonfuel channel explains
why it cannot be refuelled. RRS feedback names the controller decision and the
zone with least drain/fill headroom.

The tabs show simulation-time line graphs for channel/bundle power peaks,
thermal/electrical output, discharged burnup, all fourteen zone levels and their
core mean, axial tilt, Keff/reactivity, average LZC level, fuel stock and score. Hover a graph
or use the sample slider to read values, filter its time window, and toggle traces.
History retains up to 4,096 observations in this browser session and clears on
**New shift**. Discharge readings come from confirmed shared-simulation fuel moves;
they remain unavailable until fuel has been discharged.

The **Iodine & xenon** tab shows core poison inventories and all fourteen
regional xenon traces. Poison follows bundle history, changes the solved power
shape, and affects zone fills. See the [gameplay model](docs/physics/iodine-xenon-gameplay.md)
for the analytic update and authored calibration. The channel inspector plots
per-bundle iodine and xenon in End A to End B order. Fresh bundles enter with
zero of both; retained bundles carry their existing inventories. Pausing freezes
these values; advancing simulation time builds poison in fresh fuel.

The physics packs are project-authored approximations for plausible gameplay.
Formal source validation is not required to develop or play the game.
The [literature-guided fuel calibration](docs/physics/literature-geometry-v4.md) uses
the published lattice-cell volume and fits the cited static burnup curve. It measures
about 0.348 mk/FPD fuel-only loss with [burnup-bound xenon replacement](docs/physics/xenon-reference-v6.md)
and the [21 fixed adjusters](docs/physics/adjusters-v5.md),
17 mk total adjuster worth and 7 mk total liquid-zone worth. Group constants
remain a surrogate, with a documented poison-basis limitation. LZC solves
at every three-minute browser simulation step, including at accelerated speeds.
The historical [v3 attempts](benchmarks/fuelling-capability-2026-10-06/README.md)
ended early on channel-power limits; the [v4 rerun](benchmarks/fuelling-capability-2026-10-07/README.md)
completed 100 days with both policies. These results predate the adjusters and corrected xenon reference.

Reference output is 650 MW electrical at 2,064 MW thermal. Burnup and
channel/bundle power use thermal fission energy; Reactor Studio shows both
core totals. The electrical conversion is an authored presentation estimate.

Runs now start from a seeded aged-core snapshot. **New aged core** on the title
screen and **New shift** draw a random 32-bit seed using browser cryptographic
randomness, as does initial loading. Explicit seed entry and **Retry same seed**
recreate a deterministic core.
The [aged-core model notes](docs/physics/aged-core-starts.md) describe the
RFSP-inspired channel ages, eight-bundle history and burnup coverage.

## Product architecture

The active product path is intentionally narrow:

```text
Native DOM/SVG web client
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

- `web/candu-playtest` owns the native DOM/SVG UI, browser protocol, worker transport,
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

Optional GitHub login, private cloud saves, guest/offline play and the endless
leaderboard are described in [player accounts and saves](docs/maintenance/player-accounts.md).
Cloud features require the documented Supabase project configuration.

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

The deployed acceptance path is [GitHub Pages](https://hkkho.github.io/CRS/).
The production workflow builds the .NET AOT WASM bridge, verifies the staged
bridge, runs frontend tests/builds and smoke/reproduction checks under `/CRS/`,
then publishes the validated Pages artifact and verifies the deployed commit.

## Scope

The repository is for the web CANDU game and the shared simulation required to
run and validate it. Separate presentation clients, headless gameplay products,
and archived duplicate test trees are intentionally excluded.

Shutdown, scram, accident progression, operator-training scenarios, full plant
operations, and plant-grade safety claims remain out of scope.

The aged-core reference uses a 190-full-power-day channel refuelling interval
(16 fresh bundles/day, 285-day average bundle residence) at 2064 MW thermal.
See [power and fuel calibration](docs/physics/power-and-fuel-calibration.md).
