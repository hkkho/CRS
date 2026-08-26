# XSEC-02 - DRAGON Source-to-Runtime Mapping Specification

## Outcome

Status: BLOCKED

Effectiveness: PARTIAL

This task defines the candidate DRAGON5 lattice source case and its complete
semantic projection to the existing Core coefficient-table contract. It does
not execute a lattice case, create a runtime pack, or admit golden data. The
candidate mapping specification received its same-reviewer technical PASS, but
the task cannot complete because its required portable-artifact T3 wrapper
exposes an existing CLI approved-data lookup failure. The failure is isolated
to the temporary `--artifacts-path` layout; the direct full solution run passes.

Applicable P1-T08 digest rows: S1-R04, S1-R05, S5-R02, S5-R03, S5-R04,
S5-R09, and S6-R03.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; execution telemetry is unavailable.
- Execution receipt or telemetry source: current Codex task; no provider
  request-level receipt was exposed.
- Attempts: one bounded specification task; elapsed time: unavailable as an
  allocable task total.
- Artifact/checkpoint status: produced; checkpoint pending after this report
  and scope reconciliation.
- Review disposition: PASS, candidate-mapping scope only.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: one bounded same-reviewer correction
  cycle with Popper (`01a03b98-9b86-7f22-9068-76bb1ce6b01e`); no fresh-review
  swarm was started.

## Files created or changed

- docs/spec/xsec-source-runtime-mapping-v1.md - candidate lattice case,
  explicit two-group/source-field/unit mapping, and fail-closed boundaries.
- reference/manifests/xsec-02-mapping-spec-v1.json - path-free machine summary
  of the candidate mapping authority.
- docs/tasks/XSEC-02.md - this task report.

Pre-existing authorities, P1-T08 evidence, XSEC-01/XSEC-RIGHTS-01 records, and
the Core contracts were inspected and not changed.

## Assumptions and design choices

- The official TCWU11 procedure is used as a documented geometry/depletion
  source. The candidate pins the Version5 commit, both source procedure
  identities, canonical-LF hashes, JEFF-3.1/XMAS-172 route, exhaustive
  allowed-deck deltas, and the post-EDI/pre-SPH export object. It is not the
  old P1-T02 smoke input and does not inherit P1-T02 admission.
- The mapping uses direct named source records and documented compressed-scatter
  profiles. It does not infer semantics from source-array order.
- `EFIS` plus `FLUX-INTG` supplies an explicit fission-only energy-per-fission
  ratio, avoiding an arbitrary runtime energy constant or all-reaction
  `H-FACTOR` substitution.
- No full-core topology or DONJON input is available from the approved evidence;
  XSEC-CORE-01 is defined as the required separate authority before XSEC-04.

## Validation commands and results

### T0/T1 - mapping-document, manifest, and authority checks

```text
$mapping = Get-Content -Raw reference/manifests/xsec-02-mapping-spec-v1.json |
  ConvertFrom-Json
# Assert format, Version5 commit, exactly two pinned source files, their paths,
# post-EDI/pre-SPH stage, no project solver controls, three bounded deck deltas,
# burnup knots, EFIS presence, and candidate-only status; reject MXIT/EXTE.
git diff --check
```

Result: PASS (`XSEC-02_MAPPING_T0_PASS`, `GIT_DIFF_CHECK_PASS`). The manifest
and specification have the expected candidate-only identity; removed solver
controls do not remain.

### T0 - project formatting

```text
$sdkRoot = Join-Path $env:TEMP 'candu-dotnet-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkRoot
$env:PATH = $sdkRoot + ';' + $env:PATH
.\tools\Check-Format.ps1
```

Result: PASS (exit 0) with the project-pinned .NET SDK 10.0.302 installed in
an external temporary directory. No project dependency or toolchain version was
changed.

### T3 - direct full solution control

```text
dotnet test ReactorSim.sln --no-restore --results-directory
  $env:TEMP\candu-xsec-02-t3-default-results-<guid>
  --logger 'console;verbosity=minimal'
```

Result: PASS (exit 0): Core `208/208`, Golden `26/26`, CLI `38/38`; no skipped
or failed tests. This demonstrates that the repository-layout data discovery
works and does not validate the portable artifact layout.

### T3 - required portable full-suite wrapper

```text
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath
  $env:TEMP\candu-xsec-02-t3-artifacts
```

Result: FAIL. Golden `26/26` and Core `208/208` passed. CLI had `36` failures
and `2` passes (38 total): the tests could not locate approved Phase 8 scenario,
scoring, and policy parameter packs from the temporary artifact output tree.
The same tests pass in the repository layout above. This is a data-pack
artifact-portability defect, not a DRAGON mapping result, but its T3 failure
blocks this risk-triggered task from completion.

### Independent code review (high)

```text
Same reviewer Popper (01a03b98-9b86-7f22-9068-76bb1ce6b01e),
submission 01a03bc8-e0c6-7503-b8c7-8163636c941c and correction submission
01a03bcc-eb56-78b1-9c78-d24835ffecc0
```

Result: PASS after one same-reviewer correction cycle. The reviewer verified
the pinned source blobs, bounded SPH omission, no unsupported solver controls,
two-group mapping, and Core/full-core exclusions. Actual model/reasoning
telemetry: UNVERIFIED. The PASS approves only the candidate mapping document;
it does not override the T3 wrapper failure or admit source data, a rerun,
runtime pack, full-core case, golden data, release, or Unity use.

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

Not applicable; no runtime numerical behavior or baseline changed.

## Deferred validation

- T6 lattice reruns and semantic export comparison - owned by XSEC-03.
- DONJON full-core/static run - blocked pending XSEC-CORE-01, then owned by
  XSEC-04.
- Pack/Core/CLI/Unity validation - owned by XSEC-05 through XSEC-07.

## Blockers, risks, and follow-up

- Blockers: the mandatory portable-artifact T3 wrapper fails because the CLI
  cannot locate already-approved Phase 8 data packs under a temporary
  `--artifacts-path` build layout. This must be repaired and revalidated in a
  separate non-physics test-infrastructure task before XSEC-02 can complete.
- Risk triggers: source-to-runtime mapping is a numerical-data/interface
  trigger; T3 and independent code review (high) are required.
- Risks accepted or deferred: the case remains candidate-only; no source
  rerun, topology, conductance, reference comparison, golden threshold, or
  runtime admission is claimed. The candidate's post-EDI/pre-SPH deck delta is
  only a reviewed source-mapping decision; XSEC-03 must still hash, run, and
  prove it.
- Follow-up work: define and execute `TEST-INFRA-02` to repair only the
  portable CLI approved-data lookup and rerun the wrapper. Then re-enter
  XSEC-02 for its required passing T3 checkpoint. `XSEC-03` remains blocked;
  `XSEC-CORE-01` must complete before XSEC-04 can begin.

## Next eligible task

`TEST-INFRA-02` - portable CLI approved-data artifact recovery, after its
bounded task-definition record is created. It must not modify XSEC physics,
mapping, source data, or runtime schemas.

Source: AGENTS.md and CODEX_TASK_TEMPLATE.md.
