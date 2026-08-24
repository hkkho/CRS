# DOC-LINK-01 - Private mobile download link

## Outcome

Status: COMPLETE

Created and deployed a private, authenticated Sites download page for the
existing CANDU physics guide PDF. The page requires ChatGPT authentication,
shows the signed-in viewer identity, and provides a mobile-friendly PDF
download button. The deployed URL is:

`https://candu-physics-guide-download.pyrx87.chatgpt.site`

The unauthenticated TLS check returned `401 Unauthorized`, confirming that the
site is not anonymously readable.

Effectiveness: SUCCESS

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high, per repository policy for
  bounded implementation and documentation work
- Actual model / reasoning: UNVERIFIED
- Execution receipt or telemetry source: Unavailable
- Attempts: 1 bounded site implementation; 2 private deployment attempts, with
  the second retry made only after the first provider TLS-certificate timeout
- Artifact/checkpoint status: produced; validated source commit and deployed
  Sites version
- Review disposition: not applicable; no physics, architecture, or gate review
  was required
- Reviewer reuse/fresh-review rationale: not applicable

## Files created or changed

- `tmp/private-physics-download-site/` - isolated Sites source project for the
  authenticated download page; it does not alter the production C# or Unity
  project.
- `tmp/private-physics-download-site/app/page.tsx` - authenticated page and PDF
  download control.
- `tmp/private-physics-download-site/app/layout.tsx` and
  `tmp/private-physics-download-site/app/globals.css` - site metadata and
  responsive mobile presentation.
- `tmp/private-physics-download-site/app/chatgpt-auth.ts` - starter-provided
  ChatGPT sign-in helper used for server-side authentication.
- `tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf`
  - binary-safe copy of the existing PDF; SHA-256
  `AEF6DF9457D17F87B4D10C1F0A7BE4C67E58B4B9A507AA97E50441E80A4258C7`.
- `tmp/private-physics-download-site/tests/rendered-html.test.mjs` - focused
  tests for anonymous redirect, authenticated rendering, and PDF packaging.
- `tmp/private-physics-download-site/.openai/hosting.json` and
  `.gitattributes` - Sites project identity and binary PDF handling.
- `docs/tasks/DOC-LINK-01.md` - this task report.

The existing Markdown guide, original PDF, runtime source, physics
specifications, and Unity adapter were inspected and not changed.

## Assumptions and design choices

- A private owner-only Sites deployment is the requested access model. No
  public or temporary file host was used.
- Platform authentication is used instead of adding app-owned OAuth or a new
  account system. The private Sites access policy protects the page and its
  static PDF asset, while the page also requires the forwarded ChatGPT user
  identity server-side.
- The PDF is copied as a binary asset and marked `*.pdf -text` so Windows Git
  line-ending conversion cannot corrupt downloads.
- Literature digest applicability: `NotApplicable` - this task only hosts an
  already-created document and does not select, change, validate, or review
  physics equations, constants, units, numerical methods, or golden data.

## Validation commands and results

### Site build (T0/T1)

```text
$env:WRANGLER_LOG_PATH='.wrangler\wrangler.log'; & '.\node_modules\.bin\vinext.cmd' build
```

Result: exit code 0. Vinext completed all five build stages and reported the
dynamic `/` route.

### Focused authenticated-page tests (T1)

```text
node --test tests/rendered-html.test.mjs
```

Result: exit code 0; 3 tests passed, 0 failed. Covered anonymous sign-in
redirect, authenticated page content/download target, and PDF magic bytes plus
size.

### Lint (T0)

```text
npm run lint
```

Result: exit code 0.

### Sites packaging

```text
"C:\Program Files\Git\bin\bash.exe" -lc "/c/Users/infin/.codex/plugins/cache/openai-bundled/sites/0.1.34/scripts/package-site.sh /c/Users/infin/candu/tmp/private-physics-download-site /c/Users/infin/candu/tmp/private-physics-download-site.tar.gz"
```

Result: exit code 0. Archive validation found `dist/server/index.js` and
`dist/.openai/hosting.json`; archive size was 498,336 bytes.

### Sites source publication

```text
git push sites-origin HEAD:main
```

Result: exit code 0. The exact validated source was pushed at commit
`00ae54c378069270e66138b83a9880a7482f4e57`; the ephemeral authentication
header is intentionally not recorded.

### Private deployment and TLS/authentication check

```text
curl.exe -sS -I --max-time 20 "https://candu-physics-guide-download.pyrx87.chatgpt.site/"
```

Result: exit code 0. The private deployment reached the host over HTTPS and
returned `401 Unauthorized` without an authenticated session. The first
deployment attempt timed out waiting for its TLS certificate; one bounded retry
succeeded and returned the URL recorded above.

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
or pricing basis not present in the execution receipt. The Sites service may
have its own account-level billing, but no task-level cost telemetry was
available here.

## Numerical differences

Not applicable; no simulation or numerical behavior changed.

## Deferred validation

- Authenticated browser interaction from the mobile Remote app - not directly
  automated because no in-app browser control was available in this session;
  the deployed URL and unauthenticated 401 response were verified.
- Re-deployments after future PDF revisions - run the same focused build/test,
  packaging, and private deployment workflow after the guide changes.
- T3-T6 physics and runtime suites - not applicable; this task did not touch
  equations, state, clocks, schemas, or numerical code.

## Blockers, risks, and follow-up

- Blockers: none; the first provider TLS timeout was resolved by one bounded
  retry.
- Risk triggers: none fired; no physics, runtime, serialization, or mobile game
  code changed.
- Risks accepted or deferred: the hosted PDF is a copied artifact and must be
  refreshed when the source guide changes. The private Sites deployment remains
  tied to the current PDF hash and source version.
- Follow-up work: when the guide is updated, copy the new PDF into the isolated
  Sites source, rerun the focused checks, create a new version, and redeploy
  privately.

## Next eligible task

None for this link task. The next project task remains governed by the
implementation plan and the required verified actual Sol review before Phase 3.

Source: [`docs/Implementation_plan.md`](../Implementation_plan.md), sections
3, 5, 6, and 9.
