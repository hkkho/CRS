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
| `XSEC-RIGHTS-01` | Record the later owner attestation that permits JEFF/JEF offline internal use for derived golden-case candidates while prohibiting raw-data repository retention, game redistribution, and runtime use. | Separate rights/owner record and independent code review (high). It preserves the historical `XSEC-01` blocker rather than rewriting it, and admits no physics or runtime data. |
| `XSEC-02` | Create and approve the bounded source-to-runtime mapping specification/ADR: exact case, geometry/material model, source labels, energy boundaries/order, collapse/homogenization, fields, units, normalization, convergence, interpolation, tolerances, and pack schema contract. | `XSEC-01` technical inventory plus `XSEC-RIGHTS-01` completed internal-use authority; rows `S1-R04`, `S1-R05`, `S5-R03`, `S5-R04`, and `S6-R03`; T0/T1/T3-risk review and one independent code review (high). No invented physics. |
| `XSEC-DECK-01` | Resolve and independently review the exact JEFF lattice deck authority after the TCWU11 assertion conflict. | `XSEC-03` blocked diagnostic; bind the exact source candidate, assertion handling or replacement procedure, all deltas, and effects on the mapping. No numerical result or data admission. |
| `XSEC-SPH-01` | Resolve and independently review official SPH execution versus pre-SPH semantic-export staging after v2 source regression failure. | `XSEC-03-R1` blocked diagnostic; preserve source assertions/state evolution, bind exact capture object/timing, and update deck authority only with source-supported evidence. No numerical result or data admission. |
| `XSEC-RUNNER-01` | COMPLETE — [`XSEC-RUNNER-01.md`](XSEC-RUNNER-01.md) reproduced the supported external DRAGON5 TCWUX11 launch context and two clean assertion-passing target runs. | Path-free provenance only; no capture, mapping, exported data, runtime pack, or golden admission. `XSEC-SPH-02` is next. |
| `XSEC-SPH-02` | `BLOCKED` — [`XSEC-SPH-02.md`](XSEC-SPH-02.md) proved an in-process source-preserving copy but not a persistent capture artifact after normal completion. | Its reviewed manifest preserves every rejected serialization/interface probe. `XSEC-SPH-03` is the separately authorized source-interface proof; XSEC-03-R2 remains ineligible until it passes. |
| `XSEC-SPH-03` | Establish a supported persistent post-EDI/pre-SPH capture interface while retaining original SPH transitions and source assertions. | Approved owner record and task definition; official pinned-release support, two clean retained captures, source assertion/baseline comparison, and independent high review are mandatory. No data export or runtime admission. |
| `XSEC-03` / reruns | Reproduce at least one admitted DRAGON5 CANDU lattice/depletion case and export the approved group constants for fresh, equilibrium-like, refuelled/perturbed, and control/poison states where the specification supports them. | `XSEC-02`, completed `XSEC-DECK-01`, completed `XSEC-RUNNER-01`, and a completed/independently reviewed `XSEC-SPH-03` PASS; exact tools/data/decks/lexemes/hashes/run logs/convergence; T6 repeat. Unsupported states are reported, not fabricated. |
| `XSEC-CORE-01` | Define and independently review the separate, bounded DONJON static-core input and explicit topology/mapping authority for the XSEC candidate. | `XSEC-03`; the exact admitted lattice constants, source/model identity, geometry/topology, units, boundary conditions, normalization, convergence, and external-vs-project-authored status must be explicit. No named-station claim and no Core conductance may be derived from DRAGON diffusion coefficients. |
| `XSEC-04` | Reproduce at least one admitted DONJON5 full-core/static case consuming the admitted constants and capture approved comparisons. | `XSEC-03` and `XSEC-CORE-01`; exact topology, constants, boundary conditions, normalization, convergence, outputs, logs, and T6 repeat. |
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
