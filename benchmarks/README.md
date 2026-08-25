# Core benchmark scenarios

Phase 4 benchmarks measure the current engine-neutral Core implementation on
committed synthetic scenarios. They are observation tools, not golden-data
consumers and not performance gates.

The P4-T08 scenario is defined in
[`P4-T08-static-solver-benchmark.json`](P4-T08-static-solver-benchmark.json)
and executed by the dependency-free `ReactorSim.Benchmarks` project. Run it
from the repository root with a pinned SDK:

```powershell
$sdkRoot='C:\Users\infin\AppData\Local\Temp\candu-sdk-10.0.302'
$env:DOTNET_ROOT=$sdkRoot
$env:Path="$sdkRoot;$env:Path"
dotnet run --project benchmarks\ReactorSim.Benchmarks\ReactorSim.Benchmarks.csproj `
  --configuration Release --no-build -- --warmup 10 --measure 200
```

The JSON result is machine-specific and should be retained outside the
repository. The runner checks converged-state usability and exact repeated
determinism, and reports elapsed time and allocations. No performance target,
optimization, tolerance approval, or reference/golden comparison is implied.

## Phase 9 CLI gameplay observation

P9-T01 adds the observation-only CLI benchmark manifest
[`P9-T01-cli-gameplay-benchmark-v1.json`](P9-T01-cli-gameplay-benchmark-v1.json)
and the dependency-free `ReactorSim.Phase9.Benchmarks` project. Run it from the
repository root after a Release build:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' `
  'benchmarks\ReactorSim.Phase9.Benchmarks\bin\Release\net10.0\ReactorSim.Phase9.Benchmarks.dll' `
  --warmup 10 --measure 200
```

It runs three frozen Phase 8 command streams through the public CLI boundary,
checks exact repeated output determinism, and reports host-specific elapsed
time and current-thread allocations. The manifest binds the approved P8-T02,
P8-T03, and P8-T05 artifact hashes and deliberately selects no performance
target. Android evidence is explicitly deferred until a representative device
and toolchain are available; no mobile result may be inferred from the desktop
observation.

## Phase 9 CLI profile observation

P9-T02 adds the bound profiling manifest
[`P9-T02-cli-profile-parameters-v1.json`](P9-T02-cli-profile-parameters-v1.json)
and an explicit profile mode over the same frozen P9-T01 streams:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' `
  'benchmarks\ReactorSim.Phase9.Benchmarks\bin\Release\net10.0\ReactorSim.Phase9.Benchmarks.dll' `
  --profile
```

The profile reports per-case timing and current-thread-allocation
min/mean/p50/p95/max observations plus command counts and UTF-8 input/output
size descriptors. The measurement boundary is one complete public
`CliApplication.Run` with null output sinks. The frozen synthetic P8 CLI cases
do not invoke the Core spatial solver, so the profile does not claim solver
hotspots or spatial solve latency; those require a separately bounded Core
benchmark. No performance target, optimization, mobile result, thermal claim,
or release budget is implied.
