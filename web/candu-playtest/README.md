# CANDU browser playtest

Milestone 0.5 is a static browser companion for testing the refuelling loop and
collecting dynamic feedback before the Unity presentation is refined. The
authoritative path is the engine-neutral C# model compiled to browser WASM and
called from a dedicated Web Worker. If the generated WASM files are absent, the
UI labels itself as `SYNTHETIC FIXTURE`; that fixture exists only to keep the
interaction surface usable during frontend work and is not a second source of
reactor truth.

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

## Playtest contract

The JSON boundary is `candu-playtest-v1`. Play mode uses the existing
`PracticeGameSessionFactory`/`GameSession` path, including the full 380-channel
by 12-bundle presentation snapshot. Lab mode exposes the explicit 2-channel by
8-bundle synthetic fixture and the real `SpatialEigenSolve` coupling for solver
experiments. Invalid and non-converged Lab transitions fail without mutating
the accepted state.

Commands, state digests, replay JSON, and feedback notes stay in the browser.
There is no backend, login, telemetry, or external reactor data in this pivot.
