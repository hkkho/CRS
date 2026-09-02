# CANDU Refuelling Game implementation guide

## Product direction

Build a fun, responsive Unity game about keeping a CANDU reactor at steady
power through on-power refuelling. The player should inspect channel and bundle
conditions, choose where and how to refuel, watch the core respond continuously,
and learn the cause-and-effect relationship between fuel history, flux shape,
xenon, reactivity control, and power production.

The project should become a game before it becomes a high-fidelity reactor
model. Use the existing synthetic model to complete the player loop, then replace
or tune synthetic coefficients with lawful, reproducible DRAGON5/DONJON5-derived
data. Scram, shutdown, accident response, and full plant simulation are out of
scope.

## Implementation status

The playable synthetic vertical slice is implemented. `ReactorSim.Game` creates
a public synthetic practice session, `UnityRuntimePort` connects it to the Unity
adapter, and `UnityGameController` binds the Bootstrap views and advances the
session in bounded fixed wall-time requests. The Controls page can execute
internally consistent four- or eight-bundle shifts toward either channel end,
while the Core Map presents the deterministic 380-channel face, 12-bundle
details, preview, and commit actions. Refuelling also produces a deterministic
localized power, tilt, and score response. The owner debug menu is available
from F1 or backquote for pause, time control, restart, inventory, pending-action,
and synthetic-response controls.

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

The first Unity runtime may use the current lightweight Phase 8 response model.
Introduce the full spatial/refuelling simulation behind the same game-session
API incrementally. This keeps a runnable build available throughout development.

## Ordered implementation

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
- For this milestone, use a deliberately simple synthetic refuelling response
  if integrating the complete Core transaction would delay playability. Bundle
  movement and burnup must still be internally consistent.
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
- grant fuel inventory, clear pending actions, reset the synthetic response, and
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

### Milestone 4: connect the existing detailed Core model

- Adapt the existing `RefuellingShift`, inventory, burnup, spatial solve,
  iodine/xenon, and regulating-system contracts into `GameSession` one subsystem
  at a time.
- Run expensive spatial recomputation on simulation cadence, not every rendered
  frame. Publish immutable presentation snapshots to Unity.
- Add predicted-delta calculations using a cheap approximation for hover/preview;
  reserve the detailed solve for committed operations and scheduled updates.
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

The data pass is complete when changing from `SyntheticPractice` to
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

Do not start with new DRAGON5/DONJON5 runs. The next implementation work should
extend the playable loop or its owner diagnostics without changing the Unity
presentation seam.
