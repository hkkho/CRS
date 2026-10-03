# One-percentage-point shape tolerance: deployment assessment

2026-10-01. The wider regional acceptance band is a substantial optimization of
the established browser controller. Keep that change, but do not replace the
browser controller with the experimental 15-minute two-check path on the basis
of these results. This is not an unconditional pass against the 1-point shape
target and the existing responsiveness targets.

The production-shaped local build works and completes the long runs. Remaining
limitations are a regional target miss after a large refuel, latency spikes,
and little spare capacity in the long fuelled browser trace. No deployment was
performed; the stable Vercel alias must be checked after a release.

## What was measured

The fresh native matrix contains 35 complete trajectories: six aged seeds and
one 72-hour fuelling case for each of Game, matched Core, and experimental
two-check Core at 30, 15 and 3 minutes. All use analytic iodine/xenon, the same
physics pack, ±0.05 mk reactivity acceptance and ±1 percentage point of regional
power-share acceptance. The seed-1001 fuelled case inserts eight bundles in
channels 75 and 324 at hour 14, without advancing time during either operation.

Browser comparison builds are Release AOT, profiling disabled, SIMD default,
and threads disabled. The tight baseline uses the same current code with only
the regional tolerance temporarily restored to 0.01 percentage points. Its
source was restored byte-for-byte before building the wider-band candidate.
Both use the same frontend and Chromium, with sequential benchmark execution.
Their controller identity is v4; the baseline is a benchmark-only build.

Each browser variant runs the standard 20-command worker trace, 50 short ticks,
10x/60x playback probes, a repeated 24-command refuelling matrix and the full
144-advance 72-hour fuelling trace. The latter fixes seed 1001 and checks the
final clock and 112 remaining fresh bundles. The current build also passes the
production-shaped smoke script used by the Vercel pipeline.

These are descriptive local measurements: one native trajectory per case and
one browser run per workload. They are not a guarantee for other CPUs or mobile
devices. Browser worker tests measure command service/transport while a title
page renders; they do not establish frame-time performance of every Studio chart.
Build SHA `8966e9eea066d2b4d562bb94bf338b1c80e8c6a7` identifies the checkout base;
the tested edits are uncommitted and not contained in that published commit.

## Native results

All 35 cases completed, with no terminal runs or solver failures. All six
experimental startups now settle within the default eight-pass budget; no
longer-bootstrap recovery was needed.

| Current policy | Six no-refuel cases, seconds | Fuelled case, seconds | No-refuel peak shape error, percentage points |
|---|---:|---:|---:|
| Established Game / 30 minutes | 44.03 | 32.80 | 0.4775 |
| Established Core / 30 minutes | 34.42 | 31.10 | 0.4775 |
| Two checks / 30 minutes | 73.90 | 15.19 | 0.2570 |
| Two checks / 15 minutes | 70.87 | 14.28 | 0.0458 |
| Two checks / 3 minutes | 209.86 | 42.07 | 0.0144 |

The saved tighter-band Core reference took 399.09 seconds across the six seeds;
the wider-band reference uses 91.4% less CPU time. Those measurements were taken
in separate runs. The fresh Game/Core reference endpoints match exactly for
power, fills and burnup across all seven paired cases. Generated-energy error
was below 4.4e-15 relative; inventory/time checks passed.

The 15-minute alternative now costs 2.06 times the established Core controller
for ordinary operation, although its fuelled trajectory is 54.1% faster. It
always predicts and checks a flux-based correction, even when already within
the acceptance bands. The established controller avoids that extra regulation
work once the measured state is acceptable. Wider tolerances therefore do not
make the experimental routine substantially cheaper.

Changing the acceptance band changes the trajectory. Relative to the saved
tight-band Game runs, maximum endpoint node-power discrepancy is 4.45% of
reference peak in the controls and 5.18% in the fuelled case. Maximum individual
fill difference is 27.81 percentage points, and bundle burnup difference is
0.01113 MWd/kg. The new criterion bounds each region's share of total power;
it is not a 1% bound on every bundle's power or on zone fills.

## Browser results

| Measurement | Tight shape band | One-point shape band |
|---|---:|---:|
| Standard one-second command median | 714 ms | 247 ms |
| Standard one-second command p95 | 1,138 ms | 300 ms |
| 100 ms tick p95 | 655 ms | 235 ms |
| Ten consecutive ticks, service p95 | 1,226 ms | 399 ms |
| Refuelling matrix, WASM median | 779 ms | 288 ms |
| Refuelling matrix, WASM p95 | 1,470 ms | 867 ms |
| Full 72-hour fuelling advance median | 1,753 ms | 814 ms |
| Full 72-hour fuelling advance p95 | 2,634 ms | 935 ms |
| Full 72-hour fuelling advance maximum | 3,671 ms | 997 ms |

The wider band cuts standard worker p95 by 73.7% and long-run p95 by 64.5%.
Both matrices accepted all 24 operations, with matching state/replay/semantic
digests between passes and no console or page errors. Both 72-hour traces
completed with the correct fuel stock. Current browser endpoint fills agree
with native Game within 5.2e-12 percentage points.

The long browser case is more demanding than the short worker test. Its two
hour-14 fuel operations take about 1.98/1.99 seconds round trip. The 935 ms
advance p95 fits a one-second request budget on this machine with little margin.
The proposed 250 ms p95 target and strict 100 ms tick deadline still fail.
At accelerated speeds, 100 ms requests take about 225–230 ms at 10x and
981–1,024 ms at 60x; these modes remain compute-limited. Main-thread rendering
and command service deadlines are separate: WASM calculation runs in a worker.

## Accuracy and release decision

All no-refuel checks meet both criteria. The established controller and
15-minute candidate stay within ±0.05 mk throughout the fuelled case. After
the first eight-bundle insertion, regional shape error reaches 1.146 percentage
points in the established controller and 1.220 points in the 15-minute candidate.
The second insertion and subsequent routine checks are inside the shape band.
These are valid diffusion solutions with a controller target miss, not failed
commands. The 30-minute two-check candidate also reaches -0.05451 mk after that
first insertion, exceeding the reactivity band.

The smoke path passes Studio operation, seven history tabs, channel/refuelling
controls, preserved sessions, reset, Designer region edits and retained invalid
draft moves, plus 1600×900, 1280×720 and 720×720 layouts. Standard frontend build
passes. The current criteria previously passed 105 Core, 31 Game, 26 Browser and
81 frontend regression cases; this benchmark slice changes only measurement
tools and documentation. No simulation code or gameplay policy changes were
made during this assessment, apart from the temporary baseline build/restoration.

Before claiming strict deployment acceptance, address the bounded refuelling
correction/target miss and expensive post-refuel spatial iterations, then rerun
these same workloads. Keep the experimental 15-minute path out of browser
gameplay; evaluate respecting the acceptance bands in its prediction/correction
before considering a migration. If transient shape overshoot and slower command
feedback are acceptable gameplay tradeoffs, the current wider-band controller
is a reasonable preview candidate, with those limits explicitly understood.

## Artifacts and reproduction

Full native traces, CSV samples, endpoint differences and derived metrics are
under `artifacts/shape-1pp-campaign-2026-10-01`. Browser files are
`browser-{tight,relaxed}-{worker,matrix,72h}.json`; the same directory contains
the passing `browser-smoke.log` and preview screenshots. The baseline build
procedure is preserved in `tmp/Build-ShapeBaseline.ps1`; it restores the source
in a `finally` block. Build logs are `tmp/shape-{baseline,relaxed}-build.log`.

```powershell
dotnet run --project tools/SingleSolveBenchmark -c Release -- --campaign artifacts/shape-1pp-campaign-2026-10-01
python tools/SingleSolveBenchmark/report-campaign.py artifacts/shape-1pp-campaign-2026-10-01
```

Publish the candidate using `tools/Build-BrowserWasm.ps1 -RunAOTCompilation
-OmitPrecompressedAssets`, build the frontend normally, and start its production
preview. From `web/candu-playtest`, using that preview URL:

```powershell
node scripts/benchmark-xenon.mjs http://127.0.0.1:4186 20
node scripts/benchmark-wasm.mjs http://127.0.0.1:4186 --bridge=direct --warm-samples=1
node scripts/benchmark-xenon.mjs http://127.0.0.1:4186 --campaign-72h
node scripts/smoke.mjs http://127.0.0.1:4186
```
