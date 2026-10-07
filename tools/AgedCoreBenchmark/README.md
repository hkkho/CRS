# Aged-core burn benchmark

The current [reactivity-scale calibration](../../docs/physics/reactivity-scale-v3.md)
measures about 0.5 mk/FPD burnup loss and 7 mk total zone worth. Use
`--reactivity-scale OUTPUT.json` for an isolated current-pack measurement;
`--fit-reactivity-scale ARCHIVED_SOURCE.json OUTPUT_DIRECTORY` reproduces the
reviewed v3 fit from the archived powerlimits-v2 source. Historical benchmark
numbers below describe their original packs and cadence.

Isolate fuel-only worth of the oldest channel in each feed direction at hour 14:

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --refuel-worth artifacts/two-channel-refuel-worth
```

This replays seed 1001 and uses tight independent inventory solves before
and after each eight-bundle operation. Initial nonuniform zone fills are held
fixed to isolate fuel worth; empty and actual live zone patterns are also
solved. It writes `refuel-worth.json`, including channel fuel histories.

Two-channel refuelling response at hour 14, with a matched no-refuel control:

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --refuel-response artifacts/two-channel-refuel-zone-response 1001
python tools/AgedCoreBenchmark/report-refuel-response.py artifacts/two-channel-refuel-zone-response
```

This selects one old channel for each discharge direction and inserts eight
fresh bundles in each. It records all fourteen fills before and after both
same-time operations and at half-hour intervals through hour 72. Seven
subplots pair Z1/Z8 through Z7/Z14; an additional seven-panel zoom shows the
event interval. The plot compares actual refuelled and no-refuel trajectories.


This diagnostic tool runs the same authoritative browser `GameSession` in

native .NET; it is a physics-results benchmark, not a WASM latency benchmark.

It samples all channels, bundles and zone fills every half-hour for three

simulated days, without user power commands or refuelling. The factory's stock

startup events remain active. Native game-over and failure semantics apply.



```powershell

dotnet run --project tools/AgedCoreBenchmark -c Release -- artifacts/aged-core-benchmark

```



To choose seeds, append them after the output directory. Default seeds are

0, 1001, 1002, 1003, 1004 and 4294967295. Outputs are full-precision `report.json`

and `samples.csv`; completed seeds are saved progressively.



Each run also solves the starting and final fuel inventories independently

with tighter convergence tolerances and no zone overlay (empty zones in the calibrated pack). These probes

check burnup-only reactivity decay without modifying the live trajectory or

the coefficient tables. The report distinguishes their endpoint estimate

from the normal gameplay solver's noisier reactivity trace.



Optional readable report and scientific plots (requires matplotlib):



```powershell

python tools/AgedCoreBenchmark/report-results.py artifacts/aged-core-benchmark
```

The report includes absolute empty-zone reactivity, live compensated net
reactivity and zone fills in `reactivity.png` / `reactivity.svg`. Append a
matching zone-worth audit directory to add tight frozen-initial-fill endpoint
probes. These are endpoint comparisons, not an interpolated frozen-fill run:

```powershell
python tools/AgedCoreBenchmark/report-results.py artifacts/aged-core-calibrated-benchmark-2026-09-30 artifacts/zone-calibrated-drain-2026-09-30
```


Absolute power uses the game's 1 GW reference scale. The decay measurement

uses RRS's unregulated base-solve reactivity, separating burnup from zone

compensation. Positive loss means decreasing reactivity. Zone endpoint

statistics and whole-run envelopes are recorded separately. The simulator

uses synthetic coefficients and excludes xenon dynamics.



To audit whether zone movement compensates the burnup loss:



```powershell

dotnet run --project tools/AgedCoreBenchmark -c Release -- --zone-worth artifacts/zone-worth-audit

```



This reruns the six default seeds for 72 hours and saves `zone-worth.json`.

Tighter endpoint solves freeze the initial zone pattern to measure burnup

loss, then apply the final pattern to measure the exact reactivity gained

from zone movement. Central differences of +/-1 percentage point measure

uniform worth (all 14 zones together) and each zone's individual worth.

The expected uniform drain assumes unchanged residual reactivity; unequal

zone worths and spatial regulation mean the arithmetic average fill need

not follow that prediction. Initial and final controlled reactivity are

reported to expose departures from that assumption.



Full-range geometry audit and a static view of both axial halves:



```powershell

dotnet run --project tools/AgedCoreBenchmark -c Release -- --zone-geometry artifacts/zone-geometry-audit

python tools/AgedCoreBenchmark/report-geometry.py artifacts/zone-geometry-audit

```



This records direct empty-to-full worth, empty-state failures if any, and

50%–100% worth with its separately labelled full-range extrapolation. The

current calibrated model uses positive absorption and measures about 6.5 mk.

The older exploratory scale fields remain diagnostics only.



Reproduce the offline fit against the archived pre-calibration source pack:



```powershell

dotnet run --project tools/AgedCoreBenchmark -c Release -- --calibrate-zones artifacts/zone-calibration-reproduced artifacts/zone-calibration-2026-09-30/source-pack.json

```



The fit uses prototype slopes 0.020/0.008 m^-1 per unit fill and seed 1001.

It calculates a common multiplier for both neutron-production groups so the

reference is critical at 50% fill, then fits total worth to 6.5 mk. It writes

a proposal only; it never edits the runtime pack or source constants.


## Joint power, fuel and zone fit

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-core artifacts/core-fit-reproduction artifacts/core-fuel-calibration-190fpd-2026-09-30/source-pack.json
```

This offline fit uses the archived zones65 pack, a 190-FPD channel interval,
2064 MW thermal, 16 fresh bundles/day and a 6.262136 MWd/kg exit target.
It writes a proposal rather than modifying runtime data. Acceptance requires
both 16-bundle worth and one-day fixed-zone decay within 0.4-0.7 mk, and
approximately 6.5 mk empty-to-full zone worth. The applied pack is versioned
separately; provenance and checksum updates accompany its installation.

## 100-day fuelling capability

`dotnet run --project tools/AgedCoreBenchmark -c Release -- --fuelling-100-days artifacts/fuelling-100-days [SEED]`

Omitting the seed draws a cryptographic uint32 and records it for replay.
The authoritative endless browser session advances every 180 simulated seconds,
with normal xenon, LZC and terminal power limits. An offline player policy refuels
eight bundles along channel flow whenever mean zone fill falls below 45%. If
paired regional fills differ by more than one percentage point, it chooses the
oldest eligible channel in the lowest-fill region; otherwise it chooses the oldest
globally. It checks power/fuel accounting at every step, records hourly and move
samples, and checkpoints `report.json` hourly. A terminal state ends the
attempt and is reported as failure to reach 100 days, never bypassed.

Compare the existing power-headroom player policy with the same seed:

```powershell
dotnet run --project tools/LongRunPlaytest -c Release -- --endless=true --days=100 --seed=1759455334 --threshold=.45 --policy=reserve --output=artifacts/fuelling-100-days-reserve
```

That tool requests 10x playback; Game still solves every internal three-minute
step and enforces all normal limits. Its JSONL observations are half-hour
snapshots and instantaneous accepted fuel moves. To plot either report, run
`python tools/AgedCoreBenchmark/plot-fuelling-capability.py REPORT.json`;
LongRunPlaytest reports require their matching `.jsonl` sidecar.
