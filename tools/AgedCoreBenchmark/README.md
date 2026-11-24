# Aged-core burn benchmark

The [geometry calibration](../../data/calibration/diffusion-v10/README.md)
records about 0.427 mk per full-power day of frozen-xenon fuel loss and 7 mk
total zone worth for that configuration. Use
`--reactivity-scale OUTPUT.json` for an isolated current-pack measurement;
`--fit-reactivity-scale ARCHIVED_SOURCE.json OUTPUT_DIRECTORY` runs the reactivity-scale fit
from the archived source pack identified by `candu6-two-group-diffusion-v1-cycle190-650mwe-powerlimits-v2`. Reproducing historical output also
requires the corresponding source and settings. Historical benchmark numbers
below describe their original packs and cadence.

## Lateral iodine–xenon tilt

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --xenon-tilt artifacts/xenon-hourly.json 1001
dotnet run --project tools/AgedCoreBenchmark -c Release -- --xenon-tilt artifacts/xenon-15min.json 1001 4
dotnet run --project tools/AgedCoreBenchmark -c Release -- --xenon-tilt artifacts/xenon-xe0.json 1001 1 0
```

The benchmark starts from the seed-aged core with iodine and xenon in equilibrium
at 50% zone fill. At hour zero it freezes the four left compartments at 80%,
the four right compartments at 20%, and the six centre compartments at 50%.
One trace holds those fills through hour 96. The other shares the first eight
hours of poison history and then restores all fills to 50% at hour eight to
measure the free response. Both use the shared C# diffusion and poison model
at 2,064 MW thermal with fixed fuel inventory and burnup. The benchmark bypasses
regulation and gameplay limit checks and tightens the spatial solver tolerances
to reduce drift in small tilts.

The JSON records hourly left and right power, mean I-135 and Xe-135 number
densities, thermal flux, effective multiplication factor, and solver residual.
Tilt is `100 × (right power − left power) / total power`. A reported period
spans two same-kind extrema with an intervening opposite extremum and at least
0.05 percentage points of movement on each leg. The argument after the seed sets
the integration steps per hour. Reports stay hourly. The final optional
number sets the xenon thermal absorption multiplier. At zero, the isotopes
still evolve; with the default pack, xenon absorption and neutron capture are
disabled because fast-group absorption is also zero. An
optional pack path after the multiplier runs an offline comparison against
another pack. Supply the multiplier positional argument when passing a pack path; use one for unscaled absorption.

The seed-1001 case with the active pack shows 25–26-hour cycles with hourly integration.
Keeping the zones split leaves a rightward bias. Releasing the split lets the
dominant half reverse and the oscillation decay toward equilibrium. See the
[xenon calibration](../../docs/physics/xenon-calibration.md) for the full-power
capture, worth, and damping comparisons.

## Refuelling response

Estimate fuel worth for selected channels at hour 14:

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --refuel-worth artifacts/two-channel-refuel-worth
```

This replays seed 1001 and uses tight independent inventory solves before
and after each eight-bundle operation. The tool groups channels by which
eight-bundle end window has greater average burnup, assigning ties to End B.
The windows are positions 0–7 and 4–11, so they overlap in four positions. It
selects the channel with the highest average burnup in each group. There can be
one or two groups; equal channel averages are resolved by the lowest channel
index. When both groups exist, `toward-end-a` is processed first. Game executes each move in the channel's actual flow direction;
the grouping does not guarantee opposite actual flow directions. Initial
nonuniform zone fills are held
fixed to isolate fuel worth; empty and actual live zone patterns are also
solved. These independent solves do not add the live-xenon overlay. It writes
`refuel-worth.json`, including position, burnup and power snapshots for bundles
in the selected channels.

Two-channel refuelling response at hour 14, with a matched no-refuel control:

The current command fails during its initial inventory check: it expects
128 fresh bundles, while the browser factory supplies unlimited fuel with a
zero stock sentinel. The plotting command requires a complete report and
`zones.csv` from a compatible run.

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --refuel-response artifacts/two-channel-refuel-zone-response 1001
python tools/AgedCoreBenchmark/report-refuel-response.py artifacts/two-channel-refuel-zone-response
```

The intended run uses the same end-window grouping to select two channels
and requires both groups to exist. It inserts eight fresh bundles in each along actual channel flow. Its event
`direction` field records the grouping label, which can differ from the move's
actual direction. It records all fourteen fills before and after both
same-time operations and at half-hour intervals through hour 72. Seven
subplots pair Z1/Z8 through Z7/Z14; an additional seven-panel zoom shows the
event interval. The plot compares actual refuelled and no-refuel trajectories.
The separate `plot-individual-zones.py` script only accepts reports whose
`controllerModel` contains `criticality-first`; the current controller ID does
not match that requirement.

## Three-day inventory and zone audits

The default diagnostic is intended to run a native `GameSession`, sample channels,
bundles and zone fills for up to three simulated days, and stop on terminal
conditions. Its initial inventory check currently expects 128 fresh bundles,
but the browser factory uses unlimited fuel and reports a zero stock sentinel.
The command therefore fails before producing the intended report. It is not a
browser-WASM latency measurement.

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- artifacts/aged-core-benchmark
```

Append seeds after the output directory; the defaults are 0, 1001, 1002, 1003, 1004 and 4294967295. With compatible finite-stock sessions, the tool writes full-precision `report.json` and `samples.csv`, saving completed seeds progressively. Independent tighter solves compare the initial and final fuel inventories without zone or live-xenon overlays; the pack's included xenon reference remains in its fuel coefficients. The live unregulated reading uses the xenon background. These are endpoint probes, not a continuous frozen-zone trajectory. Check the pack and reference power when interpreting a saved report.

The native session advances iodine and xenon and has no scripted startup
events. The independent endpoint probes are separate static solves.

Generate a readable report and plots with matplotlib:

```powershell
python tools/AgedCoreBenchmark/report-results.py artifacts/aged-core-benchmark
```

Append a matching zone-worth audit directory to include tighter frozen-initial-fill endpoint probes. The audit's final probe is at 72 hours. The plotter checks pack and seed identities but does not verify matching settings or inventories, so check those before comparing it with a trajectory. The report distinguishes unregulated reactivity, compensated reactivity and zone fills. A positive loss means decreasing reactivity.

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --zone-worth artifacts/zone-worth-audit
dotnet run --project tools/AgedCoreBenchmark -c Release -- --zone-geometry artifacts/zone-geometry-audit
python tools/AgedCoreBenchmark/report-geometry.py artifacts/zone-geometry-audit
```

The zone-worth audit reports endpoint comparisons and central-difference estimates for uniform and individual zone movements. The geometry audit records direct and extrapolated worth measurements using a reconstructed static-mask mapping. That mapping retains node weights and logical zone IDs but does not retain the game's tube water-column overlap or separate absorber-zone IDs. Because this audit uses uniform fills, the changed absorber-zone IDs do not affect its solves; the static-mask filling rule does. Neither audit adds a live-xenon overlay to these independent solves. Results depend on their input pack and solver settings.

To fit an archived source pack, supply its path explicitly. The fit writes a proposal; it does not modify runtime data.

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --calibrate-zones artifacts/zone-calibration-reproduced artifacts/zone-calibration-2026-09-30/source-pack.json
```

## Joint power, fuel and zone fit

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-core artifacts/core-fit-reproduction artifacts/core-fuel-calibration-190fpd-2026-09-30/source-pack.json
```

This offline fit uses the archived zones65 pack, a 190-FPD channel interval,
2064 MW thermal, 16 fresh bundles/day and a 6.262136 MWd/kg exit target.
It writes a proposal rather than modifying runtime data. Acceptance requires
both 16-bundle worth and one-day fixed-zone decay within 0.4-0.7 mk. The fit
targets approximately 6.5 mk empty-to-full zone worth, but does not include a
final zone-worth check in that acceptance condition. The applied pack is versioned
separately; provenance and checksum updates accompany its installation.

## 100-day fuelling capability

`dotnet run --project tools/AgedCoreBenchmark -c Release -- --fuelling-100-days artifacts/fuelling-100-days [SEED]`

Omitting the seed draws a cryptographic uint32 and records it for replay.
The native endless `GameSession` uses the real-time comparison cadence and advances every 180 simulated seconds,
with normal xenon, LZC and terminal power limits. An offline player policy refuels
eight bundles along channel flow whenever mean zone fill falls below 45%. If
paired regional fills differ by more than one percentage point, it chooses the
oldest eligible channel in the lowest-fill region; otherwise it chooses the oldest
globally. It checks power/fuel accounting at every step, records hourly and move
samples, and checkpoints `report.json` hourly. A terminal state ends the
attempt and is reported as failure to reach 100 days, never bypassed.
The output argument is a directory; the tool writes `report.json` inside it.
Use a fresh directory to preserve an existing report.

Compare the existing power-headroom player policy with the same seed:

```powershell
dotnet run --project tools/LongRunPlaytest -c Release -- --endless=true --days=100 --seed=1759455334 --threshold=.45 --policy=reserve --output=artifacts/fuelling-100-days-reserve
```

That tool requests 10x playback; Game still solves every internal three-minute
step and enforces all normal limits. Its JSONL observations are half-hour
snapshots and instantaneous accepted fuel moves. Its output argument is a
filename prefix: the summary and telemetry use `.json` and `.jsonl` suffixes.
Use a fresh prefix to preserve existing output. To plot either report, run
`python tools/AgedCoreBenchmark/plot-fuelling-capability.py REPORT.json`;
LongRunPlaytest reports require a `.jsonl` sidecar with the same basename.
The plotter overwrites `fuelling-capability.png` beside the report and uses
fixed annotations for the recorded 100-day experiment, including its 45%
refuelling trigger and power and tilt limits. Its curves show saved samples,
not every internal step; consult the report for the final outcome and extrema.
