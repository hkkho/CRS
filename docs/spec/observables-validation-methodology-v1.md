# Observables and validation methodology v1

## Status and scope

This document is the engine-neutral P2-T05 contract for observable identity,
units, sampling, reference coverage, deterministic comparison, validation tiers,
and provisional tolerance profiles. It defines how later implementation tasks
must report evidence for the P2-T01 through P2-T04 contracts.

It does not approve production tolerances, create a golden baseline, add runtime
code, alter equations, or publish/redistribute DRAGON5/DONJON5 inputs or raw
listings. Numerical tolerance approval remains a later owner-gate decision:
the G2 forced closure approves no tolerance; G4 is the earliest approval gate
for spatial quantities, followed by G5 for burnup, G6 for RRS, G7A for
kinetics/Xe, and G7B when optional feedback is enabled. A provisional result
must never be represented as an approved release result.

All examples and numeric values in this document are synthetic methodology
examples. They are not copied from private/raw reference artifacts or golden
data.

## Normative terms and units

The words **must**, **must not**, and **may** are normative. Every observable has
an authoritative quantity unit, scope, state/time binding, comparison rule, and
evidence source. Missing units, missing state identity, non-finite values,
ambiguous reference provenance, or an absent required tolerance profile fails the
validation record closed.

| Quantity family | Authoritative unit | Typical comparison |
| --- | --- | --- |
| `SimulationTime`, event time, cadence | `s` | exact serialized value and event order |
| channel/position/count/index | dimensionless integer | exact equality |
| bundle identity/command/event ID | canonical bytes | exact byte equality |
| flux `phi_g` | `m^-2 s^-1` | quantity-specific profile |
| power `P_i`, `P_total` | `W` | absolute/relative profile |
| multiplication `k` | dimensionless (`1`) | scalar absolute/relative profile |
| reactivity `rho` | dimensionless (`1`) | scalar absolute/relative profile |
| cross sections/coefficient overlays | `m^-1` | one quantity-specific profile per catalog ID |
| burnup `B` | `J/kg_HM` | one quantity-specific profile per catalog ID |
| cumulative energy | `J` | conservation/profile comparison |
| I/Xe atom inventory | atoms | nonnegative/conservation/profile |
| I/Xe number density | `m^-3` | derived-unit/profile comparison |
| temperature | `K` | scalar profile |
| purity mass fraction | `kg/kg` | range/exact or profile |
| poison concentration | `kg/m^3` | scalar profile |
| precursor/amplitude/actuator/fill fractions | dimensionless (`1`) | range/exact/profile |

Display units such as `MW`, `MWd/t_HM`, pcm, percent, ppm, or degrees Celsius
must be converted at the boundary and never compared as if they were
authoritative units.

## Observable record contract

Every emitted `ObservableRecord` contains the following fields. The record
schema is closed: an omitted required field, an unknown enum, an extra
comparison component, or a duplicate identity fails closed.

| Field | Requirement |
| --- | --- |
| `ObservableId` | deterministic lowercase UUID derived from the identity key below; never random |
| `QuantityId` | versioned quantity name from the catalog below |
| `Scope` | structured scope union and canonical key, not a free-form label |
| `SimulationTime` | exact finite time in `s` for `Runtime`; explicit `NotApplicable` tag for `Parser`/`Provenance` |
| `StateBinding` | complete version/digest tuple below |
| `InputDigest` | SHA-256 digest of topology/data-pack/table/map/input identity |
| `Value` | discriminated `Available` finite scalar/vector/event/bytes/structured payload or `Unavailable` failure payload |
| `ComparisonRuleId` | exact rule, norm, alignment, and acceptance mode |
| `ToleranceProfileId` | one quantity/unit profile or explicit `Exact` rule |
| `ReferenceId` | source identity; coverage and artifact availability are separate fields |
| `CoverageClass` | `Direct`, `Indirect`, `Synthetic`, or `NotCovered` |
| `ArtifactAvailability` | `CommittedSynthetic`, `ExternalApproved`, `PrivateExternal`, or `Absent` |
| `EvidenceApproval` | `Candidate`, `NotApproved`, or `Approved` |
| `ValidationDomain` | `Parser`, `Runtime`, or `Provenance` |
| `Status` | `Pass`, `Fail`, `Deferred`, `NotCovered`, or `NotApplicable` |
| `EvidencePath` | committed synthetic path or external artifact label; never a private host path in a committed file |
| `OrderKey` | canonical tuple for repeated records |

`Scope` is a closed tagged union: `Global`; `Entity` with a declared kind
(`Channel`, `BundleSlot`, `Bundle`, `Node`, `Edge`, `Face`, `Actuator`,
`Branch`, `Conductance`, `Controller`, or `Queue`); `Vector` with a declared
kind (`Node`, `NodeGroup`, `EdgeGroup`, `FaceGroup`, `Group`, `Region`,
`RegionPartition`, or `Bundle`); `SourceMap`; `Event`; `Interval`;
`Solve`; `SolvePair`; `Snapshot`; `Run`; `RunPair`; `Fixture`; `Failure`;
`Parser`; `Provenance`; `Lookup`; or `TimeSeries`. A catalog scope such as
`node/group` is a display token whose exact representation is selected by the
closed `(DisplayToken, ValueKind)` mapping below; it is never an inferred
free-form scope value. Its `ScopeKey` contains the declared IDs, canonical node
membership, event owner/UUID/sequence, table/lookup identity, or paired state
keys required by that kind. Scope kinds and keys are not inferred from array
order.

`StateBinding` is a structured tuple containing `CoreStateVersion`,
`SpatialStateVersion`/`SpatialSolveId` when applicable, `PowerSnapshotId` and
`PowerSnapshotVersion` when applicable, `KineticStepIndex` when applicable,
`NuclideStateVersion` when applicable, `TopologyVersion`, `DataPackVersion`,
`CoefficientDigest`, and `SnapshotDigest`. A `Runtime` record must supply its
applicable runtime values. `Parser` and `Provenance` records encode every
runtime-only member as the explicit `NotApplicable` tag and never invent a time
or state version. A `SolvePair` also carries both ordered endpoint bindings. A
field that is not applicable is represented by an explicit `NotApplicable` tag,
never by an omitted field.

### Version binding and serialization lifecycle

Runtime `StateBinding` consumes the shared `VersionLifecycleV1` contract from
P2-T03 and P2-T04. The counters are owner-scoped and their applicability is
determined by the record scope, never by array position or by whether a value
happens to be zero:

| Record scope | Required version members |
| --- | --- |
| Accepted spatial solve or power snapshot | `CoreStateVersion`, valid `SpatialStateVersion`/`SpatialSolveId`, valid `PowerSnapshotVersion`/`PowerSnapshotId` when a power snapshot exists, applicable `NuclideStateVersion` or the aggregate `SnapshotDigest`, topology/data-pack versions, coefficient digest, and snapshot digest |
| Bundle power/history or bundle I/Xe record | the owning bundle's `CoreStateVersion` and `NuclideStateVersion`; valid spatial/power versions and IDs only when the record consumes an accepted solve/snapshot |
| Committed burnup/refuelling/nuclide event | the post-commit `CoreStateVersion`, the applicable post-event bundle nuclide versions, and `NotApplicable` spatial/power members when the event invalidates those bindings |
| Invalid, stale, rejected, parser, or provenance binding | explicit `NotApplicable` for every unusable or non-runtime member; no last-known version is presented as a usable value |

For an aggregate record that has no single owning bundle, the ordered inventory
and nuclide state are bound by its canonical scope and `SnapshotDigest`, while
`NuclideStateVersion` is `NotApplicable`. For a bundle-scoped record it is the
exact version on that persistent bundle identity. `CoreStateVersion` identifies
the committed P2-T03 epoch; `SpatialStateVersion` and
`PowerSnapshotVersion` identify the accepted solve/snapshot epochs and advance
only in their specified atomic acceptance. Invalidation preserves the audit
counter internally but changes the serialized binding member to
`NotApplicable`. A failed transaction restores the complete pre-transaction
binding and counter tuple byte-for-byte.

The serializable `VersionLifecycleV1` state includes the immutable initial
counters, current counters, binding statuses, accepted IDs, and all binding
digests. Its canonical field order is the P2-T03 order: lifecycle identity and
schema, initial global counters, current global counters, sorted per-bundle
nuclide versions, binding statuses, accepted IDs, and state/coefficient/
topology/data-pack/snapshot digests. Counters use the fixed `UInt64` encoding
defined below; applicability uses the closed `NotApplicable` tag. Save/load
and replay must reject missing, duplicate, reordered, stale, or incoherent
version records, including a before/after tuple that does not match the one
authorized commit event. `state.version_binding` and
`serialization.round_trip` evidence therefore checks lifecycle transitions,
not merely the presence of field names. This amendment adds no numerical
tolerance, comparison relaxation, or invented reference value.

Every event-boundary runtime snapshot that contains a delayed RRS, zone, or
adjuster owner also contains its complete `QueueStateV1`. Round-trip and replay
evidence compares queue owner/ID, immutable and current allocator values,
motion time, requested/available/physical projections, pending-record order,
the applied-source-event and allocated-command-ID registries, command and queue
digests, and before/after queue event bodies exactly. A save is not admitted
inside an enqueue or motion/consume transaction; interrupted
scratch, including a frozen due batch, is discarded and replay begins from the
last complete boundary. Missing queue state, sequence rewind/reuse, command
migration, or a newly due command applied over a preceding interval fails
closed.

`Value` is a tagged union. `Available` carries a finite scalar, integer,
boolean, canonical vector, event, bytes, or structured payload in the catalog
unit. `Unavailable` carries only a
closed reason such as `Nonconverged`, `InvalidState`, `MissingReference`, or
`DeferredProfile`; it carries no fabricated numeric value. A failed spatial
solve therefore emits `Status=Fail, Value=Unavailable{Nonconverged}` and the
diagnostic observables, never the last iterate as a usable value.

The identity key is the exact tuple `(ValidationDomain, QuantityId, ScopeKind,
ScopeKey, SimulationTimeOrNotApplicable, StateBinding identity, component key,
event sequence)`. Encode
it as the canonical semantic bytes defined below, hash it with SHA-256, take
the first 16 bytes, set the UUID version nibble to `8` and the RFC-4122 variant
bits, and render the result as lowercase `8-4-4-4-12` ASCII. This is the only
`ObservableId` derivation; a collision or duplicate identity fails closed.

For an aggregate `ValueKind=Vector` record, the identity tuple's record-level
`component key` is the explicit `NotApplicable` component key. The concrete
component keys are the ordered, finite keys inside the vector payload and are
not additional record identities. A scalar or structured record that measures
one component uses that component's key in the identity tuple. This distinction
prevents a vector's repeated payload keys from being silently conflated with a
second record-level key.

The total order uses this fixed rank table: `Global=0`, `Entity=1`,
`Vector=2`, `SourceMap=3`, `Event=4`, `Interval=5`, `Solve=6`, `SolvePair=7`,
`Snapshot=8`, `Run=9`, `RunPair=10`, `Fixture=11`, `Failure=12`, `Parser=13`,
`Provenance=14`, `Lookup=15`, and `TimeSeries=16`. The total order is the tuple
`(ValidationDomainRank, SimulationTimeOrNotApplicable,
StateBinding.CoreStateVersionOrNotApplicable, EventRank, Sequence, EventIdBytes,
ScopeKindRank, ScopeKey, QuantityId, component key, ObservableId)`. `EventRank`
is the explicit P2-T03/P2-T04 event rank; non-event records carry an explicit
`NotApplicable` tag that sorts before applicable event ranks. `Sequence` and
`EventIdBytes` are always present as a tagged applicable/not-applicable pair.
Strings and UUIDs compare by canonical
bytes; numeric fields in this ordering compare numerically, not by their
little-endian serialized bytes. `OrderKey` is exactly this structured tuple with
the fixed tags and numeric fields; it may not be replaced by a digest or
alternate text form. Its canonical bytes are encoded once by the serialization
contract below and must not contain a wall-clock, thread, hash-table, or
Unity-frame value. An observable with `Status=Deferred` or `NotCovered` is not a
pass.

For an aggregate vector, `OrderKey.ComponentKey` is the same explicit
`NotApplicable` key used by the identity tuple; only the vector payload's
internal component-key sequence contains concrete member keys.

### Scope and sampling rules

The scope must identify the exact object measured. A global total must declare
the contributing canonical node set; a region must declare its disjoint node
membership; a bundle observable must carry `BundleId`, location, and
`NodeVolume`; an event observable must carry command/event identity and sequence.
No region or bundle mapping is inferred from array order.

The sample time must be a valid state boundary. For a positive-duration interval,
the record must bind the exact P2-T02 spatial result, P2-T03 power snapshot, and
P2-T04 kinetic/nuclide state. A record captured after a state transition but
bound to the previous digest is invalid.

For vector values, components use the canonical P2-T01 node/group/order keys.
For event sequences, records use the explicit event rank and sequence/UUID-byte
ordering from P2-T03/P2-T04. No unordered collection, wall-clock timestamp,
random value, or Unity frame count can affect an observable.

### Canonical component keys and norms

Every vector profile selects exactly one `Norm` ordinal from the byte-code table.
The v1 component orders are:

- spatial flux, coefficient, fission-source, and absorption-overlay vectors:
  ascending
  `(ChannelId, BundlePosition, Group)`;
- `spatial.source_shape_change_inf`: ascending node key
  `(ChannelId, BundlePosition)`;
- node power vectors: ascending `(ChannelId, BundlePosition)`;
- regional vectors: ascending `RegionId`, then ascending member node key;
- bundle vectors and histories: ascending `(ChannelId, BundlePosition,
  BundleIdBytes)` exactly as P2-T03 requires; `BundleId` is opaque and is never
  case-folded or otherwise normalized;
- before/after normalization vectors: the fixed two keys `before`, `after`;
- I/Xe term vectors: `IProduction`, `IDecay`, `XeProduction`, `XeFromIDecay`,
  `XeDecay`, `XeAbsorption`, in that order;
- precursor/group vectors: ascending serialized group index; and
- any time series: ascending numeric `SimulationTime`, then the state/event
  tie-break tuple in the total record order.

Single-component records use `Scalar`; component vectors in this contract use
`L_inf` unless their crosswalk row explicitly names `L1` or `Weighted`; sum,
bound, and conservation rows use `Invariant`. A missing, duplicate, or
reordered component key fails before the selected norm is evaluated. The
component order is part of the profile digest, not an implementation choice.

### Scope-token and value-kind crosswalk rules

The scope strings in the quantity crosswalk are display tokens for these exact
representations; they are not additional enum values. The `(DisplayToken,
ValueKind)` pair is closed and one-to-one. Aliases in one cell have identical
representations, and `NonVector` means `Scalar`, `Structured`, `Bytes`, or
`Integer` as assigned by the explicit quantity partition immediately below.

| Display token | NonVector canonical scope/key and component | `Vector` canonical scope/key and component |
| --- | --- | --- |
| `global` | `Global(NodeSet)`, `NotApplicable` | `Global(NodeSet)`, `NotApplicable` |
| `global/solve`, `solve` | `Solve(SolveId, SpatialStateVersion, GroupIndexOrNA)`, `NotApplicable` | `Solve(SolveId, SpatialStateVersion, GroupIndexOrNA)`, `NotApplicable` (payload `BeforeAfter`) |
| `global/time`, `time` | `TimeSeries(OwnerKey, Time)`, `Time` | not used |
| `global/group` | `Vector.Group(GroupSet={GroupIndex})`, `Group` | `Vector.Group(GroupSet)`, `NotApplicable` (payload `Group`) |
| `group solve` | `Solve(SolveId, SpatialStateVersion, GroupIndex)`, `Group` | not used |
| `channel` | `Entity.Channel(ChannelId)`, `NotApplicable` | not used |
| `channel/position` | `Entity.BundleSlot(ChannelId, BundlePosition)`, `NotApplicable` | not used |
| `node` | `Entity.Node(ChannelId, BundlePosition)`, `NotApplicable` | `Vector.Node(NodeSet)`, `NotApplicable` (payload `Node`) |
| `node/group` | `Entity.Node(ChannelId, BundlePosition)`, `Group` | `Vector.NodeGroup(NodeGroupSet)`, `NotApplicable` (payload `NodeGroup`) |
| `node/edge` | `Entity.Edge(NodeKey, NodeKey)`, `NotApplicable` | not used |
| `edge/group` | `Entity.Edge(NodeKey, NodeKey)`, `Group` | `Vector.EdgeGroup(EdgeGroupSet)`, `NotApplicable` (payload `EdgeGroup`) |
| `node/face` | `Entity.Face(ChannelId, BundlePosition, FaceId)`, `NotApplicable` | not used |
| `node/face/group` | `Entity.Face(ChannelId, BundlePosition, FaceId)`, `Group` | `Vector.FaceGroup(FaceGroupSet)`, `NotApplicable` (payload `FaceGroup`) |
| `edge/face/group` | `Entity.Conductance(ConductanceKind, InteriorEdgeKeyOrNA, BoundaryFaceKeyOrNA)`, `Group` | not used |
| `region` | `Vector.Region(RegionId, MemberNodeSet)`, `NotApplicable` | `Vector.Region(RegionSet)`, `Region` |
| `region partition` | `Vector.RegionPartition(OrderedRegionSet, CompleteMemberNodeSets)`, `NotApplicable` | `Vector.RegionPartition(OrderedRegionSet, CompleteMemberNodeSets)`, `NotApplicable` (payload `Region`) |
| `region/time` | `TimeSeries(Owner=Region(RegionId), Time)`, `Time` | not used |
| `bundle` | `Entity.Bundle(BundleId, ChannelId, BundlePosition, NodeVolume)`, `NotApplicable` | `Vector.Bundle(BundleSet)`, `NotApplicable` (payload `Bundle`) |
| `bundle/node` | `Entity.Bundle(...)`, `Node` | not used |
| `bundle/event` | `Event(..., Owner=Bundle(BundleId), ...)`, `NotApplicable` | not used |
| `bundle/history` | `TimeSeries(Owner=Bundle(BundleId), Time)`, `Time` | not used |
| `bundle/interval` | `Interval(OwnerKind=Bundle, OwnerKey, Start, End, StateVersion)`, `NotApplicable` | `Interval(OwnerKind=Bundle, OwnerKey, Start, End, StateVersion)`, `NotApplicable` (payload `Term`) |
| `bundle/lookup`, `node/group/table` | `Lookup(OwnerKind, OwnerKey, TableId, Bracket)`, `NotApplicable` | not used |
| `event` | `Event(..., Owner=NotApplicable, ...)`, `NotApplicable` | not used |
| `event/channel` | `Event(..., Owner=Channel(ChannelId), ...)`, `NotApplicable` | not used |
| `event/snapshot` | `Event(..., Owner=Snapshot(SnapshotId), ...)`, `NotApplicable` | not used |
| `event/time` | `Event(..., OwnerKeyOrNA, ...)`, `Time` | not used |
| `actuator/event` | `Event(..., Owner=Actuator(ActuatorId), ...)`, `NotApplicable` | not used |
| `actuator/time` | `TimeSeries(Owner=Actuator(ActuatorId), Time)`, `Time` | not used |
| `branch` | `Entity.Branch(BranchId)`, `NotApplicable` | not used |
| `solve pair` | `SolvePair(StartSolveKey, EndSolveKey)`, `NotApplicable` | `SolvePair(StartSolveKey, EndSolveKey)`, `NotApplicable` (payload `Node`) |
| `snapshot`, `run`, `run pair`, `fixture` | corresponding tagged key, `NotApplicable` | not used |
| `failed run`, `failed solve` | `Failure(FailureKey)`, `NotApplicable` | not used |
| `source/map`, `parser`, `provenance` | corresponding tagged source/artifact key, `NotApplicable` | not used |

The value-kind assignment is explicit per `QuantityId`; it is not inferred
from the unit, rule token, or scope. The following five non-empty sets are a
disjoint partition of all 135 crosswalk IDs, and every ID occurs exactly once;
the legal `Bool` set is currently empty and reserved for a future pure-boolean
quantity:

| ValueKind | PayloadSchemaId | QuantityIds |
| --- | --- | --- |
| `Structured` | `InvariantV1` unless a named schema is shown | `inventory.bundle_id_uniqueness`, `spatial.power_sum_invariant`, `spatial.region_power_sum_invariant`, `spatial.coefficient_invariant_sigma_a_ge_sigma_f`, `spatial.coefficient_invariant_chi_sum`, `spatial.coefficient_invariant_fission_support`, `spatial.coefficient_invariant_fission_product`, `burnup.energy_accounting`, `burnup.interval_energy_accounting`, `burnup.monotonicity`, `kinetics.amplitude_flux_invariant`, `kinetics.amplitude_node_power_invariant`, `kinetics.amplitude_total_power_invariant`, `xenon.atom_balance`, `feedback.poison_mass_accounting`; `inventory.location_legality`=`LocationV1`; `refuel.position_mapping`=`MappingV1`; `refuel.atomicity`=`AtomicityV1`; `refuel.history`=`HistoryV1`; `rrs.actuator_command_event`, `rrs.actuator_state_transition`=`EventV1`; `rrs.saturation`=`SaturationV1`; `feedback.disabled_zero`=`DisabledZeroV1`; `feedback.rrs_map_ownership`, `feedback.optional_map_ownership`=`OwnershipV1`; `feedback.queue_cadence`=`QueueStateV1`; `parser.dragon.kinf`, `parser.donjon.keff`=`ParserV1`; `parser.manifest.file_digest`=`ManifestV1` |
| `Vector` | `VectorV1` | `spatial.flux`, `spatial.source_shape_change_inf`, `spatial.normalization_source_totals`, `spatial.normalization_power_totals`, `xenon.production_decay_absorption`, `xenon.absorption_overlay`, `feedback.rrs_overlay`, `feedback.optional_overlay`, `feedback.zone_overlay`, `feedback.adjuster_overlay`, `feedback.poison_overlay` |
| `Integer` | `IntegerV1` | `topology.channel_count`, `topology.bundle_position_count`, `topology.slot_count`, `state.core_version`, `spatial.iteration_count`, `kinetics.step_index`, `feedback.queue_order`, `diagnostics.clamp_count`, `diagnostics.forbidden_clamp` |
| `Bytes` | `BytesV1` | `topology.channel_record`, `topology.adjacency`, `topology.boundary_face`, `topology.flow_binding`, `inventory.slot_occupancy`, `state.version_binding`, `state.snapshot_digest`, `spatial.conductance_binding`, `spatial.coefficient_identity`, `spatial.interpolation_bracket`, `spatial.convergence`, `spatial.inner_solve`, `burnup.coefficient_bracket`, `refuel.scheme`, `refuel.direction_binding`, `kinetics.step`, `kinetics.integration_policy`, `xenon.volume_binding`, `feedback.zone_grouping`, `feedback.branch_schema`, `determinism.output_digest`, `determinism.repeat_equal`, `determinism.shuffle_invariant`, `serialization.round_trip`, `diagnostics.first_failure`, `diagnostics.nonconvergence` |
| `Scalar` | `ScalarV1` | `inventory.residence_time`, `spatial.k`, `spatial.power`, `spatial.total_power`, `spatial.region_power`, `spatial.region_power_fraction`, `spatial.node_volume`, `spatial.sigma_a`, `spatial.sigma_f`, `spatial.nu_sigma_f`, `spatial.sigma_s_1_to_2`, `spatial.chi`, `spatial.energy_per_fission`, `spatial.fission_source`, `spatial.edge_conductance`, `spatial.boundary_conductance`, `spatial.implied_neutron_yield`, `spatial.residual_absolute_inf`, `spatial.residual_relative_inf`, `spatial.delta_k_abs`, `spatial.delta_k_rel`, `spatial.power_balance_rel`, `spatial.normalization_scale`, `spatial.interpolation_alpha`, `spatial.inner_residual_absolute_inf`, `spatial.inner_residual_relative_inf`, `burnup.current`, `burnup.energy`, `burnup.interval_power`, `burnup.interval_energy`, `burnup.mass`, `burnup.coefficient_alpha`, `kinetics.amplitude`, `kinetics.reference_power`, `kinetics.actual_flux`, `kinetics.actual_node_power`, `kinetics.actual_total_power`, `kinetics.fission_rate_density`, `kinetics.precursor`, `kinetics.reactivity`, `kinetics.step_size`, `xenon.I135_inventory`, `xenon.Xe135_inventory`, `xenon.node_volume`, `xenon.I135_number_density`, `xenon.Xe135_number_density`, `rrs.total_power_error`, `rrs.tilt_error`, `rrs.total_integral_error`, `rrs.tilt_integral`, `rrs.actuator_command`, `rrs.actuator_state`, `feedback.poison_mass`, `feedback.poison_concentration`, `feedback.moderator_volume`, `feedback.temperature_state`, `feedback.purity_state`, `feedback.zone_fill_state`, `feedback.adjuster_state`, `feedback.queue_cadence_time` |

`Bool` remains a legal payload kind for a future pure-boolean quantity, but no
current catalog ID uses it: the listed atomicity, saturation, support, and
uniqueness records carry structured evidence rather than an uninformative bit.
The `ValueKind` and `PayloadSchemaId` in this partition are the values encoded
in the tolerance profile and observable record; no class-based fallback is
permitted.

## Observable catalog

### Topology and state integrity

| QuantityId | Scope | Observable |
| --- | --- | --- |
| `topology.channel_count` | global | exactly 380 declared channels |
| `topology.bundle_position_count` | global | exactly 12 positions per production channel |
| `topology.slot_count` | global | exactly 4,560 channel-position slots |
| `topology.channel_record` | channel | exact channel ID/coordinates/declared slots |
| `topology.adjacency` | node/edge | exact canonical neighbor key set and direction |
| `topology.boundary_face` | node/face | exact boundary label and endpoint binding |
| `topology.flow_binding` | channel | explicit flow and endpoint binding |
| `inventory.slot_occupancy` | channel/position | exactly one live bundle in each production slot |
| `inventory.bundle_id_uniqueness` | global | no duplicate live/history identity |
| `inventory.location_legality` | bundle | valid channel/position and movement history |
| `inventory.residence_time` | bundle | `current_time - InsertedAt` in `s` |
| `state.version_binding` | event/snapshot | exact state/data/topology digest relationship |
| `state.core_version` | event/snapshot | exact `CoreStateVersion` transition |
| `state.snapshot_digest` | snapshot | exact bound snapshot and coefficient digest |

Counts, IDs, locations, flow enums, and event movement are exact comparisons.
The three production counts are model contract values, not reference-derived
measurements.

### Spatial solver and normalization

| QuantityId | Scope | Observable |
| --- | --- | --- |
| `spatial.k` | global/solve | effective multiplication factor |
| `spatial.flux` | node/group | normalized group flux |
| `spatial.power` | node | P2-T02 integrated fission power |
| `spatial.total_power` | global | sum of node powers in `W` |
| `spatial.region_power` | region | regional fission power in `W` |
| `spatial.region_power_fraction` | region | regional power divided by total |
| `spatial.power_sum_invariant` | global | exact node-power sum invariant |
| `spatial.region_power_sum_invariant` | region partition | exact regional-power sum invariant |
| `spatial.node_volume` | node | P2-T02 control volume in `m^3` |
| `spatial.sigma_a` | node/group | absorption coefficient in `m^-1` |
| `spatial.sigma_f` | node/group | fission coefficient in `m^-1` |
| `spatial.nu_sigma_f` | node/group | yield-weighted fission coefficient in `m^-1` |
| `spatial.sigma_s_1_to_2` | node | downscatter coefficient in `m^-1` |
| `spatial.chi` | node/group | fission-spectrum fraction in `1` |
| `spatial.energy_per_fission` | node | fission energy in `J` |
| `spatial.fission_source` | node | fission source rate density in `m^-3 s^-1` |
| `spatial.edge_conductance` | edge/group | finite-volume conductance in `m^2` |
| `spatial.boundary_conductance` | node/face/group | boundary conductance in `m^2` |
| `spatial.coefficient_invariant_sigma_a_ge_sigma_f` | node/group | exact lower-bound invariant |
| `spatial.coefficient_invariant_chi_sum` | node | exact sum-to-one invariant |
| `spatial.coefficient_invariant_fission_support` | node/group | exact `Sigma_f=0` iff `nuSigma_f=0` invariant |
| `spatial.implied_neutron_yield` | node/group | finite positive implied yield in `1` when fission is nonzero; `NotApplicable` for zero/zero fission fields |
| `spatial.coefficient_invariant_fission_product` | node/group | conditional fission/yield product invariant; `NotApplicable` when `nu_g` is absent |
| `spatial.conductance_binding` | edge/face/group | exact topology/conductance binding |
| `spatial.residual_absolute_inf` | solve | absolute residual infinity metric |
| `spatial.residual_relative_inf` | solve | zero-safe relative residual metric |
| `spatial.delta_k_abs` | solve | `abs(k^(n+1)-k^(n))` |
| `spatial.delta_k_rel` | solve | dimensionless relative `k` change |
| `spatial.source_shape_change_inf` | solve pair | normalized source-shape change |
| `spatial.power_balance_rel` | solve | post-normalization power balance |
| `spatial.normalization_scale` | solve | exact power-normalization scale `alpha` in `1` |
| `spatial.normalization_source_totals` | solve | before/after source totals in `s^-1` |
| `spatial.normalization_power_totals` | solve | before/after power totals in `W` |
| `spatial.coefficient_identity` | node/group/table | exact table, unit, interpolation, and digest identity |
| `spatial.interpolation_bracket` | node/group/table | exact table/bracket and out-of-range result |
| `spatial.interpolation_alpha` | node/group/table | interpolation fraction in `1` |
| `spatial.convergence` | solve | convergence flag/reason/iterations |
| `spatial.iteration_count` | solve | exact outer iteration count |
| `spatial.inner_solve` | group solve | exact method, status, and iteration count |
| `spatial.inner_residual_absolute_inf` | group solve | absolute inner residual in `m^-3 s^-1` |
| `spatial.inner_residual_relative_inf` | group solve | zero-safe relative inner residual in `1` |

The fission-support invariant is always evaluated. If both `Sigma_f` and
`nuSigma_f` are zero it passes and `spatial.implied_neutron_yield` is
`NotApplicable`; a zero/nonzero mismatch is `Fail`. When fission is nonzero,
both coefficients and the implied yield must be finite and strictly positive or
the record is `Fail`. The product identity is evaluated only when `nu_g` is
supplied; otherwise its status is `NotApplicable`.

`spatial.k` and power/flux values use the exact P2-T02 normalization and
residual contracts. A nonconverged or failed spatial result has no usable
available value; it records `Status=Fail` with an `Unavailable` reason, not the
last iterate as a pass. `spatial.delta_k_abs` and `spatial.delta_k_rel` bind the
same ordered consecutive finite `k` values from one solve iteration; the
relative form is `delta_k_abs / max(abs(k_next), abs(k_current))` and is
`NotApplicable` only when the solve has no consecutive pair. Their thresholds
are the P2-T02 `k_absolute_tolerance` and `k_relative_tolerance` policy fields;
P2-T05 supplies no numeric threshold.

### Burnup and refuelling

| QuantityId | Scope | Observable |
| --- | --- | --- |
| `burnup.current` | bundle | derived `J/kg_HM` burnup |
| `burnup.energy` | bundle | cumulative fission energy in `J` |
| `burnup.energy_accounting` | bundle | `B = InitialBurnup + E/m_HM` |
| `burnup.interval_power` | bundle/interval | actual snapshot power in `W` |
| `burnup.interval_energy` | bundle/interval | `P * Delta_t` in `J` |
| `burnup.interval_energy_accounting` | bundle/interval | exact `P * Delta_t` invariant |
| `burnup.mass` | bundle | authoritative heavy-metal mass in `kg_HM` |
| `burnup.monotonicity` | bundle/history | nonnegative nondecreasing burnup |
| `burnup.coefficient_bracket` | bundle/lookup | exact table/bracket identity |
| `burnup.coefficient_alpha` | bundle/lookup | interpolation fraction in `1` |
| `refuel.scheme` | event | `S4`/`S8` declarative scheme identity |
| `refuel.direction_binding` | event/channel | flow-to-shift direction agreement |
| `refuel.position_mapping` | event | complete moved/inserted/discharged mapping |
| `refuel.atomicity` | event | commit/rollback and unchanged-on-failure evidence |
| `refuel.history` | bundle/event | ID, location, residence, power, nuclide history |

Burnup uses actual P2-T04 amplitude-scaled power and exact P2-T03 state/snapshot
bindings. A positive-duration interval split at a kinetic, nuclide, refuelling,
or coefficient event must produce separately ordered interval observables.

### Kinetics, xenon, RRS, and feedback

| QuantityId | Scope | Observable |
| --- | --- | --- |
| `kinetics.amplitude` | global/time | dimensionless power amplitude |
| `kinetics.reference_power` | global | fixed spatial-shape reference power in `W` |
| `kinetics.actual_flux` | node/group | amplitude-scaled flux in `m^-2 s^-1` |
| `kinetics.actual_node_power` | node | amplitude-scaled local power in `W` |
| `kinetics.actual_total_power` | global | amplitude-scaled total power in `W` |
| `kinetics.fission_rate_density` | node | local fission rate density in `m^-3 s^-1` |
| `kinetics.amplitude_flux_invariant` | node/group | exact `actual flux = amplitude * shape` invariant |
| `kinetics.amplitude_node_power_invariant` | node | exact `P_i = amplitude * P_shape,i` invariant |
| `kinetics.amplitude_total_power_invariant` | global | exact `P_total = amplitude * P_ref` invariant |
| `kinetics.precursor` | global/group | delayed-neutron precursor state |
| `kinetics.reactivity` | global | `rho_spatial = rho_total` from effective `k` |
| `kinetics.step` | time | exact policy/state/version binding |
| `kinetics.step_size` | time | step duration in `s` |
| `kinetics.step_index` | time | exact integration index |
| `kinetics.integration_policy` | global/time | exact configured policy and partition bytes |
| `xenon.I135_inventory` | bundle | authoritative I-135 atom inventory |
| `xenon.Xe135_inventory` | bundle | authoritative Xe-135 atom inventory |
| `xenon.production_decay_absorption` | bundle/interval | signed source/sink terms |
| `xenon.atom_balance` | bundle/interval | exact atom-inventory balance invariant |
| `xenon.absorption_overlay` | node/group | dynamic Xe `m^-1` addition |
| `xenon.volume_binding` | bundle/node | exact node-volume binding identity |
| `xenon.node_volume` | bundle/node | control volume in `m^3` |
| `xenon.I135_number_density` | bundle/node | derived I-135 density in `m^-3` |
| `xenon.Xe135_number_density` | bundle/node | derived Xe-135 density in `m^-3` |
| `rrs.total_power_error` | global/time | setpoint minus measured power |
| `rrs.tilt_error` | region/time | target fraction minus measured fraction |
| `rrs.total_integral_error` | global/time | total integral error in `W s` |
| `rrs.tilt_integral` | region/time | regional integral error in `s` |
| `rrs.actuator_command` | actuator/time | requested command and bounds |
| `rrs.actuator_state` | actuator/time | delayed/rate-limited physical state |
| `rrs.actuator_command_event` | actuator/event | exact command event identity/order |
| `rrs.actuator_state_transition` | actuator/event | exact delayed/rate-limited transition |
| `rrs.saturation` | actuator/event | explicit saturation diagnostic |
| `feedback.rrs_overlay` | node/group | RRS local absorption overlay and digest |
| `feedback.optional_overlay` | node/group | optional temperature/purity/device overlay and digest |
| `feedback.disabled_zero` | branch | exact zero overlay when disabled |
| `feedback.zone_grouping` | global | exact 14 logical/6 physical mapping |
| `feedback.poison_mass` | global/time | add/withdraw accounting in `kg` |
| `feedback.poison_mass_accounting` | global/time | exact add/withdraw mass invariant |
| `feedback.poison_concentration` | global/time | bulk poison concentration in `kg/m^3` |
| `feedback.moderator_volume` | global/time | moderator volume in `m^3` |
| `feedback.zone_overlay` | node/group | liquid-zone absorption overlay in `m^-1` |
| `feedback.adjuster_overlay` | node/group | adjuster absorption overlay in `m^-1` |
| `feedback.poison_overlay` | node/group | bulk-poison absorption overlay in `m^-1` |
| `feedback.branch_schema` | branch | exact enabled/reference/current schema and digest |
| `feedback.temperature_state` | branch | temperature in `K` |
| `feedback.purity_state` | branch | purity mass fraction in `kg/kg` |
| `feedback.zone_fill_state` | branch | zone fill fraction in `1` |
| `feedback.adjuster_state` | branch | adjuster state in `1` |
| `feedback.rrs_map_ownership` | source/map | exact RRS owner/map/sign certificate |
| `feedback.optional_map_ownership` | source/map | exact optional-branch owner/map/sign certificate |
| `feedback.queue_cadence` | event/time | exact due-time queue and cadence order |
| `feedback.queue_cadence_time` | event/time | due time in `s` |
| `feedback.queue_order` | event/time | exact queue sequence |

RRS/device observables compare effective overlays and resulting spatial shape;
they do not accept a separately added direct reactivity term in v1.

### Determinism, serialization, and diagnostics

| QuantityId | Scope | Observable |
| --- | --- | --- |
| `determinism.output_digest` | run | digest of ordered output bytes |
| `determinism.repeat_equal` | run pair | exact digest/byte equality |
| `determinism.shuffle_invariant` | fixture | same result after input permutation |
| `serialization.round_trip` | snapshot | exact supported-field round trip |
| `diagnostics.first_failure` | failed run | first canonical invalid category/key |
| `diagnostics.nonconvergence` | failed solve | policy, residual, iteration evidence |
| `diagnostics.clamp_count` | run | explicit clamp/saturation counts by owner |
| `diagnostics.forbidden_clamp` | run | exact zero for data/flux/density/state clamps |
| `parser.dragon.kinf` | parser | exact documented KINF lexeme/provenance |
| `parser.donjon.keff` | parser | exact documented K-effective lexeme/provenance |
| `parser.manifest.file_digest` | provenance | exact path/length/SHA/repeat record |

Determinism is exact. A different digest or byte sequence is a failure even if
the numerical values appear close.

### Canonical observable bytes

The digest payload is a versioned semantic envelope, independent of the host
filesystem and human-readable report. Its bytes are constructed in this exact
order: ASCII magic `CANDU-OBSERVABLE-V1`, one zero byte, schema version as
unsigned little-endian `uint32`, record count as unsigned little-endian
`uint32`, then records in the total order above. Each record contains exactly
these hashed fields, in this order: `ObservableId`, `QuantityId`, `Scope`,
`SimulationTime`, `StateBinding`, `InputDigest`, `Value`, `ComparisonRuleId`,
`ToleranceProfileId`, `ReferenceId`, `CoverageClass`, `ArtifactAvailability`,
`EvidenceApproval`, `ValidationDomain`, `Status`, and `OrderKey`.
`EvidencePath` is not hashed. A field is encoded as a one-byte type tag, an
unsigned little-endian `uint32` byte length, and its payload. Arrays contain
their count and already-canonical element order; maps are sorted by canonical
key bytes before encoding.

The v1 byte-code table is closed. The schema version is the literal unsigned
little-endian `uint32` value `1`. Type tags are: `0x00=Null`, `0x01=UInt8`,
`0x02=UInt16`, `0x03=UInt32`, `0x04=UInt64`, `0x05=Int32`, `0x06=Int64`,
`0x07=Float64`, `0x08=Bool`, `0x09=Uuid16`, `0x0A=Utf8`, `0x0B=Bytes`,
`0x0C=Array`, `0x0D=Struct`, `0x0E=AvailableValue`, `0x0F=UnavailableValue`,
and `0x10=NotApplicable`. `UInt8`, `UInt16`, `UInt32`, `UInt64`, `Int32`,
and `Int64` have exactly 1, 2, 4, 8, 4, and 8 little-endian bytes. `Float64`
has exactly 8 IEEE-754 binary64 bytes. `Bool` is `0x00=false` or `0x01=true`.
`Uuid16` is exactly 16 bytes. A UUID displayed as lowercase ASCII is converted
by removing hyphens, decoding each adjacent pair of hex digits from left to
right into bytes `0..15` (network/display order), and never using .NET
`Guid`'s mixed-endian byte layout. `Utf8` and `Bytes` use the preceding
`uint32` length. Arrays and structs use a `uint32` element/field count; maps are
encoded as sorted key/value pairs with no duplicate keys.

Field widths are fixed: `ObservableId` and event IDs are `Uuid16`;
`CoreStateVersion`, `SpatialStateVersion`, `PowerSnapshotVersion`,
`KineticStepIndex`, `NuclideStateVersion`, `Sequence`, `InitialNextSequence`,
`NextSequence`, and iteration counts are `UInt64`; `ChannelId` is `UInt32`,
`BundlePosition` and group indices are
`UInt16`, and the schema version is the `UInt32` value defined above.
`TopologyVersion` and `DataPackVersion` are strict UTF-8 strings; opaque IDs
are length-prefixed `Bytes`. `EventRank` is `UInt16` and `ScopeKindRank` is
`UInt8`; digests are
32-byte `Bytes`; times and authoritative floating quantities are `Float64`.
All enum ordinals other than `EventRank` are `UInt8` and are assigned in
declaration order: the
`ScopeKind` rank table above, `CoverageClass=(Direct=0, Indirect=1,
Synthetic=2, NotCovered=3)`, `ArtifactAvailability=(CommittedSynthetic=0,
ExternalApproved=1, PrivateExternal=2, Absent=3)`,
`EvidenceApproval=(Candidate=0, NotApproved=1, Approved=2)`,
`ValidationDomain=(Parser=0, Runtime=1, Provenance=2)`, and
`Status=(Pass=0, Fail=1, Deferred=2, NotCovered=3, NotApplicable=4)`.
`Norm=(Scalar=0, L_inf=1, L1=2, Weighted=3, Invariant=4)` and
`Acceptance=(Exact=0, Absolute=1, Relative=2, Or=3, And=4, Invariant=5)` use the same
`UInt8` ordinal rule. `EventRank` is the P2-T04 table, amended by the G2-C03
RRS queue contract:
`Burnup=0, Refuelling=1, ControllerCommandGeneration=2, ActuatorMotion=3,
BranchUpdate=4, SpatialSolve=5, KineticNuclideStep=6`.
`Value` uses `AvailableValue=(Scalar=0, Vector=1, Event=2, Bytes=3,
Structured=4, Integer=5, Bool=6)` or
`UnavailableValue=(Nonconverged=0, InvalidState=1, MissingReference=2,
DeferredProfile=3, NotApplicable=4)` followed by its fixed reason/payload
fields. `NotApplicable` fields carry no payload. No implementation may assign
an ordinal, width, or alternate union layout outside this table.

Structured layouts are also fixed. `ScopeKey` variants are:
`Global=(NodeSetCountUInt32, NodeKeyArray)`;
`Entity=(EntityKindOrdinalUInt8, EntityKey)`, with
`EntityKindOrdinal` closed as `Channel=0, BundleSlot=1, Bundle=2, Node=3,
Edge=4, Face=5, Actuator=6, Branch=7, Conductance=8, Controller=9, Queue=10`;
`Entity.Channel=(ChannelIdUInt32)`, `Entity.BundleSlot=(ChannelIdUInt32,
BundlePositionUInt16)`, `Entity.Bundle=(BundleIdBytes, ChannelIdUInt32,
BundlePositionUInt16, NodeVolumeFloat64)`,
`Entity.Node=(ChannelIdUInt32, BundlePositionUInt16)`,
`Entity.Edge=(NodeKey, NodeKey)`, `Entity.Face=(ChannelIdUInt32,
BundlePositionUInt16, FaceIdUtf8)`, `Entity.Actuator=(ActuatorIdBytes)`,
`Entity.Branch=(BranchIdBytes)`, `Entity.Controller=(ControllerIdBytes)`, and
`Entity.Queue=(QueueIdBytes)`;
`Entity.Conductance=(ConductanceKindOrdinalUInt8, InteriorEdgeKeyOrNA,
BoundaryFaceKeyOrNA)`, where `ConductanceKind` is
`InteriorEdge=0, BoundaryFace=1` and exactly one key is applicable.
`InteriorEdgeKey` is the lexicographically ordered pair of endpoint `NodeKey`s;
`BoundaryFaceKey` is `(ChannelIdUInt32, BundlePositionUInt16, FaceIdUtf8)`;
the two keys never share an untagged or positional encoding;
`Vector=(VectorKindOrdinalUInt8, VectorKey)`, with `VectorKindOrdinal` closed
as `Node=0, NodeGroup=1, EdgeGroup=2, FaceGroup=3, Group=4, Region=5,
RegionPartition=6, Bundle=7`;
`Vector.Node=(NodeCountUInt32, NodeKeyArray)`,
`Vector.NodeGroup=(NodeGroupCountUInt32, NodeGroupKeyArray)`,
`Vector.EdgeGroup=(EdgeGroupCountUInt32, EdgeGroupKeyArray)`,
`Vector.FaceGroup=(FaceGroupCountUInt32, FaceGroupKeyArray)`,
`Vector.Group=(GroupCountUInt32, GroupIndexArray)`,
`Vector.Region=(RegionIdUtf8, MemberNodeCountUInt32, MemberNodeKeyArray)`,
`Vector.RegionPartition=(RegionCountUInt32, repeated RegionIdUtf8,
MemberNodeCountUInt32, MemberNodeKeyArray)`,
`Vector.Bundle=(BundleCountUInt32, BundleIdArray)`,
`SourceMap=(MapIdBytes, TargetKeyArray)`,
`Event=(EventRankUInt16, SequenceUInt64, EventIdUuid16,
OwnerKindOrdinalOrNAUInt8, OwnerKeyOrNA, EventBodyStruct)`,
`Interval=(OwnerKindOrdinalUInt8, OwnerKey, StartTimeFloat64,
EndTimeFloat64, CoreStateVersionUInt64)`,
`Solve=(SolveIdBytes, SpatialStateVersionUInt64, GroupIndexOrNAUInt16)`,
`SolvePair=(StartSolveKey, EndSolveKey)`, `Snapshot=(SnapshotIdBytes,
SnapshotVersionUInt64)`, `Run=(RunIdBytes)`, `RunPair=(RunIdABytes, RunIdBBytes)`,
`Fixture=(FixtureIdUtf8)`, `Failure=(FailureKeyUtf8)`,
`Parser=(SourceIdUtf8, RecordOrdinalUInt64)`,
`Provenance=(ArtifactDigestBytes32, SourceIdUtf8)`,
`Lookup=(OwnerKindOrdinalUInt8, OwnerKey, TableIdUtf8, BracketStruct)`, and
`TimeSeries=(OwnerKindOrdinalOrNAUInt8, OwnerKeyOrNA, SeriesIdUtf8,
ComponentKey)`. `ComponentKey` variants are
`Node=(ChannelIdUInt32, BundlePositionUInt16)`,
`NodeGroup=(ChannelIdUInt32, BundlePositionUInt16, GroupIndexUInt16)`,
`FaceGroup=(ChannelIdUInt32, BundlePositionUInt16, FaceIdUtf8,
GroupIndexUInt16)`, `EdgeGroup=(NodeKey, NodeKey, GroupIndexUInt16)`,
`Group=GroupIndexUInt16`, `Region=RegionIdUtf8`,
`Bundle=BundleIdBytes`, `Term=TermOrdinalUInt8`,
`BeforeAfter=OrdinalUInt8`, `Event=(EventRankUInt16, SequenceUInt64,
EventIdUuid16)`, `Time=SimulationTimeFloat64`, and
`NotApplicable=empty`. `Interval.OwnerKindOrdinal` is closed as
`Bundle=0, Global=1, Region=2, Node=3, Run=4`.
`Lookup.BracketStruct` is
`(LowerIndexUInt32, UpperIndexUInt32, AlphaFloat64, ResultStatusOrdinalUInt8)`;
`Lookup.OwnerKindOrdinal` is closed as `Bundle=0, NodeGroup=1, Region=2,
Global=3`; its `OwnerKey` is respectively `BundleIdBytes`,
`(ChannelIdUInt32, BundlePositionUInt16, GroupIndexUInt16)`, `RegionIdUtf8`,
or `NodeSet`. `Event.OwnerKindOrdinalOrNA` is closed as
`Channel=0, Bundle=1, Snapshot=2, Actuator=3, Branch=4, Controller=5, Queue=6,
NotApplicable=255`; `TimeSeries.OwnerKindOrdinalOrNA` is closed as `Global=0,
Region=1, Bundle=2, Actuator=3, Event=4, Snapshot=5, Controller=6, Queue=7,
NotApplicable=255`.
`Scope` is `(ScopeKindOrdinalUInt8, ScopeKey)`. `StateBinding` is
`(CoreStateVersionUInt64OrNA, SpatialStateVersionUInt64OrNA,
SpatialSolveIdBytesOrNA, PowerSnapshotIdBytesOrNA,
PowerSnapshotVersionUInt64OrNA, KineticStepIndexUInt64OrNA,
NuclideStateVersionUInt64OrNA, TopologyVersionUtf8OrNA, DataPackVersionUtf8OrNA,
CoefficientDigestBytes32OrNA, SnapshotDigestBytes32OrNA)`; `AvailableValue` is
`(ValueKindOrdinal, UnitIdUtf8, PayloadSchemaIdUtf8, ComponentOrderSpec, Payload)` where `ValueKindOrdinal` is
`Scalar=0, Vector=1, Event=2, Bytes=3, Structured=4, Integer=5, Bool=6`.
Their payloads are
`Scalar=(Float64)`, `Integer=(UInt64)`, `Bool=(0x00 or 0x01)`,
`Vector=(CountUInt32, repeated ComponentKey and Float64)`,
`Event=(EventRankUInt16, SequenceUInt64, EventIdUuid16, EventBodyStruct)`,
`Bytes=(LengthUInt32, Bytes)`, and
`Structured=(SchemaIdUtf8, FieldCountUInt32, repeated FieldOrdinalUInt16,
FieldIdUtf8, tagged FieldValue)`;
`UnavailableValue` is
`(ReasonOrdinal, DiagnosticKeyUtf8)`; and `OrderKey` is
`(ValidationDomainRank, SimulationTimeOrNA, CoreStateVersionOrNA,
EventRankOrNA, SequenceOrNA, EventIdBytesOrNA, ScopeKindRank, ScopeKey,
QuantityId, ComponentKey, ObservableId)`. `ComparisonRuleId`,
`ToleranceProfileId`, `ReferenceId`, and
all quantity IDs are strict UTF-8 strings. These layouts, field order, and
`NotApplicable` tags are part of v1 and are not implementation choices.

The structured payload schemas are closed and field order is by the declared
ordinal, never by source or map insertion order. Each field is defined as
`(FieldOrdinalUInt16, FieldIdUtf8, FieldTypeTag, ApplicabilityTag)`; ordinals
are contiguous from zero. `FieldTypeTag` is one of the v1 byte tags or the
named nested schema, and `ApplicabilityTag` is either `Always` or an explicit
`NotApplicable` tag. The complete named schema field layouts are:

| SchemaId | Ordered fields with fixed types |
| --- | --- |
| `InvariantV1` | `Left:Float64`, `Right:Float64`, `ResidualOrViolation:Float64`, `InvariantKind:UInt8`, `Applicability:UInt8`, `Status:UInt8` |
| `LocationV1` | `BundleId:Bytes`, `ChannelId:UInt32`, `BundlePosition:UInt16`, `NodeVolume:Float64`, `InsertedAt:Float64`, `MovementStatus:UInt8` |
| `MappingV1` | `CommandId:Bytes`, `Moved:Array<Bytes>`, `Inserted:Array<Bytes>`, `Discharged:Array<Bytes>`, `PositionBinding:Array<PositionBindingV1>` |
| `PositionBindingV1` | `BundleId:Bytes`, `OldChannelIdOrNA:UInt32OrNA`, `OldBundlePositionOrNA:UInt16OrNA`, `NewChannelIdOrNA:UInt32OrNA`, `NewBundlePositionOrNA:UInt16OrNA`, `MovementStatus:UInt8` |
| `AtomicityV1` | `CommandId:Bytes`, `BeforeDigest:Bytes32`, `ProposedDigest:Bytes32`, `CommitStatus:UInt8`, `RollbackReasonOrNA:Utf8OrNA`, `UnchangedDigestOrNA:Bytes32OrNA` |
| `HistoryV1` | `BundleId:Bytes`, `Sequence:UInt64`, `Location:LocationV1`, `ResidenceTime:Float64`, `Power:Float64`, `NuclideStateDigest:Bytes32` |
| `EventV1` | `EventRank:UInt16`, `Sequence:UInt64`, `EventId:Uuid16`, `OwnerKey:ScopeKey`, `TransitionBody:EventBodyStruct` |
| `SaturationV1` | `Requested:Float64`, `Bounded:Float64`, `State:UInt8`, `LowerBound:Float64`, `UpperBound:Float64`, `RateLimit:Float64`, `Delay:Float64`, `Saturated:Bool`, `EventId:Uuid16` |
| `DisabledZeroV1` | `BranchId:Bytes`, `Enabled:Bool`, `ExactZero:Float64`, `OverlayDigest:Bytes32`, `OwnerMapDigest:Bytes32` |
| `OwnershipV1` | `MapId:Bytes`, `OwnerId:Bytes`, `TargetKeySet:Array<ScopeKey>`, `SignCertificate:Bytes`, `MapDigest:Bytes32` |
| `QueueV1` | `QueueId:Bytes`, `CommandId:Uuid16`, `TargetId:Bytes`, `OwnerKey:ScopeKey`, `SourceKind:UInt8`, `SourceEventId:Uuid16`, `SourceStateBindingDigest:Bytes32`, `EnqueueTime:Float64`, `Delay:Float64`, `DueTime:Float64`, `EventRank:UInt16`, `Sequence:UInt64`, `Requested:Float64`, `Bounded:Float64`, `LowerBound:Float64`, `UpperBound:Float64`, `RateLimit:Float64`, `CommandDigest:Bytes32` |
| `QueueAvailableV1` | `TargetId:Bytes`, `Bounded:Float64`, `LowerBound:Float64`, `UpperBound:Float64`, `RateLimit:Float64` |
| `QueueStateV1` | `QueueId:Bytes`, `OwnerKey:ScopeKey`, `GenerationCadenceOrNA:Float64OrNA`, `InitialNextSequence:UInt64`, `NextSequence:UInt64`, `LastMotionTime:Float64`, `AvailableCommands:Array<QueueAvailableV1>`, `PendingCommands:Array<QueueV1>`, `AppliedSourceEventIds:Array<Uuid16>`, `AllocatedCommandIds:Array<Uuid16>`, `QueueDigest:Bytes32` |
| `ParserV1` | `SourceLexeme:Utf8`, `FiniteValueOrNA:Float64OrNA`, `SourceDigest:Bytes32`, `ToolId:Utf8`, `ToolVersion:Utf8`, `ProvenanceKey:ScopeKey` |
| `ManifestV1` | `PathFreeName:Utf8`, `ByteLength:UInt64`, `Sha256:Bytes32`, `RepeatIndex:UInt64`, `RepeatDigest:Bytes32` |

The nested `ScopeKey` and `ComponentKey` layouts are the typed layouts above;
they are not opaque strings. The status ordinals used by these schemas are
closed: `MovementStatus=(Present=0, Inserted=1, Moved=2, Discharged=3,
NotApplicable=255)`, `CommitStatus=(Committed=0, RolledBack=1, Rejected=2,
NotApplicable=255)`, `ResultStatus=(InRange=0, BelowRange=1, AboveRange=2,
Rejected=3, NotApplicable=255)`, `TransitionStatus=(Applied=0, Rejected=1,
NotApplicable=255)`, `QueueTransitionKind=(Enqueue=0, MotionAndConsume=1,
NotApplicable=255)`, and `SaturationState=(Unsaturated=0, LowerBound=1,
UpperBound=2, NotApplicable=255)`. `InvariantKind=NotApplicable` is always
the single fixed ordinal `0`, not a missing field. Every field's tag and fixed
width are specified by the enclosing type table; missing, duplicate, or
reordered fields fail closed.

`EventBodyStruct` is itself discriminated as
`(EventBodyKindOrdinalUInt8, BodySchemaIdUtf8, FieldCountUInt32, repeated
FieldOrdinalUInt16, FieldIdUtf8, tagged FieldValue)`. The kind and schema ID
must agree. `EventBodyKind` is closed as `RefuelMapping=0, RefuelAtomicity=1,
ActuatorCommand=2, ActuatorTransition=3, Saturation=4, Queue=5,
BranchUpdate=6, Burnup=7, NotApplicable=255`. The complete event-body schemas
are:

| BodySchemaId | Ordered fields with fixed types |
| --- | --- |
| `RefuelMappingBodyV1` | `SchemeId:Utf8`, `EffectiveTime:Float64`, `ShiftCount:UInt16`, `MovedBundleIds:Array<Bytes>`, `InsertedBundleIds:Array<Bytes>`, `DischargedBundleIds:Array<Bytes>`, `PositionBinding:Array<PositionBindingV1>` |
| `RefuelAtomicityBodyV1` | `CommandId:Bytes`, `BeforeDigest:Bytes32`, `ProposedDigest:Bytes32`, `CommitStatus:UInt8`, `RollbackReasonOrNA:Utf8OrNA`, `UnchangedDigestOrNA:Bytes32OrNA` |
| `ActuatorCommandBodyV1` | `QueueId:Bytes`, `CommandId:Uuid16`, `ActuatorId:Bytes`, `OwnerKey:ScopeKey`, `SourceKind:UInt8`, `SourceEventId:Uuid16`, `SourceStateBindingDigest:Bytes32`, `EnqueueTime:Float64`, `Delay:Float64`, `DueTime:Float64`, `EventRank:UInt16`, `Sequence:UInt64`, `Requested:Float64`, `Bounded:Float64`, `LowerBound:Float64`, `UpperBound:Float64`, `RateLimit:Float64`, `CommandDigest:Bytes32` |
| `ActuatorTransitionBodyV1` | `QueueId:Bytes`, `ActuatorId:Bytes`, `PreviousState:Float64`, `NextState:Float64`, `AvailableBefore:Float64`, `AvailableAfter:Float64`, `MotionStartTime:Float64`, `EffectiveTime:Float64`, `DeltaTime:Float64`, `ConsumedCommandIds:Array<Uuid16>`, `EventRank:UInt16`, `TransitionStatus:UInt8`, `StateDigest:Bytes32` |
| `SaturationBodyV1` | `Requested:Float64`, `Bounded:Float64`, `State:UInt8`, `LowerBound:Float64`, `UpperBound:Float64`, `RateLimit:Float64`, `Delay:Float64`, `Saturated:Bool`, `EventId:Uuid16` |
| `QueueBodyV1` | `QueueId:Bytes`, `OwnerKey:ScopeKey`, `TransitionKind:UInt8`, `EventTime:Float64`, `QueueBeforeDigest:Bytes32`, `QueueAfterDigest:Bytes32`, `NextSequenceBefore:UInt64`, `NextSequenceAfter:UInt64`, `MotionStartTimeOrNA:Float64OrNA`, `MotionEndTimeOrNA:Float64OrNA`, `DueBatchCutoffTimeOrNA:Float64OrNA`, `Commands:Array<QueueV1>`, `AvailableBefore:Array<QueueAvailableV1>`, `AvailableAfter:Array<QueueAvailableV1>` |
| `BranchUpdateBodyV1` | `BranchId:Bytes`, `Enabled:Bool`, `MapDigest:Bytes32`, `StateDigest:Bytes32` |
| `BurnupBodyV1` | `BundleId:Bytes`, `PreviousBurnup:Float64`, `NextBurnup:Float64`, `IntervalEnergy:Float64`, `StateDigest:Bytes32` |

Every event record selects one of these schemas or the explicit
`NotApplicable` body tag; an unrecognized body kind, omitted required field,
duplicate field, or incompatible field type fails closed. The `EventV1` and
structured queue/transition payloads use these same body definitions rather
than an unconstrained struct.

`QueueV1` is the canonical per-command projection defined by the kinetics
specification. `QueueStateV1` is the complete event-boundary queue state, and
`QueueBodyV1` is an atomic enqueue or motion/consume event projection; no one of
the three substitutes for another. `TargetId` is the typed actuator, zone, or
adjuster target. `OwnerKey` is `Entity.Controller(ControllerId)` for RRS or the
typed branch owner for a branch queue. `QueueId`, owner, and target are all
validated; moving a record between queues or owners is a digest failure.

`SourceKind` uses the closed enum (`Controller=0`, `Manual=1`, `Scheduled=2`).
Every RRS entry requires `EventRank=2`; zone and adjuster entries require
`EventRank=4`. `SourceEventId` and `SourceStateBindingDigest` preserve generation
provenance. An automatic RRS source binding identifies the measurement snapshot
frozen before same-time rank-0/rank-1 commits; manual and scheduled bindings
identify the post-rank-1 event pre-state. `EnqueueTime`, `Delay`, and `DueTime` preserve the exact one-addition
binary64 construction, while `Requested`/`Bounded` and the limits preserve
saturation evidence. `GenerationCadenceOrNA` is a positive finite periodic
generation cadence stored once in `QueueStateV1` when applicable and explicit
`NotApplicable` otherwise; it is not a `QueueV1` command field, does not replace
`Delay`, does not participate in due-time arithmetic, and does not alter the
canonical order `(DueTime, EventRank, Sequence, CommandIdBytes)`.

For RRS, `CommandDigest` is SHA-256 of the `CANDU-RRS-COMMAND-V1` magic, zero
separator, `UInt32` schema version `1`, and exact `RRSCommandV1` field list in
its declared order, including `QueueId`, `CommandId`, target, and `OwnerKey`,
and excluding only the digest itself. Zone and adjuster records use their exact
owner-declared field lists and `CANDU-ZONE-COMMAND-V1` or
`CANDU-ADJUSTER-COMMAND-V1` magic. `QueueStateV1.QueueDigest` is
SHA-256 over the queue-state magic/schema and every ordered state field except
the digest itself. `PendingCommands` use canonical queue order;
`AvailableCommands` use ascending target bytes, and `AppliedSourceEventIds` and
`AllocatedCommandIds` are the append-only ascending UUID-byte sets used for
replay and historical collision checks. A digest without both underlying sets
is invalid. A command nested in `PendingCommands` must repeat the same queue ID
and owner or fail. `NextSequence >= InitialNextSequence`, and the exact
`UInt64` difference equals the `AllocatedCommandIds` array count; that count
and the `AppliedSourceEventIds` count must fit the canonical `UInt32`
array-count encoding.

`QueueBodyV1.TransitionKind` is closed as `Enqueue=0` and
`MotionAndConsume=1`. Enqueue bodies use `NotApplicable` for all three motion/
cutoff times, carry the inserted command batch, advance `NextSequence` by the
batch count, and carry byte-identical available-command before/after arrays.
Motion/consume bodies carry the exact elapsed interval, frozen due batch,
available-command before/after arrays, unchanged sequence values, and
before/after queue digests. This body records that physical motion used
`AvailableBefore` and that `AvailableAfter` became active only at
`MotionEndTime`. Missing, duplicate, reordered,
non-finite, owner-inconsistent, retroactive, or digest-inconsistent fields fail
closed; no epsilon or tolerance is used for time or due-time equality.

UTF-8 strings are strict, shortest-form UTF-8 with no BOM. Integers are fixed
width little-endian. UUIDs are their 16 raw bytes. Finite IEEE-754 binary64
values are eight little-endian bytes; negative zero is rejected for
authoritative quantities rather than silently normalized. NaN and infinity,
non-canonical encodings, an omitted required field, an unknown type tag, or an
unexpected trailing byte fails closed. Null is encoded only for a field whose
schema explicitly permits it, with a distinct null tag; absence is not null.
The digest is `SHA-256(CanonicalObservableBytes)`.

`EvidencePath` is excluded from the semantic payload; `ReferenceId` and
`InputDigest` are the path-free provenance identities that may be included.
Working directories, host-local paths, process IDs, thread IDs, locale,
timestamps, and private artifact paths are excluded from the semantic payload.
They may appear only in a separate human-readable reporting envelope and must
not affect `determinism.output_digest` or `determinism.repeat_equal`.

## Comparison rules

### Exact discrete and byte comparison

Use exact comparison for IDs, counts, enums, indices, state/event order,
serialized time fields, schema/data versions, digests, source lexemes, exact
power-normalization flags, atomicity outcomes, and replay bytes. A missing or
extra record fails; it is never ignored as an optional column.

### Scalar absolute/relative comparison

For a finite scalar actual value `x` and reference `r`, a provisional profile
may specify:

```text
error_abs = abs(x - r)
error_rel = error_abs / max(abs(r), ReferenceScale)
```

`ReferenceScale` is an explicit profile field with the same unit and must be
positive when the relative rule is used. A zero-reference quantity must use an
explicit absolute rule or an explicit positive scale; no hidden epsilon is
invented. The profile declares whether acceptance is `absolute OR relative` or
`absolute AND relative`; no implementation chooses this per quantity. The
acceptance inequalities are `error_abs <= AbsoluteTolerance` and
`error_rel <= RelativeTolerance`; both error values and every threshold must be
finite and the applicable threshold must be present. `OR` and `AND` are
evaluated exactly as declared. An absent threshold makes the record `Deferred`,
not a zero-tolerance pass.

### Vector and integrated comparison

A vector profile declares canonical `L_inf`, `L1`, weighted integrated, or
quantity-specific conservation comparison. For aligned finite vectors `x` and
`r`, with `d_i = x_i-r_i`, the required reductions are:

```text
E_inf_abs = max_i abs(d_i)
E_inf_rel = E_inf_abs / max(max_i abs(r_i), ReferenceScale)
E_L1_abs  = sum_i abs(d_i)
E_L1_rel  = E_L1_abs / max(sum_i abs(r_i), ReferenceScale)
E_w_abs   = sum_i w_i * abs(d_i) / sum_i w_i
E_w_rel   = E_w_abs / max(sum_i w_i * abs(r_i) / sum_i w_i, ReferenceScale)
```

`w_i` must be finite, nonnegative, have the declared integration weight unit,
and have a positive finite sum. `ReferenceScale` has the quantity unit and is
positive whenever a relative reduction is used. The profile declares the
reduction, threshold, and `OR`/`AND` rule; pass means the declared error
inequality is true. A vector must have the same canonical component keys and
sample times on both sides. Missing, duplicate, reordered, non-finite, or
unpaired components fail before a norm is computed. Time-series reductions
align exact serialized `SimulationTime` values; interpolation or resampling is
not allowed unless a later approved profile names its method, source, and
weights. For power fractions, the sum-to-one and per-region values are reported
separately.

### Invariants and conservation

Some observables are invariant checks rather than reference comparisons:

```text
sum_i P_i = P_total
sum_region P_region = P_total
B = InitialBurnup + CumulativeFissionEnergy / m_HM
Sigma_a,eff >= Sigma_f
chi_2 = 1 - chi_1
live_slots = 4,560
```

For an equality `L = R`, report `abs(L-R)` and pass only when the declared
equality rule passes. For a lower-bound invariant `L >= R`, report the
directional violation `max(0, R-L)` in the left/right unit and pass only when
that violation is exactly zero for a contract invariant (or is within an
explicit later-approved bound). A negative `Sigma_a,eff` margin therefore
cannot be hidden by an absolute residual. Every derived error, reduction,
weight, and invariant operand must be finite. The invariant record reports
both operands, unit, directional/equality rule, residual or violation,
profile ID, and status; it does not replace the component observables.

## Provisional tolerance profiles

### Profile contract and status

Each non-exact quantity comparison references a `ToleranceProfile`:

| Field | Requirement |
| --- | --- |
| `ProfileId` / `Version` | stable identity |
| `QuantityId` | exactly one catalog quantity and one authoritative unit |
| `ValueKind` | exactly one `Scalar`, `Vector`, `Event`, `Bytes`, `Structured`, `Integer`, or `Bool` payload |
| `PayloadSchemaId` | exact schema ID from the closed quantity partition |
| `ComponentOrderSpec` | exact canonical component order; `NotApplicable` for non-vector payloads |
| `Norm` | scalar, `L_inf`, `L1`, weighted, or invariant |
| `AbsoluteTolerance` | finite nonnegative value in quantity unit, when used |
| `RelativeTolerance` | finite nonnegative dimensionless value, when used |
| `ReferenceScale` | explicit same-unit positive scale, when required |
| `Acceptance` | exact, absolute, relative, OR, AND, or invariant rule |
| `ComparisonRuleId` | exact rule identity derived from the profile ID |
| `InvariantKind` | `NotApplicable`, `Equality`, `LowerBound`, `ConditionalEquality`, or `Support` |
| `Applicability` | `Always`, `WhenNuGPresent`, `WhenFissionNonzero`, or `WhenConsecutiveKPair` |
| `ThresholdState` | `NotApplicable` for exact rules, `Deferred` until every numeric threshold is explicitly supplied, otherwise `Specified` |
| `ApprovalStatus` | `Provisional`, `Approved`, or `Deferred` |
| `OwnerGate` | gate that may approve or replace it |
| `DataDigest` | exact profile bytes |

Every numeric profile declared by P2-T05 has `ApprovalStatus=Provisional` and
`ThresholdState=Deferred`; this records the intended quantity and norm without
inventing a numeric value. An emitted comparison whose threshold state is
`Deferred` has `Status=Deferred`, never `Pass`. Exact structural profiles have
`ApprovalStatus=Deferred`, `ThresholdState=NotApplicable`, and no numeric
tolerance until their named contract gate approves them. Only the named later
gate may transition a profile to `Approved`, and the transition records the
old/new profile digest, evidence, and reason. The G2 forced closure preserves
the mathematical-design record but approves no numeric tolerance. Tolerances
are never loosened because a result fails.

### Executable rule and profile encoding

The crosswalk rule tokens expand deterministically as follows:

| Crosswalk token | `Norm` | `Acceptance` | `InvariantKind` | `Applicability` |
| --- | --- | --- | --- | --- |
| `exact` or any `exact ...` rule | `Invariant` | `Exact` | `Equality` | `Always` |
| `scalar profile` | `Scalar` | `Or` | `NotApplicable` | `Always` |
| `scalar profile; NotApplicable when fission fields are zero` | `Scalar` | `Or` | `NotApplicable` | `WhenFissionNonzero` |
| `scalar profile; NotApplicable when no consecutive k pair` | `Scalar` | `Or` | `NotApplicable` | `WhenConsecutiveKPair` |
| `L_inf` or `two-component L_inf` or `two-component L_inf profile` | `L_inf` | `Or` | `NotApplicable` | `Always` |
| `equality invariant` | `Invariant` | `Invariant` | `Equality` | `Always` |
| `conditional equality invariant; NotApplicable when nu_g is absent` | `Invariant` | `Invariant` | `ConditionalEquality` | `WhenNuGPresent` |
| `lower-bound invariant` | `Invariant` | `Invariant` | `LowerBound` | `Always` |
| `support invariant` | `Invariant` | `Invariant` | `Support` | `Always` |

For every crosswalk row, `ComparisonRuleId` is the exact UTF-8 string
`P2-T05-rule-` followed by that row's unique `ProfileId`; no rule ID is
implicit or shared. `ThresholdState` ordinals are
`NotApplicable=0, Deferred=1, Specified=2`; `ApprovalStatus` ordinals are
`Deferred=0, Provisional=1, Approved=2`; `InvariantKind` ordinals are
`NotApplicable=0, Equality=1, LowerBound=2, ConditionalEquality=3,
Support=4`;
`Applicability` ordinals are `Always=0, WhenNuGPresent=1,
WhenFissionNonzero=2, WhenConsecutiveKPair=3`; and `OwnerGate` ordinals are
`G2=0, G3=1, G4=2, G5=3, G6=4, G7A=5, G7B=6`. A `Deferred` threshold is
encoded with the `NotApplicable` value tag and cannot be compared as zero.

`ToleranceProfileBytes` uses magic `CANDU-TOLERANCE-V1`, schema version
`UInt32=1`, and these fields in order: `ProfileIdUtf8`, `QuantityIdUtf8`,
`UnitIdUtf8`, `ValueKindOrdinalUInt8`, `PayloadSchemaIdUtf8`,
`ComponentOrderSpec`, `NormOrdinalUInt8`, `AcceptanceOrdinalUInt8`,
`InvariantKindOrdinalUInt8`, `ApplicabilityOrdinalUInt8`,
`ComparisonRuleIdUtf8`, `AbsoluteThresholdTagAndFloat64OrNA`,
`RelativeThresholdTagAndFloat64OrNA`, `ReferenceScaleTagAndFloat64OrNA`,
`ThresholdStateOrdinalUInt8`, `ApprovalStatusOrdinalUInt8`,
`OwnerGateOrdinalUInt8`. `DataDigest` is `SHA-256` of these canonical bytes;
it is recorded after the payload and is not included in its own hash. The
profile and rule bytes are immutable; a revision creates a new profile ID and
records the prior digest and reason.

`ComponentOrderSpec` is either the explicit `NotApplicable` tag for a
non-vector value or the profile-level struct
`(OrderKindOrdinalUInt8, ComponentKeySchemaIdUtf8, ComparatorOrdinalUInt8,
TieBreakSchemaIdUtf8)`. It contains the key schema and comparator only; it
never contains a concrete component count or concrete key list. Concrete
counts and ordered keys are encoded in the observable record's `Scope` and
`Vector` payload, so one immutable profile applies to every legal topology.
`OrderKind` is `SpatialNodeGroup=0, Node=1, Region=2, BundleHistory=3,
BeforeAfter=4, IxeTerm=5, PrecursorGroup=6, TimeSeries=7`; each
`ComponentKeySchemaId` names a fixed key-field layout and `ComparatorOrdinal`
names its canonical ascending/tie-break comparator. Thus a component reorder
policy changes the profile bytes and `DataDigest`; concrete record reordering
or membership changes the record bytes, not the profile. It cannot be hidden in
an implementation-only comparison routine. `PayloadSchemaIdUtf8` and
`ComponentOrderSpec` are also
present in `AvailableValue` as `(ValueKindOrdinal, UnitIdUtf8,
PayloadSchemaIdUtf8, ComponentOrderSpec, Payload)` and are included in the
record digest.

The vector quantity assignment is explicit: `spatial.flux` uses
`SpatialNodeGroup`; `spatial.source_shape_change_inf` uses `Node`;
`spatial.normalization_source_totals` and
`spatial.normalization_power_totals` use `BeforeAfter`;
`xenon.production_decay_absorption` uses `IxeTerm`; and
`xenon.absorption_overlay`, `feedback.rrs_overlay`,
`feedback.optional_overlay`, `feedback.zone_overlay`,
`feedback.adjuster_overlay`, and `feedback.poison_overlay` use
`SpatialNodeGroup`. These are the exact `ComponentOrderSpec` values hashed in
the corresponding profiles.

The component-order vocabulary is closed: `ComparatorOrdinal` is
`AscendingCanonical=0, FixedDeclared=1`; `ComponentKeySchemaId` is one of
`NodeKeyV1=(ChannelIdUInt32, BundlePositionUInt16)`,
`NodeGroupKeyV1=(ChannelIdUInt32, BundlePositionUInt16, GroupIndexUInt16)`,
`BeforeAfterKeyV1=(BeforeAfterOrdinalUInt8)`, or
`IxeTermKeyV1=(TermOrdinalUInt8)`; and `TieBreakSchemaId` is one of
`ChannelPositionV1`, `ChannelPositionGroupV1`, `BeforeAfterDeclaredV1`, or
`IxeTermDeclaredV1`. The exhaustive current vector assignment is:

| QuantityId | OrderKindOrdinal | ComponentKeySchemaId | ComparatorOrdinal | TieBreakSchemaId |
| --- | ---: | --- | ---: | --- |
| `spatial.flux` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `spatial.source_shape_change_inf` | 1 | `NodeKeyV1` | 0 | `ChannelPositionV1` |
| `spatial.normalization_source_totals` | 4 | `BeforeAfterKeyV1` | 1 | `BeforeAfterDeclaredV1` |
| `spatial.normalization_power_totals` | 4 | `BeforeAfterKeyV1` | 1 | `BeforeAfterDeclaredV1` |
| `xenon.production_decay_absorption` | 5 | `IxeTermKeyV1` | 1 | `IxeTermDeclaredV1` |
| `xenon.absorption_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `feedback.rrs_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `feedback.optional_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `feedback.zone_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `feedback.adjuster_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |
| `feedback.poison_overlay` | 0 | `NodeGroupKeyV1` | 0 | `ChannelPositionGroupV1` |

### Gate ownership matrix

| Quantity family | Numerical/design owner |
| --- | --- |
| topology, state identity, canonical serialization, replay, and diagnostics | G3 contract review |
| spatial normalization, coefficients, convergence, and power | G4 after G2 design approval |
| burnup, interpolation, refuelling, and energy history | G5 |
| ordinary RRS, actuators, liquid zones, adjusters, and bulk poison | G6 |
| kinetics, I/Xe, and their spatial coupling | G7A |
| temperature/purity/device feedback when enabled | G7B; disabled branches remain exact-zero design records |

The exact profile ID is assigned per row in the crosswalk below. No profile is
shared by two `QuantityId`s, units, norms, or acceptance modes. A later
revision must preserve the quantity, unit, norm, status history, and reason
for change. Replacing a failed profile with a looser one or replacing golden
data is a stop condition.

The complete display-code expansion is:

| Display code | `ThresholdState` | `ApprovalStatus` | `OwnerGate` |
| --- | --- | --- | --- |
| `Specified / G2` | `NotApplicable` | `Deferred` | G2 |
| `Specified / G3` | `NotApplicable` | `Deferred` | G3 |
| `Specified / G4` | `NotApplicable` | `Deferred` | G4 |
| `Specified / G5` | `NotApplicable` | `Deferred` | G5 |
| `Specified / G6` | `NotApplicable` | `Deferred` | G6 |
| `Specified / G7A` | `NotApplicable` | `Deferred` | G7A |
| `Specified / G7B` | `NotApplicable` | `Deferred` | G7B |
| `Specified / P1 contracts` | `NotApplicable` | `Deferred` | G3 |
| `Deferred / G4` | `Deferred` | `Provisional` | G4 |
| `Deferred / G5` | `Deferred` | `Provisional` | G5 |
| `Deferred / G6` | `Deferred` | `Provisional` | G6 |
| `Deferred / G7A` | `Deferred` | `Provisional` | G7A |
| `Deferred / G7B` | `Deferred` | `Provisional` | G7B |

### Quantity crosswalk

The following is the complete one-row-per-`QuantityId` crosswalk. It is the
authoritative check that every catalog entry has a unit, scope, comparison
rule, profile status, and owner. The final column is a compact serialization of
the separate fields `ThresholdState`, `ApprovalStatus`, and `OwnerGate`:
`Specified / G4` means `ThresholdState=NotApplicable`,
`ApprovalStatus=Deferred`, `OwnerGate=G4`; `Deferred / G4` means
`ThresholdState=Deferred`, `ApprovalStatus=Provisional`, `OwnerGate=G4`.
The same expansion applies to every gate name below. No row claims approval.

| QuantityId | Unit | Scope | Rule / unique ProfileId | ThresholdState / ApprovalStatus / OwnerGate |
| --- | --- | --- | --- | --- |
| `topology.channel_count` | integer | global | exact / `P2-T05-topology-channel-count-v1` | Specified / G3 |
| `topology.bundle_position_count` | integer | global | exact / `P2-T05-topology-position-count-v1` | Specified / G3 |
| `topology.slot_count` | integer | global | exact / `P2-T05-topology-slot-count-v1` | Specified / G3 |
| `topology.channel_record` | bytes | channel | exact / `P2-T05-topology-channel-record-v1` | Specified / G3 |
| `topology.adjacency` | bytes | node/edge | exact canonical set/order / `P2-T05-topology-adjacency-v1` | Specified / G3 |
| `topology.boundary_face` | bytes | node/face | exact label/binding / `P2-T05-topology-boundary-v1` | Specified / G3 |
| `topology.flow_binding` | bytes | channel | exact / `P2-T05-topology-flow-v1` | Specified / G3 |
| `inventory.slot_occupancy` | bytes | channel/position | exact set / `P2-T05-inventory-occupancy-v1` | Specified / G5 |
| `inventory.bundle_id_uniqueness` | `1` | global | exact invariant / `P2-T05-inventory-unique-v1` | Specified / G5 |
| `inventory.location_legality` | bytes | bundle | exact invariant / `P2-T05-inventory-location-v1` | Specified / G5 |
| `inventory.residence_time` | `s` | bundle | scalar profile / `P2-T05-inventory-residence-v1` | Deferred / G5 |
| `state.version_binding` | bytes | event/snapshot | exact / `P2-T05-state-binding-v1` | Specified / G3 |
| `state.core_version` | integer | event/snapshot | exact transition / `P2-T05-state-core-version-v1` | Specified / G3 |
| `state.snapshot_digest` | SHA-256 bytes | snapshot | exact / `P2-T05-state-snapshot-digest-v1` | Specified / G3 |
| `spatial.k` | `1` | global/solve | scalar profile / `P2-T05-spatial-k-v1` | Deferred / G4 |
| `spatial.flux` | `m^-2 s^-1` | node/group | `L_inf` / `P2-T05-spatial-flux-v1` | Deferred / G4 |
| `spatial.power` | `W` | node | scalar profile / `P2-T05-spatial-power-v1` | Deferred / G4 |
| `spatial.total_power` | `W` | global | scalar profile / `P2-T05-spatial-total-power-v1` | Deferred / G4 |
| `spatial.region_power` | `W` | region | scalar profile / `P2-T05-spatial-region-power-v1` | Deferred / G4 |
| `spatial.region_power_fraction` | `1` | region | scalar profile / `P2-T05-spatial-region-fraction-v1` | Deferred / G4 |
| `spatial.power_sum_invariant` | `W` | global | equality invariant / `P2-T05-spatial-power-sum-v1` | Deferred / G4 |
| `spatial.region_power_sum_invariant` | `W` | region partition | equality invariant / `P2-T05-spatial-region-power-sum-v1` | Deferred / G4 |
| `spatial.node_volume` | `m^3` | node | scalar profile / `P2-T05-spatial-node-volume-v1` | Deferred / G4 |
| `spatial.sigma_a` | `m^-1` | node/group | scalar profile / `P2-T05-spatial-sigma-a-v1` | Deferred / G4 |
| `spatial.sigma_f` | `m^-1` | node/group | scalar profile / `P2-T05-spatial-sigma-f-v1` | Deferred / G4 |
| `spatial.nu_sigma_f` | `m^-1` | node/group | scalar profile / `P2-T05-spatial-nu-sigma-f-v1` | Deferred / G4 |
| `spatial.sigma_s_1_to_2` | `m^-1` | node | scalar profile / `P2-T05-spatial-sigma-s-v1` | Deferred / G4 |
| `spatial.chi` | `1` | node/group | scalar profile / `P2-T05-spatial-chi-v1` | Deferred / G4 |
| `spatial.energy_per_fission` | `J` | node | scalar profile / `P2-T05-spatial-energy-fission-v1` | Deferred / G4 |
| `spatial.fission_source` | `m^-3 s^-1` | node | scalar profile / `P2-T05-spatial-fission-source-v1` | Deferred / G4 |
| `spatial.edge_conductance` | `m^2` | edge/group | scalar profile / `P2-T05-spatial-edge-conductance-v1` | Deferred / G4 |
| `spatial.boundary_conductance` | `m^2` | node/face/group | scalar profile / `P2-T05-spatial-boundary-conductance-v1` | Deferred / G4 |
| `spatial.coefficient_invariant_sigma_a_ge_sigma_f` | `m^-1` | node/group | lower-bound invariant / `P2-T05-spatial-invariant-sigma-a-v1` | Deferred / G4 |
| `spatial.coefficient_invariant_chi_sum` | `1` | node | equality invariant / `P2-T05-spatial-invariant-chi-v1` | Deferred / G4 |
| `spatial.coefficient_invariant_fission_support` | `1` | node/group | support invariant / `P2-T05-spatial-invariant-fission-support-v1` | Deferred / G4 |
| `spatial.implied_neutron_yield` | `1` | node/group | scalar profile; `NotApplicable` when fission fields are zero / `P2-T05-spatial-implied-yield-v1` | Deferred / G4 |
| `spatial.coefficient_invariant_fission_product` | `m^-1` | node/group | conditional equality invariant; `NotApplicable` when nu_g is absent / `P2-T05-spatial-invariant-fission-v1` | Deferred / G4 |
| `spatial.conductance_binding` | bytes | edge/face/group | exact topology binding / `P2-T05-spatial-conductance-binding-v1` | Specified / G4 |
| `spatial.residual_absolute_inf` | `m^-3 s^-1` | solve | scalar profile / `P2-T05-spatial-residual-abs-v1` | Deferred / G4 |
| `spatial.residual_relative_inf` | `1` | solve | scalar profile / `P2-T05-spatial-residual-rel-v1` | Deferred / G4 |
| `spatial.delta_k_abs` | `1` | solve | scalar profile; NotApplicable when no consecutive k pair / `P2-T05-spatial-delta-k-abs-v1` | Deferred / G4 |
| `spatial.delta_k_rel` | `1` | solve | scalar profile; NotApplicable when no consecutive k pair / `P2-T05-spatial-delta-k-rel-v1` | Deferred / G4 |
| `spatial.source_shape_change_inf` | `1` | solve pair | `L_inf` / `P2-T05-spatial-shape-change-v1` | Deferred / G4 |
| `spatial.power_balance_rel` | `1` | solve | scalar profile / `P2-T05-spatial-power-balance-v1` | Deferred / G4 |
| `spatial.normalization_scale` | `1` | solve | scalar profile / `P2-T05-spatial-normalization-scale-v1` | Deferred / G4 |
| `spatial.normalization_source_totals` | `s^-1` | solve | two-component `L_inf` profile / `P2-T05-spatial-normalization-source-totals-v1` | Deferred / G4 |
| `spatial.normalization_power_totals` | `W` | solve | two-component `L_inf` profile / `P2-T05-spatial-normalization-power-totals-v1` | Deferred / G4 |
| `spatial.coefficient_identity` | bytes | node/group/table | exact table/digest / `P2-T05-spatial-coefficient-id-v1` | Specified / G4 |
| `spatial.interpolation_bracket` | bytes | node/group/table | exact bracket/result status / `P2-T05-spatial-interpolation-bracket-v1` | Specified / G5 |
| `spatial.interpolation_alpha` | `1` | node/group/table | scalar profile / `P2-T05-spatial-interpolation-alpha-v1` | Deferred / G5 |
| `spatial.convergence` | bytes | solve | exact policy/status / `P2-T05-spatial-convergence-v1` | Specified / G4 |
| `spatial.iteration_count` | integer | solve | exact count / `P2-T05-spatial-iteration-count-v1` | Specified / G4 |
| `spatial.inner_solve` | bytes | group solve | exact method/status/count / `P2-T05-spatial-inner-solve-v1` | Specified / G4 |
| `spatial.inner_residual_absolute_inf` | `m^-3 s^-1` | group solve | scalar profile / `P2-T05-spatial-inner-residual-abs-v1` | Deferred / G4 |
| `spatial.inner_residual_relative_inf` | `1` | group solve | scalar profile / `P2-T05-spatial-inner-residual-rel-v1` | Deferred / G4 |
| `burnup.current` | `J/kg_HM` | bundle | scalar profile / `P2-T05-burnup-current-v1` | Deferred / G5 |
| `burnup.energy` | `J` | bundle | scalar profile / `P2-T05-burnup-energy-v1` | Deferred / G5 |
| `burnup.energy_accounting` | `J/kg_HM` | bundle | equality invariant / `P2-T05-burnup-accounting-v1` | Deferred / G5 |
| `burnup.interval_power` | `W` | bundle/interval | scalar profile / `P2-T05-burnup-interval-power-v1` | Deferred / G5 |
| `burnup.interval_energy` | `J` | bundle/interval | scalar profile / `P2-T05-burnup-interval-energy-v1` | Deferred / G5 |
| `burnup.interval_energy_accounting` | `J` | bundle/interval | equality invariant / `P2-T05-burnup-interval-energy-accounting-v1` | Deferred / G5 |
| `burnup.mass` | `kg_HM` | bundle | scalar profile / `P2-T05-burnup-mass-v1` | Deferred / G5 |
| `burnup.monotonicity` | `J/kg_HM` | bundle/history | lower-bound invariant / `P2-T05-burnup-monotonic-v1` | Deferred / G5 |
| `burnup.coefficient_bracket` | bytes | bundle/lookup | exact bracket / `P2-T05-burnup-bracket-v1` | Specified / G5 |
| `burnup.coefficient_alpha` | `1` | bundle/lookup | scalar profile / `P2-T05-burnup-alpha-v1` | Deferred / G5 |
| `refuel.scheme` | bytes | event | exact / `P2-T05-refuel-scheme-v1` | Specified / G5 |
| `refuel.direction_binding` | bytes | event/channel | exact / `P2-T05-refuel-direction-v1` | Specified / G5 |
| `refuel.position_mapping` | bytes | event | exact complete mapping / `P2-T05-refuel-mapping-v1` | Specified / G5 |
| `refuel.atomicity` | `1` | event | exact commit/rollback / `P2-T05-refuel-atomicity-v1` | Specified / G5 |
| `refuel.history` | bytes | bundle/event | exact ordered history / `P2-T05-refuel-history-v1` | Specified / G5 |
| `kinetics.amplitude` | `1` | global/time | scalar profile / `P2-T05-kinetics-amplitude-v1` | Deferred / G7A |
| `kinetics.reference_power` | `W` | global | scalar profile / `P2-T05-kinetics-reference-power-v1` | Deferred / G7A |
| `kinetics.actual_flux` | `m^-2 s^-1` | node/group | scalar profile / `P2-T05-kinetics-actual-flux-v1` | Deferred / G7A |
| `kinetics.actual_node_power` | `W` | node | scalar profile / `P2-T05-kinetics-actual-node-power-v1` | Deferred / G7A |
| `kinetics.actual_total_power` | `W` | global | scalar profile / `P2-T05-kinetics-actual-total-power-v1` | Deferred / G7A |
| `kinetics.fission_rate_density` | `m^-3 s^-1` | node | scalar profile / `P2-T05-kinetics-fission-rate-v1` | Deferred / G7A |
| `kinetics.amplitude_flux_invariant` | `m^-2 s^-1` | node/group | equality invariant / `P2-T05-kinetics-amplitude-flux-invariant-v1` | Deferred / G7A |
| `kinetics.amplitude_node_power_invariant` | `W` | node | equality invariant / `P2-T05-kinetics-amplitude-node-power-v1` | Deferred / G7A |
| `kinetics.amplitude_total_power_invariant` | `W` | global | equality invariant / `P2-T05-kinetics-amplitude-total-power-v1` | Deferred / G7A |
| `kinetics.precursor` | `1` | global/group | scalar profile / `P2-T05-kinetics-precursor-v1` | Deferred / G7A |
| `kinetics.reactivity` | `1` | global | scalar profile / `P2-T05-kinetics-reactivity-v1` | Deferred / G7A |
| `kinetics.step` | bytes | time | exact policy/state binding / `P2-T05-kinetics-step-v1` | Specified / G7A |
| `kinetics.step_size` | `s` | time | scalar profile / `P2-T05-kinetics-step-size-v1` | Deferred / G7A |
| `kinetics.step_index` | integer | time | exact / `P2-T05-kinetics-step-index-v1` | Specified / G7A |
| `kinetics.integration_policy` | bytes | global/time | exact bounds/partition / `P2-T05-kinetics-policy-v1` | Specified / G7A |
| `xenon.I135_inventory` | atoms | bundle | scalar profile / `P2-T05-xenon-I-v1` | Deferred / G7A |
| `xenon.Xe135_inventory` | atoms | bundle | scalar profile / `P2-T05-xenon-Xe-v1` | Deferred / G7A |
| `xenon.production_decay_absorption` | atoms/s | bundle/interval | `L_inf` / `P2-T05-xenon-terms-v1` | Deferred / G7A |
| `xenon.atom_balance` | atoms | bundle/interval | equality invariant / `P2-T05-xenon-atom-balance-v1` | Deferred / G7A |
| `xenon.absorption_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-xenon-overlay-v1` | Deferred / G7A |
| `xenon.volume_binding` | bytes | bundle/node | exact binding identity / `P2-T05-xenon-volume-binding-v1` | Specified / G7A |
| `xenon.node_volume` | `m^3` | bundle/node | scalar profile / `P2-T05-xenon-node-volume-v1` | Deferred / G7A |
| `xenon.I135_number_density` | `m^-3` | bundle/node | scalar profile / `P2-T05-xenon-I-density-v1` | Deferred / G7A |
| `xenon.Xe135_number_density` | `m^-3` | bundle/node | scalar profile / `P2-T05-xenon-Xe-density-v1` | Deferred / G7A |
| `rrs.total_power_error` | `W` | global/time | scalar profile / `P2-T05-rrs-total-error-v1` | Deferred / G6 |
| `rrs.tilt_error` | `1` | region/time | scalar profile / `P2-T05-rrs-tilt-error-v1` | Deferred / G6 |
| `rrs.total_integral_error` | `W s` | global/time | scalar profile / `P2-T05-rrs-total-integral-v1` | Deferred / G6 |
| `rrs.tilt_integral` | `s` | region/time | scalar profile / `P2-T05-rrs-tilt-integral-v1` | Deferred / G6 |
| `rrs.actuator_command` | `1` | actuator/time | scalar profile / `P2-T05-rrs-command-v1` | Deferred / G6 |
| `rrs.actuator_state` | `1` | actuator/time | scalar profile / `P2-T05-rrs-state-v1` | Deferred / G6 |
| `rrs.actuator_command_event` | bytes | actuator/event | exact event identity/order / `P2-T05-rrs-command-event-v1` | Specified / G6 |
| `rrs.actuator_state_transition` | bytes | actuator/event | exact transition identity/order / `P2-T05-rrs-state-transition-v1` | Specified / G6 |
| `rrs.saturation` | `1` | actuator/event | exact diagnostic / `P2-T05-rrs-saturation-v1` | Specified / G6 |
| `feedback.rrs_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-feedback-rrs-overlay-v1` | Deferred / G6 |
| `feedback.optional_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-feedback-optional-overlay-v1` | Deferred / G7B |
| `feedback.disabled_zero` | `m^-1` | branch | exact zero / `P2-T05-feedback-disabled-v1` | Specified / G2 |
| `feedback.zone_grouping` | bytes | global | exact map / `P2-T05-feedback-zone-map-v1` | Specified / G6 |
| `feedback.poison_mass` | `kg` | global/time | scalar profile / `P2-T05-feedback-poison-mass-v1` | Deferred / G6 |
| `feedback.poison_mass_accounting` | `kg` | global/time | equality invariant / `P2-T05-feedback-poison-accounting-v1` | Deferred / G6 |
| `feedback.poison_concentration` | `kg/m^3` | global/time | scalar profile / `P2-T05-feedback-poison-concentration-v1` | Deferred / G6 |
| `feedback.moderator_volume` | `m^3` | global/time | scalar profile / `P2-T05-feedback-moderator-volume-v1` | Deferred / G6 |
| `feedback.zone_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-feedback-zone-overlay-v1` | Deferred / G6 |
| `feedback.adjuster_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-feedback-adjuster-overlay-v1` | Deferred / G6 |
| `feedback.poison_overlay` | `m^-1` | node/group | `L_inf` / `P2-T05-feedback-poison-overlay-v1` | Deferred / G6 |
| `feedback.branch_schema` | bytes | branch | exact schema/state identity / `P2-T05-feedback-branch-schema-v1` | Specified / G3 |
| `feedback.temperature_state` | `K` | branch | scalar profile / `P2-T05-feedback-temperature-v1` | Deferred / G7B |
| `feedback.purity_state` | `kg/kg` | branch | scalar profile / `P2-T05-feedback-purity-v1` | Deferred / G7B |
| `feedback.zone_fill_state` | `1` | branch | scalar profile / `P2-T05-feedback-zone-fill-v1` | Deferred / G6 |
| `feedback.adjuster_state` | `1` | branch | scalar profile / `P2-T05-feedback-adjuster-v1` | Deferred / G6 |
| `feedback.rrs_map_ownership` | bytes | source/map | exact one-owner certificate / `P2-T05-feedback-rrs-owner-v1` | Specified / G6 |
| `feedback.optional_map_ownership` | bytes | source/map | exact one-owner certificate / `P2-T05-feedback-optional-owner-v1` | Specified / G7B |
| `feedback.queue_cadence` | bytes | event/time | exact queue schema / `P2-T05-feedback-queue-v1` | Specified / G6 |
| `feedback.queue_cadence_time` | `s` | event/time | scalar profile / `P2-T05-feedback-queue-time-v1` | Deferred / G6 |
| `feedback.queue_order` | integer | event/time | exact queue sequence / `P2-T05-feedback-queue-order-v1` | Specified / G6 |
| `determinism.output_digest` | SHA-256 bytes | run | exact / `P2-T05-determinism-output-v1` | Specified / G3 |
| `determinism.repeat_equal` | bytes | run pair | exact / `P2-T05-determinism-repeat-v1` | Specified / G3 |
| `determinism.shuffle_invariant` | bytes | fixture | exact / `P2-T05-determinism-shuffle-v1` | Specified / G3 |
| `serialization.round_trip` | bytes | snapshot | exact / `P2-T05-serialization-roundtrip-v1` | Specified / G3 |
| `diagnostics.first_failure` | bytes | failed run | exact canonical order / `P2-T05-diagnostics-first-v1` | Specified / G3 |
| `diagnostics.nonconvergence` | bytes | failed solve | exact policy/evidence / `P2-T05-diagnostics-nonconvergence-v1` | Specified / G4 |
| `diagnostics.clamp_count` | integer | run | exact count by owner / `P2-T05-diagnostics-clamp-v1` | Specified / G3 |
| `diagnostics.forbidden_clamp` | integer | run | exact zero / `P2-T05-diagnostics-forbidden-clamp-v1` | Specified / G3 |
| `parser.dragon.kinf` | `1` | parser | exact lexeme/bytes / `P2-T05-parser-dragon-kinf-v1` | Specified / G3 |
| `parser.donjon.keff` | `1` | parser | exact lexeme/bytes / `P2-T05-parser-donjon-keff-v1` | Specified / G3 |
| `parser.manifest.file_digest` | bytes | provenance | exact path/length/SHA / `P2-T05-parser-manifest-v1` | Specified / G3 |

The parser-domain IDs are intentionally distinct from `spatial.k` and any
runtime quantity. They cannot be promoted to a runtime-direct or golden
comparison without an approved mapping, unit/normalization proof, and gate.

## Reference coverage and provenance

### Coverage classes and artifact availability

Every reference comparison declares the independent fields
`CoverageClass`, `ArtifactAvailability`, `EvidenceApproval`, and
`ValidationDomain`:

- `CoverageClass` is `Direct` when the source contains the same observable
  quantity under a documented unit and normalization; `Indirect` when it only
  supplies provenance or parser evidence; `Synthetic` for an explicitly labeled
  hand-worked fixture; or `NotCovered` when no source evidence exists. Coverage
  describes semantic quantity identity, not approval.
- `ArtifactAvailability` is `CommittedSynthetic`, `ExternalApproved`,
  `PrivateExternal`, or `Absent`. `PrivateExternal` describes handling, not
  semantic coverage. A candidate parser observation may therefore be
  `Direct + PrivateExternal`, but it may not be a release/golden pass and may
  not use `EvidenceApproval=Approved` without an explicit redistribution
  approval.
- `EvidenceApproval` is `Candidate`, `NotApproved`, or `Approved`; G1's
  compact outputs are `NotApproved` candidate evidence.
- `ValidationDomain` is `Parser`, `Runtime`, or `Provenance`. A parser-direct
  result is not a runtime-direct result merely because both values are called
  `k` or `K-effective`.

`Indirect`, `Synthetic`, or `NotCovered` records must not be mislabeled as
`Direct` golden comparisons. `ReferenceId`, source version, source digest,
license/redistribution status, and all four fields above are required in every
comparison record.

### P1 reference map

| Reference evidence | QuantityId / domain | CoverageClass | ArtifactAvailability | EvidenceApproval | Permitted use |
| --- | --- | --- | --- | --- | --- |
| P1-T05 DRAGON compact export | `parser.dragon.kinf` / Parser | Direct | PrivateExternal | NotApproved (candidate) | parser T2 and provenance audit only |
| P1-T06 DONJON compact export | `parser.donjon.keff` / Parser | Direct | PrivateExternal | NotApproved (candidate) | parser T2 and provenance audit only |
| P1-T07 manifests/repeat records | `parser.manifest.file_digest` / Provenance | Direct | PrivateExternal | NotApproved (candidate) | workflow determinism audit only |
| P1 case metadata | case labels/settings / Provenance | Indirect | CommittedSynthetic | NotApproved | fixture/provenance context only |
| DRAGON/DONJON raw inputs/listings | no runtime `QuantityId` / Provenance | NotCovered | PrivateExternal | NotApproved | optional local checks only; never committed |
| P1 compact exports | burnup, I/Xe, RRS, tilt, thermal, refuelling history | NotCovered | PrivateExternal | NotApproved | no inference permitted |

The map intentionally does not turn the three DRAGON observations or the one
DONJON smoke observation into a full-core solver baseline. A new direct mapping
requires an approved specification, immutable data identity, unit/normalization
proof, and the relevant gate; it cannot be inferred from a private artifact.

### Reference comparison record

A reference comparison records source tool/version, compact artifact identity,
parser/schema version, source lexeme/digest, units, normalization, quantity ID,
validation domain, coverage class, artifact availability, evidence approval,
profile status, and result. Raw DRAGON5/DONJON5 inputs, private paths, listings, and host-local
locations must remain external/private. A missing license or redistribution
boundary is a stop condition. `EvidencePath` in a committed record is a
synthetic relative path or an external artifact label, never a host-local
private path.

### P1-T08 literature digest crosswalk and case admission

Every physics-related task after P1-T08 must read the committed
`candu-literature-digest-v1` evidence index and record its digest version plus
the applicable claim row IDs. When no claim applies, the task records a concrete
`NotApplicable` coverage reason. A citation is an evidence pointer, not an
approval and not a replacement for an approved equation, unit, normalization,
tolerance, schema, reference input, or golden value.

If a task proposes to use literature to create or change a DRAGON5/DONJON5 case,
the task record must contain a complete case-admission proof for every cited
candidate numeric claim:

1. exact source program, version, build/commit, and coupling-tool identity;
2. nuclear-data library, version, processing/format, and checksum;
3. geometry, topology, mesh, group structure, and homogenization mapping;
4. state, depletion/refuelling history, initial conditions, and event ordering;
5. authoritative units and all boundary conversions;
6. normalization, power/flux/reference-state definition, and sign convention;
7. output quantity identity, schema, ordering, and comparison rule;
8. independent reproduction or traceable-equivalence evidence, including
   convergence/statistical information where applicable; and
9. license and redistribution authority for every input, output, and artifact.

Missing information is recorded as `NotReported` or `NotProven` and blocks
`EvidenceApproval=Approved` and any golden-case use. Source-reported values may
remain `Candidate` evidence with a named gap list. Safety, full thermal-
hydraulics, SCWR, thorium, accident-tolerant-cladding, and advanced-cycle rows
are `ContextOnly` unless a later task explicitly authorizes a bounded scope.
The P1-T08 digest cannot override this specification or silently promote a
published number into runtime behavior.

The committed digest and manifest are evidence records; source PDFs, raw
listings, private paths, and unapproved publisher artifacts remain external.
Later case reports must identify the external artifact label and SHA-256 without
committing a host-local path.

## Validation tiers and commands

The cheapest applicable tier runs first; a higher tier does not erase a failed
lower tier.

| Tier | Purpose | Required evidence |
| --- | --- | --- |
| T0 | scope/format/schema/static contract | paths, lengths, SHA, links, fences, units, required terms |
| T1 | focused synthetic/hand-worked checks | exact command, fixture IDs, counts, arithmetic/status |
| T2 | parser/reference compact workflow | exact fixture/output/digest/lexeme checks |
| T3 | headless build/test/determinism suite | build output, test counters, failure/skips |
| T4 | core/Unity adapter or serialization contract | adapter import/serialization evidence |
| T5 | platform/mobile-sensitive behavior | device/platform evidence |
| T6 | fresh offline reference rerun | external source/version/license/output hashes |

P2-T05 itself requires T0/T1 static checks, the reference-coverage audit, T3
because this task changes the tolerance contract and deterministic reporting,
and the actual T4 Unity EditMode/PlayMode plus desktop-player smoke suites
because it defines a serialization contract. The static byte/tag/layout check
is T0/T1 evidence, not a substitute for T4. If the pinned Unity editor is not
available, T4 is explicitly deferred and P2-T05 cannot claim complete gate
evidence. T6 is required only when a reference baseline, raw input, or
generated reference output changes. T5 is not implied.

### Focused T1 methodology

The P2-T05 focused check must verify, using committed synthetic fixtures only:

1. Every catalog quantity has a unit, scope, comparison rule, and profile status.
2. The one-row-per-`QuantityId` crosswalk has no missing, duplicate, or
   mismatched unit/scope/profile rows.
3. Exact records reject missing/extra/reordered IDs and event entries.
4. Zero-reference scalar comparisons require an explicit absolute rule or
   positive reference scale.
5. Vector components use canonical order and reject non-finite/duplicate data;
   aligned trajectories reject missing times and implicit interpolation.
6. Scalar/vector/invariant reductions produce finite errors and apply explicit
   pass inequalities, including directional lower-bound violations.
7. Energy, node/total/regional power, slot-count, coefficient, chi, conductance,
   `delta_k_abs`, `delta_k_rel`, amplitude-coupling, atom/poison accounting,
   and disabled-zero invariants report independent observable records; fission
   support is always checked and the fission-product invariant is
   `NotApplicable` when `nu_g` is absent.
8. Repeated canonical serialization produces identical digest/bytes while
   excluding host paths and environment values; queue round trips preserve the
   allocator, requested/available/physical state split, motion time, pending
   order, owner binding, and command/queue digests.
9. Reference coverage labels do not claim unsupported burnup, kinetics, RRS,
   thermal, or full-core observables.
10. The observable and tolerance byte-code tables have unique tags, ordinals,
    widths, field order, and explicit deferred/not-applicable encodings.
11. A delayed-command fixture proves exact due-time scheduling, same-time
    zero-delay consumption, monotonic sequence allocation and deterministic
    command ID derivation, causal motion under the command available before the
    interval, and byte-for-byte rollback for rejected enqueue/consume batches.

No private/raw reference path may be a required parameter in a committed test.
An optional private path, if used in an external local check, must be an
explicit command parameter and the report must state truthfully whether it ran.

## Synthetic methodology examples

### Exact determinism

Run the same synthetic state, commands, tables, maps, and cadence twice. If the
ordered output digests are `D1` and `D2`, the observable is `Pass` only when
`D1 == D2` byte-for-byte. A different map input order must still produce the
same digest after canonical sorting; a different ID or event order must fail.

### Delayed-command causality and rollback

Use the four synthetic queue fixtures in the P2-T04 specification. The
observable set must separately record command generation, queue before/after
state, physical transition, available command, allocator, both ID registries, due
batch, and rollback digest. In the zero-delay fixture, physical state at
`t=10 s` is `0.2` while the newly available command is `0.9`; reporting
physical state `0.9`, or applying any part of the new command over `[8 s,10 s]`,
is `Fail`. The allocator-overflow fixture passes only when every authoritative
pre/post byte is equal and the first canonical diagnostic identifies allocator
capacity. Input permutation must preserve deterministic command IDs and queue
digest after canonical ordering. Both the applied-source-event and
allocated-command ID registries are compared exactly.

### Power and regional fractions

For synthetic node powers `[2 W, 3 W, 5 W]`, `P_total=10 W`. A region containing
the first two canonical nodes has `P_region=5 W` and fraction `0.5`. The catalog
records the three node powers, total, region total, fraction, and sum-to-total
invariant separately. It does not substitute the fraction for power.

### Zero-reference comparison

For a synthetic reference `r=0` and actual `x=0.0001 m^-1`, a relative-only
profile is invalid because no positive `ReferenceScale` exists. An explicit
absolute profile in `m^-1` may diagnose the result; its provisional status is
reported. The check never invents an epsilon.

### Reference limitation

The P1 compact DRAGON and DONJON records may produce `Direct` parser observables
for their documented output fields. A proposed Xe inventory or refuelling
history from the same compact files is `NotCovered`, even if a private listing
happens to contain suggestive text.

## Fail-closed reporting and gate handoff

Each validation run produces a machine-readable summary and a human-readable
report containing command, working directory, external artifact root, input
digests, output digests, counts, profiles, coverage classes, statuses, and first
failure. It must distinguish:

- implementation failure versus missing/deferred evidence;
- first-pass gate status versus final corrected status;
- provisional tolerance use versus approved tolerance use; and
- parser/reference evidence versus runtime validation.

An observable failure, missing profile, unsupported reference claim, numerical
non-determinism, NaN/Inf, unit mismatch, failed invariant, or unproven
normalization blocks completion of the affected gate. The report must identify
the next eligible task and any required escalation. No failure is hidden by
loosening a profile, replacing golden data, or marking unsupported evidence as
covered.

## Definition of done for P2-T05

- Every P2-T01 through P2-T04 state/equation/transition family has named
  observables, scope, authoritative unit, sampling binding, comparison rule,
  and profile status.
- Exact, scalar, vector, invariant, conservation, and deterministic comparison
  rules are explicit, including zero-reference behavior.
- Quantity-specific provisional profile ownership/status is explicit and does
  not pretend to be G4-approved.
- P1 reference coverage is mapped without inferring unsupported burnup, kinetics,
  feedback, thermal, or refuelling values and without committing private/raw
  artifacts.
- T0 through T6 applicability and focused T1 evidence requirements are explicit.
- Synthetic examples demonstrate deterministic, regional, zero-reference, and
  reference-coverage behavior.
- No runtime implementation, tolerance relaxation, golden-data replacement,
  Unity dependency, private path, or reference publication is introduced.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md), Phase 2,
P2-T05, and the approved P2-T01 through P2-T04 specifications.
