# Candu Refuelling Simulator (CRS)

A Stylized Simulator demo made by ChatGPT and Gemini.

[Play CRS](https://hkkho.github.io/CRS/), a CANDU on-power refuelling game. Keep a seeded reactor within its operating limits while replacing fuel and building score.

- Choose **Start game**, then **Start**. Each simulated half-day takes 7.5 seconds when the next solve is ready. Choose **0.5×** for 10 seconds or **1.5×** for 5 seconds. Press the selected speed again to return to normal.
- Choose **Tutorial** for a guided tour of the readings and limits, then compare two fuel channels on seed 1001.
- Double-click a channel, select it and press **Enter** or **R**, or use **Queue fuel**. Each order moves eight bundles. The next unsolved step takes the waiting orders together.
- Use the map selector and liquid-zone trends to decide where to refuel. **Pause** freezes playback. **Retry seed** recreates the starting core and **New core** draws another seed.
- Choose **Custom physics** to build a sandbox with edited power, absorption, poison, geometry, fuel coefficients or solver settings. The settings follow the run through retries and local update checkpoints.

See the [player manual](docs/code-library/manual/index.md) for controls, scoring, maps and recovery. Report bugs and request features in [GitHub Issues](https://github.com/hkkho/CRS/issues).

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

The [v11 xenon calibration](docs/physics/xenon-calibration.md) and [v10 geometry fit](data/calibration/diffusion-v10/README.md) record the active model. The physics are authored gameplay approximations. Shutdown, scram, accidents, operator training and full plant operations are outside the game’s scope.
