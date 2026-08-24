# Round 2026-08-16 — G4 technical-admission resolution task set

## Authorization and boundary

Status: AUTHORIZED TASK SET / ROUTING RECORD. This file is not a completed
task report and does not replace an individual `docs/tasks/<TASK-ID>.md`
report.

On 2026-08-16 the project owner authorized the remaining in-plan scope for
bounded execution, confirmed redistribution rights for the selected DONJON5
data, and confirmed that the final game will use a faster reduced model with
offline-generated interpolation data rather than the exact reference data at
runtime.

This authorization is sufficient to execute the chain below as separate task
IDs. It does not convert the candidate into a golden baseline and does not
authorize a new equation, unit, sign, normalization, convergence rule,
tolerance, public schema, or runtime dependency. `G4` remains the owner
gate for spatial tolerance and golden approval. `G5`, `G6`,
`G7A`, and `G7B` retain their respective ownership for later
quantities.

The chain is deliberately limited to the three static cases admitted by
`P4-T06-G4A`. The refuelled, RRS, and poison cases remain deferred until
their approved inputs and named later gates exist. DONJON5 and DRAGON5 remain
offline evidence tools only.

## Technical block being resolved

Fresh `G4-R1` is `BLOCKED` because:

- the R5 artifact is synthetic candidate evidence, not an independent
  approved solver/reference baseline;
- all 57 comparison records remain `Deferred`/`Candidate`;
- G4B provides test-only candidate consumers but no approved golden payload or
  numeric threshold; and
- the refuelled case is nonconverged while RRS and poison are not covered.

The resolution path must establish independent, reproducible evidence without
changing the frozen P2 contracts. A green test run alone is not sufficient.

## Authorized task chain

| Task ID | Bounded outcome | Depends on | Required evidence / handoff |
|---|---|---|---|
| `P4-T06-G4C` | Record the independent reduced-model validation authority and exact case/normalization mapping for the three G4A static cases. **Current result: COMPLETE.** The authority package and exact mapping are recorded; G4D must provide the independent candidate computation. Bind the model/tool/version, source and pack identity, topology/geometry, units, normalization, observable mapping, reproducibility method, and rights. Use existing P2 equations/contracts only; stop on an authority or mapping gap. | `G4-R1`; `P4-T06-G4A`; `P4-T06-G4B`; P2-T01/P2-T02/P2-T05; P1-T08 digest/report. | T0/T1 decision audit, applicable digest row IDs or `NotApplicable` with reason, same-context code review (high). No runtime code, tolerance, or golden approval. Next: `P4-T06-G4D`. |
| `P4-T06-G4D` | Produce a path-free, hash-bound offline reproduction artifact and manifest for the three static cases using the G4C authority. **Current result: COMPLETE.** The standalone checker emits three converged candidate snapshots and 57 deferred records, binds the frozen input hashes, repeats byte-identically, and rejects a hash-mismatched input. It is independent of the candidate projection being checked and adds no runtime schema or dependency. | `P4-T06-G4C`; frozen P2-T01/P2-T02/P2-T05 contracts; P1-T08 digest rows named by G4C. | T0/T1 and risk-triggered T3 passed; T6 was not applicable because no external reference baseline was regenerated; same-context code review (high). Candidate-only; no tolerance, golden, runtime-schema, or equation change. Next: `P4-T06-G4E`. |
| `P4-T06-G4E` | Add or update bounded test-only consumers for the G4D candidate artifact. **Current result: COMPLETE.** The consumer suite binds the exact three case IDs, all 10 profile/quantity identities, source/pack/manifest/artifact hashes, SI units, normalization fields, typed scopes/state/order bindings, and the 57 `Deferred`/`Candidate` records. It adds no threshold, tolerance approval, golden payload, runtime dependency, or evidence relabeling. | `P4-T06-G4D`; G4B consumer boundary; P2-T05 methodology. | T0/T1 passed; T3 passed Core 103/103 and Golden 8/8; same-context code review (high) PASS with actual telemetry `UNVERIFIED`. Next: `G4-R2`. |
| `G4-R2` | Re-run the Phase 4 gate against G4C/G4D/G4E evidence. **Current result: BLOCKED.** Direct G4D validation and the full regression suite pass, but the artifact remains `Candidate`/`Deferred`, no approved numerical baseline or tolerance authority exists, and the refuelled/RRS/poison slots remain deferred. No tolerance or golden status was changed. | `P4-T06-G4E`; G4A/G4B; P4 Core/T08; P1-T08. | Immutable `docs/gates/G4-R2.md`, T1 direct/wrapped reproduction checks, T3 Core 103/103 plus Golden 8/8, T6 not applicable, and same-context code review (high) PASS with actual telemetry `UNVERIFIED`. The technical gate remains BLOCKED; reference-dependent work stays stopped while frozen independent work may overlap under AGENTS.md. |

## Parallel scope authorization

All remaining in-plan work is authorized for decomposition and scheduling in
future rounds, subject to the implementation plan, frozen specifications,
gate-overlap policy, and one-task-at-a-time execution. This includes
independent Phase 5 work and the later Phase 6-11 roadmap. It does not allow
dependent work to consume unapproved spatial tolerances, hide nonconvergence,
replace golden data, or skip its owner gate. `P5-T10` and `G5`
remain dependent on spatial admission and cannot be treated as cleared by this
authorization.

## Required task discipline

Each task must read `AGENTS.md`, `docs/Implementation_plan.md`,
the named specifications, `P1-T08`, `G4-R1`, and all prerequisite
reports before acting. Each task writes its own
`docs/tasks/<TASK-ID>.md` report and runs the cheapest applicable
validation, escalating to T3 and code review (high) for numerical,
reference-data, deterministic, schema, or public-contract changes. No task
may loosen a tolerance, invent a value, or treat candidate evidence as
approved.

## Current handoff

`G4-R1` is complete as an immutable `BLOCKED` rerun. T1 candidate
admission passed, T3 passed Golden 4/4 plus Core 103/103 with no failures or
skips, and the same-context review returned `BLOCKED — review PASS` with
two retained evidence findings and no Medium/Low findings. `P4-T06-G4C` is
complete: it records the frozen authority package and exact case/normalization
mapping for the three admitted static cases, while preserving the candidate
and deferred status. `P4-T06-G4D` is complete: its independent offline
reproduction and manifest are hash-bound and path-free, its three cases
converge in two outer iterations, its 57 records use corrected typed P2-T05
scopes/order/state bindings, and its final same-context review returned PASS
with no actionable findings. Its 57 records remain Deferred/Candidate.
`P4-T06-G4E` is complete: its four test-only consumers bind the G4D
artifact/manifest and frozen input hashes, exact case/profile identities,
units, normalization fields, typed P2-T05 scopes/state/order bindings, and the
Deferred/Candidate boundary. Focused G4E tests passed 4/4; the T3 rerun passed
Golden 8/8 and Core 103/103. `G4-R2` is now complete as a technical
`BLOCKED` rerun: its reports preserve the missing approved baseline/tolerance
authority and incomplete coverage findings. No prior task was restarted, and
the historical `docs/gates/G4.md` and `docs/gates/G4-R1.md` reports remain
unchanged. Reference-dependent work stays stopped; only frozen independent
work may overlap under `AGENTS.md`.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), and
[`docs/gates/G4-R1.md`](../gates/G4-R1.md).
