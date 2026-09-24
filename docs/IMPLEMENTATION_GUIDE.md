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

## Current implementation

The browser surface is one Phaser 3 canvas using the public
`ReactorSim.Game` session through the versioned `candu-playtest-v2` browser
bridge. The bridge runs in a worker and the client fails closed when the
authoritative WASM module is unavailable.

Starting a run initializes one live 380-channel by 12-position `GameSession`
and opens Operations. Operations owns pacing, channel inspection, refuelling,
and the power/RRS/score/burnup/inventory feedback from the full-core snapshot.
Core Designer opens from Operations as an engineering view of that same live
session. Its `configure-cell` and `solve` commands pass through the bridge;
successful edits replace the live equilibrium atomically, and returning to
Operations preserves the run clock, inventory, score, and accepted refuelling
state.

The finite fuel budget is `FreshBundlesAvailable`.
`RefuelRequestsRemaining` is a scenario/runtime counter and must not be
presented as fuel inventory.

## Gameplay bargain and player loop

The player repeatedly:

- runs or pauses the session and reads RRS reserve, actual power, score,
  inventory, and operation count;
- selects a channel and inspects bundle positions, burnup, local power, and
  tilt;
- chooses a direction and four- or eight-bundle shift, previews the order, then
  confirms or cancels it;
- observes the authoritative bundle movement, power/RRS response, discharged
  burnup, score, and event history;
- opens Core Designer when an engineering inspection is useful, edits a live
  cell or reflective boundary, solves the full core, then returns to the same
  run; and
- restarts after terminal RRS exhaustion and attempts a better run.

The UI may explain current state and consequences already present in a snapshot,
but it must not invent a fuel budget or future state.

## Runtime architecture and boundaries

Keep the browser a consumer of the shared application contract:

```text
Vercel-deployed Phaser/Vite client
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
`ReactorSim.BrowserHost` publishes it for `browser-wasm`. Phaser consumes
snapshots and sends commands without reimplementing reactor physics.

The active runtime never invokes external analysis tools. Project-authored
physics packs required at runtime are embedded into `ReactorSim.Core`.
Repository `data`, `reference`, solver benchmarks, and research tooling are
development/validation inputs only.

## Current physics and provenance

The practice session uses the project-authored
`candu6-two-group-diffusion-v1-infinite-cell-calibrated` path. It is a
regulated steady-state practice model rather than a sub-second transient claim.

The shared full-core adapter publishes explicit SI watts, normalized power,
eigenvalue `k`, and `rho = (k - 1) / k`, together with solve identity and
diagnostics. Short operation intervals reuse the retained equilibrium projection
for deterministic burnup integration. The browser displays signed axial tilt
from the solved thermal flux and two-sided RRS reserve from the live liquid-zone
fill limits. The practice score uses the spatial tilt and power projection,
without the legacy scenario tilt/control-margin score. Burnup can change the
equilibrium reactivity at the hourly full-core solve; this does not imply a
short-time decay transient.

The exact active finite-volume operator, boundary handling, source iteration,
and normalization are documented in
[`physics/active-two-group-solver.md`](physics/active-two-group-solver.md).

The repository contains iodine/xenon contracts, but the current practice
projection exposes xenon only as an unavailable/static compatibility state.

## Browser playtest notes

The visible client uses a 1600 × 900 Phaser tactical viewport with
`Phaser.Scale.FIT`. It sends only protocol commands to the worker-hosted
bridge. Local command history, state digests, replay JSON, and feedback notes
are companion tooling rather than a second authority.

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

Follow [`WEB_ROADMAP.md`](WEB_ROADMAP.md) for the ordered web roadmap,
completion stages, deployed acceptance path, and exclusions.

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

Functional acceptance is a deployed browser playthrough: launch the game, run
time, inspect channels, preview/cancel/confirm refuelling in both directions,
observe power/RRS/score/inventory feedback, exercise Core Designer and control
surfaces, and verify no console or bridge errors occur.
