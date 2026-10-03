# Optional research builds

Default game publishing omits GPU fixture types, embedded WGSL, GPU host modules
and the GPU JS export. It is single-threaded and needs neither WebGPU nor shared
memory. Profiling JS export is included only with `EnableRuntimeProfiling=true`.
GPU code and its numerical comparisons are retained unchanged behind
`EnableResearchExperiments=true`. CPU threads remain independently opt-in.

From the repository root, the research invariant suite is:

```powershell
./tools/Test-DotNet.ps1 -Suite Core -EnableResearchExperiments
```

For isolated Browser experiment checks while another suite is running:

```powershell
dotnet test tests/ReactorSim.Browser.Tests/ReactorSim.Browser.Tests.csproj --artifacts-path tmp/research-tests -p:EnableResearchExperiments=true --filter "FullyQualifiedName~GpuFixtures|FullyQualifiedName~CoupledGpuExperiment|FullyQualifiedName~ResearchBuildBoundaryTests"
cd web/candu-playtest
npm run test:gpu
```

To reproduce a browser experiment, create a separate preview. `research` mode
builds the frontend without copying its default public runtime. The publish
script permits experimental targets only under repository `tmp` and isolates
research intermediates. It cannot replace `public/wasm` with a GPU build.

```powershell
# From the repository root:
cd web/candu-playtest
npm run build -- --mode research --outDir ../../tmp/research-site
cd ../..
./tools/Build-BrowserWasm.ps1 -Configuration Release -EnableResearchExperiments -OmitPrecompressedAssets -TargetPath tmp/research-site/wasm -StagingPath tmp/research-publish
cd web/candu-playtest
npm run preview -- --outDir ../../tmp/research-site --port 4174
# In another terminal in web/candu-playtest:
node scripts/benchmark-gpu.mjs http://localhost:4174 --coupled --software
```

Add `-RunAOTCompilation` when measuring AOT command latency; do not compare a JIT
research preview with AOT gameplay as a speed claim. `--probe` checks adapter
availability only. The complete matrix is read-only: C# validates successive
states using its existing double-precision equations, canonical group/node order,
normalization, regional shape agreement and criticality budgets. GPU outputs
cannot commit session state. Existing measured limits and environment conditions
are in [prototype results](../physics/gpu-prototype-results.md) and
[coupled validation](../physics/gpu-coupled-validation.md). Kernel or budget changes
require new numerical evidence; this cleanup changes neither.

CPU thread experiments retain [their existing setup](../physics/cpu-parallel-experiment.md)
and [alternate host instructions](../physics/cpu-main-thread-hosting.md). Historical
result pages predate the explicit GPU build option; use this setup for current
reproduction. Default and research build-boundary tests check resources, managed
types and bridge surface in both configurations.

With default and research previews running, the actual WASM export check is:

```powershell
node scripts/research-boundary-smoke.mjs http://127.0.0.1:4173 http://localhost:4174
```

This checks that default exports omit GPU/profiling, research returns the
4,560-node fixture, and fixture generation preserves the session snapshot.
