# TEST-INFRA-02 - portable CLI approved-data artifact recovery

## Outcome

Status: COMPLETE

The CLI test project now declares the exact existing approved Phase 8 scenario,
scoring, and baseline-policy artifacts, their manifests, their three owner
approval records, and the root marker required by the existing manifest binding.
They are copied into the relocated test-output tree at the same
repository-relative paths the unchanged CLI validation expects. No runtime
lookup, pack bytes, approval bytes, schema, physics behavior, or numerical
authority changed.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; request-level execution telemetry is
  unavailable.
- Execution receipt or telemetry source: current Codex task; no provider
  request-level receipt was exposed.
- Attempts: one bounded implementation attempt; elapsed time: unavailable as
  an allocable task total.
- Artifact/checkpoint status: produced; checkpoint pending after this report
  and scope reconciliation.
- Review disposition: not applicable. The frozen runtime/data boundary was not
  crossed, and no AGENTS.md independent-review trigger fired.
- Review evidence verification: not applicable.
- Reviewer reuse/fresh-review rationale: not applicable.

## Files created or changed

- `docs/tasks/TEST-INFRA-02-REQUEST.md` - bounded owner-authorized recovery
  definition, frozen inputs, exclusions, and validations.
- `tests/ReactorSim.Cli.Tests/ReactorSim.Cli.Tests.csproj` - explicit
  test-output declarations for the existing approved Phase 8 artifacts,
  manifests, approval records, and root marker.
- `docs/tasks/TEST-INFRA-02.md` - this report.

The CLI/Core source, Unity adapter, scenario/scoring/policy artifacts,
manifests, approval records, XSEC mapping, physics specifications, schemas,
tolerances, and golden values were inspected but not changed.

## Assumptions and design choices

- The existing three pack loaders deliberately bind their artifact and manifest
  paths to a root marked by `AGENTS.md` and require their corresponding owner
  record. Copying those already-approved support files to the test output
  preserves that contract; weakening or rewriting the runtime locator would not
  be a test-infrastructure-only repair.
- Every copied data file retains its current `data/scenarios/...` relative path,
  and every approval record retains its current `docs/tasks/...` relative path.
  The manifests therefore retain their exact path and SHA-256 verification.
- Literature digest applicability: NotApplicable. This task chooses no physics
  equation, constant, unit, normalization, tolerance, golden/reference value,
  or nuclear-data input.

## Validation commands and results

### T0 - formatting and whitespace

```text
$sdkRoot = Join-Path $env:TEMP 'candu-dotnet-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkRoot
$env:PATH = $sdkRoot + ';' + $env:PATH
.\tools\Check-Format.ps1
git diff --check
```

Result: PASS; both commands exited 0. The exact pinned .NET SDK was restored
outside the repository for validation; no project toolchain version changed.

### T1 - relocated CLI approved-data consumption

```text
dotnet test tests\ReactorSim.Cli.Tests\ReactorSim.Cli.Tests.csproj \
  --artifacts-path $env:TEMP\candu-test-infra-02-focused-<guid> \
  --filter 'FullyQualifiedName~P8T05BaselinePolicyTests' \
  --logger 'console;verbosity=minimal'
```

Result: PASS (exit 0): CLI `6/6`, zero failures/skips, from a fresh artifact
root. The tests load the relocated policy, scenario, and scoring authorities
through the unchanged default-path and manifest-validation flow.

The first focused invocation incorrectly combined a fresh artifact root with
`--no-restore`; it failed before compilation with `NETSDK1004` because that new
root had no generated assets file. The final command above permits its required
restore and passed. This was a validation-command correction, not a code or
data failure.

### T3 - direct full headless regression

```text
dotnet test ReactorSim.sln --no-restore \
  --results-directory $env:TEMP\candu-test-infra-02-direct-<guid> \
  --logger 'console;verbosity=minimal'
```

Result: PASS (exit 0): Core `208/208`, Golden `26/26`, CLI `38/38`; zero
failures and zero skips.

### T3 - portable full-suite wrapper

```text
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite \
  -ArtifactsPath $env:TEMP\candu-test-infra-02-full-<guid>
```

Result: PASS (exit 0): Core `208/208`, Golden `26/26`, CLI `38/38`; zero
failures and zero skips. This recovers the XSEC-02 blocking condition without
changing any XSEC artifact or runtime behavior.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed |
| Cached input tokens | Unavailable | No task-level telemetry exposed |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed |
| Output tokens | Unavailable | No task-level telemetry exposed |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed |
| Total tokens | Unavailable | No task-level telemetry exposed |
| Estimated cost | Unavailable | No reliable task-level price allocation |
| Goal-service total | Unavailable | Reported separately; not added to this task |

Cost formula/basis: unavailable. No per-worker or review cost is invented from
aggregate service usage.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, normalization,
convergence rule, tolerance, physics data, schema, pack byte, or golden value
changed.

## Deferred validation

- Unity EditMode/PlayMode and desktop-player checks - deferred; no Unity
  adapter, serialization contract, or player data-loading behavior changed.
- DRAGON5/DONJON5 reruns - deferred; no reference tool, source case, nuclear
  data, or reference baseline changed.
- XSEC mapping revalidation - owned by the separate `XSEC-02-R1` task, which
  must rerun the now-repaired portable T3 wrapper before XSEC-02 can be marked
  complete.

## Blockers, risks, and follow-up

- Blockers: none for this task.
- Risk triggers: none. This is output-only test-data declaration work; all
  runtime, interface, pack, and numerical behavior remained frozen.
- Risks accepted or deferred: the existing CLI runtime loaders still require a
  repository-marked root and approval-record presence. This task makes that
  current behavior portable for tests but does not decide deployment behavior.
- Follow-up work: define and execute `XSEC-02-R1` for revalidation only. It
  must not modify the reviewed mapping unless fresh validation exposes a new
  defect; XSEC-03 remains ineligible until it passes.

## Next eligible task

`XSEC-02-R1` - rerun the mandatory XSEC source-mapping T3 evidence after the
portable CLI data recovery, then finalize or retain the XSEC-02 disposition.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`TEST-INFRA-01.md`](TEST-INFRA-01.md), and
[`TEST-INFRA-02-REQUEST.md`](TEST-INFRA-02-REQUEST.md).
