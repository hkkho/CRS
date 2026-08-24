# Reduced offline model and interpolation-data boundary v1

**Status:** frozen P4-T06-R3 boundary input. This document is a semantic
boundary specification; it is not a new runtime serialization schema, a new
physics-equation selection, a tolerance profile, or a golden-data approval.

## Purpose and authority

This document freezes the alternate path selected by `P4-T06-R2D` after the
direct Candu6.x2m source-to-P2-T02 admission audit remained unresolved. The
final game may use a faster reduced model and offline-generated interpolation
data, but it must not use the exact external reference data as its runtime
model.

The frozen P2-T01 topology and units, P2-T02 two-group solver and
normalization, P2-T03 burnup/refuelling transitions, P2-T04 kinetics/I-Xe/RRS
contracts, and P2-T05 observable methodology remain the authorities for their
fields. This specification only defines how an offline reduced or synthetic
producer may be separated from those existing contracts.

The applicable P1-T08 literature rows are `S1-R02`, `S1-R03`, `S1-R04`,
`S1-R06`, `S1-R11`, `S5-R03`, `S5-R04`, `S5-R09`, `S6-R03`, and `S6-R08`.
They provide source, tool, CANDU, and applicability context only. They do not
authorize a runtime equation, coefficient value, unit conversion,
normalization, tolerance, or golden value.

## Frozen boundary

The allowed conceptual flow is:

```text
synthetic or separately admitted offline input
        |
        v
reduced-model producer or deterministic converter (offline only)
        |
        v
candidate compact projection of BurnupCoefficientTableV1
        |
        v
existing SpatialRecomputeRequestV1
  + explicit topology-bound conductances
  + explicit node volumes
  + caller-owned solve and convergence policies
        |
        v
existing P2-T02 spatial solver and P5-T06 recomputation boundary
```

The producer is not a runtime dependency. Runtime performs no DRAGON5 or
DONJON5 execution, raw-reference parsing, model fitting, or source conversion.
The runtime-facing projection is the existing `BurnupCoefficientTableV1`
contract; this task does not add a second table type or a new public data-pack
schema.

The direct Candu6.x2m path is retired for P2-T02 admission. Its output may be
retained as licensed, offline candidate or calibration evidence, but it is not
the authority for runtime coefficients, node identity, volumes, per-fission
energy, topology, conductances, normalization, or golden comparison values.

## Contract ownership

| Quantity or decision | Existing authority | R3 boundary rule |
| --- | --- | --- |
| Channel, bundle-position, node, adjacency, boundary, and flow identity | P2-T01 topology and indexing | Must be explicit and must not be inferred from source or array order. |
| Burnup-indexed material coefficients | `BurnupCoefficientTableV1` and P2-T02 | The projection must be semantically valid for the existing v1 table contract. |
| Node volume `V_i` | `SpatialNodeVolumeV1` and P2-T02 | Supplied separately as explicit positive SI `m^3`; never placed in a material table or inferred from a reference array. |
| Edge conductance `T_g,ij` and boundary conductance `B_g,if` | Existing validated `SpatialCoefficientSet` and P2-T02 | Supplied separately in SI `m^2`; R3/R4 do not derive them from diffusion coefficients, `STRD`, geometry, or source ordering. |
| Power, flux, fission source, `k`, and normalization | P2-T02 solve and caller request | Remain solver/request quantities; no source power array is converted into a runtime normalization. |
| Burnup, bundle material identity, and lifecycle binding | P2-T03, `BundleInventory`, and `SpatialRecomputeRequestV1` | The existing bundle and request bindings choose the table; R3 adds no node-map or material-map schema. |
| Lookup bracket and interpolation | `BurnupCoefficientTableV1.TryLookup` | Exact knots and existing linear interpolation only; invalid/out-of-range lookup fails closed. |
| Comparison quantities and acceptance thresholds | P2-T05, G4, and G5 | R3 creates no numeric threshold, tolerance, baseline, or golden result. |

## Runtime-facing projection

An R4 candidate pack may contain a compact representation, but after loading it
must produce the existing table semantics below. R3 does not prescribe JSON
member order or canonical byte encoding for that offline representation.

For each admitted `MaterialVariantId`, the projection contains exactly the
existing `BurnupCoefficientTableV1` identity and row concepts:

- non-empty `TableId`, `SchemaVersion = 1`, `DataVersion`,
  `MaterialVariantId`, and non-empty `UnitsProfileId`;
- path-free `SourceProvenance` and a non-null `Digest32` checksum identity;
- one or more strictly increasing, finite, nonnegative burnup knots in
  `J/kg_HM`;
- finite, nonnegative absorption, fission, nu-fission, and group-1-to-group-2
  downscatter coefficients in `m^-1`;
- canonical `ChiGroup1` in `[0,1]`, with `ChiGroup2` derived as
  `1 - ChiGroup1`, never independently stored or interpolated; and
- strictly positive finite `EnergyPerFissionJ` in `J` at every row.

The existing coefficient-domain invariants remain in force, including
absorption greater than or equal to fission in each group, matching zero
support for fission and nu-fission, and a finite positive implied yield when
fission is nonzero. A producer may not repair an invalid row after the fact.
The loader/converter must reject it.

The projection must not smuggle in any of the following fields because they
belong to separate contracts: `NodeKey`, `BundleId`, channel geometry,
topology or adjacency, flow direction, `V_i`, `T_g,ij`, `B_g,if`, flux, power,
`k`, source totals, normalization scale, convergence policies, or gate
tolerances. If a future pack needs one of these fields, the task must stop for
an approved contract decision instead of widening this boundary implicitly.

`SpatialRecomputeRequestV1` remains the binding point: it receives the
validated conductance source, one validated node volume per spatial node, the
tables, the existing inventory/material identities, explicit solve policies,
the exact data version, and the existing state/digest identities. The request
then performs the existing burnup lookup and records its table, material,
bracket, interpolation fraction, and checksum binding.

## Allowed transformations

The R4 implementation may perform only these classes of transformation:

1. Parse an offline input whose source identity, semantic field names, and
   units are explicit.
2. Copy or project already-authorized material values into the existing table
   fields, preserving their meaning and recording the source and transform
   identity.
3. Convert units only when both source and target units are explicit and the
   conversion is recorded in the R4 manifest. An unresolved Candu volume,
   conductance, energy, normalization, or topology mapping is a hard stop;
   R3 selects no such conversion.
4. Assemble rows in explicit burnup order and validate them with the existing
   Core contracts. Ordering may not acquire physical meaning from an input
   array position.
5. Compute the existing table checksum and pack identity using deterministic
   R4 serialization rules. The serialization is offline packaging detail and
   must remain semantically equivalent to the v1 table; it must not become a
   replacement public runtime schema.
6. Use the already approved exact-knot and linear interpolation behavior in
   `BurnupCoefficientTableV1.TryLookup` at runtime.

An offline reduced producer may use licensed external output as candidate
calibration evidence, subject to an explicit source and transformation record.
R3 does not select a fitting method, smoothing method, regression, surrogate
equation, or numerical constants. A later task must approve any such change if
it changes an equation or the meaning of a runtime quantity.

## Prohibited transformations and admission shortcuts

The following are not allowed under this boundary:

- deriving `T_g` or `B_g` from `D_g`, `STRD`, geometry, or an undocumented
  formula;
- inferring volume, energy per fission, topology, node identity, flow
  direction, or normalization from array position, case dimensions, or a
  source listing;
- copying exact Candu6.x2m output into a runtime table merely because the
  source is licensed or has matching production counts;
- filling a missing field with a default, repairing a negative/non-finite
  value, sorting an ambiguous source into physical meaning, or silently
  dropping an unknown field;
- extrapolating, clamping, smoothing, or replacing an invalid coefficient
  outside the existing table lookup contract;
- independently interpolating `ChiGroup2`, changing P2-T02 signs or equations,
  changing caller-owned convergence policies, or introducing a hidden
  normalization or tolerance; or
- retaining a DONJON5/DRAGON5 executable, raw artifact, private local path, or
  source conversion step as a runtime dependency.

## Valid domain and failure behavior

The valid projection domain is exactly the existing Core contract:

- every value is finite;
- burnup knots are nonnegative and strictly increasing;
- coefficient rows satisfy the existing nonnegative, absorption/fission,
  fission-support, chi, and positive-energy invariants;
- a lookup burnup is finite, nonnegative, and within the first-to-last table
  knot; and
- the requested material variant, data version, topology instance, node
  volumes, conductances, lifecycle identity, and digests bind through the
  existing request contract.

An out-of-range lookup, missing table, duplicate material variant, stale data
version, missing node volume, conductance mismatch, invalid source provenance,
checksum failure, non-finite value, or failed solver/convergence condition
fails closed. No partial table, partial spatial state, or last-known state is
presented as accepted output.

R3 chooses no lower or upper burnup endpoint, table density, node count,
production topology, solve cadence, target power, or convergence tolerance.
Those remain explicit data or caller policies owned by the existing contracts
and later tasks/gates.

## Provenance and evidence classes

Every candidate pack must carry a path-free, stable provenance identity that
can be checked against the source and transform manifest without exposing a
private local path. The manifest must distinguish at least:

| Evidence class | Meaning at R3/R4 | Runtime/gate status |
| --- | --- | --- |
| `synthetic` | Values intentionally created for bounded tests or a separately specified reduced producer | Candidate test evidence; not a golden approval. |
| `candidate_external` | Values or observations derived from the licensed external reference path | Offline candidate/calibration evidence only; not direct P2 authority. |
| `reduced_projection` | Values emitted by the approved reduced offline boundary and projected into the existing table contract | Runtime-compatible candidate until comparison and gate disposition. |
| `approved_golden` | A comparison result admitted by its owner gate | Not creatable by R3 or R4. |

The evidence class is manifest/provenance metadata, not a new field in
`BurnupCoefficientTableV1`. Exact external inputs and raw outputs remain
offline artifacts. The final runtime pack contains only the reduced projection
and the identities required by the existing contracts.

## R4, R5, and gate handoff

`P4-T06-R4` may now implement a deterministic offline converter/validator and
candidate compact pack, provided it:

1. consumes only a synthetic or explicitly admitted offline input;
2. emits values that reconstruct `BurnupCoefficientTableV1` v1 exactly in
   meaning and validates every row;
3. records source, transform, units, data version, material variants, table
   checksums, pack identity, evidence class, and path-free provenance;
4. keeps conductances, topology, node volumes, normalization, and policies in
   their existing separate contracts; and
5. demonstrates deterministic output and fail-closed invalid-data behavior.

R4 must stop if it needs to invent a missing equation, field mapping, unit,
topology, conductance, normalization, tolerance, or public schema. It must not
claim a golden baseline.

`P4-T06-R5` may compare the Core solver and reduced/synthetic path using the
P2-T05 observable identities and representative snapshots. The comparison
domain and numerical results remain candidate evidence until G4. G4 owns
spatial comparison quantities and tolerances; G5 owns burnup/refuelling
comparison quantities and tolerances. `P5-T10` remains separately gated by the
admitted reduced/synthetic sequence and G4.

## Non-authorizations and stop conditions

This boundary does not authorize:

- a change to any P2 equation, sign, unit, normalization, convergence rule,
  tolerance, public schema, or golden value;
- a new runtime dependency on reference tools, exact reference data, native
  libraries, or private artifacts;
- a new node-to-table mapping, conductance generator, topology generator,
  reduced-model equation, or calibration algorithm; or
- closing G4, G5, or the historical direct-admission blocker as if a source
  mapping had been proven.

Stop and request an additional approved task/specification on a source conflict,
unclear unit or normalization, missing provenance, non-reproducibility,
nondeterminism, invalid numerical value, incompatible public contract, or any
attempt to loosen a tolerance or replace a golden/reference value.

## Traceability

- [P4-T06-R2D disposition](../tasks/P4-T06-R2D.md)
- [P4-T06 follow-up chain](../tasks/ROUND-2026-08-16-P4-REFERENCE-CHAIN.md)
- [P2-T01 topology, indexing, units, and boundaries](topology-indexing-units-boundaries-v1.md)
- [P2-T02 two-group solver, normalization, and convergence](two-group-solver-normalization-convergence-v1.md)
- [P2-T03 refuelling and burnup transitions](refuelling-burnup-transitions-v1.md)
- [P2-T05 observables and validation methodology](observables-validation-methodology-v1.md)
- [P1-T08 CANDU literature digest](../reference/candu-literature-digest-v1.md)
