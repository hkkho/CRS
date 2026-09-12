# CANDU Refuelling Game

This repository contains a playable Unity game about steady-state CANDU
on-power refuelling. The current product is a deterministic practice run: keep
the reactor at useful power, manage the automatic regulating-system (RRS)
reserve, spend a finite fresh-bundle inventory carefully, and build score from
stable operation and useful discharged burnup. The game should become more
realistic after this loop is enjoyable and reliable.

Read [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) for the
current product contract, architecture boundaries, physics provenance, and
development priorities.

## Current product

- `unity/ReactorGame` is the primary playable surface. It starts a synthetic
  practice session and binds the Dashboard, Controls, Timeline, Core Map, and
  F1/backquote debug menu. The Unity runtime advances the shared session through
  bounded 100 ms wall-time requests.
- The Dashboard exposes the RRS reserve and a compact RUN STAKES surface. The
  player-facing budget is `FreshBundlesAvailable`; `RefuelRequestsRemaining` is
  a scenario/runtime counter and is not the fuel budget. A terminal RRS reserve
  state stops automatic advancement and offers a run restart.
- The Core Map presents 380 selectable channels and the 12 bundle positions in
  a selected channel. Refuelling commits through `GameSession`, and the shared
  projection supplies the resulting power, tilt, burnup, and score feedback.
- `src/ReactorSim.Core` owns engine-neutral deterministic state transitions,
  inventory, burnup, topology, control contracts, and the two-group spatial
  solve. `src/ReactorSim.Game` owns the reusable session, commands, and
  presentation snapshot. Unity is an input and presentation layer over those
  authorities.
- `src/ReactorSim.Cli` runs synthetic scenarios headlessly for validation and
  long-horizon checks.
- `web/candu-playtest` is a secondary Phaser 3 companion for interaction and
  bridge playtesting. It uses the same `ReactorSim.Game` session through the
  versioned browser bridge, fails closed when the authoritative WASM bridge is
  unavailable, and never substitutes a browser simulator for Unity acceptance.
- `data` and `reference` contain synthetic packs, design context, and the
  offline DRAGON5/DONJON5 integration material.

## Simulation contract

The practice session uses the project-authored
`candu6-two-group-diffusion-v1-infinite-cell-calibrated` pack. It is a
surrogate, not a plant rating or an external DRAGON/DONJON result. The shared
full-core adapter publishes explicit SI watts, normalized power, `k`, and
`rho = (k - 1) / k`, along with solve identity and diagnostics. Its static
flux shape is normalized to the operator target; short operation intervals
reuse the retained equilibrium projection for deterministic burnup integration.
This is a regulated steady-state practice model, not a sub-second transient
claim.

The Core repository includes iodine/xenon contracts, but the current
`GameSession` practice projection intentionally exposes xenon as an unavailable
static compatibility state with no coupling in Unity or the browser companion.

## Gameplay bargain

The player must maintain automatic RRS reserve away from both 0% and 100%,
conserve finite fresh bundles, and earn score through stable power and useful
discharged burnup. Refuelling operations change the authoritative bundle state;
the shared projection then supplies power and RRS feedback. The UI does not
invent a fuel budget or unmodeled future state.

Shutdown, scram, accident progression, operator-training scenarios, and full
plant operations are out of scope for this product.

## Quick start

Install Unity `6000.3.21f1` and a .NET `10.0.3xx` SDK, then run:

```powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
```

Open `unity/ReactorGame` in Unity and run `Assets/Scenes/Bootstrap.unity`.
The practice session starts at 10x simulation speed. Use Controls to pause,
resume, change playback speed, queue power and tilt targets, or refuel a
numbered channel toward either end. The synthetic inventory starts with 128
fresh bundles and accepts `NAT-U-SYNTHETIC` fuel in four- or eight-bundle
shifts. Use Core Map to inspect channels and commit a shift; press F1 or
backquote for the debug menu.

Automated checks are focused on the code being changed:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-DotNet.ps1
powershell -ExecutionPolicy Bypass -File tools/Test-Browser.ps1
powershell -ExecutionPolicy Bypass -File tools/Test-UnityImport.ps1
```

`Test-DotNet.ps1` runs all available .NET suites by default; use `-Suite Core`,
`-Suite Game`, `-Suite Browser`, or `-Suite All` to select one. The browser
runner expects dependencies to be installed and runs the bridge suite, Vitest,
and production build. The Unity runner discovers the pinned editor or accepts
`-UnityEditorPath`, prepares the simulation DLLs, and runs the import/compile
and Bootstrap smoke. The primary acceptance path is still an owner playthrough
of the Unity scene and debug menu.

### Browser companion

Run the secondary Phaser companion independently:

```powershell
cd web/candu-playtest
npm install
npm run dev
```

For a production-shaped build, stage the authoritative bridge from the
repository root and then build the web project:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm test
npm run build
```

The Phaser client is a static companion. Its bridge runs in a worker, the
runtime never invokes DRAGON5, DONJON5, or another analysis executable, and
local command history/replay and feedback notes remain local to the browser.

## Next priorities

In order, keep the work focused on the live player loop:

- Tune the Unity dashboard, refuelling feedback, pacing, and accessibility
  using the existing authoritative snapshot; keep the RUN STAKES bargain
  legible during normal play and at terminal RRS exhaustion.
- Improve operation readability and debug-menu diagnostics without duplicating
  `GameSession` rules or adding presentation-owned simulation rules.
- Validate scoring and practice pacing against stable-power, reserve, inventory,
  and discharged-burnup behavior through focused tests and owner playtests.
- Admit a compact, versioned offline DRAGON5/DONJON5-derived data pack behind
  the existing runtime seam only after provenance, licensing, units, group
  ordering, topology, convergence, and power-balance checks are documented.

Richer reactor physics can follow those priorities as a separate validated
data/model effort; it is not a prerequisite for making the current Unity run
playable.
