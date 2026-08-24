# P6-T02 Owner Approval Record

Status: `APPROVED / SYNTHETIC TEST-ONLY`

Date received: `2026-08-20`

Owner: `Kevin Ho`

## Approved scope

The owner approved the fixture identified as `P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1`
for synthetic test-only Core / ReducedModel P6-T02 evidence. The approval does
not authorize a production CANDU claim, an external-reference claim, or a
golden-data claim.

The approved fixture identity is:

- Map ID: `00000000-0000-0000-0000-00000000a602`
- Mapping ID: `00000000-0000-0000-0000-00000000a601`
- Mapping version: `p6-t02-synthetic-grouping-v1`
- Topology ID: `00000000-0000-0000-0000-00000000a603`
- Data version: `p6-t02-synthetic-liquid-zone-map-v1`

The approved 14-logical-zone / 6-physical-assembly grouping, synthetic target
topology, positive absorption sign certificate, reference fill profile, and
test-only units are those described in the associated proposal:
`docs/tasks/P6-T02-OWNER-APPROVAL-DRAFT.md`.

## Approval text received

```text
APPROVED: P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T02 evidence;
       no production CANDU, external reference, or golden-data claim.
SIGN: Approved as written.
IDENTITY: Approved as written.
BOUNDARY: Queue allocation/transition/rollback and scenario packages remain
          reserved for P6-T06/P6-T07.
DATE: 2026-08-20
```

The owner name is recorded from the follow-up owner message that resolved the
placeholder in the initial approval block. This record is the approval
authority for admitting the synthetic fixture; it does not approve new
equations, production data, queue behavior, or scenario packages.

## Execution boundary

P6-T02 may generate, validate, consume, and report the approved synthetic map,
state-dependent overlay, disabled-zero behavior, and rate-limited motion
projection. Queue allocation/transition/rollback and scenario packages remain
reserved for P6-T06/P6-T07. The original blocked attempt remains historical
evidence in `docs/tasks/P6-T02.md` until the task report is amended with the
new execution result.

