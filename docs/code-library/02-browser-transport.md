---
hide:
  - toc
---

# 2. Browser commands and transport

[Previous: startup](01-startup.md) · [Library](index.md) · [Next: daily turn](03-daily-turn.md)

The browser **transport** carries requests and results between execution
contexts. Its **protocol** is the agreement about request names, data fields,
versions and responses. These mechanisms connect TypeScript presentation code
to C# simulation code without giving the interface direct access to mutable
reactor objects.

## Read the command path beside the code

The excerpts below follow one command from the controller into the worker. A
**promise** represents a value or failure that will arrive later. Chaining promises
serializes requests without calculating reactor physics on the browser main thread.

### The controller accepts an authoritative response

This is the dispatch method's main execution block, after availability and pending
command checks. The `finally` block runs after either success or failure.

```annotated-source
path: web/candu-playtest/src/sessionController.ts
title: Dispatch, publish the result and release pending state
language: typescript
start: const previous = this.snapshotValue;
end: 'this.syncScheduler();

  this.emit();

  }'
steps:
- through: const response = await this.bridge.dispatch(command, responseOptions);
  explanation: '**Keep the before-state and await the response.** The previous snapshot is retained for impact feedback. The bridge sends the command to the authoritative runtime; `await` suspends this method until the response arrives.'
- through: this.presentation.accept(response, previous);
  explanation: '**Record evidence, then prepare feedback.** The journal records the command and its returned acceptance for replay. Presentation compares the old and returned observations to explain changes; neither operation decides whether the reactor accepts the command.'
- through: if (command.type === "reset" && response.accepted) this.history.clear();
  explanation: '**Reconcile local planning state.** A successful day or reset consumes the current draft, while a rejected command retains it for correction. A successful reset also clears observations from the old run.'
- through: return response;
  explanation: '**Publish the returned snapshot.** Store the response and authoritative observation, notify subscribers, then return the same response to the caller. Updating this reference makes maps, reports and controls observe the same accepted state.'
- through: throw error;
  explanation: '**Surface transport failure.** Save a readable error, notify listeners and rethrow so the caller can handle it. A missing response is not evidence that the command was rejected or that retrying is safe.'
- through: 'this.scheduler.releaseForegroundCommand();

    }'
  explanation: '**Release pending bookkeeping.** `finally` decrements counters even if dispatch threw. `Math.max` prevents negative counts. When the last foreground operation finishes, its label is cleared and scheduler suppression is released.'
- through: 'this.syncScheduler();

    this.emit();

    }'
  explanation: '**Refresh lifecycle and controls.** Recompute scheduler activity and notify listeners after cleanup. The interface can now leave its waiting state. The enclosing dispatch method returns or rethrows the outcome selected above.'
```

### The bridge serializes main-thread operations

This helper belongs to `WorkerProtocolBridge`. The promise returned to the caller
retains failure, while a separate recovered tail allows the queue to be maintained.

```annotated-source
path: web/candu-playtest/src/bridge.ts
title: Keep requests in a single promise chain
language: typescript
start: 'private enqueue<T>(operation: () => Promise<T>): Promise<T> {'
end: 'return result;

  }'
steps:
- through: 'throw error;

    });'
  explanation: '**Append one operation.** `then(operation, operation)` schedules it after either resolution or rejection of the previous tail. Processing failures call `failReady` to mark the bridge unavailable, then remain rejected for this caller. `T` is the generic result type.'
- through: 'return result;

    }'
  explanation: '**Separate queue continuity from the result.** The stored queue tail catches rejection so the promise chain itself is reusable. The returned `result` still rejects; this catch does not declare a failed command successful or resend it.'
```

### The worker serializes and returns requests

The worker also has a queue because messages can arrive while earlier asynchronous
work is waiting. Request IDs connect each reply to its waiting main-thread caller.

```annotated-source
path: web/candu-playtest/src/wasmWorker.ts
title: Process each worker message in order
language: typescript
start: scope.addEventListener("message", (event) => {
end: 'scope.postMessage({ type: "error", id: event.data.id, error: formatError(error) });

  }

  });

  });'
steps:
- through: const resultJson = await dispatchRequest(api, event.data);
  explanation: '**Queue the request and call managed code.** Appending to `requestQueue` prevents overlap between asynchronous handlers. `getWasmExports` waits for startup. The timer begins before dispatch and measures the worker call, not the whole UI interaction.'
- through: ': new TextEncoder().encode(resultJson).byteLength;'
  explanation: '**Measure transport cost.** Compute elapsed milliseconds and UTF-8 payload bytes. The string-length fallback is used if `TextEncoder` is unavailable. These values support performance diagnosis and do not change simulation results.'
- through: 'returnedUtf8PayloadBytes,

    });'
  explanation: '**Return the matching result.** The response carries the original request ID, JSON and measurements. The bridge resolves the promise registered for that ID.'
- through: 'scope.postMessage({ type: "error", id: event.data.id, error: formatError(error) });

    }

    });

    });'
  explanation: '**Return failure without breaking the queue.** Exceptions become an error message tied to the same ID. Because the catch finishes normally, this queue item resolves and later messages can be considered. Bridge lifecycle checks still determine whether the client remains usable.'
```


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

### Yield JavaScript between the daily export calls

This branch is inside the worker's `dispatch` case, after the optional profiling
path. The JSON may be an envelope with `payload` or a direct command. The check
selects the daily exports; it does not implement any reactor rule in TypeScript.

```annotated-source
path: web/candu-playtest/src/wasmWorker.ts
title: Route a daily command through begin and continue
language: typescript
start: const root = JSON.parse(request.commandJson);
end: return normalizeJson(await api.dispatchJson(request.commandJson));
steps:
  - through: if (!api.beginDailyDispatchJson || !api.continueDailyDispatchJson) throw new Error("This WASM host lacks yielding daily execution. Reload the updated game.");
    explanation: '**Choose the daily route and check capability.** Parse the command JSON, inspect its payload type and require both daily exports. An older host is rejected explicitly so the client cannot silently select a different execution contract.'
  - through: let progress = JSON.parse(normalizeJson(await api.beginDailyDispatchJson(request.commandJson)));
    explanation: '**Begin preparation in C#.** Await the managed begin call and normalize its return into JSON before parsing. Begin validates the envelope and prepares the detached fuel movements. Its progress object describes completion status and requested duration.'
  - through: await new Promise(resolve => setTimeout(resolve, 0));
    explanation: '**Publish progress and yield.** While the operation is incomplete, post progress using the original request ID. A zero-delay timer yields to the worker event loop before the next managed call. It does not advance simulation time or schedule a smaller physical integration step.'
  - through: return normalizeJson(progress.responseJson);
    explanation: '**Continue until a final response exists.** The managed continuation calculates the daily operation. Loop termination requires its completed flag; the final protocol response is returned to the normal worker message handler.'
  - through: return normalizeJson(await api.dispatchJson(request.commandJson));
    explanation: '**Route other commands normally.** Leaving the daily branch reaches ordinary dispatch. Its returned value is normalized to the same JSON-string boundary so the surrounding worker handler can return either kind of result consistently.'
```

### Finish the managed daily operation through normal response handling

`BeginDailyDispatchJson` has already stored a pending operation and the original
command JSON. This complete continuation method calculates it, then passes its
prepared result into the usual protocol dispatcher.

```annotated-source
path: src/ReactorSim.Browser/DailyDispatch.cs
title: Complete daily work and construct one protocol response
language: csharp
start: public string ContinueDailyDispatchJson()
end: |
  finally { _preparedDayResult = null; }
  }
  }
steps:
  - through: _pendingDay.CalculateDay();
    explanation: '**Require and calculate the pending day.** The runtime lock protects its pending-operation fields. Calling continue without begin is an invalid lifecycle operation. Otherwise Game performs the detached calculation and decides whether a completed result is available.'
  - through: |
      RequestedSimulationSeconds = _pendingDay.RequestedSimulationSeconds
      });
    explanation: '**Return progress if incomplete.** A null result produces a serialized progress object containing advanced and requested simulation seconds. The default completed flag remains false; JavaScript can yield and call again. This branch describes pending work, not an adopted intermediate observation.'
  - through: _pendingDay = null; _pendingDayCommand = null;
    explanation: '**Prepare exactly one final dispatch.** Retain the completed Game result and original command, then clear the pending-operation fields. The normal dispatcher will consume the prepared result rather than execute the day again.'
  - through: |
      finally { _preparedDayResult = null; }
      }
      }
    explanation: '**Use the shared protocol response path, then clear temporary state.** Dispatch performs ordinary response mapping, sequence and replay processing. The wrapper marks the returned response completed. The finally block clears the prepared result even if response construction throws; the closing braces release the lock and end the method.'
```

## Failure and cleanup

`AuthoritativeProtocolBridge` selects unavailable status after fatal failure and
refuses commands. `BridgeRecoveryView` presents that state.
`dispose()` removes subscriptions, clears timers, rejects pending work and
terminates the worker. New restore candidates use their own workers, described
in [chapter 6](06-saves-lifecycle.md).

Transport metrics record WASM-call duration, payload byte size, parse/materialize
time and whether core replacement was included. They help distinguish a slow
solve from expensive payload handling or rendering.
