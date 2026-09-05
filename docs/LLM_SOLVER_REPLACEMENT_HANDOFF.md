# LLM handoff: replacing the CANDU game diffusion solver

This document is the compact technical context for an LLM that will replace
the current spatial diffusion implementation. It describes the game and the
contracts the replacement must preserve. It is not a reactor-safety model and
it is not permission to add shutdown, scram, accident, or operator-training
features.

## Mission and product boundary

CANDU Refuelling Game is a deterministic Unity game about maintaining a
steady-state CANDU reactor through on-power refuelling. The player reads a
380-channel core heat map, inspects the 12 bundles in a channel, chooses a
refuelling direction and four- or eight-bundle shift, previews the result,
commits it, and watches power, burnup, tilt, score, and inventory respond.

The playable game is the priority. The current model is intentionally synthetic
and observable rather than plant-grade. A solver replacement must preserve the
refuelling loop, immediate feedback, scoring, debug controls, deterministic
replays, and Unity/browser presentation seams. DRAGON5 and DONJON5 are offline
tools only; Unity and browser runtime code must load compact versioned data and
must never invoke those executables.

## Authority and code layout

~~~text
Unity views / debug menu
        |
UnityGameController + UnityRuntimePort
        |
ReactorSim.Game.GameSession
        |
ReactorSim.Core state transitions + full-core solver
        |
embedded synthetic data pack -> future admitted offline export
~~~

- src/ReactorSim.Core is engine-neutral deterministic C# (netstandard2.1).
  It owns topology, bundle state, refuelling transitions, burnup, spatial
  contracts, and the current solver.
- src/ReactorSim.Game owns the reusable application session. GameSession owns
  the authoritative practice core state and the accepted full-core solve,
  translates player commands, integrates burnup, scores actions, and creates
  immutable presentation snapshots.
- unity/ReactorGame is presentation and input. UnityRuntimePort forwards
  commands to GameSession; Unity does not duplicate physics rules.
- src/ReactorSim.Browser and web/candu-playtest provide a static companion
  playtest. Browser Play calls GameSession through the versioned WASM bridge.
  Browser Lab directly exercises the lower-level spatial contracts on an
  explicit synthetic two-channel/eight-position fixture.

The most relevant files are:

- src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs
- src/ReactorSim.Core/Domain/FullCoreDiffusionDataPackContracts.cs
- src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs
- src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs
- src/ReactorSim.Core/Domain/SpatialConvergenceContracts.cs
- src/ReactorSim.Core/Domain/SpatialStencilContracts.cs
- src/ReactorSim.Core/Domain/Candu6CoreTopologyContracts.cs
- src/ReactorSim.Core/Domain/GameRefuellingContracts.cs
- src/ReactorSim.Game/GameSession.cs
- src/ReactorSim.Game/PracticeGameSessionFactory.cs
- src/ReactorSim.Game/CorePresentationContracts.cs
- src/ReactorSim.Browser/LabPlaytestSession.cs
- docs/spec/two-group-solver-normalization-convergence-v1.md
- docs/spec/topology-indexing-units-boundaries-v1.md

## Current solve path

PracticeGameSessionFactory loads the embedded JSON pack, creates the canonical
CANDU-6 topology and stencil through FullCoreDiffusionModelV1.TryCreateCandu6,
then constructs GameSession. The session constructor immediately solves the
initial practice inventory. Every full-core solve follows this path:

~~~text
BundleState[4560]
  -> BundleInventory (canonical slot binding)
  -> burnup/material table lookup for every live bundle
  -> SpatialCoefficientSet (nodes, reciprocal edges, boundaries)
  -> SpatialEigenIteration (two group source iteration)
  -> SpatialOperator + deterministic Jacobi inner solves
  -> SpatialEigenSolve + outer convergence policy
  -> FullCoreDiffusionSolveResultV1
~~~

The current adapter is FullCoreDiffusionModelV1.TrySolve:

~~~csharp
TrySolve(
    IEnumerable<BundleState> bundles,
    double targetPowerWatts,
    double initialEigenvalue = 1.0,
    IReadOnlyList<double>? initialGroup1Flux = null,
    IReadOnlyList<double>? initialGroup2Flux = null)
~~~

It validates the bundle inventory, binds coefficients from the pack using each
bundle's current burnup and material variant, accepts optional previous k and
two flux vectors as a warm start, and returns either a valid converged result
or a diagnostic failure. The normal practice target is
PracticeReferencePowerWatts = 1_000_000_000.0; this is a display/game scale,
not a claim about a plant rating.

The accepted result currently contains:

- Group1Flux and Group2Flux, each in canonical 4,560-node order;
- NodePowerWatts, 4,560 nonnegative node powers;
- TotalPowerWatts;
- EffectiveK and Reactivity;
- PowerBalanceRelativeError;
- SpatialSolve, including status and diagnostics;
- data-pack identity and SolverIdentity.

The replacement may preserve this type and replace its internals, or introduce
a small interface implemented by the new solver. If an interface is added,
keep the game-facing result/projection stable and inject the implementation in
PracticeGameSessionFactory; do not move solver logic into Unity or TypeScript.

## Core state, time, and atomicity

The practice core has 380 channels × 12 physical bundle positions = 4,560
spatial nodes. Each BundleState carries stable identity, explicit
(ChannelId, BundlePosition), material variant, initial burnup, cumulative
fission energy, heavy-metal mass, insertion time, and optional detailed state.
Current burnup is derived as:

~~~text
current_burnup_J_per_kg_HM = initial_burnup_J_per_kg_HM
                            + cumulative_fission_energy_J / heavy_metal_mass_kg
~~~

The current practice initializer creates 380×12 NAT-U-SYNTHETIC bundles with
deterministic radial/axial burnup, 20.6 kg HM per bundle, and 128 fresh bundles.

Refuelling is an immutable atomic transition in
SyntheticGameCoreStateV1.TryRefuel:

- toward-end-b: insert fresh bundles at positions 0..shift-1, move old
  positions upward by shift, and discharge old positions 12-shift..11;
- toward-end-a: insert fresh bundles at positions 12-shift..11, move old
  positions downward by shift, and discharge old positions 0..shift-1;
- only shifts of 4 or 8 are accepted;
- fresh inventory, bundle identities, positions, operation count, and last
  operation metadata update together.

GameSession.RefuelChannel first creates the candidate inventory, solves it,
and only then swaps _coreState and _fullCoreSolve and applies score. If the
candidate solve fails, the command is rejected and the accepted session state
must remain unchanged. PreviewRefuelChannel performs the same candidate solve
but returns a projected core without mutating inventory, score, or counters.

During AdvanceWallMilliseconds, the scenario runtime advances first. For each
positive state segment, GameSession uses the accepted node powers and the
current power response to integrate fission energy in chunks of at most 600
simulation seconds. A changed bundle invalidates its accepted power/binding
metadata. The full-core solve is recomputed when simulation time has advanced
by at least 3,600 seconds, normally with the previous k and flux vectors as
warm starts. The rendered UI is not the simulation clock and must not drive
the numerical iteration.

## Topology and indexing contract

Never infer adjacency from an array offset. The explicit CoreTopology and
SpatialStencil are authoritative.

- ChannelId and BundlePosition are zero-based.
- Flat storage index is channel_id * 12 + bundle_position.
- Canonical node order is ascending channel, then ascending bundle position.
- Channel IDs are row-major over a stepped 22×22 display lattice with row
  lengths:

  ~~~text
  6, 12, 14, 16, 18, 18, 20, 20, 22, 22, 22,
  22, 22, 22, 20, 20, 18, 18, 16, 14, 12, 6
  ~~~

- Cardinal neighbors connect the same bundle position in adjacent channels.
- Axial neighbors connect adjacent positions inside one channel with explicit
  TowardEndA/TowardEndB directions.
- Outer cardinal faces and both channel-end faces are explicit vacuum
  boundaries in the CANDU-6 stencil. Reflective and specified-leakage labels
  are supported by the generic spatial contracts.
- Flow direction alternates by (column + display_row) % 2; it is gameplay and
  channel metadata, not permission to reorder spatial nodes.
- Neighbor terms sort by direction rank
  North, East, South, West, TowardEndA, TowardEndB, then target channel and
  target position. Boundary terms sort by face rank
  North, East, South, West, EndA, EndB, then position.

Every reciprocal interior edge has one shared conductance key. Both endpoint
rows must refer to that same conductance. Every explicit boundary face has one
conductance record whose value agrees with its boundary classification.

## Two-group physics contract

Group 1 is fast and group 2 is thermal. The canonical energy-group order is
["fast", "thermal"]. All authoritative quantities are finite IEEE-754
double values with these units:

| Quantity | Unit |
| --- | --- |
| scalar flux phi_g,i | m^-2 s^-1 |
| absorption, fission, downscatter, nu*fission | m^-1 |
| node volume | m^3 |
| energy per fission | J |
| edge/boundary conductance | m^2 |
| node/total power | W |
| k, rho | dimensionless |

For node i:

~~~text
Sigma_r,1 = Sigma_a,1 + Sigma_s,1->2
Sigma_r,2 = Sigma_a,2

F_i(phi) = nuSigma_f,1,i * phi_1,i
         + nuSigma_f,2,i * phi_2,i

A_g(phi_g)_i = Sigma_r,g,i * phi_g,i
              + (1 / V_i) * [
                    sum_j T_g,ij * (phi_g,i - phi_g,j)
                  + sum_boundary B_g,if * phi_g,i
                ]

A_1(phi_1) = chi_1 * F(phi) / k
A_2(phi_2) = Sigma_s,1->2 * phi_1 + chi_2 * F(phi) / k
~~~

Local fission power and total power are:

~~~text
P_i     = V_i * E_f,i * (Sigma_f,1,i * phi_1,i
                       + Sigma_f,2,i * phi_2,i)
P_total = sum_i P_i
~~~

After obtaining a positive finite shape, normalize both flux groups by
alpha = P_target / P_total. This changes amplitude and power, not k or
shape ratios. k is not forced to 1. State-level reactivity is always
rho = (k - 1) / k.

The coefficient contract requires positive node volume and energy per fission;
nonnegative cross sections and downscatter; Sigma_a >= Sigma_f in each
group; Sigma_f == 0 if and only if nuSigma_f == 0; positive finite implied
neutron yield for nonzero fission; and chi_1 + chi_2 == 1. Current pack
loading stores only chi_1 and derives chi_2 = 1 - chi_1. Fission absorption
is already included in Sigma_a and must not be added again to removal.

## What the current numerical engine does

The current implementation is a sequential source/eigen iteration:

1. Start from supplied fluxes or an all-ones shape, then normalize to the
   target power.
2. Compute volume-integrated fission production Q from the normalized state.
3. Solve group 1 for fission source chi_1 * F / k.
4. Solve group 2 for downscatter from the new group-1 solution plus
   chi_2 * F / k.
5. Update k with k_next = k * Q_trial / Q_current.
6. Normalize the trial flux to target power and recompute production from the
   normalized flux. The next iteration uses those recomputed values, not stale
   trial values.
7. Evaluate residuals and source-shape change; return only after all outer
   convergence conditions pass.

SpatialOperator is matrix-free. Its removal-plus-leakage application is
deterministic and uses the explicit stencil. The current inner method is
Jacobi (jacobi-v1): it applies the operator, performs
x_next = x + (b - A*x) / diagonal, rejects negative/non-finite values, and
accepts only when the global absolute or relative infinity residual passes.

The embedded practice pack currently specifies:

~~~text
inner absolute residual tolerance = 1e-10
inner relative residual tolerance = 1e-6
inner maximum iterations          = 64
outer k absolute tolerance        = 1e-4
outer k relative tolerance        = 1e-3
outer residual tolerance          = 2e-3
outer source-shape tolerance      = 1e-3
outer power-balance tolerance     = 1e-12
outer maximum iterations          = 300
~~~

An outer iteration converges only if:

~~~text
delta_k_abs <= k_abs_tol OR delta_k_rel <= k_rel_tol
residual_relative_infinity <= residual_tol
source_shape_change_infinity <= source_shape_tol
power_balance_relative <= power_balance_tol
~~~

Residuals use global infinity norms over both groups and all nodes. Reductions
are left-to-right in canonical node/group/local-term order; do not introduce
unordered parallel reduction or silent precision changes if deterministic
replay parity is required.

Invalid coefficients, incompatible dimensions, zero production, non-finite or
negative flux/power, failed inner solve, forbidden upscatter, or exhausted
outer iterations fail closed. A failed or nonconverged spatial result has no
usable final state. The current low-level result exposes diagnostic counters
for invalid coefficients, negative flux, non-finite values, failed inner
solves, rejected upscatter, and clamps; successful v1 operation uses no clamps.

## Current data pack

The embedded resource is
src/ReactorSim.Core/EmbeddedData/candu6-two-group-diffusion-pack-v1.json.
Its important identity fields are:

~~~text
schema_version       = 1
data_pack_version    = candu6-two-group-diffusion-v1-infinite-cell-calibrated
topology_schema_id   = candu6-380x12-grid-v1
units_profile_id     = SI-v1
model_id             = candu6-two-group-full-core-diffusion-v1
solver_id            = spatial-eigen-jacobi-v1
energy_group_order   = fast, thermal
evidence_class       = synthetic-calibrated
~~~

The current geometry is synthetic: node volume 0.05 m^3, axial conductances
(0.0008, 0.0004) m^2, transverse conductances (0.0012, 0.0006) m^2, and
vacuum boundary conductances (0.0024, 0.0012) m^2 for groups 1 and 2. It has
one NAT-U-SYNTHETIC burnup table with strict knots from 0 through
1.728e12 J/kg_HM (0 through 20 MWd/kg HM), linearly interpolated without
extrapolation or clamping.

The loader computes topology/content digests and rejects unsupported schema,
units, model, solver identity, dimensions, bad provenance, bad checksums, or
non-finite numbers. It currently hard-codes acceptance of
spatial-eigen-jacobi-v1; a replacement solver must intentionally update the
solver identity/loader policy and pack version rather than silently reusing the
old identity.

## Presentation contract that must remain playable

GameSession maps the solve into GameCorePresentationSnapshot:

- exactly 380 channel snapshots;
- each channel has average burnup, channel watts, local power fraction,
  local absolute tilt, flow direction, and exactly 12 bundle snapshots;
- each bundle has position, stable ID, fuel type, burnup in MWd/kg_HM, watts,
  insertion time, and state version.

GamePhysicsPresentationSnapshot exposes source/model ID, solve state,
authority flag, binding version, reference/target/total/channel/bundle watts,
power amplitude, actual power fraction, k, rho, power-balance error, solver
identity, iteration count, and residual. These values must remain finite; absent
measurements use null at the browser JSON boundary, never NaN or infinity.

The displayed actual power is deliberately separate from the operator setpoint:

~~~text
actual_power_fraction = requested_power_fraction
                       * current_effective_k / reference_effective_k
total_power_watts      = solved_shape_total_watts * actual_power_fraction
~~~

At startup the current and reference k are equal, so actual power starts at
the requested fraction. Burnup integration uses the actual projected bundle
watts. Refuelling re-solves the shape and changes k, local powers, total
power, and score; it must not be replaced with a generic additive refuelling
response or a background decay timer.

## Replacement guidance

The safest implementation is to replace the numerical backend behind
FullCoreDiffusionModelV1 or behind a new Core-level solver interface while
leaving GameSession, Unity, and browser Play unchanged.

The new solver should:

1. accept the same explicit bundle/topology/material state and target watts;
2. preserve canonical node ordering and the 380×12 mapping;
3. bind each live bundle to the selected data-pack/material/burnup state;
4. produce finite nonnegative node powers and a positive total power;
5. provide k, state-level rho, power-balance diagnostics, solve status, and
   a stable solver/data identity;
6. either provide compatible fast/thermal flux vectors or make the warm-start
   and low-level Lab compatibility decision explicit;
7. reject failed/nonconverged candidates without publishing them;
8. preserve pack provenance, units, group order, topology digest, content hash,
   and versioned solver identity; and
9. keep the runtime independent of DRAGON5/DONJON5 executables.

If the replacement is a direct DONJON/TRIVAC export consumer rather than a
runtime numerical solve, it still needs a runtime interpolation/binding layer
that maps every live bundle's burnup/material state to the node power and
criticality quantities expected above. Do not put raw vendor/private files in
the runtime pack unless redistribution is lawful.

The browser Lab coupling is the main deliberate secondary compatibility point:
LabPlaytestSession currently calls SpatialEigenIteration.TryCreate,
SpatialEigenSolve.TryCreate, and reads SpatialEigenIterationState plus
SpatialSolveDiagnostics. Either retain those types as an adapter around the
new backend, or update Lab and its tests as part of the solver migration. Do
not let a layout-only compatibility fixture be reported as authoritative
solver output.

## Acceptance checks

At minimum, preserve or replace focused tests for:

- embedded pack/topology compatibility and full-core convergence;
- normalized total power and positive bundle/node powers;
- fresh-bundle refuelling changing k/power in the expected direction;
- one-node algebra, symmetric-node shape, record-order independence, and
  deliberate nonconvergence fail-closed behavior;
- preview non-mutation and committed refuelling atomicity;
- burnup integration from actual projected bundle watts;
- browser Play parity and Lab failure semantics; and
- Unity EditMode/PlayMode Bootstrap, Core Map, refuelling, and debug-menu smoke.

Useful commands after implementation are:

~~~powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Test-Focused.ps1 -Project tests/ReactorSim.Core.Tests/ReactorSim.Core.Tests.csproj -FullyQualifiedName ReactorSim.Core.Tests.FullCoreDiffusionModelTests.FullCoreSolveConvergesAndNormalizesPower
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
powershell -ExecutionPolicy Bypass -File tools/Test-UnityImport.ps1
~~~

The owner’s primary acceptance path remains: launch the Unity Bootstrap scene,
run time at multiple speeds, inspect channels, preview and commit refuelling in
both directions, observe actual power/burnup feedback, and exercise the debug
menu. Do not broaden the change into shutdown, scram, accident progression, or
plant simulation.
