# Complete coupled GPU solver validation — 2026-10-01

The later [relaxed-criteria retest](gpu-relaxed-criteria-retest.md) checks the
current controller trajectories and supersedes this matrix for readiness:
warm GPU solves remain slower, and refuel states narrowly fail independent
spatial residual verification despite meeting the new agreement criteria.

The complete two-group GPU experiment passes numerical validation on NVIDIA
Ampere. It does **not** qualify for switching gameplay to this implementation:
the diagnostic transfer/verification path is slower than the CPU warm solves,
and the planned live controller/transaction/replay integration is still pending.
Gameplay remains on the CPU. No deployment or commit was made.

## Implemented numerical path

Core owns `coupled-spatial-v1.wgsl`: both Jacobi inner solves, frozen fission
source, group-1-to-2 downscatter, production/power reductions, eigenvalue updates,
power normalization, source-shape changes and coupled equation residuals.
Reductions use a fixed hierarchical tree. Flux remains GPU-resident throughout
each solve; the host reads a small convergence summary every eight outer steps
and reads flux only after provisional convergence. Limits and tolerances come
from the shared pack, including inner relative residual 1e-7.

BrowserHost owns buffer allocation, pipeline caching, dispatch, bounded waits,
device/error checks and cleanup. No physics equations were added to TypeScript.
The developer export is read-only and rejects an experiment after reset or a
change to its accepted coefficient binding. It cannot commit a GPU candidate.

C# independently evaluates the final and preceding states with the **existing
f64 operator and convergence evaluator**. It applies one uniform f64 power
normalization to each returned state before checking the original 1e-12 power
balance policy; this repairs representation roundoff without iterating or
changing the shape. Per-group flux, node power, reactivity and regional power
fractions are compared with an independently converged f64 solve.

The first plain-f32 coupled cold starts stalled near 1.02e-7 despite passing
the earlier fixed-source experiment. Compensated leakage summation and fused
multiply-add cleared those failures at the same 1e-7 policy. An indirect-dispatch
experiment was measured and removed: it was substantially slower and failed f64
verification in two cases. The final version uses compensated direct dispatch.

## Final release-shaped numerical matrix

Release browser-WASM AOT, profiling/threads disabled, Chromium 151.0.7922.34,
default browser flags, NVIDIA Ampere nonfallback adapter. No builds or tests ran
during the final measurement. The kernel came from the embedded Core resource;
`developmentKernelOverride` was false.

Kernel digest:
`79f50dd8a0855043317a1ce7f02c002f435cdd81d8ef4a2f7d34f3a3be33df8c`.
Build metadata reports base commit `8966e9eea066d2b4d562bb94bf338b1c80e8c6a7`;
these are uncommitted branch changes, not a clean-commit release benchmark.

Four successive states were tested: aged core, channel-210 refuel, one simulated
hour of poison/power evolution, and a moderator cell with a reflective end-a
boundary. Each state used a perturbed warm start and a cold uniform start, with
two repeats: **16/16 solves converged and passed f64 verification and comparison
budgets**. Warm starts perturb the already accepted state by alternating 2% flux
changes and 1% eigenvalue change. They are not captured live RRS candidate inputs.
Cold starts exercise solving the same effective coefficients from uniform flux.
All four CPU session snapshots remained unchanged by GPU experiments.

| Maximum measured error | Result | Acceptance budget |
| --- | ---: | ---: |
| Group-1 normalized flux | 5.224e-5 | 1e-4 |
| Group-2 normalized flux | 5.213e-5 | 1e-4 |
| Normalized node power | 5.213e-5 | 1e-4 |
| Regional power fraction, fourteen regions | 5.995e-6 | 5e-4 |
| Reactivity difference | 0.000181 mk | 0.05 mk |
| Coupled equation residual, f64 | 1.997e-5 | 2e-5 |
| Source-shape change, f64 | 4.281e-8 | 1e-5 |
| Power balance after f64 normalization | 5.661e-15 | 1e-12 |

A separate aged warm-start correctness probe on Google SwiftShader (reported as
a fallback adapter) also passed the independent checks. That is one software
probe, not a second complete hardware/device matrix; it took about 736 ms in GPU
execution and is unsuitable as an acceleration backend.

## Performance gate: not passed

Times below use the second repeat with cached pipelines. CPU timings cover the
full f64 numerical solve on the identical coefficients/start/policy. GPU timings
include execution, final result readback/worker response and f64 verification;
they exclude diagnostic fixture export/reference construction. All values are ms.

| State | CPU warm solve | GPU warm + verification | CPU cold solve | GPU cold + verification |
| --- | ---: | ---: | ---: | ---: |
| Aged | 69.9 | 161.6 | 1001.6 | 575.8 |
| Refuel | 71.6 | 91.0 | 992.5 | 571.1 |
| Poison/power change | 60.3 | 78.7 | 989.4 | 573.6 |
| Moderator/reflective | 64.9 | 156.5 | 992.0 | 559.5 |

The isolated warm GPU execution ranged from roughly 40 to 177 ms across both
repeats; the CPU warm solve ranged from 60 to 72 ms. Cached pipeline lookup,
upload/readback and validation therefore cannot be ignored when reporting a
speedup. The first pipeline compilation was a separate startup cost. The harness
also exports JSON fixtures and constructs a CPU reference, both diagnostic costs
that are unsuitable for live operation and excluded from the table.

Cold solves improve, but not by the planned 2x after result validation. Warm
results are slower through this path. These are small numerical benchmarks,
not complete RRS worker commands: the scheduled-solve p95 below 250 ms and 2x
full-command improvement have **not** been demonstrated. Do not infer a full-game
latency from this matrix or enable the live backend on numerical agreement alone.

## Remaining work before gameplay switches

1. Replace the diagnostic JSON/reference path with resident coefficient/state
   buffers and a binary numerical request/result contract. Reduce dispatch and
   verification overhead, then repeat identical CPU/GPU comparisons.
2. Introduce immutable live candidate requests and asynchronous acceptance with
   inventory/poison/layout/generation bindings, bounded CPU fallback and stale
   result rejection. The current developer experiment is not that transaction seam.
3. Shadow actual RRS candidates and commands; compare controller branches, zone
   fills, scoring, refuels, Designer edits, reset/cancel, and fallback behavior.
4. Define backend/precision/replay identities and validate representative adapters.
   Qualify the full worker timing gates before opt-in rollout and Vercel smoke.

## Checks and reproduction

88 Core tests, 24 browser bridge tests, 81 frontend tests and five GPU executor
tests passed. Release AOT publish and production frontend build passed. Final
local browser smoke exercises the unchanged CPU-authoritative playable loop.

After AOT publishing and building the frontend, run from `web/candu-playtest`:

```powershell
npm run test:gpu
npm run benchmark:gpu -- http://127.0.0.1:4174 --coupled
npm run benchmark:gpu -- http://127.0.0.1:4174 --coupled --probe --software
```

The complete benchmark exits nonzero for either failed f64 convergence or failed
comparison budgets. Unsupported adapters are reported explicitly. For iterative
shader development only, `--development-kernel` reads the Core working-tree WGSL
instead of the embedded resource and marks the report as overridden; such a run
does not replace the final embedded-kernel validation.

Raw final matrix: `tmp/gpu-coupled-production.json`. Software probe:
`tmp/gpu-coupled-software-probe.json`. Native/browser/build/smoke logs are under
`tmp/gpu-coupled-*`. Prior failed arithmetic and dispatch experiments were retained
locally to distinguish measured failure modes from the final implementation.
