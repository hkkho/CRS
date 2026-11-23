# Refuelling-step contract

The `commit-day` command applies a fuel plan and advances the simulation. The public time-based game uses a 43,200-second interval; the supported daily mode uses an 86,400-second interval and an explicit plan. Both modes use the transaction and failure rules below.

## Transaction

1. Validate `expectedCompletedDays`, unique in-range channel indices, eligibility and stock. An empty plan advances without fuelling. Time-based play buffers up to eight waiting orders in the browser; the bridge accepts lists of up to 380 channels.
2. Sort the list by channel index and fork the run clock and solver. Apply each eight-bundle move with channel flow to the candidate. Retained bundles keep their history. Time-based play solves the combined inventory once and uses the resulting zone fills for every order's zone bonus. Daily mode solves after each move.
3. If still operating, use the post-refuelling solution to integrate fission energy, burnup, poison and operating score once over the interval. Solve the endpoint equilibrium and regulation.
4. Adopt the candidate and return the accepted movements, discharge burnup, unexecuted channels, fuel, energy, points and terminal reason in `lastDayResult`. The completed-day counter advances on whole-day boundaries.

The browser sends the latest snapshot's `completedDays` as `expectedCompletedDays`. Two consecutive half-day steps can therefore use the same counter value; the counter advances when the second step reaches the next whole day.

Validation or numerical failure discards the entire candidate. In time-based play, a combined batch that crosses an operating limit commits every move and ends the run before time advances. In daily mode, a move that crosses an operating limit commits and stops the remaining orders. A limit crossed at the interval endpoint commits the elapsed interval. Transport interruption can leave the outcome unknown, so the browser stops and offers recovery.

Time-based play uses the `practice-demo-batch-refuelling-v1` cadence identity. Daily mode uses `practice-daily-one-step-reference-zones-v2`. Checkpoints carry their cadence identity so they replay against the same rules. See [the walkthrough](../code-library/03-daily-turn.md) and [wire protocol](../spec/browser-playtest-protocol-v2.md).
