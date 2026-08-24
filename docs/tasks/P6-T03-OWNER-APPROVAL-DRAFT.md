# P6-T03 owner-approval packet — draft for user decision

Status: `DRAFT / PENDING USER APPROVAL`

This packet is a proposed synthetic decision only. It is not a runtime data
pack, a golden baseline, or an approval by itself. No P6-T03 source or data
implementation will consume these values until the approval block is accepted.

## Recommended decision

Approve **Option A: a project-authored synthetic, test-only adjuster-bank set
and influence-map package** for bounded engine-neutral Core / ReducedModel
Phase 6 work.

This authorizes P6-T03 state, grouping, insertion-fraction bounds, the frozen
`Manual`/`Prescribed`/`RateLimited` modes, owner-bound delayed-motion
projection, local map overlay, sign/bounds/determinism checks, and local
transaction rollback evidence. It does not authorize production CANDU data,
external-reference or golden claims, shutdown/trip behavior, or scenario
packages.

## Frozen requirements this decision does not change

- The adjuster overlay remains
  `DeltaSigma_a,g,i = sum_bank Weight_(bank,i,g) *
  (StateFraction_bank - ReferenceFraction_bank)`.
- Fractions are dimensionless and bounded inclusively to `[0,1]`; rate limits
  use fraction per second; delay and update time use seconds; map targets use
  `m^-1` absorption-overlay units.
- The map consumes `StateFraction` only. It never reads `CommandFraction` as
  physical state and never creates a direct-reactivity duplicate path.
- `Prescribed` is limited to initial setup or an explicitly scheduled setup;
  it does not bypass the runtime rate/delay contract.
- Disabled branches remain serialized and contribute exactly zero.
- The adjuster is a mechanical reactivity device, not a shutdown bank; trip,
  scram, accident progression, safety response, and thermal-hydraulic behavior
  remain out of scope.

## Proposed synthetic fixture

### Identity and scope

| Field | Proposed value |
|---|---|
| Fixture ID | `P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1` |
| Data version | `p6-t03-synthetic-adjuster-bank-map-v1` |
| Adjuster-set ID | `00000000-0000-0000-0000-00000000a701` |
| Influence-map ID | `00000000-0000-0000-0000-00000000a702` |
| Bank-grouping ID | `00000000-0000-0000-0000-00000000a703` |
| Synthetic topology ID | `00000000-0000-0000-0000-00000000a704` |
| Queue schema | `CANDU-ADJUSTER-QUEUE-V1` |
| Command schema | `CANDU-ADJUSTER-COMMAND-V1` |
| Evidence domain | Synthetic / engine-neutral Core / ReducedModel |
| Owner | `Kevin Ho` |
| Reference fraction | `0.5` for both banks |
| Sign certificate | `P6-T03-SYNTHETIC-POSITIVE-ABSORPTION-V1` |
| Entry count | `12` (`2` banks × `3` target nodes × `2` groups) |

### Bank identities and state fixture

| Bank | Bank ID | Queue binding | Target nodes | Initial mode | Reference / command / state | Rate | Delay | Update time |
|---|---|---|---|---|---:|---:|---:|---:|
| A | `00000000-0000-0000-0000-00000000a705` | `NotApplicable (Manual)` | `(0,0)`, `(1,0)`, `(2,0)` | `Manual` | `0.5 / 0.5 / 0.5` | `0.1 fraction/s` | `2 s` | `0 s` |
| B | `00000000-0000-0000-0000-00000000a706` | `00000000-0000-0000-0000-00000000a708` | `(3,0)`, `(4,0)`, `(5,0)` | `RateLimited` | `0.5 / 0.5 / 0.5` | `0.1 fraction/s` | `2 s` | `0 s` |

Both banks are enabled in the initial fixture. `Prescribed` is an approved
mode option for setup-only coverage, not an implicit runtime command. The
RateLimited queue owner is an explicit typed `Entity.Branch` binding with
branch ID `00000000-0000-0000-0000-00000000a709`. Manual mode has no queue
state or queue-owned projection; this is explicit `NotApplicable` data, not a
missing field.

### Exact ordering, normalization, and initial queue state

The bank-grouping version is `p6-t03-synthetic-bank-grouping-v1`. Its complete
ordered body is:

```text
ordinal 0: BankId ...a705 -> TargetNodes [(ChannelId=0, BundlePosition=0),
                                           (ChannelId=1, BundlePosition=0),
                                           (ChannelId=2, BundlePosition=0)]
ordinal 1: BankId ...a706 -> TargetNodes [(ChannelId=3, BundlePosition=0),
                                           (ChannelId=4, BundlePosition=0),
                                           (ChannelId=5, BundlePosition=0)]
```

The topology key set is exactly the ordered sequence
`(ChannelId=0..5, BundlePosition=0)`. No target or grouping membership is
inferred from array order. Normalization is explicitly `None`: the listed
weights are consumed as supplied in `m^-1` per unit dimensionless fraction.

The complete initial queue state for Bank B is:

```text
QueueId = ...a708
OwnerKey = Entity.Branch / ...a709
GenerationCadenceOrNA = NotApplicable
InitialNextSequence = 0
NextSequence = 0
AvailableCommands = [
  { TargetId = BankId ...a706, Bounded = 0.5,
    LowerBound = 0, UpperBound = 1, RateLimit = 0.1 fraction/s }
]
LastMotionTime = 0 s
PendingCommands = []
AppliedSourceEventIds = []
AllocatedCommandIds = []
QueueDigest = generated from these canonical fields after approval
```

Bank A has `QueueState = NotApplicable` because its initial mode is `Manual`.
The bank state stores no duplicate available-command or motion-time fields;
the RateLimited projections come only from the supplied queue state. P6-T03
uses this complete state as an immutable input for pure causal motion and
local scratch rollback. Queue allocation, enqueue/consume transitions, and
queue-state rollback remain outside this approval.

### Proposed map entries

Every listed bank/node pair has two entries:

| Bank | Target nodes | Group 0 weight | Group 1 weight |
|---|---|---:|---:|
| A | `(0,0)`, `(1,0)`, `(2,0)` | `0.0020 m^-1` per unit fraction | `0.0010 m^-1` per unit fraction |
| B | `(3,0)`, `(4,0)`, `(5,0)` | `0.0015 m^-1` per unit fraction | `0.00075 m^-1` per unit fraction |

The source unit is `dimensionless` and the target unit is `m^-1`. The positive
weights mean that, for this synthetic fixture only, increasing insertion above
the reference fraction increases the local absorption overlay. This is a
test convention, not a CANDU physical, production, or golden sign claim.

The validator must reject duplicate `(BankId, TargetNode, Group)` keys,
non-finite or signed-zero weights, wrong units, missing banks or targets,
identity/version mismatch, reference-state mismatch, and any contribution
from a disabled bank.

### Deterministic identity and boundary rules

After approval, P6-T03 must generate and record the canonical bank-grouping,
topology, map, reference-state, owner, and package digests from the exact
approved fields. The digests are not placeholders and are not supplied by
this draft.

P6-T03 may validate the adjuster-owned delayed-motion projection from the
complete supplied queue state and local state/map scratch rollback. It may not
allocate, enqueue, consume, transition, or roll back queue state. Shared queue
allocation/transition/rollback integration and all scenario packages remain
reserved for `P6-T06`/`P6-T07`. No production/external CANDU map, nuclear-data
claim, reference comparison, golden value, or safety behavior is authorized by
this packet.

## Approval block

To authorize the recommended synthetic fixture, reply with:

```text
APPROVED: P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T03 state,
       grouping, bounds, modes, sign, rate/delay, owner-binding, and
       deterministic local overlay/rollback evidence; no production CANDU,
       external reference, or golden-data claim.
SIGN: Approved as written: positive synthetic absorption overlay for increased
      insertion, reference fraction 0.5, two banks, six target nodes, two
      groups, and the 12 proposed map entries.
IDENTITY: Approved as written: AdjusterSetId `00000000-0000-0000-0000-00000000a701`,
          MapId `00000000-0000-0000-0000-00000000a702`, GroupingId
          `00000000-0000-0000-0000-00000000a703`, TopologyId
          `00000000-0000-0000-0000-00000000a704`, BankIds `...a705` and
          `...a706`, QueueId `...a708`, and deterministic
          digest generation/review after approval.
BOUNDARY: P6-T03 may validate adjuster-owned delayed motion from the complete
          supplied queue state and local state/map scratch rollback. It may
          not allocate, enqueue, consume, transition, or roll back queue state;
          shared queue allocation/transition/rollback and scenario packages
          remain reserved for P6-T06/P6-T07. No shutdown, trip, safety,
          production, external-reference, or golden-data behavior.
DATE: 2026-08-20
```

If the synthetic values, sign, or boundary are not acceptable, reply `REJECT`
with the corrected fields. A production or external package requires a separate
complete authority, licensing, reproducibility, unit, normalization, and sign
record before it can be considered.
