# CANDU Refuelling Game

This repository contains a web-first CANDU on-power refuelling game. The
Vercel-deployed `web/candu-playtest` is the primary product and acceptance path
until the web game is highly functional. It is a deterministic practice run:
keep the reactor at useful power, manage the automatic regulating-system (RRS)
reserve, spend a finite fresh-bundle inventory carefully, and build score from
stable operation and useful discharged burnup. The game should become more
realistic after this loop is enjoyable and reliable.

Read [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) for the
current product contract, architecture boundaries, and physics provenance.
The exact active finite-volume and two-group eigen equations are documented in
[`docs/physics/active-two-group-solver.md`](docs/physics/active-two-group-solver.md).
The sole active work plan is [`docs/WEB_ROADMAP.md`](docs/WEB_ROADMAP.md).

## Current product

- `web/candu-playtest` is the primary playable and acceptance surface. Its
  Vercel deployment exposes the live browser refuelling loop and feedback. It
  is one Phaser 3 canvas using the same `ReactorSim.Game` session through the
  versioned browser bridge. The title has one Play start; Operations then
  contains the live 380-channel run and an in-run Core Designer for that same
  reactor. The bridge owns one full-core `GameSession`, fails closed when the
  authoritative WASM bridge is unavailable, and never substitutes a browser
  simulator for the shared authorities.
- `unity/ReactorGame` is retained as a runnable Unity presentation and input
  surface, but Unity feature development is paused while the web game becomes
  highly functional. It starts a project-authored practice session and binds the
  Dashboard, Controls, Timeline, Core Map, and F1/backquote debug menu. The
  Unity runtime advances the shared session through bounded 100 ms wall-time
  requests.
- The Dashboard exposes the RRS reserve and a compact RUN STAKES surface. The
  player-facing budget is `FreshBundlesAvailable`; `RefuelRequestsRemaining` is
  a scenario/runtime counter and is not the fuel budget. A terminal RRS reserve
  state stops automatic advancement and offers a run restart.
- The Core Map presents 380 selectable channels and the 12 bundle positions in
  a selected channel. Refuelling commits through `GameSession`, and the shared
  projection supplies the resulting power, tilt, burnup, and score feedback.
- Core Designer is an engineering view opened from Operations during the same
  run. It edits the live 380-channel by 12-position topology and shows the
  authoritative two-group solve. Its `configure-cell` command changes a
  selected cell's fuel state and reflective faces, and `solve` recomputes the
  same full-core `GameSession`; returning to Operations preserves the same
  clock, inventory, and score.
- `src/ReactorSim.Core` owns engine-neutral deterministic state transitions,
  inventory, burnup, topology, control contracts, and the two-group spatial
  solve. `src/ReactorSim.Game` owns the reusable session, commands, and
  presentation snapshot. Unity is an input and presentation layer over those
  authorities.
- `src/ReactorSim.Cli` runs practice scenarios headlessly for validation and
  long-horizon checks.
- `data` and `reference` contain project-authored packs and design context.

## Simulation contract

The practice session uses the project-authored
`candu6-two-group-diffusion-v1-infinite-cell-calibrated` pack. It is the
deterministic game model, with explicit provenance and SI units. The shared
full-core adapter publishes explicit
SI watts, normalized power, `k`, and `rho = (k - 1) / k`, along with solve
identity and diagnostics. Its static flux shape is normalized to the operator
target; short operation intervals reuse the retained equilibrium projection for
deterministic burnup integration. This is a regulated steady-state practice
model, not a sub-second transient claim. The project-authored pack is the
active simulation and acceptance path. See the [active solver equations](docs/physics/active-two-group-solver.md)
for the exact operator, editable material overlay, and iteration.

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

The primary acceptance path is the deployed Vercel site for
`web/candu-playtest`. Use that deployment to exercise the live browser
refuelling loop, immediate power/RRS/score feedback, and the playtest/debug
controls.

For local browser development, stage the authoritative bridge from the
repository root and run:

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

Unity is retained but frozen. If you need to inspect the existing Unity
surface or validate a shared change that affects it, install Unity
`6000.3.21f1` and a .NET `10.0.3xx` SDK, then run:

```powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
```

Open `unity/ReactorGame` in Unity and run `Assets/Scenes/Bootstrap.unity`.
The existing practice session starts at 10x simulation speed. Use Controls to
pause, resume, change playback speed, queue power and tilt targets, or refuel
a numbered channel toward either end. The project-authored inventory starts
with 128
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
and Bootstrap smoke. The primary acceptance path is an owner playthrough of
the deployed Vercel browser playtest; use the Unity scene and debug menu only
for retained-surface checks or shared-change validation.

### Browser playtest

Run the Phaser playtest independently for local development:

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

The Phaser client is a static companion. Its bridge runs in a worker, uses the
same Core solver as the live game session, and keeps local command
history/replay and feedback notes local to the browser. The compact
project-authored data pack ships with the authoritative bridge.

### Browser Operations and Core Designer

The title has one `PLAY` entry. It initializes one live 380-channel by
12-position `GameSession`, then opens Operations. Operations is the main game
surface: run the clock, inspect channels and bundle positions, refuel, and
read the authoritative power, RRS, score, burnup, and inventory feedback.

Open Core Designer from Operations to edit the live reactor. The workspace
shows all 380 channels, the 12 axial positions in the selected channel, each
cell's fuel state, reflective faces, group fluxes, local power, convergence,
eigenvalue, and residual diagnostics. `configure-cell` commits a cell edit as
an atomic full-core transaction; `solve` reruns the same two-group diffusion
solver with the current design. A nonfuel cell receives the model's moderator
coefficient row, so it carries no fission source or power while retaining its
bundle identity. Reflective interior faces are applied to both sides of the
shared edge; an outer reflective face has zero leakage. Return to Operations to
continue the same run with the edited reactor state. See the [solver math
document](docs/physics/active-two-group-solver.md) for the equations and
boundary conditions.

## Active work plan

Follow [`docs/WEB_ROADMAP.md`](docs/WEB_ROADMAP.md) for the ordered web-first
roadmap, completion stages, acceptance path, and exclusions. Unity feature
development remains paused during this phase.
