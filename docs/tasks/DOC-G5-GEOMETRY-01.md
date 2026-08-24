# DOC-G5-GEOMETRY-01 - Add approved ReducedModel geometry visualization and redeploy guide

## Outcome

Status: COMPLETE

Added a separate visualization of the approved ReducedModel geometry in the
student guide, in addition to the existing model cards, evidence map, solver
views, and state/history visualizations. Regenerated the PDF, synchronized the
private download asset, and redeployed the owner-only website.

Effectiveness: SUCCESS

The new figure shows the logical 2 x 2 channel plane, channel coordinates,
axial flow directions, transverse links, 12 axial bundle positions per
channel, and the channel-end boundary faces. The accompanying text records the
approved topology totals: 48 spatial nodes, 92 reciprocal edges, and 104
boundary records.

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: Unavailable
- Attempts: 1; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; documentation/PDF/site-only change
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/physics/candu-nuclear-diffusion-student-guide.md` - added the
  ReducedModel channel-plane geometry figure and version 2.6 change record.
- `tools/Check-PhysicsGuideConsistency.ps1` - required the new guide version
  and geometry markers.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - regenerated 21-page
  PDF.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf`
  - synchronized byte-for-byte with the regenerated PDF and published.
- `docs/tasks/DOC-G5-GEOMETRY-01.md` - this task report.

Pre-existing files inspected but not changed: `docs/Implementation_plan.md`,
`docs/PROJECT_SCOPE.md`, the approved G4K ReducedModel definition and
comparison evidence, prior G5 documentation reports, and
`tools/build_physics_guide_pdf.py`.

## Assumptions and design choices

- The geometry view uses the approved G4K ReducedModel topology: four channels
  at logical coordinates `(0,0)`, `(1,0)`, `(0,1)`, and `(1,1)`, with 12 axial
  positions per channel.
- The figure labels the approved transverse links `0-1`, `0-2`, `1-3`, and
  `2-3`, plus the EndA/EndB directions and boundary faces.
- Integer channel coordinates are explicitly presented as topology
  coordinates, not inferred metre distances; physical lengths, volumes, and
  boundary data remain explicit SI inputs in the approved model.
- This is a documentation visualization only. It does not select or change an
  equation, unit, sign convention, boundary condition, convergence rule,
  tolerance, golden value, runtime schema, or authority disposition.
- The private site remains custom/owner-only with one allowed user, no groups,
  and no external visitors.

## Validation commands and results

### Guide build and PDF artifact validation (T0/T1)

```text
python tools/build_physics_guide_pdf.py
```

Result: PASS. Generated 21 pages with source SHA-256
`5ec5389ab4e2eec954b1ed0ef37b08923665039844248aa6fca3d662ddd62cb7`.

```text
python -c "import fitz; from pathlib import Path; pdf=Path(r'output/pdf/candu-nuclear-diffusion-student-guide.pdf'); out=Path(r'tmp/pdfs/physics-guide-render-20260818-geometry-v2'); out.mkdir(parents=True,exist_ok=True); doc=fitz.open(pdf); [page.get_pixmap(matrix=fitz.Matrix(1.5,1.5),alpha=False).save(str(out/f'page-{i+1:02d}.png')) for i,page in enumerate(doc)]; print(f'PYMUPDF_RENDER_PASS pages={len(doc)} output={out}')"
```

Result: PASS. PyMuPDF rendered all 21 pages; Poppler was unavailable, so the
approved fallback renderer was used. Visual inspection of the geometry pages,
continuation pages, glossary/change-record pages, and the 21-page contact sheet
found no clipping, blank page, or layout defect.

```text
python -c "from pypdf import PdfReader; from pathlib import Path; p=Path(r'output/pdf/candu-nuclear-diffusion-student-guide.pdf'); t='\n'.join((x.extract_text() or '') for x in PdfReader(str(p)).pages); terms=['Model geometry and ReducedModel visualizations','Channel-plane cross-section','transverse links: 0-1 East/West','48 spatial nodes, 92 reciprocal edges, and 104 boundary records']; print('PYPDF_MARKER_PASS pages='+str(len(PdfReader(str(p)).pages))+' '+ ' '.join(f'{x}={t.count(x)}' for x in terms))"
```

Result: PASS. All four geometry markers occurred exactly once.

### Repository consistency (T0)

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Check-PhysicsGuideConsistency.ps1
```

Result: PASS. `pages=21`, `core_static_solver=present`, private PDF sync PASS,
and guide SHA-256 `5EC5389AB4E2EEC954B1ED0EF37B08923665039844248AA6FCA3D662DDD62CB7`.

### Website validation (T0/T1)

```text
npm run build
npm test
npm run lint
```

Result: PASS. The site build completed; the rendered HTML tests passed 4/4;
lint exited successfully.

### Publication validation

The regenerated output PDF and the private-site PDF copy matched exactly at
SHA-256 `C9F1AF06F58542F4FDC8B9B152F12FC2A20937E688F813FCE7FEC0A5312EF9F3`.
The source was pushed, packaged from that exact source state, saved as the next
private site version, deployed successfully, and verified active at the
existing private URL. Access remained custom/owner-only with one allowed user,
zero groups, and zero external visitors.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | No task-level telemetry exposed |
| Cached input tokens | `Unavailable` | No task-level telemetry exposed |
| Cache-write input tokens | `Unavailable` | No task-level telemetry exposed |
| Output tokens | `Unavailable` | No task-level telemetry exposed |
| Reasoning output tokens | `Unavailable` | No task-level telemetry exposed |
| Total tokens | `Unavailable` | No task-level telemetry exposed |
| Estimated cost | `Unavailable` | No price/usage receipt exposed |
| Goal-service total | `Unavailable` | Reported separately; not allocable here |

Cost formula/basis: unavailable because no execution receipt or billing
telemetry was exposed for this documentation/publishing task.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, tolerance, or golden
data changed.

## Deferred validation

- Direct production/full-reference CANDU authority reproduction remains deferred
  to the approved G4/G6 evidence chain because this task only documents the
  already-approved ReducedModel boundary.
- Phase 6/G6 RRS implementation and its T3/reference validation remain
  deferred to the activated Phase 6 task-definition chain.

## Blockers, risks, and follow-up

- Blockers: none.
- Risk triggers: none; the change did not affect equations, runtime behavior,
  serialization, tolerances, golden data, or numerical backends.
- Risks accepted or deferred: the figure is a topology visualization and does
  not imply physical distances or direct production/external authority.
- Follow-up work: continue with the activated Phase 6/G6 task-definition chain;
  preserve the historical G4-R5 no-admission report and G4-R6 bounded PASS.

## Next eligible task

Phase 6/G6 task-definition chain (`P6-T01` through `P6-T07`, then `G6`), as
recorded in `docs/tasks/ROUND-2026-08-17-P6-RRS-CHAIN.md`. No Phase 6
implementation task was started by this documentation update.

Source: [`AGENTS.md`](../../AGENTS.md) and
[`CODEX_TASK_TEMPLATE.md`](../../CODEX_TASK_TEMPLATE.md).
