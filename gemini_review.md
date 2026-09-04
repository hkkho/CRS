# Comprehensive Architectural & Technical Review: CANDU Refuelling Game

**Repository:** `candu`  
**Review Date:** September 2026  
**Target Environment:** .NET 10 / C# (`netstandard2.1` Core & Game assemblies) + Unity 6 (`6000.3.21f1`)

---

## 1. Executive Summary & Mission

The **CANDU Refuelling Game** repository is an ambitious, high-rigor engineering project building an interactive Unity simulation game centered on steady-state on-power refuelling of a CANDU-6 nuclear reactor (380 fuel channels, 12 bundle positions per channel = 4,560 active bundles).

### Key Product Philosophy & Priority
1. **Playability First:** The primary goal is to make a fun, responsive, playable game loop before integrating complex offline reactor physics. The player manages reactor power, corrects flux tilt, inspects burnup profiles, and schedules bi-directional bundle shifts (4-bundle or 8-bundle) across the core.
2. **Engine-Neutral Core:** All domain physics, kinetics, spatial diffusion solvers, regulating system logic, scoring, and state transitions live in `ReactorSim.Core` and `ReactorSim.Game`, keeping zero dependency on Unity or graphical runtimes.
3. **Deterministic & Bounded Time:** Simulation time advances in discrete, deterministic wall-time ticks (e.g., 100 ms). Unity frame delta time drives presentation requests only; simulation state transitions are purely reproducible and replayable via canonical hashes/digests.
4. **Offline Physics Data:** High-fidelity multi-group lattice (DRAGON5) and full-core diffusion (DONJON5) codes remain offline tools. Unity consumes compact, versioned, lawful runtime data packs.

---

## 2. Architecture & Repository Structure

```
candu/
├── src/
│   ├── ReactorSim.Core/      # Engine-neutral domain, solvers, kinetics, refuelling, scoring
│   ├── ReactorSim.Game/      # Reusable application session layer (GameSession, factory)
│   └── ReactorSim.Cli/       # Headless CLI runner, baseline policies, soak testing, replays
├── unity/
│   └── ReactorGame/          # Unity 6 project (Assets/ReactorGame.Unity)
│       ├── Assets/Plugins/   # Compiled ReactorSim.Core.dll & ReactorSim.Game.dll
│       └── Assets/Scenes/    # Bootstrap.unity
├── tests/
│   ├── ReactorSim.Core.Tests/# Unit tests for domain, solvers, kinetics, refuelling
│   ├── ReactorSim.Cli.Tests/ # CLI and replay tests
│   └── ReactorSim.Golden.Tests/ # Golden comparison and literature calibration tests
├── benchmarks/               # Solver benchmarks (P4-T08, P9-T01/02/03/04)
├── data/                     # Scenario definitions, parameter packs, golden test fixtures
├── reference/                # Literature digests, DRAGON5/DONJON5 deck specs
└── tools/                    # PowerShell build & import automation, reference scripts
```

### Architectural Seams & Data Flow

```
+-------------------------------------------------------------------------+
|                           Presentation Layer                            |
|                          (unity/ReactorGame)                            |
|                                                                         |
|  [Phase10ShellView] <-> [DashboardView] [ControlsView] [TimelineView]  |
|            ^                                                            |
|            | (Event-driven / Snapshot-based)                            |
|  [Phase8UnityRuntimeAdapter]                                            |
|            ^                                                            |
|            | (IPhase8RuntimePort)                                       |
|  [UnityRuntimePort]                                                     |
|            ^                                                            |
|  [UnityGameController] (Unscaled Update loop -> bounded tick cadence)   |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                            Application Layer                            |
|                          (src/ReactorSim.Game)                          |
|                                                                         |
|  [GameSession] (Public reusable session orchestration)                  |
|  [PracticeGameSessionFactory]                                           |
|  [GameSessionSnapshot] (Immutable snapshot projection)                  |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                            Simulation Core                              |
|                          (src/ReactorSim.Core)                          |
|                                                                         |
|  * Topology (380 channels x 12 bundles)                                 |
|  * SyntheticGameCoreStateV1 (Deterministic bundle movement & inventory) |
|  * 2-Group Spatial 3D Diffusion Solver (Jacobi power iteration)         |
|  * Kinetics & Xenon/Iodine Dynamics (I-135 / Xe-135 spatial coupling)   |
|  * Regulating Systems (LZC 14 zones, 21 Adjusters, Bulk Poison)         |
|  * Scored Scenario Runtime & Deterministic Replay Archive               |
+-------------------------------------------------------------------------+
```

---

## 3. Detailed Component Review

### 3.1 `src/ReactorSim.Core` (The Simulation Engine)

`ReactorSim.Core` is a mature, exceptionally well-tested C# library targeting `netstandard2.1`.

#### Highlights:
- **Contract-Driven Design:** Ubiquitous use of `ContractValidationResult<T>` and strict value objects (`ChannelId`, `BundlePosition`, `StableId`, `MaterialVariantId`) ensures invalid states cannot be constructed.
- **Spatial Neutron Diffusion:**
  - Full 3D finite-difference two-group neutron diffusion model (`SpatialEigenSolve`, `SpatialOperator`, `SpatialStencil`).
  - Implements power iteration with deterministic Jacobi linear solver policy.
  - Handles reflective and vacuum boundary conditions across arbitrary core stencils.
- **Kinetics & Xenon/Iodine Coupling:**
  - Non-linear Iodine-135 $\to$ Xenon-135 decay chains coupled to local thermal flux.
  - Spatial xenon oscillations and reactivity feedback modeled accurately.
- **Reactor Regulating System (RRS):**
  - **14 Liquid Zone Control (LZC) compartments:** Differential water filling for bulk reactivity and spatial flux tilt control.
  - **21 Adjuster Rods across 7 banks:** Stainless steel/cobalt absorption control for xenon override and flux flattening.
  - **Bulk Poison Addition / Purification:** Soluble boron/gadolinium in the moderator.
- **Deterministic Bundle History & Invariants:**
  - Tracks individual bundle identities, initial burnup, cumulative fission energy, heavy metal mass, insertion time, and power history.
  - Strict invariants protect against bundle duplication or inventory leakage.

---

### 3.2 `src/ReactorSim.Game` (Application Service Layer)

Extracted from previously private CLI logic to serve as the unified, reusable bridge between simulation runtimes and client presentation (Unity & CLI).

#### Highlights:
- **`GameSession`:** Wraps `Phase8ScoredScenarioRuntimeV1` and `SyntheticGameCoreStateV1`. Exposes deterministic commands:
  - `AdvanceWallMilliseconds(ulong wallMilliseconds)`
  - `QueuePowerTarget(double targetFraction)`
  - `QueueTiltTarget(double targetFraction)`
  - `SetPlaybackMode(string playbackModeId)`
  - `Pause()` / `Resume()`
  - `RefuelChannel(uint channelIndex, string directionId, ushort shiftCount, string fuelTypeId)`
- **`PracticeGameSessionFactory`:** Preconfigures standard equilibrium tutorial scenarios with balanced time models (100 ms control tick, 10x accelerated default play mode, 1x audit mode) and 128 fresh natural uranium bundles (`NAT-U-SYNTHETIC`).

---

### 3.3 `unity/ReactorGame` (Unity Presentation Layer)

The Unity project targets Unity 6 (`6000.3.21f1`).

#### Highlights:
- **Zero Simulation State in Presentation:** Unity scripts only observe immutable `Phase8UnityPresentationSnapshotV1` snapshots and dispatch `Phase8UnityInputCommandV1` commands.
- **Time Pacing in `UnityGameController`:**
  - Uses `Time.unscaledDeltaTime` to measure real-world time.
  - Accumulates wall milliseconds and dispatches bounded discrete chunks (100 ms).
  - Enforces `MaximumCatchUpTicksPerFrame = 5` to prevent frame-rate stutter or spiral-of-death catchups.
- **Pure Procedural uGUI Construction:**
  - `Phase10ShellView`: Generates responsive 16:9 canvas with navigation header and safe-area margins.
  - `Phase10DashboardView`: Displays real-time operational metrics (Power %, Tilt %, Control Margin %, Remaining Refuel Requests, Score).
  - `Phase10ControlsView`: Interactive numeric inputs and touch-friendly buttons for power/tilt targets, speed adjustments, and channel refuelling (4- or 8-bundle shifts toward End A or End B).
  - `Phase10TimelineView`: Procedural bar graphs visualizing power, tilt, and control margin trends over the last 24 snapshot samples, alongside an event log.

---

### 3.4 `src/ReactorSim.Cli` & `tests/`

- **Headless Verification:** `ReactorSim.Cli` allows headless verification of complete gameplay runs, baseline policy testing, and long-horizon soak testing (`P8T06SoakTests`).
- **Comprehensive Test Coverage:**
  - **275 total tests** across `ReactorSim.Core.Tests`, `ReactorSim.Cli.Tests`, and `ReactorSim.Golden.Tests` all execute in ~27 seconds.
  - Verification includes exact analytical solutions, manufactured spatial authority cases, and literature calibration boundaries.

---

## 4. Roadmap & Milestone Progress Review

| Milestone | Objective | Status | Assessment |
| :--- | :--- | :---: | :--- |
| **Milestone 0** | Launch readiness, flexible SDK, build scripts | **COMPLETE** | Build tools (`Prepare-UnityCore.ps1`, `Build-UnityDemo.ps1`) and clean assembly references exist and work seamlessly. |
| **Milestone 1** | Playable synthetic vertical slice | **IN PROGRESS (85%)** | Core refuelling loop, bundle transitions, runtime port, and controls are operational. The **Core Map** (380-channel grid / heat map) is the remaining deliverable. |
| **Milestone 2** | Debug menu & playtesting tools | **PLANNED** | Spec defined in Implementation Guide (backquote/F1 overlay for instant state overrides, jump-to-equilibrium, cheat inventory). |
| **Milestone 3** | Game loop polish & operational tradeoffs | **PLANNED** | Refuelling recommendations, trade-off legibility, scoring feedback, scenario variety. |
| **Milestone 4** | Full Core spatial solve integration | **PLANNED** | Connecting the full 3D two-group diffusion solver and kinetics to `GameSession` on background cadence. |
| **Milestone 5** | DRAGON5/DONJON5 realism pass | **PLANNED** | Ingestion of offline-generated multi-group cross section packs. |

---

## 5. Strengths & Engineering Highlights

1. **High Code Cleanliness & Type Safety:** Immutability, nullability hygiene, and contract validations are strictly enforced across all domain entities.
2. **Determinism by Design:** Strict separation of wall time vs. simulation time; exact floating-point reproducibility across repeats verified by automated benchmarks.
3. **Robust Separation of Concerns:** Unity assembly has no knowledge of internal matrix equations or differential equations; Core has zero reference to `UnityEngine`.
4. **Resilient UI Pacing:** Unity game controller handles frame-rate jitter gracefully without drifting simulation time.
5. **Excellent Automated Testing Baseline:** 275 tests covering unit behaviors, invariants, and golden benchmarks.

---

## 6. Identified Technical Debts, Risks & Gaps

### 1. Placeholder Core Map
- **Issue:** `Phase10ShellView` page `CoreMap` currently displays placeholder text.
- **Impact:** Players must enter channel numbers manually in the Controls page (0–379) rather than clicking on a core lattice heat map.
- **Remedy:** Implement `CoreMapView` with a 380-channel layout showing radial burnup / power color-coding and 12-bundle axial drill-down.

### 2. Vertical Slice vs. Full Core Model Bridge
- **Issue:** `GameSession` currently tracks bundles via `SyntheticGameCoreStateV1` and scenario state via `Phase8ScoredScenarioRuntimeV1`. The rich full-core spatial diffusion solver (`SpatialEigenSolve`) is not yet actively executing during live session ticks.
- **Impact:** Refuelling alters inventory and recorded bundle shifts, but does not yet trigger a real-time 3D neutron flux re-solve in the vertical slice.
- **Remedy:** Follow Milestone 4 plan: invoke `SpatialRecompute` on a controlled background simulation cadence, maintaining high UI frame rates.

### 3. Pure Imperative UI Layout
- **Issue:** All Unity views (`Phase10ShellView`, `Phase10ControlsView`, etc.) construct their entire GameObject hierarchy and UI components purely in C# code.
- **Impact:** While this eliminates prefab corruption and makes headless testing easier, code files are lengthy (~800 lines per view) and visual tweaking requires recompilation.
- **Recommendation:** Maintain clean helper methods / component builders or modularize sub-panels to keep view code maintainable.

---

## 7. Detailed Implementation Plan: What to Implement Next

```
+-------------------------------------------------------------------------------+
|                        ORDERED IMPLEMENTATION ROADMAP                         |
|                                                                               |
|  [Slice 1: Interactive Core Map]                                              |
|      - 380-channel lattice heat map                                           |
|      - Channel selection & 12-bundle axial inspection                         |
|      - Pre-refuelling outcome preview & commit                                |
|                                |                                              |
|                                v                                              |
|  [Slice 2: Closed-Loop Refuelling & Score Response]                           |
|      - Local reactivity & flux tilt synthetic response                        |
|      - Immediate score points & trend feedback                                |
|                                |                                              |
|                                v                                              |
|  [Slice 3: In-Game Debug & Playtesting Menu]                                  |
|      - F1 / Backquote overlay                                                 |
|      - Time-jump, manual state overrides, inventory grant                     |
|                                |                                              |
|                                v                                              |
|  [Slice 4: Operational Decision & Guidance Polish]                            |
|      - Legible tradeoffs (flattening vs. burnup vs. cost)                     |
|      - Candidate channel recommendation hints                                 |
|                                |                                              |
|                                v                                              |
|  [Slice 5: Core Spatial Diffusion Model Integration]                         |
|      - Background cadence 3D two-group solve                                  |
|      - Immutable presentation snapshots for full core physics                 |
+-------------------------------------------------------------------------------+
```

### Slice 1: Interactive 380-Channel Core Map (Milestone 1 Completion)

#### Objective
Replace the placeholder text on the **Core Map** page of the Unity shell with an interactive 380-channel CANDU-6 core face, allowing the player to select channels, inspect bundle burnup profiles, preview a refuelling shift, and commit operations visually.

#### Detailed Layout Mockup:
```
+-------------------------------------------------------------------------------------+
|                                   CoreMapView                                       |
|                                                                                     |
|  +------------------------------------+  +---------------------------------------+  |
|  |       380-Channel Lattice Map      |  |         Selected Channel Details      |  |
|  |                                    |  |                                       |  |
|  |     . . [C190] . .                 |  | Channel: 190 | Avg Burnup: 7.2 MWd/kg |  |
|  |   . . . . . . . . .                |  | Flow: End A -> End B                  |  |
|  |  . . . . [Sel] . . .               |  |                                       |  |
|  |   . . . . . . . . .                |  | 12-Bundle Axial Burnup Profile:       |  |
|  |     . . . . . .                    |  | [B1][B2][B3][B4][B5][B6]...[B12]      |  |
|  |                                    |  |                                       |  |
|  | Color: Burnup (Green->Yellow->Red) |  | [Preview 4-Shift End A] [Commit Shift]|  |
|  +------------------------------------+  +---------------------------------------+  |
+-------------------------------------------------------------------------------------+
```

#### Files & Responsibilities:
1. **`unity/ReactorGame/Assets/ReactorGame.Unity/CoreMapView.cs` (New):**
   - **Lattice Grid:** Builds a 22×22 grid layout filtering out corner coordinates to render the 380 circular fuel channels.
   - **Heat Map Color Coding:** Colors channels by average burnup (e.g., fresh green $< 4$ MWd/kg $\to$ nominal yellow $\to$ depleted red $> 8$ MWd/kg) or local power fraction.
   - **Selection & Inspector:** Clicking a channel displays its 12 bundle positions horizontally with individual burnup bars, material type, and insertion age.
   - **Refuelling Preview:** Visualizes bundle shift before committing (e.g., shifting 4 fresh bundles from left pushes 4 depleted bundles out to the right).
   - **Commit Action:** Dispatches `Phase8UnityInputCommandV1.RefuelChannel` directly from the Core Map.
2. **`unity/ReactorGame/Assets/ReactorGame.Unity/Phase10ShellView.cs`:**
   - Mount and bind `CoreMapView` inside `Page.CoreMap`.
3. **`unity/ReactorGame/Assets/ReactorGame.Unity/UnityGameController.cs`:**
   - Bind `CoreMapView` to the adapter during `Initialize()`.

#### Verification:
- **EditMode Test:** Verify `CoreMapView` builds 380 channel buttons and correctly projects bundle states for channel 0 and channel 379.
- **PlayMode Smoke:** Launch `Bootstrap.unity`, navigate to `Core Map`, select Channel 190, execute a 4-bundle shift toward End A, and verify bundle burnup bars update immediately.

---

### Slice 2: Closed-Loop Synthetic Reactivity & Scoring Response

#### Objective
Coupling refuelling actions directly to localized synthetic reactivity and flux tilt changes so that the reactor reacts immediately to player actions.

#### Technical Scope:
1. **Localized Flux & Tilt Feedback in `SyntheticGameCoreStateV1` / `GameSession`:**
   - Channels in outer/inner regions and north/south/east/west quadrants contribute to the core's total reactivity and absolute tilt fraction.
   - Inserting fresh fuel increases local reactivity in that quadrant, moving flux tilt toward that quadrant.
   - Discharging high-burnup bundles removes poison/depleted fuel, producing an immediate positive reactivity delta.
2. **Scoring Feedback:**
   - Award points for:
     - High average discharge burnup (effective fuel utilization).
     - Keeping flux tilt below threshold ($\le 5\%$).
     - Keeping total reactor power within operating envelope ($0.98 - 1.02$).
   - Penalize:
     - Premature discharge of fresh fuel.
     - Saturated liquid zone controllers or excessive control movement.

#### Verification:
- Unit test in `GameSessionTests.cs` showing that refuelling a top quadrant channel shifts `AbsoluteTiltFraction` and awards positive `ScoreTotal`.

---

### Slice 3: In-Game Debug & Playtesting Menu (Milestone 2)

#### Objective
Provide a non-intrusive debug menu toggled with `~` (Backquote) or `F1` allowing the project owner and developers to jump to any game state in under a minute without modifying code or save files.

```
+--------------------------------------------------------------------+
|                         DEBUG PLAYTEST MENU (F1)                   |
|                                                                    |
|  [Time Controls]                                                   |
|    Pause / Single Step / Time Scale: [1x] [10x] [60x]              |
|    Jump Time: [+1 Hour] [+1 Day] [Jump to Equilibrium]             |
|                                                                    |
|  [Core State Override]                                             |
|    Selected Channel: [ 190 ]  Set Burnup Band: [Fresh/Mid/Depleted]|
|    Scale Local Power: [ -10% | +10% ]  Scale Tilt: [ -5% | +5% ]   |
|                                                                    |
|  [Inventory & Cheats]                                              |
|    [Grant +100 Bundles]  [Clear Pending Actions]  [Reset Score]    |
|                                                                    |
|  [Snapshot & Diagnostics]                                          |
|    [Save State JSON]  [Load State JSON]  [Copy State Digest Hash]  |
|    Recent Event Digest: "Channel 190 refuelled (x4 NAT-U)"         |
+--------------------------------------------------------------------+
```

#### Components to Implement:
1. **`unity/ReactorGame/Assets/ReactorGame.Unity/DebugMenuView.cs`:**
   - Implements uGUI overlay on top sorting layer.
   - Input listener for `KeyCode.BackQuote` / `KeyCode.F1`.
   - Exposed debug methods connecting to `GameSession`.
2. **`src/ReactorSim.Game/GameSession.cs`:**
   - Add explicit debug methods marked `[Conditional("DEBUG")]` or segregated in a `DebugSessionExtensions` class to prevent polluting release gameplay scoring.

---

### Slice 4: Operational Legibility & Channel Recommendations (Milestone 3)

#### Objective
Turn refuelling into an engaging strategy loop with clear trade-offs and assistive guidance.

#### Technical Highlights:
- **Refuelling Recommendation Filter:** Highlight high-burnup channels that are prime candidates for refuelling based on axial burnup symmetry and discharge efficiency.
- **Trade-off Legibility:** Show tooltips comparing:
  - *Immediate gain:* Local power boost & flux tilt correction.
  - *Cost:* Fresh bundle expenditure & temporary xenon transient spike.
- **Audio/Visual Polish:** Sound cues and animations for bundle ram actuation, bundle discharge, and high-tilt warning alarms.

---

### Slice 5: Connecting the Full 3D Two-Group Spatial Solver (Milestone 4)

#### Objective
Upgrade the synthetic practice response model to the full `SpatialEigenSolve` finite-difference diffusion engine without causing UI frame drops.

#### Architecture:
1. **Cadence Decoupling:**
   - UI runs at 60 FPS (16.6 ms).
   - Scenario advances at fixed 100 ms wall ticks.
   - Heavy 3D neutron spatial recomputations run on a background worker or on a 1-second cadence, publishing immutable snapshots to the game session.
2. **Preview Approximation:**
   - Real-time hover preview uses a fast perturbation-theory approximation ($O(1)$) rather than full eigenvalue iteration ($O(N)$).
   - Full Jacobi power iteration executes upon operation commit.

---

## 8. Summary of Immediate Next Tasks

| Priority | Task | Target Assembly | File(s) |
| :---: | :--- | :--- | :--- |
| **1** | Implement `CoreMapView.cs` (380-channel UI + 12-bundle inspection) | `ReactorGame.Unity` | `unity/.../CoreMapView.cs`, `Phase10ShellView.cs` |
| **2** | Wire `CoreMapView` into `UnityGameController` and `Bootstrap.unity` | `ReactorGame.Unity` | `unity/.../UnityGameController.cs`, `Bootstrap.unity` |
| **3** | Add EditMode & PlayMode tests for Core Map interaction | `ReactorGame.Unity` | `unity/.../Tests/Editor/CoreMap.EditModeTests.cs` |
| **4** | Implement `DebugMenuView.cs` (F1 overlay for playtesting) | `ReactorGame.Unity` | `unity/.../DebugMenuView.cs`, `UnityGameController.cs` |
| **5** | Couple bundle shifts to localized tilt/power synthetic delta | `ReactorSim.Game` | `src/ReactorSim.Game/GameSession.cs`, `SyntheticGameCoreStateV1.cs` |
