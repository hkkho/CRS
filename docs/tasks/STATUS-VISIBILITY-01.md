# STATUS-VISIBILITY-01 - Refresh derived status surfaces and handoff

## Outcome

Status: COMPLETE

The derived student guide, generated PDF, private site, and clean-session LLM
handoff were aligned with the current authoritative scope and task evidence.
The handoff now instructs a clean session to query the exact current scope row
instead of selecting a stale hard-coded task. The task changed no physics,
public schema, tolerance, golden authority, runtime behavior, or status
database.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: UNVERIFIED
- Execution receipt or telemetry source: current Codex task; no separate model telemetry exposed
- Attempts: 1; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; this was a derived-document, consistency-check, and private-site route-test task with no code or public-contract change
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/NEXT_GOAL_PROMPT.md` - replaced the stale hard-coded task handoff with an exact-scope-row query and one-task execution prompt.
- `docs/physics/candu-nuclear-diffusion-student-guide.md` - refreshed dated status, current verification counts, limitation text, and change history to version 2.8.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - regenerated the derived 22-page PDF from the guide source.
- `tools/Check-PhysicsGuideConsistency.ps1` - added current-snapshot, stale-wording, and handoff checks.
- `tools/build_physics_guide_pdf.py` - refreshed the PDF cover status summary.
- `tmp/private-physics-download-site/README.md` - refreshed the dated private handoff summary.
- `tmp/private-physics-download-site/app/page.tsx` - refreshed the authenticated home-page status surface.
- `tmp/private-physics-download-site/app/roadmap/page.tsx` - refreshed the authenticated roadmap evidence and current-position handoff.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf` - synchronized byte-for-byte with the root PDF.
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - updated route assertions to reject stale status snapshots.
- `docs/PROJECT_SCOPE.md` - recorded completion and preserved the original G6 snapshot while adding current recovery verification.
- `docs/tasks/STATUS-VISIBILITY-01.md` - this task report.

Pre-existing authorities and prerequisite reports were inspected but not
changed: `AGENTS.md`, `docs/Implementation_plan.md`, the exact
`STATUS-VISIBILITY-01` scope row and refactoring proposal, `P1-T08` evidence
records, the completed recovery reports, and the site/package configuration.

## Assumptions and design choices

- `docs/PROJECT_SCOPE.md`, task reports, specifications, and gates remain the
  technical authority. Guide, PDF, site, and handoff text remain dated derived
  views.
- The original G6 review snapshot (`44/44`, `154/154`) is retained as
  historical evidence in the phase table; newer recovery verification is shown
  separately as focused P6 `46/46`, direct Core `158/158`, and Golden `19/19`.
- The artifact-output wrapper result (`156/156` Core and `19/19` Golden) is
  retained as the TEST-INFRA-01 recovery baseline rather than being presented
  as the latest full direct count.
- `P1-T08` is NotApplicable to technical selection: this task only changes
  derived status text and consistency assertions and selects no physics,
  constants, units, normalization, tolerances, or golden data.
- Private-site publication/deployment was not performed because publication
  is separately authorized and optional in the task scope.

## Validation commands and results

### Direct Phase 6 snapshot (T1 evidence refresh)

```text
$sdkPath = 'C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'
$env:DOTNET_ROOT = $sdkPath
$env:Path = "$sdkPath;$env:Path"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = 'C:\Users\infin\AppData\Local\Temp\candu-cli-home-status-visibility'
& (Join-Path $sdkPath 'dotnet.exe') test 'tests/ReactorSim.Core.Tests/ReactorSim.Core.Tests.csproj' --no-restore --filter 'FullyQualifiedName~P6T' --logger 'console;verbosity=minimal'
```

Result: exit 0; focused Core Phase 6 tests `46/46` passed, zero failures and
zero skips.

### PDF generation (T0/T1)

```text
python .\tools\build_physics_guide_pdf.py
```

Result: exit 0; generated 22-page PDF from the Markdown source with source
SHA-256 `2CF5C5FDD2A9270C2975439C24C28452212E7AD459FB01327003DDAD39A8E158`.

### Guide/site consistency (T0)

```text
& '.\tools\Check-PhysicsGuideConsistency.ps1'
```

Result: exit 0;
`PHYSICS_GUIDE_CONSISTENCY_PASS pages=22 core_static_solver=present private_pdf_sync=PASS guide_sha256=2CF5C5FDD2A9270C2975439C24C28452212E7AD459FB01327003DDAD39A8E158`.

### PDF artifact synchronization (T0)

```text
Get-FileHash 'output/pdf/candu-nuclear-diffusion-student-guide.pdf' -Algorithm SHA256
Get-FileHash 'tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf' -Algorithm SHA256
```

Result: exit 0; both hashes are
`5D8C67191D6FC883314F591F8CAE8D22D14FDB75A09DF01552E95AAD81C36397`.

### PDF visual inspection (T1)

```text
PyMuPDF fallback renderer: fitz.Matrix(1.5, 1.5), rendered every page of output/pdf/candu-nuclear-diffusion-student-guide.pdf to a temporary QA directory.
```

Result: Poppler `pdfinfo`/`pdftoppm` was unavailable; the temporary PyMuPDF
fallback rendered all `22` pages successfully. Representative cover, body,
and closing pages were visually inspected with no clipping, overflow, or
broken layout observed.

### Private site build and rendered routes (T1)

```text
npm test
```

Result: exit 0; Vinext build completed with routes `/` and `/roadmap`; all 4
Node route/asset tests passed, zero failures/skips:
visitor redirect, authenticated home page, authenticated roadmap, and PDF
asset packaging.

### Source diff hygiene (T0)

```text
git diff --check -- . ':(exclude)output/pdf/candu-nuclear-diffusion-student-guide.pdf'
```

Result: exit 0 for source/text changes. The generated ReportLab PDF is tracked
with the repository's `astextplain` diff driver and contains expected PDF
trailing spaces in generated cross-reference/object lines; it was validated by
generation, byte-hash synchronization, page rendering, and the dedicated
consistency check.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No per-task telemetry exposed |
| Cached input tokens | Unavailable | No per-task telemetry exposed |
| Cache-write input tokens | Unavailable | No per-task telemetry exposed |
| Output tokens | Unavailable | No per-task telemetry exposed |
| Reasoning output tokens | Unavailable | No per-task telemetry exposed |
| Total tokens | Unavailable | No per-task telemetry exposed |
| Estimated cost | Unavailable | No allocable request-level pricing telemetry exposed |
| Goal-service total | Unavailable | Goal aggregate not exposed |

Cost formula/basis: unavailable; no request-level or worker-level billing
telemetry was exposed, so no allocation is invented.

## Numerical differences

Not applicable; no numerical behavior changed. Displayed counts were corrected
to already verified evidence: direct Core `158/158` plus Golden `19/19`,
focused P6 `46/46`, and the TEST-INFRA-01 wrapper baseline Core `156/156` plus
Golden `19/19`.

## Deferred validation

- Private-site publication/deployment - deferred because publication is a
  separately authorized optional boundary.
- Unity EditMode/PlayMode and device validation - deferred because this task
  changed no Unity adapter, serialization contract, or runtime behavior.
- Full T3/T4/T5/T6 escalation - not triggered by derived-document and route
  assertion changes; the underlying test evidence was refreshed only to prevent
  stale status display.

## Blockers, risks, and follow-up

- Blockers: none.
- Risk triggers: none; no equations, units, signs, convergence, indexing,
  determinism, public interface, golden data, or runtime code changed.
- Risks accepted or deferred: current G6 remains conditional and synthetic /
  test-only; no RRS comparison, production, external-reference, or full-core
  authority is implied.
- Follow-up work: an owner decision is required before selecting planned Phase
  7 or re-sequencing the explicitly non-physics Phase 8 CLI slice.

## Next eligible task

None until the owner decides whether to start planned Phase 7 or re-sequence
the non-physics Phase 8 CLI slice.

Source: [`AGENTS.md`](../../AGENTS.md) and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
