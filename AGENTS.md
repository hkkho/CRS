# Repository instructions

These instructions apply to the entire repository. They are the operational
protocol for changing it.

## Document authority

Use the smallest current source that answers the question; do not turn a
template, a status register, or a historical report into a new technical
authority.

| Source | Authority |
|---|---|
| `AGENTS.md` | How work is selected, executed, reviewed, validated, and reported. |
| `docs/Implementation_plan.md` | Frozen product scope, architecture, phase/gate intent, and capability definition. |
| `docs/PROJECT_SCOPE.md` | Current delivery state, evidence gaps, and searchable handoffs; it never authorizes a technical change. |
| `docs/spec/` and accepted ADRs | Approved technical decisions for their stated scope. |
| `docs/tasks/` and `docs/gates/` | Immutable evidence of what was attempted, observed, or waived. |
| Templates and derived guides | Reusable structure or explanation only; they do not override the sources above. |

If current authorities conflict, preserve the historical record, stop the
affected work, and request a decision. Do not resolve a conflict by silently
rewriting evidence or choosing a new physics rule.

## Required reading and task scope

Before changing files:

1. Read `docs/Implementation_plan.md` completely.
2. Read the current task request and every approved specification it names.
3. Read the reports for prerequisite tasks under `docs/tasks/`.
4. Inspect the working tree and preserve unrelated user changes.
5. For phase, gate, or cross-task work, query `docs/PROJECT_SCOPE.md` before
   reading unrelated table rows; use the exact task/gate/phase ID as the search key.

Execute one task ID at a time; do not combine multiple task IDs in one execution.
A later task or phase may be scheduled in a separate execution while an upstream
gate is incomplete only under the gate-overlap policy below. If requirements
conflict or an approved specification is missing for the current task, stop and
report the conflict instead of improvising.

## Model routing, review evidence, and usage accounting

- Use GPT-5.6 Luna for bounded implementation and documentation work. Use high reasoning by default; select max only when the task is unusually complex and the expected quality benefit justifies the extra tokens. Terra may be used as a documented fallback or benchmark.
- Use **code review (high)** for every activity that the implementation plan previously routed to Sol: architecture, physics, risk-triggered validation, phase gates, golden-data decisions, performance/mobile readiness, and release review. This is the active project review lane until the user explicitly changes it; do not route new work to Sol by default.
- A requested model or review label is not evidence of the review that ran. Before accepting independent code-review evidence, verify the actual model and reasoning effort from the available execution receipt or telemetry. If verification is unavailable or differs from the request, mark the evidence `UNVERIFIED` and do not call it a verified approval.
- Use one bounded reviewer attempt for a candidate and preserve the same reviewer/context for the final disposition after corrections. Do not start repeated fresh-review swarms. A new reviewer requires a concrete reason such as unavailable context, conflict of interest, or a changed candidate, recorded in the report.
- If an implementation worker produces no checkpoint or artifact within the task's defined timebox, stop that attempt, record `NO_ARTIFACT`, and reclassify or use an approved fallback. Do not silently retry the same assignment without bound.
- Each task report records requested and actual model/effort, worker or reviewer identifiers, attempt count, artifact/verdict status, elapsed time, and token fields when telemetry exposes them: input, cached input, cache-write input, output, reasoning output, and total. Record the price source/date and cost formula or state that cost is unavailable; never invent a per-worker allocation. Goal-service totals are reported separately and are not added to request totals.

## Architecture boundaries

- `src/ReactorSim.Core` is an engine-neutral C# library. It must not use Unity types, lifecycle calls, serialization attributes, or asset references.
- `src/ReactorSim.Cli` is the first client, deterministic harness, and batch runner. It may depend on the core; the core must not depend on it.
- `unity/ReactorGame` is a thin presentation adapter. It must not own or duplicate simulation state-transition logic.
- DRAGON5 and DONJON5 are offline reference tools only. They are never runtime dependencies and their source is not ported into the game.
- OpenMC code, OpenMC-generated production data, and an OpenMC runtime dependency are out of scope.
- Runtime behavior must not depend on Unity frame timing.

## Change discipline

- Keep each change within the current task's allowed files and explicit objective.
- Do not combine unrelated cleanup, refactoring, dependency upgrades, or formatting with a task.
- Do not add packages, native dependencies, CI workflows, Unity assets, or speculative abstractions unless the current task explicitly requires them.
- Do not select or change physics equations, units, sign conventions, convergence criteria, numerical tolerances, golden data, public schemas, or runtime architecture without an approved task and specification.
- Do not add shutdown, scram, safety-system, accident-progression, full thermal-hydraulic, CFD, or operator-training behavior.
- Preserve deterministic behavior and explicit dependency direction.
- Do not commit, push, publish, create releases, configure remotes, or perform destructive repository operations unless the user explicitly asks.

## Gate overlap and critical blockers

- Gates are evidence checkpoints, not automatic serial blockers. A later task or
  phase may overlap an incomplete gate when its inputs are already frozen, it does
  not depend on the unresolved finding, and the task report names the upstream
  gate, assumption, and deferred evidence.
- A gate is a **critical blocker** only when proceeding would require inventing or
  changing an equation, unit, sign, boundary condition, convergence rule,
  tolerance, golden/reference value, public state contract, or safety/out-of-scope
  behavior; would hide nondeterminism, NaN/Inf, nonconvergence, invalid data, or a
  failed regression; or would cross an unapproved licensing, native-dependency,
  signing, publication, or release boundary.
- A forced closure is an explicit administrative waiver, not a technical PASS.
  Use the exact disposition `FORCED CLOSED / WAIVED`, preserve the historical gate
  reports and findings, and carry unresolved non-critical evidence into the next
  named task or gate.
- G2 is currently `FORCED CLOSED / WAIVED` by user direction. Existing Phase 2
  specifications may be implemented and Phase 3+ work may proceed where no
  critical blocker is introduced. Do not use the waiver to select new physics,
  alter tolerances, replace golden data, or bypass a critical stop condition.
- When an incomplete gate is non-critical, continue with the next eligible task
  instead of waiting. When it is critical, stop only the dependent work; unrelated
  phases may continue if their scope is independent.

## Literature evidence for physics tasks

`P1-T08` is the required literature-review task for the curated CANDU,
DRAGON5, and DONJON5 source set in the implementation plan. Until its report is
`COMPLETE`, do not begin a new task that selects, changes, validates, or reviews
physics equations, constants, units, normalization, coefficient generation,
burnup/refuelling behavior, kinetics/xenon behavior, regulating-system or
feedback behavior, reference cases, tolerances, or golden data. `P1-T08` itself
and task-definition/report-only work are the only exceptions.

After `P1-T08` completes, every such physics-related task must:

1. Read the approved literature digest and the `P1-T08` report before acting.
2. List the applicable digest row IDs in its task request and report, or state
   `NotApplicable` with a concrete coverage reason.
3. Distinguish background/context evidence from reproducible numerical
   evidence and from an approved runtime or golden-data authority.
4. Preserve the exact source/model/tool/version, nuclear-data, geometry, units,
   normalization, and applicability limitations recorded by the digest.

The literature digest does not override approved specifications and does not by
itself authorize equations, constants, tolerances, golden values, or reference
baseline changes. A published number may become golden evidence only through a
separate authorized task that reproduces or traceably maps the exact case,
 records numerical differences, and receives the required code-review (high)/gate
 approval.
Stop on a source conflict, inaccessible required artifact, unclear license, or
unresolved model/unit/normalization mismatch.

## Numerical and runtime rules

When numerical implementation is authorized by a later task:

- Use `double` for the authoritative simulation until an approved profiling task permits otherwise.
- Use explicit SI units where practical and document the unit of every stored or serialized value.
- Use stable, explicit channel and bundle indexing; never infer flow direction from array order.
- Keep simulation time discrete and explicit.
- Prefer preallocated flat arrays in hot loops.
- Record convergence state, residual or error measures, iteration counts, clamps, and invalid-state diagnostics.
- Fail closed on NaN, infinity, invalid coefficients, negative concentrations, incompatible data versions, checksum failures, or nonconvergence.

## Proportionate validation

| Level | Use |
|---|---|
| T0 | Static/format/schema/build check for the touched artifact. |
| T1 | Focused test, fixture, deterministic case, or named manual inspection. |
| T2 | Changed module plus its immediate adapter. |
| T3 | Full headless Core, replay, regression, and approved golden-consumer suites. |
| T4 | Unity EditMode/PlayMode and desktop-player smoke. |
| T5 | Android/iOS build, device, and mobile-performance evidence. |
| T6 | Reference-case reproduction/regeneration and manifest comparison. |

Run the cheapest check that could realistically catch an error in the current change:

1. Inspect the final diff or, before the first commit exists, inspect status plus every created file.
2. Build the affected project or validate the affected document/data format.
3. Run only the focused T1 check named by the task.
4. Record exact commands and results in the task report.

Do not run T3-T6 or the full test suite unless the task, a gate, or a plan trigger requires it.

At minimum, escalate to T3 and independent code review (high) with verified actual-review evidence when a change affects equations, interpolation, normalization, units, signs, boundary conditions, convergence, indexing, flow direction, refuelling transitions, burnup, xenon, regulating-system response, feedback, clocks, determinism, RNG, saves, replay, golden data, data-pack schemas, manifests, tolerances, public cross-project interfaces, numerical backends, floating-point behavior, precision, or runtime-affecting toolchain versions. Unexpected nondeterminism, NaN/Inf, nonconvergence, unexplained reference error, or a regression failure also triggers escalation.

Run T4 as well for core/Unity adapter or serialization-contract changes, T5 for platform-specific or mobile-sensitive changes, and T6 only when the reference baseline changes.

## Required task report

Every task ends with `docs/tasks/<TASK-ID>.md` containing:

- outcome and completion status;
- files added or changed;
- requested and actual model/reasoning evidence, review disposition, and attempt/artifact status;
- token fields and cost basis when telemetry exposes them, or an explicit unavailable/not-allocable statement;
- assumptions and design choices;
- exact validation commands and results;
- numerical differences, when applicable;
- deferred tests and the gate that will run them;
- blockers, risks, follow-up work, and whether a risk trigger fired;
- the next eligible task.

Use `docs/tasks/TASK_REPORT_TEMPLATE.md` when available. Inspect scope after writing the report. Stop after the current task.

## Stop conditions

Stop and request a decision when:

- DRAGON5/DONJON5 licensing or required nuclear-data availability is unclear;
- a reference result, unit, or normalization cannot be reproduced;
- specifications conflict or a required equation is missing;
- success would require loosening a tolerance or replacing golden data;
- identical inputs produce nondeterministic results;
- a mobile implementation needs an unapproved native dependency or license;
- work would expand into shutdown/scram, full thermal hydraulics, or another excluded area; or
- a destructive operation, secret, signing identity, store submission, commit, push, or publication is required without authorization.

Out-of-scope proposals belong in `docs/backlog.md` when that file is available; do not implement them opportunistically.
