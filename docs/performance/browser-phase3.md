# Browser shell, rendering and pace measurements

Local Release AOT WASM and production Vite preview, Chromium, 2026-10-02.
These are machine-specific observations, not device-wide guarantees.

## Feature loading (task 14)

`scripts/benchmark-startup.mjs` takes three cold contexts per condition, cache
disabled. Constrained means page CPU 4×, network 60ms latency and 1.5MB/s.
Worker CPU throttling is not claimed. Median milliseconds:

| Condition | Shell before / after | Usable Begin before / after | Runtime request to ready before / after |
| --- | --- | --- | --- |
| Normal | 166 / 40 | 2,528 / 2,480 | 2,381 / 2,446 |
| Constrained | 951 / 310 | 23,056 / 22,494 | 22,202 / 22,192 |

Initial decoded JavaScript fell from 1,832,628 to 581,150 bytes; all decoded
response bodies fell from 30,780,011 to 29,526,557 bytes. Page-target encoded
bytes fell from 379,203 to 34,762, **excluding worker traffic**. These are different
metrics and must not be added together. WASM dominates usable-start latency;
the split substantially improves shell appearance but only modestly improves
Begin readiness. Raw samples: `tmp/phase3-startup-before.json` and `-after.json`.

The native launcher/Studio share one controller and history. Only opening
Designer imports Phaser; returning sleeps its loop and restores navigation.
Download failure leaves a retryable Studio. The 1.25MB Designer chunk still
triggers Vite's warning; its threshold was not increased. Keyboard smoke passed
through real WASM Designer actions and return; 116 Vitest tests/build passed.

## Rendering baseline (task 15)

`scripts/benchmark-rendering.mjs` uses a production WASM 380-channel snapshot,
with **synthetic timestamps** to stress 4,096 retained observations. It does not
simulate 4,096 reactor steps. Thirty updates per category, same viewport/machine.
Baseline p50/p95 synchronous render ms: pending 2.3/2.6, compact root updates
2.1/2.5, selection 2.0/2.2, full zones history 14.4/17.5. Pending updates caused
2,841 DOM mutations each. Double-rAF frame opportunity was about 33ms; this
includes display cadence and cannot isolate GPU paint. Initial direct WASM call
was 1,948ms, JSON parse 5.5ms, decoded payload 2,690,628 bytes. WASM call includes
serialization; payload bytes are a size, not transfer duration or solver time.
Transport/command timings are measured separately below. Baseline raw report:
`tmp/phase3-render-before.json`.

After optimization (same fixture, 30 samples):

| Update | Render p50 before / after (ms) | Render p95 before / after (ms) | DOM mutations before / after |
| --- | --- | --- | --- |
| Pending | 2.3 / 0.1 | 2.6 / 0.2 | 2,841 / 32 |
| Compact snapshot | 2.1 / 0.8 | 2.5 / 1.0 | 2,821 / 120 |
| Selection | 2.0 / 1.4 | 2.2 / 1.6 | 2,821 / 190 |
| Full history | 14.4 / 7.7 | 17.5 / 9.0 | 2,856 / 167 |

Controller notifications distinguish status from immutable snapshot changes.
Studio caches watchlist ordering and axial/movement inputs, compares map
attributes, and patches stable SVG structures. Confirmed movement details remain
open across unchanged notifications. History reduces **display paths only** to
bucket endpoints/extrema, preserving missing-value breaks, equal-time transitions
and every step transition. All 4,096 samples remain available to exact inspection.
The note reports observations retained, not reduced path vertices. Frame cadence
stayed about 33ms; no GPU paint speedup is claimed. Regression tests cover stable
nodes/focus, changed axial readings and reduction invariants.

A stronger canvas-visibility smoke caught hidden-parent Phaser sizing after task
14; refreshing parent bounds on entry corrected it. Final full smoke now checks
visible canvas dimensions and passed gameplay, geometry, history and challenge.
119 tests passed at the task 15 boundary; the later pace tests bring the count
higher. `tmp/phase3-render-after.json` contains raw measurements.

Chromium timeline tracing separately measured CPU Paint / Layout / style work
across 30 updates: pending 18.7 / 7.6 / 3.0ms, compact 15.0 / 9.8 / 1.7ms,
selection 23.9 / 14.8 / 4.0ms, history 10.3 / 10.3 / 2.6ms. These are aggregate
CPU timeline totals, not GPU/compositor latency. There is no baseline paint
comparison. `tmp/phase3-render-trace.json` is the trace summary. Instrumented
solver categories are documented separately in `../physics/runtime-profile.md`;
that older diagnostic binary changes AOT optimization and its component timings
must not be subtracted from the current production timings.

## Requested and achieved pace (task 16)

Studio reports requested speed and observed **simulation minutes per real
second**, averaged over a bounded 15-second/120-observation window. It includes
solver/foreground waits, resets on pause, speed changes, hiding, leaving Studio,
terminal state and backwards time/reset, and requires 500ms before displaying a
rate. The quiet Solving label reflects requests in flight without disabling
foreground pause for a clock request. Rate measurement is independent of whether
the scheduler can dispatch while a request is pending; it never drives physics.
The scheduler remains one in-flight advance and one pending 100ms quantum, with
no catch-up and no tolerance changes.

The retired real-time pace harness measured the **production worker asset** through the real
parser/materializer: 20 1x ticks (including scheduled spatial solves), six each
at 10x/60x, three refuels and three explicit full shape solves. The final run had
no concurrent builds, test suites or other browser benchmarks. All commands
accepted. Its source remains in Git history; current daily latency is measured
through daily acceptance. Milliseconds, local machine only:

| Operation | Samples | Total median / maximum | WASM median | Parse/materialize median | Queue/transfer/encoding remainder median | Review budget |
| --- | --- | --- | --- | --- | --- | --- |
| 1x tick | 20 | 11.0 / 199.9 | 9.0 | 1.7 | 0.1 | 250 |
| 10x tick | 6 | 191.4 / 232.3 | 170.8 | 9.8 | 10.3 | 300 |
| 60x tick | 6 | 847.6 / 871.9 | 828.0 | 8.9 | 10.5 | 1,100 |
| Refuel | 3 | 253.4 / 262.8 | 234.9 | 8.9 | 9.4 | 350 |
| Shape solve | 3 | 1,787.3 / 1,799.6 | 1,771.1 | 6.2 | 10.0 | 2,250 |

Worker module readiness was 150ms; full initialization another 1,976ms.
Cold Begin budgets are 3,500ms normal / 30,000ms constrained, based on task 14's
cold samples. Command review budgets are approximately observed maximum +25%,
rounded up. They are machine-specific investigation thresholds, **not** watchdog
limits, promised device latency or new gameplay roadblocks. Small sample maxima
are not robust population p95/p99 estimates. See
`../../benchmarks/browser-latency-budgets.json` and `tmp/phase3-pace-final.json`.

An actual scheduler 60x tick delivered 198.3 simulated minutes/real second.
Pause queued during that tick completed in 806ms; no later tick overtook it or
appeared after pause. A subsequent foreground refuel was accepted in 211.5ms.
The observed rate resets to unavailable while paused. Standard playback labels
represent requests and are not equivalent to the measured rate.

WASM durations include simulation, RRS, snapshot mapping, digest and serialization;
production scopes are compiled out. Published last-solve diagnostic duration is
zero in this build and **must not be read as a zero-cost solver**. The remainder
is elapsed round trip minus worker call and parser time, so includes queues,
string encoding/copying and scheduling rather than isolated transfer time.
Payload-changing ticks/refuels return roughly 2.69MB versus ~12.7kB for routine
compact ticks. Current measurements prioritize shared controller/solve cost and
changed-core payload work; they do not justify relaxing physics tolerances.
Earlier benchmark attempts omitted fuelTypeId in a harness request; corrected
runs use the same draft factory as Studio. Initial pace/lifecycle and 320px
checks caught issues which were fixed before final acceptance.

Final acceptance: 32 Browser .NET tests, 124 frontend tests and production build
passed. Full local gameplay/recovery and keyboard smoke passed. Visual review
caught a desktop flex-column wrap issue introduced by the pace label; final
`pace-layout-smoke.mjs` checks actual 60x rate, quiet telemetry, foreground pause,
rate reset and clock-card containment at 320/640/720/1280/1600px. No deployment is
claimed. Final entry JS is 100.15kB (30.12kB gzip); task 14 startup measurements
were taken at its 96.41kB feature-split boundary. The on-demand Designer remains
1.25MB; WASM still dominates cold readiness.
