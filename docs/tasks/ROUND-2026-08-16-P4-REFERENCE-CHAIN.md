# Round 2026-08-16 — P4 reference/data follow-up task set

## Authorization and boundary

Status: AUTHORIZED TASK SET / ROUTING RECORD. This file is not a completed
task report and does not replace an individual `docs/tasks/<TASK-ID>.md`
report.

The project owner authorized a new goal round on 2026-08-16 and confirmed that
the selected DONJON5 reference data may be redistributed under the project's
rights. The owner also confirmed that the final game will not ship or use the
exact reference data as its runtime model; the intended delivery is a faster
reduced model with offline-generated interpolation data.

This direction removes the legal uncertainty recorded in historical `P4-T06`,
but it does not by itself select a new equation, coefficient, unit,
normalization, convergence rule, tolerance, public schema, or golden value.
Those decisions remain bounded by the frozen Phase 2 specifications and the
owner gates. The historical `docs/tasks/P4-T06.md` report is preserved.

The following boundaries apply to every task in this set:

- DONJON5 and DRAGON5 remain offline reference tools and are never runtime
  dependencies.
- Exact reference inputs and raw outputs are not placed in the game runtime;
  any committed derived data must be compact, versioned, manifested, and
  covered by the confirmed rights.
- The reduced/interpolation path must be traceable to the approved P2-T02 and
  P2-T03 contracts. A simplification that changes an equation, unit,
  normalization, or public contract stops for an approved specification.
- G4 owns spatial tolerance approval and G5 owns refuelling/depletion
  tolerance approval. Candidate comparisons cannot be represented as approved
  golden evidence before those gates.
- “All remaining scope authorized” means the in-plan work may be decomposed
  and executed in bounded task IDs. It does not authorize bulk execution,
  gate bypass, or work outside the implementation plan.

## Authorized task chain

| Task ID | Bounded outcome | Depends on | Required evidence / handoff |
|---|---|---|---|
| `P4-T06-R1` | Record the rights determination, artifact boundary, and reduced-runtime intent without rewriting historical P4-T06. | User direction; P4-T06; P2-T05 reference policy. | T0/T1 documentation and scope audit. Next: `P4-T06-R2`. |
| `P4-T06-R2` | Build a path-free DONJON5 case-admission audit: exact tool/build, authorized data identity, topology/geometry, exported fields, units, normalization, output mapping, and reproducibility inputs. The audit may close as `BLOCKED` when solver readiness or applicability is not proven. | `P4-T06-R1`; external authorized case package. | T1 admission audit; T6 only if the reference case is actually rerun. No raw private payload in Git. Current result: `BLOCKED`; next: `P4-T06-R2A`. |
| `P4-T06-R2A` | Resolve the R2 applicability finding by rejecting the PWR/assembly package for direct CANDU admission and selecting an explicitly identified CANDU-contextual offline source profile, with its applicability limits and execution status recorded. This task may not infer `T/B`, `V`, `E_f`, CANDU production topology, normalization, or tolerances from unapproved fields. | `P4-T06-R2`; P2-T01/P2-T02/P2-T05; approved mapping/source decision if a new equation or unit binding is required. | Bounded applicability/source-selection record, P1-T08 digest coverage, and code review (high). T6 only as a source execution probe; no runtime implementation or raw private payload. Next: `P4-T06-R2B`. |
| `P4-T06-R2B` | Audit the R2A profile and the same-pinned Candu6.x2m case for a solver-ready source-to-Core mapping: explicit `T/B`, `V`, `E_f`, units, normalization, topology, and node-level observables. Current result: the Candu6.x2m source run matches the 380 x 12 production counts, but the frozen-field mapping authority is still missing. | `P4-T06-R2A`; P2-T01/P2-T02/P2-T05; an approved source/mapping decision where the frozen contract does not already bind the field. | T1 mapping/authority audit, P1-T08 digest coverage, and code review (high). T6 recorded for the Candu6.x2m source probe; no runtime implementation or raw private payload. Current result: `BLOCKED`; next: `P4-T06-R2C`. |
| `P4-T06-R2C` | Close the final Candu6 source-to-P2-T02 admission gap by producing a path-free manifest or approved converter input that explicitly binds source node keys, `V_i` in m^3, `E_f,i` in J, preassembled `T_g`/`B_g` in m^2, normalization, direction, and node observables. If the source cannot provide a field, obtain or record the exact approved specification/source decision; do not derive conductances from `D_g`/`STRD` or infer identity from array order. Current result: `BLOCKED`; the source audit found no such export and preserved the unit/topology gaps. | `P4-T06-R2B`; P2-T01/P2-T02/P2-T05; explicit owner/specification decision for any missing field. | T1 mapping manifest/authority audit, P1-T08 digest coverage, and code review (high). T6 only when the solver-ready export is regenerated. No runtime implementation or raw private payload. Next: `P4-T06-R2D`. |
| `P4-T06-R2D` | Record the owner-approved technical disposition that unblocks or replaces direct Candu6 admission: either a path-free source-produced P2-T02 export, an approved P2-T02 mapping/addendum defining every missing field and provenance rule, or an explicit separate synthetic/reduced-model boundary that removes DONJON5 from the affected comparison scope. This task records a decision; it may not invent conductances, units, topology, normalization, tolerances, or golden values. | `P4-T06-R2C`; P2-T01/P2-T02/P2-T05; owner/specification decision. | T0/T1 decision record, applicable P1-T08 digest coverage, and code review (high). Stop if no technical authority decision is available. Next: `P4-T06-R3` only after a direct or alternate boundary is approved. |
| `P4-T06-R3` | Freeze the reduced offline-model and interpolation-data boundary using existing P2 contracts and the R2D disposition, including allowed transformations, valid domain, provenance, and runtime/non-runtime separation. **Current result: COMPLETE.** The existing `BurnupCoefficientTableV1`/`SpatialRecomputeRequestV1` surfaces remain authoritative; no new equation, schema, mapping, tolerance, or golden value was selected. | `P4-T06-R2D`; P2-T01/P2-T02/P2-T03/P2-T05. | Physics task with P1-T08 digest coverage, T3 Core 103/103, and same-context code review (high) PASS (`UNVERIFIED` receipt/model telemetry). Next: `P4-T06-R4`. |
| `P4-T06-R4` | Implement the offline conversion/validation path and produce a candidate compact interpolation data pack plus manifest, only from the admitted case and approved reduced-model contract. **Current result: COMPLETE.** The synthetic-only converter emitted a deterministic candidate pack and manifest, validated existing `BurnupCoefficientTableV1` rows/tables, bound artifact/table/source/pack digests, and rejected invalid schema/metadata/provenance inputs. | `P4-T06-R3`. | T0/T1 and risk-triggered T3 complete; focused checks passed, Core 103/103 passed, and same-context code review (high) returned PASS (`UNVERIFIED` receipt/model telemetry). Next: `P4-T06-R5`. |
| `P4-T06-R5` | Compare the Core solver and reduced data path against the admitted reference boundary selected by R2D and prepare the required representative spatial snapshots. **Current result: COMPLETE.** R5 produced 3 exact homogeneous candidate snapshots, preserved 1 nonconverged refuelled perturbation and 2 not-covered RRS/poison cases, and emitted 57 deferred typed records. | `P4-T06-R2D`, `P4-T06-R4`, P4 Core reports. | Focused R5 checks, T3 Core 103/103, T6 deferred because no external baseline was rerun, and same-context code review (high) PASS (`UNVERIFIED` telemetry). Candidate evidence only; no tolerance approval. Next: `G4` audit, currently `BLOCKED`; then `P4-T06-G4A`. |
| `G4` | Execute the Phase 4 spatial-validation gate against admitted evidence and approve quantities/tolerances only if the gate inputs are sufficient. **Original result: BLOCKED; fresh `G4-R1` result: BLOCKED.** | `P4-T06-R5`, then `P4-T06-G4A`/`G4B`. | [`docs/gates/G4.md`](../gates/G4.md) preserves the original result; [`G4-R1`](../gates/G4-R1.md) records the fresh rerun. The next task set is [`ROUND-2026-08-16-G4-ADMISSION-CHAIN.md`](ROUND-2026-08-16-G4-ADMISSION-CHAIN.md), starting with `P4-T06-G4C`. |
| `P4-T06-G4A` | Resolve the G4 evidence-admission and representative-case coverage block. **Current result: COMPLETE.** G4A admits the three deterministic static synthetic cases as a bounded comparison basis and explicitly defers the nonconverged refuelled and not-covered RRS/poison slots. | `G4` blocked audit; R2D/R3/R4/R5; P2-T01/P2-T02/P2-T05; applicable P1-T08 digest rows. | [`P4-T06-G4A` report](P4-T06-G4A.md); T0/T1 passed; final code review (high) PASS, actual review telemetry `UNVERIFIED`. No equation, unit, convergence rule, tolerance, golden value, or public-contract change. Next: `P4-T06-G4B`. |
| `P4-T06-G4B` | Add bounded golden-comparison consumers for the baseline admitted by G4A and bind manifest/profile identities without inventing thresholds or relabelling synthetic evidence. **Current result: COMPLETE.** G4B added four test-only consumers, closed the exact six-case disposition, and bound the existing artifact/profile identities. | `P4-T06-G4A`; an admitted comparison baseline and case disposition. | T0/T1 plus T3 complete: four Golden consumers and Core 103/103 passed; same-context code review (high) PASS, actual review telemetry `UNVERIFIED`. No tolerance approval or golden relabelling. Fresh `G4-R1` remains blocked; next: `P4-T06-G4C` in the new admission task set. |
| `P5-T09` | Complete the remaining I/Xe-bearing lifecycle, discharge, transaction, and power-history/digest binding required by the frozen Phase 2 contracts. | P5-T01–P5-T08; P2-T03/P2-T04/P2-T05. | T3 and code review (high); use independent gate overlap only where reference evidence is not required. |
| `P5-T10` | Compare approved deterministic refuelling histories against admitted reference sequences and prepare G5 evidence. | `P4-T06-G4A`, `P4-T06-G4B`, fresh `G4`, `P4-T06-R5`, `P5-T09`. | T3/T6 as applicable and code review (high). Candidate evidence remains pending G5. |
| `G5` | Approve refuelling/depletion comparisons and tolerances. | `P5-T10`. | Gate report and long deterministic history evidence. |

After G5, the remaining Phase 6–11 roadmap is authorized for the same
one-task-at-a-time decomposition: P6/G6, P7A/G7A, optional P7B/G7B, P8/G8,
P9/G9, P10/G10, and P11/G11. Those tasks must be instantiated from the
implementation plan and frozen Phase 2 contracts before execution; this record
does not invent their missing reference data, thresholds, or implementation
details.

## Required task discipline

Each task uses the exact task ID above, reads `AGENTS.md`, the implementation
plan, the named specifications, prerequisite reports, and the current scope
lookup, then writes its own report. Physics-related tasks after P1-T08 must
read the digest and report applicable row IDs or a concrete `NotApplicable`
reason. The chain stops on missing case data, unit/normalization mismatch,
non-reproducibility, licensing conflict, nondeterminism, or a request to loosen
a tolerance or replace golden data.

## Current handoff

`P4-T06-R1` is complete. `P4-T06-R2` produced the authorized external-case
audit and identified the technical admission gap. `P4-T06-R2A` completed the
bounded applicability decision: the PWR package is rejected for direct CANDU
admission and a CANDU-contextual offline source profile is selected. `P4-T06-R2B`
then demonstrated a successful hash-bound same-pinned Candu6.x2m source probe
with the exact 380-channel/12-bundle/4560-bundle production counts, but did not prove the
frozen Core coefficient and node-output mapping. `P4-T06-R2C` completed the
bounded source capability audit and remains historical `BLOCKED` evidence for
direct source admission. `P4-T06-R2D` is now `COMPLETE`: the owner selected the
separate synthetic/reduced-model boundary, retired direct Candu6.x2m admission
into P2-T02, and preserved the frozen P2 contracts and gate ownership of
tolerances/golden evidence. `P4-T06-R3` is now `COMPLETE`: the reduced/offline
interpolation boundary is frozen against the existing Core table and
recomputation contracts, with no new equation, mapping, schema, tolerance, or
golden value. `P4-T06-R4` is now `COMPLETE`: the offline synthetic-only
converter and validator produced the candidate pack and manifest, passed the
focused determinism/fail-closed checks and T3 Core 103/103, and received a
same-context code-review (high) PASS with no remaining findings; actual review
receipt/model telemetry is `UNVERIFIED`. `P4-T06-R5` is now `COMPLETE`: the
comparison output is committed at
`data/comparisons/p4-t06-r5-candidate-snapshots-v1.json`, regeneration is
byte-identical, all 57 records are deferred, and the candidate-only status
preserves the nonconverged/not-covered cases. The separate G4 execution is
recorded as `BLOCKED` in [`docs/gates/G4.md`](../gates/G4.md): no approved
comparison baseline, tolerance authority, or Golden consumer tests were
available. `P4-T06-G4A` is now complete: it admitted the three deterministic
static synthetic cases as a bounded comparison basis and explicitly deferred
the nonconverged refuelled and not-covered RRS/poison slots. `P4-T06-G4B` is
now complete: it added four test-only candidate consumers, bound the existing
artifact/profile identities, and preserved candidate/deferred evidence. Fresh
`G4-R1` is now complete as an immutable `BLOCKED` rerun: T1 admission passed,
T3 passed Golden 4/4 plus Core 103/103 with no failures or skips, and the
same-context review returned `BLOCKED — review PASS` with two retained
evidence findings and no Medium/Low findings. The next eligible task is
`P4-T06-G4C` in the [new G4 admission task set](ROUND-2026-08-16-G4-ADMISSION-CHAIN.md).
The remaining chain is authorized and must continue one task ID at a time; it
must not be executed as a combined multi-task change. Independent Phase 5 work
remains eligible under the gate-overlap policy, but `P5-T10` remains dependent
on the `G4-R2` outcome.
