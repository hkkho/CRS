# CPU parallel execution experiment

Branch: `cpu-parallel-experiment`. Date: 2026-10-01.

GPU migration is paused at the user's request. Gameplay remains on the
authoritative shared C# CPU simulation. Previous read-only GPU experiments are
retained; no GPU backend is selected by the game.

## Implementation

`SpatialOperator` supports one to eight contiguous row partitions, with one
partition as the normal default. `EnableCpuParallelism=true` selects two default
partitions; the browser publishing script also enables the threaded WASM runtime.
Native callers can explicitly select a partition count on operator or eigen
iteration construction.

Only independent removal/leakage rows run concurrently. Every row retains its
existing neighbor/boundary arithmetic order and double precision. Group solves,
Jacobi updates, residual checks, power/fission sums, outer iterations, and RRS
candidate sequencing retain their original order. The four controller candidates
cannot simply run concurrently: each uses the preceding candidate's solution.

Partitions write disjoint destination ranges and join before returning. When
multiple rows fail, the earliest canonical row supplies the diagnostic. Output
is cleared only after all writers finish. Input validation and buffer alias
rejection retain the original behavior. The serial path creates no parallel
scheduling closures.

## Native results

Release .NET 10.0.11, 16 reported logical processors, 4,560 nodes, embedded
`innerrel1e7` pack. The fixture is the synthetic practice inventory, not an aged
live browser command. Warm input perturbs the converged two-group shape by
alternating ±2%; cold input is uniform with initial k=1. Each configuration has
one unreported warmup and three measured samples. Timings exclude solver
construction; compare them only within this native experiment.

| Start | Serial median | Two partitions | Four partitions |
|---|---:|---:|---:|
| Warm, 21 outer iterations | 23.75 ms | 18.99 ms | 18.07 ms |
| Cold, 236 outer iterations | 314.22 ms | 250.41 ms | 228.72 ms |

Two partitions reduced solve time by about 20%; four reduced it by 24–27%.
All configurations produced exactly identical group flux arrays, k, and outer
iteration counts. Focused tests additionally check exact normalized power,
repeated operator applications, invalid inputs, alias rejection, and concurrent
overflow failure handling. This is a small exploratory sample, not a browser
speedup claim. Some other test/build processes were active during measurement.

Raw measurements: `tmp/cpu-parallel-native-final.json`.

Reproduce from the repository root:

```powershell
dotnet build -c Release tools/CpuSpatialBenchmark
dotnet tools/CpuSpatialBenchmark/bin/Release/net10.0/CpuSpatialBenchmark.dll
```

## Browser result: worker bootstrap blocked; alternate host now works

Both serial and threaded Release AOT builds compiled. The serial production
worker benchmark completed refuelling, iodine/xenon evolution, zone feedback,
normal/accelerated pacing, and state/replay digest capture. Its exploratory
eight-sample advance median was 536.5 ms and p95 was 578.2 ms; this is a baseline,
not a threaded comparison (`tmp/cpu-parallel-browser-serial.json`).

The threaded build failed before a session could initialize in the application's
dedicated worker. Browser diagnostics confirmed `crossOriginIsolated=true` and
`SharedArrayBuffer` availability, then recorded:

```text
mono_wasm_pthread_on_pthread_attached () failed
TypeError: Cannot read properties of undefined (reading 'dispatchEvent')
```

This matches the unresolved [dotnet/runtime worker startup issue #114140](https://github.com/dotnet/runtime/issues/114140).
The local runtime was .NET 10.0.11. That hosting path cannot provide threaded
browser command timings. The standard product still uses its serial worker.
See `tmp/cpu-threaded-debug.log` and
`tmp/cpu-parallel-threaded-smoke.log` for the reproduction.

The subsequent [alternate hosting experiment](cpu-main-thread-hosting.md)
successfully initializes the runtime on the browser main thread and executes
serialized simulation commands on managed background threads. It preserves
sampled poison/zone states and final state/replay digests. Its measured browser
speedup is small; it remains a developer experiment.

The default `public/wasm` runtime remains serial. The publishing script refuses
to stage an experimental threaded runtime into that default directory; use a
temporary target to reproduce:

Threaded build intermediates are isolated in `tmp/cpu-threaded-build` to avoid
reusing serial runtime/AOT output when switching configurations.

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-BrowserWasm.ps1 `
  -RunAOTCompilation -EnableCpuParallelism -OmitPrecompressedAssets `
  -TargetPath tmp/cpu-threaded-wasm -StagingPath tmp/cpu-threaded-publish
```

Local Vite server/preview isolation headers can be enabled with
`$env:CANDU_CPU_THREADS = '1'`. That does not select the threaded runtime.
Vercel configuration remains unchanged; no deployment was made.

## Browser hosting follow-up

The alternate host establishes serialized command ownership and demonstrates
background execution without patching generated runtime internals. It still
needs integration into the actual product transport and broader browser/device
testing before becoming a selectable gameplay backend.

Once startup is reliable, compare serial/threaded complete worker-equivalent
commands, exact per-step state and replay digests, rejection atomicity, and p95
latency on more than one browser/device. Native gains alone do not justify
enabling threading in the deployed game.

## Verification

- `tools/Test-DotNet.ps1 -Suite All`: 92 Core, 31 Game, 24 Browser tests passed.
- Final Core rerun after isolating parallel scheduling: 92 passed.
- `tools/Test-Browser.ps1`: 24 bridge tests, 81 frontend tests, production build passed.
- Native benchmark tool: clean Release build; exact serial/parallel comparisons passed.
- Clean final serial Release AOT publish passed after regenerating build intermediates.
- Serial browser gameplay smoke passed; threaded worker startup failed as documented.
- Isolated threaded publishing and the guard against overwriting `public/wasm` were verified.
