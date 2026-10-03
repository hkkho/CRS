# Alternate CPU threading host

2026-10-01, branch `cpu-parallel-experiment`. Developer test only; the deployed
and default browser game continue to use the single-threaded WASM worker.

## Arrangement

The multithreaded runtime is initialized in the browser window rather than in
the application's dedicated JavaScript worker. In `CPU_PARALLEL` builds, every
bridge export returns `Task<string>` and submits its complete operation to one
serialized managed command queue. The JS thread marshals promises; simulation
initialization, commands, snapshots, profiling, and developer fixtures run on
managed thread-pool workers.

The queue owns the entire bridge command stream. Jobs await their predecessor
before touching session state. A job's failure reaches its own promise but is
observed by the queue so that subsequent jobs can continue. Numerical row
partitions operate inside one job and join before the command completes.
No reactor equations are duplicated in JavaScript.

The host info probe reported JS entry thread 1 and command thread 7 in the
measured runs (thread IDs are diagnostic and may vary). In the standard build,
the existing synchronous exports and worker arrangement remain intact.

This avoids the startup failure from bootstrapping threaded .NET inside a JS
worker. It follows the runtime's thread affinity boundary: [runtime threading notes](https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/threads.md).
The test has not yet connected this host to the live Phaser/Studio transport.

## Browser validation

An isolated diagnostic page executes the same initialization, channel-210
four-bundle refuel, 0.95 power target, eight one-second advances, fifty 100 ms
advances, and 10x/60x commands as the production-worker benchmark. AOT builds use
.NET 10.0.11 and two operator partitions, without profiling instrumentation.

The alternate host matched every sampled poison digest, coupled poison digest,
zone fill and simulation time, plus the final state digest, replay digest, poison
digest, fourteen fills, and final time (48,600 simulated seconds). No console
errors were recorded. A 16 ms UI event-loop heartbeat kept running throughout
the background calculations, including initialization and refuelling.

Fresh sequential runs, after compilation completed:

| Measurement | Standard serial worker | Async threaded host |
|---|---:|---:|
| Advance median, eight samples | 524.55 ms | 518.57 ms |
| Advance p95 | 568.71 ms | 557.16 ms |
| Refuel | 1,278.97 ms | 1,273.33 ms |

The alternate host's longest heartbeat gap was 23.01 ms. These eight-sample
timings show no substantial browser speedup: roughly 1% at the median can fall
within measurement variation. This compares complete hosting paths, including
different transport/marshalling costs; it does not isolate parallel kernel time.
Native solver gains must not be extrapolated to browser gameplay.

Raw results:

- `tmp/cpu-hosting-serial-fresh.json`
- `tmp/cpu-main-host-aot-fresh.json`
- `tmp/cpu-main-host-probe.json` (non-AOT startup/functionality check only)

## Reproduction

Build into a temporary directory; the publisher protects `public/wasm`:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1 `
  -EnableCpuParallelism -RunAOTCompilation -OmitPrecompressedAssets `
  -TargetPath tmp/cpu-main-host-aot -StagingPath tmp/cpu-main-host-aot-publish
```

Build the frontend, copy that runtime to `web/candu-playtest/dist/cpu-threaded-main`,
and start preview with `CANDU_CPU_THREADS=1` so all responses carry isolation
headers. The developer harness supplies its own empty same-origin page; it does
not replace the product's runtime or launch a second physical simulation in the UI.

```powershell
node web/candu-playtest/scripts/benchmark-cpu-hosting.mjs `
  http://127.0.0.1:4175 /cpu-threaded-main/ 8 tmp/cpu-hosting-serial-fresh.json
```

The optional fourth argument checks the serial reference and exits unsuccessfully
on a mismatch. Queue tests cover ordering/nonoverlap and fault recovery. The
browser harness also submits simultaneous snapshot/rejected-command requests
and requires the rejected command to preserve the accepted snapshot.
The physical fields remain unchanged; sequence and replay advance for the
rejection audit, preserving the existing bridge contract. The reference-trace
digests are captured before that extra guard, with post-guard audit fields
reported separately. `tmp/cpu-hosting-guard-probe.json` confirms this check.

`tools/Test-Browser.ps1` passed 26 bridge/host-queue tests, 81 frontend tests,
and the production build. Both threaded and standard Release AOT publishes
passed. The final standard serial gameplay smoke passed refuelling, zone
editing, history/session preservation, and reset checks.

## Next algorithm experiment

The current controller's up to four full-core evaluations happen at one
timestamp, while burnup and analytic iodine/xenon already integrate a frozen
accepted shape. See the [single-snapshot proposal](single-snapshot-step-proposal.md)
for reducing controller solves, simulated timestep sizes, consistency checks,
and long-run error/performance measurements. That proposal has not changed
gameplay physics in this hosting slice.
