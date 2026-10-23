# 2. Browser commands and transport

[Previous: startup](01-startup.md) · [Library](index.md) · [Next: daily turn](03-daily-turn.md)

The browser **transport** carries requests and results between execution
contexts. Its **protocol** is the agreement about request names, data fields,
versions and responses. These mechanisms connect TypeScript presentation code
to C# simulation code without giving the interface direct access to mutable
reactor objects.

## The controller is the browser's session boundary

[BridgeSessionController](../../web/candu-playtest/src/sessionController.ts)
combines the current observation, pending-command status, draft plan, presentation
summary, history and replay journal. Native views receive `SessionUpdate` values
through `subscribe()` and send `CanduCommand` objects through `dispatch()`.

`subscribe()` immediately supplies the current update and returns an unsubscribe
function. `emit()` records eligible history and marks the update as `snapshot`
or `status` according to snapshot object identity. A progress notification can
therefore update a waiting display without rebuilding the core map.

### A typical dispatch, in order

1. Reject public-client real-time commands when `requiredPacing` is daily.
2. Reject during restore, while WASM is unavailable, or when a second daily
   commitment would overlap a pending request.
3. Increment pending counters, record the foreground command and emit status.
4. Select full responses for engineering commands/reset, compact responses for
   ordinary game commands, unless the caller supplied options.
5. Await `bridge.dispatch(command, options)`.
6. Record the exact command and response in `RunJournal`.
7. Update `SessionPresentation` using the response and previous observation.
8. Clear the draft after an accepted day/reset; clear history after reset.
9. Replace the observed snapshot and notify subscribers.
10. In `finally`, release pending counters and foreground scheduling suppression,
    then emit the settled status even when an exception occurred.

The `dailyPlan` array belongs here so navigation does not discard it.
`setDailyPlan()` deduplicates and sorts the draft, and blocks edits during pending
work, restore or a terminal run. Core eligibility and stock validation still occur
in Game when the day is submitted.

## Two queues protect one stateful runtime

| Queue | Location | What it protects |
| --- | --- | --- |
| `operationQueue` | `WorkerProtocolBridge` in [bridge.ts](../../web/candu-playtest/src/bridge.ts) | Orders main-thread requests, including snapshot materialization and sequence selection |
| `requestQueue` | [wasmWorker.ts](../../web/candu-playtest/src/wasmWorker.ts) | Prevents concurrent calls into the managed runtime, including while daily dispatch yields |

The browser main thread remains responsive while numerical work runs inside the
worker. The managed host is single threaded, so calls into its stateful
`GameSession` must execute in order. Managed locks also guard runtime access.

`WorkerProtocolBridge.request()` assigns a request ID and stores its promise and
deadline in a pending map. Result/error messages resolve or reject the matching
request. Progress messages keep daily progress visible and refresh its liveness
deadline only as valid progress advances. Fatal startup, worker or timeout errors
reject pending work and terminate the worker.

The configured 120-second startup and 180-second command ceilings are failure
ceilings, not promises of normal latency. A lost worker response does not prove
that no calculation happened. The UI treats an interrupted/unknown result
separately from an authoritative rejection or physical loss.

`WasmProtocolBridge` in the same file is a direct-export adapter useful to direct
callers and reproduction tooling. The public factory uses the lifecycle wrapper
and worker path.

## The JSON boundary

[protocol.ts](../../web/candu-playtest/src/protocol.ts) contains TypeScript command,
snapshot and export types plus runtime parsing/materialization.
[PlaytestProtocol.cs](../../src/ReactorSim.Browser/PlaytestProtocol.cs),
[PlaytestDtos.cs](../../src/ReactorSim.Browser/PlaytestDtos.cs) and
[PlaytestWireParsing.cs](../../src/ReactorSim.Browser/PlaytestWireParsing.cs)
define the managed side. The identity is `candu-playtest-v2`; the complete wire
contract is [protocol v2](../spec/browser-playtest-protocol-v2.md).

**Example 1. A daily command encoded as JSON.** JavaScript Object Notation (JSON)
represents data using named
fields, objects and arrays. **Serialization** converts an in-memory command into
this transferable representation. The example requests refuelling of channels
18 and 42 after two completed days. Its day and sequence values are illustrative;
an actual request uses the current observation's values.

```json
{
  "protocol": "candu-playtest-v2",
  "responseMode": "compact",
  "baseSequence": 7,
  "payload": {
    "type": "commit-day",
    "expectedCompletedDays": 2,
    "channelIndices": [18, 42]
  }
}
```

The outer object is the **envelope**, which specifies how the request should be
interpreted. Its `payload` object contains the gameplay action:

| Field | Interpretation in this example |
| --- | --- |
| `protocol` | Identifies the `candu-playtest-v2` wire contract expected by the runtime |
| `responseMode` | Requests a compact response, which the client reconstructs using its prior observation |
| `baseSequence` | Asserts that sequence 7 is the client's current transport baseline |
| `payload` | Groups the fields describing the requested game action |
| `type` | Selects the `commit-day` operation |
| `expectedCompletedDays` | Asserts that the reactor has completed exactly two days, preventing use of a stale decision |
| `channelIndices` | Lists zero-based channel identities; `[18, 42]` requests two eight-bundle moves, subject to authoritative validation |

The braces enclose objects, and the square brackets enclose the channel array.
The two checks serve different purposes: `baseSequence` checks the observation
used by compact transport, whereas `expectedCompletedDays` checks the game day.
Neither is a wall-clock timestamp. If the full day survives and completes, this
request advances the completed-day count from two to three. A physical loss or
rejection has the distinct outcomes described in [chapter 3](03-daily-turn.md).

## What PlaytestRuntime.Dispatch does

In [PlaytestRuntime.cs](../../src/ReactorSim.Browser/PlaytestRuntime.cs),
`DispatchJson()` forwards to `Dispatch()`. The latter performs the request's
validation, routing and response construction:

1. Reject ordinary dispatch while a staged day is pending.
2. Parse an object, require the protocol and a nonempty command type, and read
   the optional base sequence and response mode.
3. Canonicalize the command payload for replay bookkeeping.
4. Check a supplied compact baseline before executing. A mismatch returns a
   resynchronization response without executing that command.
5. Route reset to a newly constructed runtime, geometry commands to
   `DispatchEngineering()`, and gameplay to `DispatchPlay()`.
6. Convert the Game result into acceptance, message, diagnostics and observation.
7. Increment the dispatch sequence for commands reaching this execution path,
   choose the response representation, compute digests and append the canonical
   command to the replay chain.
8. Serialize the response using the Browser JSON contract.

An early malformed request or baseline mismatch does not follow exactly the same
sequence/replay path as an executed but Game-rejected command. A sequence
increment therefore records dispatch ordering without necessarily indicating
that simulation time advanced.

## Full and compact responses

| Response component | Meaning |
| --- | --- |
| Full `snapshot` | Complete observation, used at initialization, explicit reads and full dispatch |
| `snapshotPatch` | Updated top-level measurements/metadata for a compact response |
| `coreReplacement` | New detailed core when projection or fuel movement requires replacement |
| `coreMeasurements` | Measurement arrays when time changed without a detailed core replacement |
| `baseSequence` / `requiresResync` | Whether the client has the required observation to materialize a compact response |

`resolveDispatchOptions()` fills an omitted compact baseline only when the queued
operation is about to send. Earlier queued commands may have changed the sequence;
capturing it at button-click time would create avoidable mismatches. An explicitly
supplied baseline stays an explicit assertion.

`parseProtocolResponse()` materializes a full `CanduSnapshot` from the compact
pieces and previous snapshot. When it raises `ProtocolResyncRequiredError`, the
bridge requests a full snapshot and uses `parseProtocolResponseWithSnapshot()`.
This recovers the observation; it does not blindly resubmit the gameplay action.
Runtime parsing also validates measurements and structural contracts, rather than
trusting a TypeScript cast of JSON.

## Daily dispatch has a begin/continue envelope

`wasmWorker.dispatchRequest()` recognizes `commit-day` and requires yielding daily
exports. It calls `beginDailyDispatchJson`, posts progress, yields its event loop,
and calls `continueDailyDispatchJson` until `completed` contains a final
`responseJson`.

[DailyDispatch.cs](../../src/ReactorSim.Browser/DailyDispatch.cs) owns the pending
`GameSession.DailyTurnOperation`. The final result is fed into normal `Dispatch()`
as `_preparedDayResult`, so response mapping, sequence and replay processing happen
once. Begin already performs candidate fuel work; the continue call performs the
large daily calculation. Yielding here does **not** subdivide the physical day
into fine time steps or expose a partially committed reactor.

## Failure and cleanup

`AuthoritativeProtocolBridge` selects unavailable status after fatal failure and
refuses commands. `BridgeRecoveryView` presents that state.
`dispose()` removes subscriptions, clears timers, rejects pending work and
terminates the worker. New restore candidates use their own workers, described
in [chapter 6](06-saves-lifecycle.md).

Transport metrics record WASM-call duration, payload byte size, parse/materialize
time and whether core replacement was included. They help distinguish a slow
solve from expensive payload handling or rendering.
