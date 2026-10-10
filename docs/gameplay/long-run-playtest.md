# Long-run playtest: 101-day survival and 1×/10× comparison

Measured on 2026-10-04 against source commit `5a0e1da6fd092252192cfc821169e51a8338c60d`. [Machine-readable results](../../benchmarks/long-run-playtest-v1.json) include all orders, daily observations, peaks, poison inventories, energy, scores and speed differences. Half-hour observations remain in generated `tmp/longrun-*.jsonl` files.

## Finding

This report records the **historical bounded build**. Free practice has since
become endless with unlimited fresh fuel. The normal main-game factory now
survives the same 101-day strategy without overrides; see the
[channel-power maps and ripple measurements](channel-power-maps.md).

The historical Free practice game could not reach 100 days: Game ended the scenario at 30 days. Extending only that horizon to 101 days also did not produce a 100-day survivor with the former 128-bundle budget in any tested strategy. Full-power reserve-based selection survived 17 days 20 hours; 80% power survived 26 days 6 hours 30 minutes. These are measured strategies, not a proof that the channel-selection heuristic is globally optimal.

A separately marked modified-sandbox experiment extended the horizon to 101 days and supplied 2,048 fresh bundles. The same reserve strategy completed 101 days at full power at both 1× and 10×, using 189 orders / 1,512 bundles and retaining 536 bundles. The simulation, physics pack, initial aged core, power limits and scoring rules were unchanged. Those measurements preceded the endless browser default.

## Method

The tool consumes authoritative Game snapshots and issues ordinary Game commands. Each 10× advance is 100 wall ms / 1,800 simulation seconds. Its paired 1× advance is 1,000 wall ms / the same 1,800 simulation seconds, executing ten 3-minute control ticks. Shared Game still performs every scheduled 30-minute spatial/RRS solve, burnup update and iodine/xenon update. Both speeds receive exactly the same target and refuelling orders at the same simulation times. Refuelling uses channel flow and eight bundles.

The harness invokes the existing private session factory only to replace the scenario horizon. It uses the published browser speed constants. Additional fuel is supplied only when an explicit `--fuel` option exceeds 128, through `DebugGrantFreshBundles`, which marks the run modified. It never suppresses a loss, changes fuel quality, edits the geometry or substitutes an approximate simulator. A terminal run stops immediately.

Native speed comparisons inspect all 380 channel powers, all 4,560 bundle powers/burnups/identities, fourteen zone fills, mean channel xenon, Keff, tilt, score, delivered energy, inventory and ending state at every half-hour and accepted move. Raw state/replay hashes and version counters are not compared across speeds: the clocks and integration counts intentionally differ. Physical measurements are compared numerically; repeated runs at the same speed remain a separate deterministic-digest check.

## Tested campaigns

All campaigns target 101 days. All losses with the 128-bundle budget were caused by average LZC below 10%. Refuelling strategies exhausted their stock before the loss; the idle run left its 128 bundles unused. The tested channel and bundle powers stayed below 7,300 kW and 935 kW; absolute tilt stayed below 20%.

| Policy/run | Seed | Target | Starting bundles | Days reached | Orders | Points | Ending |
| --- | --- | --- | --- | --- | --- | --- | --- |
| idle-10x | 1001 | 100% | 128 | 9.4375 | 0 | 199.75 | LZC average level below 10% |
| reserve-pair | 1001 | 100% | 128 | 17.8333 | 16 | 376.34 | LZC average level below 10% |
| oldest-pair | 1001 | 100% | 128 | 17.6250 | 16 | 363.73 | LZC average level below 10% |
| low-power-pair | 1001 | 80% | 128 | 26.2708 | 16 | 123.44 | LZC average level below 10% |
| seed1002 | 1002 | 100% | 128 | 17.8750 | 16 | 387.68 | LZC average level below 10% |
| seed1013 | 1013 | 100% | 128 | 17.7500 | 16 | 376.45 | LZC average level below 10% |
| extended-fuel-pair | 1001 | 100% | 2048 | 101.0000 | 189 | 2059.26 | Practice horizon completed |

`oldest` uses the highest mean burnup when LZC falls below 30%; this measures its channel selection, not continuous repeated use of the N/R shortcut. `reserve` uses the heuristic below. Seeds 1002/1013 were single-speed 10× sensitivity checks; the reserve, oldest, low-power and extended-fuel seed-1001 campaigns were paired 1×/10× runs.

## Channel-selection algorithm

1. Observe after every 30 minutes. At mean LZC below 30%, order a refuel if at least eight bundles remain.
2. Consider refuellable channels that have not been refuelled within 30 days.
3. Rank by `meanBurnup × referenceChannelWatts × (1 + 3 × powerDeficit) × bundleHeadroom`.
4. Power deficit is the positive fractional shortfall relative to that channel's reference at the selected power target. Bundle headroom is `clamp((935000 − observedPeakBundleWatts / targetFraction) / 250000, 0.05, 1)`.
5. Break ties by channel index, insert eight bundles with flow, and inspect the resulting authoritative snapshot. A move has no rollback or predicted-physics preview.

This favors depleted channels that contribute useful reactivity and avoids prioritizing channels with little fresh-fuel power headroom. It is a deterministic player heuristic. The full-power run's first order raised mean LZC from 29.47% to 32.56%; its selected channel rose from about 5977.0 to 6662.3 kW. Fuel replenishment remained the limiting resource.

## Full-power run with the shipped fuel stock

| Order | Day | Channel index | Mean burnup MWd/kg | LZC before → after % | Channel after kW | Peak bundle after kW | Stock after |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 4.9375 | 224 | 5.44 | 29.47 → 32.56 | 6662.3 | 676.9 | 120 |
| 2 | 5.4792 | 173 | 5.37 | 30.00 → 32.54 | 6116.4 | 618.2 | 112 |
| 3 | 6.0208 | 180 | 5.38 | 29.90 → 33.08 | 6794.3 | 691.3 | 104 |
| 4 | 6.6667 | 241 | 5.56 | 29.87 → 32.90 | 6558.9 | 670.2 | 96 |
| 5 | 7.3125 | 75 | 5.58 | 29.68 → 32.47 | 6307.1 | 641.4 | 88 |
| 6 | 7.9583 | 324 | 5.59 | 29.28 → 31.84 | 6052.6 | 614.5 | 80 |
| 7 | 8.5000 | 186 | 5.50 | 29.18 → 31.43 | 5715.5 | 579.7 | 72 |
| 8 | 8.8542 | 91 | 5.55 | 29.67 → 32.55 | 6397.9 | 651.6 | 64 |
| 9 | 9.5208 | 327 | 5.60 | 29.26 → 31.57 | 5756.4 | 582.9 | 56 |
| 10 | 9.8750 | 197 | 5.38 | 29.80 → 32.84 | 6635.3 | 675.9 | 48 |
| 11 | 10.5208 | 118 | 5.65 | 29.61 → 32.49 | 6363.1 | 649.9 | 40 |
| 12 | 11.1667 | 214 | 5.63 | 29.26 → 31.38 | 5526.5 | 560.6 | 32 |
| 13 | 11.5208 | 249 | 5.35 | 29.61 → 32.33 | 6325.9 | 641.5 | 24 |
| 14 | 12.0000 | 36 | 5.39 | 29.93 → 31.93 | 5478.6 | 552.1 | 16 |
| 15 | 12.5417 | 336 | 5.47 | 29.27 → 31.37 | 5560.4 | 562.3 | 8 |
| 16 | 12.8958 | 134 | 5.37 | 29.62 → 32.76 | 6760.0 | 686.7 | 0 |

Stock reached zero on day 12.8958. Mean LZC was 32.76% on day 13, 27.85% on day 14, 23.56% on day 15, 18.44% on day 16 and 14.16% on day 17. On day 17.8333 it crossed the lower limit at 9.84%.

Final score was 376.34. Delivered energy was 883392 MWh thermal / 278200 MWh electrical estimate. The campaign peak was 6800.5 kW/channel and 692.7 kW/bundle. Maximum absolute tilt was 1.30%.

## 101-day modified-sandbox run

| Day | Mean LZC % | Tilt % | Current peak channel kW | Current peak bundle kW | Orders | Stock | Points |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 50.00 | 1.29 | 6505.3 | 664.2 | 0 | 2048 | 0.00 |
| 5 | 32.56 | 1.24 | 6707.8 | 683.2 | 1 | 2040 | 105.66 |
| 10 | 32.84 | 1.09 | 6772.7 | 691.0 | 10 | 1968 | 211.06 |
| 20 | 30.21 | 0.71 | 6767.4 | 686.9 | 29 | 1816 | 419.24 |
| 30 | 30.75 | 0.14 | 6762.9 | 688.7 | 49 | 1656 | 622.67 |
| 50 | 31.71 | -0.64 | 6837.6 | 698.5 | 88 | 1344 | 1021.39 |
| 60 | 30.02 | -0.78 | 6836.4 | 694.0 | 107 | 1192 | 1217.65 |
| 75 | 32.46 | -0.80 | 6904.2 | 705.8 | 138 | 944 | 1517.85 |
| 90 | 31.47 | -0.65 | 6823.2 | 694.7 | 168 | 704 | 1828.56 |
| 100 | 29.31 | -0.45 | 6990.5 | 714.0 | 187 | 552 | 2038.48 |
| 101 | 30.49 | -0.39 | 7097.9 | 725.9 | 189 | 536 | 2059.26 |

Campaign extrema: LZC 29.14–50.00%; absolute tilt at most 1.30%; channel peak 7127.8 kW; bundle peak 729.3 kW. Final LZC was 30.49%, signed tilt -0.39%, score 2059.26, thermal energy 5003136 MWh, and electrical estimate 1575600 MWh. No operating limit was crossed. The ending was the extended horizon completing, not a reactor loss.

## Speed comparison

The 101-day pair had 4,848 matched half-hour advances and 189 matched refuels. Fuel identity, inventory, ending reason and ending time agreed. The following maxima cover the entire trajectory, not just the final snapshot:

| Measurement | Maximum absolute difference, 1× vs 10× |
| --- | --- |
| simulationSeconds | 0.000e+0 |
| score | 5.548e-11 |
| thermalMwh | 5.821e-11 |
| lzcFraction | 1.767e-13 |
| tiltFraction | 1.943e-16 |
| effectiveK | 1.776e-15 |
| zoneFillFraction | 1.769e-13 |
| channelPowerWatts | 1.397e-8 |
| channelXeRelative | 7.105e-15 |
| bundlePowerWatts | 3.143e-9 |
| burnupMwdPerKg | 1.883e-13 |

The differences are floating-point rounding at negligible scales; the runs were physically equivalent for the tested inputs. This does not promise bitwise identity between different speeds or prove every possible target/move timing is equivalent. The browser live scheduler also excludes solver wait time from its clock, so measured real-time throughput need not be exactly ten times faster at 10×. At 10× there is one operator opportunity every 30 simulated minutes; at 1× it is every 3 minutes. The paired test aligns decisions to the coarser grid.

## Deployed browser verification

The deployed AOT WASM build at https://hkkho.github.io/CRS/ was tested at both speeds with the same 16 orders. Each run ended on day 17.833333 with zero stock and low LZC. Across 33 matching order/day/terminal checkpoints, there were 0 lifecycle/inventory mismatches and 0 browser errors. Maximum channel-power difference was 6.519e-9 W; maximum LZC difference was 7.044e-14 of full scale. The browser reproduced the native ending time and score. Studio was opened through Begin shift, paused through its native control, and captured in `tmp/longrun-browser/studio-start.png`.

The separate hosted reproduction matrix accepted all six paused/live refuelling cases across three representative channels, matched deterministic digests, and reported no console/page errors. The deployed commit matched the source commit above.

## Reproduce

From the repository root:

```powershell
dotnet run --project tools/LongRunPlaytest -c Release -- --policy=reserve --days=101 --pair=true --output=tmp/longrun-reserve-pair
dotnet run --project tools/LongRunPlaytest -c Release -- --policy=reserve --days=101 --power=0.8 --pair=true --output=tmp/longrun-low-power-pair
dotnet run --project tools/LongRunPlaytest -c Release -- --policy=reserve --days=101 --fuel=2048 --pair=true --output=tmp/longrun-extended-fuel-pair
```

Other policies: `none`, `oldest`, `daily` (two orders/day). Options include `--seed`, `--threshold`, `--power`, `--days`, `--fuel`, `--pair`, and `--output`. Using `--days=30` consumes the normal browser factory with no horizon override. JSON summaries and move files are written beside half-hour JSONL telemetry. A significant speed difference makes the tool fail after saving the report.

The earlier two-speed browser replay harness has been retired. Its source remains
in Git history. Current browser releases use daily-turn acceptance and deterministic
WASM reproduction; current 100-day campaigns use `tools/LongRunPlaytest` with
`--pacing=daily-turn`. See [daily turns](daily-turn-mode.md) and the
[v8 campaign](../../benchmarks/axial-marshak-v8-2026-10-10/README.md).

## Validation and limits

The harness builds in Release with zero warnings/errors and its short paired refuelling/added-fuel smoke passes. `tools/Test-DotNet.ps1 -Suite Game` passed all 70 tests. `tools/Test-Browser.ps1` passed 37 browser contract tests, 129 frontend tests and the production frontend build. No shared runtime, browser defaults, deployment configuration or existing gameplay behavior was edited.

Only the extended-fuel campaign was run through 101 days, and only for seed 1001. The budgeted campaigns stop on the first actual loss; they cannot advance a dead reactor to day 100. No search over every seed, ordering or power schedule was attempted. The observed 1,512-bundle consumption is a successful budget for this run, not a proven minimum. A playable 100-day product mode would need an explicit duration/fuel-balance change and its own acceptance tests.
