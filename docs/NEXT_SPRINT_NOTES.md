# Next orchestrator handoff

Status: canonical implementation handoff, refreshed 2026-09-09.

Use this document to start the next orchestrator chat. `README.md` and
`docs/IMPLEMENTATION_GUIDE.md` remain useful architecture references, while
`gemini_review.md`, `review.md`, and the phase/task-numbered documents are
historical review evidence rather than current task trackers.

## Product boundary

Build the playable Unity steady-state CANDU refuelling game first. The player
must be able to inspect the 380-channel core, preview and commit four- or
eight-bundle shifts in either direction, advance time, see power/burnup/xenon
and score respond, and use a debug menu to reach useful playtest states.

Keep shutdown, scram, accident progression, and operator-training scenarios
out of scope. Keep `ReactorSim.Core` engine-neutral and authoritative for state
transitions. Unity and browser code are presentation/input layers. DRAGON5 and
DONJON5 remain offline tools; runtimes consume only compact, versioned packs.

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
4. **Candidate recommendations and operational trade-off guidance are absent.**
   Preview metrics exist, but there is no engine-neutral ranking service or
   optional recommendation surface.
5. **Objectives and onboarding remain thin.** There is a practice scenario and
   controls, but no concise objective flow covering guided refuelling, tilt
   correction, discharge-burnup optimization, and an equilibrium campaign.
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
   `tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs` and four Vitest cases
   in `web/candu-playtest/src/uiSlice.test.ts`; both focused suites and the web
   production build pass.
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

### Wave 1 — fix the reported playable surfaces

8. **Reproduce and fix the deployed browser clock.** Build the production WASM
   artifact, serve the exact deployment output, and observe snapshot time over
   multiple worker dispatches. Check worker asset paths, initialization,
   pending-command scheduling, visibility throttling, and deployment headers.
   Acceptance: production-shaped smoke proves monotone simulation time and
   visible burnup/state updates after pause/resume.
9. **Add the paired burnup profile.** Reuse the power-profile layout and render
   all 12 bundle burnups immediately below it with clear units, fresh-fuel
   treatment, and accessible text. Acceptance: reducer/component test plus a
   real-browser visual smoke at desktop and narrow widths.
10. **Implement engine-neutral candidate ranking.** Add a Game/Core service that
    evaluates a bounded set of valid channel/direction/shift candidates through
    existing preview semantics and returns stable ranked reasons. Inputs and
    scoring weights must be explicit; no Unity rules. Acceptance: ordering is
    deterministic, invalid/unavailable candidates are excluded, and requesting
    recommendations does not mutate session state.
11. **Expose recommendations and trade-offs.** Add an optional Unity panel that
    shows two or three candidates with predicted power, tilt, discharge burnup,
    fuel cost, xenon tendency, and score effect. The player still chooses and
    commits. Acceptance: recommendation-to-preview-to-commit smoke with clear
    explanation of why ranking changed.
12. **Add short objective progression.** Implement four bounded steady-state
    objectives: guided first refuel, correct a tilt, optimize discharge burnup,
    and sustain an equilibrium campaign. Keep scenario rules in Core/Game and
    presentation in Unity. Acceptance: success/failure is deterministic and a
    practice run remains roughly 10–20 minutes at intended acceleration.

### Wave 2 — owner controls and presentation quality

13. **Define a versioned Game save/archive contract.** Capture initial identity,
    commands, accepted state/digests, and replay metadata. Reject tampered or
    mismatched archives before mutation. Do not serialize Unity objects.
14. **Add Unity save/load controls.** Bind the Game archive to explicit debug
    slots/files, visibly label restored debug state, and prove round-trip parity.
15. **Add bounded debug overrides.** Implement burnup/residence presets first,
    then power/tilt/xenon/control/device overrides and heat-map layers only as
    concrete playtest needs arise. Every override must be visible and excluded
    from normal scoring.
16. **Polish feedback and accessibility.** Add movement animation, tooltips,
    annotated timeline causes, accessible palettes, and restrained audio in
    separate small tasks. Preserve responsiveness and non-audio feedback.

### Wave 3 — optional lawful realism pass

17. **Finish runtime-pack admission independently of reactor tools.** Define one
    compact schema/loader selection path with units, energy-group order,
    topology digest, provenance, version, and checksum; prove synthetic pack
    swapping does not alter Game/Unity rules.
18. **Produce and admit a lawful offline export.** Only after source/licensing
    decisions are recorded, run DRAGON5/DONJON5 offline, export the selected
    branch grid, compare trends/invariants, and package only redistributable
    runtime fields. Never invoke either executable from Unity or browser.

### Final documentation reconciliation

19. Update `README.md` and `docs/IMPLEMENTATION_GUIDE.md` to match implemented
    behavior and the rebuilt test layout. Add “historical review” banners or
    supersession links to `gemini_review.md`, `review.md`, and
    `docs/LLM_SOLVER_REPLACEMENT_HANDOFF.md`; do not rewrite review evidence as
    if it were a new independent review.

## Orchestrator execution rules

- Read `AGENTS.md`, `README.md`, this file, and the relevant section of
  `docs/TEST_CASE_REBUILD_DRAFT.md` before dispatching work.
- Use GPT-5.6 Luna/max for every bounded coding, test, or documentation task.
  Use Terra/high only for a required clarification and Sol/medium only when
  critically stuck.
- Preserve unrelated worktree changes. Never bulk-restore the archived tests.
- Keep `unity/ReactorGame` runnable after each slice.
- Dispatch no intermediary implementation-review task. Integrate the result,
  run proportionate focused checks, inspect the final combined diff for scope,
  and leave the next independent review to Gemini as requested.
- Commit each completed slice with its focused checks and report the hash.

## Copy-ready prompt for a new orchestrator chat

> Continue the CANDU refuelling game from `docs/NEXT_SPRINT_NOTES.md`. Read
> `AGENTS.md`, `README.md`, `docs/IMPLEMENTATION_GUIDE.md`, and
> `docs/TEST_CASE_REBUILD_DRAFT.md`. Preserve the intentional legacy-test
> archive under `archive/test-cases/legacy/2026-09-07/`. Start at the first
> incomplete task in the dependency-ordered plan. Delegate each bounded coding,
> test, or documentation task to GPT-5.6 Luna at max reasoning, one writer at a
> time. Keep Core authoritative and engine-neutral, keep Unity/browser as
> presentation, and keep DRAGON5/DONJON5 offline. Do not add shutdown, scram,
> accident, or operator-training scope. Do not commission intermediary reviews;
> integrate and test the work, commit completed slices, and leave the later
> independent review to Gemini.
