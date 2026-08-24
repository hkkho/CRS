# DOC-REVIEW-01 - source, LLM-plan, test-evidence, and refactoring review

## Outcome

Status: COMPLETE

Completed a bounded source-code and planning review, surfaced fresh test results
in the living guide, regenerated PDF, and private-site source, and recorded a
proposal for follow-up refactoring. No simulation code, equation, unit,
tolerance, golden/reference value, public Core contract, or site deployment was
changed.

The direct pinned headless regression is green: Core `154/154`, Golden
`19/19`, and focused Phase 6 Core `44/44`, with zero failures/skips. The review
also identified a material test-infrastructure defect: the standard
artifact-output full-suite wrapper reports Core `142/154` because test data and
repository-root lookup are coupled to the normal build directory. Golden remains
`19/19` in that wrapper run. This is a portability/evidence defect, not a new
reactor-physics result.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer for documentation/review maintenance; one independent
  code-review (high) reviewer for the source assessment.
- Requested root model / reasoning: GPT-5.6 Luna / high, per repository
  routing.
- Actual root model / reasoning: `UNVERIFIED`; no root execution receipt or
  telemetry was exposed.
- Requested reviewer model / reasoning: `gpt-5.6-terra` / high.
- Actual reviewer model / reasoning: `UNVERIFIED`; the available reviewer
  result did not expose verifiable model/effort telemetry.
- Execution receipt or telemetry source: root unavailable; reviewer context
  `01a025e1-fe8b-7f31-9956-53cf766f12a1`, with no model/usage receipt.
- Attempts: one bounded root review/documentation attempt; one bounded
  independent reviewer attempt; elapsed time: `Unavailable`.
- Artifact/checkpoint status: produced the review proposal, updated guide/PDF,
  synchronized private-site PDF, and site source/test changes.
- Review disposition: `CONDITIONAL PASS` for the bounded source assessment.
  The reviewer found no reason to rewrite the engine-neutral architecture, but
  recorded the controller-to-queue seam, parallel queue representations, and
  duplicate influence-map digest code as refactoring candidates.
- Review evidence verification: `UNVERIFIED` because actual reviewer telemetry
  is unavailable.
- Reviewer reuse/fresh-review rationale: one fresh, read-only independent
  review was used for this candidate. No review swarm or retry was started.

## Files created or changed

- `docs/Refactoring_implementation_plan_2026-08-21.md` - proposal-only source
  and LLM-plan assessment, prioritized recovery sequence, boundaries, and
  acceptance evidence.
- `docs/physics/candu-nuclear-diffusion-student-guide.md` - version 2.7 status,
  G6 conditional boundary, current direct test results, wrapper limitation,
  traceability, and change record.
- `tools/build_physics_guide_pdf.py` - PDF cover status updated to match the
  guide's G6/test-evidence summary.
- `tools/Check-PhysicsGuideConsistency.ps1` - checks the current Phase 6/G6
  markers, P6 reports, source presence, and the controlling reopened P6-T02
  disposition instead of incorrectly reading its historical first attempt as
  current status.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - regenerated 22-page
  PDF artifact.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf`
  - byte-for-byte synchronized regenerated PDF.
- `tmp/private-physics-download-site/app/page.tsx` - authenticated download
  page now shows the latest Core/Golden/Phase 6 result and the wrapper caveat.
- `tmp/private-physics-download-site/app/roadmap/page.tsx` - derived Phase 6/G6
  status, current regression counts, and deferred RRS comparison boundary.
- `tmp/private-physics-download-site/app/globals.css` - visual treatment for
  the validation evidence note.
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - assertions
  for the current result, wrapper limitation, and no stale "Phase 6 has not
  started" claim.
- `tmp/private-physics-download-site/README.md` - current handoff and local
  validation context.
- `docs/tasks/DOC-REVIEW-01.md` - this report.

Pre-existing files inspected but not changed include `AGENTS.md`, the complete
`docs/Implementation_plan.md`, `docs/PROJECT_SCOPE.md`, the retired
`LLM_REPOSITORY_SCAFFOLDING_BLUEPRINT.md`, `docs/NEXT_GOAL_PROMPT.md`, G5/G6,
P6-T01 through P6-T07 reports, all reviewed Core source and test files,
`ReactorSim.Cli/Program.cs`, and the private site's hosting configuration. No
Core, CLI, Unity, test behavior, physics data, or specification source was
changed.

## Assumptions and design choices

- This review treats `docs/PROJECT_SCOPE.md`, task reports, and gate reports as
  status authorities. The guide, PDF, roadmap, and this proposal are derived
  displays and do not authorize implementation.
- Direct test counts are presented with their artifact-layout limitation rather
  than relabeling the wrapper failure as a physics or numerical regression.
- The refactoring document is intentionally a proposal, not a rewrite of the
  frozen implementation plan. Each recommended item must receive its own task
  request and source scope.
- The P1-T08 literature digest is `NotApplicable` to this documentation/source
  review: no physics equation, constant, unit, normalization, comparison,
  tolerance, or golden value was selected or changed.
- The site's source and PDF asset were updated locally but were not saved,
  pushed, or deployed. Publication/hosting requires separate explicit owner
  authorization.

## Validation commands and results

### T3 - direct pinned full headless regression

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; & (Join-Path $sdkPath 'dotnet.exe') test ReactorSim.sln --configuration Release --nologo --no-restore --logger 'console;verbosity=minimal'
```

Result: exit code `0`; Core `154/154` passed and Golden `19/19` passed, with
zero failures and zero skips.

### T1 - direct pinned focused Phase 6 regression

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; & (Join-Path $sdkPath 'dotnet.exe') test tests\ReactorSim.Core.Tests\ReactorSim.Core.Tests.csproj --configuration Release --nologo --no-restore --filter 'FullyQualifiedName~P6T' --logger 'console;verbosity=minimal'
```

Result: exit code `0`; focused Phase 6 Core `44/44` passed, with zero failures
and zero skips.

### T3 - artifact-output full-suite wrapper characterization

```text
$sdkPath='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'; $env:DOTNET_ROOT=$sdkPath; $env:PATH="$sdkPath;$env:PATH"; & .\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite
```

Result: nonzero exit after the wrapper's `--artifacts-path` relocation. Core
reported `142/154` passed with twelve fixture/root-location failures; Golden
reported `19/19` passed. The failures trace to Core tests that calculate a
repository root from `AppContext.BaseDirectory` or search upward for
`ReactorSim.sln` after binaries are placed beneath a temporary artifacts
directory. This result is retained as a defect characterization and deferred
to `TEST-INFRA-01`; no retry or source workaround was hidden in this task.

### T1 - guide consistency and PDF synchronization

```text
$guidePythonDirectory='C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python'; $previousPath=$env:PATH; try { $env:PATH="$guidePythonDirectory;$previousPath"; & .\tools\Check-PhysicsGuideConsistency.ps1 } finally { $env:PATH=$previousPath }
```

Result: exit code `0`; `PHYSICS_GUIDE_CONSISTENCY_PASS pages=22`, current
Phase 3/4/5/6 and G4/G5/G6 markers present, and private PDF byte synchronization
passed. An initial consistency check correctly caught P6-T02's historical
first-attempt `BLOCKED` header; the check now verifies the report's controlling
reopened `COMPLETE / SYNTHETIC TEST-ONLY` addendum without rewriting history.

### T1 - PDF text, render, and visual inspection

```text
& 'C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -c "from pathlib import Path; from pypdf import PdfReader; p=Path(r'output/pdf/candu-nuclear-diffusion-student-guide.pdf'); reader=PdfReader(str(p)); text='\n'.join(page.extract_text() or '' for page in reader.pages); required=['G6 CONDITIONAL PASS evidence','Core 154/154 PASS; Golden 19/19 PASS','Current test results and test-runner limitation','Test-FullHeadlessSuite.ps1']; missing=[item for item in required if item not in text]; assert not missing, missing; print(f'PDF_TEXT_PASS pages={len(reader.pages)} bytes={p.stat().st_size}')"
& 'C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe' -png -r 144 output\pdf\candu-nuclear-diffusion-student-guide.pdf tmp\pdfs\physics-guide-render-20260821-v27\page
```

Result: exit code `0`; PDF text check passed with 22 pages and 69,657 bytes.
All 22 PNG pages rendered. The PDF renderer emitted missing-display-font
warnings for `Symbol` and `ArialUnicode`, but visual inspection of the cover,
current-test page, updated limitations page, and change-record page found no
clipping, overlap, blank pages, or unreadable text.

### T0 - synchronized PDF identity

```text
Get-FileHash -Algorithm SHA256 -LiteralPath output/pdf/candu-nuclear-diffusion-student-guide.pdf,tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf
```

Result: both files matched SHA-256
`87919058A224DDF2561B436EF6B4DE4E0200DE81726E3F58A32A226F28C00140`.

### T1 - private-site lint, build, and authenticated route tests

```text
npm.cmd run lint
npm.cmd test
```

Result: lint exited `0`. The site build completed and all `4/4` authenticated
route/asset tests passed. The first candidate test run caught one stale
assertion for the old G5 handoff phrase; the assertion was updated to the
current phase-row wording and the final run passed.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No request-level telemetry exposed. |
| Cached input tokens | Unavailable | No request-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No request-level telemetry exposed. |
| Output tokens | Unavailable | No request-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No request-level telemetry exposed. |
| Total tokens | Unavailable | No request-level telemetry exposed. |
| Estimated cost | Unavailable | No price source or allocable request receipt exposed. |
| Goal-service total | Unavailable | No separate goal-service total exposed; not added to request totals. |

Cost formula/basis: unavailable; no provider billing receipt, actual model
receipt, or allocable request-level telemetry was exposed.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, sign, convergence rule,
tolerance, golden value, physics data, or runtime contract changed. The report
only records existing direct and wrapper test results.

## Deferred validation

- Portable artifact-output full-suite PASS - deferred to proposed
  `TEST-INFRA-01`; the current wrapper must not be called passing evidence.
- Controller-to-queue atomicity, queue representation consolidation, and
  influence-map digest deduplication - deferred to separately approved Phase 6
  refactoring tasks with T3 and independent code review (high).
- Applicable external/production RRS comparison and unconditional G6 claim -
  deferred to an owner-authorized comparison package and G6 re-entry.
- Browser UI QA and private Sites publication - not run. Local source build and
  authenticated route tests passed, but hosting changes require explicit
  authorization.

## Blockers, risks, and follow-up

- Blockers: none for the review/documentation deliverables. The portable
  full-suite wrapper remains a blocker for treating that wrapper as CI/release
  evidence.
- Risk triggers: source review touched determinism-adjacent queue/replay and
  test-evidence boundaries, so one independent code review (high) was
  performed. No implementation candidate was changed in this task.
- Risks accepted or deferred: direct tests remain green but no external,
  production, full-core, or unconditional RRS authority is claimed; reviewer
  actual-model evidence remains `UNVERIFIED`.
- Follow-up work: proposed `TEST-INFRA-01`, `P6-INTEGRATION-01`,
  `P6-INTERNALS-01`, `CORE-NAMING-01`, and `STATUS-VISIBILITY-01` are defined
  in `docs/Refactoring_implementation_plan_2026-08-21.md` and require separate
  authorization.

## Next eligible task

None under `DOC-REVIEW-01`. The recommended next separately authorized task is
`TEST-INFRA-01` - make the full suite artifact-portable. The frozen plan's
Phase 7 work remains subject to selection from the current project scope and
must not be combined with that test-infrastructure task.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`docs/gates/G6.md`](../gates/G6.md), and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
