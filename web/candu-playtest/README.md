# CANDU browser playtest

Milestone 0.5 is a static browser companion for testing the refuelling loop and
collecting dynamic feedback before the Unity presentation is refined. The
authoritative path is the engine-neutral C# model compiled to browser WASM and
called from a dedicated Web Worker. The UI fails closed with
`Authoritative WASM bridge unavailable` and disables simulation controls if the
bridge cannot load.

## Local run

```text
npm install
npm run dev
```

The static client can be built with `npm run build` and previewed with
`npm run preview`.

To stage the authoritative .NET bridge locally, run this from the repository
root:

```powershell
.\tools\Build-BrowserWasm.ps1
```

The script publishes `src/ReactorSim.BrowserHost` for `browser-wasm` and copies
its `wwwroot` output into `public/wasm`. Generated WASM output is intentionally
ignored; deployments should run the staging script before the Vercel build.
The staging script also writes `public/wasm/build-info.json` with the source
commit, Release configuration, target framework, and runtime identifier.

## Production deployment

`.github/workflows/deploy-candu-playtest.yml` is the production path. On a push
to `main` or `master`, or from `workflow_dispatch`, it:

1. installs the pinned .NET 10 SDK and `wasm-tools` workload;
2. publishes `src/ReactorSim.BrowserHost/ReactorSim.BrowserHost.csproj` in
   Release mode and stages the complete output under `public/wasm`;
3. verifies `main.mjs`, a `.wasm` payload, and build provenance;
4. runs `npm ci`, frontend tests, and the Vite production build;
5. links and pulls the existing Vercel project, runs `vercel build --prod`, and
   checks that the bridge assets survived into `.vercel/output`;
6. deploys the prebuilt output with `vercel deploy --prebuilt --prod
   --archive=tgz`; and
7. runs a Chromium smoke test against the deployment.

Configure these GitHub Actions secrets on the `hkkho/CRS` repository before
running the workflow:

- `VERCEL_TOKEN`
- `VERCEL_ORG_ID`
- `VERCEL_PROJECT_ID`

The workflow uses the Vercel CLI and never uses the inline-files deployment
connector. The Vercel project is linked from `web/candu-playtest`, so the
frontend remains a static Vite deployment with no backend or authentication.

For local staging, run `./tools/Build-BrowserWasm.ps1` from the repository root,
then `npm ci`, `npm test`, and `npm run build` from `web/candu-playtest`.

## Playtest contract

The JSON boundary is `candu-playtest-v1`. Play mode uses the existing
`PracticeGameSessionFactory`/`GameSession` path, including the full 380-channel
by 12-bundle presentation snapshot. Lab mode exposes the explicit 2-channel by
8-bundle synthetic fixture and the real `SpatialEigenSolve` coupling for solver
experiments. Invalid and non-converged Lab transitions fail without mutating
the accepted state.

The Play snapshot carries the shared full-core two-group physics contract in
explicit SI units: reference/target/total/channel/bundle watts, dimensionless
amplitude, state-level `k`, `rho = (k - 1) / k`, and solver diagnostics. The
authoritative path assembles the 380 × 12 CANDU-6 stencil and binds each bundle
to the embedded versioned pack. Bundle power sums are normalized to the
requested amplitude at the static solve, then exposed as actual fission power
after the relative criticality response; the setpoint and actual value remain
separate. Refuelling re-solves the candidate inventory, and burnup advances
from retained actual bundle watts. The current pack is project-authored
`synthetic-calibrated` data, not a CANDU plant rating or an external
DRAGON/DONJON result; the next pass can replace it with an offline admitted
export without changing the browser contract.

Commands, state digests, replay JSON, and feedback notes stay in the browser.
There is no backend, login, telemetry, or external reactor data in this pivot.
