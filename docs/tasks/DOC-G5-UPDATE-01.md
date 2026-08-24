# DOC-G5-UPDATE-01 - publish the final G5 status to the guide and site sources

## Outcome

Status: COMPLETE

The living physics guide, regenerated PDF, private authenticated download page,
and derived roadmap now record the final G5 `PASS` for the approved
`ReducedModel` / engine-neutral Core scope. The site also records Phase 6/G6 as
activated for task-definition routing only. Direct production/full-core CANDU
authority remains explicitly deferred.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer; documentation and site-artifact maintenance
- Requested model / reasoning: GPT-5.6 Luna / high
- Actual model / reasoning: UNVERIFIED; no per-task execution receipt exposed
- Execution receipt or telemetry source: Unavailable
- Attempts: one bounded documentation/site update; elapsed time: Unavailable
- Artifact/checkpoint status: produced guide source, PDF, synchronized site PDF, and site source updates
- Review disposition: not applicable; no physics, runtime, tolerance, golden-data, or public-contract behavior changed
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `docs/physics/candu-nuclear-diffusion-student-guide.md` - updated to guide version 2.4 with G4-R6/G5 status, validation snapshot, traceability, and Phase 6 handoff.
- `tools/build_physics_guide_pdf.py` - updated the PDF cover status to match the bounded G4-R6/G5 disposition.
- `tools/Check-PhysicsGuideConsistency.ps1` - updated current guide markers and task/gate checks for G5 and the preserved correction chain.
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf` - regenerated PDF artifact.
- `tmp/private-physics-download-site/README.md` - updated site handoff documentation.
- `tmp/private-physics-download-site/app/page.tsx` - updated authenticated download-page handoff.
- `tmp/private-physics-download-site/app/roadmap/page.tsx` - updated derived Phase 4/5/6, G4/G5, and correction-chain status.
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - updated roadmap assertions for G5 and Phase 6.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf` - synchronized byte-for-byte with the regenerated PDF.

Pre-existing files were inspected but not changed: `docs/PROJECT_SCOPE.md`,
`docs/gates/G5.md`, `docs/tasks/P5-T16.md`, `.openai/hosting.json`, and the
pre-existing site `package.json` change. No simulation source or test behavior
was changed.

## Assumptions and design choices

- The G5 wording is bounded to the authority already recorded by G4-R6 and
  G5; it does not imply direct CANDU, DONJON5, DRAGON5, production, or
  full-core authority.
- Historical G4-R3/G4-R5 no-admission records remain visible in the roadmap;
  they were not rewritten as current PASS evidence.
- The PDF remains generated from the Markdown guide, and the site distributes
  the exact regenerated PDF bytes.
- The site source was updated and locally validated, but no version was saved,
  pushed, or deployed because the repository requires separate authorization
  for publication/hosting changes.

## Validation commands and results

### Guide consistency and PDF synchronization (T1)

```text
& '.\tools\Check-PhysicsGuideConsistency.ps1'
```

Result: exit code 0; `PHYSICS_GUIDE_CONSISTENCY_PASS pages=18`, core static
solver present, and private PDF sync PASS.

### PDF text and page-count check (T1)

```text
python -c "from pathlib import Path; from pypdf import PdfReader; p=Path(r'output/pdf/candu-nuclear-diffusion-student-guide.pdf'); r=PdfReader(str(p)); t='\n'.join(page.extract_text() or '' for page in r.pages); required=['G4-R6','Phase 5/G5 status and validation snapshot','44/44 PASS','Core 110/110; Golden 19/19 PASS','129/129 PASS','Phase 6/G6']; missing=[x for x in required if x not in t]; assert not missing, missing; print(f'PDF_TEXT_PASS pages={len(r.pages)} bytes={p.stat().st_size}')"
```

Result: exit code 0; 18 pages, 56,745 bytes, all six G5 markers present.

### PDF visual render and inspection (T1)

```text
& 'C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe' -png -r 144 'output\pdf\candu-nuclear-diffusion-student-guide.pdf' 'tmp\pdfs\physics-guide-render-20260818-g5-v2\page'
```

Result: 18 PNG pages rendered and inspected through the latest contact sheet
and representative full-page renders; no clipping, overlap, blank-page, or
unreadable-layout defect observed.

### Website build and authenticated route tests (T1)

```text
npm test
npm run lint
```

Result: build completed; 4/4 route and asset tests passed; lint exited 0.

### PDF byte identity (T0)

```text
Get-FileHash -Algorithm SHA256 -LiteralPath output/pdf/candu-nuclear-diffusion-student-guide.pdf,tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf
```

Result: both copies match SHA-256
`4FEA0CD819218BF4B5A014D19BFF14EC6B054E549AE6751AD7ED9A0CFC846022`.

## Token and cost accounting

Per-task request telemetry is unavailable. No reviewer or worker allocation is
invented for this documentation-only task.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | no per-task receipt exposed |
| Cached input tokens | `Unavailable` | no per-task receipt exposed |
| Cache-write input tokens | `Unavailable` | no per-task receipt exposed |
| Output tokens | `Unavailable` | no per-task receipt exposed |
| Reasoning output tokens | `Unavailable` | no per-task receipt exposed |
| Total tokens | `Unavailable` | no per-task receipt exposed |
| Estimated cost | `Unavailable` | no price source or allocable billing formula exposed |
| Goal-service total | `Unavailable` | no separate goal service used for this task |

Cost formula/basis: unavailable; no per-task billing receipt or price source was
exposed.

## Numerical differences

Not applicable; no numerical behavior, equation, unit, tolerance, golden value,
or runtime contract changed. The validation counts and status text were copied
from the current G4-R6/G5 authority records.

## Deferred validation

- Browser visual QA and Sites publication were not run; the user requested the
  source update, and publication requires separate explicit authorization.
- Production/full-core external CANDU authority remains deferred to its own
  authorized task/gate.

## Blockers, risks, and follow-up

- Blockers: none for the local guide, PDF, and site-source update.
- Risk triggers: none; no runtime or numerical behavior changed.
- Risks accepted or deferred: the private live site remains on its previously
  saved version until a separately authorized Sites publish.
- Follow-up work: Phase 6/G6 task-definition chain is the next eligible project
  work; direct production/full-core authority remains a separate deferred path.

## Next eligible task

Phase 6/G6 task-definition chain, or a separately authorized Sites version/save
and private deployment if the owner wants these source updates published.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`docs/gates/G5.md`](../gates/G5.md), and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
