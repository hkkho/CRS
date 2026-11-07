# Build, release and reproduction

The [Pages workflow](../../.github/workflows/deploy-candu-playtest.yml) publishes the [game](https://hkkho.github.io/CRS/) at `/CRS/` and the [documentation](https://hkkho.github.io/CRS/docs/) beneath it. Release validation runs the shared .NET suites, frontend checks, AOT WASM build, gameplay and update smoke, direct reproduction matrix, and documentation build. The publication job smoke-tests the assembled artifact and checks its deployed revision. Use **Release validation** for branch protection.

## Build locally

From the repository root, run the Release checks and stage the same single-threaded WASM runtime used by Pages.

```powershell
./tools/Test-DotNet.ps1 -Configuration Release
./tools/Test-Browser.ps1 -Configuration Release
./tools/Build-BrowserWasm.ps1 -RunAOTCompilation -OmitPrecompressedAssets
cd web/candu-playtest
npm ci
npx playwright install chromium
npm run build:pages
```

For normal local development, `npm run dev` serves the browser package at the root path using the staged runtime.

## Build the documentation

From the repository root, install the pinned [docs dependencies](../../tools/docs/requirements.txt) in a Python environment, then run the following after the frontend build. Set `CRS_DOCS_REF` to the staged source revision when checking release identity.

```powershell
python -m pip install -r tools/docs/requirements.txt
python -m unittest discover -s tools/docs -p "test_*.py"
python -m mkdocs build --strict --site-dir web/candu-playtest/dist-pages/docs
python tools/docs/verify_site.py --site-dir web/candu-playtest/dist-pages/docs
```

Pages combines the two builds after assembling the offline game asset list. This keeps documentation out of the game cache. The documentation hook publishes `docs/build-info.json` separately from `wasm/build-info.json`.

## Verify the Pages path

From `web/candu-playtest`, start the production preview.

```powershell
npm run preview -- --configLoader runner --outDir dist-pages --base /CRS/ --host 127.0.0.1 --port 4175
```

Run the acceptance checks in a second terminal.

```powershell
npm run smoke -- http://127.0.0.1:4175/CRS/
npm run smoke:docs -- http://127.0.0.1:4175/CRS/
npm run smoke:upgrade
npm run smoke:upgrade -- ../../tmp/custom-physics-upgrade --custom-physics
npm run benchmark -- http://127.0.0.1:4175/CRS/ --bridge=direct --label=pages-local --warm-samples=1
```

## Maintain the physics inputs

Canonical and embedded packs must match byte for byte. Use [Sync-PhysicsPacks.ps1](../../tools/Sync-PhysicsPacks.ps1) and preserve hashes, units, group ordering and provenance. Use `AgedCoreBenchmark` for calibration, `ChannelReferenceBenchmark` for ripple references, `LongRunPlaytest` for campaigns and `BrowserWireFixtures` to regenerate the shared wire fixture. Follow the [calibration records](../physics/calibration-provenance.md) for reproduction commands. External analysis tools support development and run offline.

The public launcher stores time-based update checkpoints in the browser. The retained daily views and their compatibility tests still use the account/save components and the checked-in [environment example](../../web/candu-playtest/.env.example).
