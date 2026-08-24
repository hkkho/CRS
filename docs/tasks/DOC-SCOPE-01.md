# DOC-SCOPE-01 - Concise project scope and delivery register

## Outcome

Status: COMPLETE

Rewrote `docs/PROJECT_SCOPE.md` from a duplicated phase/task ledger into a
compact delivery register. The new version keeps the required searchable keys,
preserves the active G2 disposition, corrects the stale P0-T01 missing-report
claim, distinguishes verified from unverified review evidence, and gives the
unstarted P4-T04 diagnostics slice a detailed, non-authorizing handoff.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root documentation implementer
- Requested model / reasoning: GPT-5.6 Luna / high, the repository default for
  bounded documentation work
- Actual model / reasoning: `UNVERIFIED`; no task-level execution receipt or
  telemetry was exposed
- Execution receipt or telemetry source: Unavailable
- Attempts: 1; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; the task changes documentation only and
  makes no runtime, physics, tolerance, golden-data, schema, or gate decision
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/PROJECT_SCOPE.md` - replaced the repeated historical task matrix with
  a concise evidence register, active Phase 4 plan, remaining roadmap, and
  maintenance contract.
- `docs/tasks/DOC-SCOPE-01.md` - this report.

The complete implementation plan, prior scope index, task reports through
`P4-T03`, `G3`, `G2-FC-01`, `G2-R4`, and the P2-T02 solver specification were
inspected but not changed. No source, test, reference, Unity, data-pack, or
private-roadmap file was changed.

## Assumptions and design choices

- The implementation plan remains the execution baseline; specifications and
  task/gate reports are cited instead of restating their detail.
- The P4-T04 section records the explicit P4-T03 handoff and P2-T02 behavior;
  it is not task authorization and does not choose a tolerance, equation, or
  public API shape.
- Literature applicability: `NotApplicable`. This documentation task does not
  select, change, validate, or review a physics equation, constant, unit,
  normalization, coefficient, reference case, tolerance, or golden value.
- `G2` remains exactly `FORCED CLOSED / WAIVED`, even though historical
  technical reruns passed. The rewrite preserves that distinction.

## Validation commands and results

### Required-reading and current-state audit - T0

```powershell
Get-Content -Raw docs\Implementation_plan.md
Get-Content -Raw docs\PROJECT_SCOPE.md
Get-Content -Raw docs\tasks\PLAN-C03.md
Get-Content -Raw docs\tasks\PLAN-C04.md
Get-Content -Raw docs\tasks\P4-T01.md
Get-Content -Raw docs\tasks\P4-T02.md
Get-Content -Raw docs\tasks\P4-T03.md
Get-Content -Raw docs\gates\G2-FC-01.md
Get-Content -Raw docs\gates\G2-R4.md
Get-Content -Raw docs\gates\G3.md
Get-Content -Raw docs\spec\two-group-solver-normalization-convergence-v1.md
rg -n "P4-T04|P4-T03|G4|PHASE-4" docs\PROJECT_SCOPE.md
git status --short --untracked-files=all
```

Result: PASS. The reports establish that P4-T01 through P4-T03 are complete,
G3 is PASS, G2 remains administratively waived, and P4-T04 is the next
unstarted Phase 4 handoff. The legacy scope index's claim that P0-T01 had no
report was contradicted by the existing P0-T01 report and was corrected.

### Scope content, links, and whitespace - T0/T1

```powershell
$ErrorActionPreference = 'Stop'
$scopePath = 'docs\PROJECT_SCOPE.md'
$scope = Get-Content -Raw -LiteralPath $scopePath
$required = @(
    '# CANDU Refuelling Game - Project Scope and Delivery Register',
    'FORCED CLOSED / WAIVED', 'G2-R4', 'G3', 'P0-T01',
    'P4-T01', 'P4-T02', 'P4-T03', 'P4-T04',
    'PHASE-0', 'PHASE-11', 'G4', 'ReactorSim.Golden.Tests',
    'code review (high)')
foreach ($term in $required) {
    if ($scope -notmatch [regex]::Escape($term)) {
        throw "Missing required scope term: $term"
    }
}
if ($scope -match 'Report not found in current task listing') {
    throw 'Stale P0-T01 missing-report wording remains.'
}
foreach ($obsolete in @(
    '## Executable task and gate index',
    '## Later-phase work-package scope',
    '## Review-depth matrix')) {
    if ($scope -match [regex]::Escape($obsolete)) {
        throw "Obsolete duplicated section remains: $obsolete"
    }
}
$badWhitespace = @()
$line = 0
Get-Content -LiteralPath $scopePath | ForEach-Object {
    $line++
    if ($_ -match '[ \t]+$') { $badWhitespace += "${scopePath}:$line" }
}
if ($badWhitespace.Count -gt 0) { throw ($badWhitespace -join "`n") }
$links = [regex]::Matches($scope, '\]\(([^)]+)\)') |
    ForEach-Object { $_.Groups[1].Value }
$missingLinks = @()
foreach ($link in $links) {
    if ($link -match '^(https?://|#)') { continue }
    $target = ($link.Trim('<>') -replace ':\d+$', '')
    $resolved = Join-Path (Split-Path -Parent $scopePath) $target
    if (-not (Test-Path -LiteralPath $resolved)) {
        $missingLinks += "$link -> $resolved"
    }
}
if ($missingLinks.Count -gt 0) { throw ($missingLinks -join "`n") }
"SCOPE_STATIC_PASS lines=$((Get-Content -LiteralPath $scopePath).Count) links=$($links.Count)"
rg -n "P4-T04|PHASE-4|G4|FORCED CLOSED / WAIVED|G2-R4|P0-T01" $scopePath
```

Result: PASS. The rewritten scope has 218 lines and 12 valid local links. It
contains every required current key, has no trailing whitespace, removes the
three duplicated legacy sections, and retains the G2/G3/P4 status evidence.

### Final documentation scope inspection - T0

```powershell
$paths = @('docs\PROJECT_SCOPE.md', 'docs\tasks\DOC-SCOPE-01.md')
$bad = @()
foreach ($path in $paths) {
    $line = 0
    Get-Content -LiteralPath $path | ForEach-Object {
        $line++
        if ($_ -match '[ \t]+$') { $bad += "${path}:$line" }
    }
}
if ($bad.Count -gt 0) { throw ($bad -join "`n") }
rg -n "P4-T04|FORCED CLOSED / WAIVED|G2-R4|P0-T01|PHASE-11" docs\PROJECT_SCOPE.md
git status --short --untracked-files=all
```

Result: PASS. The corrected final audit returned
`DOC_SCOPE_FINAL_PASS scope_lines=218 report_lines=204 links=validated
status_targets=2`. Both target documents were present in the unborn-worktree
status; their local links and trailing-whitespace checks passed.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No task-level telemetry exposed. |
| Estimated cost | Unavailable | No verified price source or task allocation exposed. |
| Goal-service total | Unavailable | Not exposed; not added to request totals. |

Cost formula/basis: unavailable; no per-task usage allocation or provider price
source was exposed, so no cost has been invented.

## Numerical differences

Not applicable. No numerical behavior, equation, coefficient, unit,
normalization, tolerance, reference baseline, or golden data changed.

## Deferred validation

- T3-T6 are not applicable to this documentation-only task. The P4-T04
  convergence implementation will trigger T3 and independent `code review
  (high)`; G4 owns the later solver/golden validation.
- The private roadmap was not changed because no phase/gate disposition,
  approved specification, or runtime model changed; this task only reconciled
  the scope register with existing evidence.

## Blockers, risks, and follow-up

- Blockers: none for the documentation rewrite.
- Risk triggers: none. The task made no runtime-affecting change.
- Risks accepted or deferred: the register is a manually maintained routing
  document, so each future task/gate must update it when status changes. The
  unverified P4-T03 reviewer receipt remains accurately labelled.
- Follow-up work: obtain a separate bounded task request for P4-T04. Do not
  begin it as part of this documentation task.

## Next eligible task

`P4-T04` - outer convergence and invalid-state diagnostics, after a separate
bounded task request. G4 remains pending.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`docs/tasks/P4-T03.md`](P4-T03.md), and
[`docs/spec/two-group-solver-normalization-convergence-v1.md`](../spec/two-group-solver-normalization-convergence-v1.md).
