# CRS

This repository is the source for the web CANDU on-power refuelling game
deployed from `web/candu-playtest` to [GitHub Pages](https://hkkho.github.io/CRS/).

Pushes to `main` deploy the validated shared WASM simulation;
see [hosting and verification](docs/maintenance/hosting.md).

The player keeps a deterministic practice reactor at useful power, manages average
LZC level between 10% and 90%, tracks fresh-bundle use, and builds
score by keeping channel powers close to their time-average reference. CRS consumes
one authoritative `GameSession`; the browser never substitutes
a second simulator when the WASM bridge is unavailable.

Select **Time based**, then **Start** once the core is ready. The shared
simulation advances twelve hours per five seconds of playback. Double-click a
channel, press Enter/R, or select it and tap **Queue fuel** to queue an
eight-bundle move. The next unsolved step takes all waiting orders together.
Yellow outlines show queued orders and a white outline shows the selected
channel. Choose channel power, ripple, burnup, or paired zone fills from the
map dropdown. Hover or focus a channel to read its value.

Power and zone fills blend between solved endpoints. Slow solves hold the last
endpoint. Pause and hidden tabs freeze playback and retain queued fuel. **Retry
seed** repeats the starting core and **New core** draws another. Time-based
play is endless free practice with unlimited fresh fuel.

When a new build is ready, the game finishes its current solve, saves the
authoritative endpoint and waiting fuel orders in this browser, then reloads.
Close other game tabs if prompted so the update can proceed. Compatible
checkpoints reopen at the **Start** screen. If the reactor model changed, the
checkpoint stays stored and the game offers a return to the launcher. Existing
daily saves remain in browser storage.

LZC control uses the fixed time-average node-power reference derived with zones
at 50% fill. The seeded aged core settles its own initial zone distribution
against that reference before play, so the initial fills need not be uniform.
The shared controller and equilibrium xenon settle together without using run
time or fuel. Existing daily saves use an older run-model identity and require a
new run; the stored save is retained.

A run ends below 10% or above 90% average LZC level, beyond ±20% global tilt,
above 7,100 kW/channel or above 900 kW/bundle. Exact equality is allowed. The
time-based core maps use fixed, continuous gradients tuned to observed channel
power, ripple, burnup and zone-fill ranges. Each legend marks the readings
that anchor its color scale. Power above 6,500 kW pulses and above
7,000 kW shakes, with static outlines under reduced motion. Hover and keyboard
tooltips show the exact reading. Scoring measures RMS deviation
from fixed channel references, earning up to one point per simulated hour.
Refuelling affects later score through its power response. See the
[scoring policy](docs/gameplay/score-balance.md),
[reference derivation](docs/physics/channel-power-reference.md) and
[daily-turn contract](docs/gameplay/daily-turn-mode.md).

The live charts show channel power and average liquid-zone level. The
fourteen zone readouts show individual fill and available control room. The
loaded build and reactor model are shown on screen.

The current [v9 circular radial boundary](docs/physics/radial-circle-v9.md) lowers
the outer-rim time-average target to about 4,200 kW and raises the middle to about
6,400 kW while retaining 2,064 MW thermal. The axial boundary still uses zero incoming
partial current at both reactor ends, with refitted criticality and device worths.
End-bundle power falls sharply; daily operation still uses one large 24-hour step.
The [100-day before/after verification](benchmarks/radial-circle-v9-2026-10-30/README.md)
records nearly unchanged fuel demand, discharge burnup, score and control range.
The physics packs are project-authored approximations for plausible gameplay.
Formal source validation is not required to develop or play the game.
The [literature-guided fuel calibration](docs/physics/literature-geometry-v4.md) uses
the published lattice-cell volume and fits the cited static burnup curve. It measures
about 0.348 mk/FPD fuel-only loss in v6 with [burnup-bound xenon replacement](docs/physics/xenon-reference-v6.md)
and the [21 fixed adjusters](docs/physics/adjusters-v5.md),
17 mk total adjuster worth and 7 mk total liquid-zone worth.
The [v7 tube model](docs/physics/lzc-tubes-v7.md) localizes LZC absorption and
tracks bottom-up water filling, retaining those device worths after retuning.
Group constants remain a surrogate, with a documented poison-basis limitation. Daily LZC solves occur at the day boundary; direct real-time comparison
retains its three-minute solve cadence.
The historical [v3 attempts](benchmarks/fuelling-capability-2026-10-06/README.md)
ended early on channel-power limits; the [v4 rerun](benchmarks/fuelling-capability-2026-10-07/README.md)
completed 100 days with both policies. These results predate the adjusters and corrected xenon reference.

Reference output is 650 MW electrical at 2,064 MW thermal. Burnup and
channel/bundle power use thermal fission energy; CRS shows both
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
For a detailed walkthrough from browser startup through commands, daily transactions,
physics, snapshots and saves, start with the
[code documentation library](docs/code-library/index.md). It includes Mermaid
diagrams and a source atlas. The published [documentation site](https://hkkho.github.io/CRS/docs/)
also includes the [player manual](https://hkkho.github.io/CRS/docs/manual/) and
[reactor theory](https://hkkho.github.io/CRS/docs/theory/). The in-game F1 manual
uses the same Markdown chapters as the website.
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
