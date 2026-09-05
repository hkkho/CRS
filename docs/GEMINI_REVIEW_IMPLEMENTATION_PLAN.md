# Gemini review implementation plan

This plan turns `review.md` into a dependency-ordered set of bounded Luna
tasks. The product target remains the playable, steady-state on-power
refuelling game. Core owns all state transitions and numerical rules; Game
projects them for Unity/browser; neither presentation surface duplicates
physics. DRAGON5/DONJON5 remain offline, and every runtime pack keeps units,
energy-group order, identity, provenance, and deterministic behavior visible.

The current implementation should be described truthfully as an **adiabatic
static-eigenmode plus point-kinetics model**. A true IQS operator is valuable,
but it is not a prerequisite for the current game loop. Compatibility with the
existing v1 JSON and public contracts is required throughout: keep legacy field
names/identities readable, add explicit migration or aliases when a schema must
advance, and never silently reinterpret an old snapshot as a different model.

## Dependency order

`A1 -> A2 -> A3 -> B1 -> B2 -> B3 -> Integration`

`B1` and `B2` may be developed against small synthetic fixtures in parallel
after `A2`, but the integration order above is the merge order. The deferred
research work starts only after these slices are stable:

`A2 + B3 -> C2 -> C1 -> C3 -> C4`

The graph is deliberately biased toward a stable regulated game. It does not
turn the current long-step practice loop into an accidental shutdown, scram, or
accident simulator.

## Current-scope tasks

### A1 — Tell the truth about the existing solver, while preserving compatibility

**Objective.** Reclassify the active path as adiabatic/static-eigenmode shape
plus point kinetics. Make the shape method, amplitude method, and diagnostic
identities explicit in Core and in published Game/browser snapshots. Preserve
the `IqsFullCoreSolver` entry point, existing v1 JSON inputs, and serialized
fields through an adapter, compatibility alias, or dual-read migration. Do not
change numerical results for an unchanged input in this slice.

**Likely files/contracts.**

- `src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs` and the related spatial
  solve contracts; add explicit formulation/method identifiers rather than
  relying on the misleading `iqs` name.
- `src/ReactorSim.Game/CorePresentationContracts.cs`, the browser protocol, and
  any snapshot/replay serialization that currently says IQS.
- `src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json` only as
  needed for compatible identity metadata; retain legacy IDs as accepted input.
- `tests/ReactorSim.Core.Tests/IqsSolverTests.cs`, Game tests, and browser
  protocol tests.

**Dependencies.** None beyond the current repository state. This is the
contract seam on which A2–C4 depend.

**Acceptance tests.**

- The old v1 pack and old serialized snapshot load successfully.
- A new snapshot identifies static eigenmode/adiabatic shape and point
  kinetics; it does not claim a fixed-source IQS solve.
- `k` and `(k - 1) / k` remain available as state diagnostics, and the
  unchanged-input replay/digest remains unchanged.
- Invalid or stale detailed solves still fail closed and candidate commits
  remain immutable/atomic.

**One-agent boundary.** One agent may edit the identity/compatibility seam and
its focused tests. It must not redesign the shape equation, add new nuclear
constants, or touch Unity presentation behavior beyond consuming the explicit
identity.

### A2 — Generalize ordered delayed-source groups and correct the synthetic pack

**Objective.** Replace the Iqs-specific six-element assumption with a validated,
ordered collection of delayed-source groups. Reuse or bridge the existing
`DelayedNeutronDataV1`/`DelayedNeutronGroupV1` contracts so group count is data,
not an array-length rule. Retain beta/decay ordering and deterministic
precursor integration. Update the project-authored synthetic pack to
`generation_time_seconds: 0.0009` and thermal-group velocity `2900.0 m/s`,
with version, source identity, evidence class, group ordering, and provenance
visible in the loaded object and snapshots.

The synthetic pack must contain only constants the project is prepared to own:
the existing synthetic fission groups may remain, but no photoneutron fractions,
decay constants, or half-lives may be invented. The schema should be able to
represent a future source kind (including photoneutron) and its data identity,
without requiring that source kind in the current pack.

**Likely files/contracts.**

- `src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs` for pack loading,
  precursor storage, validation, and compatibility projection.
- `src/ReactorSim.Core/Domain/Phase7KineticsContracts.cs` for the generalized
  ordered source-group contract; add a source-kind/data-identity field only if
  needed to distinguish supplied fission and future photoneutron data.
- `src/ReactorSim.Core/EmbeddedData/candu6-two-group-iqs-pack-v1.json`, or a
  compatible successor pack with a dual-read loader.
- `src/ReactorSim.Game/CorePresentationContracts.cs` and browser protocol fields
  for pack version/provenance and group count.
- `tests/ReactorSim.Core.Tests/IqsSolverTests.cs`, `P7T01KineticsTests.cs`,
  serialization tests, and browser protocol tests.

**Dependencies.** A1. The new group model must use A1's truthful formulation
identity and keep legacy six-array JSON readable.

**Acceptance tests.**

- One-, six-, and more-than-six-group synthetic fixtures load, preserve exact
  serialized order, and advance deterministically with nonnegative finite
  precursors.
- Mismatched ordering, beta sums, decay constants, or source metadata fail
  closed with stable diagnostics.
- The embedded pack reports `0.0009` seconds, `2900.0 m/s` for the thermal
  group, an explicit version/provenance identity, and no photoneutron records.
- Existing six-group equilibrium and point-kinetics tests remain valid after
  the generalized storage change.

**One-agent boundary.** One agent owns the data/contract migration and focused
  Core tests. It must not add guessed photoneutron data, implement the RRS loop,
  or change the spatial operator.

### A3 — Regulate synthetic steady state without erasing local shape

**Objective.** Connect the existing synthetic RRS/LZC contracts to the practice
  loop. Add a deterministic automatic bulk/zone compensation path so a positive
  refuelling perturbation is driven toward near-zero **net** reactivity while
  the candidate's local flux shape, channel tilt, and bundle powers remain the
  spatial solve's responsibility. Expose enough state to explain the result:
  core/static reactivity, compensated net reactivity, LZC/RRS command and
  state, bounds/saturation, amplitude, and cadence.

The one-hour/game-speed path must use a documented, deterministic steady-state
cadence. It may partition a long gap into bounded internal substeps or use a
controlled steady-state update, but it must not present a 600-second point step
as a faithful sub-second transient. Scheduled burnup, regulation, and shape
recompute must share one simulation clock and use the same ordered partition.

**Likely files/contracts.**

- `src/ReactorSim.Game/GameSession.cs` and
  `src/ReactorSim.Game/PracticeGameSessionFactory.cs` for orchestration and
  atomic practice-state updates.
- `src/ReactorSim.Core/Domain/Phase6RrsContracts.cs`,
  `Phase6LiquidZoneContracts.cs`, and `Phase7IntegrationContracts.cs` for the
  synthetic regulator, actuator bounds, and deterministic cadence; add a narrow
  steady-state controller contract if the existing industrial-shaped fixture
  cannot be composed directly.
- `src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs` for the distinction between
  shape reactivity and compensated point-kinetics reactivity.
- `src/ReactorSim.Game/CorePresentationContracts.cs`, browser protocol, and
  `tests/ReactorSim.Core.Tests/P6T05RrsControllerContractsTests.cs`,
  `GameSessionTests.cs`, `IqsSolverTests.cs`, and browser parity tests.

**Dependencies.** A1 and A2. A2 supplies the corrected generation time and
  generalized source storage; A1 supplies the diagnostic names.

**Acceptance tests.**

- Refuelling that creates a positive synthetic perturbation produces a bounded
  amplitude and net reactivity that trends toward the target under automatic
  regulation; it does not run away over a long practice advance.
- Controller bounds and saturation are represented explicitly and do not cause
  NaN, negative power, or silent state changes.
- The same long gap, partitioned or advanced through the owner debug menu,
  produces the same canonical digest as the scheduled path.
- Local channel power/tilt from the spatial candidate remains observable after
  scalar compensation; the controller does not flatten the candidate shape.
- Pause, restart, preview, commit, and failed solve paths remain deterministic
  and atomic; the Unity/browser snapshots expose the new diagnostics.

**One-agent boundary.** One agent may compose or add the synthetic regulator,
cadence, diagnostics, and focused tests. It must not implement adjoint physics,
nodewise poison coupling, or true IQS fixed-source transport.

### B1 — Add a reference adjoint and use it for shape normalization

**Objective.** Provide a reference two-group adjoint importance field by a
  deterministic transpose solve or by loading an explicitly identified,
  validated reference field. Store its topology/data digest and use it in the
  shape constraint
  `sum(nodeVolume * adjoint[g,node] * flux[g,node] / velocity[g])`.
  Remove the active path's silent `W = 1` assumption. A missing or invalid
  weighted field must fail closed; a legacy uniform-constraint mode may remain
  only as an explicitly named compatibility mode for old data.

**Likely files/contracts.**

- `src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs` and
  `FullCoreDiffusionModelContracts.cs` for reference binding and candidate
  normalization.
- `src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs`,
  `SpatialEigenIterationContracts.cs`, and a small new
  `Adjoint...Contracts.cs` if the transpose operator needs a first-class
  contract.
- The synthetic pack/manifest or a compact reference-adjoint payload with
  explicit topology, energy-group, units, identity, and digest metadata.
- `tests/ReactorSim.Core.Tests/IqsSolverTests.cs` and spatial operator tests.

**Dependencies.** A1 and A2; A3 is not required for the isolated adjoint
  fixture but must consume the weighted result before integration.

**Acceptance tests.**

- The reference adjoint is finite, nonnegative, deterministic, canonical in
  the 380 x 12 node order, and has a bounded transpose residual (or a verified
  loaded digest).
- A candidate's weighted constraint equals the reference constraint after
  normalization to tolerance; scaling a raw shape does not change the
  normalized result.
- Center/peripheral synthetic perturbations receive different importance
  weights, while zero perturbation preserves the reference projection.
- Invalid topology, group order, digest, or adjoint length fails closed and a
  preview cannot mutate the committed solver.

**One-agent boundary.** One agent owns the reference adjoint contract/solve or
  loader and normalization tests. It must not change reactivity semantics,
  add dynamic precursor fields, or tune Unity displays.

### B2 — Add adjoint-weighted first-order reactivity, retain k/rho diagnostics

**Objective.** Compute the operational first-order perturbation reactivity from
  the reference adjoint, current shape, and local coefficient perturbation
  (`-ΔA + ΔF`) with an explicit denominator. Keep `EffectiveK` and
  `(k - 1) / k` as separate spatial diagnostics for comparison and debugging;
  they must not be mislabeled as the new dynamic/perturbation result. Use the
  adjoint-weighted value for the controlled gameplay response while preserving
  candidate immutability and the existing non-authoritative solve behavior.

**Likely files/contracts.**

- `src/ReactorSim.Core/Domain/IqsFullCoreSolver.cs`,
  `FullCoreDiffusionModelContracts.cs`, and a focused
  `AdjointReactivity...Contracts.cs` or helper.
- `src/ReactorSim.Game/CorePresentationContracts.cs` and browser protocol for
  distinct `k`, state rho, weighted perturbation rho, and solve diagnostics.
- `tests/ReactorSim.Core.Tests/IqsSolverTests.cs`, spatial fixture tests,
  `GameSessionTests.cs`, and browser protocol/replay tests.

**Dependencies.** B1, plus A3's distinction between core and compensated net
  reactivity.

**Acceptance tests.**

- A local synthetic coefficient perturbation has the expected sign and changes
  with adjoint importance; the weighted value is not merely the static k delta.
- The reference state reports zero first-order perturbation within tolerance,
  while `EffectiveK`/state rho remain available and finite.
- Perturbation integration uses explicit units and deterministic loop order,
  rejects a zero/nonfinite denominator, and leaves the old candidate active on
  failure.
- Replays and browser/Unity snapshots preserve both diagnostic identities.

**One-agent boundary.** One agent may implement the first-order weighted
  calculation and its contracts/tests. It must not implement full dynamic
  beta/Lambda integrals, the IQS fixed-source operator, or a Krylov solver.

### B3 — Own nodewise iodine/xenon state and bind its thermal overlay atomically

**Objective.** Make Core the owner of one deterministic iodine-135/xenon-135
  state per diffusion node. Advance production, decay, and burnup/flux-driven
  terms on the shared simulation clock. Build the thermal absorption overlay
  from nodewise Xe state before committed or scheduled spatial solves, while
  retaining the base coefficient identity. A preview must solve against a
  snapshot; a commit must atomically publish the updated nuclide state,
  overlay digest, and spatial candidate only after all validation/convergence
  checks succeed.

**Likely files/contracts.**

- `src/ReactorSim.Core/Domain/Phase5CompleteStateContracts.cs`,
  `Phase5CompleteBurnupContracts.cs`, `Phase7KineticsContracts.cs`,
  `Phase7XenonSpatialCouplingContracts.cs`, and `Phase7IntegrationContracts.cs`
  for node ownership, time/version bindings, and stable digests.
- `src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs`,
  `IqsFullCoreSolver.cs`, and `SpatialRecomputeContracts.cs` for the thermal
  `Sigma_Xe` overlay and atomic candidate input.
- `src/ReactorSim.Game/GameSession.cs` and
  `CorePresentationContracts.cs` for scheduled orchestration and minimal
  selected-channel/mean/max poison diagnostics.
- `tests/ReactorSim.Core.Tests/P7T02IxeTests.cs`,
  `P7T05XenonCouplingTests.cs`, `IqsSolverTests.cs`, `GameSessionTests.cs`, and
  browser protocol/replay tests.

**Dependencies.** A2 and A3; B1/B2 should be present before the final solve
  binding so poison changes affect weighted shape/reactivity consistently.

**Acceptance tests.**

- The 4,560-node state has exact node ownership/versioning, deterministic
  nonnegative finite updates, and a reproducible state digest.
- Zero iodine/xenon overlay reproduces the base solve; a localized Xe change
  changes only the expected thermal coefficient/solve diagnostics and affects
  the next shape in the expected direction.
- A localized power history shows deterministic iodine/Xe evolution across the
  scheduled cadence; preview leaves state unchanged; failed/nonconverged
  candidate solves roll back all state.
- Stale time, topology, state, or data-pack bindings fail closed, and the game
  snapshot exposes only compact diagnostics rather than duplicating node rules
  in Unity/browser.

**One-agent boundary.** One agent owns Core/Game poison state binding, overlay
  integration, and focused tests. It must not add photoneutron constants,
  replace the base diffusion solver, or implement the research IQS operator.

### Integration — Prove the shared boundary and keep the playable project runnable

**Objective.** Integrate A1–B3 with focused Core/Game/browser coverage and
  verify that the Unity assembly seam remains usable. Keep changes limited to
  compatibility adapters, snapshot/protocol plumbing, and focused test fixes;
  do not broaden the physics scope during integration.

**Likely files/contracts.**

- `tests/ReactorSim.Core.Tests`, `tests/ReactorSim.Browser.Tests`, and
  `web/candu-playtest/src/{protocol,bridge,reducer}*.{ts,tsx}` as needed for
  the published identity/diagnostic fields.
- `src/ReactorSim.Game` snapshot/replay contracts and
  `unity/ReactorGame/Assets/ReactorGame.Unity` only where a field binding is
  required.
- `tools/Prepare-UnityCore.ps1` and the existing Unity `ImportSmoke` test.

**Dependencies.** A1–B3 complete and individually focused tests green.

**Acceptance tests.**

- Run the focused Core and Game tests for the changed contracts, plus the
  browser protocol/reducer tests and a production-shaped browser build when
  the browser bridge is affected.
- Run `dotnet build ReactorSim.sln` and
  `powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1`.
- If a Unity executable is available, run the existing import/EditMode smoke;
  otherwise verify the prepared DLLs and report the import check as unavailable
  without changing the physics plan.
- Exercise the browser bridge and Unity snapshot compatibility enough to prove
  the same source/solve identity, reactivity diagnostics, and deterministic
  digest are exposed at both boundaries.
- Inspect the final diff and stage only the intended implementation files for
  that slice; do not create intermediary review, gate, approval, or evidence
  documents.

**One-agent boundary.** One integration agent may repair cross-project wiring
  and focused tests. It must not redesign A–B behavior, add new model constants,
  or create a second simulator.

## Deferred research slices

These tasks cover the remaining technical findings without pulling the current
product into shutdown, scram, accident progression, or operator-training
scenarios. They require the current steady-state loop and data contracts to be
stable first.

### C1 — Implement a true fixed-source IQS shape operator

**Objective.** Replace the static eigenmode shape update with the inhomogeneous
  time-dependent diffusion equation only for a future transient-capable mode:
  include the `dot(P)/P` and backward-shape frequency terms plus delayed-source
  forcing, while retaining atomic candidate semantics and the A1 formulation
  identity. The steady-state game may continue using A1's adiabatic path.

**Likely files/contracts.** `IqsFullCoreSolver.cs` or a new
`FixedSourceIqsSolver.cs`, `SpatialOperatorContracts.cs`, new fixed-source
operator/source contracts, node-state bindings from B3/C2, and focused Core
solver tests.

**Dependencies.** A1, A2, B1, B3, and C2's nodewise source fields.

**Acceptance tests.** A manufactured multi-node case matches the fixed-source
  residual and shows delayed shape inertia after a local perturbation; removing
  the source/frequency terms reduces to the documented static compatibility
  path; nonconverged solves leave the committed candidate unchanged and all
  values/digests are deterministic.

**One-agent boundary.** One agent owns only the fixed-source operator and its
  solver fixtures. It must not change the practice default, invent data, or
  select a preconditioner beyond a narrow interface needed by C4.

### C2 — Add nodewise fission-delayed and lawfully admitted photoneutron fields

**Objective.** Extend the generalized source-group model from A2 into spatial
  precursor fields: fission-delayed groups first, and photoneutron groups only
  when a lawfully reusable, versioned data source and provenance are admitted.
  Keep source kind/order, beta, decay, emission weighting, topology, and pack
  digest explicit. The current synthetic pack remains six supplied fission
  groups with no invented photoneutron constants.

**Likely files/contracts.** `Phase7KineticsContracts.cs`,
`Phase5CompleteStateContracts.cs`, `Phase7XenonSpatialCouplingContracts.cs`
or a sibling precursor contract, runtime pack schemas/manifests under `data`,
and `IqsFullCoreSolver.cs`/C1 source binding.

**Dependencies.** A2's ordered group contract, B3's node ownership/versioning,
and an explicit offline data/provenance decision. No runtime DRAGON5/DONJON5
execution.

**Acceptance tests.** Group records are ordered and digest-bound; nodewise
  fission precursor production/decay is deterministic and nonnegative; a
  photoneutron pack is rejected unless its source identity/provenance is
  complete; the existing synthetic pack and replay remain unchanged.

**One-agent boundary.** One agent owns the precursor-field contract, offline
  pack admission, and focused tests. It must not alter the Unity loop or fill
  missing photoneutron values from memory.

### C3 — Compute dynamic bilinear rho, beta-effective, and Lambda integrals

**Objective.** For the future dynamic solver, derive reactivity, effective
  delayed fractions, and prompt generation time from the reference adjoint and
  current shape/source fields rather than treating B2's first-order estimate as
  exact. Keep B2's weighted result and the static k/rho values as diagnostics
  during migration.

**Likely files/contracts.** New or extended adjoint/kinetics contracts beside
  `IqsFullCoreSolver.cs`, `Phase7KineticsContracts.cs`, data-pack identity and
  snapshot fields, plus manufactured-integral tests.

**Dependencies.** B1, B2, C1, and C2.

**Acceptance tests.** Reference-state integrals satisfy the expected
  normalization, units, and beta sum; controlled local perturbations agree in
  sign/order with B2; changing group order or adjoint digest fails closed; the
  same canonical input gives the same integrals across Core/browser builds.

**One-agent boundary.** One agent owns only the bilinear integral definitions,
  data binding, and tests. It must not retune the game score or introduce new
  controller behavior.

### C4 — Add a deterministic Krylov backend behind the fixed-source interface

**Objective.** Provide a deterministic Krylov solve (for example GMRES or
  BiCGStab with a bounded block preconditioner) for the 4,560-node fixed-source
  operator, selected only after profiling shows the current method needs it.
  Keep a small-fixture/reference backend and explicit convergence diagnostics.

**Likely files/contracts.** The fixed-source operator from C1, a solver-backend
  interface and convergence contracts near
  `SpatialConvergenceContracts.cs`/`SpatialEigenIterationContracts.cs`, plus
  deterministic performance and residual tests.

**Dependencies.** C1's operator, C2/C3 source and coefficient contracts, and a
  measured benchmark showing the need for a backend change.

**Acceptance tests.** The backend converges to the manufactured/reference
  solution within tolerance, reports residual/iteration/termination state,
  rejects nonfinite or unconverged results, is deterministic for fixed inputs,
  and does not regress the small-fixture or adiabatic compatibility path.

**One-agent boundary.** One agent owns the backend abstraction,
  preconditioner, and solver tests. It must not change physics contracts,
  browser behavior, or make the research backend the practice default without
  an explicit product decision.

## Review coverage matrix

| Review deficiency or recommendation | Disposition | Task(s) |
|---|---|---|
| Current code is adiabatic point kinetics, not true IQS; static eigenmode is mislabeled | Correct the public formulation/identities now; retain the true fixed-source implementation as research work | A1, C1 |
| Flat `W = 1` shape normalization | Replace active normalization with a solved/loaded reference adjoint; keep any flat mode explicitly legacy-only | B1 |
| Reactivity is only static `k`/rho delta | Use adjoint-weighted first-order perturbation for the gameplay response while retaining `k`/rho diagnostics; reserve full dynamic integrals for the research path | B2, C3 |
| 600-second micro-step smears delayed dynamics | Make the current loop an explicitly regulated steady-state cadence with deterministic bounded partition/equilibrium behavior; do not claim transient fidelity | A3; C1–C3 for true transients |
| Prompt generation time is `0.0001 s` | Correct the synthetic pack to `0.0009 s`, with visible version/provenance | A2 |
| Delayed source is hard-coded to six groups | Generalize ordered groups and preserve legacy six-group loading | A2 |
| Photoneutron groups are absent | Do not invent constants; keep the current pack fission-only and admit lawful nodewise photoneutron data later | A2, C2 |
| Thermal velocity is `2200 m/s` | Correct the synthetic thermal velocity to `2900 m/s` and expose energy-group order/units | A2 |
| No closed-loop RRS/LZC compensation causes runaway power | Connect the synthetic automatic regulator and show bounded net rho/control diagnostics without flattening local shape | A3 |
| Iodine/xenon contracts are decoupled from the spatial solve | Make nodewise I/Xe Core state advance on the shared clock and bind its Xe thermal overlay atomically | B3 |
| True IQS needs delayed spatial source and dynamic frequency terms | Defer until nodewise source fields and steady-state contracts are stable; verify with a manufactured fixed-source case | C1, C2 |
| True IQS needs nodewise fission-delayed/photoneutron precursor fields | Add fission fields and lawfully admitted photoneutron fields as explicit data-bound state | C2 |
| True IQS needs dynamic bilinear rho/beta/Lambda | Implement after fixed-source shape and source fields; preserve B2 as the migration diagnostic | C3 |
| A 4,560-node Krylov/preconditioned solve may be needed | Add only after a measured fixed-source performance need, behind a deterministic backend interface | C4 |
| Existing strengths: fail-closed validation, immutable candidates, deterministic ordering, Core/Game separation | Preserve as non-regressions in every task and prove at integration boundaries | A1–B3, Integration |
