# Core family retirement decisions (task 21)

The [type-level consumer manifest](core-family-consumers.json) records 161 public
and internal declarations in the Phase5/6/7, reduced-model and archive families,
plus their supporting legacy state/command/power fixtures. It records both the
pre-relocation consumers and current direct/peer consumers. Identifier searches
are conservative and include comments: a shared contract reference indicates
compile-time reachability, not that gameplay executes every method. Refresh with
`python tools/Audit-CoreFamilies.py` from any directory.

| Family | Owner and decision |
| --- | --- |
| Phase5 bundle state, nuclide envelopes, power histories | Core: public inventory/lifecycle and spatial xenon contracts still use them |
| Complete-state digest and bundle canonical codec | Extracted intact into Core `CompleteStateDigestContracts.cs`; power snapshot reconstruction and research share them |
| Canonical finite/nonnegative validation | Extracted intact into Core `CanonicalKineticValidation.cs`; spatial binding and research kinetics share it |
| Complete batch integration, refuelling, burnup and invariant reports | Research `Legacy`: retained atomic transaction/reproduction APIs |
| Phase6 queues, devices, influence maps and old regulator | Research `Legacy`: retained historical models; gameplay uses Practice RRS |
| Phase7 delayed-neutron integration/substep policy | Research `Legacy`: deterministic kinetics tests still execute it |
| Phase7 spatial xenon state/coupling | Core: full-core model/public spatial APIs and executed xenon invariants use it |
| Reduced power, old power response, state snapshot, command application and manufactured fixtures | Research `Legacy`: retained research and test dependencies |
| State/replay archive codec | Research `Serialization`: preserved historical archive API; no normal browser dependency |

Twenty-three source files and 133 declarations (125 public) moved to the existing
`ReactorSim.Core.Research` assembly. Namespaces and public signatures remain the
same; callers of relocated APIs now need that project reference. Core tests
already reference research. No tool has a direct identifier consumer of these
relocated types; all sixteen tool/benchmark projects were nevertheless built.
No public type, data pack or serializer was deleted. Types with no direct/peer
consumer are retained as optional historical API rather than silently orphaning
their source; this decision is explicit in the manifest.

The two shared primitive extractions preserve their bodies and canonical field
order. Inventory, clock, atomic rejection, units, group ordering and numerical
budgets have not changed. The dependency test checks both the absent Core/Game
research reference and actual type assembly ownership, preventing legacy code
from quietly returning to the product dependency graph.

[The type-preservation report](../../benchmarks/task21-type-preservation.json)
compares all 161 declaration bodies with the recorded repository HEAD after
newline normalization: every body is identical, including both shared
extractions. All 24 original browser response byte hashes also remain identical.

## Coverage retained

| Invariant | Executed coverage |
| --- | --- |
| SI power, energy and burnup | `BurnupAndPowerTests`: known power, monotone burnup and mixed-power atomic rejection |
| Bundle identity/ownership conservation | `TopologyInventoryRefuellingTests`: both directions/sizes, uniqueness and stale request rejection |
| Canonical state and group ordering | `KineticsXenonControlDeterminismTests`: nonsemantic reorder/digest equality and ordered-group rejection |
| Time ordering and deterministic replay | Same suite: refuel/time replay across wall partitions; Game run-clock tests |
| Rejected-command atomicity | Core stale complete-refuel fixture, Game geometry/refuel guards and Browser rejection/replay tests |
| Wire representation | Six shared serialized fixtures and 24 original full-response byte hashes |
| Research separation | `ResearchDependencyBoundaryTests`, plus existing kinetics/xenon invariants in the Core test executable |

No replacement coverage is required for deleted behavior because none was
deleted. Existing invariant tests continue to exercise retained implementations
through the research project. Default checks use `tools/Test-DotNet.ps1` and
`tools/Test-Browser.ps1`; optional GPU checks use the
[research setup](research-builds.md). Assembly separation does not certify source
data or add a gameplay verification gate.

## Serialization, packs and history

Newtonsoft remains necessary: `FullCoreDiffusionDataPackContracts.cs` parses the
active embedded diffusion pack, and `IqsKineticsDataPack.cs` parses retained
shared adjoint/IQS metadata. The historical state codec moved intact to research;
its lack of player-facing consumers is not a reason to remove pack parsing or
invent a new serializer. Research receives the package transitively from Core.

Core still owns `EmbeddedData/candu6-two-group-diffusion-pack-v1.json` and
`EmbeddedData/candu6-two-group-iqs-pack-v1.json`, including logical resource names,
metadata, digests and approximation notes. No pack bytes or provenance changed.
[Historical research](historical-research.md) indexes the retained specifications
and paused proposals. Tool/data-source staging classification is now in the
[maintained-tool index](research-tools.md).
