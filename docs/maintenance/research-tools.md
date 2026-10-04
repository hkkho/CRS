# Maintained tools, data and archived reproduction

The [inventory](research-inventory.json) records purpose, owner, command, inputs,
outputs, filename consumers and keep/archive decisions for all 58 authored
tools/scripts and 101 data/reference/embedded artifacts. Refresh it with
`python tools/Inventory-Research.py`. Consumer scanning includes source, tests,
scripts and metadata. An empty direct-consumer list has explicit archive
collection ownership; it is not evidence for deleting provenance.

## Current native commands

Run from the repository root with .NET 10. The inventory supplies the complete
project path; use `dotnet run --project <path> --artifacts-path tmp/task26-tools -- <arguments>`.
All seven invocations below ran successfully during task 26.

| Tool | Verified arguments | Inputs and output |
| --- | --- | --- |
| AgedCoreBenchmark | `tmp/task26-aged 1001` | Seed plus embedded packs; aged-core report in the selected directory |
| SingleSolveBenchmark | `900 2 2` | Two experimental cadence steps; stdout JSON; does not replace GameSession |
| CpuSpatialBenchmark | none | Embedded pack, 1/2/4 row workers; exact-result comparison and stdout timings |
| GameplayBalanceBenchmark | `--compare benchmarks/gameplay-balance-v2.json benchmarks/gameplay-balance-v2.json` | Tracked prior-phase report; verifies baseline/tuned physical equality, score conversion and ranking, not a new balance study |
| ReplayBookkeepingBenchmark | none | Solver-free 100/1,000/10,000 command measurements; stdout CSV |
| Phase4ContractCorpus | `tmp/task26-contract.json` | Two seeded command sequences; 24 response hashes |
| ReactorSim.Benchmarks | `--warmup 1 --measure 1` | Retained three-node static manifest; stdout measurements |

Gameplay balance generation remains `dotnet run --project tools/GameplayBalanceBenchmark/GameplayBalanceBenchmark.csproj -- tmp/gameplay-balance.json`.
Its previous same-seed results are in [the balance report](../gameplay/score-balance.md).
The comparison command reads the tracked report directly, so it works in a fresh
checkout. Array-only generated reports remain supported. This maintenance task
does not claim to have regenerated the balance study.

The [101-day playtest](../gameplay/long-run-playtest.md) supplies a separate
duration/fuel-budget experiment and 1x/10x comparison. Run
`dotnet run --project tools/LongRunPlaytest -c Release -- --policy=reserve --days=101 --pair=true --output=tmp/longrun-reserve-pair`.
The offline tool uses the authoritative Game session and explicitly records
horizon/fuel overrides. The browser replay in `scripts/long-run-playtest.mjs`
consumes its order schedule through normal deployed WASM commands.

The [channel-power maps](../gameplay/channel-power-maps.md) include a normal
endless 101-day main-game run. Use `--endless=true` to measure the current
browser configuration without debug stock or horizon overrides. Per-channel
maxima and simulation-time weighted ripple are recorded in its report;
`python tools/Plot-LongRunPower.py tmp/longrun-maps` validates the integrals
and renders the standalone maps from all eight campaign reports.

The historical P9 `--profile`/`--profile-manifest` mode is retired because its
versioned parameter manifest is absent. It now fails immediately with a clear
replacement message. Static benchmark mode remains runnable and was verified;
the whole program is not classified as broken. Historical profile source stays
in place for reference, with no current supported invocation.

## Browser and repository commands

The current browser acceptance and complete-command matrix are:

```powershell
cd web/candu-playtest
node scripts/smoke.mjs http://127.0.0.1:4173
node scripts/benchmark-wasm.mjs http://127.0.0.1:4173 --label=task26 --warm-samples=1
```

The smoke imports the Studio, shift, recovery and zone helpers rather than
executing those modules as standalone applications. Additional accessibility,
pace, startup, rendering and xenon scripts retain their named prior-phase
measurements in [browser performance](../performance/browser-phase3.md).
GPU/thread/profile experiments remain optional, with setup and comparison
budgets in [research builds](research-builds.md). Script syntax was checked
separately from integration execution; the inventory does not treat parsing as
a new device or performance result.

Shared checks remain `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1`.
`Build-BrowserWasm.ps1` publishes from embedded data and was exercised in task 21;
this task does not change published numerical values. `Audit-CoreFamilies.py`
refreshes the type-consumer boundary; `Inventory-Research.py` refreshes this index.

## Canonical pack and runtime independence

`data/packs/candu6-two-group-diffusion-pack-v1.json` is the canonical authored
diffusion source. Core's embedded copy is a staging mirror. `Sync-PhysicsPacks.ps1`
checks SHA-256 equality by default; `-Stage` copies the canonical bytes to the
mirror and verifies equality. Core builds enforce that same byte equality before
compilation. This protects two copies from drifting; it does not require external
source verification or plant calibration.

```powershell
./tools/Sync-PhysicsPacks.ps1
# After intentionally editing the canonical pack:
./tools/Sync-PhysicsPacks.ps1 -Stage
```

The matching check passed. An isolated stale-copy build check failed as expected,
and the matching-copy check succeeded. Neither test altered runtime data.
The runtime reads embedded resources and does not read `data` or invoke external
analysis programs. IQS shared metadata and optional WGSL resources retain Core
ownership; no pack or metadata bytes changed.

## Archive in place

PhysicsData, SpatialComparison, IndependentSpatialReproduction,
AnalyticalSpatialReference, ManufacturedSpatialAuthority,
RepresentativeReducedAuthority, P7T06SyntheticAuthority, P7T07LiteratureCases and
P7T08LiteratureCalibration are archived historical executables. Each was built
and invoked with no arguments to verify its usage interface (expected exit 1/2).
Their usage output gives the original artifact/definition/manifest arguments;
these are not current gameplay development prerequisites. No numerical archive
rerun or source attestation is claimed by that interface check.

Legacy maps, reduced packs, schema examples, decks, candidate/golden comparisons
and their manifests are indexed individually with hashes and consumers. Artifacts
still copied by executed tests remain invariant fixtures. Other records are
archived in their original paths so digest bindings and hardcoded reproduction
paths remain intact. No version is deleted merely because another version exists;
no proven-safe physical relocation was necessary. The index is their explicit
archive home and lifecycle status.

The Python authority/export/report scripts and DRAGON/DONJON PowerShell workflows
are archived alongside those artifacts. Python compilation and PowerShell parsing
verified their syntax; optional packages, licenses, external binaries and historical
numerical outputs were not re-executed. Their source commands and
[historical research index](historical-research.md) preserve reproduction details.
External programs remain offline development tools, never runtime dependencies.
