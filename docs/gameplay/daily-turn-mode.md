# Daily-turn refuelling mode

Status: implemented. Daily turns are the default for new browser test runs.
The delivery slices below document the implementation and acceptance contract.
The explicit `?pacing=real-time` developer URL retains the legacy loop.
See [local verification and calculation time](../performance/daily-turns.md).

## Product decision

Replace the real-time browser play loop with a daily decision loop: inspect the
core, choose the channels to refuel today, then advance one simulated day. The
reactor remains at power; waiting or inspecting never advances time. Keep the
existing endless Free practice objective and unlimited fresh fuel as the default.
Pacing and objectives are separate: the optional one-day challenge also uses daily
turns, with its existing fuel budget, objective and badge rules.

Daily turns are the launcher, initial initialization and new-run default.
Retry same seed and Try new seed preserve the run's objective and pacing mode.
Real-time pacing stays available only as an explicit legacy/developer option for
comparison and old-save replay, outside the normal test loop. Do not switch pacing
inside an active run in the first release.

## Player loop

1. Begin on **Day 1**, at elapsed simulation time zero. Inspect the existing
   power/burnup maps, channel inspector, watchlist and history.
2. Use **Add to today's fuel plan** on any eligible channel. Each entry means one
   eight-bundle move with that channel's flow. Highlight selected channels and
   list their names, outgoing burnup and total fresh bundles required.
3. Remove entries or clear the plan freely. Selecting a channel for inspection
   does not add it. Highest burnup remains an inspection shortcut. Planning
   changes no fuel, power, poison, score or simulation time.
4. Use **Refuel & advance 1 day** to commit the plan. With an empty plan, label
   the same action **Advance 1 day without refuelling**; skipping fuel is valid.
5. Apply today's moves at the current day boundary, then run 86,400 simulated
   seconds. Return to a frozen **Day 2** decision state with the updated core,
   consumed fuel, delivered energy, score and daily result. Repeat indefinitely
   until an existing operating limit ends the run.

No new fuel quotas or cooldowns are introduced. A channel may appear only once
per day; there is no initial gameplay cap on the number of distinct eligible
channels. Game validates the total against stock for finite objectives. All
planned channels execute in ascending authoritative channel-index order, shown
in the plan; click order does not affect the result.

Replace pause/resume, speed buttons and the one-hour step with the daily action
and plan controls in normal Studio. Keep rated power at the existing reference
target for the first daily release; retain power-target experiments in legacy
developer pacing. Update the R shortcut to add/remove the inspected channel and
the on-screen help accordingly. Space retains native button activation and no
longer toggles playback. Provide native Add/Remove/Clear/Advance buttons, stable
focus, keyboard map operation, 320px reflow and quiet announcements.

## Authoritative turn contract

`ReactorSim.Game` owns the pacing mode, committed day count, plan validation,
refuelling execution, time advance, terminal decision and immutable daily result.
Use a dedicated simulation-time advance in Game/PracticeRunClock rather than
having Browser resume the clock and convert a day into wall milliseconds.
`IsPaused` can remain true between daily decisions, but an explicit pacing field
must distinguish a daily run from a paused real-time run.

Proposed inputs and outputs (names to finalize during implementation):

```text
initialize/reset: pacingMode = "daily-turn" | "real-time"
commit-day: expectedCompletedDays, channelIndices[]
snapshot: pacingMode, completedDays, lastDayResult
```

Keep bridge session `mode: "play"` separate from pacing. New browser starts with
omitted pacing select daily turns. Reset without pacing preserves the active
run's pacing. Channel inputs are unique integer indices within the 380-channel
core. Game derives flow, shift count and existing fuel type; the client does not
send or implement alternative reactor rules. `expectedCompletedDays` and the
existing compact `baseSequence` guard stale or repeated submissions.

Before any mutation, reject a terminal run, wrong pacing, stale day, malformed or
duplicate indices, ineligible channels, or insufficient total stock. Rejected
commands preserve the reactor state and last accepted daily result. A numerical
failure while constructing the turn also rejects the entire turn without
consuming fuel or time: build refuelling and advance on detached candidates and
commit once. The daily session forks the clock and solver commit owner to provide this
multi-operation guarantee.

Check limits after every accepted fuel move and at the final day boundary. A
physical limit crossing is an accepted terminal outcome: stop on a violating
fuel move, or commit the completed one-day update with its final loss reason.
Keep existing strict limits and allowed equality. Daily turns do not calculate
intermediate intraday states. Respect a finite objective's remaining horizon.
A normal day advances exactly 86,400 seconds; a fuel-move loss advances no time.

Use one large simulation interval for each daily decision. Freeze the accepted
post-refuelling power/flux over that interval, integrate bundle fission energy,
analytic iodine/xenon and operating score once, then perform one final spatial
and LZC equilibrium solve. Do not subdivide the day into real-time ticks. The
real-time developer path retains its 180-second cadence and solver tolerances.
Physics cadence identity `practice-daily-one-step-v1` distinguishes this model
from the earlier 480-step implementation; its daily saves cannot be silently
replayed using the new integrator. Bundle identities, inventory, provenance and
challenge eligibility retain their existing contracts.

The daily result includes start/end simulation times, requested/executed/unexecuted
channels, every accepted immutable fuel movement, fuel used, useful discharges,
score and energy deltas, end-state metrics, and any terminal reason. Existing
`LastFuelMovement` is insufficient for a multi-channel turn. Keep one last daily
result and bounded history; do not accumulate full-core snapshots for every
internal step indefinitely.

## Browser, history and saves

Browser publishes the command, pacing and daily result in capabilities, full and
compact responses, mappings, validators and digests. Update shared serialized
fixtures and make compatibility explicit. A client with daily controls requires
the daily capability and fails closed if an older WASM host lacks it. Keep
legacy command behavior stable for explicitly real-time runs. In daily runs,
reject `advance`, `step`, resume/live-speed and direct `commit-refuel` commands
that could bypass daily planning; inspection remains available.

The controller never starts the wall-clock scheduler for a daily run, including
after Begin, visibility changes, navigation, restore or status updates. Keep the
draft as shared browser view state across Studio navigation; submit it once as
one foreground command. Disable plan mutations and repeat submission while a
turn is pending. Clear the draft on accepted day/reset; retain it on a known
rejection. Do not automatically retry a timeout whose outcome is unknown.

Record day boundaries and confirmed fuel movements in history. Existing history
inspection must retain coherent complete observations and disable operating
commands. Do not manufacture within-day full-core samples by interpolation.
The daily report supplies aggregate deltas; internal-step history is deferred.

Save the explicit pacing mode and exact `commit-day` inputs in the journal. A
new save version also preserves the unsubmitted draft separately from simulation
commands, validating it against the restored snapshot. Restore into an isolated
worker and verify the authoritative state digest before adoption. Missing pacing
in version-1 saves means legacy real-time: replay with the original initialization
and digest policy, rather than applying the new default. If legacy verification
cannot be retained, reject the old save clearly while preserving the current
run. Never silently reinterpret old command history as daily turns.

## Delivery slices

| Slice | Ownership and concrete result | Focused acceptance |
| --- | --- | --- |
| 1. Shared turn | Game contracts, GameSession, PracticeRunClock and factory; detached multi-operation turn, explicit simulation-time advance and immutable result. Core changes only if required to reuse existing candidates. | Empty, single and multiple-channel days; exact duration; canonical order; stock/geometry/duplicate/stale rejection atomicity; numerical rollback; fuel-move and day-end terminal decisions; all movements retained; challenge horizon and provenance. Compare repeated daily runs, assert one clock interval/bundle exposure, and verify analytic frozen-flux poison and energy accounting. |
| 2. Bridge | Browser runtime, DTOs, mapping, capabilities and digests; BrowserHost only where exports require it. | Full/compact parity, capability negotiation, hostile inputs, stale base sequence/day, repeat submission, early-ending result and deterministic journal replay; legacy fixtures remain explicit real-time. |
| 3. Playable browser | Protocol, controller, Studio navigation/order/map/status views, launcher, announcements, history, runSave and restore. | Default initialization, zero automatic ticking, editable multi-channel plan, no-fuel day, pending/error behavior, day report, history guards, reset/retry semantics, save/draft round trip, keyboard and narrow layout. |
| 4. Default rollout | Product docs, smoke/reproduction scripts and Pages workflow acceptance. | New run uses daily turns without a flag; actual worker/WASM loop advances a full day; existing one-day challenge and save paths pass; explicit real-time reproduction remains available. |

Keep `web/candu-playtest` runnable during maintenance. README and
IMPLEMENTATION_GUIDE describe the delivered daily loop. Existing cloud databases
require the `daily_turn_saves` migration to accept version-2 saves.

Full-day calculation may take longer than real-time command budgets. An animated
waiting screen follows actual progress and flashes Core stayed alive or Core lost
from the authoritative result. Success shows the earned day score and total;
rejections and unknown interrupted outcomes remain distinct. Reduced-motion
preferences disable decorative animation. There is no artificial calculation
deadline or added scoring bonus. The watchdog detects an unresponsive calculation.

Measure complete production-worker turns with zero, one and several channels.
The worker yields before the one-day calculation so the animated screen can
render. Its progress is indeterminate while the large step is in flight and the
actual outcome appears when the authoritative response returns. No artificial
percentage or animation delay is added. Numerical iterations within an equilibrium
solve do not advance simulation time or split the exposure interval.

Run `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1` for implementation checks.
Extend Pages-shaped smoke and reproduction under `/CRS/` to select channels of
both flow directions, advance one day, verify eight bundles per executed channel,
check authoritative time/score/energy, exercise an empty next day and restore a
saved daily run. Cover early terminal outcomes separately. Wire the daily smoke
into predeploy and deployed Pages verification with the expected commit identity.
Report any unavailable deployment checks explicitly.

## Completion condition

A new tester can open the product, inspect indefinitely with frozen state,
choose today's channels, commit one day, understand the actual fuel moves and
daily outcome, and repeat or restore the run. Daily turns are the shipped test
default; authoritative physics, deterministic behavior, scoring, operating
limits and the existing objective scope remain intact.
