# Two-check spatial campaign benchmarks

The experimental shared Core solver predicts zone fills using the last accepted
state, checks that prediction with diffusion, then checks a correction based on
the measured flux. Both checks use the same frozen fuel and iodine/xenon snapshot.
Startup first seeks self-consistency. Routine movement scales with elapsed time;
instantaneous refuelling gets one shared event movement budget and advances no
energy or isotope time. The browser still uses the established Game controller.
The experimental benchmark now defaults to 15-minute (900-second) intervals;
the browser controller remains at 30 minutes. Each routine interval performs
two spatial checks, with one elapsed-time-scaled movement budget.

The current controller criticality criterion is inclusive ±0.05 mk, or
dimensionless rho ±5e-5. Regional fraction convergence now allows 0.01, or one
percentage point of total power; diffusion iteration tolerances are unchanged.
Controller identity v4 records this shape change. The historical campaigns below
used the earlier 1e-4 regional criterion. The fresh complete matrix and matched
AOT browser comparison are in the [deployment assessment](shape-tolerance-deployment-readiness.md).
Changing criteria changes command selection and acceptance as well as the
reported convergence flag, so old trajectories cannot simply be relabelled.

## Method

Use six aged seeds (0, 1001, 1002, 1003, 1004, 4294967295), each for 72 simulated
hours, with analytic iodine/xenon enabled. Power targets are 100%, 95% at 120 s,
and 100% at 360 s. Compare the authoritative Game reference, a matched direct
Core reference, two checks every 1800 s, and two checks every 180 s. Integration
is split at common 180 s control boundaries and startup events. Sample at half
hours and immediately before/after fuel operations.

The seed-1001 fuelling case replaces eight bundles in each of channels 75 and
324 at hour 14, sequentially at the same timestamp. Actions selected by the Game
reference are replayed unchanged into all Core policies. Timing is native
Release, serial CPU, with profiling scopes compiled out. Each case is one
measured trajectory, so timings are descriptive and do not establish browser
latency. Candidate counts exclude bootstrap and include fuel events.

Reproduce from the repository root:

```powershell
dotnet run --project tools/SingleSolveBenchmark -c Release -- --campaign artifacts/two-check-campaign-005mk-2026-10-01
python tools/SingleSolveBenchmark/report-campaign.py artifacts/two-check-campaign-005mk-2026-10-01
```

Use a separate output directory for each tolerance or physics-pack revision.
`--resume` rejects a different criterion or pack. If startup fails, preserve the
failure and use `--recover-initialization <directory>` for a separately reported
32-pass bootstrap diagnostic; routine solving and tolerances are unchanged.

Run the 15-minute campaign against the preserved ±0.05 mk comparison:

```powershell
dotnet run --project tools/SingleSolveBenchmark -c Release -- --fifteen-minute-campaign artifacts/two-check-campaign-15min-2026-10-01 artifacts/two-check-campaign-005mk-2026-10-01
python tools/SingleSolveBenchmark/report-campaign.py artifacts/two-check-campaign-15min-2026-10-01
```

This measures only the new 15-minute trajectories. The output retains the earlier
reference, three-minute and thirty-minute measurements with their provenance;
it does not overwrite them. Startup recovery remains separately reported.

## Earlier ±0.01 mk baseline

Full precision is in `artifacts/two-check-campaign-2026-10-01/comparison.json`,
its generated `summary.md`, and `initialization-recovery.json`. Both two-check
policies failed the default eight-pass bootstrap for seed 4294967295. A separate
32-pass bootstrap enabled that seed to complete all 72 hours.

Including this explicitly bounded recovery, the six Core references took
260.69 s; two checks every 30 minutes took 69.97 s (73.2% less), and every three
minutes took 222.65 s (14.6% less). The matched Game/Core reference endpoints
agreed to 2.51e-14 relative node power across six controls and the fuelled case.

The 30-minute variant had peak regional fraction error 0.00861, compared with
0.00478 for the reference; the three-minute variant reduced it to 0.000166.
Controller shape convergence was still missed at some checks. Endpoint zone
fills differed by up to 26.48 percentage points, node power by up to 4.19% of
reference peak, and bundle burnup by up to 0.00658 MWd/kg. Fast computation alone
does not establish equivalence of these control policies.

The fuelled 72-hour reference took 68.91 s, versus 13.94 s at 30 minutes and
39.68 s at three minutes. The first three-minute-policy fuel event left
-0.04140 mk, outside the old ±0.01 mk band but within ±0.05 mk. A fresh wider-band
campaign is required because acceptance decisions themselves change.

## Current ±0.05 mk results

Full traces and the generated comparison are in
`artifacts/two-check-campaign-005mk-2026-10-01`. Both two-check policies again
exhausted the default eight-pass startup budget for seed 4294967295; separate
32-pass diagnostics completed both 72-hour campaigns with unchanged criteria.
The following six-seed totals include those successful diagnostic runs and
exclude the failed startup attempts:

| Core policy | Earlier ±0.01 mk, seconds | Current ±0.05 mk, seconds | Current time reduction versus reference |
|---|---:|---:|---:|
| Established controller / 30 minutes | 260.69 | 399.09 | — |
| Two checks / 30 minutes | 69.97 | 74.81 | 81.3% |
| Two checks / 3 minutes | 222.65 | 210.99 | 47.1% |

These are single trajectories. The wider band changes controller choices:
it does not guarantee faster calculation, and the larger established-controller
cost makes its relative speed comparison different from the earlier baseline.

All completed three-minute cases, including fuelling, stayed within ±0.05 mk.
The fuelled case peaked at 0.04140 mk, took 38.27 s versus 77.84 s for the Core
reference, and missed the shape criterion on 49 of 1442 checks. Across the six
no-refuel three-minute campaigns, peak regional fraction error was 0.000162;
the regional criterion therefore remains a separate limitation.

The 30-minute no-refuel cases stayed within the criticality band, but the first
fuel operation reached -0.05451 mk. Its 15.48 s fuelled trajectory cannot be
described as meeting the new criticality criterion throughout. A stronger event
correction or fallback remains necessary before adopting this cadence in gameplay.

The benchmark harness initially grouped energy multiplication differently from
Game. Matching `power * (amplitude * stepSeconds)` resolved the reference
outlier on seed 4294967295; its original trace is preserved in
`before-rerun-reference-core-1800-4294967295-False.json`. Across all seven paired
Game/Core endpoints, maximum power discrepancy is now 1.98e-8 relative to peak,
fill discrepancy 8.06e-6 percentage points, and burnup discrepancy
3.66e-10 MWd/kg. The other reference traces remain within these comparison
limits. Generated-energy accounting error was below 2.7e-15 relative.

Validation passed 101 Core, 31 Game, 26 Browser bridge and 81 frontend tests,
plus the production frontend build. The acceptance boundary is tested at both
signs and just beyond ±0.05 mk; excessive shape error still rejects convergence.

## Selected experimental cadence: 15 minutes

The 15-minute campaign is in `artifacts/two-check-campaign-15min-2026-10-01`.
Six 72-hour control cases took 73.42 s in total, including the successful
32-pass startup diagnostic for seed 4294967295 and excluding its failed
eight-pass attempt. Each completed control has 288 routine intervals and 576
spatial checks. This is 81.6% less native CPU time than the preserved reference
measurement (399.09 s); these are single trajectories measured in separate runs.

The fuelled case completed 72 hours in 14.30 s, with 580 checks including the
two fuel events. Its peak absolute reactivity was 0.04692 mk, within ±0.05 mk
throughout. All six completed controls also stayed in that band, peaking at
0.01702 mk. Maximum no-refuel regional fraction error was 0.000458, compared
with 0.000162 at three minutes and 0.00257 at thirty minutes. The regional
criterion is still missed at some checks (31 of 290 in the fuelled case).

Only the experimental benchmark default changes to 900 s. Browser gameplay
retains its established controller and 1800 s cadence. The shared ±0.05 mk
criterion and elapsed-time-scaled experimental movement budget are unchanged.
