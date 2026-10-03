# GPU spatial prototype results

Baseline measured 2026-10-01 on branch `gpu-spatial-migration` with inner relative
tolerance 1e-8. The later 1e-7 policy rerun is recorded below. This completes the initial
operator/fixed-step experiment in the [migration plan](gpu-migration-plan.md).
The complete eigen solver and live GPU candidate acceptance are not implemented.
Gameplay continues to use the authoritative C# CPU solver.

## Environment and coverage

Release browser-WASM AOT build, profiling disabled, threads disabled; full
Chromium 151.0.7922.34. WebGPU reported NVIDIA / Ampere, nonfallback adapter.
The hardware run used `--enable-unsafe-webgpu --use-angle=d3d11`. A separate
full-Chromium run without these flags also selected that adapter and passed the
same comparisons. Adapter availability must still be checked on each browser.
The bundled headless-shell probes did not provide a usable device here.

Four successive states were tested: aged core, four-bundle refuel, one simulated
hour of changing poison/power, and a moderator cell with an end-a reflective
boundary. Both groups used 4,560 nodes, with 0, 3, 32 and 128 fixed Jacobi steps.
Each combination had a warm-up and three measured runs: 96 measured samples.
Every fixture experiment left its CPU session snapshot unchanged.

Kernel digest:
`540ecd04ef6ffc769ccee5a18cd48fcb5521da3e4be590d542193730d6d6178c`.
Build metadata records base commit `8966e9eea066d2b4d562bb94bf338b1c80e8c6a7`;
this measurement includes uncommitted branch changes and is not a clean-commit
release benchmark.

## Accuracy gate

All 96 samples passed the diagnostic normalized infinity-error budget of 2e-6.
Maximum operator error was 1.247e-7; maximum final-flux error was 1.504e-7
against the independent existing f64 CPU operator/reference.

The f32 residual stalled at approximately 5.93e-8 to 8.06e-8 by 32 steps and
did not improve at 128. The f64 reference residual was at most 1.63e-16 for
these fixed-source cases. The GPU diagnostic residual uses the rounded f32
source/operator; even that failed the then-current 1e-8 inner-solve tolerance.
It is not an authoritative CPU verification of a complete candidate.

Those baseline results did not qualify f32-only gameplay. The subsequent
user-authorized tolerance change clears the measured fixed-source residual gate,
but complete candidate verification remains necessary before integrating GPU
coupled fission, normalization and eigenvalue iteration. GPU prediction with f64
CPU correction and compensated arithmetic remain options if full-solver or
cross-device checks fail.

## Timing gate

Hardware-run mean times in milliseconds, 24 samples per row:

| Fixed steps | CPU iteration loop | GPU upload + execution/readback + result copy | GPU submission/readback | GPU worker round trip |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 0.00 | 5.99 | 3.02 | 8.00 |
| 3 | 0.27 | 6.29 | 3.48 | 8.42 |
| 32 | 2.94 | 6.26 | 3.50 | 8.42 |
| 128 | 12.07 | 9.05 | 6.24 | 11.11 |

The CPU column excludes the initial operator application and fixture packing.
GPU timings include the initial operator application and reuse compiled
pipelines. The first pipeline compilation cost about 151.5 ms; it is excluded
from the warm timing table. GPU total median/p95 over all measured step counts
was 6.3/11.2 ms. The default-launch run measured 6.6/14.0 ms.

Diagnostic JSON fixture export took a mean 33-45 ms depending on step count,
including CPU reference construction. That cost is separate from GPU worker
round trip and must not be hidden in any end-to-end claim. This transport is
unsuitable for live solves; resident coefficient buffers and binary requests
are planned.

Short solves are slower on the GPU in this prototype. At 128 steps the isolated
iteration timing advantage is modest, and those extra steps cannot remove the
f32 precision floor. These measurements do not establish a speedup over the
approximately 710 ms scheduled-solve p95 reported in the CPU profile. Full
solver/controller benchmarks remain a rollout gate.

## Validation and reproduction

Core suite: 84 passed. Browser bridge: 23 passed. Frontend: 81 passed. GPU
executor unit tests: 4 passed. Release AOT publish, production frontend build,
and local production browser smoke passed. No browser page errors occurred in
either successful GPU run. No deployment was made.

After an AOT build and frontend build, run from `web/candu-playtest`:

```powershell
npm run test:gpu
npm run benchmark:gpu -- http://127.0.0.1:4174 --hardware
npm run benchmark:gpu -- http://127.0.0.1:4174
```

Local raw results: `tmp/gpu-hardware-result-final.json` and
`tmp/gpu-default-result-final.json`. The harness emits build/adapter identities,
per-sample timing/error/residual data and separate convergence/diagnostic gates.

## Authorized one-decade tolerance change and rerun

Later on 2026-10-01, the user authorized changing the inner relative residual
tolerance from 1e-8 to 1e-7. Both the authored and embedded packs now carry that
policy and the version `candu6-two-group-diffusion-v1-cycle190-650mwe-innerrel1e7`.
Absolute residual, outer eigenvalue/source-shape/residual limits, iteration
bounds and power-balance limits are unchanged. The changed pack digest/version
identifies the numerical-policy change; old/new exact state digests need not match.

The rebuilt AOT runtime passed the same 96 GPU comparisons on NVIDIA Ampere,
both with the hardware flags and with default full Chromium. All 24 measured
32-step cases and all 24 measured 128-step cases passed the new residual gate.
Residuals ranged from 5.51e-8 to 6.95e-8. Maximum operator/flux errors against the
new f64 reference were 1.164e-7 / 1.728e-7, within the separate 2e-6 diagnostic
agreement budget. All four CPU session snapshots remained unchanged by GPU
experiments. This remains a rounded-source fixed-group diagnostic, not a complete
GPU candidate acceptance check. Gameplay is still CPU-driven.

The hardware-flag run overlapped native checks and is used only for accuracy.
The later default-launch GPU run had no tests or builds running: GPU total
median/p95 was 6.7/14.1 ms. The same isolated production CPU worker workload used
in the earlier runtime profile measured:

| CPU worker metric | Former 1e-8 policy | Current 1e-7 policy |
| --- | ---: | ---: |
| One-second WASM median / p95 | 634.9 / 710.9 ms | 611.9 / 675.7 ms |
| One-second worker round-trip median / p95 | 651.5 / 727.4 ms | 626.1 / 691.7 ms |
| 100 ms UI tick round-trip median / p95 | 8.6 / 710.2 ms | 9.0 / 676.1 ms |

This single before/after observation suggests a modest improvement; it does not
establish statistical significance. Scheduled stalls still exceed the 250 ms
target. Future complete-GPU measurements must compare against this relaxed CPU
policy on the same machine, rather than attributing policy gains to GPU execution.

Validation passed: 86 Core tests, 31 Game tests, 23 browser bridge tests, 81
frontend tests, Release AOT publish, frontend production build and browser smoke.
Two new full-core regression cases at zone fills 0.5 and 0.8 compare the new
policy with 1e-8 using identical material data: reactivity difference <= 0.05 mk,
maximum normalized node-power difference <= 1e-4, and unchanged power-balance
acceptance. No deployment was made.

Raw reruns: `tmp/gpu-tolerance-accuracy.json` (hardware accuracy),
`tmp/gpu-tolerance-result.json` (isolated default-browser GPU), and
`tmp/gpu-tolerance-worker.json` (isolated production CPU worker).
