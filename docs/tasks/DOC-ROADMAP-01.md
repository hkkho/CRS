# DOC-ROADMAP-01 - Detailed project roadmap and Sol-validation webpage

## Outcome

Status: COMPLETE

The existing private authenticated Sites page now includes a mobile-friendly
`/roadmap` route. It presents all 12 implementation phases, the official
numbered task/report rows currently represented in the plan and repository, all
Sol gates (including optional G7B), and the supporting G2 corrective/review and
documentation records. Each row identifies whether Sol validation is required,
conditional, or gate-spaced, and states the level of specification or code and
the T-level evidence reviewed. The root PDF page links to the roadmap.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: Luna / high for bounded site and documentation work, per repository routing policy
- Actual model / reasoning: `UNVERIFIED`; no worker execution receipt or actual-model telemetry was exposed for this root execution
- Execution receipt or telemetry source: Unavailable
- Attempts: 1 implementation attempt; elapsed time: Unavailable
- Artifact/checkpoint status: produced
- Review disposition: not applicable; this task changed site presentation/documentation and test coverage, not reactor equations, constants, runtime solver code, or golden data
- Reviewer reuse/fresh-review rationale: not applicable; no independent Sol review was required for this scoped documentation/site change

## Files created or changed

- `tmp/private-physics-download-site/app/roadmap/page.tsx` - added the authenticated phase/task/gate/Sol-validation matrix and roadmap explanation
- `tmp/private-physics-download-site/app/page.tsx` - added the roadmap link and description to the existing authenticated PDF page
- `tmp/private-physics-download-site/app/globals.css` - added responsive roadmap, table, status, legend, and navigation styles
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - added authenticated roadmap coverage and root navigation coverage
- `docs/tasks/DOC-ROADMAP-01.md` - recorded this task evidence

Pre-existing files inspected but not changed: `docs/Implementation_plan.md`,
the existing task reports under `docs/tasks/`, `tmp/private-physics-download-site/app/layout.tsx`,
`tmp/private-physics-download-site/app/chatgpt-auth.ts`,
`tmp/private-physics-download-site/.openai/hosting.json`, the existing PDF asset,
and the existing private-site package/lock files.

## Assumptions and design choices

- The implementation plan is the source of truth. Planned work packages are
  nested under their phase row because they do not yet have separate numbered
  task IDs; numbered task rows remain distinct from planning work packages.
- Current report status is shown as recorded. In particular, G2 remains
  `INCOMPLETE`, and G2-R3 is not presented as authoritative Sol approval because
  PLAN-C02 requires mismatched or unverified actual-model evidence to remain
  `UNVERIFIED`.
- The page describes Sol spacing as one bounded phase-gate review with immediate
  risk-trigger escalation. It does not invent per-worker model evidence or
  assign costs from an aggregate.
- The page is private and uses the existing `requireChatGPTUser` path. No public
  access, new authentication mechanism, external dependency, physics equation,
  constant, tolerance, data pack, or runtime behavior was added.
- Literature applicability: `NotApplicable`. This is a site/documentation
  consumer of already approved project records; it does not select, change,
  validate, or review equations, constants, units, normalization, coefficients,
  burnup, xenon, feedback, reference cases, or golden data.

## Validation commands and results

### Site lint

```text
npm run lint
```

Result: exit code 0. ESLint passed after replacing internal route anchors with
Next `Link` components.

### Production site build

```text
$env:WRANGLER_LOG_PATH='.wrangler\wrangler.log'; & '.\node_modules\.bin\vinext.cmd' build
```

Result: exit code 0. Both dynamic routes built: `/` and `/roadmap`. The direct
Windows command was used because the package scripts use POSIX inline environment
syntax.

### Focused rendered HTML checks (T1)

```text
node --test tests\rendered-html.test.mjs
```

Result: exit code 0; 4 passed, 0 failed. Checks cover unauthenticated redirect,
authenticated PDF page, authenticated roadmap content/Sol review depth, and PDF
magic bytes/size.

### Diff and source preparation

```text
git diff --check
git status --short
git rev-parse HEAD
```

Result: diff check passed. The exact source commit was
`575342bc3f925f713de5508f2a427bc886206c63`; it was pushed to `sites-origin`.

### Sites package, save, and private deployment

```text
& 'C:\Program Files\Git\bin\bash.exe' -lc "/c/Users/infin/.codex/plugins/cache/openai-bundled/sites/0.1.34/scripts/package-site.sh /c/Users/infin/candu/tmp/private-physics-download-site /c/Users/infin/candu/tmp/private-physics-download-site.tar.gz"
```

Result: package succeeded. Sites saved version 2 as
`appgprj_6a79e6f4571c81919c1ee46d6e611b00~appgver_1316fc0732288191b09c52a5a11fbe10`
from the pushed commit. The archive was recorded at 1,495,040 bytes with 92
files. Private deployment
`appgdep_6a7b1ed016388191afc23dd5dedb96bb` reached `succeeded` at:

`https://candu-physics-guide-download.pyrx87.chatgpt.site`

An unauthenticated `curl.exe -I` request returned `401 Unauthorized`, as expected
for the owner-only private site.

## Token and cost accounting

Record request-level or worker-level values only when telemetry exposes them.
Do not invent a per-worker allocation from an aggregate total.

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

Cost formula/basis: unavailable. No per-worker or per-tool cost is inferred from
the page size, shell output, or aggregate service usage.

## Numerical differences

Not applicable; no numerical behavior changed.

## Deferred validation

- Browser visual inspection at multiple viewport sizes - deferred because the
  user requested the webpage/deployment, not a visual QA pass, and no browser
  inspection was required by this documentation task.
- T3-T6 physics and runtime gates - deferred to their named project gates; this
  change does not alter the simulation.

## Blockers, risks, and follow-up

- Blockers: none for the webpage deployment.
- Risk triggers: none fired for reactor behavior; the page documents existing
  risk-trigger policy without changing it.
- Risks accepted or deferred: the roadmap is a manually maintained projection of
  the implementation plan and task reports; it should be regenerated or checked
  whenever those source records change.
- Follow-up work: keep the roadmap consistent as task reports and gate
  dispositions change; do not treat the displayed G2-R3 report as approval until
  actual Sol evidence is verified.

## Next eligible task

Verified actual-model Sol review and disposition of G2 - Approve complete
mathematical specification. Until G2 is authoritative, do not begin production
physics-solver implementation.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md), sections 5-6.
