# Channel ripple scoring — current policy v3

`practice-channel-ripple-v3` scores closeness to a fixed **380-channel reference**.
The nominal thermal total remains **2,064 MW**, or **5.431579 MW/channel** on
average. Each channel has its own target; see
[reference derivation](../physics/channel-power-reference.md).

For channel `c`, ripple ratio is `r[c] = actual thermal watts[c] / reference watts[c]`.
The target ratio is 1. The equal-channel RMS deviation is
`e = sqrt(sum((r[c] - 1)^2) / 380)`. Each authoritative burnup integration
interval earns `hours / (1 + (e / 0.10)^2)`, using the accepted shape and actual
power amplitude for that interval. No wall-time or browser calculation affects
points. Paused time earns nothing. Maximum is 1 point/hour (24/day, 720/30 days).
A 5%, 10%, or 20% RMS deviation earns 0.8, 0.5, or 0.2 points/hour.
The 10% scale is an authored gameplay curve, not a plant operating limit.
The continuous curve rewards improvements even above 10% deviation.

The reference is fixed at nominal full thermal power. Changing the operator's
power target changes actual watts but does not move the scoring target. This
also measures bulk power departure rather than silently normalizing it away.
Tilt is still a displayed operational measurement; it is no longer a separate
score term. All channels count equally, including low-power peripheral channels.

Refuelling earns no immediate reward and carries no direct point deduction.
Its effects on channel powers determine subsequent points. The compatibility
refuelling breakdown publishes zero reward/cost/net points. Fuel stock, burnup
history, useful-discharge progress, challenge outcome and badge rules continue
to describe the run independently of scoring.

The browser receives reference watts, all ripple ratios, RMS deviation and the
current points/hour from Game in both full and compact snapshots. Studio shows
the selected channel's actual/target watts and ratio, plus core RMS and rate.

Reproduce the reference and three starting-seed readings:

```powershell
dotnet run --project tools/ChannelReferenceBenchmark -- tmp/channel-reference
```

The previous v2 balance study below is historical; its rewards, rankings and
reported totals do not apply to v3.

---

## Historical gameplay score balance — task 06

The old operating score could contribute 43,200 points per ideal day, compared
with at most 48 points for an eight-bundle discharge. On seed 1002, the tested
idle policy outscored the considered policy despite missing the challenge.

## Authored policy v2

`practice-fuel-and-operation-v2` earns at most **1 operating point per simulated
hour**: `hours * (0.7 * powerQuality + 0.3 * tiltQuality)`. The existing quality
measurements and thresholds remain unchanged, as do physics and per-bundle
refuelling rewards/costs. Eight discharged bundles at 6 MWd/kg earn 24 net points,
equal to one ideal day of operating score. Eight discarded fresh bundles cost
12 points. Over a 30-day free-practice horizon, the operating cap is 720 points;
the 128-bundle inventory's maximum net discharge reward is 768 points.

The one-day challenge remains 24 hours with 128 available fresh bundles. The
successful policies below used 16 bundles, achieved eight useful discharges and
retained headroom; changing duration or stock was unnecessary for this initial
balance correction. No button-acceptance bonus or mandatory action was added.

## Controlled comparison

All policies use the authoritative Game/Core session, target 100%, the same
24-hour horizon and three-hour advance intervals. No scores are based on CPU
wall time. The highest-burnup and considered policies each make eight-bundle
moves at the start and 12-hour mark. Highest-burnup uses the default direction;
considered evaluates both discharge directions using Core inventory candidates
and chooses the greatest actual discharge reward, with deterministic ties. It
is a bounded scripted strategy, not a claim of globally optimal reactor control.
Waste/reversal starts with the same productive move, then performs three
immediate opposing shifts that discard newly inserted fuel.

| Seed | Policy | Before total | Tuned total | Operating | Net discharge | Fuel used | Useful discharged | Outcome |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1001 | no-refuels | 39953.49 | 22.20 | 22.20 | 0.00 | 0 | 0 | missed |
| 1001 | highest-burnup-default-direction | 39858.29 | 50.04 | 22.13 | 27.91 | 16 | 0 | missed |
| 1001 | waste-reversal | 39438.76 | 11.28 | 21.92 | -10.64 | 32 | 4 | missed |
| 1001 | considered-discharge | 40207.36 | 73.12 | 22.31 | 50.81 | 16 | 8 | success |
| 1002 | no-refuels | 42029.11 | 23.35 | 23.35 | 0.00 | 0 | 0 | missed |
| 1002 | highest-burnup-default-direction | 40830.14 | 50.58 | 22.67 | 27.91 | 16 | 0 | missed |
| 1002 | waste-reversal | 41628.12 | 12.49 | 23.13 | -10.64 | 32 | 4 | missed |
| 1002 | considered-discharge | 41946.40 | 74.09 | 23.28 | 50.82 | 16 | 8 | success |
| 1013 | no-refuels | 40130.84 | 22.29 | 22.29 | 0.00 | 0 | 0 | missed |
| 1013 | highest-burnup-default-direction | 39332.97 | 49.77 | 21.84 | 27.93 | 16 | 0 | missed |
| 1013 | waste-reversal | 39628.88 | 11.38 | 22.02 | -10.64 | 32 | 4 | missed |
| 1013 | considered-discharge | 40161.08 | 73.12 | 22.28 | 50.83 | 16 | 8 | success |

Every trial delivered **15,600 MWh estimated electrical / 49,536 MWh thermal**
and reached **86,400 simulated seconds**. Comparing baseline with tuned output
checks energy, survival, run status/reason, outcome, badge status, fuel/useful
counts, discharge components and reserve for exact equality. Only operating
points, total score and scoring-policy identity changed. Across all three seeds,
considered discharge beats default-direction discharge, which beats waiting,
which beats repeated fresh reversal. The new operating contribution matches the
old contribution divided by 1,800 within 1e-9.

Raw results: [gameplay-balance-v2.json](../../benchmarks/gameplay-balance-v2.json).

## Reproduce

```powershell
dotnet run --project tools/GameplayBalanceBenchmark -c Release -- tmp/gameplay-balance.json
dotnet run --project tools/GameplayBalanceBenchmark -c Release -- --compare tmp/gameplay-balance-baseline.json tmp/gameplay-balance.json
```

The optional comparison accepts a historical v1 baseline or the same policy
version for repeat checks. The committed raw file retains the measured v1/v2
reports; extract its `baseline` array to a temporary JSON file for the historical
comparison command. These are synthetic gameplay measurements, not plant validation.

This is an initial deterministic balance pass. Human completion/restart-rate
observations remain task 05's follow-up. Other targets, deliberate geometry edits,
longer runs, more seeds and optimal policies were not assessed here; standard vs
modified-run comparison belongs to task 12. No leaderboard is introduced.
