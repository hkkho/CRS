# DOC-G5-MODELS-01 - document the test and gate models in the guide PDF

## Outcome

- Status: `COMPLETE`
- Effectiveness: `SUCCESS`
- Objective: add clear descriptions and visualizations of the models used by
  tests and gates to the student guide PDF, synchronize the private download,
  and publish the updated private site after the user's prior authorization.
- No simulation equations, units, tolerances, golden values, schemas, or
  runtime behavior were changed.

## Content added

The guide now includes a model stack and evidence map that distinguish:

- offline DRAGON5/DONJON5 reference provenance and the no-runtime boundary;
- the frozen P2-T02 two-group finite-volume diffusion contract;
- P4-T05 synthetic algebraic and toy spatial cases plus the P4-T06-G4I
  manufactured four-node case;
- the G4J/G4-R5 project-authored 48-node ReducedModel candidate and its
  historical `Candidate` / `Deferred` / `NoGolden` disposition;
- the G4K independent dense generalized-eigen authority and G4-R6
  `ApprovedGolden` / `ReducedModel`-only PASS for five scenarios and six
  profiles;
- the P5-T10/G5 state/history ledger and its exact structural,
  authentication, persistence, and restart evidence; and
- the future Phase 6/G6 RRS and actuator model boundary.

Three ASCII visualizations were added to the PDF source: the authority/model
stack, the 48-node ReducedModel topology and two-group solve, and the Phase 5
power-to-burnup/history ledger. The guide version is now 2.5.

## Files changed

- `docs/physics/candu-nuclear-diffusion-student-guide.md`
- `tools/build_physics_guide_pdf.py`
- `tools/Check-PhysicsGuideConsistency.ps1`
- `output/pdf/candu-nuclear-diffusion-student-guide.pdf`
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf`

The PDF builder change removes a table-spacer page artifact and slightly
reduces table leading so the complete historical change record remains on the
final page without an extra blank page. The consistency checker now expects the
guide's current version marker `2.5`.

## Review and telemetry

- Requested implementation model/reasoning: GPT-5.6 Luna / high for bounded
  documentation and artifact work, per `AGENTS.md`.
- Actual implementation model/reasoning receipt: unavailable in repository
  telemetry.
- Code review: not required for this documentation/layout-only change; no
  numerical, runtime, public-contract, or golden-data behavior changed.
- Attempts: one bounded documentation/PDF update; one publication attempt.
- Artifact status: produced, rendered, inspected, synchronized, and published
  successfully.
- Token and cost fields: unavailable/not allocable; no per-worker allocation is
  claimed.

## Validation commands and results

Guide and PDF:

- `python tools/build_physics_guide_pdf.py` - exit 0; generated the final PDF
  with guide source SHA-256
  `143ACBDAE7EEDCFBEA47CFC2F97D157B65CCEC72ED1BEC6589C3855B9375D344`.
- Final PDF text inspection - 20 pages, 64,914 bytes, and exactly one hit for
  each new model/evidence/visualization marker.
- PyMuPDF render at 150 DPI - 20 pages rendered to
  `tmp/pdfs/physics-guide-render-20260818-models-v5/`; full contact sheet and
  representative model, evidence-table, visualization, and change-record
  pages were visually inspected with no clipping, overlap, or unreadable
  content. System Poppler was unavailable, so a PyMuPDF Python fallback
  renderer was used and recorded explicitly here.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Check-PhysicsGuideConsistency.ps1`
  - exit 0; `PHYSICS_GUIDE_CONSISTENCY_PASS pages=20`, core static solver
  present, and private PDF sync passed.
- Local final PDF and private-site PDF copy SHA-256 match:
  `73944F584EC4F57BEFA74EAB18EC0ABE933E3E24F1E465DDC060676134B83134`.

Website source and publication:

- `npm run build` - exit 0.
- `npm test` - exit 0; 4/4 tests passed, zero failures and zero skips. A
  parallel build/test attempt encountered a generated-directory race; the
  test was rerun serially and passed cleanly.
- `npm run lint` - exit 0.
- Exact validated site source committed and pushed as
  `4f5d9744d9b73e448fda7c7747edfa2d6e1cc2ca`.
- Sites packaging passed; version save succeeded; private deployment
  succeeded at `https://candu-physics-guide-download.pyrx87.chatgpt.site`.
- Final site inspection reported active custom access with one owner, zero
  external visitors, and zero allowed groups. The site source tree was clean
  after publication.

## Numerical differences

Not applicable. The update explains existing evidence and changes PDF layout
only. It does not select or change an equation, coefficient, unit, sign,
convergence policy, tolerance, golden payload, or numerical reference value.

## Deferred tests and authority boundaries

- Direct DRAGON5/DONJON5 reproduction, direct production/full-core CANDU
  authority, 380-channel coverage, release claims, safety claims, and mobile
  validation remain deferred exactly as recorded by G4-R6 and G5.
- Phase 6/G6 RRS, liquid-zone, adjuster, poison-rate, and actuator behavior is
  described as future work only; no current G4/G5 credit is assigned to it.
- No additional browser QA was required after the successful private Sites
  deployment; local website build, tests, lint, PDF render, and consistency
  checks passed.

## Blockers, risks, and next eligible task

- Blockers: none.
- Risk trigger: not fired; this was documentation, PDF layout, and authorized
  publication work without numerical/runtime changes.
- The historical G4-R5 blocked disposition and G4-R6 bounded ReducedModel PASS
  were preserved and explicitly distinguished; no historical report was
  rewritten.
- Next eligible work: continue the already activated Phase 6/G6 task-definition
  chain under the existing frozen specifications and gate rules.

## Authority and evidence

- Operating protocol: `AGENTS.md`
- Product/phase authority: `docs/Implementation_plan.md`
- Current scope and gate lookup: `docs/PROJECT_SCOPE.md`
- Guide update source: `docs/physics/candu-nuclear-diffusion-student-guide.md`
- G4-R6 authority disposition: `docs/gates/G4-R6.md`
- G5 disposition: `docs/gates/G5.md`
- Prior publication report: `docs/tasks/DOC-G5-PUBLISH-01.md`
