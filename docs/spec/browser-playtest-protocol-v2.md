# Browser playtest protocol v2

`candu-playtest-v2` reports schema version 2 and keeps the v1 command and
response envelope shape. It changes
the live snapshot to carry `axialTiltFraction` and `rrsReserveFraction` from
`GameSession`. Both are dimensionless. Axial tilt is the signed first moment
of the solved group-2 (thermal) neutron flux: End A is negative and End B is
positive. RRS reserve is normalized to 0–1 from the nearest
liquid-zone fill boundary, so a 50% fill in every zone means full reserve.

Full snapshots and compact patches also publish `runStatus` (`running`,
`paused`, `completed`, or `ended`) and `runEndReason` (empty for a live run).
`completed` means the practice horizon was reached; `ended` covers an earlier
terminal outcome. RRS `isGameOver` continues to describe physical RRS exhaustion,
so consumers must use run status to stop gameplay at the horizon. Older v2 hosts
may omit these additive fields; clients then retain the RRS terminal fallback.
The top-level `targetPowerFraction` is the applied target, equal to
`physics.targetPowerWatts / physics.referencePowerWatts`, not a fixed nominal
power or a queued request. Targets are rejected while paused and applied on a
subsequent running advance.

The snapshot no longer carries `absoluteTiltFraction`,
`controlMarginFraction`, `targetTiltFraction`, `staticReactivity`, or
`staticReactivityMethodId`. `physics.reactivity` remains the solved
`(k - 1) / k`; `physics.coreReactivity` is the uncompensated equilibrium value,
and `physics.compensatedNetReactivity` is the value after liquid-zone
coefficient overlays. The practice run integrates burnup and refreshes the
equilibrium shape every half hour and after accepted refuelling. Spatial
iodine/xenon evolves analytically between solves; prompt kinetics is out of scope.

The v1 `queue-tilt-target` browser command is removed because it only changed a
legacy scenario scalar, not the spatial flux solve. Other v1 commands, core
fields, compact patches, and failure semantics remain
as described in the v1 specification (historical v1 specification, retained in Git history).


## Replay recording and identity

The live bridge uses `sha256-chained-replay-v2`, published in capability metadata
and the top-level `replayDigestAlgorithm` on initialization and dispatch responses.
Digest strings retain the `sha256:` prefix and lowercase hexadecimal encoding.
This replaces `sha256-canonical-replay-v1`; values across these algorithms are
not comparable. The simulation state-digest algorithms are unchanged.

Let `H(text)` be SHA-256 of UTF-8 text with that output encoding. Initialize with:

```text
D0 = H("sha256-chained-replay-v2|" + scorePolicyId + "|" + mode + "|" + initializationJson)
Dn = H(D(n-1) + "|" + canonicalCommandJson)
```

`initializationJson` is the exact successful Initialize request string, preserving
the earlier initialization identity contract. Each dispatched command is canonical
JSON using the bridge's sorted-object-key canonicalization. Accepted commands and
commands rejected by the dispatcher both advance sequence and digest. Malformed
requests, protocol/seed validation failures, and compact base-sequence resync
requests do neither. Snapshot reads do not append anything.

Reset creates a fresh chain using its resolved seed and shift ID. Its initialization
string is exactly `{"protocol":"candu-playtest-v2","mode":"play","seed":SEED,"shiftId":"SHIFT"}`
with decimal SEED and the resolved SHIFT. The reset command is then appended as
the first command of that new chain, at sequence 1; earlier commands are excluded.

The bridge retains only the current digest, with no command or diagnostic history
collections. Dispatch responses stream command, sequence, acceptance, diagnostics,
and digests. A caller needing a replay must durably record the successful Initialize
request and every sequence-advancing dispatch response in order, including rejected
commands and resets. Record before discarding a response; a digest alone cannot
reconstruct commands. The browser's bounded chart history is not a replay archive.
There is no bridge replay-export API or promise of server-side retained evidence.

## Refuelling score metadata

Full snapshots and compact patches publish `scorePolicyId` and
`lastRefuellingScore`. The latter is explicitly null before the first accepted
fuel move and after reset. Otherwise it contains `policyId`, `dischargeReward`,
`freshFuelCost` (a positive deduction) and `netPoints` (signed). Values are authored
game points, calculated in Game from the actual discharged bundles. The summary
persists through time advances and rejected commands; it is not the current
command's `scoreDelta`. Operating score remains separate from this operation
summary. Older v2 hosts may omit these additive fields.

The current policy is `practice-fuel-and-operation-v2`: discharge components
retain the per-bundle v1 rule; operating reward is now capped at one point per
simulated hour, retaining the 70% power / 30% tilt quality ratio. v1 totals are
not directly comparable with v2 totals. Older responses with v1 metadata remain
readable; replaying commands under v2 produces the v2 score and digest. Replay digests include
this policy ID before the mode, initialization JSON and command history. Thus
replay digests differ from older builds even for identical commands; compare runs
only under the same scoring policy. The scoring formula itself is unchanged by
this metadata addition.

## Shift objectives and cumulative results

New full snapshots and compact patches include optional `shift` metadata from
Game. Older v2 snapshots without this field remain readable. `shift.id` is
`free-practice` or `useful-fuel-day-v1`; the object includes the seed, objective,
horizon and remaining simulation seconds, fuel budget/consumption, useful bundle
count/goal and burnup threshold, thermal MWh, estimated electrical MWh, cumulative
operating points, discharge reward and fresh-fuel cost, outcome and badge status.
Outcome is `in-progress`, `success`, `missed` or `ended`; physical run state remains
separate in `runStatus` and `runEndReason`.

Initialize and reset accept optional `shiftId`. Initialize defaults to free
practice; reset retains the current objective and seed when omitted. Invalid
objective IDs reject atomically. The challenge starts paused, uses a 24-hour
horizon and the existing 128-bundle stock; free practice retains 30 days. The
challenge requires eight actual discharged bundles at >= 6 MWd/kg and horizon
completion without a terminal RRS result. The reward is a run-local badge, with
no bonus points or scoring-weight change. Replay initialization records the
objective ID; snapshot digests include its progress and cumulative totals.

Energy integrates the same accepted per-node fission-energy increments as burnup;
startup inventory, rejected transactions and paused wall time contribute nothing.
Electrical MWh is an estimate at the existing 650/2064 conversion ratio. Fuel and
discharge score totals accumulate only after accepted refuelling transactions.
