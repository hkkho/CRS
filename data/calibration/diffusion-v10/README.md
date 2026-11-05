# Reduced diffusion calibration

The [candidate pack](candidate.json) uses 1.5 cm fast and 0.75 cm thermal
diffusion coefficients along and across channels. Its four interior conductances
are 37.5% of the radial-v9 values. The existing circular radial and Marshak
axial boundary conditions derive their conductances from the new interior
values. The fuel coefficient rows, xenon reference, adjuster strengths and
liquid-zone strengths are identical to baseline.

The [fixed-zone comparison](fixed-zones.json) used seeds 1001–1003, the
same starting fuel and baseline equilibrium xenon for each pair, and the same
with-flow eight-bundle move in the highest-burnup channel with four direct
neighbours. Liquid zones were held at 50% for the power measurements. Retained
fuel carried its xenon and fresh fuel entered without xenon. The eigenvalue
solver normalized each shape to 2,064 MW thermal, including the supercritical
candidate. Zone worth used additional frozen-xenon solves at 0% and 100% fill.

| Measurement across the three seeds | Radial v9 | Diffusion only |
| --- | ---: | ---: |
| Half-fill Keff | 0.999959–1.000100 | 1.055392–1.055477 |
| Full-fill Keff | 0.996576–0.996735 | 1.051979–1.052107 |
| Liquid-zone worth, mk | 6.988–7.004 | 6.383–6.411 |
| Fuel-only loss, mk per full-power day | 0.436–0.456 | 0.419–0.431 |
| Peak initial channel power, kW | 6,608–6,760 | 6,280–6,532 |
| Peak initial bundle power, kW | 831–850 | 850–882 |
| Fuelled-channel power gain | 9.60–10.34% | 13.43–16.38% |
| Direct-neighbour power gain | 1.13–2.20% | 3.36–7.08% |
| Mean second-ring gain | 0.97–1.54% | 2.74–5.13% |

The native Browser bridge baseline completed 100 days with seed 1001 and the
oldest-LZC policy, which schedules two channels per day plus a third below
45% mean zone fill. It consumed 1,816 bundles and reached peak channel and
bundle powers of 6,859.5 and 853.7 kW. With the diffusion-only pack, startup
failed with approximately +49 mk of residual reactivity, so campaign fuel,
discharge and score statistics were unavailable.

A separate sensitivity trial scaled both neutron-production groups by
0.9478925226198047 and refit adjuster and water strength to 17 and 7 mk. Its
static half-fill Keff was 1.000000447. With the same seed and policy it completed
six days, then ended during the day-seven fuel moves at 910.54 kW in a bundle.
That trial retained the v9 Marshak axial boundary.

## Burnup-only criticality trial

The [burnup sweep](burnup-sweep.csv) used the diffusion-only candidate and
Core's existing seeded inventory generator. It varied the target mean
discharge burnup, scaling the existing axial exposure profile at each seed.
Fuel reaction tables and device strengths remained at v9 values. Every
reported solve used 50% liquid-zone fills, equilibrium xenon calculated for
that inventory, and fixed 2,064 MW thermal power.

For seed 1001, a target of 13.995 MWd/kg HM gave Keff 1.00000246 and a
starting peak bundle power of 1,059.75 kW. Across five seeds around the
14 MWd/kg target, starting peaks ranged from 988 to 1,060 kW, all above the
900 kW gameplay limit. At a target of 8 MWd/kg, seed 1001 was just under the
bundle limit at 897.83 kW, with Keff still 1.04304 at half fill.

If 14 MWd/kg became the steady mean discharge target at unchanged thermal
power and bundle mass, energy balance would imply approximately 7.16 bundles
per full-power day and 425 full-power days between channel visits, compared
with 16 bundles/day and 190 days today. This is an energy-balance estimate.
The trial only solved the starting inventories.
The seed-1001 equilibrium xenon solve failed to converge at 16 MWd/kg.
The sweep stopped there, so higher targets were not evaluated.


## V10 release candidate

Replaying day seven of the Marshak trial isolated the failure to the third
move, channel 322. The core peak was 833.54 kW immediately before the move
and 910.54 kW immediately after it. Both axial extrapolation trials, 0.12 m
and 0.22 m beyond each end face, completed the seed-1001 campaign. The 0.22 m
trial gave more bundle power margin and was selected.

The [active pack](../../packs/candu6-two-group-diffusion-pack-v1.json) keeps
1.5 cm fast and 0.75 cm thermal diffusion, the v9 radial circle, the 190-day
channel interval and the 16-bundle/day nominal fuel cycle. It uses an authored
0.22 m effective extrapolation at each axial end. The distance is common to
both groups and represents the game's effective end reflector. The pack
still derives conductance from each group's diffusion coefficient.

The [recorded fit](fit.json) applies the common production multiplier
0.9458763247001912 to both groups across all burnup knots. Fission heating,
fuel absorption, downscatter, and the xenon model retain their v9 values.
The fit restores half-filled Keff to 1.000000454 and frozen-Xe adjuster and
water worths to 17.00069 and 7.00003 mk. Frozen-Xe fuel loss measures 0.42716
mk/full-power day. The scoring reference is recalculated from v10 and
old physics checkpoints remain stored when replay is refused.

| Seed and pack | Days | Fresh bundles | Mean discharge, MWd/kg HM | Score | Peak channel, kW | Peak bundle, kW |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1001, v9 | 100 | 1,816 | 6.160 | 2,304.4 | 6,859.5 | 853.67 |
| 1001, v10 | 100 | 1,736 | 6.183 | 2,150.0 | 6,623.9 | 844.06 |
| 1002, v10 | 100 | 1,728 | 6.180 | 2,168.6 | 6,760.4 | 852.30 |
| 1003, v10 | 100 | 1,736 | 6.181 | 2,130.3 | 6,664.9 | 832.74 |

The [campaign summary](campaign-summary.json) records native Browser bridge
daily-turn results with the same oldest-LZC policy. Peaks are sampled
endpoints. The game enforces limits after each individual fuel move as well
as at the end of the turn. V10 averaged 17.28–17.36 bundles/day under this
policy. The nominal 16 bundles/day is the energy-balanced cycle reference.
Thermal energy was 4,953,600 MWh in every run. Sampled mean zone fills across
the v10 runs stayed within 43.89–50.98%.

In separate same-seed comparisons at the browser's 12-hour cadence, direct
neighbours gained 3.32–7.01% immediately with normal regulation and
2.74–6.03% at the half-day endpoint versus the no-fuel run. This uses the
release calibration and seeds 1001–1003.

Reproduce the fit and the seed-1001 campaign:

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-diffusion data/calibration/diffusion-v10/candidate.json tmp/diffusion-fit 2.2 0.22
dotnet run --project tools/LongRunPlaytest -c Release -- --pacing=daily-turn --policy=oldest-lzc --channels-per-day=2 --days=100 --seed=1001 --output=tmp/diffusion-campaign
```

A native 12-hour pacing run also completed 20 half-day steps on each of
seeds 1001–1003, fuelling the two oldest channels in the first half of each
day. Peak endpoint powers across those runs were 6,735 kW/channel and
856.26 kW/bundle.

The local AOT build passed Pages-subpath gameplay, documentation, and
same-pack update recovery. A separate checkpoint test verifies that a v9
checkpoint is kept and rejected before command replay on v10. The first
Windows AOT link crashed in `wasm-opt`; limiting Binaryen to one core via
`BINARYEN_CORES=1` completed the same AOT build locally.
The AOT reproduction matrix produced matching cold and warm digests for
all six cases, with no browser errors.
