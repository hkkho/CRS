# DOC-REFRESH-01 - Current publication and clean-session handoff refresh

## Outcome

Status: COMPLETE

The repository publication surfaces now agree with the current authorities as
of 2026-08-16. The private website no longer presents the obsolete P4-T04
handoff; it surfaces the blocked G4-R3 disposition, the next eligible
P4-T06-G4H decision, and the P5-T10/G5 dependency. The living physics guide
was updated to version 2.3, its PDF was regenerated and visually checked, and
the authenticated site asset was kept byte-identical to the generated PDF. A
copy-ready clean-session prompt for P4-T06-G4H was added.

Effectiveness: SUCCESS

This task was a documentation/publication refresh only. It did not change
runtime physics, equations, units, signs, convergence policy, tolerances,
golden data, public schemas, or Unity behavior. The G4-R3 blocker remains
explicit and was not bypassed.

## Execution and model evidence

- Role: root implementer; bounded documentation and site refresh
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: `UNVERIFIED`; no execution receipt or actual-model telemetry was exposed
- Execution receipt or telemetry source: root task context; unavailable
- Attempts: 1; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; no runtime physics, numerical baseline, or public Core contract changed
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/physics/candu-nuclear-diffusion-student-guide.md` - records the G4-R3 blocked disposition, P4-T06-G4H handoff, and P5-T10/G5 dependency; removes literal PDF break markers; advances the guide to version 2.3.
- `docs/PROJECT_SCOPE.md` - reconciles the current handoff with the completed G4-R3 report and names P4-T06-G4H as the next eligible decision while preserving historical records.
- `tools/Check-PhysicsGuideConsistency.ps1` - verifies the current guide version, G4-R3 blocked evidence, P4-T06-G4H routing, and the blocked-report status.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - regenerated 17-page PDF.
- `tmp/private-physics-download-site/README.md` - records the current publication handoff and source authorities.
- `tmp/private-physics-download-site/app/page.tsx` - adds the current-handoff panel to the authenticated download page.
- `tmp/private-physics-download-site/app/roadmap/page.tsx` - updates Phase 4/5, G4-R3, P4-T06-G4H, P5-T10, and G5 status rows.
- `tmp/private-physics-download-site/app/layout.tsx` - updates site metadata.
- `tmp/private-physics-download-site/app/globals.css` - styles the handoff panel.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf` - synchronized byte-identical PDF asset.
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - adds current-handoff roadmap assertions.
- `docs/NEXT_GOAL_PROMPT.md` - copy-ready clean-session prompt for P4-T06-G4H.
- `docs/tasks/DOC-REFRESH-01.md` - this task report.

The pre-existing `tmp/private-physics-download-site/package.json` edit was
inspected and left unstaged; it was not part of this refresh commit. Existing
runtime, reference, Unity, task, gate, and generated-worktree files were
preserved.

The site publication source commit is `5867b424ee55e2eb6a71f406a12eddaa987ae82f`.
The existing owner-only Sites project saved version 4 and deployed
successfully at `https://candu-physics-guide-download.pyrx87.chatgpt.site`.

## Assumptions and design choices

- `DOC-REFRESH-01` is used because the user did not provide a task ID; this
  remains a documentation/publication task and does not authorize P4-T06-G4H
  implementation.
- The current scope register is the status authority. The website and PDF are
  derived publication surfaces and explicitly say so.
- `G4-R3` is recorded as `BLOCKED`; no raw candidate difference was promoted
  to a tolerance or golden value.
- The next clean session is routed to `P4-T06-G4H`, which must either supply an
  admissible numeric authority or explicitly decline production G4 approval.
- The PDF copy under the private site is a distribution copy of the generated
  output, not a second editable source.
- Literature applicability is `NotApplicable` to the publication mechanics;
  the guide and clean-session prompt preserve the applicable P1-T08 row IDs
  already named by G4G/G4-R3 rather than selecting new physics evidence.

## Validation commands and results

### PDF generation and distribution sync (T0/T1)

```text
& 'C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' '.\tools\build_physics_guide_pdf.py'
```

Result: exit 0; `PHYSICS_GUIDE_PDF_PASS`; the generated source digest was
`8c48ab8ff4fcb8894627e3fe9d3eed59c9270c0c5bfbcd3bd8810e52b2aeaefb`.

The final generated/output and private-site PDF SHA-256 is
`C74235CD23F7A5E875CE37D727EBAF6F52527631C2EFE3255347CC49715CEF15` and the
two files are byte-identical.

### Guide consistency check (T1)

```text
& .\tools\Check-PhysicsGuideConsistency.ps1
```

Result: exit 0; `PHYSICS_GUIDE_CONSISTENCY_PASS pages=17`, with the current
guide, required task/gate markers, PDF freshness, and private PDF sync all
passing.

### PDF extraction and render inspection (T1)

```text
pdftoppm -png -r 120 output\pdf\candu-nuclear-diffusion-student-guide.pdf tmp\pdfs\physics-guide-render-20260816-v8\page
```

Result: exit 0; 17 PNG pages rendered. The complete contact sheet and
representative full-size pages were inspected: no clipped text, overlap,
broken tables, literal `<br>` metadata, or unreadable section transitions were
observed. Poppler emitted non-fatal missing-display-font warnings for
`Symbol` and `ArialUnicode`.

The PDF text check passed with `pages=17`, `missing=0`, and
`hashes_equal=True` for the required current-status markers and the two PDF
copies.

### Site lint, build, and focused rendered-page tests (T0/T1)

```text
npm run lint
npm run build
npm test
```

Result: all commands exited 0. The authenticated route tests passed 4/4:
anonymous redirect, authenticated PDF page, authenticated roadmap, and PDF
asset packaging. The build emitted `/` and `/roadmap` successfully.

### Private publication

Result: the existing owner-only Sites project was saved as version 4 from the
validated source commit and deployed successfully. Access configuration was
unchanged; it remains custom/owner-only with no external visitors.

## Token and cost accounting

Request-level token and billing telemetry was not exposed to this task, so no
per-task usage or cost is estimated.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed |
| Cached input tokens | Unavailable | No task-level telemetry exposed |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed |
| Output tokens | Unavailable | No task-level telemetry exposed |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed |
| Total tokens | Unavailable | No task-level telemetry exposed |
| Estimated cost | Unavailable | No verified price source or task allocation exposed |
| Goal-service total | Unavailable | Not exposed; not allocable to this task |

Cost formula/basis: unavailable; no request-level billing receipt or verified
price source was exposed.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, normalization,
convergence rule, tolerance, reference baseline, or golden data changed.

## Deferred validation

- T3-T6 runtime/reference suites - deferred to the named physics tasks and
  gates because this refresh changed only derived documentation/publication
  surfaces and a consistency guard.
- P4-T06-G4H evidence and code review (high) - deferred to the next clean
  session; it owns the numeric-authority decision and must verify actual review
  telemetry.
- Browser-authenticated visual interaction QA - not run as a separate test;
  focused rendered HTML tests passed and the site was published under the
  existing owner-only access policy.

## Blockers, risks, and follow-up

- Blockers: none for this refresh. The project-level G4-R3 technical blocker
  remains intentionally visible.
- Risk triggers: none; no runtime, equation, unit, tolerance, golden-data,
  serialization, clock, or public Core/Unity interface changed.
- Risks accepted or deferred: the website and PDF are derived surfaces and
  must be refreshed whenever the current scope or approved evidence changes.
- Follow-up work: execute only `P4-T06-G4H` in the next clean session; do not
  start P5-T10 until the G4 production disposition permits it.

## Next eligible task

`P4-T06-G4H` - owner-authorized numeric baseline and tolerance decision.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), and
[`docs/gates/G4-R3.md`](../gates/G4-R3.md).
