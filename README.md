# CRS

[Play CRS](https://hkkho.github.io/CRS/), a CANDU on-power refuelling game. Keep a seeded reactor within its operating limits while replacing fuel and building score.

- Choose **Time based**, then **Start**. The game displays each simulated half-day over five seconds when the next solve is ready.
- Double-click a channel, select it and press **Enter** or **R**, or use **Queue fuel**. Each order moves eight bundles. The next unsolved step takes the waiting orders together.
- Use the map selector and liquid-zone trends to decide where to refuel. **Pause** freezes playback. **Retry seed** recreates the starting core and **New core** draws another seed.
- Choose **Custom physics** to build a sandbox with edited power, absorption, poison, geometry, fuel coefficients or solver settings. The settings follow the run through retries and local update checkpoints.

See the [player manual](docs/code-library/manual/index.md) for controls, scoring, maps and recovery.

## Develop and verify

The browser presents observations from one authoritative C# simulation running in a Web Worker. The [implementation guide](docs/IMPLEMENTATION_GUIDE.md) explains ownership, transaction rules and model compatibility. The [code library](docs/code-library/index.md) traces the current execution path, and the [roadmap](docs/WEB_ROADMAP.md) records remaining work.

From the repository root, stage the browser-WASM runtime and start Vite.

```powershell
./tools/Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm run dev
```

Run `./tools/Test-DotNet.ps1 -Configuration Release` and `./tools/Test-Browser.ps1 -Configuration Release` from the root. See [release and reproduction](docs/maintenance/hosting.md) for the Pages build, docs preview and smoke checks.

The [v10 calibration](data/calibration/diffusion-v10/README.md) records the active model fit. The physics are authored gameplay approximations. Shutdown, scram, accidents, operator training and full plant operations are outside the game’s scope.
