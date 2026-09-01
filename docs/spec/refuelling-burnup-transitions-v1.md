# Refuelling and burnup transitions v1

## Status and scope

This document is the engine-neutral mathematical and state-transition contract
for P2-T03. It defines the minimum v1 behavior for an explicit refuelling shift,
bundle identity movement, discharge/insertion boundaries, burnup integration, and
interpolation of burnup-indexed lattice coefficients.

It does not implement a solver, define Unity behavior, select a named-station
refuelling schedule, add shutdown/scram behavior, or reproduce a DRAGON5/DONJON5
workflow. Kinetics, xenon, RRS, temperature, purity, and feedback state are
deferred to later specifications. The P2-T02 two-group solver consumes the
coefficient values selected by this contract; it does not own bundle movement or
burnup history.

All examples and numeric values in this document are explicitly synthetic
contract examples. They are not copied from private/raw listings, DRAGON5,
DONJON5, or a golden dataset.

## Normative terms and authoritative units

The words **must**, **must not**, and **may** are normative. Authoritative
arithmetic uses finite IEEE-754 `double` values and explicit SI units. A value
with a missing, incompatible, or ambiguous unit is invalid input.

| Quantity | Meaning | Unit |
| --- | --- | --- |
| `t` | simulation time | `s` |
| `Delta_t` | positive elapsed simulation interval | `s` |
| `B_b` | bundle burnup, energy per heavy-metal mass | `J/kg_HM` |
| `E_b` | integrated fission energy assigned to a bundle | `J` |
| `m_HM,b` | heavy-metal mass represented by bundle `b` | `kg_HM` |
| `P_b` | bundle fission power | `W` (`J/s`) |
| `P_i` | P2-T02 integrated node fission power | `W` |
| `V_i` | solver node volume | `m^3` |
| `Sigma_*` | macroscopic cross section or transfer coefficient | `m^-1` |
| `E_f` | energy released per fission used by P2-T02 | `J` |
| `alpha` | interpolation fraction | dimensionless (`1`) |
| `BundleId` | stable bundle identity | opaque identifier |
| `CommandId` | stable command identity | opaque identifier |

`MWd/t_HM` is a display unit only. If it is accepted at an external boundary,
it must be converted once to the authoritative `J/kg_HM` value before state
validation. The exact relationship used for that conversion is:

```text
1 MWd/t_HM = 86,400,000 J/kg_HM
```

No burnup value may be compared or interpolated before unit normalization.

## Topology and bundle state

### Runtime node identity

The v1 production node key is the P2-T01 pair
`(ChannelId, BundlePosition)`. `ChannelId` and `BundlePosition` are zero-based;
the production model has 380 channels, 12 positions per channel, and 4,560
channel-position slots. Position `0` is `EndA`; position `N-1` is `EndB`. A
future aggregate or sub-bundle mesh must provide an approved mapping and may
not silently split or combine bundle power in this task.

Each occupied v1 slot contains exactly one `BundleState`:

| Field | Contract | Unit |
| --- | --- | --- |
| `BundleId` | stable and globally unique while retained in the scenario history | opaque |
| `ChannelId` | valid P2-T01 channel identifier | dimensionless |
| `BundlePosition` | valid position in that channel | dimensionless |
| `MaterialVariantId` | key into the approved burnup coefficient library | opaque |
| `InitialBurnup` | burnup at insertion, retained as an immutable origin value | `J/kg_HM` |
| `CumulativeFissionEnergy` | authoritative fission energy accumulated since insertion | `J` |
| `B` | derived current burnup `InitialBurnup + CumulativeFissionEnergy/m_HM` | `J/kg_HM` |
| `m_HM` | finite, strictly positive mass supplied by the validated fuel template | `kg_HM` |
| `InsertedAt` | simulation time at which this identity entered the core | `s` |
| `ResidenceTime` | derived `current_time - InsertedAt` | `s` |
| `Power` | latest accepted power snapshot for this node, or unset after a state transition | `W` |
| `PowerSnapshotId` | exact snapshot binding for `Power`, or unset after a state transition | opaque |
| `PowerHistory` | append-only accepted `(SnapshotTime, Power, CoreStateVersion, SpatialStateVersion, PowerSnapshotVersion)` records | `s`, `W`, integer |
| `CoefficientTableId` | table identity used for the current burnup lookup | opaque |
| `CoefficientBracket` | diagnostic bracket used for the current coefficient lookup | indices |
| `StateVersion` | core state version at which this bundle row was last committed | integer |
| `I135AtomInventory` / `Xe135AtomInventory` | authoritative I-135/Xe-135 atom inventories owned by this persistent bundle identity | atoms |
| `InitialI135` / `InitialXe135` | immutable explicit initial atom inventories for this bundle identity | atoms |
| `NodeVolume` | validated P2-T02 control volume for the current slot | `m^3` |
| `I135NumberDensity` / `Xe135NumberDensity` | derived atom inventories divided by the current `NodeVolume`; never independently mutated | `m^-3` |
| `NuclideStateVersion` | per-bundle accepted I/Xe transaction counter from `VersionLifecycleV1` | integer |
| `NuclideDataId` / `NuclideDataDigest` | exact I/Xe yield, decay, cross-section, and table identity | opaque |
| `I135XeHistory` / `NuclideHistoryDigest` | append-only accepted I/Xe transition history and its canonical digest | structured / bytes32 |

`B` is derived, not independently mutated. For every live or discharged bundle,
the authoritative energy-accounting invariant is:

```text
B = InitialBurnup + CumulativeFissionEnergy / m_HM
CumulativeFissionEnergy >= 0 J
```

`Power` is not an independent physical input. After a successful P2-T02 solve,
the v1 node mapping is:

```text
P_b = P_(ChannelId, BundlePosition)
```

The mapping is one bundle to one P2-T02 node. A missing, duplicate, or
ambiguous mapping fails closed.

`ResidenceTime` is derived and must be finite and nonnegative. Moved bundles
retain their `InsertedAt` and complete append-only `PowerHistory`; a newly
inserted bundle starts with an empty history, and a discharged bundle retains
the final history in its immutable discharge record. The core state also owns a
`CoreStateVersion` unsigned integer that increments exactly once for each
committed burnup interval or committed refuelling batch and never increments on
rejection. A state version is part of every snapshot and history record. Its
initial value is the explicit `InitialCoreStateVersion` in the scenario's
`VersionLifecycleV1` manifest; it is not inferred or silently reset to zero.

### Cross-spec version lifecycle

`VersionLifecycleV1` is the shared lifecycle contract for the P2-T03,
P2-T04, and P2-T05 specifications. It contains four kinds of unsigned,
fixed-width `UInt64` counters:

| Counter | Scope and owner | Explicit initial value | The only successful increment |
| --- | --- | --- | --- |
| `CoreStateVersion` | one global committed P2-T03 state epoch | `InitialCoreStateVersion` in the scenario manifest | exactly once when one burnup interval or one complete refuelling batch commits |
| `SpatialStateVersion` | one global accepted P2-T02 spatial-solve epoch | `InitialSpatialStateVersion` in the scenario manifest | exactly once when one P2-T02 spatial solve and its bound power snapshot are accepted atomically |
| `PowerSnapshotVersion` | one global accepted-power-snapshot epoch | `InitialPowerSnapshotVersion` in the scenario manifest | exactly once with the accepted P2-T02 power snapshot that owns that solve |
| `NuclideStateVersion` | one counter on each persistent bundle identity | explicit per-bundle scenario value or the resolved fresh-fuel template value | exactly once for that bundle when its I-135/Xe-135 inventory transaction commits |

The manifest supplies every initial value explicitly, together with its schema,
data-pack, topology, and digest identities. Initial values may be nonzero (the
synthetic P2-T03 histories use `CoreStateVersion=100`); a missing, duplicated,
non-integer, negative, or incompatible initial value fails closed. The initial
values are immutable provenance fields and the corresponding current counters
start equal to them. No value is copied from a reference listing, inferred from
array order, or chosen as a hidden default. A fresh inserted bundle receives
the explicit version from its resolved fuel template; an existing bundle keeps
the explicit version carried in its serialized state.

The counters are epochs, not validity flags. `VersionLifecycleV1` separately
stores `SpatialBindingStatus` and `PowerBindingStatus`, each exactly `Valid` or
`Invalid`, plus the accepted solve/snapshot identity and digests when valid. An
invalid binding retains its last committed counter for audit but cannot be used
to advance time or produce a runtime value. P2-T05 encodes every invalid or
non-applicable binding member as the explicit `NotApplicable` tag; zero is a
real counter value and is never used as an invalid sentinel.

The lifecycle transitions are:

| Operation | Version and binding result |
| --- | --- |
| Scenario initialization | Set every current counter equal to its explicit initial value. Set both spatial and power binding statuses to `Invalid` unless an accepted snapshot is supplied with a complete matching binding. Validate all per-bundle nuclide versions before the first solve. |
| Accepted P2-T02 solve | Preflight the current `CoreStateVersion`, nuclide versions, topology/data-pack versions, coefficient digest, and exact time. Increment `SpatialStateVersion` and `PowerSnapshotVersion` once each in one atomic commit, create the `SpatialSolveId` and `PowerSnapshotId`, set both binding statuses to `Valid`, and serialize the same post-increment tuple everywhere. |
| Rejected, stale, nonconverged, or non-finite solve | Increment neither spatial nor power counter, append no power history, and leave the prior lifecycle state byte-for-byte unchanged. A candidate with any mismatched version or digest is unusable. |
| Accepted burnup interval | Commit all proposed bundle energy/burnup values, increment `CoreStateVersion` once, and invalidate both spatial and power bindings. Spatial and power counters do not increment merely because they were invalidated. |
| Accepted refuelling batch | Commit the complete batch, increment `CoreStateVersion` once, invalidate both spatial and power bindings, preserve each moved bundle's `NuclideStateVersion`, and assign each inserted bundle its explicit template version. |
| Accepted I/Xe integration | Increment `NuclideStateVersion` exactly once for every affected bundle, invalidate spatial and power bindings because the absorption state changed, and leave `CoreStateVersion` unchanged unless a separate P2-T03 transaction also commits. |
| Bundle movement or discharge | Move the persistent bundle's nuclide counter and inventories unchanged; retain the counter in the discharge record. The refuelling transaction's `CoreStateVersion` is the binding for the new location. |
| Failed or rolled-back transaction | Restore every counter, validity status, identity, digest, history, event, and command-applied marker from the transaction's pre-state. No partial increment or invalidation survives a failure. |

An accepted P2-T02 solve is the only operation that advances either spatial or
power snapshot version. If the same core state is solved again, the new solve
still receives new spatial and power versions; equal time does not make the
results interchangeable. A committed burnup, refuelling, nuclide, or other
authoritative coefficient/input transition invalidates any bound solve and
power snapshot before the next positive-duration interval. The invalidation
does not decrement or reuse a counter. The next accepted solve must bind the
new current state and receives the next versions.

Every transaction's scratch state includes the complete `VersionLifecycleV1`
object. Therefore a failed postcondition, failed coefficient lookup, failed
nuclide validation, failed solve, or rejected command restores versions and
binding state together with inventory, energy, coefficient bindings, event
log, histories, and the command-applied set. A committed transaction never
decrements a counter; rollback restores the prior transaction boundary rather
than applying a compensating increment.

### I/Xe ownership in refuelling state

The P2-T04 `NuclideState` is part of the P2-T03 authoritative `BundleState`; it
is not a side table inferred from a slot, material name, or array position. The
following layouts are versioned logical payloads inside the existing P2-T05
event and state envelopes; they do not add a new observable kind.

`NuclideTransitionRecordV1` has this fixed field order and type:

| Field | Type / encoding |
| --- | --- |
| `OwnerEventId` / `RecordId` | canonical `Uuid16` event identity / canonical `Uuid16` derived from the exact owner/event/bundle/sequence bytes; never random |
| `RecordSequence` / `EventRank` | `UInt64` / `UInt16` |
| `EventTime` / `DeltaTime` | `Float64` / `Float64`; exact simulation seconds, with `DeltaTime=0` forbidden for an integration record |
| `BundleId` | canonical bundle identity bytes |
| `CoreStateVersionBefore` / `CoreStateVersionAfter` | `UInt64` / `UInt64OrNA` |
| `NuclideStateVersionBefore` / `NuclideStateVersionAfter` | `UInt64` / `UInt64` |
| `NodeVolume` | `Float64`, `m^3`, finite and strictly positive |
| `I135AtomInventoryBefore` / `I135AtomInventoryAfter` | `Float64`, atoms |
| `Xe135AtomInventoryBefore` / `Xe135AtomInventoryAfter` | `Float64`, atoms |
| `I135NumberDensityBefore` / `I135NumberDensityAfter` | `Float64`, `m^-3`, derived from the corresponding inventory and volume |
| `Xe135NumberDensityBefore` / `Xe135NumberDensityAfter` | `Float64`, `m^-3`, derived from the corresponding inventory and volume |
| `I135DirectProduction` / `I135DecayLoss` | `Float64`, atoms/s; direct I source is nonnegative and I decay loss is nonpositive |
| `Xe135DirectProduction` / `Xe135FromI135Decay` / `Xe135DecayLoss` / `Xe135AbsorptionLoss` | `Float64`, atoms/s; direct and decay-source terms are nonnegative and loss terms are nonpositive |
| `NuclideDataId` / `NuclideDataDigest` | length-prefixed UTF-8 identity / `Bytes32` |
| `StateBindingDigest` | `Bytes32OrNA`, binding the exact spatial/flux input; an accepted integration record requires `Bytes32`, while a non-runtime diagnostic uses explicit `NotApplicable` |
| `RecordDigest` | `Bytes32`, SHA-256 of the complete preceding record body |

The record body is encoded with the P2-T05 fixed-width encodings, a `UInt32`
schema version and `UInt32`-counted fields where an array is present. Its
`RecordId` is derived exactly as UUIDv8 from the first 16 bytes of SHA-256 over
`ASCII "CANDU-NUCLIDE-TRANSITION-ID-V1"`, the P2-T05 zero separator,
`OwnerEventId`, `BundleId`, and `RecordSequence` encoded in that order; the
UUID version nibble and RFC-4122 variant bits use the P2-T05 rule. Its
`RecordDigest` is SHA-256 over `ASCII "CANDU-NUCLIDE-TRANSITION-V1"`, the P2-T05
zero separator, schema version, and every preceding field in order; the digest
is not included in its own input. Records in `I135XeHistory` are a
`UInt32`-counted array sorted by `(EventTime, EventRank, RecordSequence,
RecordIdBytes)` and no record may be inserted, removed, or reordered during a
refuelling move.

`NuclideHistoryDigest` is SHA-256 over `ASCII
"CANDU-NUCLIDE-HISTORY-V1"`, the zero separator, schema version, owning
`BundleId`, the array count, and the complete ordered record bytes. The empty
history has the same defined input with count zero; it is a real digest, not an
omitted field. `NuclideStateEnvelopeV1` then has the fixed order:
`ASCII "CANDU-NUCLIDE-STATE-V1"`, zero separator, schema version,
`BundleId`, `I135AtomInventory`, `Xe135AtomInventory`, `InitialI135`,
`InitialXe135`, `NodeVolume`, `I135NumberDensity`, `Xe135NumberDensity`,
`NuclideStateVersion`, `NuclideDataId`, `NuclideDataDigest`, the counted
`I135XeHistory`, and `NuclideHistoryDigest`. `NuclideStateDigest` is SHA-256 of
those envelope bytes and is excluded from them. Number densities are serialized
derived values: validation recomputes them from the atom inventories and the
current positive volume and rejects any mismatch; they are never an
independent source of state.

The complete P2-T03 `BundleStateV1` projection has no wildcard tail. Its fixed
order is `BundleId`, `ChannelId`, `BundlePosition`, `MaterialVariantId`,
`InitialBurnup`, `CumulativeFissionEnergy`, derived `B`, `m_HM`, `InsertedAt`,
derived `ResidenceTime`, optional `Power`, optional `PowerSnapshotId`, counted
`PowerHistory`, `CoefficientTableId`, `CoefficientBracket`, `StateVersion`,
and the complete `NuclideStateEnvelopeV1`. Optional values use the P2-T05
explicit applicability tags. Live bundle arrays are sorted by
`(ChannelId, BundlePosition, BundleIdBytes)`.

For a moved bundle, the atom inventories, initial inventories, nuclide version,
data identity, and complete I/Xe history are copied byte-for-byte. Only the
destination `NodeVolume` changes, and the two densities are recomputed from the
unchanged atom inventories. A discharge retains the final envelope. A fresh
inserted bundle receives the explicit template envelope, including its initial
inventories, template `NuclideStateVersion`, `NuclideDataId`, and
`NuclideDataDigest`; no equilibrium, hidden zero, or reference-listing value is
introduced. The zero-duration refuelling instant performs no I/Xe integration.

The immutable discharge payload is `DischargeRecordV1` with fixed order:
`ASCII "CANDU-DISCHARGE-RECORD-V1"`, zero separator, schema version,
`BundleId`, `ChannelId`, `BundlePosition`, `MaterialVariantId`, `InitialBurnup`,
`CumulativeFissionEnergy`, `m_HM`, `InsertedAt`, `DischargedAt`,
`DischargeEnd` (`EndA=0` or `EndB=1`, `UInt8`), `CoefficientTableId`,
`CoefficientBracket`, `StateVersion`,
`CoreStateVersionAtDischarge`, `CommandId`, `PowerHistory`, the complete
`NuclideStateEnvelopeV1` at the discharge volume, and `RecordDigest`. Array
fields are `UInt32`-counted and sorted by their declared canonical keys;
`RecordDigest` is SHA-256 over every preceding field and is not self-included.
The discharge envelope is therefore a complete final state, not only a
`NuclideStateDigest` reference.

The refuelling batch projection is `RefuelBatchV1`. Its fixed order is
`ASCII "CANDU-REFUEL-BATCH-V1"`, zero separator, schema version, `BatchId`,
`EffectiveTime`, `CommandCount:UInt32`, and the complete sorted
`RefuelShiftCommandV1` array. Each command body has fixed order `CommandId`,
`Sequence`, `EffectiveTime`, `ChannelId`, `SchemeId`, `ShiftDirection`,
`InsertedBundleSpecs` (each complete template envelope), `SchemaVersion`,
`DataPackVersion`, and `InsertedAt`. The commands are sorted by
`(Sequence, CommandIdBytes)` and the batch ID is the UUIDv8 value derived from
the first 16 bytes of SHA-256 over `ASCII "CANDU-REFUEL-BATCH-ID-V1"`, the
zero separator, effective time, command count, and every complete sorted
command byte. A batch never uses one singular command's fields as its digest
projection.

`RefuelTransactionV1` has fixed order `ASCII
"CANDU-REFUEL-TRANSACTION-V1"`, zero separator, schema version, `BatchId`,
`RefuelBatchV1` bytes, `CurrentTimeBefore`, `CoreStateVersionBefore`,
`CoreStateVersionAfterOrNA`, the complete `VersionLifecycleV1` before/after
tuple, sorted `BundleStateV1` before and proposed arrays, sorted
`DischargeRecordV1` array, sorted applied-command identities, sorted committed
event identities, `CommitStatus` (`Committed=0`, `RolledBack=1`, `Rejected=2`,
`NotApplicable=255`), and the first canonical rejection diagnostic or explicit
`NotApplicable`. `BeforeDigest` hashes the complete pre-state
projection plus the complete batch bytes; `ProposedDigest` hashes the complete
prepared post-state projection plus the same batch bytes. If preflight rejects
before a normal post-state can be prepared, the proposed projection is still a
defined `RejectedProposedStateV1`: the unchanged pre-state projection, the
complete batch bytes, `CommitStatus=Rejected=2`, and the first canonical
diagnostic. If a postcondition fails after a prepared proposed projection was
formed, the same deterministic result uses `CommitStatus=RolledBack=1` instead
of `Rejected=2`; both statuses leave the live projection unchanged and retain
the unchanged before digest.
It is not live state and is never committed. Thus every rejected
`RefuelAtomicityBodyV1` has a deterministic required `ProposedDigest`, and
`UnchangedDigestOrNA` equals the pre-state digest.

The fixed P2-T05 `RefuelMappingBodyV1` has no `CommandId`; its outer
`EventV1.EventId` is the canonical command identity and links it one-to-one to
the `RefuelAtomicityBodyV1.CommandId`. A rejected operation returns one
mapping/atomicity diagnostic pair per command, in batch order, but appends no
committed event. All pairs share the batch before/proposed digest. All strings,
UUIDs, `UInt64` counters, `Float64` values, optional tags, and arrays use the
P2-T05 encodings above. No implementation may replace one of these fixed lists
with a map, unordered collection, or omitted-field projection.

The cross-spec conformance matrix is normative:

| Concern | P2-T03 refuelling contract | P2-T04 kinetics/Xe contract |
| --- | --- | --- |
| Ownership | `BundleState` owns one complete I/Xe envelope for each persistent `BundleId` | `NuclideState` is attached to that same persistent `BundleId`; no duplicate owner exists |
| Moved bundle | inventories, initial values, version, data identity, and history move unchanged; densities use destination volume | atom inventories/version/history move unchanged and densities are recomputed from the new volume |
| Fresh insertion | validated fuel template supplies equal current/initial I/Xe fields, version, data identity, empty history, and explicit empty-history digest | template values are explicit; no inferred equilibrium or hidden reset is legal |
| Discharge | immutable discharge record retains the final envelope and its canonical digest | final inventories, densities, version, data identity, and I/Xe history remain auditable |
| Commit/version | refuel increments `CoreStateVersion` once, but does not increment a moved bundle's `NuclideStateVersion` | only an accepted I/Xe integration increments an affected bundle's nuclide version |
| Atomicity | preflight, proposed post-state, event body, and rollback cover the complete envelope | P2-T04 state/version/history bindings are inputs to the P2-T03 transaction |
| Invalid binding | missing, duplicate, stale, reordered, non-finite, or digest-incompatible I/Xe state rejects before mutation | save/load/replay and event validation fail closed on the same conditions |

The two specifications must agree on every row before the transaction is
eligible for implementation.

The serialized lifecycle envelope is canonical and versioned. Its fixed logical
order is: lifecycle schema/version identity; immutable initial counters;
current `CoreStateVersion`, `SpatialStateVersion`, and
`PowerSnapshotVersion`; canonical `(BundleId, NuclideStateVersion)` entries;
binding statuses; accepted solve/snapshot IDs; and their state, coefficient,
topology, data-pack, and snapshot digests. Global entries precede bundle
entries, and bundle entries are sorted by canonical `BundleId` bytes. Each
counter is encoded as the P2-T05 `UInt64`; status and applicability use the
closed enum/tag encodings there. Save/load rejects missing, duplicate,
reordered, stale, or internally inconsistent lifecycle fields. Replay records
the complete before/after version tuple for every committed transition and
rejects any command whose before tuple does not equal the live tuple or whose
after tuple does not match the specified single-increment rule.

### Channel state and direction

Each channel has a fixed P2-T01 topology record and an explicit flow-direction
enum. V1 binds refuelling direction to that declared endpoint direction:

```text
flow_direction = EndAtoEndB -> ShiftDirection = TowardEndB
flow_direction = EndBtoEndA -> ShiftDirection = TowardEndA
```

A command carries `ShiftDirection` for audit, but the value must equal this
binding. A command cannot choose the opposite direction, and direction is never
inferred from array order, channel number, or alternating parity.

For a channel with `N` positions, the insertion and discharge rules are:

| `ShiftDirection` | New bundle enters | Bundle discharged | Existing position `p` moves to |
| --- | --- | --- | --- |
| `TowardEndB` | `EndA` / position `0` | old position `N-1` | `p + 1` for `0 <= p < N-1` |
| `TowardEndA` | `EndB` / position `N-1` | old position `0` | `p - 1` for `1 <= p < N` |

The single-position table above is the `m=1` illustration only. Production v1
uses a declarative `RefuelSchemeDefinition` with `m` equal to exactly `4` or
`8`. A scheme shifts a complete train atomically: it inserts `m` fresh bundles,
moves every retained bundle by `m` positions, and discharges the `m` boundary
bundles. It never overwrites an identity in place and never leaves a duplicate
`BundleId`.

## Refuelling command and atomic transition

### Declarative scheme definition

`RefuelSchemeDefinition` is versioned data, not a sequence of one-bundle
commands. The v1 scheme set contains at least:

| Field | Requirement |
| --- | --- |
| `SchemeId` | unique scheme identity |
| `ShiftCount` | exactly `4` or `8` in v1 |
| `DirectionBinding` | `EndAtoEndB -> TowardEndB` and `EndBtoEndA -> TowardEndA` |
| `FreshFuelTemplateId` | resolves the complete inserted train template |
| `InsertedSlotOrder` | ascending physical position order at the insertion end |
| `SchemaVersion` | compatible scheme schema version |

The fresh-fuel template supplies the authoritative `MaterialVariantId`, exact
`m_HM`, allowed `InitialBurnup`, coefficient-table identity, and required count
for every inserted slot. The command may carry the bundle identities and
initial values, but they must match the resolved template exactly after unit and
schema normalization. A missing template, count mismatch, mass mismatch,
non-fresh material, or out-of-domain initial burnup rejects the complete scheme.

### Command fields

A `RefuelShiftCommand` contains one complete declarative scheme application:

| Field | Requirement |
| --- | --- |
| `CommandId` | canonical lowercase ASCII UUID bytes; unique in the command log |
| `Sequence` | unsigned 64-bit integer, unique at its effective time |
| `EffectiveTime` | finite simulation time `t >= current_time` |
| `ChannelId` | valid channel in the P2-T01 topology |
| `SchemeId` | valid `RefuelSchemeDefinition` with `ShiftCount` `4` or `8` |
| `ShiftDirection` | explicit value equal to the channel flow-direction binding |
| `InsertedBundleSpecs` | exactly `ShiftCount` new identities in canonical insertion order |
| `SchemaVersion` / `DataPackVersion` | compatible with current state |
| `InsertedAt` | must equal `EffectiveTime`; supplied for audit, not wall-clock time |

The command contains no raw nuclear-data path, private listing path, solver
tolerance, or Unity object reference. A caller that needs a named fuel type
must resolve it to the validated `FreshFuelTemplateId` before validation.

Each `InsertedBundleSpec` contains `BundleId`, `MaterialVariantId`,
`InitialBurnup`, `m_HM`, the resolved coefficient-table identity, and one
complete explicit I/Xe template envelope: `I135AtomInventory`,
`Xe135AtomInventory`, `InitialI135`, `InitialXe135`, the destination
`NodeVolume`, `NuclideStateVersion`, `NuclideDataId`, `NuclideDataDigest`, and
the explicit `NuclideHistoryDigest` for its initial history. A fresh template
has `I135AtomInventory == InitialI135`, `Xe135AtomInventory == InitialXe135`,
an empty `I135XeHistory` with the defined empty-history digest, and no
integration record before insertion. The specification list is ordered by the
physical positions it will occupy, not by arrival time or an arbitrary
collection order.

### Preconditions

Before mutation, the command validator must verify all of the following:

1. The current simulation state is valid and `current_time <= EffectiveTime`.
2. The channel exists, has the P2-T01 position count, and contains exactly one
   valid occupied bundle at every position.
3. The scheme exists, `ShiftCount` is `4` or `8`, and
   `InsertedBundleSpecs.Length == ShiftCount`.
4. The channel flow enum and command direction satisfy the normative endpoint
   binding, and the insertion/discharge ends agree with the direction table.
5. Every inserted identity is absent from all live bundles, discharge records,
   prior commands, and the other inserted specs in this command.
6. Every inserted spec matches the resolved fresh-fuel template for material,
   exact mass, initial burnup, table identity, and schema/data-pack version.
7. Every initial burnup is within the library domain and all masses are finite
   and strictly positive.
8. `Sequence` is unique among commands at `EffectiveTime`; `CommandId` has the
   canonical UUID representation and is unique in the scenario history.
9. The command has not already been applied and its schema/data-pack versions
   are compatible with the current state.
10. Every live bundle, discharge record, and inserted template envelope has
    finite nonnegative atom inventories and initial inventories, a finite
    strictly positive `NodeVolume`, finite derived number densities that equal
    the exact inventory/volume divisions, an explicit `NuclideStateVersion`,
    and a matching `NuclideDataId`/`NuclideDataDigest`. Its I/Xe history is
    canonical, append-only, version-bound, and free of duplicate or reordered
    records. Missing, stale, duplicate, or material/table-incompatible nuclide
    state rejects the command before any slot or discharge mutation. For every
    fresh inserted template, current and initial I/Xe inventories must be
    exactly equal, the history must be the empty counted sequence, and
    `NuclideHistoryDigest` must equal the defined empty-history digest.
11. All commands at the same effective time are available for batch preflight;
    duplicate inserted identities, duplicate channels, conflicting schemes,
    any other cross-command collision, or any I/Xe envelope/version/digest
    collision rejects the whole equal-time batch.

An invalid precondition rejects the complete command or equal-time batch. It
must not discharge a bundle, move a partial train, increment the clock, or
update a history record.

### Proposed post-state

The transition is evaluated in a scratch/prepared state using the following
rules. The live state is replaced only after every validation succeeds.

For `TowardEndB`, with old identities `old[p]` and inserted specs
`inserted[q]` in ascending insertion-position order:

```text
new[q]   = inserted[q]  for 0 <= q < m
new[p+m] = old[p]       for 0 <= p < N-m
discharge = old[N-m .. N-1] in ascending old position order
```

For `TowardEndA`:

```text
new[N-m+q] = inserted[q]  for 0 <= q < m
new[p-m]   = old[p]       for m <= p < N
discharge = old[0 .. m-1] in ascending old position order
```

The inserted train receives `InsertedAt = EffectiveTime`, the template-approved
initial burnups and masses, `CumulativeFissionEnergy = 0 J`, and no accepted
power snapshot until a new solver result exists. Its `Power` and
`PowerSnapshotId` are unset, not a synthetic zero power. It also receives the
explicit template I/Xe envelope. For a fresh v1 template,
`I135AtomInventory == InitialI135` and
`Xe135AtomInventory == InitialXe135`; its `NuclideStateVersion`,
`NuclideDataId`/`NuclideDataDigest`, empty `I135XeHistory`, and explicit
`NuclideHistoryDigest` must equal the validated template. Densities are derived
from the destination `NodeVolume`. A zero-duration refuel commit does not
increment that version or append an I/Xe integration record.

Every moved bundle keeps its `BundleId`, derived burnup, mass, material key,
inserted time, cumulative energy, current and initial I/Xe inventories,
`NuclideStateVersion`, nuclide data identity, and complete I/Xe history. Its
channel/position and destination-volume fields are updated only in the
proposed state; its densities are recomputed from the new volume and are not
copied as authoritative inputs. The discharged train is removed from the live
inventory and appended to immutable discharge records containing the complete
final bundle envelope: final and initial I/Xe inventories, discharge volume and
derived densities, nuclide version, data identity/digest, complete I/Xe history
and history digest, final burnup/energy state, discharge end, effective time,
command identity, and the canonical state digest.

The transition must then verify:

- every live `(ChannelId, BundlePosition)` is unique and occupied;
- every live `BundleId` remains globally unique;
- every inserted identity occurs exactly once and every discharged identity
  occurs zero times in the live inventory and exactly once in a discharge record;
- all derived burnups remain finite and nonnegative, all cumulative energies
  remain finite and nonnegative, all masses remain finite and positive, and
  every material key remains resolvable; and
- every live, inserted, and discharged nuclide envelope is finite, uniquely
  owned, data-compatible, and internally exact: atom inventories and initial
  inventories are nonnegative, `NodeVolume` is positive, densities equal the
  inventory/volume divisions, the moved envelope is byte-for-byte unchanged
  apart from the destination volume and derived densities, the fresh envelope
  equals its validated template, and each discharge record retains the final
  envelope exactly once; and
- the event payload is deterministic under the canonical position order.

Only after these checks pass does the implementation atomically replace the
inventory, append the scheme event and all discharge records, increment the
`CoreStateVersion`, and retain the command as applied. The proposed post-state
must also perform a coefficient-table lookup for every live bundle and store its
table identity and bracket. The prior power snapshot is invalidated; every
live bundle has `PowerSnapshotId` unset until a new P2-T02 solve is accepted
for the exact post-transition state. If any postcondition, I/Xe validation,
nuclide-data lookup, or coefficient lookup fails, the original inventory,
burnup/energy history, every I/Xe atom, initial value, volume, derived-density
check, nuclide version, data identity, I/Xe history/digest, clock, coefficient
bindings, complete `VersionLifecycleV1` state, event log, discharge records,
and command-applied set remain byte-for-byte unchanged.

The refuelling event uses the existing P2-T05 closed bodies exactly as defined:
`RefuelMappingBodyV1` records mapping and position fields only, while the
paired `RefuelAtomicityBodyV1` carries one `CommandId`,
`BeforeDigest`, `ProposedDigest`, `CommitStatus`, `RollbackReasonOrNA`, and
`UnchangedDigestOrNA`. Neither body claims fields that are absent from P2-T05.
The digest inputs are the complete `RefuelTransactionV1` projections above, so
the I/Xe fields are bound without changing the public body schema. Each
command in an equal-time batch has exactly one mapping/atomicity pair, pairs
are ordered by `(Sequence, CommandIdBytes)`, and every pair for one batch uses
the same batch before/proposed digest. The pair's `CommandId` identifies its
command; the shared digest identifies the all-or-none batch.

On a successful batch, the pairs and all `DischargeRecordV1` payloads are
appended together after the complete projection commits. On rejection, no
committed event-log entry is appended; the operation returns one non-committed
mapping/atomicity diagnostic pair per command in the same canonical order, with
the first canonical rejection reason and `UnchangedDigestOrNA` equal to the
unchanged before digest. The required `ProposedDigest` is the deterministic
rejected-proposed-state digest defined above. These diagnostics are evidence,
not mutated authoritative state.
For a bundle-scoped record, `StateBinding.NuclideStateVersion` is the exact
bundle counter. For an aggregate equal-time batch with no single owning bundle,
the outer P2-T05 `StateBinding` uses `NuclideStateVersion=NotApplicable` and
the ordered bundle-envelope `SnapshotDigest`; invalid spatial/power members
also use their explicit `NotApplicable` tags. Replay verifies the same envelope
bytes, pair cardinality, ordering, and applicability before accepting the
event.

### Event and time ordering

Simulation time is discrete and explicit. `AdvanceTo(t1)` requires finite
`t1 >= current_time` and processes the interval in increasing event order. A
refuel command at `t1` is applied only after burnup for `[current_time,t1]` has
been integrated and before the next solver snapshot at `t1`. The refuelling
instant itself has zero duration and adds no burnup.

A positive-duration interval may start only from a valid `PowerSnapshot` whose
`SnapshotTime == current_time`, `StateVersion == CoreStateVersion`,
`SpatialStateVersion`, `PowerSnapshotVersion`, every applicable bundle
`NuclideStateVersion`, inventory digest, burnup/energy digest, coefficient
digest, topology version, and data-pack version all match the live state. A
committed burnup interval increments
`CoreStateVersion`, derives every new `B`, performs and stores every coefficient
lookup, and invalidates the old snapshot. A committed refuelling batch increments
`CoreStateVersion` again, performs the post-shift lookups, and invalidates the
snapshot again. The next P2-T02 solve is therefore mandatory at the exact
post-transition time and state before another positive-duration interval can
begin. No stale power, stale coefficient bracket, or synthetic zero-power
placeholder may authorize advancement.

If several commands are scheduled at one time, they form one atomic equal-time
batch. The batch is sorted by `(Sequence, CommandIdBytes)`, where
`CommandIdBytes` is the unsigned byte sequence of the canonical lowercase ASCII
UUID. `Sequence` must be unique at that effective time; the UUID tie-breaker is
still part of the serialized order contract. The complete sorted batch is
preflighted against a scratch state, including cross-command identity and
channel collisions, I/Xe envelope identity, nuclide-data digests, and history
bindings, and is committed only if every scheme succeeds. A failure rolls back
every command in that batch, including commands on different channels, and
restores all moved, inserted, and discharged I/Xe inventories, initial values,
volumes, derived-density checks, versions, data identities, histories, and
digests together with the rest of the state. A command with `EffectiveTime` in
the past, a negative time interval, or a non-finite time fails closed. The
implementation must not consult wall-clock time or Unity frame timing.

### Synthetic I/Xe refuelling atomicity fixture

This contract fixture uses symbolic values only; it is not a production or
literature reference. Let a moved bundle `M` have explicit pre-state envelope
`(A_I,M, A_Xe,M, I0,M, Xe0,M, V_old, version_M, data_M, history_M)` and let its
destination volume be `V_new`, with `V_old > 0` and `V_new > 0`. The proposed
move must retain `A_I,M`, `A_Xe,M`, `I0,M`, `Xe0,M`, `version_M`, `data_M`, and
`history_M` byte-for-byte while producing exactly
`I135NumberDensity=A_I,M/V_new` and
`Xe135NumberDensity=A_Xe,M/V_new`. Let a fresh bundle `F` resolve a template
envelope `(A_I,F, A_Xe,F, version_F, data_F, empty_history_F)`; its inserted
current and initial inventories, version, data identity, and history must equal
that template, with densities derived from its insertion volume. Let a
discharged bundle `D` retain its final envelope in one immutable discharge
record.

After constructing this complete proposed state, inject one synthetic
postcondition failure such as an incompatible coefficient-table digest. The
transaction must reject with `committed=false`; the live slot map, all three
envelopes, every derived-density check, all I/Xe histories and digests,
`NuclideStateVersion` values, `CoreStateVersion`, lifecycle bindings, event log,
discharge records, command-applied set, and before/after state digests must be
byte-for-byte equal to the pre-state. No I/Xe time is accrued at the refuelling
instant. This fixture is an atomicity check, not a numerical acceptance
threshold.

## Burnup integration

### Accepted power snapshot

After a successful P2-T02 solve, the runtime records one `PowerSnapshot` at
the exact explicit state time. `SnapshotTime` must equal `current_time`, and
`StateVersion` must equal `CoreStateVersion` at commit. The snapshot carries
inventory, burnup/energy, coefficient, topology, and data-pack digests as well
as the solver result identity, `SpatialStateVersion`, and
`PowerSnapshotVersion`. The two snapshot versions are the post-increment
values from the same atomic `VersionLifecycleV1` commit; they cannot be
supplied independently or reused from an older snapshot. For every live
bundle, `P_b` is the finite, nonnegative node power in watts and is enumerated
in canonical bundle order. A missing bundle power, negative power, non-finite
value, stale time, stale digest, or mismatched core, spatial, nuclide, or
power-snapshot version invalidates the snapshot.

When the snapshot commits, each live bundle appends exactly one
`(SnapshotTime, P_b, CoreStateVersion, SpatialStateVersion,
PowerSnapshotVersion)` record to `PowerHistory` and receives the snapshot
identity in `PowerSnapshotId`. A rejected or stale solve appends nothing. This
history is retained through shifts and copied unchanged into a discharge
record.

V1 uses a deterministic piecewise-constant (left-endpoint) integration rule
for each interval. For a bundle `b` that remains live throughout
`[t_n,t_(n+1)]`:

```text
Delta_t = t_(n+1) - t_n
Delta_E_b = P_b(t_n) * Delta_t
CumulativeFissionEnergy_b(t_(n+1)) =
    CumulativeFissionEnergy_b(t_n) + Delta_E_b
B_b(t_(n+1)) = InitialBurnup_b +
    CumulativeFissionEnergy_b(t_(n+1)) / m_HM,b
```

The equivalent unit checks are:

```text
[W] * [s] = [J]
[J] / [kg_HM] = [J/kg_HM]
```

The rule is exact for a constant accepted power over the interval. It is a
deliberate v1 contract, not a claim about a reference-code time integrator.
The interval must be split at every refuelling event; a bundle that is
inserted or discharged at the endpoint receives no power contribution on an
interval in which it was not live.

For each interval, the implementation must evaluate bundles in ascending
`(ChannelId, BundlePosition, BundleId)` order, accumulate no unordered sum, and
record `Delta_t`, `P_b`, `Delta_E_b`, old/new cumulative energy, derived old/new
burnup, and the first invalid bundle if the operation fails. Burnup is monotone
nondecreasing. A negative/non-finite power, negative/non-finite increment,
overflow to a non-finite energy or burnup, or burnup outside the
coefficient-library domain fails closed; no clamp, saturation, or automatic step
reduction is allowed.

### Interval transaction

Burnup advancement is itself transactional:

1. Validate the target time and current power snapshot.
2. Compute all proposed energy and burnup values in canonical bundle order.
3. Validate finiteness, nonnegativity, mass, library-domain, energy-accounting,
   and monotonicity invariants for every proposed bundle.
4. Perform the coefficient lookup for every proposed derived burnup.
5. Replace cumulative energy and the derived burnup, increment
   `CoreStateVersion`, invalidate the prior power snapshot, and append one
   `BurnupAdvanceRecord` only if every bundle and lookup succeeds.

On failure, no bundle burnup, simulation time, solver snapshot, event record,
diagnostic state, or `VersionLifecycleV1` field is partially updated. A
refuelling command is evaluated only after the preceding interval transaction
has committed.

## Burnup-indexed coefficient library and interpolation

### Table contract

Each `MaterialVariantId` resolves to a versioned `BurnupCoefficientTable` with
strictly increasing finite knots `B_0 < B_1 < ... < B_M` in `J/kg_HM`. Every
row contains the P2-T02 coefficient set required by the solver, including at
least:

- `Sigma_a,1`, `Sigma_a,2`;
- `Sigma_f,1`, `Sigma_f,2`;
- `nuSigma_f,1`, `nuSigma_f,2`;
- `Sigma_s,1to2`;
- canonical primary `chi_1`; `chi_2` is derived as `1 - chi_1`; and
- `E_f` and any other explicitly approved power coefficient.

The table also carries an identity, schema/data version, checksum, material
key, unit metadata, and source provenance. It carries no runtime file path to a
private listing. A table with duplicate/reordered knots, missing units,
non-finite values, incompatible versions, checksum failure, negative forbidden
coefficients, `Sigma_f`/`nuSigma_f` mismatch, non-finite `chi_1`, or
`chi_1` outside `[0,1]` is rejected before lookup. If an input row carries a
redundant `chi_2`, it must equal the canonical derived value exactly after
normalized parsing; no comparison tolerance is invented.

Temperature, purity, liquid-zone, adjuster, and bulk-poison dimensions are not
silently added to this table; their branch schema is deferred to P2-T04.

### Lookup and interpolation rule

Given a validated bundle burnup `B`:

1. If `B = B_j`, select row `j` exactly and record bracket `(j,j)`.
2. If `B_j < B < B_(j+1)`, select the unique bracket and compute

   ```text
   alpha = (B - B_j) / (B_(j+1) - B_j)
   c(B) = (1 - alpha) * c_j + alpha * c_(j+1)
   ```

   for every interpolated scalar coefficient `c` other than `chi_2`, using
   scalar `double` arithmetic and the canonical coefficient order. Interpolate
   the canonical primary spectrum component and derive the other component:

   ```text
   chi_1(B) = (1 - alpha) * chi_1,j + alpha * chi_1,(j+1)
   chi_2(B) = 1 - chi_1(B)
   ```
3. If `B < B_0` or `B > B_M`, reject the lookup. V1 does not extrapolate,
   clamp, saturate, or silently select the nearest row.

The endpoints must satisfy `alpha=0` and `alpha=1` by the exact endpoint branch.
After interpolation, the implementation must revalidate all finite/range
constraints and the P2-T02 relationships, including `Sigma_a >= Sigma_f`,
nonnegative downscatter, nonnegative fission products, `chi_1` in `[0,1]`, and
the canonical `chi_2 = 1 - chi_1` construction. It must not renormalize an
invalid spectrum, repair a negative coefficient, or independently interpolate
the derived spectrum component.

The lookup result records the table identity, bracket indices, input burnup,
interpolation fraction, output units, and coefficient checksum. It does not
interpolate bundle identity, mass, topology, time, power, `k`, or convergence
diagnostics. Two lookups with identical table bytes and burnup bits must produce
identical output bytes under the same canonical scalar order.

## Hand-worked synthetic shift histories

The following full 12-position histories are the admissible v1 scheme fixtures.
Identifiers, powers, masses, table rows, and burnups are synthetic. A reduced
four-position algebra illustration follows them only to make the movement
formula visible; it is not an admissible production command.

### Declarative 4-/8-bundle scheme fixtures

Use synthetic `MaterialVariantId=MAT-SYN`,
`FreshFuelTemplateId=FT-SYN-1000KG`, table `TABLE-SYN-v1`, and
`DataPackVersion=synthetic-v1`. The template requires exactly `m_HM=1000
kg_HM`, `InitialBurnup=0 J/kg_HM`, and a fresh identity for every inserted
bundle. Both scheme definitions use canonical ascending physical position order:

| `SchemeId` | `ShiftCount` | Template | Inserted positions for `TowardEndB` | Inserted positions for `TowardEndA` |
| --- | ---: | --- | --- | --- |
| `S4` | 4 | `FT-SYN-1000KG` | `0,1,2,3` | `8,9,10,11` |
| `S8` | 8 | `FT-SYN-1000KG` | `0,1,2,3,4,5,6,7` | `4,5,6,7,8,9,10,11` |

The synthetic table domain includes `0 <= B <= 100,000 J/kg_HM`. At `t=0 s`,
each channel has a valid accepted snapshot at `CoreStateVersion=100`, every
bundle has `m_HM=1000 kg_HM`, `InitialBurnup=0`, and `Power=1 MW`. For each
ten-second interval, the left-endpoint integration gives:

```text
Delta_E = 1,000,000 W * 10 s = 10,000,000 J
Delta_B = 10,000,000 J / 1,000 kg_HM = 10,000 J/kg_HM
```

The snapshots and coefficient lookups below are required at each event
boundary; `PS1` and `PS2` are new P2-T02 results at the exact post-transition
state and time, not carried-forward powers.

#### Flow `EndAtoEndB`: 4 then 8 bundles

Channel `10` declares `flow_direction=EndAtoEndB`, so commands must use
`ShiftDirection=TowardEndB`.

At `t=0`, positions `0..11` contain `[A0,A1,A2,A3,A4,A5,A6,A7,A8,A9,A10,A11]`.
After the `PS0` snapshot, advance to `t=10 s`; all old bundles have
`B=10,000 J/kg_HM` and `CumulativeFissionEnergy=10,000,000 J`. Apply one
atomic command with `CommandId=00000000-0000-0000-0000-000000000041`,
`Sequence=41`, `SchemeId=S4`, and inserted identities `[F0,F1,F2,F3]`:

```text
positions 0..11: [F0,F1,F2,F3,A0,A1,A2,A3,A4,A5,A6,A7]
discharged:      [A8,A9,A10,A11] at EndB, each B=10,000 J/kg_HM
```

Commit the batch at `CoreStateVersion=102`, perform all coefficient lookups,
invalidate `PS0`, and accept `PS1` at `t=10 s` for the exact new inventory.
Advance the interval `[10 s,20 s]` from `PS1`; then `F0..F3` have
`B=10,000 J/kg_HM` and `A0..A7` have `B=20,000 J/kg_HM`. Apply one atomic
8-bundle command with `CommandId=00000000-0000-0000-0000-000000000081`,
`Sequence=81`, `SchemeId=S8`, and inserted identities `[G0,G1,G2,G3,G4,G5,G6,G7]`:

```text
positions 0..11: [G0,G1,G2,G3,G4,G5,G6,G7,F0,F1,F2,F3]
discharged:      [A0,A1,A2,A3,A4,A5,A6,A7] at EndB, each B=20,000 J/kg_HM
```

Commit at the next `CoreStateVersion`, perform the new lookups, invalidate
`PS1`, and accept `PS2` at `t=20 s`. The complete 4-bundle and 8-bundle
operations are each one transaction; they are not four or eight equal-time
single-bundle commands.

#### Flow `EndBtoEndA`: 4 then 8 bundles

Channel `11` declares `flow_direction=EndBtoEndA`, so commands must use
`ShiftDirection=TowardEndA`.

At `t=0`, positions `0..11` contain `[B0,B1,B2,B3,B4,B5,B6,B7,B8,B9,B10,B11]`.
After a `PS0A` snapshot, advance to `t=10 s` and apply one atomic `S4` command
with `CommandId=00000000-0000-0000-0000-000000000042`, `Sequence=42`, and
inserted identities `[Z0,Z1,Z2,Z3]`:

```text
positions 0..11: [B4,B5,B6,B7,B8,B9,B10,B11,Z0,Z1,Z2,Z3]
discharged:      [B0,B1,B2,B3] at EndA, each B=10,000 J/kg_HM
```

Accept `PS1A` at `t=10 s` for this exact post-shift state. After the next
ten-second interval, `B4..B11` have `B=20,000 J/kg_HM` and `Z0..Z3` have
`B=10,000 J/kg_HM`. Apply one atomic `S8` command with
`CommandId=00000000-0000-0000-0000-000000000082`, `Sequence=82`, and inserted
identities `[H0,H1,H2,H3,H4,H5,H6,H7]`:

```text
positions 0..11: [Z0,Z1,Z2,Z3,H0,H1,H2,H3,H4,H5,H6,H7]
discharged:      [B4,B5,B6,B7,B8,B9,B10,B11] at EndA, each B=20,000 J/kg_HM
```

Perform lookups, invalidate `PS1A`, and accept `PS2A` at `t=20 s` before any
later positive-duration interval. Reapplying either command ID, changing its
direction, changing its scheme count, or changing a template mass is rejected
without mutating the channel.

### Reduced algebra illustration: TowardEndB

At `t=0 s`, a channel has:

```text
position:  0    1    2    3
identity:  A    B    C    D
burnup:   80k  60k  40k  20k J/kg_HM
```

For the `m=1` movement formula only (not an admissible v1 scheme command), use
`ShiftDirection=TowardEndB`, inserted bundle `E`, and
`InitialBurnup_E=0 J/kg_HM`. The proposed post-state is:

```text
position:  0    1    2    3
identity:  E    A    B    C
burnup:     0   80k  60k  40k J/kg_HM
discharged: D at EndB
```

At `t=10 s`, use equal synthetic mass `m_HM=1000 kg_HM` and the accepted
start-of-interval powers for `E,A,B,C` of `1,2,3,4 MW`. The interval record is:

| Bundle | Power | `Delta_E = P*Delta_t` | `Delta_B = Delta_E/m` | New `B` |
| --- | ---: | ---: | ---: | ---: |
| E | `1 MW` | `10,000,000 J` | `10,000 J/kg_HM` | `10,000 J/kg_HM` |
| A | `2 MW` | `20,000,000 J` | `20,000 J/kg_HM` | `100,000 J/kg_HM` |
| B | `3 MW` | `30,000,000 J` | `30,000 J/kg_HM` | `90,000 J/kg_HM` |
| C | `4 MW` | `40,000,000 J` | `40,000 J/kg_HM` | `80,000 J/kg_HM` |

The clock advances to `10 s` only after all four rows commit. The discharged
`D` receives no power contribution after `t=0 s`.

### Reduced algebra illustration: TowardEndA

Starting from the same position layout `[A,B,C,D]`, use the `m=1` movement
formula with a channel whose declared `flow_direction=EndBtoEndA`, inserted
bundle `Z` at `EndB`:

```text
position:  0    1    2    3
identity:  B    C    D    Z
discharged: A at EndA
```

The result demonstrates the opposite endpoint binding. This is an algebra
illustration only; the admissible v1 `S4`/`S8` transactions are the complete
12-position histories above. Applying any complete scheme command ID again is
rejected and leaves its state unchanged.

### Interpolation check

For a synthetic material table with burnup knots `0`, `50,000`, and `100,000
J/kg_HM`, suppose one scalar coefficient has rows `0.20`, `0.30`, and `0.40
m^-1`. A bundle at `B=80,000 J/kg_HM` selects bracket `(1,2)` and

```text
alpha = (80,000 - 50,000) / (100,000 - 50,000) = 0.6
c(B) = 0.4 * 0.30 + 0.6 * 0.40 = 0.36 m^-1
```

The lookup is synthetic, uses no reference value, and must return the exact
endpoint row for `B=50,000 J/kg_HM` rather than performing a neighboring blend.

## Determinism and fail-closed diagnostics

Every successful burnup or refuelling result records:

- state/data-pack/topology versions and checksums;
- simulation time before and after;
- command or interval identity and deterministic order key;
- affected channel, scheme/count, positions, bundle identities, and boundary end;
- complete moved, inserted, and discharged I/Xe envelopes: atom inventories,
  initial inventories, node volumes, derived densities, nuclide versions, data
  identities/digests, history/digest bindings, and the canonical envelope
  before/proposed/unchanged digests;
- old/new derived burnup, initial burnup, mass, power, cumulative energy, table
  bracket, and interpolation fraction where applicable;
- `CoreStateVersion`, snapshot binding/digest status, `committed=true`, and zero
  invalid/clamp counts.

The first invalid condition is reported in canonical order. NaN, infinity,
negative burnup, negative power, zero/negative mass, invalid or negative I/Xe
inventory, non-positive node volume, derived-density mismatch, duplicate
identity, duplicate slot, invalid topology, invalid table,
checksum/version mismatch, stale or reordered I/Xe history, out-of-domain
lookup, past time, conflicting command, failed solver snapshot, stale snapshot,
template mismatch, partial post-state, attempted clamp, or nonconvergent
prerequisite solver
result fails closed with `committed=false` and no usable partial state.

Repeated execution with identical state bytes, command bytes, table bytes, and
power snapshots must produce identical event, inventory, history, and
diagnostic bytes. No unordered collection, wall-clock value, random value, or
Unity frame count may affect the result.

## Reference-case boundary

The P1 compact exports are parser/provenance evidence only. They do not provide
an approved refuelling schedule, bundle identities, burnup history, heavy-metal
mass, coefficient table, or transition tolerance. DRAGON5 and DONJON5 remain
offline reference tools and are not runtime dependencies. P2-T03 adds no raw
listing, private path, golden data, or publication/redistribution step.

## Definition of done for P2-T03

- Bundle state fields, identity, mass, burnup, time, and units are explicit.
- Declarative `S4` and `S8` schemes bind inserted trains, discharged trains,
  direction, and every moved position atomically.
- Both endpoint directions bind to the channel flow enum; an opposite direction
  is rejected.
- Preconditions, atomic postconditions, event ordering, and rollback behavior
  are explicit.
- The complete I-135/Xe-135 bundle envelope, including inventories, initial
  values, node volume, derived densities, nuclide version, data identity,
  history, discharge serialization, and equal-time rollback, is explicit and
  conforms to P2-T04 without incrementing moved-bundle nuclide versions.
- Burnup integration has a deterministic rule, dimensional check, event split,
  authoritative cumulative-energy invariant, exact snapshot lifecycle, and
  invalid-state behavior.
- Burnup-indexed coefficients have strict knots, units, interpolation,
  endpoint, range, and post-interpolation validation rules.
- Hand-worked synthetic 4-/8-bundle histories cover both directions, identity
  movement, discharge, nonzero-time event splits, post-transition solver
  snapshots, burnup increments, and interpolation.
- No runtime implementation, reference baseline, or later-phase behavior is
  introduced.

Source: the retained P2-T03 specification and the retained P2-T01/P2-T02
specifications in this directory.
