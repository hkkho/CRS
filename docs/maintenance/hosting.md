# Browser hosting

- Published game: https://hkkho.github.io/CRS/

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
200% zoom-equivalent and narrow layouts. The published game is smoke-tested and
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
npm run smoke:scales -- http://127.0.0.1:4175/CRS/
npm run smoke:accessibility -- http://127.0.0.1:4175/CRS/
npm run smoke:saves -- http://127.0.0.1:4175/CRS/
node scripts/benchmark-wasm.mjs http://127.0.0.1:4175/CRS/ --bridge=direct --label=pages-local --warm-samples=1
```

The worker resolves the runtime through Vite's base URL. The normal `/` build
remains available for local development; `build:pages` produces the `/CRS/` site.
Registration alone does not confirm deployment: inspect the workflow and published
commit in `wasm/build-info.json`.
