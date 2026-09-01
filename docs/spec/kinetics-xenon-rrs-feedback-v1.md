# Kinetics, xenon, RRS, and feedback data v1

## Status and scope

This document is the engine-neutral P2-T04 mathematical and data contract for
power amplitude, delayed-neutron kinetics, I-135/Xe-135 history, regulating
system state, and temperature/purity/device influence maps. It defines signs,
units, initial conditions, update cadence, coupling to the approved P2-T02
spatial solve and P2-T03 burnup/refuelling state, diagnostics, and invalid-state
behavior.

V1 does not add shutdown, scram, trip, accident progression, safety-system,
thermal-hydraulic, CFD, operator-training, or Unity behavior. It does not copy
DRAGON5/DONJON5 source or claim that a reference listing supplies kinetics or
feedback constants. Temperature and purity have a validated branch schema even
when their feedback contribution is disabled. Dynamic thermal/purity evolution
is explicitly deferred.

All examples and numeric values in this document are synthetic contract
examples. They are not copied from a private listing, DRAGON5, DONJON5, or a
golden dataset.

## Normative terms and unit policy

The words **must**, **must not**, and **may** are normative. Authoritative
arithmetic uses finite IEEE-754 `double` values and explicit SI units. A missing,
non-finite, incompatible, or ambiguous unit fails closed. No production default
parameter, convergence tolerance, or nuclear-data value is selected here.

| Quantity | Meaning | Unit |
| --- | --- | --- |
| `t` | explicit simulation time | `s` |
| `Delta_t` | positive integration interval | `s` |
| `a` | dimensionless power amplitude relative to `P_ref` | dimensionless (`1`) |
| `P_ref` | fixed power represented by a P2-T02 shape solve | `W` |
| `P_total` | actual total fission power | `W` |
| `P_i` | actual local node/bundle power | `W` |
| `phi_g` | neutron flux in group `g` | `m^-2 s^-1` |
| `R_f` | local fission rate density | `m^-3 s^-1` |
| `rho` | reactivity | dimensionless (`1`) |
| `Lambda` | prompt generation time | `s` |
| `beta_j` | delayed-neutron fraction of precursor group `j` | dimensionless (`1`) |
| `lambda_j` | precursor decay constant | `s^-1` |
| `C_j` | normalized precursor concentration | dimensionless (`1`) |
| `A_I`, `A_Xe` | authoritative I-135/Xe-135 atom inventory | atoms |
| `N_I`, `N_Xe` | derived I-135/Xe-135 number density | `m^-3` |
| `gamma_I`, `gamma_X` | atoms produced per fission | dimensionless (`1`) |
| `sigma_Xe,g^a` | microscopic Xe absorption cross section | `m^2` |
| `Sigma_Xe,g^a` | macroscopic Xe absorption contribution | `m^-1` |
| `T` | temperature branch state | `K` |
| `w_p` | moderator purity mass fraction | dimensionless (`kg/kg`) |
| `c_poison` | bulk-poison mass concentration | `kg/m^3` |
| `e_P` | RRS power error | `W` |
| `I_P` | RRS integrated power error | `W s` |
| `u_cmd,q` | requested actuator command | dimensionless (`1`) |
| `u_state,q` | delayed/rate-limited physical actuator state | dimensionless (`1`) |
| `K_P` | proportional RRS gain | `W^-1` |
| `K_I` | integral RRS gain | `(W s)^-1` |
| `M` | local absorption influence-map coefficient | `m^-1` per source unit |
| `DeltaSigma_a` | active local absorption overlay | `m^-1` |

Display units such as percent power, pcm, degrees Celsius, or ppm must be
converted at the boundary. `rho` is authoritative dimensionless reactivity;
`pcm` is display-only and no conversion may be applied twice.

## State ownership and coupling boundary

### Kinetic state

The v1 global `KineticState` contains:

| Field | Contract | Unit |
| --- | --- | --- |
| `SimulationTime` | exact current simulation time | `s` |
| `Amplitude` | nonnegative power amplitude `a` | `1` |
| `Precursor[j]` | one normalized delayed-neutron state per configured group | `1` |
| `InitialAmplitude` | explicit scenario initial value | `1` |
| `InitialPrecursor[j]` | explicit scenario initial values | `1` |
| `P_ref` | fixed positive spatial-shape power reference | `W` |
| `SpatialSolveId` | exact P2-T02 result used for current coupling | opaque |
| `SpatialStateVersion` | accepted P2-T02 solve epoch from `VersionLifecycleV1`; `NotApplicable` when no valid solve is bound | integer |
| `SpatialReactivity` | authoritative `rho_spatial = rho_total` from effective `k` | `1` |
| `FeedbackOverlayDigest` | local absorption contributions bound to the solve | opaque |
| `KineticStepIndex` | deterministic integration index | integer |

The point-kinetics amplitude owns only power magnitude and precursor state. The
P2-T02 solver owns flux shape, coefficients, leakage, normalization, `k`, and
spatial diagnostics. P2-T03 owns bundle identity, burnup, refuelling events,
and cumulative energy. No system may duplicate another system's authoritative
state.

### Amplitude coupling to the spatial solve

At a valid P2-T02 solve, the shape is normalized to the configured `P_ref`.
Let `phi_shape,g,i` and `P_shape,i` be its flux and local powers. The actual
state at amplitude `a` is:

```text
phi_actual,g,i = a * phi_shape,g,i
P_i             = a * P_shape,i
P_total         = a * P_ref
R_f,i           = sum_g Sigma_f,g,i * phi_actual,g,i
```

`a=1` means the actual total power equals `P_ref`; it does not force the
eigenvalue `k` to one. A non-finite or negative amplitude, shape, or power
fails closed. The amplitude is not power-normalized again by the kinetics
integrator.

The P2-T03 `PowerSnapshot` and every bundle `PowerHistory` record consume the
actual `P_i` above, never `P_shape,i`. Each record additionally binds
`Amplitude`, `KineticStepIndex`, `P_ref`, `SpatialSolveId`,
`SpatialStateVersion`, `PowerSnapshotVersion`, `NodeVolume`,
`CoreStateVersion`, the applicable per-bundle `NuclideStateVersion`, and the
kinetic/nuclide/coefficient digests. Any amplitude change creates a new
exact-time snapshot boundary and splits the next burnup interval; P2-T03 may
not integrate a shape-only or stale-amplitude power.

### Reactivity ownership and sign

For every valid spatial solve with `k > 0`, define:

```text
rho_spatial = (k - 1) / k
rho_total   = rho_spatial
```

Positive `rho_total` increases the prompt amplitude derivative; negative
`rho_total` decreases it. `rho_spatial` is computed after the P2-T02 solve on
the effective coefficient set. Xe and every enabled device/branch map affect
that coefficient set; there is no separate `rho_xenon`, `rho_RRS`, or generic
branch reactivity term in v1. This is the single ownership rule that prevents
the same effect from being counted in both `k` and kinetics.

V1 influence maps target local non-fission absorption overlays. They carry
`DeltaSigma_a,g,i` in `m^-1` and are applied before P2-T02. A map never becomes
both a coefficient overlay and a direct reactivity term. A future approved
specification may add another target mode, but v1 rejects it.

The effective coefficient contract is:

```text
DeltaSigma_a,g,i = sum over active source maps of map_weight * source_delta
Sigma_a,g,eff,i = Sigma_a,g,base,i + DeltaSigma_a,g,i
                 + Sigma_Xe,g^a,i
```

The effective set must pass every P2-T02 invariant, including
`Sigma_a,eff >= Sigma_f`. Because the overlay is node/group specific, it can
change relative flux shape and regional power rather than only scaling a
uniform amplitude.

## Delayed-neutron point kinetics

### Coefficient contract

The delayed-neutron data pack contains `G >= 1` groups with finite values:

```text
0 < Lambda
0 <= beta_j < 1
lambda_j > 0
beta = sum_j beta_j < 1
```

`Lambda`, `beta_j`, and `lambda_j` are supplied data, not hidden constants.
Group order is the serialized index order `j=0..G-1`; every scalar sum uses
that order. A missing, non-finite, negative, duplicate, or dimensionally
invalid group record fails before integration.

`Amplitude` and `Precursor[j]` must be finite and nonnegative. V1 has no
external neutron source term and no implicit criticality or equilibrium
initialization.

### Continuous equations and signs

For a frozen reactivity and state over an integration interval, the v1 equations
are:

```text
da/dt   = ((rho_total - beta) / Lambda) * a
          + sum_j lambda_j * C_j

dC_j/dt = (beta_j / Lambda) * a - lambda_j * C_j
```

The first term is prompt production/removal. The delayed precursor decay term
is a positive source to `a`, while precursor production is positive and decay is
negative in `C_j`. These signs are normative.

### Deterministic discrete update

V1 uses an explicit left-endpoint Euler update with the current finite state and
current `rho_total`:

```text
a_next   = a + Delta_t * [((rho_total - beta) / Lambda) * a
                           + sum_j lambda_j * C_j]
C_next,j = C_j + Delta_t * [(beta_j / Lambda) * a - lambda_j * C_j]
```

The result is accepted only when every value is finite and nonnegative. A
negative result is not clamped, reflected, or silently reduced by an automatic
step-size choice; the step fails closed and records the first offending field.
The caller supplies a positive `Delta_t` policy. A future approved integrator
may replace Euler only under a separate equivalence specification.

The update records the old/new amplitude, every old/new precursor, reactivity
breakdown, `Delta_t`, spatial solve identity, and step index. Identical input
bytes and ordering produce identical output bytes.

## I-135 and Xe-135 state

### Ownership and initial conditions

V1 attaches `NuclideState` to the persistent bundle identity, so its I/Xe
history moves with a bundle and is retained on discharge. This is an explicit
gameplay simplification: V1 has no cross-bundle xenon transport or thermal-
hydraulic mixing model.

Each live bundle has:

| Field | Contract | Unit |
| --- | --- | --- |
| `I135AtomInventory` | authoritative finite nonnegative I-135 amount | atoms |
| `Xe135AtomInventory` | authoritative finite nonnegative Xe-135 amount | atoms |
| `NodeVolume` | P2-T02 control volume for the current slot | `m^3` |
| `I135NumberDensity` | derived `I135AtomInventory / NodeVolume` | `m^-3` |
| `Xe135NumberDensity` | derived `Xe135AtomInventory / NodeVolume` | `m^-3` |
| `NuclideStateVersion` | per-bundle accepted I/Xe commit counter from `VersionLifecycleV1` | integer |
| `InitialI135` / `InitialXe135` | explicit initial atom inventories for audit | atoms |
| `NuclideDataId` / `Digest` | yields, decay, and cross-section identity | opaque |
| `I135XeHistory` | append-only accepted I/Xe transition records, including production, decay, absorption, before/after inventories, volume, and owner-scoped version binding | structured |
| `NuclideHistoryDigest` | canonical digest of the complete `I135XeHistory` for this bundle | bytes32 |

Initial values at scenario start and for every fresh-fuel template are explicit
atom inventories and an explicit `NuclideStateVersion`. An input number density
must be converted once using the validated starting `NodeVolume`; all-zero
initial state is legal only when supplied explicitly. The implementation must
not infer equilibrium concentrations, copy values from a reference listing, or
reset a moved bundle's history. A discharged record keeps the final atom
inventories, derived densities, and `NuclideStateVersion`. When a bundle moves
to a node with a different `NodeVolume`, its atom inventories and nuclide
version move unchanged and its number densities are recomputed from the new
volume.

### NuclideStateVersion lifecycle and cross-spec binding

`NuclideStateVersion` is owned by the persistent bundle identity. It starts at
the explicit per-bundle scenario value or the resolved fresh-fuel template
value and increments exactly once when that bundle's accepted I-135/Xe-135
integration transaction commits. A failed, stale, non-finite, or rejected
integration increments no bundle version and changes no inventory. A refuelling
move retains the counter with the bundle; a discharge record retains its final
counter; a newly inserted bundle receives its explicit template counter. A
bundle's version does not increment merely because a spatial solve is accepted,
because a regulating action is queued, or because the bundle changes position.

An accepted nuclide integration changes the absorption input and therefore
invalidates the current spatial and power bindings under the shared
`VersionLifecycleV1` contract. It does not silently increment
`CoreStateVersion`; a separate committed P2-T03 burnup or refuelling transaction
owns that counter. The next accepted P2-T02 solve must bind the post-integration
per-bundle versions, and its atomic commit advances both
`SpatialStateVersion` and `PowerSnapshotVersion`.

Every runtime event and every accepted power record carries the applicable
owner-scoped version tuple: global `CoreStateVersion`, accepted spatial and
power versions when a valid solve/snapshot exists, and the bundle's
`NuclideStateVersion` for bundle-owned I/Xe values. If a spatial or power
binding is invalid, its version, ID, and digest are serialized as the explicit
P2-T05 `NotApplicable` tag rather than as zero or the last usable value.
Missing, duplicate, reordered, stale, or inconsistent version fields fail
closed during save/load, replay, and event validation. Transaction scratch
state includes all version counters, validity statuses, IDs, and digests, so a
failed stage restores them byte-for-byte with the inventories and histories.

### Refuelling atomicity binding

P2-T03 refuelling is an owner-preserving transaction over the complete
P2-T04 `NuclideState`, not merely over bundle location and burnup. Its
authoritative bundle envelope is the tuple
`(I135AtomInventory, Xe135AtomInventory, InitialI135, InitialXe135,
NodeVolume, I135NumberDensity, Xe135NumberDensity, NuclideStateVersion,
NuclideDataId, NuclideDataDigest, I135XeHistory, NuclideHistoryDigest)`.
Inventories and initial values are finite nonnegative atom counts; volume is
finite and strictly positive; densities are derived from the current volume and
must match exactly. A density is never used to repair an inventory. The
history is append-only, canonical, and bound to the same bundle, data identity,
and version tuple.

The refuelling transaction follows these cross-spec rules:

- A moved bundle carries its atom inventories, initial inventories, nuclide
  version, data identity/digest, and complete I/Xe history unchanged. Only the
  destination volume changes, so densities are recomputed from the unchanged
  atoms. Refuelling does not increment a moved bundle's `NuclideStateVersion`.
- A fresh inserted bundle receives explicit template initial I/Xe inventories,
  with `I135AtomInventory == InitialI135` and
  `Xe135AtomInventory == InitialXe135`, template `NuclideStateVersion`,
  `NuclideDataId`/`Digest`, an empty `I135XeHistory`, and its defined empty
  `NuclideHistoryDigest`. No equilibrium, inferred value, or implicit zero is
  permitted, and the zero-duration refuelling instant appends no I/Xe
  integration record.
- A discharged record retains final atoms, initial atoms, discharge volume and
  derived densities, version, data identity/digest, complete history, and
  history digest. Its binding is immutable and remains associated with the
  persistent bundle identity.
- P2-T03 preconditions reject missing, duplicate, stale, reordered, non-finite,
  volume-incompatible, or digest-incompatible I/Xe state before mutation. Its
  proposed-state checks require exact-once insertion/movement/discharge and
  exact state/data agreement. Equal-time multi-channel batches validate these
  conditions across the whole scratch state.
- The existing P2-T05 `RefuelMappingBodyV1` and `RefuelAtomicityBodyV1` remain
  the event envelopes exactly as specified; the mapping body has no I/Xe field.
  Their transaction digests are computed over the complete I/Xe-bearing
  `RefuelTransactionV1` state projection, including all fields above; a digest
  over a location/burnup-only projection is invalid. The body fields
  `BeforeDigest`, `ProposedDigest`, and `UnchangedDigestOrNA` therefore bind
  the complete envelope. Invalid spatial or power bindings use P2-T05
  `NotApplicable`, never zero or a stale usable version.
- A failed lookup, postcondition, event-body check, or equal-time command
  rejects with `committed=false` and restores all I/Xe fields, histories,
  versions, data digests, lifecycle state, event/command markers, discharge
  records, and the other authoritative state byte-for-byte.

The shared canonical byte contract is defined once by P2-T03 and required by
P2-T04. `I135XeHistory` is a `UInt32`-counted array of
`NuclideTransitionRecordV1` values sorted by
`(EventTime, EventRank, RecordSequence, RecordIdBytes)`. Each record includes
its event identity, exact time and duration, bundle identity, before/after
`CoreStateVersion` and `NuclideStateVersion`, node volume, before/after I/Xe
inventories and derived densities, all production/decay/absorption terms,
`NuclideDataId`/`NuclideDataDigest`, and its exact state-binding digest. The
record domain separator is `CANDU-NUCLIDE-TRANSITION-V1`; the history domain
separator is `CANDU-NUCLIDE-HISTORY-V1`, with schema version, owning bundle,
count, and complete ordered record bytes. `NuclideHistoryDigest` excludes
itself. `RecordId` is UUIDv8 from the first 16 bytes of SHA-256 over
`CANDU-NUCLIDE-TRANSITION-ID-V1`, the P2-T05 zero separator, `OwnerEventId`,
`BundleId`, and `RecordSequence`; `RecordDigest` is the final non-self-
included SHA-256 field. `StateBindingDigest` is `Bytes32` for an accepted
integration record and explicit `NotApplicable` only in a non-runtime
diagnostic. The signed transition terms are separate direct I production, I
decay loss, direct Xe production, Xe source from I decay, Xe radioactive-decay
loss, and Xe absorption-loss fields. `NuclideStateEnvelopeV1` uses the fixed order
`BundleId`, current and initial inventories, `NodeVolume`, derived densities,
`NuclideStateVersion`, data identity/digest, counted history, and history
digest, with domain separator `CANDU-NUCLIDE-STATE-V1`; its state digest also
excludes itself. All values use P2-T05 fixed-width encodings and no unordered
collection or wildcard field is legal.

The shared discharge payload is named `DischargeRecordV1` and retains the
complete final `NuclideStateEnvelopeV1` alongside the P2-T03 identity, burnup,
energy, coefficient, power-history, command, explicit `DischargeEnd` enum, and
discharge-time fields. The shared batch payload is `RefuelBatchV1`: it uses
`CANDU-REFUEL-BATCH-V1` and its UUIDv8 `BatchId` uses
`CANDU-REFUEL-BATCH-ID-V1`; it has a `UInt32`-counted complete sorted
`RefuelShiftCommandV1` array ordered by `(Sequence, CommandIdBytes)`. The
shared full transaction projection is named `RefuelTransactionV1` and uses the
domain separator `CANDU-REFUEL-TRANSACTION-V1`; its before/proposed/unchanged
digests exclude themselves and cover the complete ordered batch/state
projection, not a singular-command or location-only subset. A preflight
rejection uses the defined `RejectedProposedStateV1` projection with the full
P2-T05 `CommitStatus=(Committed=0, RolledBack=1, Rejected=2,
NotApplicable=255)` encoding. Preflight failure uses `Rejected=2`; a failure
after a prepared proposed state exists uses `RolledBack=1`; both preserve the
required deterministic `ProposedDigest` and unchanged before digest.

For an aggregate equal-time refuelling event with no single owning bundle, the
outer P2-T05 `StateBinding` sets `NuclideStateVersion=NotApplicable` and binds
the canonical ordered bundle-envelope list through `SnapshotDigest`. A
bundle-scoped I/Xe or discharge record carries its exact bundle counter. Each
command in a batch has one mapping/atomicity pair, linked by the outer P2-T05
`EventV1.EventId` equal to the atomic body's `CommandId`; pairs share the
full-batch before/proposed digest and are sorted by `(Sequence, CommandIdBytes)`.
A rejected batch returns one mapping/atomicity diagnostic pair per command,
non-committed and including the required rejected-proposed `ProposedDigest`, in
that order but does not append a committed event-log entry. These rules preserve the
fixed P2-T05 schema while making batch cardinality and applicability
deterministic.

The two specifications use this conformance matrix as the admission check:

| Contract member | P2-T03 requirement | P2-T04 requirement |
| --- | --- | --- |
| State owner | complete envelope belongs to persistent `BundleId` in `BundleState` | `NuclideState` belongs to the same persistent bundle and is retained on discharge |
| Movement | atoms, initial values, version, data identity, and history unchanged; densities use destination volume | same atom/version/history rule; no cross-bundle transport or hidden reset |
| Fresh template | current equals initial for both nuclides, version/data identity are explicit, history is empty, and empty-history digest is validated | template values are explicit and validated before first solve |
| Discharge | immutable record contains the final envelope and digest | final atoms/densities/version/data/history remain auditable |
| Version | refuel increments `CoreStateVersion` once and not a moved nuclide version | only accepted I/Xe integration increments an affected `NuclideStateVersion` |
| Rollback | complete envelope and equal-time batch restore byte-for-byte | scratch state includes inventories, histories, counters, statuses, IDs, and digests |

Any disagreement in this matrix is a failed cross-spec validation, not an
implementation choice.

### Production, decay, and absorption data

The validated `NuclideDataId` is keyed by material/table identity and supplies
finite nonnegative yields `gamma_I`, `gamma_X` and positive decay constants
`lambda_I`, `lambda_X` in `s^-1`. It supplies microscopic Xe absorption cross
sections `sigma_Xe,g^a >= 0` in `m^2` for the explicit solver groups. A missing
group, non-finite value, negative yield/cross section, material/table mismatch,
or non-positive decay constant fails closed.

Using the actual flux from the amplitude coupling, define the local fission rate
density:

```text
R_f = sum_g Sigma_f,g * phi_actual,g       [m^-3 s^-1]
```

The v1 production and loss equations are:

```text
dN_I/dt  = gamma_I * R_f - lambda_I * N_I

dN_Xe/dt = gamma_X * R_f + lambda_I * N_I
            - [lambda_X + sum_g sigma_Xe,g^a * phi_actual,g] * N_Xe
```

`gamma_I * R_f` and `gamma_X * R_f` are positive production terms. I-135 decay
is a positive Xe source and a negative I sink. Xe radioactive decay and neutron
absorption are negative Xe sinks. The per-atom absorption rate
`sigma_Xe,g^a * phi_actual,g` has unit `s^-1`.

For the common P2-T02 control volume `V_i`, the equivalent authoritative atom
equations are:

```text
dA_I/dt  = V_i * (gamma_I * R_f - lambda_I * N_I)
dA_Xe/dt = V_i * (gamma_X * R_f + lambda_I * N_I
                   - lambda_X,eff * N_Xe)
N_I  = A_I / V_i
N_Xe = A_Xe / V_i
```

The same `V_i` binds fission-rate density, nuclide density, and P2-T02 node
state during a substep. This preserves atom inventory when a bundle changes
location.

### Discrete nuclide update

With flux, amplitude, rates, and `NodeVolume` frozen at the left endpoint, V1
updates densities or, equivalently, the authoritative atom inventories:

```text
N_I,next  = N_I + Delta_t * (gamma_I * R_f - lambda_I * N_I)

lambda_X,eff = lambda_X + sum_g sigma_Xe,g^a * phi_actual,g
N_Xe,next = N_Xe + Delta_t *
            (gamma_X * R_f + lambda_I * N_I - lambda_X,eff * N_Xe)
```

Every next inventory and derived density must be finite and nonnegative. A
negative density or inventory is a failed state, not a clamp opportunity. The
same explicit `Delta_t` event split used by P2-T03 applies; refuelling events do
not accrue nuclide time at the zero-duration instant. The update records
production, decay, absorption, old/new inventories/densities, `NodeVolume`,
`NuclideDataId`, and the exact flux/spatial-solve identity.

### Xe coupling to P2-T02

Every P2-T02 base coefficient table used by this contract carries
`XenonBasis=Excluded` and `ReferenceXeNumberDensity=0 m^-3` in its immutable
coefficient identity. A table marked `Included`, `Equilibrium`, or with a
nonzero reference Xe density is rejected by v1. This makes the base absorption
explicitly xenon-free and prevents a dynamic concentration from being added to
an already poisoned table.

For each node and group, the dynamic Xe absorption contribution is:

```text
Sigma_Xe,g^a = sigma_Xe,g^a * N_Xe       [m^-1]
Sigma_a,g,eff = Sigma_a,g,base + DeltaSigma_a,g + Sigma_Xe,g^a
```

`Sigma_Xe` is absorption only. It is not fission, not scattering, and not a
separate reactivity term. The effective coefficient set must be revalidated by
P2-T02, including the complete active feedback overlay, `Sigma_a >= Sigma_f`,
finiteness, and all group invariants.
The next spatial solve is mandatory before positive-duration advancement after
the Xe contribution changes. The coefficient digest includes `XenonBasis`,
`NuclideDataId`, and the dynamic atom-inventory digest; a mismatch fails closed.

## Spatial, kinetics, and event cadence

### Exact event order

Simulation time is explicit; wall-clock time and Unity frame timing are never
inputs. A `SimulationAdvance` to `t_next` is split at every earlier event among:

- a P2-T03 burnup/refuelling boundary;
- an RRS update time;
- the earliest pending delayed-command `DueTime` greater than the current
  simulation time for an RRS, liquid-zone, or adjuster queue;
- a configured spatial-solve time;
- a configured kinetics/nuclide integration time; and
- an explicitly scheduled branch-state update.

At an event time `t`, the deterministic order is:

1. Validate the current state and exact event batch. If an automatic RRS update
   is due, freeze one `ControllerMeasurementSnapshot` before any rank-0 or
   rank-1 commit. It contains the measured total/regional powers and exact
   accepted spatial, power, kinetic, nuclide, topology, and data-pack binding
   used by that update. A missing or stale required measurement rejects the
   event before mutation.
2. Commit the preceding P2-T03 burnup interval as its own transaction. It
   remains committed even if a later refuelling batch at the same time fails.
3. Commit any due P2-T03 refuelling batch as its own atomic transaction; a
   failed batch leaves the committed preceding burnup state intact and leaves
   all spatial/power snapshots invalid.
4. Commit explicit controller-command-generation, actuator-motion, branch,
   zone, adjuster, and poison actions in the canonical event order. Rank `2`
   generates RRS commands. Rank `3` first advances each RRS physical actuator
   from its recorded motion time to `t` under the command that was available
   before `t`, then consumes the frozen due batch to select the command available
   after `t`. Rank `4` applies the equivalent fixed subphases to branch-owned
   liquid-zone and adjuster queues. The cutoff and zero-delay rules below are
   normative; a newly available command never acts retroactively over an
   interval that ended at `t`.
5. Resolve all influence maps into local absorption overlays and validate the
   effective coefficient set.
6. Perform a P2-T02 solve using current burnup, nuclide absorption, overlays,
   topology, and coefficient state. The result must bind exact state/time,
   amplitude, `CoreStateVersion`, every applicable `NuclideStateVersion`, and
   the complete version/digest data. Acceptance atomically advances the
   spatial and power snapshot versions as specified by `VersionLifecycleV1`.
7. Compute `rho_spatial` from the resulting `k`; the kinetics reactivity
   breakdown has no second feedback/Xe addition.
8. Integrate point kinetics and I/Xe over the next interval using the accepted
   left-endpoint spatial state. Any amplitude/precursor/nuclide change is an
   explicit next-snapshot boundary and splits the following P2-T03 burnup
   interval.

No positive-duration interval may begin without an exact spatial solve and a
valid kinetic/nuclide state bound to the same `CoreStateVersion`, time,
coefficient digest, and topology/data-pack versions. A failed stage rolls back
only its uncommitted transaction and returns no usable spatial/kinetic result;
it never rolls back a previously committed P2-T03 transaction.

### Cadence policies

`CadencePolicy` supplies positive finite intervals or explicit event schedules
for spatial solves, RRS updates, kinetics, and nuclide integration. Pending
delayed-command due times are scheduler events even when they fall between
those configured cadences. Every
periodic schedule also supplies an explicit `EpochTime` and integer event index;
for period `Period`, the next time is constructed as
`EpochTime + EventIndex * Period` in canonical integer order. V1 does not select
numeric default intervals. A cadence that is non-positive, non-finite, not
representable in the explicit time unit, or that permits a positive interval
without a required spatial solve is invalid. The scheduler chooses the minimum
of all configured event times and every canonical pending `DueTime`; it compares
the stored finite binary64 values exactly. If an interval contains multiple
events, the smallest next event is processed first; equal-time events use the
fixed order above. A pending record with `DueTime < current_time` is an invalid
scheduler/state invariant and fails closed rather than being silently applied
late.

P2-T03's exact post-burnup/refuelling solve requirement takes precedence over a
larger configured spatial cadence. No solver result is reused after a state,
coefficient, amplitude, nuclide, or influence-map digest changes.

### Feedback overlay ownership

The spatial input carries a `FeedbackReferenceStateDigest` identifying the
base coefficient state at which all active overlay source references are zero.
Every active source (`RRS`, temperature, purity, liquid zone, adjuster, or bulk
poison) has exactly one owner and exactly one `InfluenceMapId`. Its reference
value/state and map digest must match the spatial input digest. A source that is
already encoded in the base coefficient table, in dynamic Xe, or in another
active overlay is rejected as a duplicate owner. The effective coefficient
digest is computed after all overlays and before P2-T02; the resulting `k` is
the sole reactivity value passed to kinetics.

### Integration stability and timestep limits

Because v1 uses explicit Euler, the caller must supply an
`IntegrationStabilityPolicy` with finite positive values:

| Field | Unit | Role |
| --- | --- | --- |
| `MaximumKineticStep` | `s` | hard upper bound for a kinetic substep |
| `MaximumNuclideStep` | `s` | hard upper bound for an I/Xe substep |
| `MaximumRRSStep` | `s` | hard upper bound for controller/actuator motion |
| `MaximumDecayRate` | `s^-1` | declared envelope for all active decay/sink rates |
| `MaximumPromptRate` | `s^-1` | declared envelope for prompt amplitude dynamics |
| `MaximumDimensionlessStep` | `1` | approved stability-envelope limit |
| `PolicyVersion` / `Digest` | opaque | exact policy identity |

Before an explicit substep, the validator requires:

```text
Delta_t <= min(MaximumKineticStep, MaximumNuclideStep, MaximumRRSStep)
Delta_t * MaximumDecayRate <= MaximumDimensionlessStep
Delta_t * MaximumPromptRate <= MaximumDimensionlessStep
```

The authoritative allowed duration is:

```text
H = min(MaximumKineticStep,
        MaximumNuclideStep,
        MaximumRRSStep,
        MaximumDimensionlessStep / MaximumDecayRate,
        MaximumDimensionlessStep / MaximumPromptRate)
```

All five terms are finite positive seconds. Every scheduler and every explicit
substep uses this same `H`; it may not choose a different bound and merely
report a policy failure afterward.

The policy values are inputs, not P2-T04 defaults. If a scheduled gap
`Delta_t_gap` exceeds the allowed bound `H`, the scheduler sets
`n = ceil(Delta_t_gap / H)` and constructs exactly `n` substeps in order: the
first `n-1` steps have duration `H`, and the final step has duration
`Delta_t_gap - (n-1)*H`. The subtraction is performed left-to-right in the
authoritative scalar order. It does not silently choose another partition,
smaller tolerance, or skipped solve.
If subdivision cannot be represented in explicit `double` time or an actual
coefficient/rate exceeds the declared envelope, the state fails closed. A
substep that remains finite and nonnegative but violates the policy is still
invalid; this prevents a merely nonnegative but unstable Euler trajectory from
being accepted.

`MaximumDecayRate` must cover every active `lambda_j`, `lambda_I`,
`lambda_X,eff`, and configured actuator/branch rate. `MaximumPromptRate` must
cover the declared absolute prompt coefficient for the current reactivity
envelope. Missing coverage is a policy failure rather than an excuse to infer a
bound.

## RRS controller and influence-map schema

### RRS state

V1 represents ordinary regulating behavior only. `RRSMode` is one of
`Manual`, `Automatic`, or `Held`; there is no scram, trip, shutdown, or accident
mode in this specification.

An `RRSState` contains:

| Field | Contract | Unit |
| --- | --- | --- |
| `ControllerId` | canonical identity of the one owning RRS controller | opaque |
| `Mode` | explicit mode enum | enum |
| `PowerSetpoint` | finite nonnegative requested total power | `W` |
| `MeasuredPower` | `a * P_ref` at the last exact state | `W` |
| `PowerError` | `PowerSetpoint - MeasuredPower` | `W` |
| `IntegralError` | deterministic total-power controller integral | `W s` |
| `TiltError[r]` | regional target minus measured power fraction | `1` |
| `TiltIntegral[r]` | deterministic regional integral | `s` |
| `ActuatorCommand[q]` | latest requested actuator value committed at enqueue; it does not govern motion until due | `1` |
| `ActuatorState[q]` | physical state currently used by the influence map | `1` |
| `ActuatorReference[q]` | state represented by the spatial solve reference | `1` |
| `Bias[q]` | configured actuator bias | `1` |
| `K_P[q]` | proportional gain | `W^-1` |
| `K_I[q]` | integral gain | `(W s)^-1` |
| `K_T[q,r]` | regional tilt gain | `1` |
| `K_TI[q,r]` | regional tilt-integral gain | `s^-1` |
| `u_min[q]`, `u_max[q]` | explicit actuator bounds | `1` |
| `RateLimit[q]` | maximum actuator-state rate | `s^-1` |
| `Delay[q]` | command-to-state delay | `s` |
| `InfluenceMapId` | exact map used for the current command | opaque |
| `ControlPolarity` | required `NegativeFeedback` declaration | enum |
| `SignCertificateId` | map/gain sign validation identity | opaque |
| `FeedbackReferenceStateDigest` | base-state binding for overlay deltas | opaque |
| `LastUpdateTime` | exact simulation time | `s` |
| `CommandQueue` | authoritative `RRSQueueStateV1` including available commands, pending records, allocator, motion time, and digest | structured |

The caller must supply the controller identity, setpoint, tilt regions/targets,
gains, biases, bounds, rate limits, delays, reference states, initial mode,
initial integrals, initial requested commands, initial available commands,
initial actuator states, initial queue/allocator/motion-time state, and the map
identity. Missing defaults are invalid.

### Total-power and tilt observables

The RRS configuration supplies a disjoint, complete set of named regional
observables. For region `r`:

```text
P_region,r = sum over i in Region_r P_i
f_region,r = P_region,r / P_total
TiltError_r = TargetFraction_r - f_region,r
```

Each region has a canonical `RegionId`, an explicit ordered node list, and a
finite target fraction. The target fractions sum to one in scalar canonical
order. A duplicate/missing node, overlapping region, nonpositive total power,
or invalid target set fails closed. This observable is the tilt signal; it is
not inferred from channel number or array order.

### Authoritative delayed-command queue state

Each controller owns exactly one `RRSQueueStateV1`; a queue cannot be shared by
two controller owners or rebound after initialization. Its authoritative fields
are:

| Field | Contract |
| --- | --- |
| `QueueId` | immutable canonical queue identity |
| `OwnerKey` | typed P2-T05 `Entity.Controller(ControllerId)` key |
| `GenerationCadenceOrNA` | positive finite automatic-generation cadence, or explicit `NotApplicable` for event-only/manual ownership |
| `InitialNextSequence` | explicit immutable first unallocated `UInt64` value |
| `NextSequence` | current first unallocated `UInt64` value |
| `LastMotionTime` | finite physical-state time, initially equal to the scenario state time |
| `AvailableCommand[q]` | finite bounded command active immediately after `LastMotionTime` |
| `PendingCommands` | complete `RRSCommandV1` records in canonical queue order |
| `AppliedSourceEventIds` | append-only canonical set of every successfully committed source-event UUID |
| `AllocatedCommandIds` | append-only canonical set of every pending or consumed command UUID |
| `QueueDigest` | SHA-256 of the canonical queue-state bytes excluding this digest field |

The canonical queue-state bytes begin with ASCII magic `CANDU-RRS-QUEUE-V1`,
the P2-T05 zero separator, and `UInt32` schema version `1`, followed by these
P2-T05-encoded fields in order: `QueueId`, `OwnerKey`,
`GenerationCadenceOrNA`, `InitialNextSequence`,
`NextSequence`, `LastMotionTime`, available-command entries in ascending
`ActuatorIdBytes` order, and pending records sorted by
`(DueTime, EventRank, Sequence, CommandIdBytes)`, followed by
`AppliedSourceEventIds` and `AllocatedCommandIds`, each in ascending UUID-byte
order. Each available entry contains `ActuatorId`, bounded command, lower/upper
bound, and rate limit. Missing, duplicate, reordered, non-finite, out-of-bound,
unknown, or owner/queue-inconsistent state fails closed. Every available entry's
target, bounds, and rate limit must equal the owning controller or branch
configuration; those copies make the queue self-contained for replay but do not
create a second mutable configuration.

Enqueue appends the one source-event ID to `AppliedSourceEventIds` and every
newly derived command ID to `AllocatedCommandIds` in the same atomic commit as
the queue records; consume never removes either. These registries are the
authoritative replay and collision checks across pending and historical events
and commands and are included in save/load/replay. A digest without both
underlying canonical ID sets is insufficient and is rejected.

`NextSequence` is monotonic and is part of every save, replay precondition, and
transaction rollback. Allocating `count` records is legal only when unsigned
addition `NextSequence + count` is representable; the assigned values are
`NextSequence .. NextSequence+count-1`, and the post-state stores the sum. Thus
`UInt64.MaxValue` is never allocated as a v1 sequence because no representable
next-unused value would remain. A consumed record does not release its sequence.
At every boundary, `NextSequence >= InitialNextSequence`, every pending sequence
lies in that half-open range, and
`count(AllocatedCommandIds) = NextSequence - InitialNextSequence`. The count and
each append to either historical ID registry must also fit the P2-T05 `UInt32`
array-count encoding; enqueue preflights all capacities before deriving or
committing any record.

`ActuatorCommand[q]`, `AvailableCommand[q]`, and `ActuatorState[q]` are three
different state projections. Enqueue changes the first and the pending queue;
due consumption changes the second; elapsed motion changes the third. No field
aliases another, and all three plus `LastMotionTime`, allocator state, pending
records, both historical ID registries, and queue digest are included in
rollback and round-trip serialization. Snapshots and saves are admitted only at
complete event boundaries; an in-progress `DueBatch` is transaction scratch and
is discarded on interruption.

### Controller sign and update

At an automatic RRS update, use:

```text
Delta_t_RRS = t - LastUpdateTime
PowerError = PowerSetpoint - MeasuredPower
IntegralError_next = IntegralError + PowerError * Delta_t_RRS
TiltIntegral_next,r = TiltIntegral_r + TiltError_r * Delta_t_RRS
ActuatorCommand_q = Bias_q + K_P,q * PowerError
                   + K_I,q * IntegralError_next
                   + sum_r (K_T,q,r * TiltError_r
                            + K_TI,q,r * TiltIntegral_next,r)
```

`Delta_t_RRS` must be finite, positive, and equal to the controller's declared
event schedule. At most one automatic generation event for one `ControllerId`
is legal at one event time. It uses the event's frozen
`ControllerMeasurementSnapshot`, including when a burnup/refuelling commit at
the same `t` has already advanced the current core state. Its successful atomic
commit sets the measured power/error fields and `LastUpdateTime=t`; a rejection
leaves those fields, `LastUpdateTime`, and every integral unchanged. Manual and
scheduled enqueue events bind the post-rank-1 event pre-state and do not advance
controller integrals or `LastUpdateTime`.

Positive total-power error means actual power is below setpoint. In a validated
negative-feedback configuration, the map/gain sign certificate requires a
small positive total-power error to produce a power-increasing effective
coefficient change, and a regional error to reduce that region's tilt. The
certificate is a data-pack sign assertion tied to `ReferenceStateDigest`; a
missing or contradictory assertion rejects `Automatic` mode. Manual mode uses
an explicit command and does not advance controller integrals. Held mode keeps
both actuator states and integrals unchanged while measurement/diagnostics
continue at the declared cadence.

`ActuatorCommand` is neither the available command nor the physical state.
Every delayed command is a closed `RRSCommandV1` record. Its canonical digest
bytes begin with ASCII `CANDU-RRS-COMMAND-V1`, the P2-T05 zero separator, and
`UInt32` schema version `1`, followed by these fields in order: `QueueId`,
`CommandId` (canonical UUID bytes),
`ActuatorId`, `OwnerKey`, `SourceKind`, `SourceEventId`,
`SourceStateBindingDigest`, `EnqueueTime`, `Delay`, `DueTime`, `EventRank`,
`Sequence`, `RequestedCommand`, `BoundedCommand`, `u_min`, `u_max`, and
`RateLimit`; `CommandDigest` follows and is not included in its own SHA-256.
`SourceKind` is `Controller`, `Manual`, or `Scheduled`; every RRS source kind is
generated in the rank-2 command-generation phase, so an RRS source/rank pair
that is not `EventRank=2` is invalid. `SourceStateBindingDigest` binds generation
to the automatic event's frozen pre-rank-0 measurement snapshot or to the
explicit manual/scheduled post-rank-1 event pre-state. It is never rebound to a
post-refuelling state merely because rank `1` committed before rank `2`.
`QueueId` and `OwnerKey=Entity.Controller(ControllerId)` must match the owning
queue, while `ActuatorId` binds the target; a record cannot be moved to another
queue, controller, actuator, or branch without failing its identity and digest
checks.

`CommandId` is deterministic and is never supplied by a random UUID generator,
wall clock, thread, or Unity frame. After the queue allocator assigns `Sequence`,
construct these canonical bytes using the P2-T05 field encodings:

```text
ASCII "CANDU-RRS-COMMAND-ID-V1", 0x00,
QueueId, SourceEventId, OwnerKey, ActuatorId, Sequence
```

Hash the bytes with SHA-256, take the first 16 bytes, set the UUID version nibble
to `8` and the RFC-4122 variant bits exactly as P2-T05 does for `ObservableId`,
and store those network/display-order bytes as `CommandId`. A collision with any
live, pending, consumed, or historical command identity fails the complete
generation transaction. `CommandDigest` is then computed over the complete
canonical field list above, including `QueueId` and the derived `CommandId`.

At the event time `t`, a command's `EnqueueTime` is exactly `t`, and its due time
is constructed exactly once as one IEEE-754 binary64 addition:

```text
DueTime = EnqueueTime + Delay
```

`EnqueueTime` and `Delay` must be finite, `Delay >= 0`, and the stored `DueTime`
must be finite, nonnegative, and representable. Overflow, a non-finite result,
negative zero in an authoritative field, or a value that does not equal the
stored binary64 result rejects the command. Due comparisons use exact stored
binary64 values (`DueTime <= t`); no epsilon, rounding, or unapproved tolerance
is used. A periodic cadence may create a later enqueue event, but it never
replaces this per-command construction.

The configured actuator bounds are applied before enqueue:

```text
BoundedCommand_q = min(u_max,q, max(u_min,q, RequestedCommand_q))
```

The candidate is accepted only when the bounds are finite and ordered,
`RateLimit_q >= 0`, the requested and bounded values are finite, the target and
source identities exist, the source binding digest matches the required frozen
measurement snapshot or explicit event pre-state, the canonical `SourceEventId`
is new in the owner event-applied registry, the event rank/source pair is valid,
`CommandId` is new, `Sequence` is unique and not exhausted, and all queue/state
digests and versions match the event precondition. Saturation is not an invalid-input repair: it is recorded as
`ActuatorSaturated=true` with both requested and bounded command values.
Invalid bounds, non-finite values, identity/version mismatches, or a non-finite
derived result fail closed. Silent clamping of data, flux, density, or state
values remains forbidden.

### RRS command enqueue and due-batch transactions

The controller-command-generation event at rank `2` is an atomic transaction.
For an automatic update it snapshots the pre-event `RRSState`, the current
queue digest, the frozen `ControllerMeasurementSnapshot` and its binding digest,
and the event-time versions. It computes the next measured power/error fields,
controller integrals, and one candidate
`RRSCommandV1` per selected actuator in canonical `ActuatorIdBytes` order. It
preflights allocator and both registry capacities, assigns consecutive sequences
in that order, derives each `CommandId`, constructs each due time exactly once, and computes
each command digest. All candidate records are then validated and sorted by
`(DueTime, EventRank, Sequence, CommandIdBytes)`. If any
precondition, digest, finite-value, rank, identity, sequence, bound, or due-time
check fails, the integral update, `ActuatorCommand` values, and queue remain
byte-for-byte unchanged, including measured power/error fields,
`LastUpdateTime`, allocator state, queue digest, and both historical ID
registries; the rejected event records the first canonical diagnostic and no
partial command is visible. If all checks pass, the measured power/error and
integral updates, `LastUpdateTime`, requested commands, allocator advance, and
every queue insertion plus the `AppliedSourceEventIds` marker commit together
with the new queue digest. Enqueue never changes
`AvailableCommand`, `LastMotionTime`, or `ActuatorState`.

Manual or scheduled enqueue uses the same all-or-none preconditions and queue
ordering. The source event has its own scheduler `Sequence`; command sequences
come only from the queue allocator and are never copied from that event or from
array/storage order. Within one source event, candidates are ordered by
`ActuatorIdBytes`, receive consecutive queue sequences, and derive their command
IDs as above. Duplicate source/target pairs, an identity collision with live or
historical state, insufficient allocator capacity, or a batch digest mismatch
rejects the entire batch. A committed record is inserted once and cannot be
re-enqueued under the same identity. The allocator advances only with the
committed insertions; a rejected or rolled-back transaction leaves it unchanged.

After all rank-2 controller-generation events at time `t` commit, the scheduler
takes one immutable `DueBatch(t)` snapshot of records present in the queue with
exact `DueTime <= t`, including commands whose `Delay=0` and therefore have
`DueTime=t`. The snapshot is sorted by
`(DueTime, EventRank, Sequence, CommandIdBytes)`. A zero-delay controller
command is consequently consumed once in the same event-time actuator-motion
phase (rank `3`), after all rank-2 generation events finish; consuming it never
recursively invokes controller generation. RRS enqueue is legal only at rank
`2`, so no RRS record may be admitted after this cutoff at the same event time;
an attempted late or wrong-rank enqueue fails before mutation. A pre-existing
record with `DueTime < t` violates the scheduler invariant because its stored due
time should have been the earlier explicit event boundary. This cutoff is part
of the deterministic event contract, not an implementation choice.

The rank-3 phase runs for every explicit event boundary before overlays and the
spatial solve, even when `DueBatch(t)` is empty, so `ActuatorState` always
represents exact time `t`. Due-batch consumption is part of that atomic
transaction. Its
preconditions are a matching queue digest and event-time binding, a finite
`Delta_t_motion = t - LastMotionTime >= 0`, valid available commands, actuator
states/limits, unique unconsumed records in the frozen batch, and exact
due-time/order validation. Before any newly due command becomes available, each
physical state advances over `[LastMotionTime,t]` under the pre-transaction
`AvailableCommand[q]` and the `RRSMode` captured before any rank-2 event at `t`,
in ascending `ActuatorIdBytes` order and the exact
stability-policy substeps:

```text
delta_u = AvailableCommand_before,q - ActuatorState_q
ActuatorState_next,q = ActuatorState_q
                       + sign(delta_u) * min(abs(delta_u), RateLimit_q*Delta_t)
```

The equation applies when that captured mode is `Manual` or `Automatic`. When
it is `Held`, the
proposed physical state is exactly the prior state for every substep, while
`LastMotionTime` still advances and a due batch may update the command that will
be available after the hold is released. Held mode never accumulates unrecorded
motion time. Any mode transition committed at `t` becomes active only for
intervals beginning at `t`; it cannot reinterpret the elapsed interval.

Here each substep's `Delta_t` is the exact partition supplied by the common `H`
rule and their sum is `Delta_t_motion`. After every proposed physical state has
validated, the last due record for each actuator in canonical batch order becomes
`AvailableCommand_after,q`; an actuator with no due record retains its prior
available command. The zero-delay command is therefore available for intervals
beginning at `t`, but it produces no instantaneous physical-state jump and does
not act over elapsed time before `t`.

All physical transitions, `LastMotionTime=t`, available-command changes,
saturation/delay diagnostics, removal of every frozen record exactly once, and
the new queue digest commit together. If any due record, target, state, rate
calculation, transition, allocator, or digest is invalid, no queue entry is
removed and no actuator, available-command, motion-time, requested-command, or
allocator state changes; the queue and RRS state return byte-for-byte to their
pre-consumption digests and the first canonical rejection is recorded. A record
never remains eligible after a committed removal, and a failed transaction
never consumes a record. The influence map receives only the resulting bounded
physical `ActuatorState` at exact time `t`.

### RRS influence map

An `RRSInfluenceMap` contains sparse local absorption-overlay entries:

```text
(ActuatorId, TargetNode, Group, Weight, SourceUnit, TargetUnit,
 ReferenceActuatorState, MapVersion, ReferenceStateDigest)
```

`TargetNode` is a canonical P2-T01 `(ChannelId, BundlePosition)` key; v1 does
not use an implicit `Global` target. `Weight` has unit `m^-1` per unit actuator
state. V1 aggregates the map deterministically:

```text
DeltaSigma_a,g,i = sum_q Weight_(q,i,g) *
                   (ActuatorState_q - ReferenceActuatorState_q)
```

Entries are sorted by `(ActuatorIdBytes, TargetChannel, TargetPosition, Group)`
before scalar reduction. A duplicate key, missing target/group, non-finite
weight, incompatible units, unknown actuator, reference-state mismatch, or
map-version mismatch fails closed. The map is a local P2-T02 coefficient
overlay only; it is not also added as a direct reactivity term. The
negative-feedback sign certificate is checked using this same overlay and
reference state.

Every `ActuatorId`, `BankId`, map ID, branch ID, and event ID uses canonical
lowercase ASCII UUID bytes for comparison. Equal-time controller/branch events
are ordered by `(EventRank, Sequence, EventIdBytes)`, where `EventRank` is
`0=Burnup`, `1=Refuelling`, `2=ControllerCommandGeneration`,
`3=ActuatorMotion`, `4=BranchUpdate`, `5=SpatialSolve`, and
`6=KineticNuclideStep`. Controller generation at rank `2` is after all due
burnup/refuelling commits and before any rank-3 actuator motion. The rank-3
motion phase uses the frozen due-batch and causal motion-before-consume rule
above; branch/zone actions at rank `4` cannot retroactively change that batch.
Within rank `4`, physical branch motion to `t`, command generation, cutoff, and
due consumption use the fixed subphases below. P2-T03's own UUID/sequence batch
ordering applies within ranks `0` and `1`. A duplicate sequence/ID, missing
event rank, wrong subphase, or source/rank mismatch fails before mutation.

## Temperature, purity, and device branch schemas

### Generic branch rule

Each branch has explicit `Enabled` state, source identity, reference value,
current state, update mode, `InfluenceMapId`, data version, and digest. The
branch coefficient-overlay contribution is zero exactly when disabled.
Disabled state is still serialized and validated; it cannot be omitted and
later inferred.

For a branch source `x` and reference `x_ref`, a validated overlay influence map
uses:

```text
DeltaSigma_a,g,i = sum_s Weight_(s,i,g) * (x_s - x_ref,s)
```

`Weight` carries `m^-1` per source unit and outputs a local absorption
coefficient. The map is sparse, sorted by canonical source/target/group order,
and has no implicit normalization. A nonzero disabled contribution, missing
branch state, direct-reactivity target, or map-version mismatch fails closed.

### Temperature branch

`TemperatureBranchState` contains one explicit temperature per configured
feedback region or node, with an explicit kind:

| Field | Unit |
| --- | --- |
| `Enabled` | boolean |
| `Mode` (`Disabled` or `Prescribed`) | enum |
| `RegionOrNodeId` | canonical region/node identity |
| `TemperatureKind` (`Fuel`, `Coolant`, or `Moderator`) | enum |
| `Temperature` | `K` |
| `ReferenceTemperature` | `K` |
| `InfluenceMapId` | opaque |
| `DataVersion` / `Digest` | versioned identity |
| `UpdateTime` | `s` |

Temperatures must be finite and physically representable by the data pack. In
`Disabled` mode, the temperature overlay is exactly zero and the value is not
evolved. In
`Prescribed` mode, a finite explicit value is supplied at each branch update;
V1 has no thermal equation and does not extrapolate between missing values.

### Purity branch

`PurityBranchState` uses authoritative moderator isotopic/purity mass fraction,
not ppm and not bulk-poison concentration:

| Field | Unit |
| --- | --- |
| `Enabled` | boolean |
| `Mode` (`Disabled` or `Prescribed`) | enum |
| `RegionOrNodeId` | canonical region/node identity |
| `ModeratorPurityMassFraction` | `kg/kg` |
| `ReferenceModeratorPurityMassFraction` | `kg/kg` |
| `InfluenceMapId` | opaque |
| `DataVersion` / `Digest` | versioned identity |
| `UpdateTime` | `s` |

The mass fraction and reference mass fraction are finite and in `[0,1]`.
`Disabled` contributes exactly
zero. `Prescribed` requires an explicit value at the update time. Chemistry,
transport, production, and slow removal are deferred; no hidden decay or
cleanup law is added.

### Liquid-zone maps

The v1 grouping schema requires exactly 14 independently tracked logical
liquid-zone regions and exactly 6 declared physical-zone assemblies. Every
logical region maps to exactly one physical assembly; multiple logical regions
may map to one assembly, but no mapping is inferred. A different grouping is a
different versioned schema. Each `LiquidZoneState` contains:

| Field | Contract | Unit |
| --- | --- | --- |
| `LogicalZoneId` | integer in `[0,13]` | `1` |
| `PhysicalAssemblyId` | integer in `[0,5]` | `1` |
| `StateFillFraction` | physical fill state used by the map | dimensionless `[0,1]` |
| `ReferenceFillFraction` | map reference state | dimensionless `[0,1]` |
| `CommandFillFraction` | requested fill state | dimensionless `[0,1]` |
| `AvailableCommandFillFraction` | read-only projection of the command queue's bounded fill active after its motion time; not separately stored | dimensionless `[0,1]` |
| `Enabled` | explicit branch state | boolean |
| `Mode` (`Disabled`, `Prescribed`, `RateLimited`) | update mode | enum |
| `RateLimit` | maximum fill-state rate | `s^-1` |
| `Delay` | command-to-state delay | `s` |
| `InfluenceMapId` | exact owning map | opaque |
| `DataVersion` / `Digest` | versioned state identity | opaque |
| `UpdateTime` | exact latest branch event time | `s` |
| `LastMotionTime` | read-only projection of the command queue's exact physical-state time; not separately stored | `s` |
| `CommandQueue` | authoritative complete owner-bound `QueueStateV1` | structured |

The logical-to-physical mapping is data and must be complete, unique, and
versioned, with exactly 14 entries and all six physical assembly IDs represented
in the declared grouping. It is never inferred from channel order. A zone map
contributes a local absorption overlay:

```text
DeltaSigma_a,g,i = sum_z Weight_(z,i,g) *
                    (StateFillFraction_z - ReferenceFillFraction_z)
```

Each fill value and map entry is validated before aggregation. In
`RateLimited` mode, after the explicit delay, the physical state follows:

```text
delta_f = available_command_fill - StateFillFraction
StateFillFraction_next = StateFillFraction
                         + sign(delta_f) *
                           min(abs(delta_f), RateLimit*Delta_t)
```

`RateLimit >= 0` and `Delay >= 0` are required. `Prescribed` updates are
explicit scenario inputs at an event time; they do not imply hidden flow or
thermal dynamics. A map does not claim to model coolant flow or thermal
hydraulics.

Zone fill commands use the closed `ZoneCommandV1` field order: `QueueId`,
`ZoneCommandId`, `LogicalZoneId`, `OwnerKey`, `SourceKind`, `SourceEventId`,
`SourceStateBindingDigest`, `EnqueueTime`, `Delay`, `DueTime`, `EventRank`,
`Sequence`, `RequestedFill`, `BoundedFill`, `LowerBound=0`, `UpperBound=1`, and
`RateLimit`, followed by `CommandDigest`. `OwnerKey` is the typed owning branch,
and queue/owner/target identity is included in the digest. The zone command ID
uses the RRS UUIDv8 derivation with domain separator
`CANDU-ZONE-COMMAND-ID-V1` and `LogicalZoneId` as the target bytes; its command
digest uses the `CANDU-ZONE-COMMAND-V1` magic, zero separator, `UInt32` schema
version `1`, and declared field order. Zone records
use `EventRank=4` (`BranchUpdate`) and the same closed source-kind enum; another
source/rank pair fails closed.

Rank `4` runs at every explicit event boundary and has these fixed subphases:
first advance every rate-limited zone from
its `LastMotionTime` to `t` under its pre-event
`AvailableCommandFillFraction`; second process all zone generation events in
`(EventRank, Sequence, EventIdBytes)` order; third freeze each owner queue's
records with exact `DueTime <= t`; and fourth consume each frozen batch in
ascending owner/queue order, selecting the final due bounded fill as the
available command after `t`. Motion uses the same `H` partition and
motion-before-consume atomic rollback contract as RRS. A zero-delay zone command
is consumed at the same `t` but affects only intervals beginning at `t`; it does
not retroactively move the fill state. If no due command exists, the previous
available command is retained. A zone command cannot alter an already frozen
RRS actuator batch. These subphases and the owner-typed queue-state serialization
are part of the zone schema, not implementation choices. Zone queue-state bytes
use the generic P2-T05 `QueueStateV1` layout with magic `CANDU-ZONE-QUEUE-V1`;
adjuster queue-state bytes use `CANDU-ADJUSTER-QUEUE-V1`.

### Adjuster maps

An `AdjusterBankState` contains `BankId`, explicit insertion fraction in
`[0,1]`, `Enabled`, `Mode` (`Manual`, `Prescribed`, or `RateLimited`),
`ReferenceFraction`, `CommandFraction`, `StateFraction`, rate limit, delay,
`InfluenceMapId`, `DataVersion`, `Digest`, and `UpdateTime`. Motion is a
rate-limited action with the same delayed-state contract as RRS actuators; the
map consumes only `StateFraction`. The v1 contribution is:

```text
DeltaSigma_a,g,i = sum_bank Weight_(bank,i,g) *
                    (StateFraction_bank - ReferenceFraction_bank)
```

The map signs and units are explicit. Mechanical motion is not a shutdown
bank, and automatic trip behavior is out of scope.

In `RateLimited` mode the bank additionally owns a complete queue state and
exposes that queue's `AvailableCommandFraction` and `LastMotionTime` as read-only
projections, never as duplicate authoritative fields. Its `AdjusterCommandV1`
substitutes `BankId`, requested/bounded fraction, the
`CANDU-ADJUSTER-COMMAND-ID-V1` ID separator, and the
`CANDU-ADJUSTER-COMMAND-V1` digest magic into the exact zone command and rank-4
subphase contract above. Missing queue state, a projection mismatch, or an
attempt to apply a newly due command over elapsed time before its due event
fails closed.

### Bulk-poison maps

`BulkPoisonState` contains `Enabled`, `Mode` (`Disabled`, `Add`, `Withdraw`, or
`Prescribed`), `PoisonMass`, `ModeratorVolume`, derived
`PoisonMassConcentration`, reference concentration, `AddRate`, `WithdrawRate`,
map identity, data version, digest, and update time:

```text
PoisonMassConcentration = PoisonMass / ModeratorVolume [kg/m^3]
DeltaSigma_a,g,i = sum_s Weight_(s,i,g) *
                    (PoisonMassConcentration - ReferenceConcentration)
```

Mass must be finite and nonnegative; moderator volume must be finite and
positive. `AddRate >= 0` and `WithdrawRate >= 0` have unit `kg/s`. `Add` and
`Withdraw` are separate rate-limited actions:

```text
DeltaMass = +AddRate * Delta_t  in Add mode
DeltaMass = -WithdrawRate * Delta_t in Withdraw mode
PoisonMass_next = PoisonMass + DeltaMass
```

`Withdraw` is the slow cleanup action and cannot exceed available mass.
`Prescribed` is permitted only for initial setup or an explicitly scheduled
scenario setup before positive-duration advancement; it cannot bypass the
runtime add/withdraw rate contract. Every action records requested/applied mass
and the map digest; no hidden cleanup law is added.

## Initial conditions and validation

The scenario manifest must provide, with units and version identities:

- `SimulationTime_0` and `KineticState` initial amplitude/precursors;
- delayed-neutron `Lambda`, `beta_j`, and `lambda_j` data;
- initial I/Xe atom inventories (or explicitly unit-converted densities) for
  every live bundle and their material/table-keyed yields/decay/cross sections;
- initial RRS mode, setpoint, integral, gains, bounds, actuator states, and
  influence map, plus the explicit queue owner/ID, initial allocator,
  available commands, pending records, and motion time;
- temperature/purity/zone/adjuster/poison branch states and maps, including
  whether each is disabled and every applicable owner-bound queue state; and
- spatial, data-pack, topology, coefficient-table, and schema identities; and
- the explicit `VersionLifecycleV1` initial counters for the global state and
  every live bundle's `NuclideStateVersion`.

No equilibrium, zero, reference, or critical value is inferred when a field is
missing. All initial fields are checked for finiteness, ranges, exact units,
unique IDs, version/checksum agreement, and canonical ordering before the first
spatial solve.

## Diagnostics and fail-closed behavior

Every committed event records:

- event type, exact simulation time, event order, state versions, and all input
  data/map/table digests;
- P2-T02 result identity, `k`, `rho_spatial`, effective coefficient-overlay
  contributions/digests, and the total reactivity;
- amplitude/precursor old/new values and update terms;
- fission-rate, node volume, I/Xe production/decay/absorption terms and
  old/new inventories/densities;
- refuelling movement, insertion, and discharge records with complete I/Xe
  envelopes, initial values, node-volume binding, nuclide versions,
  data/history digests, and before/proposed/unchanged atomicity digests;
- RRS error, integral, raw/bounded actuator values, saturation flag, and map
  identity, plus queue owner/ID, allocator, requested/available/physical state,
  motion interval, due-batch order, and before/after queue digests;
- branch values, disabled flags, map entries used, and contributions; and
- `committed=true`, `invalid_count=0`, and the deterministic first/last key.

NaN, infinity, negative amplitude/precursor/density, invalid fission rate,
invalid or negative I/Xe inventory, non-positive node volume, derived-density
mismatch, invalid decay/cross-section data, negative power, invalid `k`, stale
or missing spatial snapshot, stale map/table/version, missing or reordered I/Xe
history, invalid cadence, duplicate map key, out-of-range branch state,
invalid actuator bound, queue-owner mismatch, command-ID collision, allocator
overflow/reuse, overdue command, retroactive motion, failed coefficient
invariant, negative explicit-Euler result, attempted data repair, or
nonconvergence fails closed with `committed=false` and no usable partial state.
An explicit RRS actuator saturation is recorded as described above and is not a
silent data clamp.

Repeated execution with identical state bytes, event bytes, table/map bytes,
and cadence inputs must produce identical kinetic, nuclide, RRS, branch,
reactivity, event, and diagnostic bytes. No unordered collection, wall-clock
value, random value, or Unity frame count may affect the result.

## Synthetic delayed-command checks

These values are synthetic contract fixtures, not production controller data.

1. At `LastMotionTime=8 s`, let one actuator have `ActuatorState=0`,
   `AvailableCommand=0.2`, and `RateLimit=0.1 s^-1`. At `t=10 s`, a rank-2
   zero-delay command requests and bounds to `0.9`. Rank `3` first moves under
   the old available command for `2 s`, producing `ActuatorState=0.2`, then
   consumes the new command. The exact post-event state is therefore
   `ActuatorState=0.2`, `AvailableCommand=0.9`, and `LastMotionTime=10 s`.
   At the next event `t=12 s`, motion under `0.9` produces
   `ActuatorState=0.4`. A result of `0.9` or any state above `0.2` at `t=10 s`
   is a retroactive-motion failure.
2. A record enqueued at exact `5 s` with exact `Delay=2 s` stores
   `DueTime=7 s`; the scheduler must expose `7 s` as the next event even when
   the periodic RRS cadence would otherwise skip it. Two records for the same
   target and due time are consumed in increasing sequence/UUID order, and the
   final record in that order becomes available after the event.
3. Starting with `NextSequence=UInt64.MaxValue-1`, an attempted two-record
   enqueue cannot represent the next-unused post-value. The complete enqueue is
   rejected: integrals, requested/available/physical state, allocator, ID
   registry, pending records, event-applied registry, and both digests remain
   byte-for-byte equal to the pre-state.
4. Permuting candidate input or pending-record storage order does not change the
   assigned target order, deterministic UUIDv8 command IDs, canonical queue
   bytes, or queue digest. Changing queue owner, target, source event, or
   sequence changes the derived identity and command digest.

## Synthetic dimensional and sign checks

### Reactivity sign

For the synthetic `k=1.02`:

```text
rho_spatial = (1.02 - 1) / 1.02 = 0.02 / 1.02 > 0
```

The positive value is a positive prompt source contribution. A synthetic
`k=0.98` produces a negative value. Neither number is a production reference.

### Xenon dimensions

The product `sigma_Xe^a [m^2] * N_Xe [m^-3]` is
`Sigma_Xe^a [m^-1]`. The per-atom rate is
`sigma_Xe^a [m^2] * phi [m^-2 s^-1] = [s^-1]`; multiplying that rate by
`N_Xe [m^-3]` gives the density loss `[m^-3 s^-1]`. The macroscopic product is
therefore an absorption addition to P2-T02, not a fission source.

### RRS sign

If `PowerSetpoint > MeasuredPower`, `PowerError > 0`. A validated negative
absorption-overlay weight for the relevant actuator lowers `Sigma_a,eff`, raises
the next spatial `k`, and therefore raises the kinetic prompt source. A positive
weight reverses that effect. The sign certificate is data-bound and is never
inferred from actuator names.

### Disabled feedback

For a disabled temperature, purity, liquid-zone, adjuster, or bulk-poison
branch, the corresponding contribution is exactly `0` and the serialized state
remains present. Enabling a branch requires a versioned map and explicit state;
it cannot be inferred from a nonzero value alone.

## Reference-case boundary and deferred behavior

P1 compact exports are provenance/parser evidence only. They do not provide
delayed-neutron constants, I/Xe yields, decay constants, RRS gains, influence
maps, temperature/purity values, or approved cadence/tolerances. DRAGON5 and
DONJON5 remain offline reference tools and are never runtime dependencies.

The following remain deferred without being silently approximated here:

- thermal-hydraulic and CFD temperature/purity evolution;
- detailed reactor regulating hardware, shutdown/scram/trip behavior, and
  accident progression;
- direct reactivity feedback maps; v1 uses only validated local absorption
  overlays before P2-T02;
- alternate kinetics integrators or automatic step-size control; and
- reference/golden comparison histories and quantity-specific tolerances.

## Definition of done for P2-T04

- Point kinetics, reactivity signs, delayed-neutron units, and deterministic
  amplitude/precursor updates are explicit.
- I-135/Xe-135 production, decay, absorption, initial conditions, units, and
  P2-T02 coupling are explicit without double counting.
- Refuelling owns a complete I/Xe envelope across movement, fresh insertion,
  discharge, event serialization, equal-time batching, and byte-for-byte
  rollback, with the P2-T03 contract aligned to the version/history rules here.
- RRS state, PI sign convention, explicit saturation diagnostics, cadence, and
  influence-map schema are explicit without safety-system behavior; delayed
  queue ownership, deterministic IDs/allocation, causal motion, same-time
  cutoff, and atomic rollback are closed.
- Temperature, purity, liquid-zone, adjuster, and bulk-poison branch schemas and
  direct influence-map representation are explicit, with disabled behavior
  producing exactly zero contribution.
- State ownership, event order, exact snapshot binding, invalid-state handling,
  diagnostics, determinism, and unit/sign checks are explicit.
- No runtime implementation, private reference data, golden tolerance, Unity
  dependency, or later-phase safety behavior is introduced.

Source: the retained P2-T04 specification and the retained P2-T02/P2-T03
specifications in this directory.
