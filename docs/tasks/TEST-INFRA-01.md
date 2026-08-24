# TEST-INFRA-01 - artifact-portable full-suite test infrastructure

## Outcome

Status: COMPLETE

The full headless test suite now resolves declared fixture data from each test
assembly's output directory instead of inferring a repository root from a
fixed parent depth or searching for `ReactorSim.sln`. Core and Golden test
projects explicitly copy their required data into output, and both projects
use the shared test-only locator for diagnostics and artifact access. The
standard wrapper passes with a fresh `--artifacts-path` relocation.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: goal thread `01a031df-5487-7a53-8ec6-865fa2cb9353`; no request-level model receipt exposed
- Attempts: one bounded implementation attempt; elapsed time: `Unavailable`
- Artifact/checkpoint status: produced
- Review disposition: not applicable; this task changed only test infrastructure and test-only fixture access
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable; no risk trigger requiring independent code review fired

## Files created or changed

- `tests/TestInfrastructure/TestDataLocator.cs` - shared output-root locator with traversal checks and actionable missing-asset diagnostics.
- `tests/ReactorSim.Core.Tests/TestDataLocatorTests.cs` - focused locator success and missing-declared-asset diagnostics.
- `tests/ReactorSim.Core.Tests/ReactorSim.Core.Tests.csproj` - explicit Core Phase 6/scenario fixture declarations and output copying; shared locator link.
- `tests/ReactorSim.Golden.Tests/ReactorSim.Golden.Tests.csproj` - shared locator link.
- `tests/ReactorSim.Core.Tests/P6T02LiquidZoneInfluenceMapTests.cs` - output-based liquid-zone package/manifest access.
- `tests/ReactorSim.Core.Tests/P6T03AdjusterContractsTests.cs` - output-based adjuster package/manifest access.
- `tests/ReactorSim.Core.Tests/P6T04BulkPoisonContractsTests.cs` - output-based poison package/manifest access.
- `tests/ReactorSim.Core.Tests/P6T05RrsControllerContractsTests.cs` - output-based controller package/manifest access.
- `tests/ReactorSim.Core.Tests/P6T07ScenarioEvidenceTests.cs` - output-based scenario and declared input-package access.
- `tests/ReactorSim.Golden.Tests/P4T06G4BAdmittedCandidateConsumerTests.cs` - shared locator for copied candidate artifacts.
- `tests/ReactorSim.Golden.Tests/P4T06G4EIndependentReproductionConsumerTests.cs` - shared locator for copied reproduction artifacts.
- `tests/ReactorSim.Golden.Tests/P4T06G4ICandidateConsumerTests.cs` - shared locator for copied candidate artifacts.
- `tests/ReactorSim.Golden.Tests/P4T06G4JRepresentativeConsumerTests.cs` - shared locator for copied representative artifacts.
- `tests/ReactorSim.Golden.Tests/P4T06G4KIndependentConsumerTests.cs` - shared locator for copied independent artifacts.
- `tests/ReactorSim.Golden.Tests/P5T10ReducedModelSequenceComparisonTests.cs` - shared locator for copied sequence artifacts.
- `docs/PROJECT_SCOPE.md` - current recovery status and next handoff.
- `docs/tasks/TEST-INFRA-01.md` - this report.

`tools/Test-FullHeadlessSuite.ps1`, runtime Core code, CLI code, Unity code,
physics specifications, schemas, data-pack content, tolerances, and golden
values were inspected but not changed.

## Assumptions and design choices

- The test output directory is the only runtime location used for declared
  test assets. This remains valid when `dotnet test --artifacts-path` relocates
  binaries and avoids repository-layout assumptions.
- Core fixture declarations preserve the repository-relative `data/...`
  structure below `TestData/`, allowing P6-T07 to validate its declared input
  paths and manifests without a repository search.
- Golden fixture declarations were already output-portable; their helper
  methods now use the same locator and diagnostic behavior as Core tests.
- The pinned SDK prerequisite is the existing `10.0.302` installation at
  `C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302`, matching
  `global.json` with roll-forward disabled. The validation environment also
  set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, an isolated `DOTNET_CLI_HOME`,
  and the pinned SDK at the front of `PATH`.
- Literature digest applicability: `NotApplicable`; no physics equation,
  constant, unit, normalization, tolerance, golden value, or numerical
  authority was selected or changed.

## Validation commands and results

### T0 - final diff whitespace check

```text
git diff --check
```

Result: exit code `0`; no whitespace errors.

### T1 - focused locator fixture

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:PATH="$sdkPath;$env:PATH"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\candu\tmp\dotnet-cli-home'; & (Join-Path $sdkPath 'dotnet.exe') test tests\ReactorSim.Core.Tests\ReactorSim.Core.Tests.csproj --configuration Release --nologo --no-restore --filter 'FullyQualifiedName~TestDataLocatorTests' --logger 'console;verbosity=minimal'
```

Result: exit code `0`; `2/2` passed, zero failures/skips. The focused suite
verified both a declared copied asset and the actionable missing-asset
diagnostic.

### T3 - direct pinned full headless regression

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:PATH="$sdkPath;$env:PATH"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\candu\tmp\dotnet-cli-home'; & (Join-Path $sdkPath 'dotnet.exe') test ReactorSim.sln --configuration Release --nologo --no-restore --logger 'console;verbosity=minimal'
```

Result: exit code `0`; Core `156/156` passed and Golden `19/19` passed, with
zero failures and zero skips.

### T3 - fresh artifact-output wrapper regression

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:PATH="$sdkPath;$env:PATH"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_CLI_HOME='C:\Users\infin\candu\tmp\dotnet-cli-home'; $artifactsPath = Join-Path ([System.IO.Path]::GetTempPath()) ('candu-test-infra-01-' + [Guid]::NewGuid().ToString('N')); & .\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath $artifactsPath
```

Result: exit code `0` with a newly created artifact root. The wrapper's TRX
counters reported Core `total=156, executed=156, passed=156, failed=0` and
Golden `total=19, executed=19, passed=19, failed=0`. The wrapper restored the
pinned solution projects and executed from relocated Debug outputs, proving
the fixture declarations are artifact-portable.

The first SDK invocation without the isolated validation environment failed
before MSBuild because the minimal SDK image lacked its first-use certificate
component; the final commands above resolved that environment prerequisite.
An initial sandboxed build also lacked read access to the standard Windows SDK
probe path; the final validation ran with the approved read-only escalation.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No request-level telemetry exposed. |
| Cached input tokens | Unavailable | No request-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No request-level telemetry exposed. |
| Output tokens | Unavailable | No request-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No request-level telemetry exposed. |
| Total tokens | Unavailable | No request-level telemetry exposed. |
| Estimated cost | Unavailable | No provider billing receipt or allocable request receipt exposed. |
| Goal-service total | Unavailable | No separate goal-service total exposed; not added to request totals. |

Cost formula/basis: unavailable; no price source, provider billing receipt, or
allocable request-level telemetry was exposed.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, sign, convergence rule,
tolerance, physics data, schema, or golden value changed. The total Core test
count increased from the pre-task `154` to `156` solely because two focused
test-infrastructure tests were added; direct and artifact-output wrapper
discovery match exactly, while Golden remains `19`.

## Deferred validation

- Unity EditMode/PlayMode and mobile checks - deferred; no Unity adapter,
  serialization contract, platform behavior, or runtime code changed.
- Reference reproduction/regeneration - deferred; no reference baseline or
  golden data changed.

## Blockers, risks, and follow-up

- Blockers: none.
- Risk triggers: none; this was a test-only artifact-location change and did
  not affect determinism, runtime contracts, physics, schemas, or data.
- Risks accepted or deferred: the existing pinned SDK prerequisite remains
  machine-specific and is documented rather than installed or changed by this
  task; no CI workflow or new dependency was added.
- Follow-up work: `P6-INTEGRATION-01` is the next eligible task. It is a
  separate Core contract refactor and requires its own focused atomicity/
  mismatch evidence, T3 regression, and independent code review (high).

## Next eligible task

`P6-INTEGRATION-01` - make controller-to-queue admission atomic, preserving v1
contracts, canonical bytes/digests, synthetic values, and queue behavior.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`Refactoring_implementation_plan_2026-08-21.md`](../Refactoring_implementation_plan_2026-08-21.md),
[`DOC-REVIEW-01.md`](DOC-REVIEW-01.md), and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
