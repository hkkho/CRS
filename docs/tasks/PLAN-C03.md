# PLAN-C03 - Code-review policy, gate overlap, G2 waiver, and searchable scope index

## Outcome

Status: COMPLETE

The active project rules now route all new activities previously assigned to Sol
through `code review (high)`. The rules permit independent work to overlap an
incomplete non-critical gate and define the critical blockers that still stop
dependent work. G2 is administratively `FORCED CLOSED / WAIVED` by explicit user
direction; the historical G2 FAIL, G2-R2 INCOMPLETE, and G2-R3 technical PASS
records remain intact. Phase 3 and later work may proceed from the frozen Phase 2
specifications, subject to critical-stop rules.

Added `docs/PROJECT_SCOPE.md` as the compact, searchable agent index for all
phases, gates, numbered tasks, current Phase 3 reports, later work packages,
review depths, overlap rules, and scope exclusions. The private HTML roadmap was
updated to match the new review vocabulary and current G2 disposition.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: Luna / high for bounded policy and documentation work, per the active routing policy
- Actual model / reasoning: `UNVERIFIED`; no worker execution receipt or actual-model telemetry was exposed for this root execution
- Execution receipt or telemetry source: Unavailable
- Attempts: 1 implementation attempt; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable for independent technical review; the G2 waiver was explicitly user-authorized and this task changed policy/documentation, not reactor runtime behavior
- Reviewer reuse/fresh-review rationale: not applicable; future technical gate work uses the new code-review (high) lane

## Files created or changed

- `AGENTS.md` - changed active review routing from Sol to code review (high), added searchable scope lookup, and added gate-overlap/critical-blocker rules
- `docs/Implementation_plan.md` - changed future review labels/prompts, added PLAN-C03 to the queue, added the overlap policy, and replaced the G2 hard serial blocker with the explicit waiver rule
- `docs/PROJECT_SCOPE.md` - added the searchable canonical scope index
- `docs/gates/G2-FC-01.md` - added the user-authorized forced-closure addendum and carried-forward evidence/critical stops
- `docs/gates/G2.md` - added a current-disposition notice while preserving the historical FAIL report
- `docs/tasks/G2.md` - identified the historical INCOMPLETE report and linked the current waiver
- `tmp/private-physics-download-site/app/roadmap/page.tsx` - aligned the private roadmap with the code-review lane, G2 waiver, overlap policy, and searchable-index reference
- `tmp/private-physics-download-site/app/page.tsx` - aligned the PDF-page description with the new review terminology
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - updated route assertions for the new current disposition
- `docs/tasks/PLAN-C03.md` - recorded this task report

Pre-existing files inspected but not changed include the Phase 2
specifications, P1-T08 digest/report, G2 corrective/rerun reports, existing
Unity/Core source, the existing PDF asset, and the Sites hosting configuration.

The main repository is an unborn/untracked scaffold. No unrelated files were
staged, committed, reset, deleted, or published.

## Assumptions and design choices

- `FORCED CLOSED / WAIVED` is deliberately distinct from `PASS`. It authorizes
  progress but does not erase or relabel historical review evidence.
- The waiver permits implementation of the existing frozen Phase 2
  specifications. It does not permit new equation, unit, sign, boundary,
  convergence, tolerance, golden-data, or public-contract decisions without the
  active code-review (high) lane.
- `docs/PROJECT_SCOPE.md` is a routing/index document, while
  `docs/Implementation_plan.md` remains the detailed execution baseline and the
  specification files remain the authority for approved model details.
- The HTML roadmap remains private and owner-authenticated. Its displayed table
  is a presentation of the Markdown/index and current reports, not a separate
  source of physics authority.
- Literature applicability: `NotApplicable`. This task changes policy,
  navigation, and scope documentation only; it does not select, change,
  validate, or review physics equations, constants, units, normalization,
  coefficients, burnup, xenon, feedback, reference cases, tolerances, or golden
  data.

## Validation commands and results

### Policy and scope assertions (T0/T1)

```text
$ErrorActionPreference='Stop'
$required = @('AGENTS.md','docs\Implementation_plan.md','docs\PROJECT_SCOPE.md','docs\gates\G2-FC-01.md','docs\gates\G2.md','docs\tasks\G2.md')
foreach($path in $required){ if(-not (Test-Path -LiteralPath $path)){ throw "Missing $path" } }
$agents = Get-Content -Raw AGENTS.md
$plan = Get-Content -Raw docs\Implementation_plan.md
$scope = Get-Content -Raw docs\PROJECT_SCOPE.md
foreach($term in @('code review (high)','FORCED CLOSED / WAIVED','critical blocker','docs/PROJECT_SCOPE.md')){ if($agents -notmatch [regex]::Escape($term)){ throw "AGENTS missing $term" } }
foreach($term in @('PLAN-C03','code review (high)','FORCED CLOSED / WAIVED','Gate overlap and critical-blocker policy')){ if($plan -notmatch [regex]::Escape($term)){ throw "plan missing $term" } }
foreach($term in @('PHASE-0','PHASE-11','P0-T01','P2-T05','G2','P3-T03','CODE-REVIEW-HIGH','Critical blockers')){ if($scope -notmatch [regex]::Escape($term)){ throw "scope missing $term" } }
$bad = @(Select-String -Path AGENTS.md,docs\Implementation_plan.md -Pattern 'Sol (gate|review|approval|led|high)|Sol /|request Sol|except Sol' -CaseSensitive:$false)
if($bad.Count -ne 0){ throw 'old active Sol routing remains' }
```

Result: `POLICY_SCOPE_PASS`; required files, search keys, forced disposition,
active code-review wording, and absence of old active Sol-routing labels passed.

### Markdown format/scope check (T0)

```text
$paths=@('AGENTS.md','docs\PROJECT_SCOPE.md','docs\gates\G2-FC-01.md','docs\gates\G2.md','docs\tasks\G2.md')
$bad=@(); foreach($p in $paths){$line=0; Get-Content -LiteralPath $p | ForEach-Object {$line++; if($_ -match '[ \t]+$'){$bad += "${p}:$line"}}}; if($bad.Count){throw 'trailing whitespace'}
```

Result: `POLICY_DOC_FORMAT_PASS`. The new/updated policy and gate-addendum files
had no trailing whitespace. Existing intentional Markdown hard breaks in the
implementation plan were preserved.

### Sites lint

```text
npm run lint
```

Result: exit code 0.

### Sites production build

```text
$env:WRANGLER_LOG_PATH='.wrangler\wrangler.log'; & '.\node_modules\.bin\vinext.cmd' build
```

Result: exit code 0. Dynamic routes `/` and `/roadmap` built successfully.

### Focused rendered HTML tests (T1)

```text
node --test tests\rendered-html.test.mjs
```

Result: exit code 0; 4 passed, 0 failed. Coverage includes authentication,
private PDF-page content, the new roadmap, the `FORCED CLOSED / WAIVED` G2
display, code-review depth, and PDF asset validity.

### Sites source/package/deployment

```text
git rev-parse HEAD
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' -lc "/c/Users/infin/.codex/plugins/cache/openai-bundled/sites/0.1.34/scripts/package-site.sh /c/Users/infin/candu/tmp/private-physics-download-site /c/Users/infin/candu/tmp/private-physics-download-site.tar.gz"
```

Result: exact pushed site commit
`3c64c287574cc7fa0c1ca22155cf5c80504d191f`; package succeeded. Sites saved
version 3 as
`appgprj_6a79e6f4571c81919c1ee46d6e611b00~appgver_20265937ad4c8191bd53c27124e3eb79`
from that commit. The archive was recorded at 1,505,280 bytes with 92 files.
Private deployment
`appgdep_6a7f11c9c0a4819196bb4e835fa1aab2` reached `succeeded` at:

`https://candu-physics-guide-download.pyrx87.chatgpt.site`

An unauthenticated request to `/roadmap` returned `401 Unauthorized`, confirming
that the private owner-only access boundary remains active.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | No request-level token telemetry exposed |
| Cached input tokens | `Unavailable` | No request-level token telemetry exposed |
| Cache-write input tokens | `Unavailable` | No request-level token telemetry exposed |
| Output tokens | `Unavailable` | No request-level token telemetry exposed |
| Reasoning output tokens | `Unavailable` | No request-level token telemetry exposed |
| Total tokens | `Unavailable` | No request-level token telemetry exposed |
| Estimated cost | `Unavailable` | No verified model/price source or allocable usage receipt |
| Goal-service total | `Unavailable` | No goal-service telemetry; not added to request totals |

Cost formula/basis: unavailable. No worker allocation or cost is inferred from
document size, shell output, or aggregate service usage.

## Numerical differences

Not applicable; no numerical behavior, equation, coefficient, unit,
normalization, tolerance, reference baseline, or golden data changed.

## Deferred validation

- No independent code review (high) was run for this administrative/user-directed
  policy change. Future technical tasks and named gates use that lane.
- T3-T6 physics and runtime gates remain deferred to their owning tasks/gates.
- The roadmap's visual inspection at multiple viewport sizes remains deferred;
  focused rendered HTML and production build checks passed.

## Blockers, risks, and follow-up

- Blockers: none for the policy/index update or private deployment.
- Risk triggers: no reactor-behavior trigger fired; the task changed review rules
  and documentation only.
- Risks accepted/deferred: G2 is administratively waived, not technically passed;
  golden consumer tests remain a later evidence requirement; historical reports
  retain their original review labels.
- Follow-up: use `P3-T03` as the next eligible implementation task and record the
  G2 overlap decision. Run code review (high) when a critical trigger or named
  gate requires it.

## Next eligible task

`P3-T03` - complete explicit simulation clock and command queue contracts using
the frozen Phase 3 specification, with the G2 overlap decision recorded.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), and
[`docs/gates/G2-FC-01.md`](../gates/G2-FC-01.md).
