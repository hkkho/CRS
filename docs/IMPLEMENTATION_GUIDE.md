# CANDU Refuelling Game implementation guide

## Product direction

Build a fun, responsive Unity game about keeping a CANDU reactor at steady
power through on-power refuelling. The player should inspect channel and bundle
conditions, choose where and how to refuel, watch the core respond continuously,
and learn the cause-and-effect relationship between fuel history, flux shape,
xenon, reactivity control, and power production.

The project should become a game before it becomes a high-fidelity reactor
model. Use the existing deterministic model to complete the player loop, then
replace the pre-calibration coefficients with lawful, reproducible
DRAGON5/DONJON5-derived data. Scram, shutdown, accident response, and full
plant simulation are out of scope.

## Implementation status

The playable deterministic vertical slice is implemented. `ReactorSim.Game`
creates a public practice session, `UnityRuntimePort` connects it to the Unity
adapter, and `UnityGameController` binds the Bootstrap views and advances the
session in bounded fixed wall-time requests. The Controls page can execute
internally consistent four- or eight-bundle shifts toward either channel end,
while the Core Map presents the deterministic 380-channel face, 12-bundle
details, preview, and commit actions. Refuelling and scheduled burnup updates
now rebind the shared two-group full-core solve, which publishes explicit
watts/k/rho and solver diagnostics to both front ends. The owner debug menu is
available from F1 or backquote for pause, time control, restart, inventory,
pending-action, and practice-score controls.

The browser algorithm/playtest pivot is also part of the implementation path.
The companion Vite/Three.js console in `web/candu-playtest` exercises the same
public `GameSession` contract through a versioned JSON bridge when browser WASM
is available. Play mode now reports the shared 380 × 12 two-group full-core
solve; Lab mode remains an opt-in small-fixture spatial-solver surface that
reuses Core contracts. The console records command history, state digests,
replay JSON, and feedback notes locally so the owner can give dynamic UI and
algorithm feedback before a Unity presentation change is justified. A
compatibility fixture may render the console when WASM is unavailable. It is
marked non-authoritative and is not a second simulator.

### Physics migration plan: reduced contract to validated CANDU design

The current power and reactivity values are a deliberate migration layer. They
make the player loop observable without hiding units or allowing presentation
code to invent reactor state:

1. **Contract and authority (implemented):** publish explicit SI watts for the
   reference, target, total, channel, and bundle quantities; carry source and
   solve identity; keep amplitude separate from normalized shape; derive only
   state-level reactivity from `k`; and treat a rejected/stale detailed solve as
   non-authoritative.
2. **State binding and full-core diffusion (implemented initial slice):**
   assemble the canonical stepped 380-channel × 12-bundle topology, bind each
   live bundle's burnup to a validated two-group coefficient table, and solve
   the resulting node system through `SpatialEigenSolve`. The converged shape
   is normalized to the requested SI watt target, checked for nonnegative finite
   power and balance, and retained for deterministic burnup integration. A
   committed refuelling operation solves immediately; normal operation re-solves
   on the one-hour simulation cadence. Per-bundle `rho` is intentionally not
   exposed.
3. **Authoritative two-group data admission (next):** replace the
   `synthetic-precalibration` pack with an offline DRAGON5 lattice/depletion and
   DONJON5/TRIVAC core-follow export. Keep the runtime schema, group ordering,
   units, topology digest, pack checksum, and provenance visible so changing
   packs does not change Game or Unity rules.
4. **Kinetics and poisons (next):** add the existing iodine/xenon and delayed
   neutron contracts behind the same snapshot boundary. Validate that power
   history, burnup, poison state, and control actions share one simulation clock;
   keep preview calculations cheap and reserve the detailed solve for committed
   or scheduled updates.
5. **Calibration and gameplay validation (next):** compare conservation,
   symmetry, refuelling-direction, replay, and long-horizon invariants against
   small synthetic cases first. Then compare trends—not individual display
   numbers—when a lawful runtime pack is introduced. Record pack version,
   energy-group ordering, units, branch grid, source identity, and export hash.

The following references are design context only. They inform the topology,
refuelling, safety, and modelling questions to resolve; they do not authorize
copying source values into runtime constants or redistributing vendor data:

- [IAEA CANDU energy-system supplement](https://nucleus.iaea.org/sites/INPRO/df7/Session%202/Vendor%205/02Supplement2_Candu_Energy.pdf)
- [IAEA Advanced Reactors Information System: CANDU overview](https://www-pub.iaea.org/MTCD/Publications/PDF/te_1444_web.pdf)
- [IAEA heavy-water reactor technology report](https://www-pub.iaea.org/MTCD/Publications/PDF/te_699_web.pdf)
- [Polytechnique Montréal CANDU reactor physics thesis repository](https://publications.polymtl.ca/5048/)
- [Polytechnique Montréal DRAGON5 information and manuals](https://merlin.polymtl.ca/version5.htm)
- [DRAGON v5 user guide](https://merlin.polymtl.ca/downloads/IGE335.pdf)
- [TRIVAC v5 diffusion solver manual](https://merlin.polymtl.ca/downloads/IGE369.pdf)
- [DONJON v5 reactor analysis manual](https://merlin.polymtl.ca/downloads/IGE344.pdf)

## Repository review

### What already exists

- `ReactorSim.Core` is a large engine-neutral C# model. It includes 380-channel,
  12-bundle topology contracts; bundle inventory and burnup; refuelling shifts;
  a two-group spatial solver; iodine/xenon state; simplified regulating-system
  behavior; deterministic scenarios; scoring; serialization; and replay.
- `ReactorSim.Cli` proves that synthetic scenarios can be created, advanced,
  inspected, scored, saved, loaded, and replayed. Its useful runtime assembly
  logic is currently private to `CliApplication` rather than exposed as a
  reusable game service.
- `unity/ReactorGame` targets Unity `6000.3.21f1`. It has a Bootstrap scene, a
  four-page uGUI shell, dashboard/controls/timeline views, and an
  `IPhase8RuntimePort` adapter. The Core DLL is copied into Unity by
  `tools/Prepare-UnityCore.ps1`.
- The data tree contains synthetic scenario and scoring packs plus extensive
  comparison fixtures. The reference tree records public literature context and
  partial DRAGON5/DONJON5 route research.

### What remains after the Milestone 1 game loop

1. The current UI still needs presentation polish such as bundle movement
   animation, feedback audio, contextual tooltips, and a fuller start flow.
2. The debug menu intentionally exposes the first useful owner controls; save/load,
   richer state overrides, and additional diagnostics can be added later.
3. The physics/data work is much deeper than the game integration and is not yet
   a complete runtime-ready DRAGON5/DONJON5 data pipeline.

The fastest route is to reuse the good engine-neutral code, extract reusable
runtime construction from the CLI, and build one vertical slice in Unity. Do
not begin by extending the solver or resolving every reference-data question.

## Target game loop

The reactor runs continuously at selectable speeds (`pause`, `1x`, `10x`, and
`60x`). A shift represents hours or a day of steady operation while presentation
updates remain smooth in real time.

The player repeatedly:

1. reads the core heat map and trend displays;
2. inspects promising channels and their 12 bundle positions;
3. compares burnup, local/channel power, flux tilt, xenon, residence time, and
   predicted post-refuelling effect;
4. chooses channel, fuelling direction, fresh-bundle type, and shift size;
5. previews the result, commits the refuelling operation, and watches bundles
   move through the channel;
6. manages the next interval while automatic regulating controls hold power;
7. earns score for stable power, flat channel powers, useful discharged burnup,
   low fuelling cost, and avoiding control saturation.

The normal game should not intentionally drive the reactor to shutdown. Failure
means an uneconomic or unstable operating run, not a simulated nuclear accident.

## Runtime architecture

Keep the simulation independent from Unity and make Unity responsible for
presentation and input:

```text
Unity views and debug menu
        |
UnityGameController + UnityRuntimePort
        |
GameSession (public reusable application layer)
        |
ReactorSim.Core state transitions and solver
        |
Synthetic pack first -> DRAGON5/DONJON5-derived runtime pack later
```

Create a small application-layer assembly rather than referencing CLI internals
from Unity. Move or extract scenario loading, fixture creation, runtime creation,
snapshot projection, and save/load orchestration from `CliApplication` into a
public `ReactorSim.Game` project that targets a Unity-compatible framework. Both
the CLI and Unity runtime port should call that API.

The first Unity runtime may use the current lightweight Phase 8 scenario and
control contracts, while the shared bundle power and reactivity projection is
already supplied by the full-core diffusion adapter. This keeps a runnable build
available while the data pack is calibrated.

## Ordered implementation

### Milestone 0.5: browser algorithm/playtest pivot (companion surface)

This milestone is deliberately placed before the Unity vertical-slice roadmap
as a fast feedback loop, not as a replacement for Unity.

- Build `web/candu-playtest` as a static Vite + React + TypeScript app with
  native Three.js rendering and an accessible 2D fallback.
- Use the versioned `candu-playtest-v1` boundary. Its browser-facing operations
  are `GetCapabilities()`, `Initialize(requestJson)`, and
  `Dispatch(commandJson)`. JSON responses carry a protocol version, model/data
  identity, explicit SI units and two-group ordering, deterministic state
  digests, and finite numeric values.
- Keep Play mode backed by `PracticeGameSessionFactory`/`GameSession`, including
  the 380-channel, 12-bundle inspection and preview/commit flow. Do not copy
  those rules into TypeScript.
- Make Lab mode invoke the existing Core spatial contracts on a small explicit
  fixture first, then expose the full synthetic core as an opt-in diagnostic
  view. A refuel is atomic: inventory and bindings are updated together,
  coefficients are rebuilt/rebound, and a failed or nonconverged solve leaves
  the prior state active.
- Run the bridge in a Web Worker so the browser remains interactive. In
  development or with an explicit compatibility-debug URL, absent WASM may
  render clearly labelled compatibility state; production must fail closed,
  identify the unavailable authoritative bridge, and disable simulation
  controls.
- Provide local-only feedback notes, state digest, command-history/replay JSON
  export, and localStorage persistence. Do not add a backend, authentication,
  telemetry, or public real-reactor data.
- Verify with bridge serialization and parity tests, solver/refuelling tests,
  frontend reducer/protocol tests, a production Vite build, and an
  `agent-browser` smoke check of the running dev server. Deploy the built static
  directory with Vercel only after the local build is green.

Done means the owner can open the browser console, run a Play trace, inspect and
preview a channel, opt into Lab diagnostics, export a reproducible local replay,
and describe what should or should not be carried into Unity. Unity remains the
primary acceptance path for the actual game.

### Milestone 0: make the project easy to launch

- Keep the SDK patch-flexible through `global.json` and verify Core builds.
- Add one script that builds Core, copies the DLL, and opens the correct Unity
  project/Bootstrap scene.
- Add a simple title/start overlay with `Practice` as the default mode.
- Remove phase/task identifiers from player-facing logs and errors.

Done means a contributor can launch the scene from a clean checkout without
manually copying assemblies or discovering paths.

### Milestone 1: playable synthetic vertical slice (implemented)

- Extract `GameSessionFactory` and `GameSession` from the CLI runtime setup.
- Implement `UnityRuntimePort : IPhase8RuntimePort` using a real `GameSession`.
- Add `UnityGameController` that creates the session, binds all views, and uses
  unscaled `Update()` time to dispatch bounded wall-time increments.
- Keep simulation stepping deterministic: accumulate frame time and advance in
  fixed wall-time chunks; cap catch-up work per frame so the UI stays responsive.
- Add a new game command for `RefuelChannel(channel, direction, shiftCount,
  fuelType)` and return a post-command snapshot plus a player-readable result.
- Use the shared full-core diffusion projection for bundle movement,
  state-derived power, and cumulative burnup. Until an offline export is
  admitted, the embedded pack must remain explicitly labelled
  `synthetic-precalibration`; no additive per-refuelling response or generic
  decay timer should stand in for bundle state.
- Replace the Core Map placeholder with a selectable 380-channel heat map. A
  details panel should show all 12 bundles and a predicted outcome before commit.
- Animate bundle insertion/movement/discharge and immediately update power,
  burnup, score, trends, and event history. The current slice updates these
  values synchronously; animation and richer trend presentation remain polish.

Done means the player can run time, inspect the core, choose and execute several
refuelling operations, see understandable consequences, and restart a run.

### Milestone 2: debug menu and owner playtesting

Build this immediately after the vertical slice, before visual polish or physics
tuning. The first menu slice is implemented and toggles with backquote/F1. It
currently includes:

- pause, single-step, time scale, restart, and deterministic seed;
- add simulated days/hours and jump to an equilibrium-like state;
- grant fuel inventory, clear pending actions, reset the practice score adjustment, and
  copy a compact bug-report state digest;
- display raw selected-channel/bundle values and the last command/result.

Future debug additions include direct burnup/residence-time bands, explicit
power/tilt/xenon/control-margin/device overrides, heat-map layer toggles, and
save/load snapshots.

Debug operations may bypass normal game rules but must be visibly marked and
kept out of release scoring. This menu is the primary feature-verification tool.

The first owner-playtesting menu is now present. The remaining work is to grow
its state overrides and diagnostics as new gameplay systems are added. Done
means the project owner can reach every important gameplay state in less than a
minute without editing JSON or using a debugger.

### Milestone 3: make the loop fun

- Replace direct numeric controls with operational decisions and clear previews.
- Add channel recommendations as optional assistance, not automatic play.
- Make tradeoffs legible: flatten power now versus burnup utilization, fuelling
  cost, xenon evolution, and future channel quality.
- Use short objectives and escalating steady-state scenarios: guided practice,
  maintain full power, correct a tilt, optimize discharge burnup, and sustain an
  equilibrium campaign.
- Add immediate visual/audio feedback, tooltips, accessible color palettes, and
  readable trend annotations.
- Tune scenario length so a useful practice run lasts roughly 10-20 minutes,
  with longer sandbox/campaign modes available through acceleration.

Done means a player can explain why a refuelling choice helped or hurt and wants
to improve their score on another run.

### Milestone 4: connect the existing detailed Core model (initial slice implemented)

- Adapt the existing `RefuellingShift`, inventory, burnup, spatial solve,
  iodine/xenon, and regulating-system contracts into `GameSession` one subsystem
  at a time.
- Run expensive spatial recomputation on simulation cadence, not every rendered
  frame. Publish immutable presentation snapshots to Unity.
- Add predicted-delta calculations using a cheap approximation for hover/preview;
  reserve the detailed solve for committed operations and scheduled updates.
- Replace the pre-calibration pack with an admitted offline export only after
  the adapter proves topology/order, normalization, convergence, and
  power-balance invariants. Keep source identity and solve state visible in
  Unity and browser snapshots during the transition.
- Profile before optimizing. Preserve a responsive UI even when simulation time
  is accelerated.

Done means the synthetic gameplay loop is driven by the repository's detailed
engine rather than the temporary response model, with no UI rewrite.

### Milestone 5: DRAGON5/DONJON5 realism pass

Keep DRAGON5 and DONJON5 offline. Unity should load compact, versioned runtime
packs and never invoke reactor-analysis executables.

1. Define the exact CANDU-6 lattice/core case and burnup/state grid needed by the
   game: fuel type, temperatures/densities, coolant state, branch variables,
   energy-group order, homogenization, and units.
2. Use public, lawfully reusable source inputs. Record source URLs/identities,
   tool versions, nuclear-data identities, deck hash, and export script version.
3. Run DRAGON5 lattice/depletion calculations to produce burnup-dependent
   two-group constants and bundle properties for the selected branches.
4. Use DONJON5 for representative full-core diffusion and refuelling histories,
   including channel/bundle power shapes and equilibrium-like operating cases.
5. Export only the compact numerical fields the runtime consumes. Convert units
   once, preserve group ordering explicitly, and keep raw/vendor data external
   when redistribution rights are unclear.
6. Add a data-pack adapter behind `GameSession`, compare the resulting gameplay
   trends with the synthetic pack, and tune display scaling, scenario pacing,
   score weights, and approximations.

The data pass is complete when changing from the current pre-calibration pack to
`Candu6DragonDonjon` requires selecting a pack, not changing gameplay code.
Physics provenance is documentation for reproducibility and lawful reuse; it is
not an approval gate that blocks ordinary game development.

## Minimal testing policy

Automated tests exist to catch a wrong implementation quickly, not to establish
project gates. For each player-visible feature, add only the smallest focused
test that protects its essential state transition. The initial target is:

1. one Core test proving a refuelling command moves/inserts/discharges the right
   bundles and updates burnup/inventory consistently;
2. one application-layer test proving `GameSession` advances and returns the
   expected post-refuelling snapshot;
3. one Unity EditMode test proving the concrete runtime port maps a refuelling
   command and snapshot correctly;
4. one Unity PlayMode smoke proving Bootstrap starts a real session and the Core
   Map and debug menu are bound and usable.

Run the focused affected tests while implementing. The full legacy suite and
reference comparisons are optional diagnostics for deep physics/data changes,
not routine completion requirements. Do not create gate reports, owner-approval
records, independent-review tasks, test-evidence documents, or per-task audit
files.

The practical acceptance check is an owner playtest using a development build:
launch, run time, inspect channels, refuel in both directions, observe the
response, exercise each debug control, save/load, and restart.

## Initial implementation slice (completed)

Start with these concrete files and responsibilities:

- new `src/ReactorSim.Game`: reusable session construction and commands;
- `src/ReactorSim.Cli/CliApplication.cs`: consume `ReactorSim.Game` instead of
  owning runtime construction;
- new `unity/ReactorGame/Assets/ReactorGame.Unity/UnityRuntimePort.cs`: concrete
  port over `GameSession`;
- new `UnityGameController.cs`: startup, binding, fixed-step real-time advance;
- extend `Phase8UnityRuntimePort.cs`: refuelling command and selected-channel
  presentation data;
- new `CoreMapView.cs`: heat map, channel selection, bundle details, preview;
- new `DebugMenuView.cs`: state controls and compact event log;
- update `Bootstrap.unity`: wire the controller and views.

Do not invoke DRAGON5/DONJON5 at runtime. The next data work may produce an
offline export pack, while gameplay changes continue through the existing Unity
and browser presentation seam.
