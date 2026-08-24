# DOC-PHYSICS-01 - Physics status and student guide

## Outcome

Status: COMPLETE

Created a first-year-university-level Markdown guide and a rendered PDF that
separate executable physics from approved design contracts and future
investigation. The guide explains the background needed to understand a
two-group diffusion model for a CANDU-style online-refuelling game, records
the planned numerical methods, connects each physics layer to player decisions,
and defines a repeatable update/consistency workflow.

Effectiveness: SUCCESS

This was documentation and validation tooling only. No runtime physics,
equations, constants, tolerances, schemas, or golden data were implemented or
changed.

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: Unspecified
- Actual model / reasoning: UNVERIFIED
- Execution receipt or telemetry source: Unavailable
- Attempts: 1; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; this task did not perform a Sol gate review
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/physics/candu-nuclear-diffusion-student-guide.md` - living Markdown
  source, student explanation, physics status, numerical-method summary, game
  coupling, traceability, and maintenance instructions.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - rendered 13-page PDF
  version of the guide.
- `tools/build_physics_guide_pdf.py` - deterministic ReportLab Markdown-to-PDF
  generator.
- `tools/Check-PhysicsGuideConsistency.ps1` - focused consistency check for
  the guide, source/spec status, runtime implementation markers, PDF freshness,
  page count, and game-coupling/maintenance sections.
- `docs/Implementation_plan.md` - documentation capability now names the guide
  and requires PDF regeneration plus the consistency check after relevant
  specification, implementation, gate, or game-loop changes.
- `docs/tasks/DOC-PHYSICS-01.md` - this task report.

Pre-existing specifications, reports, source scaffolding, and Unity files were
inspected and not changed, except for the implementation-plan documentation
capability noted above.

## Assumptions and design choices

- `DOC-PHYSICS-01` is used as the task ID because the request did not provide
  one; the work remains documentation-only and does not start Phase 3.
- The current executable-status statement is intentionally explicit: the
  runtime contains no reactor neutronics, diffusion, burnup, kinetics, xenon,
  or regulating-system implementation. Phase 1 reference tooling is described
  as offline provenance/parsing infrastructure, not runtime physics.
- P2-T01 through P2-T05 are presented as candidate design contracts. P2-T01
  and P2-T02 remain pending G2 approval, and no candidate numerical value is
  promoted to a runtime constant or golden result.
- P1-T08 is COMPLETE. Applicable digest rows are `S1-R02`, `S1-R03`,
  `S1-R05`, `S1-R06`, `S1-R07`, `S1-R08`, `S1-R09`, `S1-R10`, `S5-R03`,
  `S5-R04`, `S5-R08`, `S6-R02`, `S6-R03`, and `S6-R05`. They are identified as
  methodology/context or candidate-case-design evidence only; none is an
  approved runtime or golden-data authority.
- The guide states the current policy requirement for a verified actual Sol
  review of prior G2 evidence before Phase 3 or later physics implementation.
- ASCII hyphens are used in the Markdown source for PDF portability. The PDF
  is generated from the Markdown rather than maintained as a second source.

## Validation commands and results

### Source and PDF generation

```text
python .\tools\build_physics_guide_pdf.py
```

Result: exit code 0. Reported
`PHYSICS_GUIDE_PDF_PASS`; output is
`output/pdf/candu-nuclear-diffusion-student-guide.pdf` and the source SHA-256
is `465CD32F28DBA95DE52CF2904E0E08B19EEF298FB3C756AA328D856A08A691DD`.

### Focused consistency check (T1 documentation check)

```text
& .\tools\Check-PhysicsGuideConsistency.ps1
```

Result: exit code 0.
`PHYSICS_GUIDE_CONSISTENCY_PASS pages=13 runtime_physics_markers=0`.

### Markdown/PDF text presence check

```text
python -c "from pypdf import PdfReader; p='output/pdf/candu-nuclear-diffusion-student-guide.pdf'; r=PdfReader(p); t='\n'.join((x.extract_text() or '') for x in r.pages); req=['No reactor physics is implemented in the runtime yet.','static two-group diffusion eigenproblem','left-endpoint Euler','Living documentation']; missing=[x for x in req if x not in t]; print(f'PDF_TEXT_PASS pages={len(r.pages)} chars={len(t)} missing={len(missing)}'); raise SystemExit(1 if missing else 0)"
```

Result: exit code 0. `PDF_TEXT_PASS pages=13 chars=30265 missing=0`.

### PDF render and visual inspection

```text
$renderDir='tmp/pdfs/physics-guide-render-20260810-v6'; New-Item -ItemType Directory -Force -Path $renderDir | Out-Null; & 'C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe' -png -r 120 'output\pdf\candu-nuclear-diffusion-student-guide.pdf' (Join-Path $renderDir 'page'); if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; Get-ChildItem -LiteralPath $renderDir -Filter '*.png' | Measure-Object | Select-Object -ExpandProperty Count
```

Result: exit code 0; 13 page images rendered and inspected. Poppler emitted
non-fatal environment warnings about missing `Symbol` and `ArialUnicode`
display fonts; the pages rendered correctly with no observed clipping,
overlap, or broken list/table layout.

### Repository format check

```text
& .\tools\Check-Format.ps1
```

Result: exit code 0.

## Token and cost accounting

Request-level token and billing telemetry was not exposed to this task, so no
per-task usage or cost is estimated.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No request telemetry exposed |
| Cached input tokens | Unavailable | No request telemetry exposed |
| Cache-write input tokens | Unavailable | No request telemetry exposed |
| Output tokens | Unavailable | No request telemetry exposed |
| Reasoning output tokens | Unavailable | No request telemetry exposed |
| Total tokens | Unavailable | No request telemetry exposed |
| Estimated cost | Unavailable | No model/price/usage receipt exposed |
| Goal-service total | Unavailable | No goal-service telemetry exposed |

Cost formula/basis: not computed; any estimate would invent a token allocation
or pricing basis not present in the execution receipt.

## Numerical differences

Not applicable; no numerical behavior changed.

## Deferred validation

- T3 solver/determinism checks - deferred to the Phase 4 implementation task
  and its required gate because this task deliberately did not implement a
  solver.
- Independent Sol review and G2 evidence re-verification - deferred until the
  project explicitly runs that review; the guide records it as a prerequisite
  for Phase 3.
- T4/T5/T6 - not applicable to this documentation-only change; T6 remains
  reserved for an approved reference-baseline change.

## Blockers, risks, and follow-up

- Blockers: none for this documentation task.
- Risk triggers: none fired; no equations, runtime clocks, schemas, numerical
  backends, tolerances, or golden data were changed.
- Risks accepted or deferred: the guide is derived documentation and can become
  stale if a future physics or game-loop change omits regeneration. The
  implementation plan and focused consistency script now make that maintenance
  step explicit.
- Follow-up work: run the verified actual Sol review required by the current
  G2 policy, then proceed only to the next eligible Phase 3 task. Update this
  guide and regenerate the PDF whenever approved source status changes.

## Next eligible task

None until the required verified actual Sol review of prior G2 evidence is
complete. After that decision, Phase 3 is the next eligible implementation
phase.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md), sections
4, 5, 6, 8, and 9.
