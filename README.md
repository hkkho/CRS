# CANDU Refuelling Game

This repository is the source for the web CANDU on-power refuelling game
deployed from `web/candu-playtest` to [GitHub Pages](https://hkkho.github.io/CRS/).

Pushes to `main` deploy the validated shared WASM simulation;
see [hosting and verification](docs/maintenance/hosting.md).

The player keeps a deterministic practice reactor at useful power, manages average
LZC level between 10% and 90%, spends a finite fresh-bundle inventory, and builds
score by keeping channel powers close to their time-average reference. Reactor Studio consumes
one authoritative `GameSession`; the browser never substitutes
a second simulator when the WASM bridge is unavailable.

Reactor Studio defaults to **daily turns**. Time stays frozen while you inspect
power/burnup maps, bundle profiles, the fuel watchlist and zone levels. Use
**Highest burnup** (`N`) to inspect a candidate, then **Add to today's fuel plan**
(`R`) to add or remove that channel. Each chosen channel receives eight fresh
bundles with its flow. Adjacent channels have opposite flow directions.

Review or clear today's list, then select **Refuel & advance 1 day**. Orders execute
in displayed channel order before the shared simulation advances 24 hours.
**Advance 1 day without refuelling** is also valid. An animated waiting screen follows the calculation and then flashes success or
failure. A surviving day shows earned points and total score. The daily report retains every
fuel movement, bundle identities, discharge burnup, fuel consumed, energy,
score and ending reason. Limits are checked after each fuel move and at the day
boundary. A violating fuel move stops remaining orders; a day-end loss commits
the completed one-day update. Invalid plans or numerical failures consume no fuel or time.

The default **Free practice** run is endless with unlimited fresh fuel. The
optional **One-day challenge** uses the same daily controls and existing finite
fuel budget: discharge at least eight bundles at 6 MWd/kg or above and finish
the day within the operating limits to earn **Efficient refueller**.
**New shift** draws a new aged-core seed; retry preserves the seed and objective.

A run ends below 10% or above 90% average LZC level, beyond ±20% global tilt,
above 7,300 kW/channel or above 935 kW/bundle. Exact equality is allowed. The
power map runs from blue through yellow to red. Scoring measures RMS deviation
from fixed channel references, earning up to one point per simulated hour.
Refuelling affects later score through its power response. See the
[scoring policy](docs/gameplay/score-balance.md),
[reference derivation](docs/physics/channel-power-reference.md) and
[daily-turn contract](docs/gameplay/daily-turn-mode.md).

The native launcher and Studio support keyboard operation, a 320px layout and
quiet operation announcements. History records complete day-boundary observations;
return to live before issuing orders. Version-2 saves include daily pacing and
the unfinished fuel plan; legacy version-1 saves replay in real-time mode.
Apply the new [cloud save migration](supabase/migrations/20261009232415_daily_turn_saves.sql)
to an existing Supabase deployment before uploading version-2 saves.

For developer comparisons, `?pacing=real-time` explicitly starts the retained
real-time loop with pause/resume, accelerated speeds and power-target controls.
Normal new test runs use one large 24-hour step: update burnup and poison once
using the post-refuelling power/flux, then solve equilibrium and LZC once at the
next day boundary. Limits are checked after fuel moves and at the day end.
Real-time developer pacing retains its three-minute integration cadence.

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
Benchmark-based color floors and zoomed graph bounds make day-to-day changes
visible. Axes expand for outliers, and off-scale operating limits stay labelled.
History retains up to 4,096 observations in this browser session and clears on
**New shift**. Discharge readings come from confirmed shared-simulation fuel moves;
they remain unavailable until fuel has been discharged.

The **Iodine & xenon** tab shows core poison inventories and all fourteen
regional xenon traces. Poison follows bundle history, changes the solved power
shape, and affects zone fills. See the [gameplay model](docs/physics/iodine-xenon-gameplay.md)
for the analytic update and authored calibration. The channel inspector plots
per-bundle iodine and xenon in End A to End B order. Fresh bundles enter with
zero of both; retained bundles carry their existing inventories. Daily planning freezes
these values; advancing a day builds poison in fresh fuel.

The current [v8 axial boundary](docs/physics/axial-marshak-v8.md) uses zero incoming
partial current at both reactor ends, with refitted criticality and device worths.
End-bundle power falls sharply; daily operation still uses one large 24-hour step.
The [100-day verification](benchmarks/axial-marshak-v8-2026-10-10/README.md) records
the changed fuelling demand and a successful LZC-guided player policy.
The physics packs are project-authored approximations for plausible gameplay.
Formal source validation is not required to develop or play the game.
The [literature-guided fuel calibration](docs/physics/literature-geometry-v4.md) uses
the published lattice-cell volume and fits the cited static burnup curve. It measures
about 0.348 mk/FPD fuel-only loss in v6 with [burnup-bound xenon replacement](docs/physics/xenon-reference-v6.md)
and the [21 fixed adjusters](docs/physics/adjusters-v5.md),
17 mk total adjuster worth and 7 mk total liquid-zone worth.
The [v7 tube model](docs/physics/lzc-tubes-v7.md) localizes LZC absorption and
tracks bottom-up water filling, retaining those device worths after retuning.
Group constants remain a surrogate, with a documented poison-basis limitation. Daily LZC solves occur at the day boundary; the real-time developer mode
retains its three-minute solve cadence.
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

Studio marks the fixed adjusters in amber on the face, separates all 21 rods
in an axial plan, and shades their overlapping bundle positions on the selected
channel's power graph. Geometry comes from the authoritative shared device map.
The [local absorber audit](docs/physics/local-absorber-power.md) measures their
power depression and the regional LZC limitation addressed by the v7 tube model.

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
The current architecture is [docs/architecture.md](docs/architecture.md).
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

For faster shared-simulation checks in the same optimized configuration as CI,
pass `-Configuration Release` to either test script. Assertions and coverage are
unchanged.

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
