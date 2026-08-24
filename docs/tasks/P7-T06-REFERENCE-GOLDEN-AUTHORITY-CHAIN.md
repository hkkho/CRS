# P7-T06 - Phase 7 reference/golden authority admission research chain

## Authorization and boundary

Status: ACTIVATED TASK-DEFINITION / ROUTING RECORD. This file adds the
owner-requested `P7-T06` task; it is not a completed task report, research
result, reference authority, or gate PASS. The eventual execution must write
`docs/tasks/P7-T06.md` and stop at the first unresolved authority conflict or
critical blocker.

The decision for this task is deliberately narrow: determine whether at least
one public, reproducible, and licensable Phase 7 kinetics/I-Xe case can be
admitted as a traceable comparison authority. If no such case is available,
build a project-authored manufactured/synthetic golden package against the
already frozen contracts, clearly labeled `Synthetic` and never represented as
physical CANDU or external-reference data.

The task does not authorize runtime changes, new equations, nuclear constants,
units, signs, normalization rules, convergence criteria, tolerances, feedback
behavior, or production claims. A candidate artifact is not an approved golden
artifact until it passes the required independent review and G7A re-entry.

## Decision and audience

- Decision owner: the project owner/gate authority after the P7-T06 report and
  high-effort review are complete.
- Audience: Core/runtime maintainers, validation reviewers, and the owner
  deciding whether G7A may be re-entered with an admitted Phase 7 case.
- Research question: can an independently reproducible case bind the existing
  P2-T04/P7 observables and histories without importing inaccessible or
  unlicensed inputs? If not, what bounded manufactured case can exercise the
  same frozen contracts without pretending to validate a named reactor?
- Time boundary: public source availability and license terms must be recorded
  as observed during execution, with access dates and immutable URLs. The
  research is not an exhaustive survey of all reactor literature.
- Success criterion: one fully manifested admissible case, or one fully
  reproducible project-authored synthetic package, with an explicit reason why
  the alternative path was not admissible.

## Frozen authority and prerequisites

- `AGENTS.md` is the execution, review, validation, and stop-condition
  authority.
- `docs/Implementation_plan.md` Phase 7A and G7A define the required history,
  timestep, nonnegative-inventory, and reference-case boundary.
- `docs/spec/kinetics-xenon-rrs-feedback-v1.md` and `docs/tasks/P2-T04.md`
  remain the authority for equations, signs, units, state ownership, event
  order, xenon-free base data, and absorption-only coupling.
- `docs/spec/observables-validation-methodology-v1.md` and
  `docs/tasks/P2-T05.md` define observable identity, provenance, comparison,
  and artifact-status requirements; they do not approve numeric values.
- `docs/tasks/P1-T08.md` and `docs/reference/candu-literature-digest-v1.md`
  are required literature inputs.
- `docs/tasks/P7-T01.md` through `docs/tasks/P7-T05.md` and
  `docs/gates/G7A.md` define the implemented Phase 7A contract and its current
  conditional synthetic-only boundary.
- Current-scope lookup: exact `PHASE-7A` / `G7A` and `P7-T06` entries in
  `docs/PROJECT_SCOPE.md` before execution.

Applicable P1-T08 rows are `S1-R09`, `S4-R05`, `S4-R06`, and `S5-R08`.
They are discovery/context or candidate-case evidence only. The executing task
must distinguish them from a reproducible numerical authority and must not
promote a digest row, publication number, or search result into runtime data.

## Bounded execution sequence

Execute this as one task ID. The external-source route and manufactured-data
route are alternatives inside `P7-T06`, not separate unreviewed tasks.

1. Inventory the frozen observables and inputs needed for a useful Phase 7
   case: kinetic amplitude/precursors, I-135/Xe-135 inventories and number
   densities, explicit fission-rate/flux forcing, node volume, xenon
   absorption overlay, state/data binding, timestep schedule, and the exact
   comparison outputs consumed by the existing Core tests.
2. Perform bounded deep research using primary sources first. Search and read
   original papers, official datasets/manuals, government or university
   technical reports, and source repositories only where the source, version,
   method, and rights can be verified. Prioritize CANDU/PHWR kinetics and
   I-Xe histories, reproducible diffusion/transport cases, and public
   DRAGON5/DONJON5 documentation or cases that meet the offline-reference
   boundary.
3. Maintain a claim-to-source ledger. For each consequential number or input,
   record title, author/publisher, publication or update date, URL, access
   date, license/redistribution status, exact model/tool/version, nuclear data,
   geometry/topology, boundary conditions, units, normalization, convergence
   settings, output definition, and applicability limitations. Record
   contradictions and inaccessible artifacts instead of filling gaps from
   memory.
4. Attempt exact case mapping before any artifact admission. A paper or public
   number is not enough: the case must reproduce the relevant observable and
   bind to the frozen state, data-pack, topology, time, and unit contracts.
5. If no external case survives the license, reproducibility, and mapping
   checks, create a project-authored manufactured/synthetic package. The
   independent generator must derive or prescribe the case from the frozen
   P2-T04/P7 equations and verify residuals, balances, nonnegative histories,
   deterministic replay, timestep refinement, and exact manifest identity. It
   must not call the runtime to generate its own expected values.
6. Preserve the candidate/approved distinction. External or manufactured
   outputs begin as `Candidate`/`Deferred`/`NoGolden` or an equivalent explicit
   status. Only the owner-authorized G7A re-entry can approve bounded synthetic
   golden consumption; no artifact becomes production or external CANDU
   authority through this routing record alone.

## Source classes and exclusions

Preferred source classes, in order:

1. Original peer-reviewed research, official public datasets, government or
   national-laboratory reports, standards, and first-party tool documentation.
2. University or institutional technical reports with complete methods and
   retrievable inputs.
3. Transparent independent reproductions that identify the primary source and
   expose enough data and code to reproduce the case.

Discovery signals may include reviews or specialist commentary, but they cannot
support final admission without a primary source or independently reproduced
case. Do not use search-result snippets as evidence.

The task must not:

- commit or redistribute proprietary DRAGON5/DONJON5 inputs, raw nuclear-data
  libraries, private station geometry, inaccessible result files, or artifacts
  whose license is unclear;
- use OpenMC code, OpenMC-generated production data, or an OpenMC runtime
  dependency, which remain out of scope under `AGENTS.md`;
- treat a published scalar, an illustrative table, or a literature digest row
  as a runtime constant, golden value, tolerance, or normalized reference case
  without exact traceable reproduction;
- invent missing histories, geometry, normalization, boundary conditions,
  convergence thresholds, tolerances, or physics constants;
- add shutdown/scram/trip, safety-system, accident, thermal-hydraulic, CFD,
  operator-training, temperature/purity evolution, or Phase 7B behavior;
- alter the P2-T04 equations, P7 state contracts, public schemas, data-pack
  identities, or existing golden artifacts opportunistically.

## Allowed files and artifact boundary

The task-definition change itself is limited to this routing record and the
current-scope handoff. The later execution may touch only the following bounded
areas after inspecting all prerequisites:

- `docs/tasks/P7-T06.md` - required immutable execution report;
- `docs/research/P7-T06-report-source.md` and a compact source ledger - the
  canonical research synthesis and claim-to-source provenance;
- `data/comparisons/p7-t06-*` and `data/comparisons/p7-t06-*.manifest.json` -
  candidate external or manufactured comparison artifacts;
- `data/golden/p7-t06-*` and manifests - only if the artifact has the explicit
  bounded approval disposition after review/re-entry;
- `tools/P7T06*` or an equivalently isolated independent generator/validator;
- focused `tests/ReactorSim.Golden.Tests/` or Core evidence consumers needed to
  prove manifest binding and deterministic consumption;
- `docs/gates/G7A.md` and `docs/PROJECT_SCOPE.md` only for the final re-entry
  and handoff evidence.

No `src/ReactorSim.Core`, CLI, Unity, or public runtime contract file is an
allowed target of this task. If the evidence implies a runtime change, stop and
request a separately scoped implementation task.

## Required validation and review

- T0: source URL/license/path audit, manifest byte length and SHA-256 checks,
  status/coverage audit, local-link audit, and `git diff --check`.
- T1: independent source-ledger/case-mapping audit, generator repeatability,
  canonical output/hash equality, unit and state-binding checks, and explicit
  nonnegative/convergence diagnostics.
- T3: full headless Core/CLI/replay/Golden consumers whenever a comparison,
  manifest, generator, golden artifact, or numerical consumer changes.
- T6: mandatory reference-case reproduction/regeneration and manifest
  comparison when an external or regenerated baseline is admitted; record
  exact source/tool/nuclear-data/geometry/version identity and numerical
  differences. T4/T5 apply only if the task crosses those adapters or
  platforms, which it must not do by default.
- Independent `code review (high)` is required for numerical/reference/golden
  admission and must use the active reviewer lane with verified actual-review
  evidence when available. Preserve the same reviewer/context for corrections.

## Stop conditions and gate overlap

Stop the affected task and report the exact gap when a required source or input
is inaccessible, the license is unclear, the reference result cannot be
reproduced, units/normalization/boundaries conflict, or success would require
loosening a tolerance, replacing golden data, changing an equation, or adding
an excluded dependency. Do not silently switch to the manufactured fallback
after a failed external case without recording the decision and its scope.

G7A is conditionally passed for the existing synthetic/test-only scope. P7-T06
may overlap that incomplete reference evidence because it is specifically the
bounded admission/reproduction task and does not change the unresolved runtime
equations or tolerances. The task must name the G7A condition and carry its
deferred evidence into the G7A re-entry report. It does not authorize Phase 7B;
temperature/purity feedback remains disabled unless a separate approved data
authority enables it.

## Required execution request

```text
Task ID: P7-T06
Requested lane / reasoning: GPT-5.6 Luna / high for bounded research, data packaging, and documentation; code review (high) for numerical/reference/golden admission
Objective: Admit one reproducible Phase 7 kinetics/I-Xe comparison authority, or build one explicitly synthetic manufactured golden package when no external case is licensable and reproducible.
Allowed files/subsystems: the bounded paths listed in this routing record; no runtime source changes.
Approved inputs: AGENTS.md; complete docs/Implementation_plan.md; P2-T04/P2-T05 specifications and reports; P1-T08 report/digest; P7-T01..P7-T05 reports; G7A.md.
Current scope lookup: P7-T06 and PHASE-7A / G7A in docs/PROJECT_SCOPE.md.
Literature digest applicability: S1-R09, S4-R05, S4-R06, S5-R08; context/candidate discovery only until exact case admission.
Required report path: docs/tasks/P7-T06.md
```

## Definition of done

- The final report distinguishes external evidence, independent reproduction,
  project-authored synthetic evidence, and unresolved gaps.
- Every admitted input/output has exact provenance, license status, units,
  normalization, model/tool/nuclear-data identity, manifest, and checksum.
- The external route is either admitted with reproducible T6 evidence or
  rejected with concrete reasons before the manufactured fallback is selected.
- Any manufactured fallback is independent of the runtime, labeled
  `Synthetic`, and validated by deterministic repeat, residual/balance,
  nonnegative-history, timestep-refinement, and manifest-consumer checks.
- No runtime equation, tolerance, public schema, dependency, safety behavior,
  or production claim changes.
- T0/T1/T3/T6 evidence and the required high review are recorded; the final
  report is written; `PROJECT_SCOPE.md` and G7A re-entry handoff are updated;
  the final scope is clean and committed.

## Next handoff

Execute `P7-T06` as one task ID. On successful candidate completion, perform
G7A re-entry for the admitted scope. If the task stops on an authority or
licensing conflict, preserve the candidate/deferred artifacts and record the
decision needed; do not invent a data set. Phase 7B remains optional and
disabled until approved temperature/purity feedback data is separately
admitted.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/spec/kinetics-xenon-rrs-feedback-v1.md`](../spec/kinetics-xenon-rrs-feedback-v1.md),
[`docs/spec/observables-validation-methodology-v1.md`](../spec/observables-validation-methodology-v1.md),
[`docs/tasks/P1-T08.md`](P1-T08.md),
[`docs/tasks/P2-T04.md`](P2-T04.md),
[`docs/tasks/P2-T05.md`](P2-T05.md), and
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md).
