# PLAN-C02 - Enforce verified model routing, bounded review, and usage accounting

## Outcome

Status: COMPLETE

Updated the project process so Luna remains the bounded implementation lane,
Sol remains the architecture/physics/gate lane, and reasoning effort is chosen
by measured task difficulty rather than defaulting to max. The policy now
requires actual-model verification before a review can count as independent Sol
evidence, limits review churn through reviewer/context reuse, records stalled
workers as `NO_ARTIFACT`, and adds per-task token/cost/effectiveness fields.

This was a documentation and process-policy task only. No runtime, physics,
schema, reference, golden-data, Unity, or test implementation changed.

Effectiveness: SUCCESS - policy controls and reporting fields were added; the
existing historical review-evidence limitation is explicitly carried forward.

## Execution and model evidence

- Role: root documentation/process edit; no Luna worker or Sol reviewer was used.
- Requested model / reasoning: no subagent override; root Codex session.
- Actual model / reasoning: root session identity/effort was not exposed as a
  task-level receipt; this report does not claim Sol review authority.
- Execution receipt or telemetry source: unavailable for this documentation task.
- Attempts: 1; elapsed time: not exposed.
- Artifact/checkpoint status: produced; five documentation files are in scope.
- Review disposition: not applicable; no independent review was requested for
  this process-only change.
- Reviewer reuse/fresh-review rationale: not applicable.

## Files created or changed

- `AGENTS.md` - added routing, evidence-verification, bounded-retry, and
  usage-accounting rules.
- `CODEX_TASK_TEMPLATE.md` - added routing/fallback and evidence requirements
  to reusable task prompts.
- `docs/Implementation_plan.md` - changed model defaults, review cadence,
  actual-model acceptance, telemetry/cost accounting, task queue labels, and
  reusable prompts.
- `docs/tasks/TASK_REPORT_TEMPLATE.md` - added execution evidence,
  effectiveness, token, and cost sections.
- `docs/tasks/PLAN-C02.md` - this report.

Pre-existing task, sprint, gate, runtime, Unity, reference, and generated files
were inspected but not changed.

## Assumptions and design choices

- `PLAN-C02` is the administrative task ID because the user requested the
  project update without naming an ID. The scope is limited to policy and
  reporting documents.
- High reasoning is the default for routine Luna implementation and routine
  Sol review; max remains available for unusually difficult decisions with a
  recorded rationale. Terra is an optional documented fallback or benchmark,
  not an unverified Sol substitute.
- Requested model labels are not treated as execution evidence. A missing or
  mismatched telemetry record is `UNVERIFIED`, not a pass.
- Cost values are not hardcoded into the plan because prices and billing rules
  can change. Reports must record the price source/date and formula, or state
  that cost is unavailable. Goal-service totals remain separate from request
  totals.
- Historical reports were not silently rewritten. A prior telemetry audit found
  that the reviewer threads recorded as Sol for G2-C04 and G2-R2/G2-R3 resolved
  to Luna in the actual model field:
  `019fe7ad-e66a-7441-bf7d-a5774d18f701` and
  `019fe7f4-ab48-7f52-bd5c-b1005c6e5664`. Under this policy, those historical
  dispositions are `UNVERIFIED` Sol evidence until a verified review is run.

## Validation commands and results

### Required reading and scope - T0

```powershell
Get-Content -Raw AGENTS.md
Get-Content -Raw docs/Implementation_plan.md
Get-Content -Raw CODEX_TASK_TEMPLATE.md
Get-Content -Raw docs/tasks/TASK_REPORT_TEMPLATE.md
Get-Content -Raw docs/tasks/PLAN-C01.md
Get-Content -Raw docs/tasks/SPRINT-2026-08-09-R2.md
Get-Content -Raw docs/tasks/G2-R3.md
git status --short --untracked-files=all
```

Result: PASS. The complete implementation plan, current repository policy,
templates, prerequisite administrative report, latest corrective sprint report,
latest gate report, and unborn/untracked worktree were inspected before edits.

### Policy contract and stale-default check - T1

```powershell
$files=@('AGENTS.md','CODEX_TASK_TEMPLATE.md','docs/Implementation_plan.md','docs/tasks/TASK_REPORT_TEMPLATE.md')
foreach($f in $files){if(-not (Test-Path -LiteralPath $f)){throw "missing $f"}}
$a=Get-Content -Raw AGENTS.md; $p=Get-Content -Raw docs/Implementation_plan.md
$c=Get-Content -Raw CODEX_TASK_TEMPLATE.md; $r=Get-Content -Raw docs/tasks/TASK_REPORT_TEMPLATE.md
foreach($term in @('Model routing, review evidence, and usage accounting','UNVERIFIED','NO_ARTIFACT')){if(-not $a.Contains($term)){throw "missing $term"}}
foreach($term in @('4.4 Routing and review-evidence acceptance','4.5 Token, cost, and effectiveness accounting','high (max if justified)','high-xhigh (max if justified)')){if(-not $p.Contains($term)){throw "missing $term"}}
foreach($term in @('Requested model / reasoning:','Routing and fallback:','actual model and','fresh-review swarm')){if(-not $c.Contains($term)){throw "missing $term"}}
foreach($term in @('Execution and model evidence','Token and cost accounting','Cached input tokens','Estimated cost')){if(-not $r.Contains($term)){throw "missing $term"}}
foreach($term in @('Luna max','Sol max','Luna-max','Sol-max')){if($p.Contains($term)){throw "stale effort label: $term"}}
'PLAN_C02_T1_PASS'
```

Result: PASS, exit `0`; required routing/evidence/accounting terms were present
and stale max-default labels were absent from the current plan.

### Repository format - T1

```powershell
& .\tools\Check-Format.ps1
```

Result: PASS, exit `0`.

### Final scope inspection - T0

```powershell
git status --short --untracked-files=all -- AGENTS.md CODEX_TASK_TEMPLATE.md docs/Implementation_plan.md docs/tasks/TASK_REPORT_TEMPLATE.md docs/tasks/PLAN-C02.md
Get-Content -Raw AGENTS.md
Get-Content -Raw CODEX_TASK_TEMPLATE.md
Get-Content -Raw docs/Implementation_plan.md
Get-Content -Raw docs/tasks/TASK_REPORT_TEMPLATE.md
Get-Content -Raw docs/tasks/PLAN-C02.md
```

Result: PASS. Only the four policy/template files and this report were edited
for this task; no runtime or physics path was added to the task scope. The
repository remains an unborn/untracked worktree with pre-existing unrelated
paths preserved.

## Token and cost accounting

This documentation task did not expose a request-level or worker-level token
receipt in the repository context.

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | no task-level receipt exposed |
| Cached input tokens | `Unavailable` | no task-level receipt exposed |
| Cache-write input tokens | `Unavailable` | no task-level receipt exposed |
| Output tokens | `Unavailable` | no task-level receipt exposed |
| Reasoning output tokens | `Unavailable` | no task-level receipt exposed |
| Total tokens | `Unavailable` | no task-level receipt exposed |
| Estimated cost | `Unavailable` | no task-level price receipt exposed |
| Goal-service total | `Unavailable` | no goal-service record used for this task |

Cost formula/basis: deferred to a provider receipt; no estimate was invented.

## Numerical differences

Not applicable; no numerical behavior, physics equation, coefficient, unit,
normalization, tolerance, reference result, golden value, or runtime data
changed.

## Deferred validation

- T2-T6 and the full runtime suite - deferred because this task changes only
  process documents and report templates.
- Verified Sol re-review of the historical G2-C04/G2-R2/G2-R3 evidence -
  deferred to a separately prepared and approved corrective review task; this
  task does not rerun a gate or begin Phase 3.
- Historical per-task cost reconstruction - not fabricated where worker-level
  telemetry was unavailable; future reports now have the required fields.

## Blockers, risks, and follow-up

- Blockers: none for the documentation update.
- Risk triggers: a model-routing/evidence-integrity trigger was recorded; no
  numerical/runtime trigger fired.
- Risks accepted or deferred: the existing G2 Sol labels remain historical
  records, but are not accepted as verified Sol authority until actual-model
  telemetry is confirmed by a corrective review.
- Follow-up: prepare and approve a verified-model review/audit of the G2 gate
  evidence before treating G2 as an authoritative Sol-approved gate or starting
  Phase 3 implementation.
- No commit, push, publication, external message, download, secret, destructive
  operation, or generated repository artifact was produced.

## Next eligible task

Prepare and approve a one-purpose verified-model review/audit for the prior G2
gate evidence. Do not start Phase 3 implementation until that review has a
verified actual Sol identity and a recorded final disposition.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md),
`AGENTS.md`, and the model-routing audit recorded above.
