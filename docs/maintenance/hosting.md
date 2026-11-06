# Browser hosting

- Published game: https://hkkho.github.io/CRS/
- Published documentation: https://hkkho.github.io/CRS/docs/

The same Pages artifact includes the MkDocs player manual, current reactor theory
and code walkthrough under `docs/`. A separate documentation job installs the
pinned Python dependencies, runs `mkdocs build --strict` and checks generated
local links/anchors. The Pages build adds that validated output **after** building
the game, so the offline game asset cache remains independent of documentation.
The predeployment documentation smoke checks routes, search, the manual and the
source revision in `docs/build-info.json`. The live check confirms the published
manual route and commit.

The F1 dialog bundles the chapters in `docs/code-library/manual` through
`manualContent.ts`; it does not fetch them at runtime. Its website link opens a
separate tab so reading the full library preserves the current reactor.

GitHub Pages uses the existing public repository and GitHub account. It needs
no additional service account or deployment secret. Pages is configured with
`build_type: workflow`. The single-threaded WASM build remains the default.

Full validation runs the Core, Game and Browser .NET suites in Release in
parallel with the AOT build. It also runs Vitest, the direct WASM reproduction
matrix and the two-version save and update smoke. The local test scripts retain
their Debug default and accept `-Configuration Release`.

Pull requests run full validation. Use **Release validation** as the required
status check when configuring branch protection for `main`. Pushes and manual
runs on `main` can reuse an unexpired validation record from a successful run of
this workflow when both its tested tree and repository-owned branch head match
the tree being released. The record lasts 30 days. A direct push without a
matching record, a merge from a fork, or an unavailable lookup runs full
validation before publication.

Every publication builds fresh Release AOT WASM and one frontend bundle under
`/CRS/`, then smoke-tests the exact artifact. The gameplay smoke covers the Start
gate, batched fuel orders, pause, keyboard input and narrow layouts. After
publication, a shorter check confirms the game and docs commit, the published
player manual and one time-based gameplay step.

Vercel deployment steps and secrets are no longer used. The two `vercel.json`
files only disable automatic Git deployments for any retained Vercel connection.

The Pages deployment job uses the `github-pages` environment with `pages: write`
and `id-token: write` permissions. See the official
[custom workflow documentation](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages).

## Local subpath verification

After staging the authoritative runtime, run from `web/candu-playtest`:

```powershell
npm run build:pages
npm run preview -- --configLoader runner --outDir dist-pages --base /CRS/ --host 127.0.0.1 --port 4175
```

In another terminal:

```powershell
npm run smoke -- http://127.0.0.1:4175/CRS/
npm run smoke:upgrade
node scripts/benchmark-wasm.mjs http://127.0.0.1:4175/CRS/ --bridge=direct --label=pages-local --warm-samples=1
```

The worker resolves the runtime through Vite's base URL. The normal `/` build
remains available for local development; `build:pages` produces the `/CRS/` site.
Registration alone does not confirm deployment: inspect the workflow and published
commit in `wasm/build-info.json`.

For a local combined release, run the documentation environment's Python with
`-m mkdocs build --strict --site-dir web/candu-playtest/dist-pages/docs` from the
repository root **after** `npm run build:pages`. Use `CRS_DOCS_REF` to match the
runtime's staged commit when checking release identity. The existing preview
server then serves `/CRS/docs/`; `npm run smoke:docs -- http://127.0.0.1:4175/CRS/`
checks the combined result from the browser package directory. Rebuilding the game
clears its output, so documentation must be regenerated afterward.
