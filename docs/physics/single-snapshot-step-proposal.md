# Single-snapshot stepping proposal

2026-10-01. Experimental native stepping script now defaults to 15-minute
(900-second) intervals with two spatial
checks of each frozen snapshot. Gameplay equations and the browser's controller
policy remain unchanged.

## What the four spatial solves mean

A scheduled regulation event runs at one simulation timestamp with one frozen
burnup/poison input. It can perform up to four complete two-group core solves:

1. Uncompensated baseline: solve without the liquid-zone absorption overlay.
   This supplies the uncompensated core reactivity and regional power observations.
2. Controlled baseline: solve with the previously accepted zone fills. Measure
   remaining reactivity and regional shape errors with the updated fuel/poison.
3. Verification: compute a small bounded 14-variable fill command, then solve
   the core with those fills. Keep it only when the measured result improves.
4. Correction: use the measured fill response to update the response model,
   compute a second command, and solve again to check it. This pass is conditional.

Each full spatial solve itself iterates over the fast and thermal group equations
until flux, eigenvalue, shape, and power checks pass. Four candidates therefore
mean repeated nonlinear/controller evaluations, not four timesteps or four energy
groups. The small fill-command algebra is cheap; repeated full-core solves dominate.

## Existing time integration already uses a frozen snapshot

Game scales the accepted local power/flux to the requested power level, holds its
shape fixed over the interval, and adds local fission energy `P_i * dt` to each
bundle. In the implemented units:

```text
delta burnup [MWd/kg HM] = P_i [W] * dt [s] / (mass_i [kg HM] * 8.64e10)
```

Iodine/xenon use the analytic coupled decay/production/burnout solution for that
frozen flux interval. Replacing that inexpensive analytic update with Euler is
not necessary to implement this proposal.

This model imposes a power amplitude on a steady shape; it does not integrate
prompt neutron kinetics. Changing the requested amplitude is not itself a
calculation of how a transient reactor reaches that power.

The current base pace advances 1,800 simulated seconds per wall second:
100 ms of UI time means 180 simulated seconds (three minutes). At 10x that is
30 minutes; at 60x it is three hours. The spatial refresh interval is currently
1,800 simulated seconds, and large requests split at those boundaries.

## Proposed experiment

The user's idea is a frozen-snapshot, explicitly coupled step. The main saving
would be reducing controller candidate solves. The two-check ordering is:

1. If there is no previous step, first settle the initial state using bounded
   full-controller passes at the same time, inventory and poison state. Require
   criticality and regional-shape convergence before starting the clock. Stop
   with an explicit failure if the pass budget is exhausted; consume no fuel
   and advance neither iodine nor xenon during initialization.
2. Otherwise always start from the last accepted snapshot: its fast and thermal
   flux, eigenvalue, zone fills, measured errors, target shape and stored controller
   response. Current burnup and iodine/xenon are the preceding interval's end state.
   Predict bounded zone fills analytically using its stored linear response model
   and residuals. This is an approximate fill-response calculation, not an
   analytical replacement for the two-group diffusion solve. Never substitute a
   uniform seed, the original startup state, or an uncompensated baseline on a routine step.
3. Perform the first spatial check using the predicted fills, with
   the previous accepted fast/thermal flux and eigenvalue as the iteration seed.
   This supplies an actual checked shape and k for those fills.
4. Use the first check's converged flux to measure regional powers and criticality
   error. Compute a bounded fine-tuning command from these current observations.
   Perform the second spatial check with those fills, seeded from the first
   check's flux and eigenvalue. Both checks use the same prepared fuel and poison
   state at the same timestamp; neither advances energy or isotopes. Keep the
   correction only when its checked result improves the prediction under the
   existing criticality-first acceptance rule. Otherwise keep the first result.
5. Normalize the accepted local power to the requested target. Integrate energy
   and analytic iodine/xenon over dt using that shape and flux.
6. Accept the checked snapshot and end-of-interval material/poison state together.
   A failed solve or integration leaves the previous accepted state intact.
   Carry the resulting material/poison state to the next step. Recheck or refresh
   the controller response when the residual or state change is too large.

If instead a solve is performed first and fills are changed afterward, its k and
shape describe the old fills. Criticality for the new fills is then an estimate;
it must not be presented as a checked equilibrium without another solve.

Small dt reduces the error from holding slowly varying coefficients and flux
fixed. It does not alone bound a fill-response approximation, cumulative drift,
or an instantaneous refuelling/layout change. A common fill correction can
target criticality, but fourteen fills also need a rule for regional power shape.
Criticality may be unattainable at the fill bounds; reserve/exhaustion semantics
must remain visible.

## Executable experiment

`tools/SingleSolveBenchmark` executes this order using shared Core rules. Run:

```powershell
dotnet build tools/SingleSolveBenchmark -c Release
dotnet run --project tools/SingleSolveBenchmark -c Release --no-build -- 180 20 2
# Previous one-check order for comparison:
dotnet run --project tools/SingleSolveBenchmark -c Release --no-build -- 180 20 1
```

Arguments are simulated step seconds (up to 1,800), interval count, and spatial
check count (1 or 2, default 2). The first interval uses the settled bootstrap
projection; each subsequent interval in the default mode solves two candidates,
starting from the immediately preceding accepted projection, even
if the solver's committed projection is older. The second starts from the first
candidate's converged flux. A second check is performed even if its fill command
is zero, keeping the work count explicit. Each interval integrates energy and the
analytic iodine/xenon update at 95% reference power. This is a native diagnostic,
not a browser command benchmark or a complete gameplay migration.

The experimental API returns immutable `PracticeSingleSolveStateV1` snapshots.
Bootstrap is bounded to eight controller passes by default, with explicit failure
if both controller tolerances are not met. Neither bootstrap nor candidate creation
commits the owning solver; the script commits after integration succeeds. Routine
steps report measured reactivity/shape residuals, without claiming convergence or
inventing an uncompensated reactivity measurement. The first prediction uses the
last accepted response model. After both checks, a measured fill secant updates
that model using only this frozen material/poison state; no secant is fitted
across changing coefficients from different timesteps. The one-check comparison
retains its response matrix and only refreshes baseline observations. Both checks
share the existing maximum movement budget of 0.08 fill per 1,800 simulated
seconds, scaled for short intervals and measured from the previous accepted fills.
The second check does not gain a separate movement allowance.

The JSON reports both checks' reactivity, separate and summed spatial iteration
counts, and whether fine tuning was accepted. On the seed-1001, twenty-interval,
180-second trace at 95% power, the nineteen routine two-check steps all met both
controller tolerances. Maximum absolute reactivity decreased from `1.3223e-5`
(one check) to `7.5911e-6` (two checks); maximum shape error decreased from
`9.4737e-5` to `9.3996e-5`. Twelve corrections were accepted. Routine candidate
count increased from 19 to 38, and total spatial iterations from 55 to 166.
Extending the two-check trace to ten simulated hours (200 intervals) also kept
all 199 routine steps within both controller tolerances, with the same peak
errors, 398 candidate solves and 1,080 spatial iterations. It accepted 109
corrections. These native traces verify the order and report its accuracy/work
tradeoff; they do not establish browser latency or behavior across other seeds,
power schedules, refuelling discontinuities and fill limits.

Fallback policies for discontinuities and excessive routine residuals, reference
comparisons and browser integration remain required before adopting this in gameplay.

## Measurement before changing gameplay

Keep the existing controller as the reference and shadow-test the alternative
over power changes, long xenon evolution, asymmetric refuelling, different aged
cores, and fills near their limits. Compare 60, 180, 300, 900, and 1,800 simulated
second steps, holding the total simulated duration and event schedule fixed.

Record criticality error, all fourteen fills, reserve, regional/axial power,
iodine/xenon, burnup, energy accounting, fallback frequency, solver iterations,
and command p95. Determinism must hold within each algorithm, although replay
digests are not expected to match different approximate algorithms.

The existing fill movement cap is 0.08 per regulation event. A smaller step
must not silently increase allowed movement per simulated hour; isolate the
integration experiment from changes in controller strength. Treat instantaneous
refuels/design edits separately from normal evolution.

Finally, count work per simulated hour: replacing four solves every 30 minutes
with one solve every three minutes increases solve calls from 8 to 20 per hour;
two checks every three minutes require 40 per hour (excluding startup).
Smaller changes may require fewer iterations per solve, but the total cost must
be measured. Keep a full-solve/controller fallback for discontinuities and failed
approximate steps; never hide negative/invalid densities or solver failures.
