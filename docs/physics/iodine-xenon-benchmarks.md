# Iodine/xenon implementation benchmark — 2026-10-01

Implemented model: [analytic spatial iodine/xenon](iodine-xenon-gameplay.md).
For component timings and ranked performance experiments, see the
[worker calculation profile](runtime-profile.md).
Measurements use local Chromium and a production Vite preview at
`http://127.0.0.1:4174`, with the authoritative C# browser-WASM host.
The final runtime is Release AOT, SIMD default, threads disabled. The checkout
contains uncommitted work: build metadata's HEAD SHA identifies the repository
base, not a published commit containing these edits. No Vercel deployment was made.

## Final AOT worker measurements

The production worker benchmark refuels channel 210, requests 95% power, advances
ten simulated hours with twenty one-second requests, then exercises fifty normal
100 ms control ticks and two samples each at 10x and 60x. Total simulation time
is 19.5 hours. All commands were accepted; poison state evolved, and the largest
zone-fill change over the first ten hours was 3.84 percentage points. Burnup,
refuelling and regulation also contribute to this run's response; the focused
spatial test isolates xenon's effect using otherwise identical fuel candidates.

| Measurement | Median | p95 | Samples |
|---|---:|---:|---:|
| One-second advance, complete worker round trip | 656.8 ms | 727.0 ms | 20 |
| One-second advance, WASM call only | 640.2 ms | 710.6 ms | 20 |
| Individual 100 ms control tick, worker round trip | 8.6 ms | 710.2 ms | 50 |
| Service time for ten consecutive 100 ms ticks | 794.1 ms | 804.0 ms | 5 |

One-second requests and aggregate tick service fit a one-second processing
budget on this machine. The proposed 250 ms p95 target does **not** pass.
Scheduled diffusion/RRS solves still produce approximately 710 ms spikes;
individual ticks therefore do not meet a strict 100 ms deadline. The existing
live clock discards elapsed wall time during slow calls rather than creating
a catch-up backlog, so these throughput measurements are not a guarantee of
perfect wall-clock pacing in Studio.

At 10x, two 100 ms advances took 697.1/686.2 ms. At 60x, they took
3685.1/3628.9 ms. Those speeds remain compute-limited. No machine-time-dependent
cadence, density clamp, asynchronous partial commit or browser-side physics
approximation was introduced to hide these limits.

The analytic poison step is O(4560), with no stability-driven substeps. Immutable
poison digests and absorption overlays are constructed lazily and reused; short
intermediate candidates do not build or hash full coefficient maps. The spatial
solve cadence remains 1,800 simulated seconds and uses accepted warm starts.

## Reproduction matrix

Each matrix has twelve cases, covering channel 210, a central channel, a peripheral
channel, both shift directions, four/eight bundles and paused/live operation.
Two passes produce 24 accepted refuelling rows. State/replay/semantic digests match
between passes; no console or page errors were reported.

| Runtime | Live advance median / p95 | Refuel median / p95 |
|---|---:|---:|
| Original local non-AOT baseline | 3674.7 / 3841.4 ms | 8430.8 / 12040.8 ms |
| Intermediate non-AOT xenon, before lazy caching | 4070.7 / 4224.4 ms | 7492.4 / 14031.0 ms |
| Final AOT xenon | 575.8 / 579.6 ms | 1006.4 / 1638.7 ms |

Live-advance timings cover twelve live preparation commands; refuel timings cover
all 24 rows. Non-AOT measurements are development diagnostics, not production
performance. Some long non-AOT runs overlapped local checks/builds, so their delta
is indicative rather than an isolated measurement of poison overhead. AOT and
non-AOT results must not be used to claim an isotope-algorithm speedup.

## Verification

- Core, Game and Browser checks passed through `tools/Test-DotNet.ps1` suite runs.
- Thirteen focused analytic cases passed, including a separate RK4 reference,
  equal removal rates, large intervals, equilibrium and power-change trends.
- The one-hour post-refuelling cadence test passed against a five-minute reference:
  Xe error below 1% of peak density, fill difference below 2 percentage points,
  and zonal power-fraction difference below 0.005.
- `tools/Test-Browser.ps1` passed: 20 bridge tests, 81 Vitest tests and frontend build.
- The deployment smoke script passed locally with the final AOT build: 380 channels,
  fourteen zones, seven history tabs, accepted refuelling, retained session/history
  behavior, Designer geometry edits, reset, and 1600/1280/720-pixel layouts.
- Final diff whitespace check passed. Existing unrelated working-tree edits were retained.

Raw local artifacts are in `tmp/xenon-before.json`, `tmp/xenon-after.json`,
`tmp/xenon-final-aot-matrix.json`, `tmp/xenon-worker-aot.json`, and
`tmp/xenon-smoke.log`. Reproduce the final worker run with:

```powershell
npm run benchmark:xenon -- http://127.0.0.1:4174 20
```

The stable Vercel alias still needs verification after deployment of this change.
