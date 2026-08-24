# DOC-INSTRUCTIONS-01 - Consolidate active instructions and reference guides

## Outcome

Status: COMPLETE

Consolidated the active documentation hierarchy so that repository rules govern
execution, the implementation plan governs product/phase intent, the project
scope register governs current status, specifications/ADRs govern technical
decisions, and task/gate reports preserve evidence. Removed stale Sol-routing
instructions, retired the generic parallel scaffolding system, corrected Phase
2 status labels, and synchronized the student guide and private roadmap with
the completed Phase 3 and bounded Phase 4 implementation state.

No runtime behavior, physics equation, unit, tolerance, golden value, schema,
or gate disposition was changed.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root documentation implementer.
- Requested model / reasoning: GPT-5.6 Luna / high under repository policy.
- Actual model / reasoning: UNVERIFIED; this task has no task-level execution
  receipt or telemetry allocation.
- Execution receipt or telemetry source: Unavailable.
- Attempts: 1 bounded documentation-maintenance attempt; elapsed time:
  unavailable.
- Artifact/checkpoint status: produced.
- Review disposition: not applicable; this task did not alter runtime,
  numerical, public-contract, or gate evidence.
- Review evidence verification: not applicable.
- Reviewer reuse/fresh-review rationale: not applicable.

## Files created or changed

- AGENTS.md - added the authoritative document map and retained the sole
  operational protocol.
- CODEX_TASK_TEMPLATE.md, docs/tasks/TASK_REPORT_TEMPLATE.md, and
  docs/gates/GATE_REPORT_TEMPLATE.md - removed stale routing instructions and
  made templates structure-only.
- docs/Implementation_plan.md and docs/PROJECT_SCOPE.md - removed duplicated
  operational prompts/queues, clarified authority order, and documented
  guide-PDF synchronization.
- LLM_REPOSITORY_SCAFFOLDING_BLUEPRINT.md - replaced 1,200 lines of generic,
  conflicting scaffolding with a short retired pointer.
- docs/adr/ADR_TEMPLATE.md, docs/adr/ADR-011-engine-neutral-json-serialization.md,
  docs/reference/candu-literature-digest-v1.md, and docs/spec/ - aligned active
  references with the current review lane and G2 waiver without changing
  technical content.
- reference/README.md, unity/README.md, and
  docs/platforms/mobile-build-prerequisites.md - made setup references
  subordinate to the authority hierarchy and current phase status.
- docs/physics/candu-nuclear-diffusion-student-guide.md,
  tools/build_physics_guide_pdf.py, tools/Check-PhysicsGuideConsistency.ps1,
  output/pdf/candu-nuclear-diffusion-student-guide.pdf, and the matching
  private-site PDF - documented the bounded static-solver foundation and
  regenerated/synchronized the guide artifact.
- tmp/private-physics-download-site/README.md,
  app/roadmap/page.tsx, tests/rendered-html.test.mjs, and package.json - made
  the derived roadmap current and its validation commands portable on Windows.
- docs/tasks/DOC-INSTRUCTIONS-01.md - this report.

Historical task and gate reports were inspected but not rewritten. Frozen
equations, tolerances, golden-data rules, and phase dispositions were preserved.

## Assumptions and design choices

- Historical reports are evidence, not active instructions; former Sol labels
  remain there rather than being relabelled.
- The G2 disposition remains exactly FORCED CLOSED / WAIVED. It is an
  administrative overlap authorization, not a technical PASS.
- P4-T01 through P4-T03 establish only the documented static-solver
  foundation. P4-T04, comparison evidence, golden consumers, and G4 remain
  required before validation claims.
- The private roadmap is a derived status view and explicitly defers to the
  scope register and linked evidence.

## Validation commands and results

### Documentation support syntax - T0

    python -m py_compile .\tools\build_physics_guide_pdf.py

    $tokens=$null; $errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile(
      (Resolve-Path .\tools\Check-PhysicsGuideConsistency.ps1),
      [ref]$tokens,
      [ref]$errors
    )

Result: DOC_SUPPORT_SYNTAX_PASS and STATIC_SOLVER_PATHS_PASS.

### Guide generation, synchronization, and consistency - T1

    C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe .\tools\build_physics_guide_pdf.py

    .\tools\Check-PhysicsGuideConsistency.ps1

Result: regenerated a 13-page PDF, copied byte-identical output to the private
site, and reported PHYSICS_GUIDE_CONSISTENCY_PASS with
core_static_solver=present and private_pdf_sync=PASS.

### PDF visual inspection - T1 manual

    C:\Users\infin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe -png -r 150 output\pdf\candu-nuclear-diffusion-student-guide.pdf tmp\pdfs\doc-instructions-01-guide-render-v2\guide

Result: rendered 13 PNG pages and inspected all pages. Corrected an initially
crowded numbered iteration list, regenerated the PDF, and confirmed the final
cover, headings, tables, list spacing, page numbers, and references were
legible with no clipping or overlap.

### Documentation hierarchy and local-link audit - T1

    Inline PowerShell audit of 18 changed Markdown documents: required
    hierarchy markers, stale-instruction exclusions, retired-blueprint line
    count, and all local Markdown link targets.

Result: DOC_GOVERNANCE_AUDIT_PASS docs=18 blueprint_lines=29 links=validated.

### Private roadmap build and tests - T1

    npm run build

    npm test

Result: the private site built successfully with both routes present. Its
four tests passed: authenticated redirect, PDF download page, current roadmap
markers, and packaged PDF asset.

## Token and cost accounting

Record request-level or worker-level values only when telemetry exposes them.
No task-level allocation was available.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry |
| Cached input tokens | Unavailable | No task-level telemetry |
| Cache-write input tokens | Unavailable | No task-level telemetry |
| Output tokens | Unavailable | No task-level telemetry |
| Reasoning output tokens | Unavailable | No task-level telemetry |
| Total tokens | Unavailable | No task-level telemetry |
| Estimated cost | Unavailable | No allocable usage or price basis |
| Goal-service total | Unavailable | Not added to request totals |

Cost formula/basis: unavailable; no request-level billing telemetry was exposed.

## Numerical differences

Not applicable; no numerical behavior or reference baseline changed.

## Deferred validation

- T3-T6 - not run. This task changed documentation, derived artifacts, and
  site presentation only; it did not change runtime behavior, numerical
  implementation, Unity integration, mobile behavior, or a reference baseline.
- Independent code review - not triggered. The task did not change an
  equation, unit, sign, convergence implementation, tolerance, golden data,
  public runtime contract, or gate disposition.

## Blockers, risks, and follow-up

- Blockers: none.
- Risk triggers: none.
- Risks accepted or deferred: the bundled Poppler wrapper failed to locate its
  executable in this workspace. The direct bundled Poppler executable rendered
  all 13 pages successfully; no document defect remained.
- Follow-up work: P4-T04 remains the next documented Phase 4 handoff. It is
  not authorized by this report.

## Next eligible task

P4-T04 - outer convergence and invalid-state diagnostics, if separately
authorized and executed against the current scope register and P2-T02.

Source: AGENTS.md, docs/Implementation_plan.md, and docs/PROJECT_SCOPE.md.
