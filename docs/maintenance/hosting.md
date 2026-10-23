# Browser hosting

- Published game: https://hkkho.github.io/CRS/
- Published documentation: https://hkkho.github.io/CRS/docs/

The same Pages artifact includes the MkDocs player manual, current reactor theory
and code walkthrough under `docs/`. A separate documentation job installs the
pinned Python dependencies, runs `mkdocs build --strict` and checks generated
local links/anchors. The Pages build adds that validated output **after** building
the game, so the offline game asset cache remains independent of documentation.
Predeployment and live documentation smoke checks verify routes, search, the
shared in-game manual and the source revision in `docs/build-info.json`.

The F1 dialog bundles the chapters in `docs/code-library/manual` through
`manualContent.ts`; it does not fetch them at runtime. Its website link opens a
separate tab so reading the full library preserves the current reactor.

GitHub Pages uses the existing public repository and GitHub account. It needs
no additional service account or deployment secret. Pages is configured with
`build_type: workflow`. The single-threaded WASM build remains the default.

The deployment workflow validates Core, Game and the browser contract in Release
configuration, followed by frontend tests/builds. The test runners retain their
Debug default for local use and accept `-Configuration Release` for optimized checks.
The Pages build consumes the validated WASM artifact, builds with `/CRS/` as its
base URL, and runs the production browser smoke test and deterministic reproduction
matrix before publishing. Only pushes to `main` and manual runs on `main` publish;
pull requests validate without deployment. Default daily acceptance includes unavailable-WASM recovery; predeployment checks
also cover keyboard-only planning/day execution, focus return, reduced motion,
200% zoom-equivalent and narrow layouts. A two-version production test verifies
the update prompt, protection of other game tabs, and preservation of the saved
day and draft after reload. The published game is smoke-tested and
benchmarked with the expected Git commit identity.

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
npm run smoke:arcade -- http://127.0.0.1:4175/CRS/
npm run smoke:scales -- http://127.0.0.1:4175/CRS/
npm run smoke:accessibility -- http://127.0.0.1:4175/CRS/
npm run smoke:saves -- http://127.0.0.1:4175/CRS/
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
