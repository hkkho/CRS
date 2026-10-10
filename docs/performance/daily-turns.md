# Daily-turn verification

The current default pack is [v8 axial Marshak](../physics/axial-marshak-v8.md).
The measurements below record the earlier v7 daily integration rollout; the
[v8 acceptance archive](../../benchmarks/axial-marshak-v8-2026-10-10/README.md)
contains the current campaign, browser checks and restored axial profile.

Daily mode uses `practice-daily-one-step-v1`: one 86,400-second frozen-power/flux
exposure and poison update, followed by one end-of-day equilibrium/RRS solve.
Solver iterations do not advance time. Refuelling orders execute first, in
channel-index order, using the shared eight-bundle movement rules. Limits are
checked after fuel moves and at the day boundary; no intraday states are sampled.
Numerical failure discards the detached turn, preserving the original run.

The browser defaults to daily pacing and remains frozen while planning. Its
worker yields before calculating the large step, allowing the animated waiting
dialog to render. Progress is indeterminate during calculation. The authoritative
result flashes survival or loss; survival shows earned day points and total score.
There is no artificial animation delay or added score bonus. Reduced motion
disables decorative animation. A transport watchdog still detects an unresponsive
worker; calculation speed is not a release performance gate.

Daily saves record the integration identity. Saves from the initial subdivided
daily model are rejected explicitly rather than replayed with different physics.
Version-1 saves retain explicit real-time replay. Existing cloud databases require
the generated `daily_turn_saves` migration to accept version-2 saves.

## Corrected 100-day campaign

The [campaign archive](../../benchmarks/daily-turns-100-days-2026-10-09/README.md)
records seed 1001, refuelling the two highest-burnup channels each day. All 100
single-step days completed without an operating-limit or numerical failure.

| Measurement | Result |
| --- | --- |
| Simulation duration | 100 days, exactly 86,400 seconds per turn |
| Fuel | 200 eight-bundle operations; 1,600 fresh bundles |
| Total score | 2,320.912464 |
| Final average LZC | 46.976244% |
| Final axial tilt | +0.581427% |
| Highest observed day-boundary channel power | 6,078.732 kW |
| Highest observed day-boundary bundle power | 567.338 kW |
| Thermal energy | 4,953,600 MWh |

This campaign uses the native .NET versioned Browser bridge with the same shared
Core/Game physics. It is not a 100-day browser UI or WASM performance test. The
archive includes exact commands, state/replay digests, every fuel movement, and
loaded assembly hashes. Roughly 338 seconds elapsed with concurrent validation;
this is not an isolated latency benchmark.

## Acceptance checks

Corrected Release verification passed 81 Game tests, 45 Browser tests, and 167
frontend/local database tests. The focused daily test directly compares analytic
one-day poison densities and per-bundle exposure, and asserts one clock interval
and one final projection solve. Canonical replay, atomic numerical rollback,
stale candidates, stock/input rejection and terminal outcomes are also covered.
The production frontend and `/CRS/` Pages build passed with rebuilt AOT WASM.

Run the default production-browser acceptance independently of the retained
real-time comparison:

```powershell
cd web/candu-playtest
npm run smoke:daily -- http://127.0.0.1:4173/CRS/
npm run smoke:saves -- http://127.0.0.1:4173/CRS/
```

The daily smoke verifies frozen planning, two refuel orders, exact day duration,
an empty next day, survival score, early fuel-move loss, and 320px layout. The save
smoke verifies local save, offline reload, exact WASM replay and unfinished draft.
These checks are local and do not claim a hosted deployment was updated.

Both production-browser checks passed on the corrected AOT build. Seed 1001's
two-channel day took 4,154 ms; its empty next day took 1,487 ms. A 380-channel
plan produced the authoritative early fuel-move loss (`LZC average level above
90%`) with the red failure dialog, no success score, and a visible shift report.
There were no reported browser errors. Offline replay of seed 2024 restored
86,400 seconds, 8 bundles consumed, score 23.336667063957762, and the next day's
unfinished draft. Narrow layout passed in both checks.

## Benchmark-based display ranges

Studio uses the corrected campaign and the current v7 initial wire snapshot to
choose display bounds. These are presentation ranges, not simulation thresholds.
The initial snapshot's channels span 4,879.789–5,883.307 kW; hottest bundles by
channel span 451.694–547.323 kW; all individual bundles span 323.164–547.323 kW.
The 100-day campaign's channel peaks span 5,796.388–6,078.732 kW, bundle peaks
536.360–567.338 kW, and mean LZC 46.959–51.131%.

| View | Initial display range |
| --- | --- |
| Channel-power map | Blue at/below 4,500 kW; red at/above the published limit (normally 7,300 kW) |
| Hottest-bundle map | Blue at/below 400 kW; red at/above the published limit (normally 935 kW) |
| Maximum-channel history | 5.7–6.2 MW thermal |
| Maximum-bundle history | 520–580 kW thermal |
| Zone / average LZC history | 45–55% |
| Axial bundle-power profile | 300–600 kW thermal |

Graphs expand to include all displayed finite readings, with 12% of the observed
span as padding and a small minimum span for constant readings. Keff uses a small absolute minimum span rather than padding by
4% of its value near one, which previously concealed its solved variation.
Burnup, poison and local-tilt histories also zoom to actual readings. Axis labels
and accessible descriptions expose the bounds. Limits outside the view are
labelled above/below it; an in-range limit also gets a dashed line. Normal map
endpoint colors clip, with the floor and limit named in the legend.

Verify the production UI with `npm run smoke:scales -- http://127.0.0.1:4173/CRS/`.
The [production result](../../benchmarks/daily-turns-100-days-2026-10-09/display-scales-browser-acceptance.json)
passed: all 380 channels retained distinct power/hottest-bundle color values,
peak-power and Keff trends used the intended ranges, and a different refuelling
plan lowered LZC to 43.47%, expanding its axis to 42.69–55% rather than clipping
the readings. Desktop and 320px layouts passed with no reported browser errors.
The display change passed `tools/Test-Browser.ps1 -Configuration Release`: 45
shared Browser tests, 172 frontend/local database tests and the production build.
The `/CRS/` Pages build also passed.

## Superseded measurements

The first implementation subdivided a day into 480 intervals of 180 seconds.
Its local browser turns took about 9–11 minutes. The user clarified that a daily
turn must use one large interval, so those measurements do not describe the
current mode. The interrupted initial campaign is explicitly marked
`superseded-by-single-day-integration` in its archived report.
