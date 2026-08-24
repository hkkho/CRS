# P6-T02 owner-approval packet — draft for user decision

Status: `DRAFT / PENDING USER APPROVAL`

This packet is the concrete decision needed to reopen P6-T02. It is not a
runtime data pack, a golden baseline, or an approval by itself. No file under
`data/packs/` will be admitted and no Phase 6 implementation will consume the
proposed values until the owner approval block below is accepted.

## Recommended decision

Approve **Option A: a project-authored synthetic, test-only liquid-zone map
package** for the bounded engine-neutral Core / ReducedModel Phase 6 work.

This is the shortest safe route to reopen P6-T02 while preserving the existing
direct-production/external-CANDU boundary. It authorizes map admission,
state-dependent overlay, rate-limited zone motion, and fixture-focused P6-T02
tests only for the explicitly synthetic fixture below. It does not authorize
P6-T06 queue allocation/transition/rollback work, P6-T07 scenario packages, a
production CANDU map, a DONJON5/DRAGON5 comparison, a golden value, full-core
coverage, thermal-hydraulic behavior, or safety behavior.

Option B is to provide an external or production map package instead. Its
required contents are listed at the end of this document; until those fields
are supplied and approved, Option B remains blocked.

## Frozen requirements that this decision does not change

The proposal uses the already approved contracts and does not change their
equations, units, event order, queue ownership, or disabled behavior:

- Overlay: `DeltaSigma_a,g,i = sum_z Weight_(z,i,g) *
  (StateFillFraction_z - ReferenceFillFraction_z)`.
- Source unit: dimensionless fill fraction. Overlay target unit: `m^-1`.
- The exact 14 logical-zone to 6 physical-assembly grouping is explicit data,
  never inferred from array or channel order.
- Rate-limited motion uses the frozen delayed command rule and
  `sign(delta_f) * min(abs(delta_f), RateLimit * Delta_t)`.
- Disabled branches contribute exact zero and remain serialized.
- The map is a local absorption overlay only; it is not a direct-reactivity
  path and does not model coolant flow or thermal hydraulics.
- The owner-bound queue remains authoritative for available command fill and
  motion time; those values are not duplicated as independent state fields.

## Proposed synthetic fixture to approve

### Identity and scope

| Field | Proposed value | Approval meaning |
|---|---|---|
| Fixture ID | `P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1` | Test-only map identity |
| Data version | `p6-t02-synthetic-liquid-zone-map-v1` | Immutable version for this bounded fixture |
| Map ID | `00000000-0000-0000-0000-00000000a602` | Stable map identity proposed for approval |
| Mapping ID | `00000000-0000-0000-0000-00000000a601` | Stable P6-T01 grouping identity proposed for approval |
| Mapping version | `p6-t02-synthetic-grouping-v1` | Exact grouping version proposed for approval |
| Topology ID | `00000000-0000-0000-0000-00000000a603` | Synthetic six-node fixture identity; not production topology |
| Evidence domain | `Synthetic / engine-neutral Core / ReducedModel` | No production or external numerical claim |
| Map owner | `Kevin Ho` | Explicit owner binding, not inferred |
| Reference fill fraction | `0.5` for every logical zone | Synthetic scenario reference, dimensionless |
| Normalization | `None` | Weights are consumed as supplied in `m^-1` per unit fill fraction |
| Direct-reactivity contribution | `Forbidden` | Any duplicate direct path fails closed |
| Target fixture | six explicit nodes `(ChannelId 0..5, BundlePosition 0)` | Reduced test topology only |
| Groups | `0` and `1` | The existing two-group overlay contract |
| Entry count | `28` (`14` logical zones × `2` groups) | Sparse, deterministic map |

### Exact logical-to-physical grouping

The proposed package binds the P6-T01 grouping in canonical logical-zone order:

```text
logical_zone_id:    0  1  2  3  4  5  6  7  8  9  10 11 12 13
physical_assembly:  0  0  1  1  2  2  3  3  4  4   5  5  0  1
```

The package will carry the proposed mapping ID/version and a SHA-256 digest
generated from the existing P6-T01 canonical grouping bytes. The proposed
topology digest is generated from the ordered six-node list
`(ChannelId=0..5, BundlePosition=0)` using the same repository canonical-byte
helper. Approval authorizes these deterministic generation rules; the exact
mapping and topology digest bytes must be recorded and independently checked
in the P6-T02 report before any map is consumed. No digest is silently borrowed
from a test fixture.

### Proposed map entries

For each logical zone `z`, let `p` be the physical assembly in the table above.
The package contains exactly two entries:

```text
(LogicalZoneId=z, TargetNode=(ChannelId=p, BundlePosition=0), Group=0,
 Weight=0.0010 m^-1 per unit fill fraction,
 SourceUnit=dimensionless, TargetUnit=m^-1,
 ReferenceFillFraction=0.5)

(LogicalZoneId=z, TargetNode=(ChannelId=p, BundlePosition=0), Group=1,
 Weight=0.0005 m^-1 per unit fill fraction,
 SourceUnit=dimensionless, TargetUnit=m^-1,
 ReferenceFillFraction=0.5)
```

The proposed sign certificate is:

> For this synthetic fixture only, increasing a zone fill above its reference
> fill increases the local absorption overlay; decreasing it reduces the
> overlay. The two positive weights above are a deliberate test convention,
> not a CANDU physical or production sign claim.

The package validator must reject duplicate `(LogicalZoneId, TargetNode, Group)`
keys, non-finite weights, wrong units, missing logical zones, missing target
nodes, map-version mismatch, and reference-state mismatch. Disabled-zero is a
runtime overlay invariant, not a property of the weight package: the P6-T02
overlay evaluator must produce exact zero for a disabled serialized branch and
must fail closed if a disabled branch contributes nonzero overlay.

### Digest and ownership binding

After approval, P6-T02 will generate and record, before any consumption:

1. the canonical grouping digest from the existing P6-T01 implementation;
2. the canonical synthetic-topology digest over the ordered six-node list;
3. the canonical map-package digest over the exact ordered identity, units,
   sign certificate, reference fill, target keys, and 28 entries;
4. the synthetic reference-state binding digest used by the map; and
5. the owner/map ownership record containing map ID, owner ID, target-key set,
   sign certificate, and map digest.

The generated digests will be recorded in the P6-T02 task report and validated
by focused tests. A digest is never supplied as an unchecked placeholder.

## Approval block

To authorize Option A, reply with this completed decision:

```text
APPROVED: P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T02 evidence;
       no production CANDU, external reference, or golden-data claim.
SIGN: Approved as written: positive absorption overlay for increased fill,
      reference fill 0.5, exact 14-to-6 grouping, six target nodes,
      two groups, and the 28 proposed entries.
IDENTITY: Approved as written: MapId `00000000-0000-0000-0000-00000000a602`,
          MappingId `00000000-0000-0000-0000-00000000a601`,
          MappingVersion `p6-t02-synthetic-grouping-v1`, TopologyId
          `00000000-0000-0000-0000-00000000a603`, and the deterministic digest
          generation/review rule above.
BOUNDARY: Queue allocation/transition/rollback and scenario packages remain
          reserved for P6-T06 and P6-T07; disabled-zero is a P6-T02 runtime
          overlay invariant.
DATE: 2026-08-20
```

The approval is recorded in `docs/tasks/P6-T02-OWNER-APPROVAL.md`; that record
supersedes the pending placeholders in this proposal.

If you do not approve the proposed synthetic weights or sign convention, reply
`REJECT` and provide the corrected table/sign certificate. If you want direct
production/external authority instead, reply `OPTION B` and attach the package
described below; the synthetic proposal will remain unadmitted.

## What approval unlocks and what it does not

Approval unlocks reopening P6-T02 to create the versioned synthetic package,
implement its validation/overlay/motion contract, and run the task-owned T1/T3
evidence. It does not authorize P6-T06 queue transitions or P6-T07 scenario
packages, and it does not automatically pass P6-T02, P6-T03 through P6-T07, or
G6; each task still requires its own approved inputs, tests, report, and review.

The final G6 disposition, if reached, will remain explicitly scoped to the
approved synthetic/Core fixture. Direct production/external CANDU authority,
full-core coverage, nuclear-data claims, release claims, and safety behavior
will remain deferred.

## Option B — production/external package checklist

Choose this route only if you can approve or supply a real package. It must
bind, without inference:

- exact 14-to-6 logical/physical grouping identity, version, and digest;
- every sparse map entry's logical source, canonical `(ChannelId,
  BundlePosition)` target, group, weight, source unit, target unit, and
  reference state;
- owner identity, map ID, target-key set, sign certificate, map digest, and
  reference-state digest;
- model/tool/data version, nuclear-data identity, geometry/topology, and
  normalization/provenance;
- licensing/redistribution boundary; and
- reproducibility instructions sufficient to regenerate the byte-identical
  package and manifest.

Until all of those fields are available and approved, Option B cannot unblock
P6-T02.

## Independent draft review

The same independent high-review context used for the prior P6-T02 audit
returned `PASS` after correction. It confirmed that the proposal is limited to
P6-T02, binds concrete map/grouping/topology identities, assigns disabled-zero
to runtime overlay validation, and clearly excludes production, external, and
golden authority. Actual reviewer model/effort telemetry was unavailable, so
the review is `UNVERIFIED`. The remaining non-blocking hardening item is for
the eventual P6-T02 report to record the exact canonical-byte field order and
self-exclusion rules for each generated digest before consumption.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/tasks/ROUND-2026-08-17-P6-RRS-CHAIN.md`](ROUND-2026-08-17-P6-RRS-CHAIN.md),
[`docs/tasks/P6-T01.md`](P6-T01.md),
[`docs/spec/kinetics-xenon-rrs-feedback-v1.md`](../spec/kinetics-xenon-rrs-feedback-v1.md),
and [`docs/spec/observables-validation-methodology-v1.md`](../spec/observables-validation-methodology-v1.md).
