# GPU retest with relaxed game criteria — 2026-10-01

Keep CPU gameplay as the default. The repeated GPU experiment agrees with the
CPU under the current game criteria, but is slower for warm solves and fails
independent spatial convergence checks on the refuel state. No deployment was made.

## Policy and scope

Both CPU and GPU use the shared pack's inner relative residual tolerance of
1e-7 and outer equation residual limit of 2e-5. The controller uses ±0.05 mk
and ±1 percentage point of total-core regional power. GPU comparison now reads
these same agreement limits from `PracticeLiquidZoneRrsIdentityV1`, replacing
the former regional agreement budget of 0.05 percentage points. The reactivity
budget was already 0.05 mk. Flux/group/node-power agreement remains 1e-4.

The controller bands are different quantities from equation residual and
source-shape convergence. They do not replace those numerical policies.
Looser controller criteria reduce regulation work; they do not directly reduce
the number of iterations inside an individual GPU spatial solve.

This is a read-only numerical experiment, not a live GPU controller benchmark.
It tests four successive CPU-authoritative states: aged, channel-210 four-bundle
refuel, one simulated hour of poison/power evolution, and moderator/reflective
geometry. Each uses both perturbed warm and uniform cold starts, twice.
Warm starts perturb accepted flux by alternating ±2% and k by +1%; they are
not captured live controller candidate inputs. All live snapshots remained
unchanged by GPU experiments.

## Numerical outcome

Two isolated hardware runs each produced the same **12/16 independently
converged** samples and **16/16 agreement passes**. GPU provisional convergence
was reported in every sample. Both repeats of the refuel warm and cold starts
failed the f64 equation-residual check:

- Warm: GPU residual 1.99851747e-5; f64 residual 2.00050743e-5.
- Cold: GPU residual 1.99618135e-5; f64 residual 2.00299316e-5.

The worst excess is about 0.15% of the 2e-5 limit. Rounded GPU coefficients
and f32 evaluation can therefore report convergence slightly before the f64
equations pass. The broader game agreement criteria do not remove this failure.
The earlier GPU matrix passed on different, tightly regulated states; that
result did not establish a margin for these new controller trajectories.

Maximum errors in the rebuilt run:

| Quantity | Error | Agreement limit |
| --- | ---: | ---: |
| Reactivity | 0.000259 mk | 0.05 mk |
| Regional power share | 0.0005995 percentage points | 1 percentage point |
| Normalized node power | 5.212e-5 | 1e-4 |

## Timing

Rebuilt run, second repeat with cached pipelines; milliseconds. CPU covers
the full f64 solve with identical coefficients/start/policy. GPU includes
upload, execution, final readback, worker response and f64 verification.
Diagnostic fixture export/CPU reference construction is excluded from GPU
timings; it adds roughly 110–132 ms warm and 1049–1079 ms cold if used directly.
These are not complete gameplay command latencies.

| State | CPU warm | GPU warm + verification | CPU cold | GPU cold + verification |
| --- | ---: | ---: | ---: | ---: |
| Aged | 68.4 | 97.9 | 1020.2 | 601.6 |
| Refuel, verification failed | 59.5 | 81.0 | 1021.8 | 597.4 |
| Poison/power | 59.4 | 85.8 | 996.0 | 604.5 |
| Moderator/reflective | 65.3 | 110.3 | 1001.5 | 598.5 |

Warm GPU results are 1.36–1.69× the CPU time. Cold GPU results take about
59–61% of CPU time, but cold starts are uncommon because gameplay retains
the previous state. GPU execution alone was 41–72 ms warm; transport and
verification erase most of the isolated kernel advantage.

Do not proceed to a full 72-hour GPU gameplay campaign on this result. The
next useful experiment would add a conservative GPU stopping margin, then
reduce resident-buffer/transfer/verification overhead and shadow actual RRS
candidates. Preserve the independent f64 acceptance check and bounded CPU
fallback rather than accepting the provisional GPU flag.

## Environment and validation

Release AOT browser-WASM, serial CPU, profiling/threads disabled, Chromium
151.0.7922.34, default flags, NVIDIA Ampere nonfallback adapter. Embedded kernel
digest `79f50dd8a0855043317a1ce7f02c002f435cdd81d8ef4a2f7d34f3a3be33df8c`.
No tests/builds overlapped measured runs. Build metadata identifies base HEAD
`8966e9eea066d2b4d562bb94bf338b1c80e8c6a7`; sources contain uncommitted changes.

105 Core tests, 26 Browser tests, 81 frontend tests and five GPU executor tests
passed. AOT publish, frontend production build and browser preview verification
passed. The benchmark deliberately exits nonzero for failed independent
convergence; both JSON reports were retained.

Raw results: `artifacts/gpu-relaxed-criteria-retest-2026-10-01/coupled-run-1.json`
(current relaxed-controller build, former tighter regional agreement budget),
and `coupled-run-2.json` (rebuilt shared agreement limits, explicit policy metadata).
Logs: `tmp/gpu-retest-core-tests.log`, `tmp/gpu-retest-browser-tests.log`,
`tmp/gpu-retest-aot-build.log`. Production browser smoke is recorded in the
same artifact directory.

Reproduce after AOT publishing and building the frontend:

```powershell
npm run test:gpu --prefix web/candu-playtest
# From web/candu-playtest, with the production preview running:
node scripts/benchmark-gpu.mjs http://127.0.0.1:4186 --coupled
```
