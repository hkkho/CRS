# Browser playtest protocol v2

The authoritative C# bridge accepts JSON commands with `protocol: candu-playtest-v2` and publishes full snapshots or compact patches. See [PlaytestRuntime](../../src/ReactorSim.Browser/PlaytestRuntime.cs), [DTOs](../../src/ReactorSim.Browser/PlaytestDtos.cs), [fixtures](../../tests/Fixtures/README.md), and the [TypeScript parser](../../web/candu-playtest/src/protocol.ts).

## Initialization and pacing

Initialize accepts `mode: play`, an unsigned 32-bit `seed`, optional `shiftId`, and `pacingMode`. Reset retains the current seed, shift and pacing unless explicitly changed.

| Pacing | Command and interval |
| --- | --- |
| `demo` | Public time-based play. `commit-day` solves a half-day, then the browser displays it at the selected playback speed when ready. The managed clock stays paused between steps. The bridge also supports direct `commit-refuel`. |
| `daily-turn` | Retained daily planning and replay. `commit-day` advances 86,400 simulated seconds. Direct refuelling and wall-clock advances are rejected. |
| `real-time` | Retained comparison tools use explicit clock advances. |

`commit-day` takes `expectedCompletedDays` and `channelIndices`. An empty list advances without fuelling. The [shared step contract](../gameplay/daily-turn-mode.md) explains ordering, loss and rollback.

For `demo` free practice, initialization may include `customPhysics`, a map of pack-field paths to finite numeric overrides. Core validates it against the embedded defaults, and Game settles the custom core and reference. The resulting `dataPackId` has a `custom-practice-v1-` content identity and the snapshot carries modified sandbox provenance. Resets reuse the custom parameters. Custom runs support local update checkpoints.

## Observations

A compact patch names its base sequence. The client must materialize it against that exact snapshot or resynchronize. Both response forms include authoritative operation acceptance and diagnostics, state and replay digests, and the active sequence. The compact response can replace detailed core readings as needed.

- `runStatus` and `runEndReason` describe the run. Treat terminal status separately from `rrs.isGameOver`, which reports physical regulator exhaustion.
- `axialTiltFraction` is the signed first moment of thermal flux, negative toward End A and positive toward End B. `rrsReserveFraction` describes remaining control room.
- `physics.reactivity` is the solved `(k - 1) / k`. `coreReactivity` and `compensatedNetReactivity` distinguish the uncompensated and zone-compensated readings.
- `lastDayResult` carries the committed moves, unexecuted channels, score change and timing. The browser may interpolate continuous measurements for display. It reveals fuel movement and terminal outcomes at the accepted endpoint.
- After a fuel batch, optional `lastDayResult.refuellingZoneFills` reports `before`, `after` and `end` fill fractions in logical zone order. The arrays represent the starting state, the post-refuelling solve and the interval endpoint. This presentation field is excluded from the existing full and compact state digests to preserve saved-run replay.
- `shift` carries the seed, fuel use, energy and objective. Current launcher runs are endless free practice. Historical objective IDs remain supported for replay.
- `lastRefuellingScore` is null before the first accepted move. It then carries discharge reward, early-discharge penalty (`freshFuelCost`), zone bonus and signed net points. `ripple` carries the peak ratio and ripple and tilt rates. See the [scoring policy](../gameplay/score-balance.md).

## Replay identity

The `sha256-chained-replay-v2` chain uses SHA-256 over UTF-8 with a `sha256:` prefix and lowercase hex.

```text
D0 = H("sha256-chained-replay-v2|" + scorePolicyId + "|" + mode + "|" + initializationJson)
Dn = H(D(n-1) + "|" + canonicalCommandJson)
```

Initialization hashes the exact successful request. Each sequence-advancing dispatch appends sorted-key canonical command JSON, including rejections handled by the dispatcher. Malformed requests, validation failures returned before dispatch, compact resync requests and snapshot reads do not advance the chain.

Reset starts a new chain from the resolved seed and shift. Its initialization JSON also includes non-real-time pacing and, for a custom run, the original custom overrides in canonical form. The reset command becomes sequence 1. Replayers must record initialization and the sequence-advancing responses and commands. The bridge keeps the current digest rather than a replay archive.
