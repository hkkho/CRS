# P6-T03 Owner Approval Record

Status: `APPROVED / SYNTHETIC TEST-ONLY`

Date received: `2026-08-20`

Owner: `Kevin Ho`

## Approved scope

The owner approved `P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1` for synthetic
test-only engine-neutral Core / ReducedModel P6-T03 state, grouping, bounds,
modes, sign, rate/delay, owner-binding, and deterministic local
overlay/rollback evidence. The approval does not authorize a production CANDU
claim, external-reference claim, or golden-data claim.

The approved identity, synthetic values, complete initial RateLimited queue
state, explicit Manual `NotApplicable` queue state, ordered grouping/topology,
units, sign certificate, and digest-generation rule are those recorded in
`docs/tasks/P6-T03-OWNER-APPROVAL-DRAFT.md`.

## Approval text received

```text
APPROVED: P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T03 state,
       grouping, bounds, modes, sign, rate/delay, owner-binding, and
       deterministic local overlay/rollback evidence; no production CANDU,
       external reference, or golden-data claim.
SIGN: Approved as written.
IDENTITY: Approved as written.
BOUNDARY: P6-T03 may validate adjuster-owned delayed motion from the complete
          supplied queue state and local state/map scratch rollback. It may
          not allocate, enqueue, consume, transition, or roll back queue state;
          shared queue allocation/transition/rollback and scenario packages
          remain reserved for P6-T06/P6-T07.
DATE: 2026-08-20
```

This record is the authority for admitting the synthetic P6-T03 fixture. It
does not approve new equations, production data, external references, golden
values, queue mutation, or scenario packages.

## Execution boundary

P6-T03 may implement and validate the approved synthetic adjuster-bank state,
exact bank grouping, insertion-fraction bounds and modes, positive synthetic
absorption overlay, complete supplied RateLimited queue-state projection,
causal delayed/rate-limited motion, owner binding, and local state/map scratch
rollback. It may not allocate, enqueue, consume, transition, or roll back queue
state. Shared queue work remains reserved for P6-T06, and scenario packages
remain reserved for P6-T07.

