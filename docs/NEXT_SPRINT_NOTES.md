# Next orchestrator handoff

Status: canonical implementation handoff, refreshed 2026-09-11.

Use this document to start the next orchestrator chat. `README.md` and
`docs/IMPLEMENTATION_GUIDE.md` remain useful architecture references, while
`gemini_review.md`, `review.md`, and the phase/task-numbered documents are
historical review evidence rather than current task trackers.

## Product boundary

Build the playable Unity steady-state CANDU refuelling game first. The player
must be able to inspect the 380-channel core, issue direct four- or eight-bundle
shifts in either direction from the main operations panel, advance time, keep
the automated RRS inside its playable operating band, see power/burnup/RRS
and score respond, and use a debug menu to reach useful playtest states. The
existing predictor/preview-and-confirm flow is transitional work to remove
from the player-facing path; a direct refuelling order should be immediately
accepted or rejected with a clear result.

Keep shutdown, scram, accident progression, and operator-training scenarios
out of scope. Keep `ReactorSim.Core` engine-neutral and authoritative for state
transitions. Unity and browser code are presentation/input layers. DRAGON5 and
DONJON5 remain offline tools; runtimes consume only compact, versioned packs.

The next cut is a player-facing vertical slice, not a physics expansion. The
automated 14-zone RRS is the primary survival/resource meter: the UI must show
compact zone status, the selected zone's target/measured/error values, explicit
warnings, and a visible game-over state when average fill reaches 0% or 100%.
Unity is the product and acceptance surface; `web/candu-playtest` is the fast
iteration and interaction proxy over the same Game/Core contracts.

## Audit result

### Implemented and not to be repeated

- The Unity practice loop, selectable 380-channel map, 12-bundle inspection,
  preview/commit, score feedback, fixed-step advancement, and debug overlay are
  present.
- The shared Game/Core path uses the full-core two-group shape, an explicitly
  adiabatic/static-eigen formulation identity, generalized delayed-source
  groups, a bounded practice regulator, a reference adjoint, adjoint-weighted
  perturbation reactivity, and spatial iodine/xenon coupling.
- Browser Play uses the shared `GameSession` through the WASM bridge and fails
  closed when that bridge is unavailable. Browser-local replay storage is not
  a Game/Unity save-state implementation.
- The legacy phase/gate-oriented tests and test scripts were intentionally
  moved to `archive/test-cases/legacy/2026-09-07/`. Do not restore them in bulk.

The Gemini physics work labelled A1-A3 and B1-B3 in
`docs/GEMINI_REVIEW_IMPLEMENTATION_PLAN.md` has therefore been implemented.
The roadmap and conclusion in `review.md` still describe several of those
items as absent; treat the review as the original input, not current status.

### Confirmed active gaps

1. **Replacement behavioral tests are mostly absent.** The archive operation
   removed historical coverage, but only the first new vertical Game test slice
   now exists. Core, browser, web, and Unity replacement cases in
   `docs/TEST_CASE_REBUILD_DRAFT.md` remain to be implemented.
2. **The deployed browser clock has a reported freeze.** The source contains a
   live Game/WASM advancement path, so this is a deployment/runtime defect to
   reproduce against the production-shaped build, not evidence that Core lacks
   time advancement. It was not reproduced in the restricted audit shell.
3. **The browser channel detail lacks the requested burnup graph.** It renders
   a bundle power profile followed by textual bundle burnup. Add a matching
   12-position burnup profile immediately below the power profile.
4. **The current player-facing interaction is the wrong shape for the next
   cut.** `web/candu-playtest/src/scenes/OperationsScene.ts` still opens a
   predictor/preview modal and hides the refuelling order behind confirmation.
   The main panel does not yet make direct refuelling, automated RRS state, the
   14-zone strip, selected-zone target/measured/error, warnings, or the
   average-fill game-over state immediately legible. Unity must receive the
   same interaction contract; the browser remains its proxy.
5. **Objectives and scoring remain thin.** There is a practice scenario and
   score feedback, but no concise run arc that teaches direct refuelling,
   rewards stable power and useful discharge burnup, and makes RRS margin a
   visible resource without adding accident or operator-training scope.
6. **Unity debug state control is incomplete.** Time, restart, fuel grant,
   pending-action clearing, score-response reset, raw diagnostics, and digest
   copy exist. Direct burnup/residence bands, power/tilt/xenon/control/device
   overrides, heat-map layer toggles, and Game/Unity save/load do not.
7. **Presentation polish is incomplete.** Bundle movement animation, contextual
   tooltips, accessible color alternatives, audio feedback, and annotated trend
   cause/effect are still described but not implemented.
8. **The lawful offline physics-data pipeline is partial.** Schemas, synthetic
   packs, parsers, and research fixtures exist, but there is no admitted,
   redistributable DRAGON5/DONJON5-derived runtime pack selectable without
   gameplay code changes.
9. **Several active docs are stale.** `README.md` understates the current
   adiabatic/adjoint/xenon/regulator path. `docs/IMPLEMENTATION_GUIDE.md` still
   calls kinetics and poisons “next.” `docs/LLM_SOLVER_REPLACEMENT_HANDOFF.md`
   describes the older spatial-eigen/Jacobi boundary and should not be used as
   the current implementation map without an update.

### Deliberately deferred research

Do not place these on the playable-game critical path:

- a true inhomogeneous fixed-source IQS operator;
- nodewise fission-delayed and heavy-water photoneutron fields;
- dynamic bilinear rho, beta-effective, and generation-time integrals; and
- a deterministic Krylov backend.

These are C1-C4 in `docs/GEMINI_REVIEW_IMPLEMENTATION_PLAN.md`. Photoneutron
data requires a lawful, versioned source and explicit provenance. The current
steady-state game must not claim transient or plant-grade fidelity.

## Work completed in this handoff

`tests/ReactorSim.Game.Tests/PracticeRefuellingCampaignTests.cs` adds one
deliberately vertical three-case campaign suite:

- preview a four-bundle shift and prove that accepted Core, xenon, solver,
  inventory, score, and every presented bundle remain unchanged;
- commit that exact preview and prove bundle identities move to the correct
  positions while fresh inventory, operation metadata, weighted reactivity,
  xenon binding, and score update together;
- reject an unsupported fuel after candidate construction and prove full
  accepted-state atomicity; and
- after an eight-bundle refuel, advance one simulated hour as one wall-time
  command and as two partitions, then compare the complete visible core,
  burnup, physics, and xenon digest.

The suite has a dedicated `ReactorSim.Game.Tests` project wired into
`ReactorSim.sln`. All three cases pass, and the full solution builds with zero
warnings after using the locally cached packages. The explicit SDK-path
properties used by the restricted shell are an environment workaround, not a
repository requirement.

## Dependency-ordered implementation plan

Each numbered item is intentionally bounded for one GPT-5.6 Luna subagent at
`max` reasoning. Run writer tasks sequentially in the shared checkout. The
orchestrator owns architecture, conflict resolution, verification, and commits.
Do not create intermediary review agents; a later Gemini pass will review the
integrated result.

### Wave 0 — establish the new behavioral safety net

1. **Complete — run and stabilize the new Game campaign suite.** Restore and run only
   `tests/ReactorSim.Game.Tests`. Fix test assumptions if public behavior is
   correctly different; do not weaken atomicity or determinism. Acceptance:
   all three tests pass without production changes. Completed 2026-09-08.
2. **Complete — rebuild Core topology and inventory coverage.** Implement
   `CORE-TOPO-001/002`, `CORE-INV-001`, and `CORE-REFUEL-001..006` from the test
   rebuild document using small manufactured fixtures. Acceptance: both shift
   directions and sizes, stable identity, conservation, insufficient inventory,
   invalid requests, and stale binding fail-closed behavior are covered.
   Completed 2026-09-08 with nine cases in
   `tests/ReactorSim.Core.Tests/TopologyInventoryRefuellingTests.cs`; preview
   non-mutation remains covered at the Game boundary where preview is public.
3. **Complete — rebuild power/burnup/spatial coverage.** Implement `CORE-BURN-001/002`,
   `CORE-SPATIAL-001..004`, and `CORE-POWER-001/002`. Acceptance: SI energy
   accounting, normalized power, symmetry, explicit boundaries, record-order
   independence, and nonconvergence are proven without plant-value goldens.
   Completed 2026-09-08 with eight cases in
   `tests/ReactorSim.Core.Tests/BurnupAndPowerTests.cs` and
   `tests/ReactorSim.Core.Tests/SpatialSolveTests.cs`; the focused slice and the
   full current Core suite pass.
4. **Complete — rebuild kinetics/xenon deterministic coverage.** Implement the
   P1 kinetics, xenon, control, and determinism cases. Acceptance: equilibrium,
   partitioned time, localized xenon, regulator bounds, and digest/order
   contracts pass. Completed 2026-09-08 with seven cases in
   `tests/ReactorSim.Core.Tests/KineticsXenonControlDeterminismTests.cs`; the
   focused slice and the full current Core suite pass.
5. **Complete — rebuild browser and web seam tests.** Added the bridge
   protocol/parity cases and reducer tests, including fail-closed WASM
   unavailability without making the TypeScript compatibility fixture
   authoritative. Completed 2026-09-08 with six Browser facts in
   `tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs` and the focused
   Vitest suites under `web/candu-playtest/src/`; both focused suites and the
   web production build pass.
6. **Complete — rebuild Unity seam smoke tests.** Added the deliberately small
   replacement seam: two EditMode facts in
   `unity/ReactorGame/Assets/ReactorGame.Unity/Tests/Editor/RuntimeSeamAndPacing.EditModeTests.cs`
   covering `UNITY-001/002`, and one PlayMode smoke in
   `unity/ReactorGame/Assets/ReactorGame.Unity/Tests/PlayMode/PrimaryLoopPlayModeSmokeTests.cs`
   covering `UNITY-003/004`. Verification: Prepare-UnityCore build 0
   warnings/errors; Unity `6000.3.21f1` EditMode 2/2 pass; PlayMode 1/1 pass.
   Completed 2026-09-09.
7. **Complete — replace only the useful test runners.** Added minimal focused
   .NET, browser, and Unity import commands. Verification completed 2026-09-09:
   Test-DotNet All passed Core 24/24, Game 3/3, and Browser 6/6;
   Test-Browser passed Browser 6/6, Vitest 4/4, and the production build (the
   existing Vite chunk-size warning is non-fatal); Test-UnityImport passed with
   the Prepare-UnityCore build at 0 warnings/errors and the Unity 6000.3.21f1
   import/compile smoke. No phase gates, approvals, evidence generation, or
   routine soaks were restored.

### Next implementation order — player-facing vertical slice

The completed Wave 0 work is the safety net, not the next product milestone.
The slices below supersede the former recommendation/save/realism order. Run
one writer task at a time in the shared checkout; keep each slice small enough
to review and play in Unity before starting the next one.

1. **Replace the predictor flow with a direct refuelling order.**

   Objective: let the player select a channel, choose four or eight bundles and
   a direction, then issue one direct `RefuelChannel` order from the main side
   panel. Return an immediate accepted/rejected result with the existing
   atomic Game/Core state transition. Remove `preview-refuel`, predictor copy,
   and confirm-gated presentation from the player-facing path. If a temporary
   compatibility seam is required during migration, keep it non-player-facing
   and delete it before the vertical-slice gate.

   Files: `src/ReactorSim.Game/GameSession.cs`,
   `src/ReactorSim.Game/CorePresentationContracts.cs`,
   `src/ReactorSim.Game/PracticeGameSessionFactory.cs`,
   `src/ReactorSim.Browser/PlaytestProtocol.cs`,
   `web/candu-playtest/src/protocol.ts`,
   `web/candu-playtest/src/commandState.ts`,
   `web/candu-playtest/src/sessionController.ts`,
   `web/candu-playtest/src/scenes/OperationsScene.ts`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase8UnityRuntimePort.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/UnityRuntimePort.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/CoreMapView.cs`, and the
   focused Game, Browser, web, and Unity seam tests that still assert preview.

   Acceptance: a direct order from Unity and browser changes the same accepted
   snapshot; invalid direction/size/fuel/inventory requests leave the complete
   state unchanged; the side panel exposes the order without opening a modal;
   no player-facing button, key hint, status, or test refers to preview or
   predicted outcome.

   Dependencies: the existing `GameSession` refuelling transaction,
   `RefuellingShift` contracts, and Wave 0 atomicity tests. Stop if this needs
   a second simulation authority or a new predictive model. Go only when the
   Unity order is playable and the browser proxy is contract-parity checked.

2. **Bound the automated RRS response for gameplay.**

   Objective: replace the current repeated static-controller loop with one
   deterministic bounded pass over the fourteen fill variables. Build a
   14-variable sensitivity/Jacobian response, solve the constrained
   least-squares fill command, perform one verification solve, and allow at
   most one bounded correction. There is no unbounded iteration or hidden
   retry loop. Preserve fail-closed atomicity and the existing project-authored
   RRS provenance.

   Files: `src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs`,
   `src/ReactorSim.Core/Domain/EquilibriumCoreSolverContracts.cs`,
   `src/ReactorSim.Game/GameSession.cs`,
   `src/ReactorSim.Game/CorePresentationContracts.cs`, and focused
   `tests/ReactorSim.Core.Tests/PracticeLiquidZoneRrsTests.cs`, relevant
   equilibrium/Game tests, and
   `tests/ReactorSim.Game.Tests/PracticeRefuellingCampaignTests.cs` cases.

   Acceptance: the 14-variable ordering, finite-difference/sensitivity inputs,
   bounds, least-squares result, verification solve, and optional single
   correction are explicit and deterministic; the solve budget is testable;
   a failed/nonconverged candidate retains the prior accepted state; a normal
   command cannot move fills outside [0,1]; and the resulting RRS values are
   available in the same immutable Game snapshot used by Unity and browser.

   Dependencies: Slice 1's direct command boundary and the existing
   `PracticeLiquidZoneRrsV1`/`GameRrsPresentationSnapshot` contracts. Stop if
   the work expands into new transient physics, plant calibration, or an
   unconstrained solver backend. Go when a repeated direct order is bounded,
   replay-stable, and visibly changes the RRS state.

3. **Make RRS the primary survival/resource meter.**

   Objective: promote the existing RRS snapshot to the primary status surface.
   Show average fill prominently, render a compact status for all fourteen
   zones, and show the selected zone's fill plus target, measured, and error
   values. Keep the heat map and bundle detail useful, but subordinate to the
   RRS state that the player is managing.

   Files: `src/ReactorSim.Game/CorePresentationContracts.cs`,
   `src/ReactorSim.Browser/PlaytestProtocol.cs`,
   `web/candu-playtest/src/protocol.ts`,
   `web/candu-playtest/src/scenes/OperationsScene.ts`,
   `web/candu-playtest/src/visuals.ts`,
   `web/candu-playtest/src/drawing.ts`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase8UnityRuntimePort.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10DashboardView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10ShellView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/CoreMapView.cs`, and focused
   presentation/reducer/EditMode tests.

   Acceptance: exactly fourteen zones are visible and stably ordered; selecting
   a zone updates its target/measured/error readout; fills and errors update
   after the direct order and after time advances; all values come from the
   shared snapshot rather than TypeScript or Unity rules; and the browser
   surface remains a fast acceptance proxy for the Unity layout/contract.

   Dependencies: Slice 2's bounded RRS result. Stop if the UI requires
   inventing a new physics value. Go when an owner can understand which zone
   is healthy, drifting, or consuming margin without opening diagnostics.

4. **Add explicit warnings and the RRS game-over boundary.**

   Objective: give the player immediate, readable warning states for poor RRS
   margin, zonal error, power/tilt drift, and unavailable fuel as appropriate to
   the existing contracts. Make average fill exactly 0% or exactly 100% a
   visible game-over state using `IsGameOver`/`GameOverReason`; do not introduce
   scram, shutdown, accident, or operator-training progression.

   Files: `src/ReactorSim.Game/GameSession.cs`,
   `src/ReactorSim.Game/CorePresentationContracts.cs`,
   `src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs`,
   `web/candu-playtest/src/scenes/OperationsScene.ts`,
   `web/candu-playtest/src/visuals.ts`,
   `web/candu-playtest/src/sessionController.ts`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10DashboardView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10ShellView.cs`, and
   focused boundary tests in Core, Game, Browser, and Unity.

   Acceptance: values just inside the boundary are warnings but playable;
   average fill at 0 or 1 is game over with the correct reason; the warning
   and game-over presentation is visible without the debug menu; normal time
   and refuelling actions cannot silently continue a finished run; restart or
   an explicitly labelled debug reset remains deterministic.

   Dependencies: Slice 3's RRS projection. Stop if “game over” is implemented
   as accident simulation or if a UI-only threshold disagrees with Core. Go
   when both Unity and browser show the same boundary behavior.

5. **Give each run a compact goal arc and coherent score.**

   Objective: add a short, deterministic steady-state run with visible goals:
   issue the guided first direct order, stabilize power/tilt, earn useful
   discharge burnup, and sustain the RRS operating band for a timed interval.
   Score stable power/tilt, useful burnup, economical direct refuelling, RRS
   margin, and completed goals. Keep objectives actionable and legible; do not
   turn them into a training narrative or plant-operations simulator.

   Files: `src/ReactorSim.Core/Domain/Phase8ScenarioRuntimeContracts.cs`,
   `src/ReactorSim.Core/Domain/Phase8ScoringContracts.cs`,
   `src/ReactorSim.Game/PracticeGameSessionFactory.cs`,
   `src/ReactorSim.Game/GameSession.cs`,
   `src/ReactorSim.Game/CorePresentationContracts.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10DashboardView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10ControlsView.cs`,
   `web/candu-playtest/src/scenes/OperationsScene.ts`, and focused Game,
   Unity, and browser tests.

   Acceptance: the next goal, progress, score delta, and end-of-run result are
   visible in the main play surface; replaying the same command/time trace
   produces the same goals and score; a useful run fits roughly 10–20 minutes
   at intended acceleration; and RRS game over ends the run without a hidden
   alternate failure system.

   Dependencies: Slices 1–4. Stop if a goal needs a new detailed-physics
   observable; expose only the smallest engine-neutral state needed. Go when a
   player can state what to do next and why a refuelling choice improved or
   hurt the score.

6. **Polish the tactical operations surface and keep Unity/browser aligned.**

   Objective: make the interface feel like a layered tactical operations
   console: rich panel materials, strong selection and movement feedback,
   readable hierarchy, accessible warning colors, contextual tooltips, and a
   paired 12-position burnup profile below the power profile. This may draw
   only interface/aesthetic language from Fire Emblem, Disgaea, and Final
   Fantasy Tactics. It must contain no characters, story, dialogue, copied IP,
   or copied assets.

   Files: `web/candu-playtest/src/scenes/OperationsScene.ts`,
   `web/candu-playtest/src/drawing.ts`, `web/candu-playtest/src/visuals.ts`,
   `web/candu-playtest/src/projection.ts`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10ShellView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/Phase10DashboardView.cs`,
   `unity/ReactorGame/Assets/ReactorGame.Unity/CoreMapView.cs`, and the
   focused Vitest, browser smoke, Unity EditMode, and Unity PlayMode checks.

   Acceptance: direct refuelling, RRS status, warnings, goals, and game over
   remain readable at desktop and narrow browser widths and in the Unity
   scene; selection and refuelling feedback are responsive with reduced-motion
   support; the burnup profile has clear units and fresh-fuel treatment; and
   no art task adds narrative/IP scope.

   Dependencies: Slices 1–5. Stop if polish hides a value or introduces a
   browser-only rule. Go when the Unity development build is enjoyable to
   repeat and the browser proxy catches layout/interaction regressions quickly.

7. **Hold the physics boundary while gameplay is being proven.**

   Objective: freeze speculative physics work beyond the already requested
   cross-section audit. The true inhomogeneous fixed-source IQS operator,
   nodewise fission-delayed/heavy-water photoneutron fields, dynamic bilinear
   rho/beta-effective/generation-time integrals, and a deterministic Krylov
   backend remain deferred. Cross-section audit/admission documentation may
   continue only as a bounded offline activity and must not displace the
   playable slices.

   Files: the already scoped `docs/spec/xsec-*.md` audit documents and their
   provenance references; do not add runtime physics files in this slice.

   Acceptance: no new speculative physics task is placed on the gameplay
   critical path; runtime code still consumes only compact, versioned,
   lawfully usable packs; and the Unity/browser game can be built, launched,
   and playtested using the deterministic project-authored model.

   Dependencies: none for the documentation boundary. Stop any physics
   expansion when it competes with a player-facing slice. Go to the optional
   DRAGON5/DONJON5 data pass only after the vertical-slice gate below passes
   and a separate provenance/licensing decision is recorded.

### Vertical-slice go/no-go gate

Go only after an owner can launch the Unity development build, issue direct
orders from the main side panel, see all fourteen RRS zones and the selected
zone target/measured/error, recognize warnings, reach and understand both
average-fill game-over boundaries through debug setup, complete a short goal
run with score feedback, and repeat the same trace deterministically. Run the
browser console against the same bridge as a fast interaction/acceptance proxy;
it is never a second authority. If any check fails, keep the next task on the
smallest failing gameplay slice and do not advance the physics/data roadmap.

## Orchestrator execution rules

- Read `AGENTS.md`, `README.md`, this file, and the relevant section of
  `docs/TEST_CASE_REBUILD_DRAFT.md` before dispatching work.
- Use GPT-5.6 Luna/max for every bounded coding, test, or documentation task.
  Use Terra/high only for a required clarification and Sol/medium only when
  critically stuck.
- Preserve unrelated worktree changes. Never bulk-restore the archived tests.
- Keep `unity/ReactorGame` runnable after each slice.
- Dispatch no intermediary implementation-review task. Integrate each bounded
  result, run proportionate focused checks, inspect the final diff for scope,
  and leave the next independent review to Gemini as requested.
- Treat Unity owner playtesting as the acceptance path and the browser as a
  fast interaction/contract proxy. Do not let browser convenience recreate a
  second simulator or a preview/predictor workflow.
- Do not start recommendations, save/archive, broad debug overrides, or
  DRAGON5/DONJON5 runtime admission until the vertical-slice go/no-go gate
  passes.
- Commit each completed slice with its focused checks and report the hash.

## Copy-ready prompt for a new orchestrator chat

> Continue the CANDU refuelling game from `docs/NEXT_SPRINT_NOTES.md`. Read
> `AGENTS.md`, `README.md`, `docs/IMPLEMENTATION_GUIDE.md`, and
> `docs/TEST_CASE_REBUILD_DRAFT.md`. Preserve the intentional legacy-test
> archive under `archive/test-cases/legacy/2026-09-07/`. Start at the first
> incomplete player-facing slice in the vertical-slice plan. Delegate each
> bounded coding, test, or documentation task to GPT-5.6 Luna at max
> reasoning, one writer at a time. Remove the player-facing predictor/preview
> flow, put direct refuelling in the main side panel, make automated RRS the
> primary survival/resource meter, and expose the fourteen-zone status plus
> selected-zone target/measured/error, warnings, game-over boundaries, goals,
> and score. Keep Core authoritative and engine-neutral, keep Unity as the
> product acceptance surface, use the browser only as a fast proxy, and keep
> DRAGON5/DONJON5 offline. Bound the RRS controller to one 14-variable
> sensitivity/Jacobian least-squares pass, one verification solve, and at most
> one correction. Do not add shutdown, scram, accident, or operator-training
> scope, and freeze speculative physics beyond the scoped cross-section audit
> until the vertical-slice gate passes. Do not commission intermediary reviews;
> integrate and test the work, commit completed slices, and leave the later
> independent review to Gemini.
