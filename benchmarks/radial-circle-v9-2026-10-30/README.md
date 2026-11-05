# Circular radial boundary verification

The [physics notes](../../docs/physics/radial-circle-v9.md) describe the geometry,
preserved axial boundary, calibration, and reference change.

Both campaigns use seed 1001, normal 2,064 MW thermal / 650 MW electrical
browser defaults, unlimited fuel, atomic one-day integration, and the same
`oldest-lzc` player policy: two oldest channels/day, adding a third when the
previous day's published average LZC is below 45%. No physics, score, power,
inventory or loss-limit overrides are used. Assemblies were copied to isolated
directories before each run so later builds could not replace loaded binaries.

These are native .NET campaigns through the versioned Browser bridge and shared
Core/Game simulation. Browser-WASM release checks are separate. Campaign wall
times are not a controlled performance comparison.

| Measurement | Baseline v8 | Circular v9 |
| --- | ---: | ---: |
| Completed days | 100 | 100 |
| Fuel bundles | 1,808 | 1,816 |
| Mean discharge burnup, MWd/kg | 6.153724 | 6.159628 |
| Score | 2,302.809 | 2,304.433 |
| Thermal energy, MWh | 4,953,600 | 4,953,600 |
| Electrical energy, MWh | 1,560,000 | 1,560,000 |
| Average LZC range, % | 44.119–50.041 | 43.371–49.985 |
| Final average LZC, % | 46.086 | 48.317 |
| Maximum absolute axial tilt, % | 0.553 | 0.545 |
| Highest observed channel power, kW | 6,135.083 | 6,859.532 |
| Highest observed bundle power, kW | 790.224 | 853.666 |

Final Keff stays within 0.999987586–1.000032667. The intended radial redistribution
raises peak powers, while fuel demand, discharge burnup, score, energy, control
range and axial tilt stay close to the baseline. These results establish this
seed/policy's 100-day survival, not all possible player actions.

![100-day measurements](campaign-comparison.png)

Evidence:

- [Fresh baseline campaign](baseline.json), [final campaign](campaign.json),
  [final commands and digests](campaign-commands.jsonl).
- [Machine-readable comparison](comparison.json).
- [Archived v8 fixed reference](baseline-reference.json),
  [all 380 final references](reference/reference.json), [CSV](reference/reference.csv),
  [power-map figure](reference-comparison.png).
- [Independent calibration](../../data/calibration/radial-circle-v9/fit/fit.json)
  and [iterations](../../data/calibration/radial-circle-v9/fit/trials.json).
- [Verification record](verification.json), [Pages daily smoke](browser-acceptance.json),
  [cold/warm WASM reproduction](reproduction.json), [browser core screenshot](browser-core.png).

Final checks passed 124 Core, 86 Game, 46 Browser and 214 frontend tests, plus
the production and `/CRS/` subpath builds. The local Release, single-threaded,
non-AOT WASM smoke passed daily fuel/empty turns, frozen planning, narrow layout,
terminal feedback and unavailable-WASM recovery. All twelve reproduction rows
were accepted; cold/warm digests matched and no browser errors were reported.
This verifies the uncommitted working tree at the recorded base commit and pack
hash; it is not a published deployment or an AOT performance measurement.

Reproduce a final campaign with a new output prefix:

```powershell
dotnet run --project tools/LongRunPlaytest -c Release -- --pacing=daily-turn --policy=oldest-lzc --channels-per-day=2 --days=100 --seed=1001 --output=tmp/radial-v9-seed1001
```


## Time-based map scale calibration

A native Browser bridge sample used the v9 pack, time-based (`demo`) pacing,
and seeds 1001, 42 and 2026. For each seed it sampled the initialized core and
ten 12-hour endpoints. The first half of each day refuelled the two eligible
channels with the highest average burnup, breaking ties by channel index. The
second half advanced without orders. This gave 12,540 readings for each
channel metric and 462 individual zone readings. Quantiles use the nearest
sample rank after sorting.

| Map reading | 5th percentile | Median | 95th percentile |
| --- | ---: | ---: | ---: |
| Channel power, kW | 4,261 | 5,492 | 6,437 |
| Ripple, % of reference | 96.94 | 100.26 | 102.44 |
| Average channel burnup, MWd/kg | 1.48 | 3.35 | 5.23 |
| Individual zone fill, % | 39.56 | 49.99 | 60.06 |

The old 5,000 kW floor flattened 30.9% of sampled powers to one color, and
99.7% of sampled ripple fell into the old 95–105% band. The 100-day daily-turn
campaign corroborates the power and ripple ranges and shows lower zone fills
over longer play (5th–95th percentile 36.47–55.35%). The time-based display
uses fixed piecewise gradients with denser stops around these observed
ranges. Equal widths between legend ticks show the color space allocated to
each numerical interval. These samples characterize the tested seeds and
refuelling policy.
