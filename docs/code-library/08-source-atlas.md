# 8. Source atlas and glossary

[Previous: development](07-development.md) · [Library](index.md) · [Next: documentation tools](09-documentation-tools.md)

A **source atlas** is a responsibility-based index of a codebase. This atlas
groups the files that participate in each subsystem and explains their relation
to the execution trace. The tables describe module responsibilities; the linked
source contains the individual type and function definitions. Corresponding
tests provide concrete examples of accepted inputs and failure behavior.

## Browser application

All links in this table are under `web/candu-playtest`.

| Files | Responsibility and connection |
| --- | --- |
| [index.html](../../web/candu-playtest/index.html), [main.ts](../../web/candu-playtest/src/main.ts) | Mount targets and composition root |
| [AppShell.ts](../../web/candu-playtest/src/AppShell.ts), [LauncherView.ts](../../web/candu-playtest/src/LauncherView.ts) | Current view lifetime and seed/objective entry |
| [sessionController.ts](../../web/candu-playtest/src/sessionController.ts) | Browser observation, draft, journal, history, dispatch and isolated restore |
| [bridge.ts](../../web/candu-playtest/src/bridge.ts), [wasmWorker.ts](../../web/candu-playtest/src/wasmWorker.ts) | Lifecycle, worker messaging, queues, deadlines and export calls |
| [protocol.ts](../../web/candu-playtest/src/protocol.ts) | Command/snapshot types, wire parsing and compact materialization |
| [commandState.ts](../../web/candu-playtest/src/commandState.ts), [gameplayPresentation.ts](../../web/candu-playtest/src/gameplayPresentation.ts) | View drafts, candidate selection and explanatory formatting over published state |
| [ArcadeView.ts](../../web/candu-playtest/src/ArcadeView.ts), [ArcadeMap.ts](../../web/candu-playtest/src/ArcadeMap.ts) | Phone-oriented challenge presentation and core map |
| [studio/StudioView.ts](../../web/candu-playtest/src/studio/StudioView.ts), [StudioNavigation.ts](../../web/candu-playtest/src/studio/StudioNavigation.ts) | Detailed workspace orchestration and view-facing session contract |
| [StudioMapView.ts](../../web/candu-playtest/src/studio/StudioMapView.ts) | Studio SVG core map and channel interaction |
| [StudioStatusView.ts](../../web/candu-playtest/src/studio/StudioStatusView.ts), [StudioOrderView.ts](../../web/candu-playtest/src/studio/StudioOrderView.ts), [DailyPlanView.ts](../../web/candu-playtest/src/studio/DailyPlanView.ts) | Readings, selected-channel detail and daily plan/report |
| [DayCalculationView.ts](../../web/candu-playtest/src/studio/DayCalculationView.ts), [SessionPresentation.ts](../../web/candu-playtest/src/SessionPresentation.ts) | Waiting/outcome feedback and persistent response summary |
| [ReactorHistory.ts](../../web/candu-playtest/src/studio/ReactorHistory.ts), [HistoryView.ts](../../web/candu-playtest/src/studio/HistoryView.ts) | Bounded observation samples and plotted/historical inspection |
| [powerReadings.ts](../../web/candu-playtest/src/studio/powerReadings.ts), [displayScales.ts](../../web/candu-playtest/src/studio/displayScales.ts), [displaySamples.ts](../../web/candu-playtest/src/studio/displaySamples.ts) | Map bands, units and chart presentation bounds/sampling |
| [devicePresentation.ts](../../web/candu-playtest/src/studio/devicePresentation.ts), [channelCoordinates.ts](../../web/candu-playtest/src/channelCoordinates.ts), [visuals.ts](../../web/candu-playtest/src/visuals.ts), [domPatch.ts](../../web/candu-playtest/src/studio/domPatch.ts) | Geometry labels, coordinate navigation, formatting and small DOM updates |
| [coreSeed.ts](../../web/candu-playtest/src/coreSeed.ts) | Browser random seed generation and seed handling |
| [liveClock.ts](../../web/candu-playtest/src/liveClock.ts), [observedPace.ts](../../web/candu-playtest/src/observedPace.ts) | Retained real-time scheduler/pace support; public daily client disables automatic advance |
| [runSave.ts](../../web/candu-playtest/src/runSave.ts), [playerStore.ts](../../web/candu-playtest/src/playerStore.ts), [PlayerPanel.ts](../../web/candu-playtest/src/PlayerPanel.ts), [cloudPlayer.ts](../../web/candu-playtest/src/cloudPlayer.ts) | Replay saves, IndexedDB, save/account UI and optional cloud adapter |
| [GameUpdateView.ts](../../web/candu-playtest/src/GameUpdateView.ts), [offlinePlugin.ts](../../web/candu-playtest/offlinePlugin.ts) | Version display, save-before-update and generated service worker |
| [BridgeRecoveryView.ts](../../web/candu-playtest/src/BridgeRecoveryView.ts), [SessionAnnouncements.ts](../../web/candu-playtest/src/SessionAnnouncements.ts), [SimulatorManual.ts](../../web/candu-playtest/src/SimulatorManual.ts) | Failure recovery, live-region feedback and in-game manual |
| [manualContent.ts](../../web/candu-playtest/src/manualContent.ts) | Bundle the MkDocs manual's Markdown chapters and render the same content for the F1 dialog |
| [package.json](../../web/candu-playtest/package.json), [vite.config.ts](../../web/candu-playtest/vite.config.ts), [scripts](../../web/candu-playtest/scripts) | Build configuration, smoke tests and reproduction tooling |

## Managed browser boundary

| Files | Responsibility |
| --- | --- |
| [BrowserHost/main.mjs](../../src/ReactorSim.BrowserHost/main.mjs), [BrowserExports.cs](../../src/ReactorSim.BrowserHost/BrowserExports.cs), [Program.cs](../../src/ReactorSim.BrowserHost/Program.cs) | Runtime loading, JS exports and empty managed startup target |
| [PlaytestBridge.cs](../../src/ReactorSim.Browser/PlaytestBridge.cs) | Static facade over a default instance runtime |
| [PlaytestRuntime.cs](../../src/ReactorSim.Browser/PlaytestRuntime.cs) | Initialization, command routing, sequence, cached Game observations and response selection |
| [DailyDispatch.cs](../../src/ReactorSim.Browser/DailyDispatch.cs) | Pending begin/continue operation and final normal-dispatch handoff |
| [PlaytestProtocol.cs](../../src/ReactorSim.Browser/PlaytestProtocol.cs), [PlaytestWireParsing.cs](../../src/ReactorSim.Browser/PlaytestWireParsing.cs) | Protocol identity, serialization/canonicalization and input parsing |
| [PlaytestDtos.cs](../../src/ReactorSim.Browser/PlaytestDtos.cs), [PlaytestJsonContext.cs](../../src/ReactorSim.Browser/PlaytestJsonContext.cs), [PlaytestSnapshotMapping.cs](../../src/ReactorSim.Browser/PlaytestSnapshotMapping.cs) | Wire schema, JSON metadata and Game-to-wire projection |
| [PlaytestDigestPolicy.cs](../../src/ReactorSim.Browser/PlaytestDigestPolicy.cs), [ReplayDigestChain.cs](../../src/ReactorSim.Browser/ReplayDigestChain.cs) | State and replay identity calculations |
| [PlaytestProfiling.cs](../../src/ReactorSim.Browser/PlaytestProfiling.cs) | Optional instrumented dispatch |

## Game layer

| Files | Responsibility |
| --- | --- |
| [PracticeGameSessionFactory.cs](../../src/ReactorSim.Game/PracticeGameSessionFactory.cs) | Authored run setup, pack/model construction and startup settlement |
| [GameSession.cs](../../src/ReactorSim.Game/GameSession.cs) | Commands, daily candidate lifecycle, fuel/exposure orchestration and adoption |
| [PracticeRunClock.cs](../../src/ReactorSim.Game/PracticeRunClock.cs) | Plan/commit simulation-time changes and forkable clock state |
| [PracticeCandidate.cs](../../src/ReactorSim.Game/PracticeCandidate.cs) | Mutable advance builder and frozen candidate values |
| [PracticeOperatingLimits.cs](../../src/ReactorSim.Game/PracticeOperatingLimits.cs), [PracticeScoring.cs](../../src/ReactorSim.Game/PracticeScoring.cs), [ShiftProgress.cs](../../src/ReactorSim.Game/ShiftProgress.cs) | End rules, channel-ripple points and challenge progress |
| [RefuellingMovement.cs](../../src/ReactorSim.Game/RefuellingMovement.cs), [DailyTurnResult.cs](../../src/ReactorSim.Game/DailyTurnResult.cs) | Confirmed bundle movement and immutable per-day report |
| [GameSessionContracts.cs](../../src/ReactorSim.Game/GameSessionContracts.cs), [CorePresentationContracts.cs](../../src/ReactorSim.Game/CorePresentationContracts.cs), [GamePresentationProjector.cs](../../src/ReactorSim.Game/GamePresentationProjector.cs) | Session/core observations and projection logic |
| [ChannelRippleSnapshot.cs](../../src/ReactorSim.Game/ChannelRippleSnapshot.cs), [AdjusterPresentation.cs](../../src/ReactorSim.Game/AdjusterPresentation.cs), [LiquidZoneTubePresentation.cs](../../src/ReactorSim.Game/LiquidZoneTubePresentation.cs) | Reference-relative power and shared device geometry observations |
| [ConfiguredCoreDesign.cs](../../src/ReactorSim.Game/ConfiguredCoreDesign.cs), [GameGeometryTransactions.cs](../../src/ReactorSim.Game/GameGeometryTransactions.cs) | Candidate geometry edits, reciprocal boundaries and solves |
| [RunProvenance.cs](../../src/ReactorSim.Game/RunProvenance.cs) | Standard versus modified sandbox provenance |

## Core families

Most filenames include `Contracts` because they contain validated domain types
and their implementations, not only interfaces. `V1`, `V2` and `PhaseN` names are
retained contract/history names; they do not by themselves identify the current
pack or a separate product.

| Family / entry file | Responsibility and nearby files |
| --- | --- |
| [Identifiers.cs](../../src/ReactorSim.Core/Domain/Identifiers.cs) | Stable identifiers, digests and typed coordinates |
| [ContractValidation.cs](../../src/ReactorSim.Core/Domain/ContractValidation.cs) | Valid results or code/path/message diagnostics |
| [TopologyContracts.cs](../../src/ReactorSim.Core/Domain/TopologyContracts.cs) | Nodes, faces, adjacency and validation; `Candu6CoreTopologyContracts` constructs the active layout |
| [BundleInventory.cs](../../src/ReactorSim.Core/Domain/BundleInventory.cs) | Bundle state and inventory contracts |
| [GameRefuellingContracts.cs](../../src/ReactorSim.Core/Domain/GameRefuellingContracts.cs) | Active game inventory, position plan, refuelling and energy updates |
| [AgedCoreSnapshotGeneratorV1.cs](../../src/ReactorSim.Core/Domain/AgedCoreSnapshotGeneratorV1.cs) | Seeded initial fuel history and burnup |
| [RefuellingShiftContracts.cs](../../src/ReactorSim.Core/Domain/RefuellingShiftContracts.cs) | General validated movement contracts; `RefuellingSchemeContracts` and `RefuellingEventContracts` supply scheme/event types |
| [FullCoreDiffusionDataPackContracts.cs](../../src/ReactorSim.Core/Domain/FullCoreDiffusionDataPackContracts.cs) | Pack loading/validation; `DiffusionDataPackAuditContracts` audits authored data |
| [BurnupCoefficientContracts.cs](../../src/ReactorSim.Core/Domain/BurnupCoefficientContracts.cs) | Burnup-dependent material coefficient contracts |
| [FullCoreDiffusionModelContracts.cs](../../src/ReactorSim.Core/Domain/FullCoreDiffusionModelContracts.cs) | Active full-core topology/coefficient/operator binding and solve orchestration |
| [SpatialStencilContracts.cs](../../src/ReactorSim.Core/Domain/SpatialStencilContracts.cs) | Neighbor/boundary stencil; `SpatialOperatorContracts` applies matrix-free equations |
| [SpatialEigenIterationContracts.cs](../../src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs) | Group solves and eigenvalue/power iteration; `SpatialConvergenceContracts` manages convergence |
| [SpatialRecomputeContracts.cs](../../src/ReactorSim.Core/Domain/SpatialRecomputeContracts.cs) | Spatial refresh contracts retained with the numerical infrastructure |
| [EquilibriumCoreSolverContracts.cs](../../src/ReactorSim.Core/Domain/EquilibriumCoreSolverContracts.cs) | Accepted projection, warm starts, prepared candidates, fork and commit |
| [PracticeLiquidZoneRrsContracts.cs](../../src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs) | Zone mapping, response and bounded equilibrium controller |
| [PracticeLiquidZoneTubes.cs](../../src/ReactorSim.Core/Domain/PracticeLiquidZoneTubes.cs), [PracticeAdjusters.cs](../../src/ReactorSim.Core/Domain/PracticeAdjusters.cs) | Localized absorption geometry and authored device behavior |
| [PracticeXenonContracts.cs](../../src/ReactorSim.Core/Domain/PracticeXenonContracts.cs) | Bundle poison evolution/rebinding/overlays; `PracticeXenonEquilibrium` and `PracticeXenonReference` support reference equilibrium |
| [PracticeChannelPowerReference.cs](../../src/ReactorSim.Core/Domain/PracticeChannelPowerReference.cs) | Fixed channel reference used by Game ripple scoring |
| [StaticAbsorptionOverlayContracts.cs](../../src/ReactorSim.Core/Domain/StaticAbsorptionOverlayContracts.cs) | Additional node absorption and validation |
| [AdjointContracts.cs](../../src/ReactorSim.Core/Domain/AdjointContracts.cs), [AdjointReactivityContracts.cs](../../src/ReactorSim.Core/Domain/AdjointReactivityContracts.cs) | Importance/reactivity mathematical contracts |
| [CompleteStateDigestContracts.cs](../../src/ReactorSim.Core/Domain/CompleteStateDigestContracts.cs), [Phase5CompleteStateContracts.cs](../../src/ReactorSim.Core/Domain/Phase5CompleteStateContracts.cs) | Canonical state identity/serialization infrastructure |
| [Phase7XenonSpatialStateContracts.cs](../../src/ReactorSim.Core/Domain/Phase7XenonSpatialStateContracts.cs), [Phase7XenonSpatialCouplingContracts.cs](../../src/ReactorSim.Core/Domain/Phase7XenonSpatialCouplingContracts.cs) | General poison/spatial contracts retained beside the active practice model |
| [Phase8ScenarioRuntimeContracts.cs](../../src/ReactorSim.Core/Domain/Phase8ScenarioRuntimeContracts.cs), [Phase8ScoringContracts.cs](../../src/ReactorSim.Core/Domain/Phase8ScoringContracts.cs) | Shared time/scenario/scoring contracts; Game implements the current practice loop |
| [IqsKineticsDataPack.cs](../../src/ReactorSim.Core/Domain/IqsKineticsDataPack.cs), [CanonicalKineticValidation.cs](../../src/ReactorSim.Core/Domain/CanonicalKineticValidation.cs) | Retained kinetic metadata/validation, not an alternate public runtime |
| [InfiniteCellDiffusionContracts.cs](../../src/ReactorSim.Core/Domain/InfiniteCellDiffusionContracts.cs), [SingleCellReflectiveDiffusionFixtureContracts.cs](../../src/ReactorSim.Core/Domain/SingleCellReflectiveDiffusionFixtureContracts.cs), [CanduTwoGroupLatticeReferenceContracts.cs](../../src/ReactorSim.Core/Domain/CanduTwoGroupLatticeReferenceContracts.cs) | Reference/fixture models supporting numerical checks |
| [SimulationConfiguration.cs](../../src/ReactorSim.Core/Domain/SimulationConfiguration.cs), [CommandClockContracts.cs](../../src/ReactorSim.Core/Domain/CommandClockContracts.cs), [LifecycleContracts.cs](../../src/ReactorSim.Core/Domain/LifecycleContracts.cs), [EventDiagnosticContracts.cs](../../src/ReactorSim.Core/Domain/EventDiagnosticContracts.cs) | General configuration, timing, lifecycle and diagnostic contracts |
| [RuntimeProfile.cs](../../src/ReactorSim.Core/Domain/RuntimeProfile.cs) | Optional numerical timing instrumentation |

## Data, research and verification tools

- [data/packs](../../data/packs) contains canonical authored packs;
  [Core/EmbeddedData](../../src/ReactorSim.Core/EmbeddedData) contains runtime copies.
- [reference](../../reference), [benchmarks](../../benchmarks) and
  [physics notes](../maintenance/knowledge-library.md) record provenance and
  measurements. Historical benchmark outcomes should not be assumed to describe
  the active v8 configuration.
- [tools/AgedCoreBenchmark](../../tools/AgedCoreBenchmark) contains calibration
  and aged-core audits; [ChannelReferenceBenchmark](../../tools/ChannelReferenceBenchmark)
  derives/checks channel reference behavior.
- [tools/LongRunPlaytest](../../tools/LongRunPlaytest) runs retained campaigns;
  [CpuSpatialBenchmark](../../tools/CpuSpatialBenchmark) measures solver behavior;
  [ReplayBookkeepingBenchmark](../../tools/ReplayBookkeepingBenchmark) measures replay costs.
- [tools/BrowserWireFixtures](../../tools/BrowserWireFixtures) supports wire
  fixtures. These native tools consume shared code; they do not replace the
  browser as the product acceptance path.

## Glossary

| Term | Meaning here |
| --- | --- |
| Amplitude | Operating multiplier applied to a reference-normalized spatial shape |
| Authoritative state | Accepted state owned by the shared simulation, from which client observations are produced |
| Bridge | Adapter carrying versioned commands and results between JavaScript and managed code |
| Build artifact | Generated files passed to a later verification or deployment stage |
| Bundle | Fuel object with persistent identity, mass, energy history and burnup |
| Burnup | Accumulated thermal fission energy per heavy-metal mass |
| Candidate | Calculated replacement state that has not yet been adopted by its owner |
| Channel | One of 380 addressable fuel channels, with twelve physical bundle slots |
| Compact response | Wire patch plus optional core replacement/measurements, materialized against a baseline |
| Composition root | Startup location where long-lived application objects and their dependencies are connected |
| Convergence | Satisfaction of the solver's specified numerical error criteria |
| Daily integration identity | Versioned choice of physical stepping semantics, currently `practice-daily-one-step-v1` |
| Digest | Deterministic identity of specified canonical data; state and replay digests serve different purposes |
| DTO | Data transfer object: a structure used to carry information across a component or serialization boundary |
| Effective k | Eigenvalue measuring neutron production relative to loss for a solved shape |
| Equilibrium projection | Accepted or candidate read-only numerical shape and associated reactivity/diagnostic data |
| Flux | Neutron-field intensity, represented in fast and thermal groups and measured per square metre per second |
| JSON | Text representation of structured data using objects, named fields, arrays and primitive values |
| LZC | Liquid-zone control; fourteen logical zone fill fractions in the model |
| mk | Reactivity display unit: 0.001 dimensionless reactivity |
| NodeKey | Channel/physical-slot identity used by topology and numerical arrays |
| Objective / shift | Game-owned run goal and progress, independent of which UI presents it |
| Overlay | Additional absorption bound to spatial nodes, such as device or poison effects |
| Poison | Iodine/xenon inventory affecting neutron absorption; follows bundle identity |
| Projection version | Identity/version of published power projection, distinct from day or wire sequence |
| Protocol | Agreement governing message versions, field meanings, request types and responses |
| Replay | Reconstruction of a run by applying its recorded commands to compatible initial conditions |
| Ripple | Relative channel-power deviation from a fixed channel reference |
| RRS | Reactor regulating-system model that calculates bounded liquid-zone response |
| RMS | Root mean square; here, the square root of mean squared relative channel-power errors |
| Sequence | Browser runtime dispatch ordering value; not simulation time |
| Snapshot | Presentation observation projected from accepted shared state |
| Serialization | Conversion of in-memory data into a transferable representation, such as JSON |
| Terminal | Game run ended by limits or scenario/objective lifecycle; commands cannot continue ordinary play |
| Transaction | Group of related state changes evaluated and adopted according to defined commit/failure rules |
| WASM | WebAssembly, the browser-supported executable format used by the .NET host |
| Web Worker | Separate browser execution context that communicates with the interface through messages |
| Warm start | Previous compatible flux/solve used as the numerical initial guess |
