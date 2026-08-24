# Nuclear Diffusion Theory in a CANDU Reactor

**Guide version:** 2.8
**Last checked:** 2026-08-24
**Status:** Living documentation; update when the physics specifications,
runtime implementation, validation gates, or game rules change.

This guide explains the physics and numerical methods behind the CANDU
refuelling game in plain language. It deliberately separates four things:

1. physics that exists in executable project code;
2. physics that is written down as a candidate engineering contract;
3. physics planned for later implementation or investigation; and
4. physics that is explicitly outside the game.

The distinction matters. A detailed equation in a specification is not yet a
working simulator, and a number reported by a paper is not automatically a
project constant.

## The short project answer

### What physics is implemented now?

**A bounded static-solver foundation is implemented in Core.** It is not a
feature-complete or validated reactor simulator. The completed Phase 3 work
provides deterministic topology, inventory, snapshots, clock/queue, archive,
replay, atomic application, and invalid-data contracts. G3 is a technical PASS.

The completed Phase 4 slices provide:

- P4-T01: deterministic topology-driven node, neighbour, and boundary-stencil
  assembly;
- P4-T02: coefficient/conductance binding and a matrix-free
  removal-plus-leakage operator; and
- P4-T03: one immutable two-group source/eigen iteration with deterministic
  scalar Jacobi inner solves, eigenvalue update, and power normalization; and
- P4-T04: caller-supplied outer convergence, zero-safe residual diagnostics,
  deterministic failure reasons, and fail-closed invalid-state results.
- P4-T05: synthetic one-node algebraic/no-leakage, symmetric, homogeneous,
  deliberate-nonconvergence, and canonical-order determinism coverage.
- P4-T08: a package-free synthetic Core benchmark that reports machine-specific
  timing and allocation observations without establishing a performance target.
- P4-T06-R3: a frozen boundary for an offline reduced/synthetic producer and
  interpolation data, with the existing coefficient-table contract as the only
  runtime-facing material-data surface.
- P5-T01: declarative 4-bundle/8-bundle scheme metadata, canonical insertion
  and discharge position plans, explicit flow-direction binding, and fail-closed
  catalog validation.
- P5-T02: an immutable, all-or-nothing location-layer shift that preserves
  retained bundle identities and represented basic state, creates fresh
  zero-energy entries, and returns deterministic ordered discharge states.
- P5-T03: named immutable refuelling mapping and atomicity event bodies,
  canonical position bindings, paired event-log append, and basic discharge
  audit records over the current Core bundle fields.
- P5-T04: immutable accepted-power projections, explicit positive-time
  left-endpoint energy integration, derived burnup, monotonicity/overflow
  checks, and proposed core-state-version advancement over the current Core
  bundle fields.
- P5-T05: immutable burnup-indexed coefficient values, strict ordered table
  metadata, coefficient-domain validation, exact-knot and linear-interpolation
  lookup, derived `chi_2`, and fail-closed out-of-range behavior.
- P5-T06: explicit epoch/period/index spatial cadence, burnup-table-to-node
  coefficient rebinding, preserved validated conductances, deterministic
  spatial solving, and atomic lifecycle spatial/power binding acceptance.
- P5-T07: explicit Phase 5 invariant reports for bundle count, identity,
  location, nonnegative derived burnup, exact energy accounting, interval
  power-times-time accounting, monotonicity, and refuelling preservation,
  insertion, and discharge partition checks.
- P5-T08: test-only deterministic multistep S4/S8 history evidence with
  explicit interval/refuelling ordering, shuffled-input replay equality,
  per-bundle burnup records, ordered discharge traces, and identity non-reuse.
- P5-T09: complete I/Xe-bearing lifecycle, discharge, power-history, digest,
  and transaction contracts over the frozen Phase 2 state boundaries.
- P5-T10: structural sequence mapping for the G4-R6-approved ReducedModel
  authority, including exact final-node burnup mapping and the approved golden
  consumer.
- P5-T11 through P5-T16: complete-state burnup and lookup binding, source and
  history digest authentication, lifecycle-bound snapshot state, persistence-
  safe archive round-trip, restore-time reconciliation, and restart-self-
  contained snapshot reconstruction.
- P6-T01 through P6-T07: a limited, engine-neutral regulating-system contract
  for 14 logical liquid zones, adjusters, bulk poison, controller projections,
  queue transitions, and five deterministic synthetic scenarios. This is
  synthetic/test-only evidence, not an external reactor-control model.

These slices now establish a bounded, project-authored ReducedModel validation
baseline for the engine-neutral Core. G4-R6 is `PASS` for the selected
ReducedModel scope, and G5 is `PASS` for Phase 5 within that same boundary.
The result includes approved golden-consumer evidence and the complete-state
correction chain through P5-T16. It does not establish direct production or
full-core CANDU/DONJON5/DRAGON5 authority, production thresholds, or a
playable CLI game loop. R4/R5 and the earlier G4-R3/G4-R5 dispositions remain
preserved as historical candidate/no-admission evidence; RRS and poison
runtime behavior remain later-phase work.
P5-T02 is bounded to the fields represented by the current immutable
`BundleInventory`; fresh-template resolution, complete discharge/event
envelopes, full power-history/digest lifecycle binding, coefficient lookup,
and later physics integration were completed only by later bounded tasks.
P5-T03 adds the approved
mapping/atomicity body shapes and basic discharge evidence, but does not yet
claim the complete I/Xe-bearing `DischargeRecordV1` or `RefuelTransactionV1`
projection. P5-T04 integrates only the current energy/mass fields. P5-T05 adds
the bounded coefficient-table domain and lookup contract, but does not claim
canonical data-pack byte serialization/checksum verification, solver-side
coefficient recomputation, the complete P2-T03 `PowerSnapshot`, or a
single-owner lifecycle commit. P5-T06 adds a bounded coefficient-to-spatial-
state recomputation boundary and exact periodic cadence/lifecycle binding, but
does not claim the full scheduler, kinetics/I-Xe transitions, canonical
data-pack checksum serialization, complete power-history binding, or reference
and golden evidence. P5-T07 adds the exact bounded invariant checks over the
current Core inventory, burnup interval, and refuelling result contracts. P5-T08
adds deterministic synthetic multistep history evidence by composing those
contracts, but does not claim a production history schema, complete
I/Xe/lifecycle/discharge envelope, reference comparison, or golden evidence.
P5-T09 now supplies the complete bounded lifecycle/I-Xe/power-history and
transaction contracts. P5-T10 is complete for the G4-R6-approved ReducedModel
path and supplies structural sequence evidence; P5-T11 through P5-T16 complete
the authenticated transition, persistence, and restart boundary. Phase 5/G5
is closed `PASS` for the approved ReducedModel/Core scope. Phase 6/G6 is a
`CONDITIONAL PASS` for the bounded synthetic/test-only Core/ReducedModel scope:
the implementation is complete for that evidence boundary, but an applicable
RRS comparison package is still required before an unconditional or external
claim is possible.

The 2026-08-24 recovery handoff also completed `TEST-INFRA-01`,
`P6-INTEGRATION-01`, `P6-INTERNALS-01`, and `CORE-NAMING-01`. Those tasks
refreshed test artifact portability, the RRS queue-admission seam, internal
canonical-byte ownership, and the task-ID compatibility inventory. They did
not change the physics model, public schema, golden authority, or G6 scope.
`STATUS-VISIBILITY-01` is complete as the derived-documentation handoff; no
named next task is selected until the owner decides whether to start planned
Phase 7 or re-sequence the explicitly non-physics Phase 8 CLI slice. The
project scope register and linked reports remain authoritative.

The implemented Phase 1 work is an **offline reference pipeline**:

- pinned DRAGON5 and DONJON5 smoke-case provenance and runners;
- strict parsers/exporters for selected reference output;
- compact parser-only fixtures, manifests, hashes, and repeatability checks.

Those tools establish how future evidence may be collected. They do not run at
game time and their `KINF` or `K-effective` observations are not runtime
constants, solver targets, or golden data.

### What exists as a physics design?

The completed P2-T01 through P2-T05 tasks define frozen, engine-neutral
implementation inputs for:

- explicit CANDU-style topology and boundary metadata;
- a static two-group diffusion eigenproblem;
- power normalization and deterministic source iteration;
- refuelling, bundle identity, burnup, and coefficient interpolation;
- point kinetics, I-135/Xe-135, regulating control, and optional feedback;
- observables, deterministic serialization, and validation evidence.

G2 is FORCED CLOSED / WAIVED: that administrative decision permits use of the
existing specifications where no critical blocker is introduced, but it is not
a technical PASS and does not approve a new equation, tolerance, golden value,
or public contract. The specifications remain the blueprint for the remaining
implementation; their numerical acceptance thresholds and production reference
comparisons remain owned by later gates.

### What remains before the static solver is feature-complete and validated?

1. G4-R6 has approved the bounded project-authored ReducedModel authority and
   six quantity-specific profiles. The earlier G4-R3/G4-R5 blockers remain
   historical records and do not become production approval.
2. G5 is complete for the approved ReducedModel/engine-neutral Core scope.
   P5-T10 through P5-T16 provide structural sequence, complete-state,
   authentication, persistence, and restart evidence; the final validation
   snapshot is recorded in Section 6.7 and the G5 gate report.
3. Direct production/full-core external authority and production thresholds
   remain deferred. G6 conditionally passes the synthetic/test-only regulating
   system; an applicable RRS comparison package and G6 re-entry remain needed.
   Phase 7A adds kinetics and xenon; Phase 7B remains optional feedback work.

## 1. Why a reactor can become a game

A reactor is a system in which neutrons cause fissions, fissions release heat,
and the player changes the conditions that determine how many neutrons survive
to cause more fissions. The game does not need to reproduce every detail of a
power station. It needs a stable chain of cause and effect:

```text
player action
    -> fuel/control state changes
    -> neutron distribution and power change
    -> burnup and isotope history change
    -> later choices become easier or harder
```

The intended game is a deterministic, text-first strategy game about online
refuelling of a CANDU-style reactor. The player chooses when and where to move
fuel and how to use limited regulating devices. The simulation then explains
the consequences through power, flux shape, burnup, xenon, tilt, and control
state.

This is an entertainment simulation, not operator training, a safety analysis,
or a model of a named commercial station.

## 2. Background: atoms, fission, and chain reactions

### 2.1 The nucleus and isotopes

An atom has a small nucleus surrounded by electrons. The nucleus contains
protons and neutrons. An isotope is identified by its number of protons and
neutrons. Uranium-235, for example, is an isotope that can fission after
absorbing a neutron.

In fission, a heavy nucleus absorbs a neutron and splits into lighter nuclei.
The event releases:

- kinetic energy of the fission fragments, which becomes heat;
- additional neutrons;
- radioactive fission products; and
- gamma radiation and other energy.

Some emitted neutrons are fast. They lose energy by colliding with matter. A
slow neutron is called thermal when its energy is close to the thermal motion
of the surrounding material. Thermal neutrons are especially important in a
heavy-water moderated reactor because they are effective at causing fission in
the fuel.

### 2.2 The chain reaction

The number of neutrons in one generation compared with the previous generation
is summarized by the effective multiplication factor, `k`:

```text
k > 1  -> neutron population tends to grow
k = 1  -> neutron population is steady
k < 1  -> neutron population tends to shrink
```

`k` is dimensionless. It is not the same as power. A reactor can have a stable
shape calculation with a particular `k`, while a separate amplitude variable
sets the actual power represented by that shape.

The project uses reactivity as a convenient signed quantity:

```text
rho = (k - 1) / k
```

Positive `rho` tends to increase power amplitude; negative `rho` tends to
decrease it. The project does not add separate hidden `rho_xenon` or
`rho_controller` terms. Those effects change absorption coefficients before the
spatial solve, and the resulting `k` supplies the one reactivity value.

## 3. What makes a CANDU-style reactor special?

The guide uses CANDU-style language without claiming a specific station
geometry. The important concepts are:

- **Heavy water:** deuterium oxide is used as moderator and, in a typical
  CANDU arrangement, coolant. Its low neutron absorption helps make efficient
  use of natural uranium fuel.
- **Pressure-tube channels:** fuel is arranged in many horizontal channels
  rather than one large pressure vessel. The project represents a production
  configuration as 380 channels with 12 bundle positions per channel, giving
  4,560 explicit channel-position nodes.
- **Fuel bundles:** each position owns a persistent bundle identity. A bundle
  can move, accumulate energy and burnup, carry I/Xe history, and eventually be
  discharged.
- **On-power refuelling:** fuel movement can occur while the reactor is
  operating. The game turns this into a central decision: refuelling changes
  local reactivity and future fuel value without resetting the whole core.
- **Regulating devices:** liquid-zone controllers, adjusters, and moderator
  poison can change local absorption and therefore power shape. The project
  includes only limited regulating behavior, not shutdown or accident systems.

The literature digest contains useful CANDU context, including multigroup to
two-group workflows, burnup/refuelling histories, xenon, and regulating-device
roles. It does not authorize a runtime number or a golden case. Relevant rows
include S1-R02, S1-R03, S1-R05, S1-R06, S1-R07, S1-R08, S1-R09, S5-R03,
S5-R04, S5-R08, S6-R02, S6-R03, and S6-R05.

## 4. From neutron transport to diffusion theory

### 4.1 The transport problem

The most detailed common description follows neutrons in position, direction,
energy, and time. It asks questions such as:

- where is a neutron?
- which direction is it moving?
- what is its energy?
- will it scatter, be absorbed, or cause fission?

That full transport description is expensive. A game needs a reduced model that
preserves important spatial and temporal cause-and-effect at interactive speed.

### 4.2 The diffusion approximation

Diffusion theory makes a simplifying assumption: in a sufficiently scattering,
nearly isotropic region, neutron flow can be estimated from the gradient of
neutron flux. In ordinary notation, a one-group diffusion balance resembles:

```text
-div(D grad(phi)) + Sigma_a * phi = source
```

Here `phi` is scalar flux, `D` is a diffusion coefficient, and `Sigma_a` is a
macroscopic absorption cross section. The first term represents leakage from
one region to another; the second represents neutrons removed by absorption.

The project does **not** derive its runtime spatial coefficients from a guessed
geometry formula. The candidate P2-T02 contract uses preassembled finite-volume
edge conductances `T` and boundary conductances `B`. This keeps the topology,
units, and coefficient provenance explicit.

Diffusion is an approximation. It becomes less reliable near sharp voids,
strongly directional streaming, or detailed boundaries. Those limitations are
part of why DRAGON5/DONJON5 remain offline reference tools rather than runtime
dependencies.

#### 4.2.1 The reduced offline data boundary

The game-facing model is intentionally smaller than a transport calculation.
P4-T06-R2D retired direct Candu6.x2m admission into the P2-T02 runtime
contracts, and P4-T06-R3 freezes the replacement boundary. An offline
synthetic or separately admitted reduced producer may create material values
for burnup-indexed interpolation, but the runtime consumes only the existing
validated `BurnupCoefficientTableV1` projection.

The table owns material coefficients, burnup knots, material identity, units,
provenance, version, and checksum identity. It does not own topology, flow
direction, node volume, edge or boundary conductance, flux, power,
normalization, or convergence policy. Those values remain explicit inputs to
the existing topology-bound `SpatialRecomputeRequestV1` and P2-T02 solver.
Runtime lookup is exact-knot or linear interpolation within the table domain;
out-of-range, invalid, stale, or ambiguously mapped data fails closed. The
reduced producer, reference tools, exact reference data, and raw artifacts
remain offline. See the [R3 boundary specification](../spec/reduced-model-interpolation-boundary-v1.md).

### 4.2.2 Models used by tests and gates

The project uses several related models for different kinds of evidence. They
must not be read as one interchangeable reactor model. A test can pass because
it proves a contract, a gate can pass for a bounded ReducedModel domain, and
the direct production or full-core authority can still remain deferred.

```text
                         offline only
                 +-------------------------+
                 | DRAGON5 / DONJON5 cases  |
                 | reference provenance    |
                 +------------+------------+
                              |
                              v  data and case boundary, never a runtime call
                 +-------------------------+
                 | P2-T02 two-group         |
                 | finite-volume contract   |
                 +------------+------------+
                              |
             +----------------+----------------+
             |                                 |
             v                                 v
  +------------------------+       +----------------------------+
  | Synthetic solver cases |       | Representative ReducedModel|
  | 1, 2, and 3-node tests |       | 4 channels x 12 positions   |
  | P4-T05 and G4I / R4    |       | G4J candidate -> R5 blocked |
  +------------+-----------+       | G4K oracle -> R6 approved   |
               |                   +-------------+--------------+
               +-------------------------+-------+
                                         v
                         +-------------------------+
                         | Engine-neutral Core    |
                         | spatial and state code |
                         +------------+------------+
                                      |
                                      v
                         +-------------------------+
                         | Phase 5 state/history  |
                         | ledger and persistence |
                         +------------+------------+
                                      |
                                      v
                         +-------------------------+
                         | G5 PASS for the       |
                         | ReducedModel/Core     |
                         | boundary only         |
                         +-------------------------+
```

The model cards below explain what each layer means.

- **Offline reference model.** DRAGON5 and DONJON5 are used to establish
  reference-case provenance and, when separately admitted, compact offline
  data. They are never called by the runtime. No current G4 or G5 result
  claims direct DRAGON5, DONJON5, or named-station authority.
- **Frozen two-group diffusion contract.** P2-T02 defines the finite-volume
  two-group eigenproblem, conductance-based leakage, source ordering, signs,
  normalization, and convergence diagnostics. P4-T01 through P4-T05 exercise
  this contract in engine-neutral Core code. It is the approved equation and
  data boundary, not by itself a full-core validation result.
- **Synthetic solver model.** P4-T05 uses the one-node/no-leakage algebraic
  example, a two-node reciprocal symmetric case, and a homogeneous three-node
  chain, plus deliberate nonconvergence and input-order permutations. P4-T06-G4I
  adds a heterogeneous four-node manufactured solution. These models test
  arithmetic, signs, normalization, failure reporting, and determinism. They
  are synthetic mathematical checks and cannot become direct CANDU authority.
- **Representative ReducedModel authority.** The G4K package retains the
  explicit 4-channel by 12-position topology: 48 nodes, 92 reciprocal edges,
  104 boundaries, alternating flow, burnup projection, two S4 shift
  directions, and overlay cases. The five scenarios are `fresh_start`,
  `equilibrium_like`, `refuelled_4_bundle_shift`, `rrs_tilt_perturbation`,
  and `bulk_poison_perturbation`. G4J was a deterministic project-authored
  candidate, but G4-R5 correctly left it `Candidate` / `Deferred` / `NoGolden`.
  G4K generated expected values through a separate Python/NumPy dense
  generalized-eigen path. G4-R6 then approved an `ApprovedGolden` payload and
  six quantity-specific profiles for the bounded `ReducedModel` domain only.
- **Phase 5 state/history model.** P5-T01 through P5-T16 add the ledger around
  the spatial model: explicit S4/S8 movement, bundle identity and location,
  accepted power, energy and burnup, coefficient lookup, spatial recompute
  cadence, I/Xe-bearing lifecycle state, event history, digest binding, and
  save/load restoration. P5-T10 and G5 use the approved ReducedModel projection
  to check structural sequence mapping and exact accounting/authentication
  contracts. They do not claim an independent external numerical-drift result.
- **RRS and feedback extension.** The RRS tilt and bulk-poison cases above are
  controlled overlays in the bounded ReducedModel evidence. P6-T01 through
  P6-T07 now implement the limited 14-zone, adjuster, poison, controller,
  queue, and scenario contracts as synthetic/test-only Core evidence. G6 is a
  `CONDITIONAL PASS`, because no applicable external or production RRS
  comparison package is admitted.

| Evidence | Model used | What a passing result means | Boundary that remains |
| --- | --- | --- | --- |
| P4-T05 synthetic tests | Algebraic one-node and toy two/three-node diffusion cases | Frozen operator, normalization, diagnostics, nonconvergence, and deterministic ordering behave as specified | No external or production numerical authority |
| P4-T06-G4I / G4-R4 | Four-node heterogeneous manufactured solution | Synthetic spatial behavior can be reproduced and, for the bounded synthetic domain, conditionally admitted | Not a CANDU, transport, burnup, or full-core result |
| P4-T06-G4J / G4-R5 | Project-authored 48-node ReducedModel candidate with five scenarios | Candidate generation and Core comparison are deterministic | Candidate remained unapproved; G4-R5 remained blocked |
| P4-T06-G4K / G4-R6 | Same bounded ReducedModel domain with independent dense generalized-eigen expected values | Five scenarios and six quantity profiles support `ApprovedGolden` use for `ReducedModel` | No direct DRAGON5/DONJON5, production, release, safety, or 380-channel claim |
| P5-T10 / G5 | ReducedModel sequence plus the Phase 5 bundle/lifecycle ledger | Refuelling movement, identity, energy/burnup mapping, digests, persistence, and restart contracts pass exactly within the admitted scope | Not an independent external burnup or full-core numerical comparison |
| Phase 6 / G6 | Synthetic/test-only RRS state, actuator, queue, and five-scenario model | P6-T01 through P6-T07 complete; G6 `CONDITIONAL PASS`; 46 focused tests and the full regression pass | No applicable RRS comparison, production, external, or golden authority |

This map is the safest way to interpret the evidence: G4-R5 did not fail to
clear a model that G4-R6 later silently relabelled. R5 rejected the candidate
as authority. G4K supplied a new independent authority package, and R6 passed
only the narrower ReducedModel scope that package actually covered.

### 4.3 Homogenization and energy groups

A real neutron spectrum is continuous in energy. A group model divides it into
energy ranges and replaces detailed behavior by group-averaged coefficients.
The project uses two groups:

- **Group 1:** fast neutrons;
- **Group 2:** thermal neutrons.

The first specification allows downscatter from group 1 to group 2 and rejects
upscatter. Each node/group stores or receives quantities such as:

| Quantity | Meaning | Unit |
| --- | --- | --- |
| `phi_g` | scalar neutron flux | `m^-2 s^-1` |
| `Sigma_a,g` | total absorption | `m^-1` |
| `Sigma_f,g` | fission part of absorption | `m^-1` |
| `nu*Sigma_f,g` | fission cross section weighted by neutron yield | `m^-1` |
| `Sigma_s,1->2` | fast-to-thermal downscatter | `m^-1` |
| `chi_g` | fission spectrum fraction | `1` |
| `V` | node volume | `m^3` |
| `E_f` | energy per fission used for game power | `J` |

The invariant `Sigma_f <= Sigma_a` prevents fission absorption from being
counted twice. The fission spectrum fractions sum to one. All authoritative
arithmetic uses finite `double` values.

## 5. The static two-group model: specified and bounded implementation

P4-T01 through P4-T05 implement the topology/stencil, coefficient/operator,
one-iteration, outer-diagnostics, and synthetic-case portions of the approved
P2-T02 model. This section explains the complete specified target. A returned
synthetic P4-T04 state is not a direct external reference result. The selected
ReducedModel authority is separately admitted by G4-R6 for its bounded scope;
that admission does not claim full-core or production CANDU validation.

### 5.1 Nodes and topology

The spatial node is the explicit pair:

```text
node = (ChannelId, BundlePosition)
```

Channel IDs, positions, coordinates, neighbours, flow direction, and boundary
faces are explicit records. Array order is only a deterministic evaluation
order; it never means physical adjacency or coolant direction.

For a production-sized configuration:

```text
380 channels * 12 positions = 4,560 nodes
```

The topology contract distinguishes transverse channel neighbours from
within-channel neighbours and requires reciprocal records. Boundary faces are
labelled `Reflective`, `Vacuum`, or `SpecifiedLeakage`. A missing boundary
conductance is invalid rather than silently treated as a default.

### 5.2 Two-group eigenvalue equations

Define removal coefficients for the two groups:

```text
Sigma_r,1 = Sigma_a,1 + Sigma_s,1->2
Sigma_r,2 = Sigma_a,2
```

The specified steady equations are:

```text
A_1 phi_1 = (chi_1 / k) * F(phi)

A_2 phi_2 = Sigma_s,1->2 * phi_1 + (chi_2 / k) * F(phi)

F_i(phi) = nu*Sigma_f,1,i * phi_1,i
            + nu*Sigma_f,2,i * phi_2,i
```

`A_g` combines removal and leakage. The positive fission and downscatter terms
are sources on the right-hand side. `k` is the eigenvalue that makes the
neutron balance self-consistent.

### 5.3 Finite-volume leakage

For node `i` and group `g`, the candidate finite-volume operator is:

```text
(A_g phi_g)_i = Sigma_r,g,i * phi_g,i
              + (1 / V_i) * [
                    sum_j T_g,ij * (phi_g,i - phi_g,j)
                  + sum_f B_g,if * phi_g,i
                ]
```

The interior term is conservative: the same conductance appears on both sides
of a reciprocal edge, with opposite flux differences. A high-flux node loses
neutrons through the left-hand leakage term; leakage is not a source.

Reflective faces use `B = 0`. Vacuum and specified-leakage faces require a
positive, approved boundary conductance. The specification does not invent a
Marshak factor or derive a coefficient from an undocumented geometry.

### 5.4 Power and normalization

Local integrated fission power is specified as:

```text
P_i = V_i * E_f,i * [
          Sigma_f,1,i * phi_1,i
        + Sigma_f,2,i * phi_2,i
      ]

P_total = sum_i P_i
```

The caller supplies a positive target power `P_target` in watts. After a valid
nonnegative shape is found:

```text
alpha = P_target / P_total
phi_g,i = alpha * phi_g,i
```

This changes flux amplitude and power, not `k` or the relative shape. The
solver records power before/after normalization, `alpha`, and power-balance
error. Power normalization is separate from criticality.

### 5.5 One implemented iteration and the outer loop

P4-T03 implements one deterministic source/power iteration of this sequence:

- Step 1: validate topology, coefficients, target power, initial flux, and
  policy.
- Step 2: normalize the initial nonnegative shape.
- Step 3: compute fission source `F` and production total `Q`.
- Step 4: solve the group 1 linear system.
- Step 5: solve the group 2 system using downscatter plus fission source.
- Step 6: reject failed, negative, or non-finite trial flux.
- Step 7: update `k` from the ratio of new to old production.
- Step 8: normalize the trial shape again and recompute `F` and `Q` from
  the new normalized state, never stale trial values.
- Step 9: in P4-T04, test convergence or continue with the normalized state.

P4-T03 returns after the normalized step. P4-T04 places repeated steps in the
specified outer convergence decision and exposes the required diagnostics.
The implemented inner method is deterministic scalar Jacobi under a
caller-supplied policy; parallel reduction, Burst, lower precision, and
unordered sums are not part of v1.

### 5.6 Specified convergence and implemented outer diagnostics

The P2-T02 outer-solve contract records:

```text
delta_k_abs
delta_k_rel
residual_absolute_inf
residual_relative_inf
source_shape_change_inf
power_balance_rel
```

Convergence requires all four conditions to pass: an absolute or relative k
change condition, residual condition, source-shape condition, and power-balance
condition. The caller supplies every tolerance and maximum iteration count;
the solver must not invent or relax them. P4-T04 implements this decision and
does not claim a new numeric threshold.

The complete result must fail closed on NaN, infinity, negative flux, zero
production, invalid conductance, failed inner solve, rejected upscatter,
attempted clamp, or nonconvergence. It must not return the last iterate as a
successful solve.

### 5.7 Synthetic case coverage

P4-T05 exercises the frozen contract with committed synthetic inputs only:

- the P2-T02 one-node/no-leakage example is checked algebraically, including
  `k=0.6875` and the normalized shape ratio;
- the smallest valid runtime topology has two bundle positions under P2-T01,
  so a two-node reciprocal case checks equal-flux symmetry and normalization;
- a homogeneous three-node chain checks nodewise shape preservation;
- a one-iteration policy exhaustion checks deterministic nonconvergence, final
  diagnostics, and the absence of a usable state; and
- reversing input coefficient record order must produce identical diagnostics
  and final flux arrays.

These checks establish synthetic contract coverage only. They do not compare
against DONJON5, create golden data, or approve numerical tolerances.

### 5.8 Model geometry and ReducedModel visualizations

The approved geometry has two useful views. The channel-plane view shows the
logical 2x2 placement and the explicit transverse links. The elevation view
below it shows the twelve axial control volumes inside each channel. The
integer `x` and `y` coordinates are topology coordinates, not inferred metre
distances; physical lengths and node volumes remain explicit SI data.

```text
Channel-plane cross-section (logical x/y; each box contains 12 axial cells)

                         y / North
                              ^
        +--------------------+--------------------+
        | Ch 2 (x=0,y=1)     | Ch 3 (x=1,y=1)     |
        | 12 axial cells     | 12 axial cells     |
        | EndA -> EndB       | EndB <- EndA       |
        +--------------------+--------------------+
        | Ch 0 (x=0,y=0)     | Ch 1 (x=1,y=0)     |
        | 12 axial cells     | 12 axial cells     |
        | EndA -> EndB       | EndB <- EndA       |
        +--------------------+--------------------+
                              +------> x / East

 transverse links: 0-1 East/West, 0-2 North/South,
                   1-3 North/South, 2-3 East/West
```

This geometry view is a visualization of the admitted ReducedModel input, not
a claim that a rectangular formula can replace the topology table. Every
channel and every exposed face is still represented by explicit records. The
four channel-plane links are repeated at each of the twelve positions, while
the two channel ends add the explicit `EndA` and `EndB` faces for every
channel. The resulting case has 48 spatial nodes, 92 reciprocal edges, and 104
boundary records.

The elevation/topology view makes the axial indexing and alternating flow
visible:

The approved G4-R6 case is a compact spatial model, not a miniature 380-channel
plant model. Its topology is explicit and its array order is only an evaluation
order. The following schematic shows the shape of the case without implying
that a row in memory is a physical flow direction.

```text
48 spatial nodes = 4 channels x 12 axial bundle positions

channel 0   (0,0) -- (0,1) -- ... -- (0,11)       flow ->
channel 1   (1,0) -- (1,1) -- ... -- (1,11)       flow <-
channel 2   (2,0) -- (2,1) -- ... -- (2,11)       flow ->
channel 3   (3,0) -- (3,1) -- ... -- (3,11)       flow <-
                 ^       ^       ^
                 +-------+-------+  explicit reciprocal transverse edges

Each position also has explicit boundary-face records and two energy groups.
The authority package binds 92 reciprocal edges and 104 boundaries.
```

At each node, the local material state is selected first and the spatial solve
then couples it to neighbouring nodes:

```text
burnup B_b
    |
    v
coefficient table lookup/interpolation
    |
    v
node i = (channel, position)
    |
    +--> fast flux phi_1 ---- downscatter ----> thermal flux phi_2
    |
    +--> absorption and fission source, with k
    |
    +--> reciprocal edge leakage T and boundary leakage B
    |
    v
normalized flux shape, node power, total power, residuals, convergence
```

The Phase 5 history model wraps that spatial solve in an explicit ledger. This
is the model exercised by P5-T10 and the final G5 correction chain:

```text
accepted bundle power P_b(t_n), time step Delta_t
                         |
                         v
       cumulative energy += P_b(t_n) * Delta_t
                         |
                         v
       burnup = initial burnup + energy / heavy-metal mass
                         |
                         v
       bind the burnup-indexed coefficient result
                         |
                         v
       recompute affected spatial state at its explicit cadence
                         |
                         v
       append history, authenticate digests, save or restore fail-closed
```

G5 passes when this ledger preserves the frozen identities, event order,
complete histories, lookup bindings, and authenticated persistence within the
approved ReducedModel/Core boundary. It does not turn the ledger into a direct
external reactor reference.

## 6. Fuel movement, burnup, and coefficient tables

### 6.1 Bundle energy and burnup

For bundle `b`, the authoritative state is cumulative fission energy and heavy
metal mass. Current burnup is derived:

```text
B_b = InitialBurnup_b + CumulativeFissionEnergy_b / m_HM,b
```

For a left-endpoint interval with accepted power `P_b(t_n)`:

```text
Delta_E_b = P_b(t_n) * Delta_t
CumulativeFissionEnergy_next = CumulativeFissionEnergy + Delta_E_b
B_next = InitialBurnup + CumulativeFissionEnergy_next / m_HM
```

The units are explicit:

```text
W * s = J
J / kg_HM = J/kg_HM
```

The interval is split at every event. Burnup is monotone and no step reduction,
clamp, or extrapolation is silently applied.

P5-T04 implements this rule as an immutable Core transition over the current
`BundleInventory` representation. The accepted power projection must bind to
the exact current time, core-state version, and opaque source-state digest, and
contain one finite, nonnegative sample for every live bundle at its explicit
node. Samples and result records use canonical `(ChannelId, BundlePosition,
BundleId)` order.
The transition computes every proposed energy and burnup value before
constructing a replacement inventory, increments the proposed core-state
version exactly once, and marks the consumed power snapshot invalid. Missing,
stale, non-finite, overflowing, or non-monotone values reject with the source
inventory unchanged. The complete P2-T03 snapshot digests/history, coefficient
lookup, and lifecycle-owner commit remain later bounded work.

### 6.2 Declarative refuelling

V1 defines complete 4-bundle and 8-bundle schemes, called `S4` and `S8`. A
scheme is one atomic transaction:

- inserted bundles receive explicit templates and fresh identities;
- bundles shift toward the declared flow endpoint;
- the boundary train is discharged;
- existing bundle identities, energy, burnup, and I/Xe history move with them;
- coefficients are looked up for the proposed state; and
- the whole operation commits only if every check succeeds.

`EndAtoEndB` binds to `TowardEndB`; `EndBtoEndA` binds to `TowardEndA`.
Equal-time commands form one all-or-none batch. A failed precondition or
postcondition restores the complete pre-state byte-for-byte.

P5-T01 implements the declarative scheme boundary in Core. A validated catalog
must contain both four-bundle and eight-bundle coverage; each definition keeps
the inserted positions in ascending physical order and produces the endpoint
discharge positions for the channel's explicit flow direction.

P5-T02 applies one of those plans as a pure immutable replacement. The target
channel must be fully occupied; retained bundles move by explicit position
mapping while keeping their stable IDs and represented basic fields, inserted
states arrive at the plan's positions with zero cumulative energy, and the
discharged states are returned in ascending old-position order. Any failed
precondition leaves the original inventory unchanged. This slice does not yet
resolve complete fresh-fuel templates, carry the later I/Xe/lifecycle envelope,
append a command/event/discharge record, integrate burnup, or look up
coefficients.

P5-T03 adds the approved `RefuelMappingBodyV1` and
`RefuelAtomicityBodyV1` shapes. It validates explicit applicability tags,
canonical one-channel position coverage, paired event-envelope time/status,
and appends the mapping/atomicity pair to an immutable event log only when
both records succeed. It also returns deterministic basic discharge audit
records containing the fields currently represented by `BundleState`. P5-T03
itself did not claim the complete I/Xe-bearing discharge and transaction
projections; P5-T09 later completes those bounded contracts. No digest is
invented by the P5-T03 slice.

### 6.3 Burnup-indexed interpolation

Each material variant resolves to a versioned table with strictly increasing
burnup knots. For a value between adjacent knots:

```text
alpha = (B - B_j) / (B_(j+1) - B_j)
c(B) = (1 - alpha) * c_j + alpha * c_(j+1)
```

The canonical `chi_1` is interpolated and `chi_2` is derived as `1 - chi_1`.
Values outside the table domain are rejected. The implementation must not
extrapolate, clamp, repair an invalid coefficient, or independently
interpolate the derived spectrum component.

P5-T05 implements this bounded contract with immutable
`BurnupCoefficientValuesV1`, `BurnupCoefficientRowV1`,
`BurnupCoefficientTableV1`, and lookup-result records. Table identity, schema
and data versions, material/unit identity, path-free provenance, and an opaque
loader-supplied checksum are retained with the lookup. Rows must be supplied
in strict increasing burnup order; finite coefficient relationships are
validated before storage and again after interpolation. Exact knots select a
single row, interior values use the adjacent bracket, and the derived spectrum
component is never independently interpolated. Missing metadata, invalid
coefficients, reordered/duplicate knots, private file-path provenance, and
out-of-range burnup fail closed. Canonical table-byte serialization and
checksum verification remain a data-pack boundary rather than an invented
runtime serializer in this slice.

### 6.4 Affected coefficient and spatial-state recomputation

P5-T06 adds the narrow boundary between the burnup table and the existing
two-group spatial solver. A cadence stores an explicit nonnegative epoch, a
strictly positive period, and an integer event index. The next event is formed
from `epoch + event_index * period` using the stored binary64 values. A result
that is not finite, representable, or strictly later when the index advances
fails closed; wall-clock time and Unity frame timing do not participate.

At the scheduled time, every live spatial node resolves the current bundle's
derived burnup against the table for its material variant. The request also
supplies one explicit positive node volume per node. The lookup result records
the bundle, node, material, burnup, table identity, bracket, and interpolation
fraction that supplied the node's material coefficients. The recomputation
does not change the approved table rule: exact knots select one row, interior
values use the adjacent linear bracket, and out-of-range values are rejected.

The existing validated edge and boundary conductances are copied in canonical
topology order. They are not re-derived from a guessed geometry or silently
replaced while burnup changes the node coefficients. The existing P2-T02
solver and its caller-supplied inner and outer policies then calculate the
replacement spatial state and normalize it to the requested positive target
power. No hidden tolerance or convergence rule is introduced by P5-T06.

Lifecycle acceptance is the final operation. The request binds exact simulation
time, core/bundle/topology/data-pack versions, topology/data/state digests, and
explicit spatial-solve and power-snapshot identities. The conductance stencil
must come from the exact lifecycle topology instance, not merely a same-sized
topology. Only a converged solve with a valid next cadence event can produce a
replacement lifecycle; failed lookup, invalid state, missing conductance,
nonconvergence, stale identity, or cadence failure produces no accepted
replacement. The contract is intentionally not the full P2-T04 scheduler,
kinetics, I/Xe, or regulating-system transition, and it does not serialize or
verify data-pack checksum bytes.

### 6.5 Phase 5 identity, location, burnup, and energy invariants

P5-T07 adds a fail-closed invariant boundary over the bounded Phase 5
contracts. The expected live bundle count is an explicit caller input; it is
not inferred from a partial inventory or silently replaced with a production
default. The validator checks the exact count, globally unique stable bundle
identities, unique legal `(ChannelId, BundlePosition)` locations, finite
nonnegative initial burnup and cumulative fission energy, positive heavy-metal
mass, and the exact derived relation
`B = InitialBurnup + CumulativeFissionEnergy / m_HM`.

For a burnup interval, the validator binds canonical
`(ChannelId, BundlePosition, BundleId)` record order to the source and result
inventories and to the accepted power snapshot. It checks exact binary64
`Delta_E = P * Delta_t`, exact cumulative-energy addition, derived burnup
accounting, nonnegative monotonic burnup, explicit time/version binding, and
the no-representable-increase failure condition. No tolerance, clamp, hidden
step reduction, or unordered sum is introduced.

For a refuelling shift, the validator checks the explicit flow-to-direction
binding, ordered discharged boundary states, exact retained identity and basic
state preservation at the shifted locations, fresh zero-energy identities at
the insertion positions, and the partition of retained, inserted, and
discharged identities. This remains a bounded identity/location/energy check;
complete I/Xe, power-history, residence, lifecycle, and long deterministic
history envelopes remain later Phase 5 work and G5 evidence.

### 6.6 Deterministic multistep history evidence

P5-T08 exercises the approved immutable transitions as a bounded evidence
harness. Each synthetic history accepts one explicit left-endpoint burnup
interval, advances the synthetic core version once, then applies the
equal-time S4 or S8 refuelling batch and advances the version once more. The
interval is therefore split at the refuelling boundary: a fresh bundle gets no
energy from the preceding interval and can contribute only to the next one.

The evidence covers repeated S4 cycles in `EndAtoEndB` flow and repeated S8
cycles in `EndBtoEndA` flow. At every step it checks exact count, location,
energy, derived burnup, monotonicity, movement, insertion, discharge, and
identity-lifetime invariants. Replaying the same history with accepted-power
records supplied in reverse order produces the same complete trace, including
every canonical per-bundle burnup record, every ordered discharged state, and
the resulting live inventory fingerprint.

The trace fingerprint is test-only repeatability evidence. It is not a new
runtime digest algorithm or a production serialization format. Direct
Candu6.x2m admission was retired by R2D; the R4/R5 candidate pack and
representative candidate remain preserved as Candidate/Deferred. G4-R6 admits
a separate project-authored ReducedModel authority with six approved profiles,
and G5 is `PASS` for the bounded Phase 5/Core sequence and lifecycle scope.
Neither gate establishes direct production/full-core CANDU authority.

### 6.7 Phase 5/G5 status and validation snapshot

The final same-context G5 review returned `PASS` for the approved
`ReducedModel` / engine-neutral Core scope. The result is a bounded validation
and lifecycle approval, not a direct production or full-core external solver
baseline.

| Evidence | Result |
| --- | --- |
| P3-T04 serialization regression | 5/5 PASS |
| P5-T11/P5-T16 focused complete-state regression | 7/7 PASS |
| Phase 5 focused Core tests | 44/44 PASS |
| Full Core and Golden suites | Core 110/110; Golden 19/19 PASS |
| Complete solution regression | 129/129 PASS |
| G4-R6 approved consumer | 3/3 PASS |
| P5-T10 sequence consumer | 2/2 PASS |
| Profile digest audit and formatter | 6 profiles PASS; clean |

The review receipt is verified as `gpt-5.6-sol` with high effort in the reused
reviewer context. The approved G4-R6 artifact and manifest hashes are
unchanged. Direct production/external authority remains deferred, and the
Phase 6/G6 result is recorded separately below.

### 6.8 Current test results and evidence boundary

The following checks were recorded or rerun on 2026-08-24 with the
repository-pinned .NET SDK 10.0.302. They are implementation evidence, not a
claim of production or external CANDU validation.

| Check | Fresh result |
| --- | --- |
| Direct full headless solution regression | Core 158/158 PASS; Golden 19/19 PASS; zero failures and zero skips |
| Direct Phase 6 focused regression | Core 46/46 PASS; zero failures and zero skips |
| `TEST-INFRA-01` artifact-output wrapper recovery | Core 156/156 and Golden 19/19 PASS; zero failures and zero skips |

The direct run shows 177 passing headless tests in the normal repository
layout. `TEST-INFRA-01` closed the former artifact-output location defect for
the recovery baseline; the later direct count includes the separately
characterized Phase 6 integration and internal tests. All Phase 6 results
remain synthetic/test-only contract evidence. G6 is still a `CONDITIONAL PASS`:
no applicable RRS comparison package, production/full-core CANDU authority, or
unconditional external claim has been admitted.

## 7. Point kinetics and xenon history

### 7.1 Quasi-static amplitude

The spatial solve supplies a shape normalized to a reference power `P_ref`.
Point kinetics adds an amplitude `a`:

```text
phi_actual,g,i = a * phi_shape,g,i
P_i             = a * P_shape,i
P_total         = a * P_ref
```

The amplitude changes power magnitude while the spatial solve determines shape.
Every actual bundle power used for burnup must be bound to the amplitude,
spatial solve, state versions, node volume, and coefficient digest.

### 7.2 Delayed-neutron point kinetics

The planned delayed-neutron groups use prompt generation time `Lambda`, delayed
fractions `beta_j`, decay constants `lambda_j`, amplitude `a`, and precursor
states `C_j`:

```text
da/dt = ((rho - beta) / Lambda) * a
        + sum_j lambda_j * C_j

dC_j/dt = (beta_j / Lambda) * a - lambda_j * C_j

beta = sum_j beta_j
```

For v1, the numerical update is explicit left-endpoint Euler:

```text
a_next = a + Delta_t * da/dt
C_next,j = C_j + Delta_t * dC_j/dt
```

Negative or non-finite values fail closed. A future approved task may compare a
different integrator, but it cannot replace Euler silently.

### 7.3 I-135 and Xe-135

The project attaches isotope state to the persistent bundle identity. There is
no cross-bundle xenon transport or thermal-hydraulic mixing model in v1.

Using actual flux and fission-rate density:

```text
R_f = sum_g Sigma_f,g * phi_actual,g

dN_I/dt  = gamma_I * R_f - lambda_I * N_I

dN_Xe/dt = gamma_X * R_f + lambda_I * N_I
            - [lambda_X + sum_g sigma_Xe,g^a * phi_actual,g] * N_Xe
```

The neutron-absorption contribution is added to the spatial absorption:

```text
Sigma_Xe,g^a = sigma_Xe,g^a * N_Xe
Sigma_a,g,eff = Sigma_a,g,base + DeltaSigma_a,g + Sigma_Xe,g^a
```

The base table must be explicitly xenon-free so dynamic Xe is not counted
twice. I/Xe inventories are authoritative; number densities are derived from
the current node volume. The next spatial solve is mandatory after Xe changes.

## 8. Regulating system and optional feedback

### 8.1 Total-power and tilt control

The limited RRS uses total-power error and regional tilt error:

```text
PowerError = PowerSetpoint - MeasuredPower
P_region,r = sum_i_in_region_r P_i
f_region,r = P_region,r / P_total
TiltError_r = TargetFraction_r - f_region,r
```

The planned controller is PI-like, with optional regional terms:

```text
IntegralError_next = IntegralError + PowerError * Delta_t_RRS
TiltIntegral_next = TiltIntegral + TiltError * Delta_t_RRS

ActuatorCommand = Bias
                + K_P * PowerError
                + K_I * IntegralError_next
                + regional tilt terms
```

The controller does not directly add reactivity. It changes an actuator state,
which a validated influence map converts into local absorption overlays before
the next spatial solve.

### 8.2 Delays, rates, and event ordering

Requested command, available command, and physical actuator state are three
different states. Commands have explicit delays, due times, bounds, deterministic
IDs, and queue ownership. Physical motion is rate-limited and causal: a newly
available command cannot retroactively affect the interval before its due time.

At an event time, the planned order is broadly:

```text
burnup -> refuelling -> command generation -> actuator motion
        -> branch updates -> spatial solve -> kinetics/Xe step
```

Every state transition is explicit, ordered, versioned, and rollback-safe.

### 8.3 Influence maps

An influence map converts a control state into local absorption change:

```text
DeltaSigma_a,g,i = sum_q Weight_(q,i,g)
                   * (ActuatorState_q - ReferenceState_q)
```

The map is sparse, versioned, and bound to an explicit node and group. It is
not also added as a separate direct reactivity term. A sign certificate checks
that an automatic controller acts as negative feedback.

### 8.4 Included branch schemas and deferred physics

The Phase 6 Core implementation contains synthetic/test-only contracts for:

- 14 logical liquid-zone regions grouped into 6 physical assemblies;
- adjuster banks with explicit insertion fractions and rate limits;
- bulk poison mass, concentration, and add/withdraw rates;
- controller projections, command queues, delays, saturation, and deterministic
  scenario evidence.

The four items above are bounded P6-T01 through P6-T07 contract evidence and
are conditionally passed by G6. They are not a validated production controller,
external-reference comparison, or golden RRS authority. The remaining branch
schemas are still deferred:

- temperature states for fuel, coolant, or moderator; and
- moderator purity mass fraction.

Disabled temperature, purity, zone, adjuster, and poison branches contribute
exactly zero. V1 has no thermal equation, CFD, chemistry law, coolant-flow
model, or hidden cleanup law. Temperature and purity are prescribed branch
states until a later task authorizes a dynamic model.

## 9. Numerical method summary

| Problem | Planned method | Why it is useful for the game |
| --- | --- | --- |
| Spatial neutronics | Two-group finite-volume operator plus sequential source/power iteration | Produces a spatial flux and power shape from explicit topology |
| Inner group solve | Backend-neutral deterministic linear solve with scalar residual acceptance | Allows a validated implementation without hiding convergence failure |
| Power scaling | Total fission-power normalization to caller target | Separates shape/criticality from the scenario's power scale |
| Burnup | Left-endpoint piecewise-constant power integration | Makes power history directly change future fuel state |
| Coefficient lookup | Strict-knot linear interpolation, no extrapolation | Makes burnup-dependent physics auditable and bounded |
| Point kinetics | Explicit Euler amplitude and delayed precursors | Adds time-dependent power response without full transient CFD |
| I/Xe | Explicit Euler production, decay, and absorption inventories | Makes recent power history affect future reactivity |
| RRS motion | Delayed commands plus rate-limited state updates | Turns control actions into understandable time-lagged choices |
| Feedback | Sparse local absorption overlays | Changes spatial shape and regional tilt rather than only total power |
| Determinism | Canonical ordering, finite `double`, explicit time, no unordered reductions | Makes saves, replays, tests, and player outcomes repeatable |

The most important numerical design principle is **fail closed**. A failed
inner solve, nonconverged outer solve, invalid coefficient, negative inventory,
stale snapshot, or inconsistent event must produce a diagnostic and no usable
partial state. The game can turn a failed scenario into a clear error or loss;
it must not silently continue with invented physics.

For explicit Euler, the caller supplies a stability policy. The permitted
substep is bounded by kinetic, nuclide, and actuator limits plus declared rate
envelopes. If a scheduled gap is too long, it is subdivided deterministically;
the simulator does not hide instability by choosing an undocumented step.

## 10. How this physics feeds the game

### 10.1 The player-facing loop

The planned game loop is:

1. **Inspect:** see channel and bundle power, burnup, residence history,
   regional tilt, xenon state, and available control actions.
2. **Choose:** refuel a legal channel, wait, change a regulating action, add or
   withdraw poison, or preserve margin for a later event.
3. **Advance:** the explicit scheduler processes burnup, transitions, delayed
   commands, spatial solves, kinetics, and isotope history.
4. **Explain:** report what changed and why, such as a local absorption change,
   a power-shape shift, a burnup increment, or a xenon penalty.
5. **Evaluate:** score survival, energy production, refuelling efficiency,
   stability, and avoidable control use.

### 10.2 Why each physics layer matters

| Physics layer | Player consequence |
| --- | --- |
| Spatial flux and power shape | A channel can be locally hot or cold even when total power looks acceptable |
| `k` and reactivity | The same refuelling action can be stabilizing in one state and destabilizing in another |
| Burnup and coefficient interpolation | Old and fresh bundles are not interchangeable; history matters |
| Refuelling direction and atomicity | A legal move changes a whole train and has a visible discharge consequence |
| RRS and tilt | The player must balance total power against regional shape |
| Kinetics | Power does not jump arbitrarily; delayed neutrons create memory |
| Xenon | Recent high power can create a later absorption penalty and change future choices |
| Deterministic events and replay | The player can learn from consequences and reproduce a strategy |

The game is successful when these layers make decisions meaningful without
forcing the player to read raw solver internals. The user interface should show
cause and effect in terms such as ``power moved toward the north region`` or
``xenon absorption increased after sustained power`` while retaining detailed
diagnostics for developers.

## 11. What remains to be investigated

The following are not yet settled runtime facts:

- exact DRAGON5/DONJON5 build identity, nuclear-data checksums, coefficient
  generation, and lawful reference-data admission;
- the performance and evidence limits of the current scalar Jacobi inner solve;
- the production coefficient data pack, leakage conductances, and boundary
  conductances;
- direct production/full-core reference authority and production thresholds;
- an applicable RRS comparison package, evidence-backed G6 tolerances, and an
  unconditional G6 disposition;
- evidence-backed G7A and optional G7B tolerances and golden cases;
- the small synthetic topology and data fixtures used before full-core data;
- the controller influence-map values, sign certificate, and regional map;
- stable timestep policies for representative rate/decay envelopes;
- whether a later kinetics integrator is worth investigating against the v1
  explicit-Euler baseline; and
- whether temperature and moderator-purity feedback improves the game enough
  to justify Phase 7B.

The literature digest is useful for methodology and candidate case design. It
does not fill these gaps. In particular, values such as a reported burnup,
shift size, group library, or controller arrangement remain candidate evidence
until the complete case-admission proof is available.

## 12. Explicitly out of scope

This project does not implement or investigate as runtime behavior:

- shutdown, scram, trip, emergency, or safety-system response;
- accident progression or operator-training behavior;
- full thermal hydraulics, CFD, coolant chemistry, or detailed temperature
  evolution;
- OpenMC code, OpenMC-generated production data, or an OpenMC runtime;
- a named commercial station map or proprietary operating history;
- thorium, SCWR, accident-tolerant-cladding, or advanced-cycle runtime models;
- hidden random behavior, Unity-frame-driven physics, or nondeterministic
  parallel reductions.

## 13. Living-document maintenance and consistency checks

This guide is a derived teaching document. Repository rules govern execution;
the implementation plan governs product/phase intent; the project scope
register governs current status; specifications and ADRs govern technical
decisions; code and task/gate reports provide implementation evidence. The
P1-T08 literature digest supplies context and candidate-case evidence only.

Update the guide when any of the following changes:

- a physics specification, unit, sign, equation, state field, or numerical
  method changes;
- a runtime physics class or data-pack loader is implemented;
- a gate changes the status of implementation, tolerance, or golden evidence;
- the literature digest adds or reclassifies a relevant claim; or
- the game loop, scoring, player decision, or out-of-scope boundary changes.

Run the consistency check from the repository root:

```powershell
& .\tools\Check-PhysicsGuideConsistency.ps1
```

Regenerate the PDF from this Markdown source:

```powershell
python .\tools\build_physics_guide_pdf.py
```

The check verifies required headings, local links, current Phase 3/4/5/6, G4,
G5, and G6 evidence, specification status markers, PDF freshness, and a
readable PDF page count. It
is intended to fail when the documented status and Core evidence diverge. PDF
pages should still be visually inspected after major layout changes.

## 14. Traceability and references

### Project documents

- [Project scope and delivery register](../PROJECT_SCOPE.md)
- [Implementation plan](../Implementation_plan.md)
- [P2-T01 topology and units](../spec/topology-indexing-units-boundaries-v1.md)
- [P2-T02 two-group solver](../spec/two-group-solver-normalization-convergence-v1.md)
- [P2-T03 refuelling and burnup](../spec/refuelling-burnup-transitions-v1.md)
- [P2-T04 kinetics, xenon, RRS, and feedback](../spec/kinetics-xenon-rrs-feedback-v1.md)
- [P2-T05 observables and validation](../spec/observables-validation-methodology-v1.md)
- [P1-T08 task report](../tasks/P1-T08.md)
- [CANDU literature digest v1](../reference/candu-literature-digest-v1.md)
- [G2 forced-closure addendum](../gates/G2-FC-01.md)
- [G3 state-contract review](../gates/G3.md)
- [P4-T01 stencil assembly](../tasks/P4-T01.md)
- [P4-T02 operator binding](../tasks/P4-T02.md)
- [P4-T03 source/eigen iteration](../tasks/P4-T03.md)
- [P4-T06-R2D reduced/synthetic admission disposition](../tasks/P4-T06-R2D.md)
- [P4-T06-R3 reduced-model/interpolation boundary](../tasks/P4-T06-R3.md)
- [P4-T06-R4 candidate interpolation pack](../tasks/P4-T06-R4.md)
- [P4-T06-R5 candidate comparison snapshots](../tasks/P4-T06-R5.md)
- [P4-T06-G4G bounded mathematical admission](../tasks/P4-T06-G4G.md)
- [G4-R3 blocked fresh gate](../gates/G4-R3.md)
- [P4-T06-G4K independent reduced-model authority](../tasks/P4-T06-G4K.md)
- [G4-R6 reduced-model G4 disposition](../gates/G4-R6.md)
- [Reduced-model/interpolation boundary specification](../spec/reduced-model-interpolation-boundary-v1.md)
- [P5-T06 affected coefficient and spatial-state recomputation](../tasks/P5-T06.md)
- [P5-T07 Phase 5 identity, location, burnup, and energy invariants](../tasks/P5-T07.md)
- [P5-T08 deterministic multistep refuelling histories](../tasks/P5-T08.md)
- [P5-T09 complete lifecycle and I/Xe contracts](../tasks/P5-T09.md)
- [P5-T10 approved ReducedModel sequence comparison](../tasks/P5-T10.md)
- [P5-T11 through P5-T16 Phase 5 correction chain](../tasks/P5-T16.md)
- [G5 Phase 5 refuelling and depletion review](../gates/G5.md)
- [P6-T07 bounded RRS scenario evidence](../tasks/P6-T07.md)
- [G6 Phase 6 regulating-system review](../gates/G6.md)

### Literature evidence boundary

The applicable P1-T08 rows for this guide are S1-R02, S1-R03, S1-R05,
S1-R06, S1-R07, S1-R08, S1-R09, S1-R10, S5-R03, S5-R04, S5-R08, S6-R02,
S6-R03, and S6-R05. They are used as `MethodologySupport` or
`CandidateCaseDesign` context only. No row is used as an approved runtime
constant or golden value.

The guide should be updated before a later task uses a literature row as
candidate numeric evidence. That task must record the full case-admission
proof: exact tool/build, nuclear-data identity, geometry, state/history, units,
normalization, output definition, independent reproduction, and licensing.

## 15. Glossary

| Term | First-year meaning |
| --- | --- |
| Absorption cross section | A measure of how likely a neutron is removed by absorption in a material |
| Burnup | Energy extracted per unit heavy-metal mass; here stored in `J/kg_HM` |
| Diffusion | A reduced model that estimates neutron movement from flux gradients |
| Eigenvalue | A value such as `k` that makes a balance equation self-consistent |
| Fission source | New neutrons produced by fission |
| Flux | Neutron flow intensity used to calculate reaction rates |
| Homogenization | Replacing detailed material regions by averaged group constants |
| Macroscopic cross section | A bulk interaction coefficient with units of inverse length |
| Precursor | A delayed-neutron-emitting fission product represented by a state variable |
| Reactivity | A signed measure derived from `k` that indicates a tendency to change power |
| Source iteration | Repeatedly solve a spatial balance using the latest fission source |
| Xenon poisoning | Absorption by Xe-135, whose concentration depends on recent power history |

## Change record

| Version | Date | Change |
| --- | --- | --- |
| 2.8 | 2026-08-24 | Refreshed the living handoff with direct Core 158/158 and Golden 19/19, Phase 6 focused 46/46, the TEST-INFRA-01 wrapper recovery, the completed Phase 6 refactor/naming chain, and the unchanged synthetic-only G6 limitation |
| 2.7 | 2026-08-21 | Added the G6 conditional synthetic/test-only disposition and recorded the then-current direct and artifact-wrapper validation boundary; kept external RRS authority deferred |
| 2.6 | 2026-08-18 | Added the ReducedModel channel-plane geometry visualization; preserved authority boundaries |
| 2.5 | 2026-08-18 | Added model cards, evidence mapping, and visualizations |
| 2.4 | 2026-08-18 | Recorded G4-R6 approval, the final G5 PASS for the bounded ReducedModel/Core scope, P5-T10 through P5-T16 evidence, and Phase 6/G6 activation; kept direct production/full-core authority deferred |
| 2.3 | 2026-08-16 | Recorded the G4-R3 BLOCKED disposition, the P4-T06-G4H next handoff, and the P5-T10/G5 dependency; kept production tolerances and golden status deferred |
| 2.2 | 2026-08-16 | Recorded the R2D-selected reduced/synthetic offline boundary, existing-table interpolation surface, direct Candu6.x2m admission retirement, and completed P5-T09 lifecycle/I-Xe contract status; kept R4/R5, G4/G5, reference comparisons, and golden consumers pending |
| 2.1 | 2026-08-15 | Recorded P5-T08 test-only deterministic multistep S4/S8 history evidence, explicit interval/refuelling ordering, shuffled-input replay equality, complete per-bundle/discharge trace coverage, and identity non-reuse; kept production history schema, reference comparisons, golden consumers, and G4/G5 approval pending |
| 2.0 | 2026-08-15 | Recorded P5-T07 explicit bundle-count, identity, location, nonnegative-burnup, exact energy-accounting, interval-energy, monotonicity, and refuelling partition invariants; kept complete lifecycle/I-Xe/power-history envelopes, deterministic long histories, reference comparisons, golden consumers, and G4/G5 approval pending |
| 1.9 | 2026-08-15 | Recorded P5-T06 explicit epoch/period/index cadence, burnup-selected node coefficient rebinding, preserved validated conductances, deterministic spatial solve, exact topology-instance binding, and converged atomic lifecycle acceptance; kept full scheduler/kinetics/I-Xe transitions, canonical checksum serialization, complete power-history binding, reference comparisons, golden consumers, and G4 approval pending |
| 1.8 | 2026-08-15 | Recorded P5-T05 immutable burnup-indexed coefficient values/table metadata, strict-knot validation, exact/linear lookup, derived `chi_2`, and fail-closed out-of-range behavior; kept canonical data-pack checksum verification, solver recomputation, complete lifecycle/history binding, reference comparisons, golden consumers, and G4 approval pending |
| 1.7 | 2026-08-15 | Recorded P5-T04 accepted bundle-power binding, explicit left-endpoint energy integration, derived burnup, immutable replacement, monotonicity/overflow validation, and proposed version advancement; kept full power-history/digest lifecycle binding, coefficient lookup, reference comparisons, golden consumers, and G4 approval pending |
| 1.6 | 2026-08-15 | Recorded P5-T03 named refuelling mapping/atomicity bodies, canonical position bindings, immutable paired event append, and bounded basic discharge audit records; kept complete I/Xe-bearing discharge/transaction envelopes, burnup, coefficient lookup, reference comparisons, golden consumers, and G4 approval pending |
| 1.5 | 2026-08-15 | Recorded P5-T02 immutable atomic directional movement over the current BundleInventory fields; kept complete lifecycle/I/Xe/event envelopes, burnup, coefficient lookup, reference comparisons, golden consumers, and G4 approval pending |
| 1.4 | 2026-08-15 | Recorded P5-T01 declarative S4/S8 scheme metadata and canonical flow-bound position-plan validation; kept atomic movement, burnup, reference comparisons, golden consumers, and G4 approval pending |
| 1.3 | 2026-08-15 | Recorded P4-T05 synthetic solved-case coverage; kept traceable reference comparisons, golden consumers, and G4 approval pending |
| 1.2 | 2026-08-15 | Recorded P4-T04 outer convergence and fail-closed diagnostics as implemented; kept synthetic coverage, reference comparisons, golden consumers, and G4 approval pending |
| 1.1 | 2026-08-15 | Corrected implemented Phase 3/4 status; separated the one-iteration solver foundation from pending outer convergence, comparison, golden-data, and G4 work; aligned maintenance and authority references |
| 1.0 | 2026-08-10 | Initial extraction of implemented status, P2 physics contracts, planned numerical methods, game coupling, and maintenance checks |
