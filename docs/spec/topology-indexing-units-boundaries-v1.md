# Topology, indexing, units, and boundaries v1

**Status:** frozen P2-T01 implementation input. G2 is FORCED CLOSED / WAIVED;
this administrative disposition does not authorize a new topology, unit,
boundary, tolerance, or golden-data decision.

## Scope and non-goals

This document defines the engine-neutral identity and topology contract for the
first CANDU-style runtime model. It covers channel and bundle-slot indexing,
explicit flow direction, topology records, boundary labels, units for topology
and state quantities, validation invariants, and the limited mapping of the two
Phase 1 reference cases.

It does not select a neutronics equation, leakage coefficient, flux
normalization, solver iteration rule, burnup equation, kinetics model, RRS
influence map, or named station geometry. Those decisions belong to P2-T02,
P2-T03, P2-T04, or later approved specifications. This document is not a data
pack, save schema, C# API, or Unity asset contract.

## Model identity

The runtime configuration supports 380 independently addressable fuel channels,
each with 12 canonical bundle positions. A smaller synthetic topology is legal
for tests only when its complete topology record declares its channel and slot
counts explicitly.

### Channel and bundle identifiers

- `ChannelId` is an unsigned, zero-based integer in `[0, channel_count - 1]`.
  The production configuration has `channel_count = 380`.
- `BundlePosition` is an unsigned, zero-based physical slot coordinate in
  `[0, bundle_position_count - 1]`. The production configuration has
  `bundle_position_count = 12` for every channel.
- A refuelling-capable runtime channel has at least two positions. Its
  canonical physical endpoint `EndA` is position `0` and `EndB` is position
  `bundle_position_count - 1`; the names are abstract endpoint identities,
  not station labels.
- A bundle slot is the pair `(ChannelId, BundlePosition)`. A persistent bundle
  identity is a separate value and is not derived from either coordinate.
- Serialized identifiers are written as decimal integers. Display labels and
  upstream labels are opaque metadata and must not be used as array indices.
- An implementation may store flat arrays for performance, but array position
  is not evidence of channel identity, physical adjacency, or fuel flow.

The valid production slot count is therefore exactly `380 * 12 = 4,560`.
Configuration loading must reject duplicate IDs, missing IDs, out-of-range
positions, inconsistent per-channel slot counts, and any count that disagrees
with the topology record.

### Channel coordinates

Each channel record carries integer `coordinate_x` and `coordinate_y` values.
They are dimensionless lattice coordinates in an arbitrary core-local frame:
increasing `x` is east and increasing `y` is north; the origin has no station
or geographic meaning. Physical distances, if later required by a solver, are
separate finite SI metre values and are never inferred from integer coordinate
difference.

The topology table, not a rectangle formula, is authoritative. A production
pack must list every channel and every valid adjacency explicitly. This avoids
silently inventing a named-unit channel map and permits small synthetic cores.

## Explicit topology records

Each channel record contains:

```text
channel_id: ChannelId
coordinate_x: integer
coordinate_y: integer
flow_direction: EndAtoEndB | EndBtoEndA
inlet_position: BundlePosition
outlet_position: BundlePosition
neighbors: NeighborRecord[]
boundary_faces: BoundaryFaceRecord[]
```

Each `NeighborRecord` contains `target_channel_id`, `source_position`,
`target_position`, and `direction`. For a transverse channel-plane record,
`target_channel_id` is different from the source channel,
`source_position == target_position`, and `direction` is one of `North`,
`East`, `South`, or `West`. There is one such record for every bundle position
that has that transverse face. Diagonal adjacency is not part of v1.

For a within-channel record, `target_channel_id` equals the source channel,
the two positions are explicit adjacent coordinates, and `direction` is
`TowardEndA` or `TowardEndB`. `TowardEndA` requires
`target_position == source_position - 1`; `TowardEndB` requires
`target_position == source_position + 1`. These relations are topology
metadata, not a flow or transport equation.

Each `BoundaryFaceRecord` contains `face_id`, `position`, and
`classification`. A channel-plane `face_id` is one of `North`, `East`,
`South`, or `West`; an end face is `EndA` or `EndB`. `position` identifies the
affected bundle slot. Boundary records carry no leakage coefficient in v1.
The conceptual record fields are part of this contract even though P2-T01 does
not create a JSON schema.

Neighbor uniqueness is keyed by
`(source_channel_id, source_position, direction)`. Boundary uniqueness is
keyed by `(channel_id, position, face_id)`. A self-reference means that the
source and target slot tuples are identical:
`(source_channel_id, source_position) == (target_channel_id, target_position)`.
Repeated cardinal directions at different positions are therefore valid, while
duplicate composite keys are not.

Channel-plane and within-channel adjacency are undirected topology relations:
every neighbor record has one reciprocal record with the inverse direction and
swapped source/target fields. For a transverse `North` edge from A to B, the
target must have `coordinate_y = source.coordinate_y + 1`,
`coordinate_x = source.coordinate_x`, and a reciprocal `South` edge. `East`,
`South`, and `West` use the corresponding `(+1,0)`, `(0,-1)`, and `(-1,0)`
coordinate deltas. A boundary face and a neighbor relation are mutually
exclusive for the same face and position.

`boundary_faces` is explicit even when the channel appears rectangular. A
boundary record identifies the plane or bundle-end face and one of the labels
below. For every channel and every bundle position, each of the four cardinal
faces (`North`, `East`, `South`, `West`) has exactly one outcome: a transverse
neighbor relation with that composite key and its reciprocal, or one boundary
classification with the same `(channel_id, position, face_id)` key. In
addition, each channel has exactly one `EndA` boundary face at position `0`
and exactly one `EndB` boundary face at position
`bundle_position_count - 1`. There is no implicit exterior condition for a
missing face.

## Flow direction

`flow_direction` is a channel property, independent of `ChannelId` ordering and
the order of `neighbors` or `BundlePosition` values in serialized arrays. The
endpoint binding is normative:

- `EndAtoEndB` requires `inlet_position = 0` and
  `outlet_position = bundle_position_count - 1`.
- `EndBtoEndA` requires `inlet_position = bundle_position_count - 1` and
  `outlet_position = 0`.

The explicit endpoint fields define the physical shift orientation. A later
refuelling implementation may validate that each legal shift follows this
declared direction, but P2-T01 does not implement a shift.

The only legal flow-direction values are `EndAtoEndB` and `EndBtoEndA`. Missing,
combined, reordered, or unknown values fail closed. The topology loader must
not infer a direction from an alternating channel number, coordinate parity,
or array order.

## Boundary labels

Every external face needed by a spatial calculation is labeled explicitly as
one of:

- `Reflective` - the boundary classification used by the DONJON smoke case;
  its numerical current/flux equation is deferred to P2-T02.
- `Vacuum` - an exterior boundary classification whose numerical treatment is
  deferred to P2-T02.
- `SpecifiedLeakage` - a boundary classification carrying a later approved
  leakage parameter and unit; no parameter or equation is introduced here.

The labels are topology metadata, not convergence criteria. A loader must reject
an unknown label, a missing boundary classification for an exposed face, a
boundary parameter with a non-finite value, or a leakage parameter supplied
before its P2-T02 schema and unit are approved. Periodic boundaries and
implicit wraparound are not in v1.

The 12 bundle positions also have explicit `EndA` and `EndB` faces at positions
`0` and `11`. `inlet_position` and `outlet_position` identify the channel ends
for flow/refuelling semantics; they do not select a thermal-hydraulic direction
or a solver boundary equation. A one-layer static reference mesh is not a
refuelling-capable runtime channel and must be represented as reference-node
metadata, not by weakening this endpoint invariant.

## Unit and representation policy

Topology identifiers, counts, coordinates, enum values, and ordinals are
dimensionless discrete values. Quantities that are present in this contract use
the following representations:

| Quantity | Internal unit/representation | Rule |
| --- | --- | --- |
| Physical length or distance | metre (`m`) | Store SI `double`; finite and nonnegative where applicable. |
| Simulation time or residence duration | second (`s`) | Store SI `double`; discrete time steps remain explicit. |
| Thermal or deposited power | watt (`W`) | Store SI `double`; no `MW` storage alias. |
| Deposited energy | joule (`J`) | Store SI `double`; integration belongs to P2-T03. |
| Mass | kilogram (`kg`) | Store SI `double`; fuel-mass semantics belong to P2-T03. |
| Energy per mass, if introduced | joule per kilogram (`J/kg`) | P2-T03 must define the exact burnup field before use. |
| K-effective/multiplication factor | dimensionless (`1`) | Finite JSON number; preserve source lexeme alongside value in reference exports. |
| Integer coordinate, count, index, ordinal | dimensionless integer | No floating-point conversion or inferred physical unit. |

Neutron flux, group constants, cross sections, diffusion/leakage coefficients,
concentrations, temperatures, poison, and controller quantities are outside
the stored-quantity set of P2-T01. P2-T02/P2-T04 must define each unit before
any runtime field is added. No unit is invented for the DRAGON/DONJON source
convergence measures `DELS`, `DELT`, or `EPSOUT`; their source lexemes and
relationships are retained exactly as specified by P1-T04.

All serialized floating-point values must be finite. A non-finite, negative
where the field is nonnegative, unitless numeric substitution, or ambiguous
unit label is invalid data and must fail closed. Unity frame time is never a
simulation-time input.

## Reference-case cross-checks

The Phase 1 cases validate provenance and source-observable handling; they do
not supply the 380-channel production topology.

| Case | Topology evidence | Accepted mapping | Explicit limitation |
| --- | --- | --- | --- |
| `dragon5-lumpss-v5.1.0-source-era` | The case descriptor describes three depletion-oriented source records | Three ordered source observations, each dimensionless and identified only by explicit source ordinal | The P1 compact contract makes no burnup-step, time-step, or geometry assertion. The listing does not define a 380-channel/12-slot runtime map, so no channel, slot, or temporal index is inferred. |
| `donjon5-afa-180-310-type1-dual-v5.1.0` | The case descriptor identifies an upstream `6x6x1` reflective static-core setup | Case-level dimensions/boundary metadata and one dimensionless K-effective observation may be cross-checked | The compact export supplies no coordinates, neighbor records, or runtime indices. It is not a usable 36-node topology and does not authorize expanding its geometry or selecting solver leakage equations. |

The reference output contract remains authoritative for source value/lexeme
agreement, source order, convergence diagnostics, and publication status.
Reference observations must not be normalized, rounded, relabeled with an
invented unit, or used to backfill undocumented coordinates.

## Validation invariants

A runtime topology/configuration loader must reject the complete input when any
of the following is true:

1. The channel IDs are not exactly the declared set, are duplicated, or exceed
   the declared count.
2. A coordinate is non-integer, duplicated, or inconsistent with an explicit
   neighbor direction.
3. A bundle position is outside the declared range, a channel has the wrong
   number of positions, or the endpoint/flow-direction binding is absent or
   inconsistent with `EndA = 0` and `EndB = bundle_position_count - 1`.
4. A flow direction, neighbor direction, boundary label, or within-channel
   reference is missing, unknown, duplicated under its composite key, or
   inferred from ordering.
5. A neighbor is missing a target/source slot, is self-referential, is out of
   range, violates its direction delta, lacks its reciprocal relation, or
   conflicts with a boundary face. Every channel/position has exactly one
   cardinal outcome for each of the four cardinal faces, and exactly one
   `EndA@0` and one `EndB@(bundle_position_count - 1)` outcome. For every
   channel and each `p` in `[0, bundle_position_count - 2]`, exactly one
   reciprocal within-channel pair connects `p` and `p + 1`; no other
   within-channel pair is legal.
6. A physical dimension is non-finite or negative where this contract requires
   nonnegative data, or a quantity is supplied in an unapproved unit.

The loader returns a structured invalid-topology diagnostic identifying the
first deterministic failure category and record location. It does not repair,
sort into meaning, wrap, clamp, or silently continue with a partial topology.

The runtime topology loader does not consume P1 compact exports, raw manifests,
source lexemes, or publication-status fields. Those are checked by the offline
reference parser/workflow and are not runtime topology invariants.

### Offline reference cross-check invariants

The separate reference-output validator must reject a reference observation that
is non-finite, has a nonmatching source lexeme, changes the prescribed source
order, or changes the P1 publication/retention status. These checks remain under
the P1 reference-output contract and must never be implemented by making the
runtime topology loader depend on the compact export format.

## Deferred decisions

The following are intentionally left for later approved specifications:

- the two-group equations, leakage stencil coefficients, and boundary numerical
  equations (`P2-T02`);
- flux/source normalization and convergence criteria (`P2-T02`);
- bundle movement, burnup units/integration, and interpolation (`P2-T03`);
- kinetics, xenon, RRS, feedback, and any control-state units (`P2-T04`);
- complete observable coverage and provisional tolerances (`P2-T05`);
- the approval of a runtime data pack or golden comparison baseline (`G2` and
  later gates); and
- any named station geometry or proprietary channel map.

## Definition of done for P2-T01

- The channel, bundle-slot, coordinate, flow-direction, slot-to-slot neighbor,
  reciprocal-edge, and boundary-face identities are explicit and do not depend
  on array order.
- Production counts are stated as 380 channels, 12 positions, and 4,560 slots;
  synthetic cases can declare smaller explicit counts.
- Units used by this contract are documented, while solver-specific units are
  explicitly deferred rather than guessed.
- The two Phase 1 cases are cross-checked without inferring undocumented
  geometry, normalization, or physics.
- Invalid topology, non-finite data, ambiguous direction, and incompatible
  reference evidence fail closed.
- No runtime code, schema migration, equation, tolerance, or private artifact
  is added by this task.

Source: the retained P2-T01 specification and
[`reference-output-formats-v1.md`](reference-output-formats-v1.md).
