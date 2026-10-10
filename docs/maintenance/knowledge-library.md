# Physics and game knowledge library

Keep equations, calibration sources and raw measurements that help refine the web
game. Old-pack reports are historical comparisons, not current acceptance values.

| Topic | Useful source |
| --- | --- |
| Ownership and loop | [Implementation](../IMPLEMENTATION_GUIDE.md), [architecture](../architecture.md), [daily turns](../gameplay/daily-turn-mode.md) |
| Diffusion equations | [Active solver](../physics/active-two-group-solver.md) |
| Axial ends and calibration | [Boundary study](../physics/axial-boundary-review.md), [Marshak v8](../physics/axial-marshak-v8.md), [v8 audit/campaign](../../benchmarks/axial-marshak-v8-2026-10-10/README.md) |
| Fuel age and energy | [Aged starts](../physics/aged-core-starts.md), [fuel calibration](../physics/power-and-fuel-calibration.md), [literature geometry](../physics/literature-geometry-v4.md) |
| Device absorption | [Adjusters](../physics/adjusters-v5.md), [LZC tubes](../physics/lzc-tubes-v7.md), [local absorption](../physics/local-absorber-power.md) |
| Bundle iodine/xenon | [Gameplay model](../physics/iodine-xenon-gameplay.md), [reference correction](../physics/xenon-reference-v6.md) |
| Power and scoring | [Reference](../physics/channel-power-reference.md), [limits](../gameplay/power-limits.md), [score](../gameplay/score-balance.md) |
| Performance | [Browser measurements](../performance/browser-phase3.md), [daily turns](../performance/daily-turns.md), [profiling](../physics/runtime-profile.md), [native CPU rows](../physics/cpu-parallel-experiment.md) |
| Literature | [Source manifest](../../reference/manifests/candu-literature-sources-v1.json), [Physics guide](../physics/candu-nuclear-diffusion-student-guide.md) |
| Release | [Tools](research-tools.md), [hosting](hosting.md), [accounts/saves](player-accounts.md) |

Retain raw reports cited by notes; summaries cannot replace numerical samples or
regression fixtures. Promote useful records into named benchmark/calibration paths.
Generated publish trees, logs, screenshots and local uploads stay ignored. See the
[cleanup record](web-only-cleanup-manifest.md) for retired components. Git history
preserves obsolete sources; current builds do not reference them.
