# CANDU Refuelling Game implementation guide

## Product direction

Build a fun, responsive web game about keeping a CANDU reactor at useful steady
power through on-power refuelling. The player inspects channels and bundles,
chooses a refuelling operation, watches the shared reactor state update, and
learns the relationship between fuel history, power shape, RRS reserve, and
score. The Vercel-deployed `web/candu-playtest` is the primary product and
acceptance path until the web game is highly functional.

The current game is intentionally a deterministic practice model. The product
bargain is visible and finite: maintain automatic RRS reserve away from both
0% and 100%, conserve fresh bundles, and earn score through stable power and
useful discharged burnup. Make that loop readable and enjoyable before adding
more physical detail. Unity is retained as a runnable presentation and input
client, but Unity feature development is paused during this web-first phase.
Shutdown, scram, accident progression, operator-training scenarios, and full
plant simulation are out of scope.

## Current implementation

`web/candu-playtest` is the primary playable surface, and its Vercel deployment
is the primary acceptance path. It is one Phaser 3 canvas using the same public
`ReactorSim.Game` contract through the versioned `candu-playtest-v1` browser
bridge. The bridge runs in a worker and the client fails closed when the
authoritative WASM module is unavailable; the browser is never a second
simulation authority.

`unity/ReactorGame` remains a runnable retained surface, but is frozen for
feature development while the web game becomes highly functional. It creates a
real `PracticeGameSessionFactory` session, binds the Bootstrap shell, and
advances the session through bounded fixed wall-time requests. The Dashboard,
Controls, Timeline, Core Map, and F1/backquote debug menu are all presentation
and input surfaces over the same runtime. `UnityGameController` owns pacing and
restart; `UnityRuntimePort` maps Unity commands to `GameSession` without
copying its rules.

The retained Unity run provides:

- a selectable 380-channel core map and the 12 bundle positions in a selected
  channel;
- refuelling toward either channel end with four- or eight-bundle shifts;
- shared full-core power, burnup, tilt, RRS, score, and event feedback after
  accepted operations;
- a tactical RRS dashboard plus RUN STAKES values for score, fresh bundles,
  refuelling count, actual power, and reserve headroom;
- bounded real-time pacing at the available playback speeds, with pause,
  single-step, time jumps, inventory grants, pending-action clearing, and
  restart in the owner debug menu;
- terminal RRS handling that stops automatic advancement, shows the
  authoritative terminal reason, and lets the owner restart the practice run.

The finite fuel budget is `FreshBundlesAvailable`. The separate
`RefuelRequestsRemaining` value is a scenario/runtime counter and must not be
presented as fuel inventory. The terminal authority is the RRS projection's
`IsGameOver` and `GameOverReason`, which are carried through the Unity snapshot
as `snapshot.Rrs`.

The browser playtest is useful for live interaction and bridge playtesting, and
the Vercel deployment is the owner-facing acceptance surface. Unity remains
available for retained-surface checks and shared-change validation, not as the
primary product surface.

## Gameplay bargain and player loop

The player repeatedly:

- keeps the run moving at a selected speed and reads the Dashboard's RRS
  reserve, actual power, score, inventory, and operation count;
- selects a channel and inspects its bundle positions, burnup, local power,
  and tilt on the Core Map;
- chooses a channel, direction, fuel type, and shift size, then commits the
  refuelling operation;
- observes the authoritative bundle movement, power/RRS response, discharged
  burnup, score, and event history;
- uses the next interval to keep reserve away from either boundary while
  spending the finite fresh-bundle inventory deliberately;
- restarts after terminal RRS exhaustion and tries to improve the next run.

The UI may explain current state and consequences already present in a
snapshot, but it must not invent a fuel budget or future state. The current
practice path exposes xenon only as the explicit unavailable/static status
described below.

## Runtime architecture and boundaries

Keep simulation independent from Unity and keep the browser a consumer of the
same application contract:

```text
Vercel-deployed Phaser 3 browser playtest  Retained/frozen Unity views and controls
                    |                                  |
             versioned browser bridge              UnityRuntimePort
                    |                                  |
                         GameSession
                              |
                 ReactorSim.Core state and solver
                              |
              project-authored pack first; offline
                 DRAGON5/DONJON5 pack later
```

`ReactorSim.Core` owns deterministic state transitions, bundle inventory,
burnup, topology, regulating-system contracts, and spatial solving.
`ReactorSim.Game` owns reusable session construction, player commands, and
immutable presentation projections. Unity and Phaser format those projections
and dispatch input; neither presentation layer duplicates simulation rules.

DRAGON5 and DONJON5 remain offline tools. Runtime code consumes compact,
versioned data packs and never launches either executable. Preserve units,
energy-group ordering, topology identity, pack checksums, source identity, and
licensing boundaries when physics data changes.

## Current physics and provenance

The practice session loads the project-authored
`candu6-two-group-diffusion-v1-infinite-cell-calibrated` pack. It is an
infinite-cell-calibrated surrogate with explicit provenance, not a plant rating
and not an external DRAGON5/DONJON5 result.

The shared two-group full-core path currently:

- binds the live 380-channel by 12-bundle inventory to the coefficient table;
- solves a deterministic static k-eigenmode shape and normalizes it to the
  requested SI watt target;
- exposes total, channel, and bundle watts, power amplitude, effective `k`,
  and `rho = (k - 1) / k`, together with solve identity and convergence/
  balance diagnostics;
- re-solves after a committed refuelling operation and on the scheduled
  full-core cadence, while shorter intervals reuse the retained equilibrium
  shape for deterministic burnup integration.

This is a regulated steady-state practice projection. Its short-interval
behavior is not a sub-second kinetics or transient claim. The existing Core
tree contains iodine/xenon contracts for later validated work, but the current
`GameSession` practice projection deliberately uses
`xenon-unavailable-static-compatibility-v1`, reports `HasCoupling = false`, and
does not expose coupled xenon dynamics to gameplay.

The application snapshot keeps the authoritative values together: `Rrs`,
`ScoreTotal`, `FreshBundlesAvailable`, `RefuellingOperationCount`,
`ActualPowerFraction`, selected-core data, and terminal RRS state. Changes to
the UI should consume those values rather than adding a second calculation or
altering Core/GameSession schemas for presentation convenience.

The following public references provide design context for topology, fuelling,
safety, and modelling. They do not authorize copying source values into
runtime constants or redistributing vendor data:

- [IAEA CANDU energy-system supplement](https://nucleus.iaea.org/sites/INPRO/df7/Session%202/Vendor%205/02Supplement2_Candu_Energy.pdf)
- [IAEA Advanced Reactors Information System: CANDU overview](https://www-pub.iaea.org/MTCD/Publications/PDF/te_1444_web.pdf)
- [IAEA heavy-water reactor technology report](https://www-pub.iaea.org/MTCD/Publications/PDF/te_699_web.pdf)
- [Polytechnique Montréal CANDU reactor physics thesis repository](https://publications.polymtl.ca/5048/)
- [Polytechnique Montréal DRAGON5 information and manuals](https://merlin.polymtl.ca/version5.htm)
- [DRAGON v5 user guide](https://merlin.polymtl.ca/downloads/IGE335.pdf)
- [TRIVAC v5 diffusion solver manual](https://merlin.polymtl.ca/downloads/IGE369.pdf)
- [DONJON v5 reactor analysis manual](https://merlin.polymtl.ca/downloads/IGE344.pdf)

## Unity implementation notes

Keep the retained scene runnable when a shared change requires it, but do not
use Unity as the primary acceptance path. Unity presentation, input, gameplay,
and polish feature work is paused during the web-first phase. The important
retained seams are:

- `UnityGameController` initializes the real session, binds views, accumulates
  unscaled wall time, dispatches bounded fixed requests, and clears pacing
  backlog when the RRS snapshot is terminal;
- `UnityRuntimePort` projects the application snapshot into the Unity-facing
  contract and maps accepted/rejected commands;
- `Phase10DashboardView` formats the existing RRS dashboard and RUN STAKES
  surface. Terminal copy and restart are presentation responses to the RRS
  projection, not new state transitions;
- `Phase10ControlsView` sends power, tilt, playback, pause/resume, and
  refuelling commands through the adapter;
- `CoreMapView` displays the authoritative channel/bundle projection and
  commits refuelling through the adapter;
- `Phase10TimelineView` and `DebugMenuView` expose current events and owner
  playtesting controls without becoming simulation owners.

Generated uGUI should stay compact, tactical, high-contrast, and readable at
the Bootstrap layout's supported sizes. Add only the smallest presentation
surface needed to make an existing authoritative value understandable.

## Browser playtest notes

The browser project is the primary product and is launched independently from
Unity. The deployed Vercel site is the primary acceptance path. For local
development:

```powershell
cd web/candu-playtest
npm install
npm run dev
```

The visible client is a fixed 1600 × 900 Phaser 3 tactical viewport using
Phaser `Scale.FIT`. It sends only protocol commands to the worker-hosted
bridge. If the bridge is absent, the title and unavailable scenes explain the
locked state and disable simulation controls. Local command history, state
digests, replay JSON, and feedback notes are appropriate companion tooling;
they are not a second authority.

For a production-shaped browser build, stage the bridge from the repository
root and run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm test
npm run build
```

## Next priorities

Work on these priorities in order, keeping the live browser loop and Vercel
deployment first:

- Tune browser dashboard readability, terminal feedback, refuelling feedback,
  pacing, accessibility, and debug/playtest controls using the existing
  snapshot values.
- Improve operation feedback and owner diagnostics while leaving authority in
  `GameSession`; do not add presentation-owned simulation rules to make the
  loop legible.
- Validate scoring and practice pacing against stable power, RRS reserve,
  finite inventory, operation count, and useful discharged burnup with focused
  checks and owner playthroughs of the deployed web game.
- Prepare and admit a compact offline DRAGON5/DONJON5-derived pack behind the
  existing data seam. Record source identities, tool/data versions, deck or
  export hashes, units, group ordering, topology, convergence, and power
  balance before comparing gameplay trends.

Unity feature development remains paused during this phase. Additional
reactor-model detail can follow as a separate validated data/model effort. It
is not the next gameplay requirement and must not displace the readable browser
refuelling loop.

## Testing and launch checks

Install Unity `6000.3.21f1` and a .NET `10.0.3xx` SDK. From the repository root:

```powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
```

The focused runners are:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-DotNet.ps1
powershell -ExecutionPolicy Bypass -File tools/Test-Browser.ps1
powershell -ExecutionPolicy Bypass -File tools/Test-UnityImport.ps1
```

`Test-DotNet.ps1` runs the available .NET suites by default and accepts
`-Suite Core`, `-Suite Game`, `-Suite Browser`, or `-Suite All`. The browser
runner expects installed dependencies and runs the bridge suite, Vitest, and
the production build. The Unity runner discovers the pinned editor or accepts
`-UnityEditorPath`, prepares the simulation DLLs, and runs the import/compile
and Bootstrap smoke.

Automated checks should protect only the behavior being changed. The owner's
functional acceptance is a playthrough of the deployed Vercel browser game:
launch the playtest, run time, inspect channels, refuel in both directions,
observe power/RRS/score/inventory feedback, exercise the browser debug/playtest
controls, reach terminal RRS state when practical, and restart the run. Use the
Unity runner only for retained-surface checks or when a shared engine-neutral
change requires Unity validation.
