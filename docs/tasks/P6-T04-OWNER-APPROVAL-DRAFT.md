# P6-T04 owner-approval packet - draft for user decision

Status: `DRAFT / PENDING USER APPROVAL`

This packet is a proposed synthetic decision only. It is not a runtime data
pack, a production CANDU input, an external-reference result, or a golden
baseline. No P6-T04 source or data implementation will consume these values
until the approval block is accepted.

## Recommended decision

Approve **Option A: a project-authored synthetic, test-only bulk-moderator-
poison state and influence-map package** for bounded engine-neutral Core /
ReducedModel Phase 6 work.

This authorizes P6-T04 state validation, derived concentration, a fixed
synthetic moderator volume, separate rate-limited add and slow explicit
withdraw actions, setup-only prescribed state, local map overlay, exact mass
accounting, nonnegative bounds, deterministic checks, and local scratch
rollback evidence. It does not authorize production CANDU data, external
reference or golden claims, hidden chemistry/decay/cleanup, controller or
safety behavior, queue transitions, or scenario packages.

## Frozen requirements this decision does not change

- The state uses the approved specification equations:
  `PoisonMassConcentration = PoisonMass / ModeratorVolume` and
  `DeltaSigma_a,g,i = sum_s Weight_(s,i,g) *
  (PoisonMassConcentration - ReferenceConcentration)`.
- Mass is authoritative in `kg` and nonnegative; moderator volume is finite
  and positive in `m^3`; derived concentration is `kg/m^3`; add and withdraw
  rates are explicit `kg/s`; time is explicit `s`.
- `Add` applies only `+AddRate * Delta_t`; `Withdraw` applies only
  `-min(WithdrawRate * Delta_t, PoisonMass)`. There is no implicit decay,
  chemistry, transport, or cleanup law.
- The map consumes the derived concentration only. It is a local absorption
  overlay and never creates a direct-reactivity duplicate path.
- `Prescribed` is limited to initial setup or an explicitly scheduled setup
  before positive-duration advancement; it cannot bypass the add/withdraw
  runtime rate contract.
- Disabled state remains serialized and contributes exactly zero.
- Shutdown, scram, trip, accident progression, safety response, thermal-
  hydraulic/CFD behavior, operator training, and Unity behavior remain out of
  scope.

## Proposed synthetic fixture

### Identity and scope

| Field | Proposed value |
|---|---|
| Fixture ID | `P6-T04-SYNTHETIC-BULK-POISON-MAP-V1` |
| Data version | `p6-t04-synthetic-bulk-poison-map-v1` |
| State schema | `CANDU-BULK-POISON-STATE-V1` |
| Influence-map schema | `CANDU-BULK-POISON-INFLUENCE-MAP-V1` |
| Poison source ID | `00000000-0000-0000-0000-00000000b405` |
| Influence-map ID | `00000000-0000-0000-0000-00000000b401` |
| Synthetic topology ID | `00000000-0000-0000-0000-00000000b404` |
| Owner | `Kevin Ho` |
| Evidence domain | `Synthetic / engine-neutral Core / ReducedModel` |
| Source unit | `kg/m^3` |
| Target unit | `m^-1` |
| Sign certificate | `P6-T04-SYNTHETIC-POSITIVE-ABSORPTION-V1` |
| Normalization | `None` |
| Entry count | `12` (`1` poison source x `6` target nodes x `2` groups) |

The existing G4-R6 prescribed poison overlay was inspected as historical
ReducedModel evidence only. This proposal creates a separately identified
P6-T04 authority; it does not promote, mutate, or relabel that earlier
artifact.

### State fixture and setup limits

The initial runtime fixture is enabled in `Add` mode:

| Field | Proposed value |
|---|---:|
| `Enabled` | `true` |
| `Mode` | `Add` |
| `PoisonMass` | `0.5 kg` |
| `ModeratorVolume` | `5.0 m^3` |
| `ReferenceConcentration` | `0.0 kg/m^3` |
| `AddRate` | `0.1 kg/s` |
| `WithdrawRate` | `0.01 kg/s` |
| `UpdateTime` | `0.0 s` |
| Prescribed setup maximum mass | `1.0 kg` |
| Prescribed setup volume | exactly `5.0 m^3` |

The initial derived concentration is therefore `0.1 kg/m^3`. The setup limit
is a synthetic fixture guard, not a general physical or production limit.
Volume is input data for this task and is not evolved by a hidden model.
`Prescribed` setup tests may use a finite mass in `[0,1.0] kg` and exactly
`5.0 m^3` at time zero or an explicitly scheduled pre-advance setup event.

### Exact map entries

The target-node key set is the explicit ordered sequence
`(ChannelId=0..5, BundlePosition=0)`. No target is inferred from array order.
Every target has one entry per group:

| Target nodes | Group 0 weight | Group 1 weight |
|---|---:|---:|
| `(0,0)`, `(1,0)`, `(2,0)`, `(3,0)`, `(4,0)`, `(5,0)` | `0.08 m^-1 per (kg/m^3)` | `0.05 m^-1 per (kg/m^3)` |

The map is sparse, sorted by source/target/group order, has no implicit
normalization, and binds the exact source unit `kg/m^3` to target unit `m^-1`.
For this synthetic fixture, increasing concentration above the reference
increases local absorption. This is a test convention, not a physical,
production, external-reference, or golden sign claim.

### Required checks and boundaries

The validator must reject duplicate source/target/group keys, non-finite or
signed-zero weights, wrong units, missing targets, non-finite or nonpositive
volume, negative mass, negative rates, setup mass above `1.0 kg`, setup volume
other than `5.0 m^3`, identity/version mismatch, reference-state mismatch,
and any nonzero disabled contribution. Each action records requested mass,
applied mass, old/new mass, derived concentration, update time, and map
digest.

P6-T04 may evaluate the poison-owned state from the complete supplied state
and perform local state/map scratch rollback. It may not allocate, enqueue,
consume, transition, or roll back shared queue state. Queue allocation and
transition/rollback integration remain reserved for P6-T06; scenario packages
remain reserved for P6-T07.

After approval, P6-T04 must generate and record canonical state, map,
reference-state, topology, and package digests from the exact approved fields.
Those digests are not placeholders and are not supplied by this draft.

## Approval block

To authorize the recommended synthetic fixture, reply with:

```text
APPROVED: P6-T04-SYNTHETIC-BULK-POISON-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T04 state,
       moderator volume, derived concentration, map binding, separate
       rate-limited add and slow withdraw/cleanup actions, setup limits,
       exact mass accounting, and deterministic local overlay/rollback
       evidence; no production CANDU, external reference, or golden-data
       claim.
SIGN: Approved as written: positive synthetic absorption overlay for
      concentration above reference; mass 0.5 kg, moderator volume 5.0 m^3,
      reference concentration 0.0 kg/m^3, add rate 0.1 kg/s, withdraw rate
      0.01 kg/s, setup maximum mass 1.0 kg, six explicit target nodes, two
      groups, and the 12 proposed map entries.
IDENTITY: Approved as written: FixtureId
          `P6-T04-SYNTHETIC-BULK-POISON-MAP-V1`, DataVersion
          `p6-t04-synthetic-bulk-poison-map-v1`, MapId
          `00000000-0000-0000-0000-00000000b401`, PoisonSourceId
          `00000000-0000-0000-0000-00000000b405`, TopologyId
          `00000000-0000-0000-0000-00000000b404`, and deterministic digest
          generation/review after approval.
BOUNDARY: P6-T04 may validate poison-owned state from the complete supplied
          state and local state/map scratch rollback. It may not allocate,
          enqueue, consume, transition, or roll back shared queue state;
          queue allocation/transition/rollback remains reserved for P6-T06,
          and scenario packages remain reserved for P6-T07. No hidden
          chemistry, decay, transport, cleanup, shutdown, trip, safety,
          production, external-reference, or golden-data behavior.
DATE: 2026-08-20
```

If any synthetic value, sign, identity, or boundary is not acceptable, reply
`REJECT` with the corrected fields. A production or external package requires
a separate complete authority, licensing, reproducibility, unit,
normalization, and sign record before it can be considered.
