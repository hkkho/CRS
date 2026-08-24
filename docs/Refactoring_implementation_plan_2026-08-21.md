# Refactoring implementation plan - 2026-08-21

## Status and authority

**Status:** Proposal only. This document records a code and planning review; it
does not amend the frozen [implementation plan](Implementation_plan.md), choose
physics, alter a tolerance or golden value, authorize a task, or change the
current [project scope register](PROJECT_SCOPE.md).

Each item below requires its own approved task request, one task ID at a time,
with the validation and review required by `AGENTS.md`. The current Phase 6/G6
status remains `CONDITIONAL PASS` for approved synthetic/test-only
Core/ReducedModel scope. It is not external, production, full-core, or golden
RRS authority.

## Review conclusion

The project does not need a broad simulation rewrite. Its foundational
architecture is appropriately conservative: `ReactorSim.Core` is engine
neutral, the Unity adapter is thin, numerical state is explicit, and the
spatial code has fail-closed diagnostics. The current direct regression is
green: Core `154/154`, Golden `19/19`, and the focused Phase 6 suite `44/44`,
each with zero failures and zero skips.

The concern about overengineering is nevertheless valid in the delivery
machinery and at the Phase 6 integration boundary. Contract and evidence
layers have grown faster than the small synthetic scenario domain they serve.
The most urgent issue is not a reactor-physics failure: the standard full-suite
wrapper relocates test binaries and exposes a test-data/root lookup defect,
producing Core `142/154` with twelve location failures while Golden remains
`19/19`. That means the project cannot yet treat the wrapper as portable CI or
release evidence.

Recommended direction: stabilize the test harness first; then reduce Phase 6
duplication behind compatibility-preserving internal adapters; then simplify
status handoffs. Do not refactor equations, units, tolerances, data packs,
canonical bytes, or golden/reference behavior as part of this recovery.

## Evidence reviewed

### Fresh test snapshot

| Invocation | Result | Interpretation |
| --- | --- | --- |
| Direct pinned `dotnet test ReactorSim.sln` from the repository layout | Core `154/154` PASS; Golden `19/19` PASS; zero failures/skips | Current direct headless regression is green. |
| Direct pinned focused P6 run | Core `44/44` PASS; zero failures/skips | The Phase 6 synthetic contract scenarios are green. |
| `tools/Test-FullHeadlessSuite.ps1 -ConfirmFullSuite` using its artifact-output layout | Core `142/154`; Golden `19/19`; twelve Core failures | The wrapper moves test assemblies below a temporary artifact root. Several Core tests infer the repository from `AppContext.BaseDirectory` or search parent directories for `ReactorSim.sln`, so their fixture lookup becomes invalid. This is a test-infrastructure defect, not a changed physics result. |

The direct result is therefore useful current evidence, but the wrapper failure
must be repaired before it is used as a portable full-suite or CI quality gate.
The SDK pin itself was also verified: `global.json` requires SDK `10.0.302`
with roll-forward disabled, while the system installation offers `10.0.303`.
The successful runs used the existing isolated `10.0.302` SDK. The toolchain
availability gap should be made explicit in the test-infrastructure task, not
silently worked around in future evidence.

### Source review

| Area | Observation | Why it matters | Proposed response |
| --- | --- | --- | --- |
| Full-suite portability | `Test-FullHeadlessSuite.ps1` passes `--artifacts-path`, while P6 test fixtures derive a root from a fixed number of parents or scan for `ReactorSim.sln`. `ReactorSim.Core.Tests.csproj` does not make all of the required P6 fixture packages hermetic in the test output. | The standard test command can report a false-looking regression solely because binaries moved. | Make test data resolution explicit and artifact-portable. |
| Controller-to-queue handoff | A controller projection contains its measurement digest, but `P6T06SourceBindingTokenV1` is supplied separately to `P6T06QueueStateV1.TryEnqueueBatch`. The current P6-T07 test manually creates the binding and candidates. | The intended causal link is split across callers. A future caller can assemble internally valid but mismatched pieces. | Add a narrow Core-owned orchestration method that derives the binding and candidates from a projection, validated event identity, and queue. |
| Two queue representations | `RrsQueueStateV1` is a supplied/read-only fixture projection, while `P6T06QueueStateV1` owns mutable transition behavior with overlapping fields and canonicalization concerns. | Parallel representations increase review and maintenance cost and make ownership less legible. | Introduce an internal adapter/projection after characterization tests protect the existing v1 bytes and public shapes. |
| Digest serialization duplication | Liquid-zone, adjuster, and bulk-poison influence-map contracts each repeat canonical digest-writing patterns. | Parallel edits can drift despite equivalent intent. | Extract shared internal canonical-writing primitives only after byte-for-byte characterization tests exist. |
| Task names in domain APIs | Phase 6 public type names such as `P6T06...` embed delivery-task history in runtime-facing contracts; several source files are multi-thousand-line contract files. | The names obscure the business model and create a costly future migration path. | Start with an API/schema inventory; introduce domain-oriented facades or aliases only when compatibility can be proven. Do not perform a wholesale rename. |
| Status surfaces | `LLM_REPOSITORY_SCAFFOLDING_BLUEPRINT.md` is explicitly retired, but `docs/NEXT_GOAL_PROMPT.md` still directs a completed P4 task. The previous site and guide status also still said Phase 6 had not started. | Stale planning surfaces make progress look opaque and invite an LLM to schedule already-complete work. | Keep `PROJECT_SCOPE.md` and task/gate reports authoritative; reduce the next-goal prompt to a scope-register pointer and make display updates part of a bounded documentation check. |

### Proportionate versus excessive complexity

The following complexity is proportionate and should be preserved:

- engine-neutral `ReactorSim.Core` with no Unity lifecycle or asset dependency;
- explicit indices, deterministic state transitions, replay/serialization
  checks, finite-value validation, and invalid-state diagnostics;
- flat-buffer-oriented spatial work, with convergence and nonconvergence
  diagnostics; and
- clear synthetic/test-only versus external/production authority boundaries.

The following is now disproportionate for the bounded synthetic Phase 6 scope:

- task-specific contract names and multiple representation layers at one
  controller-to-actuator handoff;
- repeated canonicalization implementations for similar map contracts;
- test fixture discovery that assumes a build-output directory layout; and
- duplicated status wording across prompts, reports, guides, PDFs, and site
  pages without a freshness check.

Repository size supports this concern but is not, by itself, proof of a defect:
the review counted 36 Core source files (about 32,692 lines), 29 Core-test
files (about 12,912 lines), seven Golden-test files (about 2,473 lines), 131
task reports, and 20 gate reports. The corrective plan therefore focuses on
clarity and evidence reliability rather than reducing line count for its own
sake.

### LLM-plan review

`LLM_REPOSITORY_SCAFFOLDING_BLUEPRINT.md` is a short retired blueprint and is
not an active authority. It should not be revived or treated as a competing
plan. The active implementation plan's separation of Core, CLI, Unity, and
offline reference tooling is sound and should remain frozen.

The operational problem is stale handoff material, not the implementation
plan's high-level architecture. In particular, `docs/NEXT_GOAL_PROMPT.md`
points to completed P4 work despite the current Phase 6/G6 disposition. This
is a good example of why a free-form LLM handoff prompt must not be another
status register. It should point readers and agents to the exact current task
or gate row in `PROJECT_SCOPE.md`, rather than restating an independently
maintained queue.

The no-op `ReactorSim.Cli` program is not classified as a current bug. A
player-facing CLI belongs to Phase 8 in the frozen plan. Adding one now would
be scope expansion, not simplification.

## Proposed implementation sequence

### 1. `TEST-INFRA-01` - make the full suite artifact-portable

**Objective:** Make the approved full-suite wrapper execute the same tests and
find the same test data when `--artifacts-path` relocates binaries.

**Allowed scope:** test project configuration, shared test-only data locator,
test fixtures, and `tools/Test-FullHeadlessSuite.ps1`. No Core runtime,
physics, schema, data-pack content, tolerance, or golden value change.

**Preferred design:** inventory every fixture dependency first. Either copy the
required declared test data into the test output or use one explicit,
test-injected repository/data root. Do not retain hardcoded parent counts or
per-test repository searches. Keep Golden tests and Core tests equally
relocation-safe.

**Acceptance evidence:**

- direct and artifact-output wrapper executions report the same discovered
  tests and zero failures/skips (at this snapshot: Core `154/154`, Golden
  `19/19`);
- wrapper execution succeeds with a newly created temporary artifact root;
- a focused test proves the locator diagnoses a missing declared asset clearly;
- the exact SDK provision path or prerequisite is documented; and
- no new dependency or CI workflow is introduced without separate approval.

**Validation:** T1 focused locator fixture, then T3 direct and wrapper full
headless regressions. This is the highest-priority recovery task.

### 2. `P6-INTEGRATION-01` - make controller-to-queue admission atomic

**Objective:** Give the Core one narrow, auditable method for translating a
validated controller projection into queue admission, instead of requiring
callers to separately compose a source-binding token and candidates.

**Allowed scope:** Phase 6 Core contracts and focused tests only. Preserve v1
public contracts, canonical bytes/digests, existing event rank, delays, rates,
and synthetic scenarios. This task must not select an RRS equation, map value,
tolerance, or external comparison.

**Acceptance evidence:**

- a mismatched projection/event/digest input fails before any queue mutation;
- valid scenarios produce byte-for-byte identical pending commands and
  digests to the pre-refactor characterization fixtures;
- one Core owner is visible at the projection-to-enqueue boundary; and
- existing P6 focused and full regressions remain green.

**Validation:** T1 mismatch/atomicity fixtures, T3 full Core and Golden
regression, and one independent code review (high) with actual receipt
verification when available. The task request must record `P1-T08`
applicability as `NotApplicable` only if it truly changes no physics selection
or numerical authority.

### 3. `P6-INTERNALS-01` - consolidate queue and digest implementation details

**Objective:** Reduce duplicate Phase 6 internal representations and repeated
canonical digest-writing code without changing v1 behavior.

**Allowed scope:** internal adapters/projections and shared internal helpers.
Do not delete, rename, or reinterpret a public v1 type until a compatibility
inventory and characterization suite pass.

**Acceptance evidence:**

- `RrsQueueStateV1` and `P6T06QueueStateV1` have one documented semantic owner
  at the integration boundary;
- existing fixture bytes, queue digests, map digests, replay bytes, and error
  codes are unchanged;
- map contract digest calculations share one tested internal primitive where
  their canonical rules are genuinely identical; and
- no public schema version, serialization form, or consumer behavior changes.

**Validation:** T1 characterization tests plus T3 full regression and
independent code review (high). Split this task if queue ownership and digest
deduplication cannot be reviewed as one small candidate.

### 4. `CORE-NAMING-01` - plan a safe migration away from task-ID domain names

**Objective:** Produce a compatibility inventory and a minimal migration plan
for task-ID-named runtime types.

**Allowed scope:** inventory and decision record first. A later implementation
task may add aliases/facades only after public API, persistence, reflection,
and test impacts are known.

**Acceptance evidence:**

- every `P#T#` public type is classified as internal-only, serialized, test
  fixture, or externally consumed;
- the proposal identifies compatibility risks and a no-break migration order;
- no bulk rename occurs in this discovery task.

**Validation:** T0 API/source inventory and focused build. Treat an actual
public-contract migration as a separate risk-triggered task with T3 and
independent code review (high).

### 5. `STATUS-VISIBILITY-01` - prevent stale handoffs without creating a new authority

**Objective:** Keep the private guide/site display and LLM handoff material
aligned with current authoritative evidence while avoiding another dashboard or
status database.

**Allowed scope:** `docs/NEXT_GOAL_PROMPT.md`, display-oriented guide/site
text, and their consistency checks. `PROJECT_SCOPE.md`, task reports, and gate
reports remain the sources of truth.

**Preferred design:** replace the stale prompt's hard-coded task selection with
a concise instruction to query the exact active scope/gate row. Retain the
current guide/site test snapshot only as clearly dated derived text. Add a
small check that rejects a known completed-task handoff or a displayed test
snapshot older than the referenced evidence. Do not create a general-purpose
LLM workflow engine or duplicated status ledger.

**Acceptance evidence:**

- guide, PDF, and private site visibly show the latest verified test result
  and its limitation;
- local consistency and site route tests reject stale Phase/Gate wording; and
- site publication remains separately authorized, private, and optional.

**Validation:** T0 text/consistency checks, T1 generated PDF text/visual
inspection and authenticated route tests. No deployment is implied.

### 6. Owner decision: preserve the Phase 8 CLI boundary

After the test harness and Phase 6 contract seams are stable, decide whether to
start the planned Phase 7 work or to re-sequence an explicitly approved Phase 8
non-physics CLI vertical slice. The current no-op CLI should not be filled with
unapproved game logic as a refactor side effect.

## Sequencing and stop conditions

1. Execute `TEST-INFRA-01` first. It restores trustworthy full-suite evidence
   and has no physics authority implications.
2. `STATUS-VISIBILITY-01` may proceed independently after its exact scope is
   approved, because it changes only derived documentation/surface text.
3. Execute `P6-INTEGRATION-01` only after the portable test baseline is green.
4. Execute `P6-INTERNALS-01` only after the atomic admission seam is clear and
   characterized.
5. Treat `CORE-NAMING-01` as a lower-priority discovery task; do not let a
   cosmetic rename delay numerical, evidence, or gameplay work.

Stop any Phase 6 candidate if it would require a new device sign, controller
gain, map, equation, unit, tolerance, golden/reference value, public schema
version, or an RRS comparison claim. Those are not refactoring choices. Keep
the existing G6 conditional boundary and require an admitted comparison package
plus G6 re-entry for any unconditional/external claim.

## Completion measures

The project is back on course when all of the following are true:

- the full-suite wrapper is portable and agrees with the direct regression;
- the user-facing guide, PDF, and private-site source show a dated, precise
  test result and any known limitation;
- one Core boundary owns the controller-projection-to-queue transition;
- duplicate queue/digest machinery is either consolidated or explicitly
  justified by a compatibility boundary;
- the current LLM handoff cannot nominate a task that the scope register marks
  complete; and
- no refactor has widened the synthetic/test-only evidence into a production
  reactor claim.

## Non-goals

- No reactor-physics rewrite, equation selection, data-pack generation, or
  numerical retuning.
- No altered golden data, acceptance tolerance, or direct external CANDU claim.
- No Unity, mobile, CI workflow, hosting, package, native dependency, or
  player-game implementation without its own approval.
- No commit, push, deployment, or release as part of this proposal.
