# Browser worker calculation profile — 2026-10-01

The slow path is the spatial equilibrium/RRS event, not iodine/xenon
integration. No isotope approximation or solver-fidelity change was made.

## Measurement method

Release AOT browser-WASM, SIMD default, threads disabled, local Chromium and
production preview at `http://127.0.0.1:4174`. The same workload was replayed in
three modes: detailed timing enabled, timing disabled in that same diagnostic
binary, and a standard binary with timing scopes compiled out. Each run refuels
channel 210, requests 95% power, then executes twenty one-second advances,
fifty 100 ms ticks, and two ticks each at 10x/60x (19.5 simulated hours).
No tests or AOT builds ran during the latency measurements. Final state, replay,
poison digests and simulation time match across all three runs. All commands
were accepted and no browser errors were reported.

The developer export `DispatchProfileJson` returns the ordinary response string
plus timing rows. Measurements never enter simulation state, replay digests,
time advancement, solver convergence decisions or the product UI. Each row has
call count, inclusive duration and exclusive duration. Inclusive rows contain
their children and must not be added together. Exclusive rows can be added.
The scope clock excludes scope construction, so unassigned diagnostic overhead
can appear in parent-exclusive time.

## Production latency versus diagnostic overhead

| Build / mode | One-second WASM median / p95 | 100 ms tick round-trip median / p95 |
|---|---:|---:|
| Diagnostic binary, timings enabled | 995.3 / 1112.5 ms | 8.7 / 1118.0 ms |
| Diagnostic binary, timings disabled | 975.6 / 1114.0 ms | 8.6 / 1109.0 ms |
| Standard binary, scopes compiled out | **634.9 / 710.9 ms** | **8.6 / 710.2 ms** |

One-second standard worker round trips are 651.5 ms median / 727.4 ms p95.
This reproduces the previous production benchmark closely. Standard 10x ticks
take 677.7/667.5 ms in WASM; 60x ticks take 3664.6/3595.0 ms. The single refuel
sample takes 1356.5 ms in WASM; it is not a refuelling percentile estimate.

Enabling the clocks has a small incremental effect in this run, but putting
timing scopes/`try/finally` into the hot functions changes AOT optimization
substantially. The diagnostic binary is approximately 1.57x slower at the
one-second WASM p95 even with clocks disabled. It must not be used to report
production latency or promise an optimization's speedup. Detailed category
durations and shares below describe the instrumented binary and are indicative
of where to investigate, not calibrated production component durations.
Standard builds now compile all simulation timing scopes out.

## Routine tick profile

Forty-five of the fifty 100 ms ticks have no scheduled spatial solve. Mean
instrumented WASM call time is 8.85 ms. Values below are means per request;
routine tick timings are close to the standard binary's observed latency.

| Work | Exclusive time | Share of measured dispatch |
|---|---:|---:|
| Game presentation snapshot | 6.00 ms | 69.0% |
| Compact state digest (excluding nested serialization) | 0.75 ms | 8.7% |
| Burnup update | 0.57 ms | 6.5% |
| All 4,560 iodine/xenon pairs | **0.55 ms** | **6.3%** |
| JSON serialization, two calls | 0.50 ms | 5.8% |

Eliminating isotope integration altogether would save only approximately
0.55 ms per routine tick in this diagnostic measurement. A linear approximation
would save less, incur timestep-dependent error, and leave the solve stalls.

## Scheduled solve profile

Five ticks trigger the 1,800-simulation-second spatial boundary. Each RRS event
performs **four full-core candidates**: uncompensated baseline, controlled
baseline, verification and correction. The controller's small bounded fill
command solve runs twice and takes only 0.20 ms in total.

| Work | Mean inclusive time per solve tick | Share of measured dispatch |
|---|---:|---:|
| Complete RRS event | 1011.36 ms | **94.4%** |
| Four eigen solves within that event | 873.12 ms | **81.5%** |
| Spatial operator applications within the eigen solves | 479.84 ms | 44.8% |
| Iodine/xenon integration | 0.52 ms | 0.05% |

These are nested rows. The operator is already included in the eigen and RRS
rows. The remaining eigen-solve exclusive work is 244.14 ms, and inner
linear-solve work excluding operator applications is 149.14 ms. JSON
serialization totals 41.60 ms when the compact response includes a changed
core. The event averages 306.8 two-group linear solves and 2593.4 spatial
operator applications. Four diffusion-result construction passes add 52.52 ms
exclusive; other coefficient, overlay and candidate assembly costs are smaller.

The refuelling sample also runs four candidates, with 736 linear solves and
5997 operator applications. At 60x, each 100 ms request runs **six RRS events**
(24 full-core candidates). This explains why faster playback becomes
compute-limited even though isotope updates remain inexpensive.

## Ranked experiments

1. **Warm-start the controlled baseline from the previous accepted controlled
   shape.** Currently it starts from the newly computed uncompensated shape:
   the event first removes zone absorption, then adds it back. The accepted
   controlled shape may be much closer to the next controlled solution when
   burnup and poison have changed only slightly. Keep the uncompensated
   diagnostic solve and all existing convergence/acceptance checks. Measure
   per-candidate iteration counts and p95 latency before/after; compare power
   shape, reactivity, zone fills, controller branches and deterministic replay.
   This is the first, relatively small experiment to try.
2. **Optimize the spatial operator and repeated iteration work.** Inspect
   compiled node/neighbor layout, cache invariant group removal coefficients,
   and reduce repeated result/metric allocation. Preserve neighbor order,
   arithmetic ordering where possible, dynamic finite/overflow checks and
   failure behavior. Use a separate microbenchmark without scopes in the hot
   kernel to measure improvements. If memory/kernel improvements are not
   sufficient, evaluate a faster deterministic preconditioned inner solver
   against the existing reference; preserve convergence and nonnegative
   accepted-flux requirements. No speedup is established yet.
3. **Reduce routine presentation work.** Avoid materializing all detailed
   bundle presentation objects for compact advance responses. Cache unchanged
   topology and accepted-shape metadata; build current detailed fuel/poison
   observations on demand. Keep snapshots immutable and current, and check
   channel selection, history, refuelling and Designer transitions. This
   targets routine tick cost, not the major full-solve stall.
4. **Cache exact isotope coefficients if still useful afterward.** A frozen
   flux/source and repeated simulation timestep allow exponential factors to
   be reused. Invalidate on projection, amplitude or timestep changes. This
   preserves the analytic model and is preferable to replacing it with Euler
   solely for performance. Its measured opportunity is small.

Changing solve cadence, convergence tolerances or reducing the spatial model
would alter the gameplay approximation and needs a separate accuracy/error
study. They are not the first response to this profile. Real-time pacing also
needs attention separately: the live clock still drops elapsed wall time during
slow calls. Making it catch up without reducing solve cost could create a
backlog rather than improve responsiveness.

## Reproduction and checks

Build the diagnostic host explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1 -RunAOTCompilation -EnableRuntimeProfiling -OmitPrecompressedAssets
cd web/candu-playtest
npm run build
npm run preview -- --host 127.0.0.1 --port 4174 --strictPort
```

In another terminal, from `web/candu-playtest`:

```powershell
node scripts/benchmark-xenon.mjs http://127.0.0.1:4174 20 --profile > ../../tmp/worker-profile-aot.json
node scripts/benchmark-xenon.mjs http://127.0.0.1:4174 20 > ../../tmp/worker-unprofiled-aot.json
node scripts/summarize-profile.mjs ../../tmp/worker-profile-aot.json ../../tmp/worker-unprofiled-aot.json
```

Rebuild without `-EnableRuntimeProfiling`, rebuild the frontend, and replay the
benchmark to obtain standard production latency. Build metadata records the
profiling flag. Default builds reject the profiling export with an explicit
diagnostic. Native focused profiling tests can be run with
`-p:EnableRuntimeProfiling=true`; the default focused tests check that the
diagnostic export is disabled. The normal browser worker never requests it.

Core (81), Game (31), Browser (22), and frontend (81) tests passed through the
repository scripts. Final focused profiling-enabled tests passed with identical
physics-command responses, and default-build focused tests passed. A concurrent
duplicate Browser test build hit a Windows apphost lock; the separate
`Test-Browser.ps1` run completed successfully. Production frontend builds and
local Studio/Designer smoke checks passed. No Vercel deployment was made.

Raw measurements: `tmp/worker-profile-aot.json`,
`tmp/worker-unprofiled-aot.json`, `tmp/worker-standard-aot.json`.
Summaries: `tmp/worker-profile-summary.json`,
`tmp/worker-profile-standard-summary.json`. These local generated files are
ignored by Git. The standard build is restored in `public/wasm` and `dist`.
