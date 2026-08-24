# P6-INTERNALS-01 - consolidate Phase 6 queue and digest implementation details

## Outcome

Status: COMPLETE

The Phase 6 internal compatibility inventory is complete and the bounded
implementation preserves the public v1 boundary. `RrsQueueStateV1` remains the
read-only P6-T05 controller-state projection, while `P6T06QueueStateV1` is the
documented semantic owner of queue admission and transitions at the
integration boundary. The two public types and their canonical layouts remain
distinct.

The genuinely identical versioned mapping-collection byte layout is now owned
by the internal `Phase6CanonicalDigestPrimitives` helper. Liquid-zone and
adjuster grouping callers continue to own their approved ordering and entry
types, while the shared helper preserves the schema identifier, version,
identity, mapping-version, entry count, entry bytes, and optional trailing
digest sequence. Approved characterization assertions cover both existing
mapping digests.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: current goal/thread
  `01a031df-5487-7a53-8ec6-865fa2cb9353`; request-level receipt unavailable
- Attempts: 1; elapsed time: `Unavailable`
- Artifact/checkpoint status: produced; implementation checkpoint
  `a8a2a52` (`refactor: share phase6 mapping canonical bytes`)
- Review disposition: PASS
- Review evidence verification: `UNVERIFIED`
- Reviewer reuse/fresh-review rationale: fresh reviewer
  `01a0320a-3434-7b51-9a46-121ee357acdc` because this was a changed candidate
  from the prior P6-INTEGRATION-01 review; one bounded review was used and the
  reviewer was not rerun after PASS

The independent reviewer reported PASS with no High, Medium, or Low findings.
The requested review lane was GPT-5.6 Luna / high; actual model, effort, and
receipt telemetry were unavailable and are therefore not treated as verified.

## Files created or changed

- `src/ReactorSim.Core/Domain/Phase6CanonicalDigestPrimitives.cs` - adds the
  internal shared writer and digest primitive for the identical versioned
  mapping-collection body.
- `src/ReactorSim.Core/Domain/Phase6LiquidZoneContracts.cs` - routes liquid-zone
  grouping digest and canonical-byte generation through the shared primitive
  without changing ordering or the public contract.
- `src/ReactorSim.Core/Domain/Phase6AdjusterContracts.cs` - routes adjuster-bank
  grouping digest and canonical-byte generation through the shared primitive
  without changing ordering or the public contract.
- `src/ReactorSim.Core/Domain/Phase6RrsContracts.cs` - documents the retained
  read-only projection/transition-owner boundary.
- `src/ReactorSim.Core/Domain/Phase6QueueTransitionContracts.cs` - documents
  `P6T06QueueStateV1` as the integration-boundary owner of queue admission and
  transitions.
- `tests/ReactorSim.Core.Tests/P6T02LiquidZoneInfluenceMapTests.cs` - adds the
  approved liquid-zone mapping-digest characterization.
- `tests/ReactorSim.Core.Tests/P6T03AdjusterContractsTests.cs` - adds the
  approved adjuster grouping-digest characterization.
- `docs/tasks/P6-INTERNALS-01.md` - this task report.
- `docs/PROJECT_SCOPE.md` - records completion and the next eligible task.

Pre-existing authorities and prerequisite reports were inspected but not
changed, including `AGENTS.md`, the complete `docs/Implementation_plan.md`,
`docs/Refactoring_implementation_plan_2026-08-21.md`, the Phase 6 chain,
the frozen RRS specification, P6-T05 through P6-T07 reports, and the
`P1-T08` literature report/digest.

## Assumptions and design choices

- The compatibility inventory treated `RrsQueueStateV1` as a supplied,
  immutable P6-T05 projection and `P6T06QueueStateV1` as the P6-T06 immutable
  transition-state owner. They are not aliases because their public v1
  canonical layouts and command types differ.
- The shared helper is limited to the two grouping collections whose prior
  byte sequences are structurally identical. RRS influence maps, liquid-zone
  influence maps, adjuster influence maps, queue states, and bulk-poison
  contracts were not merged because their canonical fields or ownership
  semantics differ.
- Callers retain sorting, validation, schema identifiers, entry-byte
  production, and public APIs. The helper only writes the already-approved
  common sequence and optional digest.
- `P1-T08` applicability: `NotApplicable`. This task performs an internal
  canonical-byte implementation refactor and ownership documentation only; it
  selects or changes no physics equation, coefficient, map value, unit,
  sign, normalization, tolerance, reference case, or golden-data authority.

## Validation commands and results

### T0 - diff and worktree check

```text
git diff --check
git status --short
```

Result: exit code 0 for `git diff --check`; no whitespace errors were
reported. The working-tree change set contained only the files listed above.

### T1 - focused grouping characterization

```text
$sdkPath = 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkPath
$env:Path = "$sdkPath;$env:Path"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = 'C:\Users\infin\AppData\Local\Temp\candu-cli-home-p6-internals'
& (Join-Path $sdkPath 'dotnet.exe') test 'tests/ReactorSim.Core.Tests/ReactorSim.Core.Tests.csproj' --no-restore --filter 'FullyQualifiedName~P6T02LiquidZoneInfluenceMapTests|FullyQualifiedName~P6T03AdjusterContractsTests' --logger 'console;verbosity=minimal'
```

Result: exit code 0; Core focused tests passed `11/11`, with zero failures and
zero skips. The approved liquid-zone mapping digest remained
`0be2b126d2fbe6fa17bda290fba044468192266159d11921330a6352f688a713`; the
approved adjuster grouping digest remained
`10548c8327f72dc9fa94f61b7698d3fdd5357ea040bae4ff32a422cbbe0d6060`.

### T3 - full headless Core and Golden regression

```text
$sdkPath = 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkPath
$env:Path = "$sdkPath;$env:Path"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = 'C:\Users\infin\AppData\Local\Temp\candu-cli-home-p6-internals'
& (Join-Path $sdkPath 'dotnet.exe') test 'ReactorSim.sln' --no-restore --logger 'console;verbosity=minimal'
```

Result: exit code 0; Core passed `158/158` and Golden passed `19/19`, with
zero failures and zero skips.

### Independent code review (high)

The bounded reviewer inspected the current candidate read-only and reported
PASS with no required corrections. The review confirmed the shared helper
preserves the prior canonical field order and optional digest behavior, both
grouping callers preserve sorting and entry bytes, and the public queue types
remain distinct with P6-T06 as the transition/admission owner. Reviewer
focused checks reported 16/16 P6-T01/T02/T03 tests and 24/24 P6-T05/T06/T07
queue/integration tests; this supplemental reviewer telemetry is not treated
as receipt-verified.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | request-level telemetry unavailable |
| Cached input tokens | `Unavailable` | request-level telemetry unavailable |
| Cache-write input tokens | `Unavailable` | request-level telemetry unavailable |
| Output tokens | `Unavailable` | request-level telemetry unavailable |
| Reasoning output tokens | `Unavailable` | request-level telemetry unavailable |
| Total tokens | `Unavailable` | request-level telemetry unavailable |
| Estimated cost | `Unavailable` | no allocable request receipt or price basis |
| Goal-service total | `Unavailable` | goal aggregate telemetry unavailable; not added to request totals |

Cost formula/basis: unavailable; no request-level billing receipt or
price/date basis was exposed, so no per-worker or per-task allocation is
invented.

## Numerical differences

Not applicable; no numerical behavior changed. Canonical bytes and digest
outputs were characterized as unchanged, and no equation, coefficient, unit,
normalization, tolerance, or golden value was selected or modified.

## Deferred validation

- Unity EditMode/PlayMode and desktop-player checks (T4) - not applicable to
  this Core-only internal helper and documentation change.
- Mobile/device/performance checks (T5) - not applicable; no platform or
  runtime-affecting dependency changed.
- Reference-case regeneration or manifest comparison (T6) - not applicable;
  no reference baseline or data pack changed.

The artifact-output wrapper baseline remains covered by `TEST-INFRA-01`; this
task's required direct T1/T3 evidence passed.

## Blockers, risks, and follow-up

- Blockers: none within the bounded task.
- Risk triggers: yes. The candidate touched deterministic canonical bytes,
  digests, and a public Core contract ownership boundary; the required T3
  regression and independent code review (high) were run.
- Risks accepted or deferred: no correctness finding. External/production RRS
  authority and unconditional G6 comparison remain outside this refactor and
  unchanged from the existing conditional synthetic-only boundary.
- Follow-up work: `CORE-NAMING-01` is the next sequenced discovery task. It
  must remain inventory/decision-record-only; any public-contract migration
  requires its own separately authorized risk-triggered task, T3 regression,
  and independent review.

## Next eligible task

`CORE-NAMING-01` - produce a compatibility inventory and minimal no-break
migration plan for task-ID-named runtime types. No bulk rename is authorized
by that discovery task.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`Refactoring_implementation_plan_2026-08-21.md`](../Refactoring_implementation_plan_2026-08-21.md),
[`ROUND-2026-08-17-P6-RRS-CHAIN.md`](ROUND-2026-08-17-P6-RRS-CHAIN.md),
and the frozen Phase 2 RRS specification.
