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
