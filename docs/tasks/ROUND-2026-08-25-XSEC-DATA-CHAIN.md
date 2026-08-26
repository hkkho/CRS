# Licensed Cross-Section Data Workstream

**Date:** 2026-08-25

**Status:** AUTHORIZED TASK SET / ROUTING RECORD

**Owner approval:**
[`XSEC-PLAN-01-OWNER-APPROVAL.md`](XSEC-PLAN-01-OWNER-APPROVAL.md)

## Frozen authority and prerequisite evidence

Every task in this chain is governed by `AGENTS.md`,
`docs/Implementation_plan.md`, `docs/PROJECT_SCOPE.md`, applicable accepted
ADRs/specifications, the P1-T08 literature digest/report, P1-T02 through P1-T07,
and G1. Physics-related tasks must name their applicable P1-T08 digest row IDs
and distinguish context, reproducible numerical evidence, and admitted runtime
or golden authority.

The existing specifications define the runtime two-group coefficient semantics
and fail-closed expectations, but do not yet approve an exact DRAGON source
reaction mapping, numerical energy-group boundary/order, collapse and
homogenization procedure, or a complete source-to-`BurnupCoefficientTableV1`
admission. `XSEC-02` owns those decisions after `XSEC-01` proves that eligible
inputs exist. No implementation task may invent them.

## Sequential task chain

| Task | Objective | Prerequisites and required evidence |
|---|---|---|
| `XSEC-PLAN-01` | Record owner approval, task IDs, boundaries, checks, and gate routing without selecting physics or data. | Documentation-only T0/T1. |
| `XSEC-01` | Research official primary sources and create the legal/technical admission inventory for exact DRAGON5, DONJON5, nuclear data, case inputs, outputs, and generated derivatives. | At minimum P1-T08 rows `S1-R02`-`S1-R07`, `S1-R11`, `S5-R02`-`S5-R05`, `S5-R09`, `S6-R03`, and `S6-R08`; exact URLs, versions/commits, hashes, sizes, platform/container identity, notices, permission text/locators, and retained evidence. One independent code review (high). No raw source/data admission before a PASS. |
| `XSEC-02` | Create and approve the bounded source-to-runtime mapping specification/ADR: exact case, geometry/material model, source labels, energy boundaries/order, collapse/homogenization, fields, units, normalization, convergence, interpolation, tolerances, and pack schema contract. | `XSEC-01` PASS; rows `S1-R04`, `S1-R05`, `S5-R03`, `S5-R04`, and `S6-R03`; T0/T1/T3-risk review and one independent code review (high). No invented physics. |
| `XSEC-03` | Reproduce at least one admitted DRAGON5 CANDU lattice/depletion case and export the approved group constants for fresh, equilibrium-like, refuelled/perturbed, and control/poison states where the specification supports them. | `XSEC-02`; exact tools/data/decks/lexemes/hashes/run logs/convergence; T6 repeat. Unsupported states are reported, not fabricated. |
| `XSEC-04` | Reproduce at least one admitted DONJON5 full-core/static case consuming the admitted constants and capture approved comparisons. | `XSEC-03`; exact topology, constants, boundary conditions, normalization, convergence, outputs, logs, and T6 repeat. |
| `XSEC-05` | Implement the offline deterministic converter/validator plus versioned schema, path-free manifest, and candidate runtime pack containing only approved fields. | `XSEC-03` and `XSEC-04`; fail closed on schema/version/hash/license/finite/unit/normalization/convergence errors; T0/T1/T2/T3 and independent code review (high). |
| `XSEC-06` | Integrate validated-pack loading and deterministic consumption through engine-neutral Core and CLI. | `XSEC-05`; no reference-tool runtime dependency; T1/T2/T3 and independent code review (high). |
| `XSEC-07` | Add the thin graphical Unity import/loading/consumption path and visible smoke/demo evidence. | `XSEC-06`; frame-timing independence; T3/T4 and independent code review (high). Host Unity licensing/security limitations must be reported exactly. |
| `XSEC-08` | Produce deterministic numerical tables, plots, and the detailed case/provenance/licensing/comparison/limitations PDF. | `XSEC-07`; render and inspect every PDF page, bind exact artifact hashes, and do not overclaim admission. |
| `G4-R7` | Conduct a fresh spatial-solver/reference-data admission gate for the exact completed candidate. | `XSEC-08`; T3/T4/T6 evidence and one independent code review (high). Historical G4 reports remain immutable. |

If newly admitted dynamic xenon, regulating-system, or feedback evidence exceeds
the previously gated synthetic domains, schedule separate `G5-R1`, `G6-R1`, or
`G7A-R1` re-entry only after `G4-R7`; do not combine those task IDs.

## Execution and stop rules

After each completed task, write its report, reconcile `PROJECT_SCOPE.md`,
inspect the diff, and commit the exact task checkpoint before starting the next
ID. An incomplete gate may overlap only under the `AGENTS.md` gate-overlap rule.
The independent P10-T05 Unity host-license/security blocker does not prevent
offline legal/specification/reference work, but any later T4 claim must carry or
resolve that evidence rather than hiding it.

Stop dependent work on unclear license/export permission, unavailable required
data, irreproducible results, unresolved model/unit/group/normalization
mismatch, missing approved physics or schema authority, nondeterminism,
NaN/Inf, nonconvergence, regression failure, unapproved native/runtime
dependency, external credential, signing/publication requirement, or host
security change.
