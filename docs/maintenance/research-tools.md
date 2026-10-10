# Tools for the web simulation

These tools support the authoritative web game through calibration, mathematical
comparison, fixtures and replay. They are not alternate game products.

| Tool | Purpose |
| --- | --- |
| `AgedCoreBenchmark` | Aged-core checks, fuelling capability, bundle absorption/adjuster audits and axial calibration; see `Program.cs` flags and benchmark reports. |
| `ChannelReferenceBenchmark` | Derive and compare fixed scoring references. |
| `LongRunPlaytest` | Authoritative bridge campaigns, including 100 daily turns and LZC-guided fuelling. |
| `CpuSpatialBenchmark` | Exact serial/parallel numerical row comparisons and native timings. Threaded browser hosting is not supported. |
| `ReplayBookkeepingBenchmark` | Replay/session bookkeeping measurements. |
| `Phase4ContractCorpus` | Regenerate/check the serialized browser response fixture; phase-era naming does not change its current protocol role. |

Run native tools with `dotnet run --project tools/<directory> -c Release -- <arguments>`.
Use a report's recorded command; old-pack results are historical comparisons.

```powershell
./tools/Sync-PhysicsPacks.ps1
./tools/Test-DotNet.ps1 -Configuration Release
./tools/Test-Browser.ps1 -Configuration Release
./tools/Build-BrowserWasm.ps1 -RunAOTCompilation -OmitPrecompressedAssets
cd web/candu-playtest
npm run build:pages
npm run smoke -- http://127.0.0.1:4173/CRS/
npm run smoke:scales -- http://127.0.0.1:4173/CRS/
npm run smoke:saves -- http://127.0.0.1:4173/CRS/
npm run benchmark -- --bridge=direct --label=local --warm-samples=1
```

Canonical and embedded diffusion packs must match byte for byte. Use
`Sync-PhysicsPacks.ps1`; preserve hashes and source identity. The IQS metadata pack
remains a shared adjoint/diffusion dependency. Provenance does not imply plant validation.

Publishing is single-threaded. Optional `-EnableRuntimeProfiling` instruments the
same solver; see [profiling](../physics/runtime-profile.md). The [hosting guide](hosting.md)
records Pages checks; the [library](knowledge-library.md) indexes physics evidence.
Obsolete external pipelines, duplicate authorities, research engine and GPU hosts
were removed in the [web-only cleanup](web-only-cleanup-manifest.md). Git history
preserves sources. External programs are never runtime dependencies.
