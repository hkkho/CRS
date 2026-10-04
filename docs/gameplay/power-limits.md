# Channel and bundle power limits

A run ends if any channel exceeds **7,300 kW thermal** or any individual bundle
exceeds **935 kW thermal**. The two conditions are independent. Exact equality
is allowed. These are authored gameplay rules, not a plant protection model.

`PracticeOperatingLimits` owns the watt-valued thresholds in Game. `GameSession`
uses the accepted channel and node powers multiplied by the applied power
amplitude. Refuelling updates the inventory, RRS and spatial projection together;
the resulting snapshot is immediately terminal if it exceeds either cap, even
while paused. A queued power target takes effect at its control tick, and power
limits are checked on short ticks without waiting for a half-hour spatial solve.
An advance ends at its first violating wall tick. Subsequent operating commands
are rejected, and the final score, energy and fuel inventory remain available.

The existing full and compact browser contracts carry `runStatus: ended` and
the explicit `runEndReason`. Physical `rrs.isGameOver` remains independent.
If both power limits are exceeded, the channel-limit reason takes precedence;
existing LZC and global-tilt reasons retain their prior precedence.

## Full-power balance and presentation

The `powerlimits-v2` shared physics pack rebalances spatial coupling and effective
leakage, then restores seed-1001 half-fill criticality. Full output remains
**2,064 MW thermal / 650 MW electric**. Fuel ages, 190-day visits, eight-bundle
shifts, iodine/xenon evolution and no-adjuster reference derivation remain active.
No limit exception or power clipping is used. See [calibration and seed results](../physics/power-limit-balance.md).

Studio has distinct channel kW, reference-ripple %, and peak-bundle kW maps.
Channel colors reach red at 7,300 kW; bundle colors reach red at 935 kW. Ripple
is actual/reference, neutral at 100%, and reaches red at each channel's absolute
cap divided by its own reference. There is no separate ripple game-over limit.
The inspector displays actual kW, target ripple, reference MW and both caps.
The axial power graph and peak-history graphs mark the respective limits.

One shared time inspector defaults to live. Explicit slider or graph selection
shows the same historical snapshot across metrics, map, zones and bundle profiles;
operating controls are disabled until Return to live. Graph hover does not pin time.
Trend history retains 4,096 observations; up to 128 complete states are retained
at half-hour observations, fuel moves and power changes, together with the latest
frame. Selected complete observations stay pinned until archive eviction or reset.
New shift clears both histories. Compact updates publish authoritative burnup,
state versions and fresh flags between solves, and replace power readings when
the applied amplitude changes.

Focused tests cover strict boundaries, independent channel/bundle checks,
actual-power scaling, the first violating tick, terminal command rejection,
paused refuelling below the caps, and full/compact bridge ending information.
Seeded start and browser playthrough checks cover the rebalanced full-power loop.
