# Browser hosting

- Primary: https://crs-candu-playtest.vercel.app
- Secondary: https://hkkho.github.io/CRS/

GitHub Pages uses the existing public repository and GitHub account. It needs
no additional service account or deployment secret. Pages is configured with
`build_type: workflow`. The single-threaded WASM build remains the default.

The deployment workflow validates Core, Game, the browser contract, frontend,
and a production browser smoke test before publishing. The Pages build consumes
that same validated WASM artifact, builds with `/CRS/` as its base URL, and runs
another browser smoke test. Only `main` publishes either host. Published sites
are smoke-tested and benchmarked with the expected Git commit identity.

The Pages deployment job uses the `github-pages` environment with `pages: write`
and `id-token: write` permissions. See the official
[custom workflow documentation](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages).

## Local subpath verification

After staging the authoritative runtime, run from `web/candu-playtest`:

```powershell
npm run build -- --base /CRS/ --outDir dist-pages
npm run preview -- --outDir dist-pages --base /CRS/ --host 127.0.0.1 --port 4175
```

In another terminal:

```powershell
node scripts/smoke.mjs http://127.0.0.1:4175/CRS/
node scripts/benchmark-wasm.mjs http://127.0.0.1:4175/CRS/ --bridge=direct --label=pages-local --warm-samples=1
```

The worker resolves the runtime through Vite's base URL. The normal `/` build
continues to serve Vercel; the `/CRS/` build serves GitHub Pages. Registration
alone does not confirm deployment: inspect the workflow and published commit.
