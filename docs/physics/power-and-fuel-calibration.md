# Power and fuel calibration

The current [v3 reactivity calibration](reactivity-scale-v3.md) supersedes the
burnup-decay and zone-worth numbers below: approximately 0.5 mk per FPD and
7 mk total, with a full-core/LZC solve every three-minute browser step.
Thermal output, the fuel-cycle interval and heavy-metal mass remain unchanged.

The active pack now also includes the [power-limit spatial rebalance](power-limit-balance.md).
Numerical benchmark results below describe the earlier pack and retain their
original provenance. Thermal output and the authored fuel cycle remain unchanged.

The user-confirmed reference is 650 MW electrical and 2,064 MW thermal.
`PracticeReferencePowerWatts` remains an alias for the thermal reference.
Core burnup integrates thermal fission energy in joules. The Game physics
snapshot derives electrical output with a fixed authored ratio of 650/2064;
Browser serializes that value and Reactor Studio displays both quantities.
The electrical estimate does not change neutron balance or fission heating.

Benchmark reports now record thermal and electrical reference watts separately
and plots derive the thermal label from report metadata. Previous artifacts
retain the power and physics version used when they were generated.

## Applied channel-interval calibration

The user clarified that 180-200 means full-power days of dwell, rather than
200 MWd/kg exit burnup. Bundle residence and channel refuelling interval are
different for a 12-bundle channel with an eight-bundle shift: four of the eight
fresh bundles remain for two visits, and four leave at the next visit.
Mean bundle residence is therefore 1.5 times the channel interval.

At 190 days between channel visits, the steady throughput is 380*8/190 =
16 bundles/day; mean bundle residence is 285 days. Energy-balanced mean exit
burnup at 2064 MW thermal and 20.6 kg HM/bundle is 6.262136 MWd/kg HM.
At 190 days mean bundle residence, the channel interval is 126.666667 days,
throughput is 24 bundles/day, and exit burnup is 4.174757 MWd/kg HM.
These are steady-state energy balances, not computed depletion histories.

The user selected the second option: 190 full-power days between channel
visits, 16 fresh bundles/day at steady throughput, and 285-day mean bundle
residence. This is a nominal equilibrium reference for the instantaneous aged
snapshot, not a guarantee that every channel reaches its discharge target
under the prescribed axial surrogate and spatial power distribution.

Both runtime pack copies now use `candu6-two-group-diffusion-v1-cycle190-650mwe`.
The ten original burnup knots are remapped by 6.2621359223300965/16; fission
contrast about the fresh row is reduced to 0.4 while preserving group yields.
A common production multiplier of 0.9618135235607236 relative to the preceding
zones65 pack restores the seed-1001 half-fill critical reference. Positive,
bounded extrapolation adds 15, 20, 25 and 30 MWd/kg knots. Runtime lookups still
reject burnup outside the pack. These coefficients are synthetic game data.

Zone absorption slopes are refitted to 0.0015964146304331297 (fast) and
0.0006385658521732518 (thermal) per metre per unit fill. Empty zones still
contribute zero absorption. The offline fit gives 6.500115 mk total zone worth,
0.456124 mk for two eight-bundle old-channel refuels at hour 14, and 0.657122 mk
loss over one full-power day with zones frozen at half-fill. Live controller
results are checked separately; these fit numbers do not include zone response.

Reproduce the offline fit using `tools/AgedCoreBenchmark --fit-core OUTPUT
artifacts/core-fuel-calibration-190fpd-2026-09-30/source-pack.json`. The archived
source, proposal and fitting trials are retained. Applied pack copies include
updated provenance, version and a SHA-256 checksum of the compact sorted-key
coefficient table excluding its checksum field.

Validation artifacts are under `artifacts/core-cycle190-benchmark-2026-09-30`,
`artifacts/core-cycle190-refuel-response-2026-09-30`, and
`artifacts/core-cycle190-refuel-worth-2026-09-30`. Their reports contain actual
native seed trajectories, matched fuel/control runs, and independent tight
same-inventory refuelling solves. Thermal fission power is 2064 MW; electrical
output uses the authored 650/2064 conversion ratio.

## Baseline benchmark before independent criticality regulation

All six seeds complete 72 hours without refuelling or a terminal state (870
equilibrium samples). Independent tight solves give 0.677-0.772 mk/day mean
fuel-reactivity loss, with seed 1001 at 0.691 mk/day. Its average zone fill
falls from 50% to 25.685%. Across seeds, peak channel power is 13.249-15.501 MW
and peak bundle power is 2168.6-2553.1 kW; full extrema and zone distributions
are in the benchmark summary. Empty-to-full worth is 6.498-6.502 mk.

At hour 14, the oldest-channel pair 75/324 discharges fuel at approximately
6.24 MWd/kg. The independent fixed-initial-zones audit gives 0.454825 mk total
fuel worth (0.301711 and 0.153114 mk individually). In the live trajectory,
net reactivity rises to 0.461 mk while the combined spatial controller retains
fills temporarily. The correction occurs around hour 26; by hour 72 mean fill
is 31.192%, versus 25.685% in the matched control. Z4 and Z11 first reach empty at hour 71.5.
The reactivity and seven-pair plots show this limitation explicitly. These
are equilibrium corrections, not valve dynamics. Refuel worth varies with
channel age and spatial importance; 0.455 mk is this pair's measured gain,
not a guarantee for every sixteen-bundle selection.

Validation passed: 68 Core tests, 24 Game tests, 20 Browser bridge tests,
74 frontend tests, the production frontend build, and the local authoritative
WASM smoke path (startup, refuelling, zone geometry, Studio, and desktop sizes).
The change is applied locally; no deployment was performed.

The completed six-seed zone audit measures about -0.0651 mk per percentage
point of uniform fill. For seed 1001, fixed-initial-zones burnup loss is
2.0795 mk and actual zone movement gains 2.0832 mk. The equal-weight mean
drain is 24.315 percentage points, while the equivalent uniform drain is
31.926 points; individual zone importance explains the difference.

The retained-fill limitation above is fixed by [independent zone regulation](rrs-independent-regulation.md). The earlier benchmark artifacts remain unchanged as the before-fix record.
