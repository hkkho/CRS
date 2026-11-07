# CANDU web game implementation guide

Develop `web/candu-playtest` and verify it on the GitHub Pages path. The public launcher offers time-based free practice and Custom physics. Shutdown, scram, accidents, operator training and full plant operations are outside scope.

## Architecture and ownership

| Layer | Responsibility |
| --- | --- |
| `web/candu-playtest` | Launcher, DOM/SVG presentation, input, replay journal and serialized worker transport |
| `ReactorSim.BrowserHost` | Single-threaded .NET browser-WASM host and JavaScript exports |
| `ReactorSim.Browser` | Versioned wire protocol, instance runtime and full/compact snapshot mapping |
| `ReactorSim.Game` | Clock, transactions, scoring, terminal decisions and immutable presentation snapshots |
| `ReactorSim.Core` | Topology, fuel inventory, burnup, poison, diffusion and liquid-zone regulation |

The client fails closed without WASM. Keep reactor rules in the shared simulation and preserve SI units, fast/thermal group order, channel-major indexing and deterministic digests. Use the [wire protocol](spec/browser-playtest-protocol-v2.md) and [code walkthrough](code-library/index.md) when changing these boundaries.

## Time-based play

The public client initializes `pacingMode: demo`. `DemoLoop` submits the waiting fuel batch through `commit-day`. Game validates the batch, sorts channels, and prepares each eight-bundle move with channel flow. It integrates a single 43,200-second interval from the surviving post-refuelling power and flux, then solves the next equilibrium state. The browser interpolates solved endpoints over five seconds. Slow solves hold the last endpoint. Pause and hidden tabs freeze playback and preserve waiting orders.

Numerical failure discards the detached candidate. A physically violating fuel move commits and skips later orders. A violation at the interval endpoint commits the elapsed time and stops play. See [shared step semantics](gameplay/daily-turn-mode.md) and [limits and scoring](gameplay/score-balance.md). Daily turns and real-time commands remain available for compatibility tests and comparison tools.

## Physics and custom runs

The [v10 calibration](../data/calibration/diffusion-v10/README.md) uses circular radial leakage and axial zero-flux extrapolation 0.22 m beyond each end. [Core's solver](physics/active-two-group-solver.md) binds fuel coefficients by burnup and carries poison with bundle identity. Startup solves the aged inventory against a [time-average reference](physics/channel-power-reference.md) derived with zones at half fill, then settles xenon and regulation without spending fuel or time.

Custom physics applies numeric overrides to the embedded pack and per-pack practice settings before startup. The shared loader validates the parameters and Game recomputes the reference and initial equilibrium. Custom runs have modified sandbox provenance and a content-based pack ID. They retain the settings across seed resets and local update checkpoints.

## Presentation and lifecycle

Render accepted readings, including terminal reasons and diagnostics, without inventing intermediate physical states. Preserve keyboard access, 320px layouts, reduced motion and focus on recovery. Dispose worker, listeners and timers when leaving a run.

Update checkpoints store the latest solved endpoint as replayable inputs plus waiting orders. IndexedDB holds the journal and session storage holds its pointer and custom settings. Restore verifies pack, scoring policy, cadence, command acceptance and the final state digest before enabling Start. Failed writes prevent reload. Incompatible checkpoints and older daily saves remain stored.

## Verification

Use `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1` in Release. Follow the [Pages verification guide](maintenance/hosting.md) for gameplay, custom physics, update restore, documentation and direct WASM reproduction.
