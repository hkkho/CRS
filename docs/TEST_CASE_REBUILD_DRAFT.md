# Behavioral test rebuild plan

Status: active rebuild. The legacy cases are archived under
`archive/test-cases/legacy/2026-09-07/`. The first vertical Game replacement
suite is implemented in `tests/ReactorSim.Game.Tests`. The Core topology,
inventory, refuelling, and stale-binding slice is implemented in
`tests/ReactorSim.Core.Tests/TopologyInventoryRefuellingTests.cs`. The Core
power, burnup, and spatial slice is implemented in
`tests/ReactorSim.Core.Tests/BurnupAndPowerTests.cs` and
`tests/ReactorSim.Core.Tests/SpatialSolveTests.cs`. The Core kinetics, xenon,
control, and determinism slice is implemented in
`tests/ReactorSim.Core.Tests/KineticsXenonControlDeterminismTests.cs`;
the Browser bridge and web UI seam slices are implemented in
`tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs` and
`web/candu-playtest/src/uiSlice.test.ts`; serialization, CLI, browser smoke, and
Unity replacement cases remain pending.

## Intent

The current repository contains a large historical suite organized around phase
and task identifiers. This document defines a new test surface from observable
product behavior and explicit simulation contracts. It is a proposal for the
replacement suite, not a promise to preserve the current test names, fixtures,
or expected values.

The primary acceptance target remains a playable steady-state CANDU refuelling
loop:

1. inspect the core and a channel's twelve bundles;
2. preview a four- or eight-bundle shift toward either end;
3. commit the shift and observe bundle, power, burnup, xenon, and score changes;
4. run and pause deterministic simulation time;
5. use the debug menu to reach important playtesting states.

Shutdown, scram, accident progression, operator-training scenarios, and plant
systems outside this loop are not test targets in this rebuild.

## Review rules for every proposed case

Before implementing a case, the author should be able to fill in all of these
fields in one or two sentences:

- **Setup:** the smallest explicit fixture or session state needed;
- **Action:** one command, transition, or observation under test;
- **Oracle:** the externally meaningful result, invariant, or diagnostic;
- **Failure boundary:** what must remain unchanged when the action is rejected;
- **Owner:** the lowest layer that can prove the behavior;
- **Priority:** P0 (playability/data integrity), P1 (important confidence), or
  P2 (diagnostic/future physics).

Cases should follow these rules:

- Prefer small manufactured fixtures for algebra and a single real practice
  session for end-to-end behavior.
- Assert identities, conservation, finiteness, ordering, and state digests
  rather than private fields or incidental implementation structure.
- Use exact numeric values only when the fixture deliberately defines them.
  Pack-dependent production values should be checked as trends and invariants.
- Every rejected command should prove atomicity where mutation is possible.
- Every deterministic case should state its seed, command sequence, time
  partition, and digest/observable comparison.
- Do not duplicate the same behavior in Core, Game, browser, CLI, and Unity.
  Higher layers should test mapping and integration, not re-prove the solver.
- Test names should describe behavior, not phase history or an external review
  process.

## Proposed replacement layout

```text
tests/
  ReactorSim.Core.Tests/
    TopologyAndInventoryTests.cs
    RefuellingTransitionTests.cs
    BurnupAndPowerTests.cs
    SpatialSolveTests.cs
    KineticsAndXenonTests.cs
    DeterminismAndArchiveTests.cs
  ReactorSim.Game.Tests/
    PracticeSessionTests.cs
  ReactorSim.Browser.Tests/
    PlaytestProtocolTests.cs
    PlaytestParityTests.cs
  ReactorSim.Cli.Tests/
    CliSmokeTests.cs
  ReactorSim.Golden.Tests/       # opt-in only, if still needed
    AdmittedPackTrendTests.cs

unity/ReactorGame/Assets/ReactorGame.Unity/Tests/
  Editor/RuntimePortAndViewTests.cs
  PlayMode/PracticeLoopSmokeTests.cs

web/candu-playtest/src/
  protocol.test.ts
  reducer.test.ts
  playtest.smoke.test.ts         # only if a browser test harness is retained
```

The exact file split can change. The important boundary is that Core owns
simulation truth, Game owns session orchestration, browser/CLI own adapters, and
Unity owns presentation wiring.

## P0: essential cases

These cases define the replacement safety net as the archived suite is rebuilt.
Each case below is intentionally narrow enough for one bounded implementation
task.

### Core topology, inventory, and refuelling

| ID | Deliberate scenario | Action and oracle | Owner |
| --- | --- | --- | --- |
| CORE-TOPO-001 | Construct the canonical CANDU-6 practice topology. | Assert 380 unique channels, 12 ordered bundle positions per channel, 4,560 unique node locations, valid grid coordinates, and explicit alternating channel flow directions. | Core |
| CORE-TOPO-002 | Inspect both axial boundaries and representative interior channels. | Convert channel/position to node identity and back. Assert the mapping is bijective and does not wrap or silently reorder at an end. | Core |
| CORE-INV-001 | Create a small inventory containing fresh, resident, and discharged bundles. | Assert identity and location uniqueness, explicit fuel type, and conservation of bundle ownership before any shift. | Core |
| CORE-REFUEL-001 | A valid four-bundle shift toward End A. | Assert the exact retained bundle order, four inserted bundle identities, four discharged identities/locations, channel burnup update, inventory decrement, and unchanged non-target channels. | Core |
| CORE-REFUEL-002 | A valid four-bundle shift toward End B. | Assert the opposite endpoint rule and the same conservation properties; do not infer direction from input array order. | Core |
| CORE-REFUEL-003 | Valid eight-bundle shifts toward both ends. | Use deliberately asymmetric bundle identities and burnups. Assert that all eight positions move according to the selected direction and that the discharge partition is exact. | Core |
| CORE-REFUEL-004 | Invalid channel, direction, shift count, fuel type, partial channel, and insufficient fresh inventory. | Each rejection returns a stable diagnostic category and leaves the complete source state unchanged. Split into separate tests if a failure has a different contract. | Core |
| CORE-REFUEL-005 | Preview the same request that will later be committed. | Preview returns projected bundle identities and physics-facing deltas without changing inventory, bundle state, score, xenon, lifecycle version, or digest; commit then applies the intended transition once. | Core/Game |
| CORE-REFUEL-006 | Attempt a refuelling operation after a stale spatial/lifecycle binding. | The candidate is rejected before partial movement; the previously accepted projection and lifecycle binding remain authoritative. | Core |

Implementation status: `CORE-TOPO-001/002`, `CORE-INV-001`,
`CORE-REFUEL-001..004`, and `CORE-REFUEL-006` are covered by the Core behavioral
suite. `CORE-REFUEL-005` is covered by the Game campaign suite because preview
is a public GameSession operation rather than a Core transition.

### Core power, burnup, and spatial solve

| ID | Deliberate scenario | Action and oracle | Owner |
| --- | --- | --- | --- |
| CORE-BURN-001 | One bundle with a known power, mass, and elapsed interval. | Apply one burnup interval and assert the defined energy-to-burnup conversion, monotone cumulative burnup, and exactly one history sample. | Core |
| CORE-BURN-002 | A multi-bundle interval with zero, positive, and invalid powers. | Positive finite power advances burnup; zero power leaves burnup and cumulative energy unchanged; negative, non-finite, stale, or incomplete inputs reject atomically. | Core |
| CORE-SPATIAL-001 | The smallest supported two-node and a symmetric three-node manufactured two-group fixture. | Solve with known boundary conditions and assert convergence, nonnegative finite flux/power, expected symmetry, power normalization, and relative balance error. The public topology contract requires at least two axial positions, so it cannot represent a one-node fixture. | Core |
| CORE-SPATIAL-002 | The same coefficient records supplied in different input orders. | Assert identical assembled operators, solve status, diagnostics, and digest. | Core |
| CORE-SPATIAL-003 | Invalid flux, missing/duplicate coefficients, invalid boundary, and exhausted iteration budget. | Assert fail-closed diagnostics, cleared/unusable candidate state, and no replacement of the last accepted solve. | Core |
| CORE-SPATIAL-004 | The embedded 380-channel × 12-bundle synthetic pack. | Assert topology/data-pack identity binding, 4,560 node coverage, converged authoritative solve, finite nonnegative node powers, and total bundle/channel/total-power sums. | Core |
| CORE-POWER-001 | A solved projection at a requested SI watt setpoint. | Assert reference power, target power, amplitude, shape, total/channel/bundle watts, `k`, and `rho = (k - 1) / k` remain separate and internally consistent; do not expose per-bundle reactivity as a substitute. | Core |
| CORE-POWER-002 | Replace one representative bundle with fresh and then with burned state. | Assert the response direction is observable (fresh and burned do not produce the same state), while the normalized shape and total-power accounting remain valid. | Core |

Implementation status: `CORE-BURN-001/002`, `CORE-SPATIAL-001..004`, and
`CORE-POWER-001/002` are covered by the Core behavioral suite using manufactured
fixtures for local operator behavior and the embedded 4,560-node practice pack
for full-core binding and power accounting.

### Game session and playable loop

| ID | Deliberate scenario | Action and oracle | Owner |
| --- | --- | --- | --- |
| GAME-SESSION-001 | Create the public practice session from the factory. | Assert a finite initial snapshot, scenario/data-pack identity, 380 channels, 12 bundles per channel, selected-channel contract, authoritative physics identity, xenon projection, and starting inventory. | Game |
| GAME-SESSION-002 | Advance a known wall-time interval at the default playback rate. | Assert the documented wall-to-simulation clock conversion, fixed control cadence, monotone time, and finite snapshot values. | Game |
| GAME-SESSION-003 | Pause, advance wall time, single-step/day-jump, and resume with a live speed. | Assert paused wall time does not advance simulation, explicit step/jump works while paused, and selecting a live speed resumes only through the documented command. | Game |
| GAME-SESSION-004 | Preview and commit one channel refuel through `GameSession`. | Assert the command result contains an actionable message and post-commit snapshot, last-refuel fields are correct, fresh inventory decreases once, and the selected channel exposes all twelve updated bundles. | Game |
| GAME-SESSION-005 | Reject a refuel after a deliberately invalid request. | Compare the complete relevant before/after snapshot projection, including Core, physics, xenon, score, inventory, pending-action, and operation-count fields. All must remain unchanged except the rejection result metadata. | Game |
| GAME-SESSION-006 | Advance through the full-core recompute/burnup cadence after a refuel. | Assert the retained node powers feed burnup, the scheduled re-solve updates the projection at the defined boundary, and a one-window advance equals an equivalent partitioned advance. | Game |
| GAME-SESSION-007 | Queue power/tilt targets and run a bounded practice interval. | Assert commands are represented in the next valid snapshot, regulation stays within configured bounds, saturation is visible when forced, and no unrelated Core state is silently changed. | Game |
| GAME-SESSION-008 | Use debug grant, clear-pending, and reset-score-adjustment controls. | Assert each debug command changes only its documented practice controls, is visibly marked as debug state, and does not invent a second simulation rule. | Game |

### Browser and Unity acceptance seams

| ID | Deliberate scenario | Action and oracle | Owner |
| --- | --- | --- | --- |
| BRIDGE-001 | Query capabilities and initialize valid Play and Lab modes. | Assert protocol version, supported operations, explicit units/group ordering, model/data identity, Play topology (380 × 12), and Lab fixture identity. | Browser bridge |
| BRIDGE-002 | Initialize with missing/wrong protocol, unknown mode, malformed JSON, wrong types, or non-finite values. | Fail closed with a useful diagnostic and no active simulation state; do not silently fall back to a second authoritative simulator. | Browser bridge |
| BRIDGE-003 | Dispatch advance, selection, preview-refuel, commit-refuel, and invalid command. | Assert monotonically defined sequence behavior, response snapshot completeness, preview non-mutation, atomic commit, and rejected-command state preservation. | Browser bridge |
| BRIDGE-004 | Run the same Play command stream twice from the same initialization. | Assert identical state and replay digests and equivalent compact snapshots, including selected-channel xenon diagnostics. | Browser bridge |
| BRIDGE-005 | Compare a browser Play command stream with the same `GameSession` stream. | Assert the browser response maps the authoritative session values without reimplementing refuelling or time rules. | Browser/Game |
| BRIDGE-006 | Run a Lab refuel with a converged solve, then force a nonconverged solve. | The first commits atomically; the second reports failure and preserves the prior Lab inventory, bindings, solve state, and digest. | Browser bridge |
| UNITY-001 | Build and bind a `UnityRuntimePort` backed by a real practice session. | Assert an explicit advance and refuel command reach GameSession and that the returned Unity snapshot maps the same operation count, inventory, selected channel, and message. | Unity EditMode |
| UNITY-002 | Feed the controller fractional wall-time and a frame catch-up spike. | Assert fixed 100 ms requests, retained fractional remainder, bounded catch-up work, and no simulation-time drift from the same accumulated wall time. | Unity EditMode |
| UNITY-003 | Load Bootstrap in PlayMode. | Assert a real session is initialized, the Core Map has 380 selectable channels and 12 bundle details, the debug menu is bound and hidden, and the initial snapshot is authoritative and finite. | Unity PlayMode |
| UNITY-004 | Select a channel, preview, commit toward each end, pause/resume, and open the debug overlay. | Assert player-visible status/result updates and state changes through the actual views; one smoke path should cover the primary loop rather than asserting every label/layout detail. | Unity PlayMode |

Implementation status: `BRIDGE-001..006` are complete as of 2026-09-08 in the
six facts in `tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs`. Unity seam
replacement cases remain pending.

## P1: important confidence cases

These should follow the P0 slice and protect the simulation seam without
turning routine development into a historical physics gate.

### Kinetics, xenon, control, and determinism

| ID | Deliberate scenario | Oracle |
| --- | --- | --- |
| CORE-KIN-001 | Start at zero relative reactivity with equilibrium precursors. | Amplitude and precursor state remain at equilibrium under one or more valid steps; all values are finite and nonnegative. |
| CORE-KIN-002 | Apply small positive and negative reactivity perturbations. | The response direction is correct, bounded by the configured integration contract, and repeatable when the same step is replayed. |
| CORE-XE-001 | Start with a zero xenon overlay, then localize one node's iodine/xenon state. | Zero overlay preserves the base solve; a localized perturbation changes only the intended node/bundle overlay and the coupled digest; no negative or non-finite number densities appear. |
| CORE-XE-002 | Advance a small spatial xenon fixture through burnout and buildup windows. | The prescribed early burnout/later buildup trend is finite, deterministic, and bound to the same simulation time as burnup and power. |
| CORE-CONTROL-001 | Give the practice regulator a positive/negative perturbation and an over-range perturbation. | Compensated net reactivity moves toward the target; command/state stay within bounds; saturation is explicit; invalid input fails closed. |
| CORE-DETERMINISM-001 | Replay one refuel-plus-time command stream with different wall-time partitions. | Final canonical digest and all player-visible observables match. |
| CORE-DETERMINISM-002 | Shuffle only input collections whose order is declared non-semantic. | Result identity, diagnostics, and digest are unchanged; order-sensitive collections remain explicitly tested as order-sensitive. |

Implementation status: complete as of 2026-09-08. The seven facts in
`tests/ReactorSim.Core.Tests/KineticsXenonControlDeterminismTests.cs` cover
`CORE-KIN-001/002`, `CORE-XE-001/002`, `CORE-CONTROL-001`, and
`CORE-DETERMINISM-001/002` through public Core and Game session seams.

### Serialization and CLI wiring

| ID | Deliberate scenario | Oracle |
| --- | --- | --- |
| ARCHIVE-001 | Save an accepted session/archive and load it into a new session. | Canonical serialization round-trips and replay reaches the same digest without requiring the original live object. |
| ARCHIVE-002 | Tamper with version, binding identity, initial state, command order, or numeric token. | Load/replay fails before mutation and reports the first actionable diagnostic. |
| CLI-001 | Run the minimum headless path: create, inspect, advance, refuel, inspect. | Process exits successfully, stdout is stable enough for human use, errors stay off stdout, and the output reflects the same session behavior already proved by Game. |
| CLI-002 | Run one archive save/load/replay command path. | CLI is verified as an adapter to the shared archive/session contract, not as a second simulation implementation. |

### Browser UI reducer and browser smoke

| ID | Deliberate scenario | Oracle |
| --- | --- | --- |
| WEB-UI-001 | Select an in-range channel, preview it, commit it, then clear preview. | Reducer stores the selection/preview, records the accepted response, and clears stale preview after commit. |
| WEB-UI-002 | Select an out-of-range channel or receive a malformed snapshot. | Reducer preserves the valid prior state and does not expose an invalid channel as selected. |
| WEB-UI-003 | Mark the authoritative WASM bridge unavailable. | Simulation controls are disabled, the unavailable bridge is identified, and no compatibility fixture is presented as authoritative. |
| WEB-UI-004 | Export/import a local command history and replay metadata. | Local-only archive preserves protocol identity and commands; malformed or different-version archives are rejected. |
| WEB-SMOKE-001 | Start the dev server and exercise initialize, inspect, preview, commit, pause, and resume in a real browser. | Page loads without console errors, controls remain responsive, the core surface is visible, and the visible result matches the bridge response. |

Implementation status: `WEB-UI-001..004` are complete as of 2026-09-08 in
`web/candu-playtest/src/uiSlice.test.ts`. `WEB-SMOKE-001` remains pending for the
production-shaped deployed-browser clock slice.

## P2: opt-in or future data-pack cases

These cases are useful when the offline physics/data work changes. They should
not be required for an ordinary gameplay change or be used as a release gate.

| ID | Deliberate scenario | Oracle |
| --- | --- | --- |
| DATA-001 | Load a runtime pack with explicit version, source identity, checksum, topology digest, units, branch grid, and two-group order. | Metadata is preserved at the snapshot boundary and a mismatched identity cannot bind to an existing state. |
| DATA-002 | Remove, duplicate, reorder, or make non-finite a coefficient row. | Admission fails closed with a data diagnostic before any session state is created. |
| DATA-003 | Query exact, bracketed, and out-of-domain burnup points. | Exact rows are selected, interior values interpolate according to the declared method, and out-of-domain values reject rather than clamp silently. |
| DATA-004 | Compare a project-authored synthetic pack with a later lawfully admitted DRAGON5/DONJON5-derived pack. | Compare trends and invariants (normalization, conservation, symmetry/refuelling direction, finite values), not undocumented plant-rating numbers. |
| DATA-005 | Verify runtime packaging with analysis tools absent. | Unity/Game/browser consume the compact pack only; no runtime path invokes DRAGON5 or DONJON5 executables. |
| GOLDEN-001 | Reproduce one small, versioned manufactured or synthetic authority fixture. | The fixture is independently understandable, has provenance, and is explicitly diagnostic/opt-in rather than a permanent approval gate. |

## Cases intentionally not carried forward automatically

The following existing categories should be deleted or rewritten from behavior,
not copied under new names:

- phase/task-numbered test families whose main oracle is an old implementation
  milestone (`P3T*` through `P8T*`, `G2C01`, and `P10T*`);
- broad duplicated contract tests that assert the same invariant at several
  internal layers;
- historical candidate/approved/literature golden comparisons whose values are
  not the current gameplay contract;
- long soak/policy suites used as routine pass/fail gates;
- tests of test infrastructure itself unless the infrastructure remains a
  supported developer interface;
- exact UI text, button counts, or hierarchy details that do not prove the
  playable refuelling behavior;
- compatibility-fixture tests that allow browser simulation to appear
  authoritative when the WASM bridge is absent;
- shutdown, scram, accident, and full-plant behavior outside the stated game
  scope.

Existing data files should only survive the rebuild when a new case names the
specific contract they prove. A file should not be retained merely because an
old test referenced it.

## Implementation order

1. Run and stabilize the new vertical Game campaign suite. It deliberately
   covers `GAME-SESSION-003..006` through preview, commit, rejection atomicity,
   and partition-deterministic burnup/xenon evolution.
2. Implement the P0 Core cases, then the browser/Unity seams. Do not copy
   archived assertions mechanically.
3. Run the new focused suite and one real Unity PlayMode smoke.
4. Restore an archived fixture only when a new behavioral case names the exact
   contract it proves.
5. Add P1 cases only when a P0 contract is stable; keep P2 cases opt-in.

## Questions for independent LLM reviewers

1. Does every P0 case protect a player-visible or state-integrity behavior, or
   is any case still a historical implementation test in disguise?
2. Are the proposed fixtures small enough that a failure identifies one cause?
3. Is any oracle over-specified for the current synthetic physics pack or
   accidentally asserting real-reactor values that the repository does not
   claim to model?
4. Are preview, commit, rejection atomicity, deterministic time partitioning,
   and browser/Unity authority boundaries covered without duplicating the same
   assertion at every layer?
5. What essential steady-state refuelling behavior is still missing while
   shutdown/scram/accident scope remains excluded?
