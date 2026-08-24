# P6-INTEGRATION-01 - atomic RRS projection-to-queue admission

## Outcome

Status: COMPLETE

The Phase 6 Core now owns one narrow RRS projection-to-queue admission seam.
`P6T06QueueStateV1.TryEnqueueRrsControllerProjection` validates the queue
owner and projection/event digest binding, maps validated projection commands
to P6-T06 candidates, preserves the caller-validated source binding, and
delegates to the existing immutable `TryEnqueueBatch` transaction. The
centered synthetic scenario uses the new seam. Existing queue command,
transition, pending-state, and digest bytes remain byte-for-byte identical to
the pre-refactor caller-composed path.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer; independent code review (high) reviewer.
- Requested model / reasoning: GPT-5.6 Luna / high for bounded implementation
  and documentation; independent code review (high).
- Actual model / reasoning: `UNVERIFIED` for the root execution and reviewer
  telemetry; the available execution environment did not expose a receipt
  proving model or reasoning effort.
- Execution receipt or telemetry source: goal/thread
  `01a031df-5487-7a53-8ec6-865fa2cb9353`; reviewer context
  `01a031fc-bc91-7323-982f-7c4b9758c6b5` (Heisenberg).
- Attempts: one bounded implementation attempt and one bounded reviewer
  attempt; elapsed time unavailable.
- Artifact/checkpoint status: produced source, focused tests, task report,
  scope handoff, and implementation checkpoint `ac87bee`.
- Review disposition: `PASS`; no High or Medium findings and no correction
  required. The reviewer recorded a Low follow-up that dedicated integrated
  coverage for Manual/Held/phase-token/saturation paths is not present; the
  adjacent P6-T06 contract suite covers those invariants and the follow-up is
  deferred rather than expanding this task.
- Review evidence verification: `UNVERIFIED` for model/effort/receipt
  telemetry; the final reviewer disposition is recorded from the reviewer
  result.
- Reviewer reuse/fresh-review rationale: one reviewer context was used for
  the single candidate; no fresh review was started.

## Files created or changed

- `src/ReactorSim.Core/Domain/Phase6QueueTransitionContracts.cs` - adds the
  non-serialized validated controller-event handoff and the single Core-owned
  RRS projection admission method.
- `tests/ReactorSim.Core.Tests/P6T07ScenarioEvidenceTests.cs` - routes the
  centered scenario through the new seam and adds byte/digest characterization
  plus mismatch-before-mutation tests.
- `docs/tasks/P6-INTEGRATION-01.md` - this report.
- `docs/PROJECT_SCOPE.md` - records completion and the next eligible recovery
  task.

Pre-existing authorities and reports were inspected but not technically
changed, including `AGENTS.md`, the complete implementation plan,
`docs/Refactoring_implementation_plan_2026-08-21.md`, the frozen
`kinetics-xenon-rrs-feedback-v1.md` specification, the P6 chain record,
P6-T01 through P6-T07 reports, and the existing Core/Golden contracts except
for the two files listed above.

## Authority, prerequisites, and literature applicability

The exact `P6-INTEGRATION-01` row in `docs/PROJECT_SCOPE.md` and the approved
refactoring proposal authorize a Phase 6 Core-only integration seam. P6-T05,
P6-T06, and P6-T07 are complete prerequisites; their synthetic queue,
controller projection, event-rank, delayed-motion, and scenario boundaries
were preserved. The frozen P2-T04 RRS specification requires rank-2 command
generation, exact delay construction, source-state binding, and atomic
all-or-none admission; the new method delegates the existing P6-T06 atomic
implementation rather than selecting new rules.

P1-T08 applicability: `NotApplicable`. This task selects no equation,
controller gain, influence-map value, unit normalization, sign convention,
convergence rule, tolerance, reference case, or golden value. It only routes
already validated synthetic projection fields through the existing queue
contract and preserves the caller-supplied source-state digest exactly.

## Assumptions and design choices

- `P6T06RrsControllerEventIdentityV1` is an adapter handoff value, not a new
  serialized v1 record. It carries controller ownership, source event ID,
  caller-validated source binding, and the expected projection digest.
- The admission method maps `Automatic` to `Controller` and `Manual` to
  `Manual`, always uses the frozen RRS rank-2 boundary, and rejects `Held`
  because Held projections do not generate new queue admissions.
- Source binding remains caller-validated in accordance with P6-T06. The
  method constructs the existing source-binding token internally but does not
  derive or replace its digest with a measurement or physics value.
- Candidate conversion recomputes the existing bounded-command rule and
  rejects a projection/candidate bounded-value mismatch before queue admission.
- Queue mutation remains immutable and atomic because the new seam delegates
  to `TryEnqueueBatch`; the input queue is unchanged on all rejection paths.
- The characterization test intentionally supplies the pre-existing synthetic
  source digest and compares the old caller-composed result with the new
  method, preserving pending command IDs, canonical bytes, transition bytes,
  and queue/command digests.

## Validation commands and results

### T0 - diff hygiene

```text
git diff --check
```

Result: exit code 0; no whitespace errors. The reviewed implementation was
checkpointed as commit `ac87bee` (`refactor: centralize RRS projection queue admission`).

### T1 - focused P6-T07 integration/scenario suite

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:Path="$sdkPath;$env:Path"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\AppData\Local\Temp\candu-cli-home-p6-integration'; & (Join-Path $sdkPath 'dotnet.exe') test 'C:\Users\infin\candu\tests\ReactorSim.Core.Tests\ReactorSim.Core.Tests.csproj' --configuration Release --nologo --no-restore --filter 'FullyQualifiedName~P6T07ScenarioEvidenceTests' --logger 'console;verbosity=minimal'
```

Result: exit code 0; 10/10 passed, 0 failed, 0 skipped. This includes the
five existing scenario/replay tests, timestep/refuelling checks, the
pre-refactor byte/digest characterization, and digest-mismatch atomicity.

### T3 - full headless Core and Golden regression

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:Path="$sdkPath;$env:Path"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\AppData\Local\Temp\candu-cli-home-p6-integration'; & (Join-Path $sdkPath 'dotnet.exe') test 'C:\Users\infin\candu\ReactorSim.sln' --configuration Release --nologo --no-restore --logger 'console;verbosity=minimal'
```

Result: exit code 0; Core `158/158` passed and Golden `19/19` passed, with
zero failures and zero skips.

### Independent code review (high)

Reviewer context `01a031fc-bc91-7323-982f-7c4b9758c6b5` returned `PASS`.
It found no High or Medium issue and no required correction. Actual model,
reasoning, and receipt verification are `UNVERIFIED`.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No request-level telemetry exposed. |
| Cached input tokens | Unavailable | No request-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No request-level telemetry exposed. |
| Output tokens | Unavailable | No request-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No request-level telemetry exposed. |
| Total tokens | Unavailable | Goal-service aggregate is not allocable to this task. |
| Estimated cost | Unavailable | No price source or request-level allocation exposed. |
| Goal-service total | Unavailable | Reported separately from request totals; not added. |

Cost formula/basis: unavailable; no provider billing receipt or allocable
worker/reviewer request telemetry was exposed, and no per-worker cost is
inferred.

## Numerical differences

Not applicable; no production numerical behavior changed. The Core test count
rose from `156` to `158` only because two integration tests were added. Golden
remained `19/19`. No approved physics, reference, tolerance, or golden value
changed.

## Deferred validation

- Dedicated integrated-path tests for Manual/Held, saturation, and
  phase-token mismatch are deferred to a later queue/internals task; adjacent
  P6-T06 focused tests already cover the underlying source/rank, saturation,
  phase-token, and rollback invariants.
- Unity T4, mobile T5, and reference-case T6 evidence are not applicable:
  this task changes only engine-neutral Core contracts and focused tests, with
  no Unity adapter, platform toolchain, reference baseline, or golden artifact
  change.
- Production CANDU behavior, external-reference comparisons, and unconditional
  G6 RRS comparison authority remain deferred under the existing Phase 6
  synthetic/test-only boundary.

## Blockers, risks, and follow-up

- Blockers: none within the bounded task.
- Risk triggers: yes. The change affects a public Core contract boundary,
  deterministic queue admission, source binding, canonical bytes/digests, and
  atomic rollback routing; T3 and independent code review (high) were run.
- Risks accepted or deferred: integrated Manual/Held and phase-token path
  coverage is a Low follow-up only; no correctness finding was identified and
  the underlying P6-T06 paths remain covered.
- Follow-up work: `P6-INTERNALS-01`; retain the same no-schema/no-physics
  boundary and split the task if queue ownership and digest deduplication are
  not one reviewable candidate.

## Next eligible task

`P6-INTERNALS-01` - consolidate queue and digest implementation details after
a compatibility inventory, without changing public v1 types, serialization,
fixture bytes, digests, replay bytes, or error codes.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`Refactoring_implementation_plan_2026-08-21.md`](../Refactoring_implementation_plan_2026-08-21.md),
[`ROUND-2026-08-17-P6-RRS-CHAIN.md`](ROUND-2026-08-17-P6-RRS-CHAIN.md), and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
