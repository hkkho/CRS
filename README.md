# CANDU Refuelling Game

This repository is the source for the web-first CANDU on-power refuelling game
deployed from `web/candu-playtest` to Vercel.

The player keeps a deterministic practice reactor at useful power, manages RRS
reserve, spends a finite fresh-bundle inventory, refuels channels, and builds
score from stable operation and useful discharged burnup. Operations and Core
Designer share one authoritative `GameSession`; the browser never substitutes
a second simulator when the WASM bridge is unavailable.

In Operations, use **Find oldest fuel** (`N`) to inspect a candidate and `M`
to switch between power and burnup maps. Choose direction (`D`) and shift size
(`4` or `8`), then refuel (`R`). The response card compares local power, tilt,
reserve, fuel stock, and score; the zone strip shows all fourteen RRS fills.
Useful discharged burnup earns points, while throwing away fresh fuel costs
points. `Space` pauses/resumes; **New shift** resets the run and fuel budget.

**Reactor Studio** is an alternative interface with a light instrument-board
layout, round core map, fuel watchlist, horizontal bundle rack, and native
keyboard-accessible controls. Open it from the title screen, or press `V` in
Operations. **Tactical view** switches back while preserving the live run,
selected channel, direction, and shift size. Core Designer returns to the view
that opened it. In Studio, resume to apply power targets and pause to step time.

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
Phaser/Vite web client
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

- `web/candu-playtest` owns the Phaser UI, browser protocol, worker transport,
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
The current product contract is
[`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md), and the active
work plan is [`docs/WEB_ROADMAP.md`](docs/WEB_ROADMAP.md).

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
