# CANDU web playtest repository triage handoff

Date: 2026-09-13  
Prepared for: a follow-on AI responsible for independently triaging the repository and proposing a new implementation plan  
Scope: diagnosis only; this report does not implement physics or presentation changes

## Executive summary

The repository contains a real deterministic 380-channel by 12-bundle simulation path, a .NET-to-WebAssembly bridge, and a playable Phaser browser client. The deployed site loads the authoritative bridge and accepts refuelling commands. However, the current player experience does not substantiate several prior completion claims.

The central failure is a mismatch between backend capability, presentation, and acceptance criteria:

- Fourteen-zone liquid-zone-controller state exists in Core, Game, the browser bridge, and the TypeScript protocol, but the Phaser operations scene does not display it.
- The player-facing headline axial tilt is an older scenario-runtime scalar rather than a signed full-core moment derived from the current spatial solution. The spatially derived per-channel tilt also discards direction.
- The equilibrium solver normalizes power back to the requested amplitude. Refuelling changes bundle/channel shape and reactivity, but it cannot create the whole-reactor power excursion implied by the UI and prior claims.
- For the measured representative refuelling operation, the liquid-zone controller returned zero movement in every zone. Existing tests explicitly accept this fallback behavior.
- The Phaser rewrite is a coherent tactical restyle, but it is made from procedural primitives and text. There are no product-authored image, animation, or audio assets in the web source. It should not be characterized as a high-fidelity graphics upgrade.
- Tests pass because they primarily prove deterministic execution, atomicity, bridge parity, payload shape, and state mutation. They do not establish physically credible response or visible player feedback.
- The stable Vercel deployment identifies a commit that is not present in any local ref. This weakens deployment traceability, although direct production measurements reproduce the same disputed behavior as the local source.

The next AI should treat the user's playtest observations as verified defects and should not infer feature completion from the existence of protocol fields or passing tests.

## Governing product constraints

Read `AGENTS.md`, `README.md`, and `docs/IMPLEMENTATION_GUIDE.md` before proposing changes.

Important constraints:

- `web/candu-playtest` and its Vercel deployment are the primary product and acceptance path.
- Unity feature work is paused. Do not reopen Unity presentation, controls, or polish work.
- `src/ReactorSim.Core` owns deterministic simulation transitions and spatial physics.
- `src/ReactorSim.Game` owns the reusable session and presentation projection.
- Phaser must consume authoritative state and must not become a second simulation authority.
- Shutdown, scram, accident progression, and operator-training scenarios are out of scope.
- The current data pack is project-authored and synthetic. DRAGON5/DONJON5 remain offline future-data sources.

## Repository and release state

At audit time:

| Item | Value |
|---|---|
| Working tree | Clean at audit start; this report is the only source-tree change made by the audit; generated build outputs are ignored |
| Checked-out branch | `codex/01a0884b9b877ff0a8ea804345146aac` |
| Checked-out commit | `11911adf2fbefc992359bcf4261e5897e2a472b2` — `Fix web shift transition freeze` |
| Local `master` | `87fe36726cbd9123087dd7635240f79228cfc38e` — `perf(wasm): ship compact full-aot browser build` |
| Stable deployment | `https://crs-candu-playtest.vercel.app` |
| Deployment-reported commit | `ba1362e5925dfa75a922012e190114b83074a832` |
| Deployment provenance issue | The deployment-reported commit cannot be resolved in the local object database or any local branch |

The deployment workflow builds the .NET browser host, stages WebAssembly, runs frontend tests, creates a Vercel prebuilt output, deploys it, and smoke-tests the stable alias. Root `vercel.json` disables ordinary Git-triggered Vercel deployment, so GitHub Actions is the intended publisher.

Before making a new plan, reconcile the deployed SHA with the authoritative remote branch and determine whether the current checkout contains all production source.

## Runtime architecture currently present

```text
Phaser OperationsScene
        |
SessionController / worker-hosted protocol bridge
        |
ReactorSim.Browser PlaytestBridge
        |
ReactorSim.Game GameSession
        |
EquilibriumCoreSolver + PracticeLiquidZoneRrs
        |
380 channels x 12 bundle nodes, synthetic two-group coefficient pack
```

The bridge correctly fails closed when authoritative WebAssembly is unavailable. Both the local production-shaped build and stable deployment reached `CANDU play mode online. 380 channels available.` during this audit.

## Verified player-facing defects

### 1. No liquid-zone response display

`web/candu-playtest/src/protocol.ts` defines `CanduRrsZoneSnapshot` and carries all 14 zones in `CanduRrsSnapshot.zones`. The bridge serializes these values from `GameRrsPresentationSnapshot`.

`web/candu-playtest/src/scenes/OperationsScene.ts` contains no references to:

- `snapshot.rrs`
- `rrs.zones`
- `averageFillFraction`
- `minimumFillFraction`
- `maximumFillFraction`
- `appliedFillCommand`
- `shapeError`

The live scene therefore cannot show zone fill, target, measured response, shape error, applied command, or zone exhaustion. The current screenshot shows power, axial tilt, score, channel heat field, selected-channel physics, bundle power profile, and direct refuelling controls, but no RRS resource surface.

This gap was already documented in `docs/NEXT_SPRINT_NOTES.md`, including the missing 14-zone strip and selected-zone target/measured/error display. It was not subsequently implemented in Phaser.

### 2. Headline axial tilt is not the refuelling-induced spatial tilt

`GameSession.CreateSnapshot()` publishes headline tilt through `CurrentTiltFraction()`. That method returns:

```csharp
Clamp(Math.Abs(_runtime.AbsoluteTiltFraction), 0.0, 1.0)
```

This value belongs to the older Phase 8 scenario runtime. It is not recomputed from `EquilibriumCoreProjectionV1.ShapeNodePowerWatts` after refuelling.

Separately, `CreateCorePresentationSnapshot()` computes each channel's axial first moment from its 12 bundle powers. That result is also wrapped in `Math.Abs`, so End A versus End B direction is discarded.

Consequences:

- The global HUD can stay at `+0.00%` after a spatially asymmetric refuelling operation.
- The selected channel can simultaneously report a substantial local tilt.
- Neither value preserves physical direction, despite the UI formatting values as signed.
- Queueing a scenario tilt target operates on a different authority than the spatial tilt shown in channel detail.

The plan must first define a single signed axial-tilt convention, its spatial aggregation, units, expected symmetry, and which layer owns it.

### 3. Whole-reactor power response is structurally flat

The equilibrium projection is normalized to the requested reference power. Presentation then multiplies the normalized shape by the scenario-runtime amplitude. In `GameSession.CreateCorePresentationSnapshot()`:

```csharp
double physicalShapeScale = amplitude;
double totalPowerWatts = projection.ShapePowerWatts * physicalShapeScale;
```

Because `projection.ShapePowerWatts` is the normalized equilibrium target, refuelling changes spatial distribution, effective multiplication factor, and reactivity, but not the displayed total-power amplitude when the runtime target remains 1.0.

This is internally consistent with the current "regulated steady-state practice projection" description, but it does not provide the immediate power response that the player was led to expect. A new plan must explicitly decide whether gameplay should expose:

- an unregulated post-refuelling state followed by regulated settling;
- paired before/after equilibrium states with clear shape and reactivity deltas; or
- another deterministic response model that does not falsely claim transient kinetics.

Do not solve this by inventing a Phaser-only animation or number.

### 4. Liquid-zone control can legally do nothing after refuelling

The RRS implementation uses a project-authored local Jacobian surrogate, bounded fill movement, full-core verification, and optional correction. Important constants include:

- 14 logical zones;
- initial fill `0.5`;
- maximum movement per event `0.08`;
- controller tolerance `1e-4`;
- response identity `synthetic-practice-liquid-zone-response-jacobian-v1`.

For the representative deployed operation, the RRS measured different zonal fractions after refuelling but rejected the proposed controlled candidate and retained its baseline fills. `tests/ReactorSim.Game.Tests/LiquidZoneRrsGameSessionTests.cs`, test `RejectedVerificationDoesNotConsumeCorrectionSolveOrReplaceBaseline`, explicitly requires:

- post-refuel zone fills equal pre-refuel zone fills;
- all applied fill commands equal zero;
- the baseline overlay remain active.

Thus this behavior is not an accidental frontend omission. It is a backend fallback currently encoded as correct behavior. The next triage should determine why the Jacobian command fails full-core verification and whether its sign, scaling, residual weighting, target definition, or acceptance policy is unsuitable.

## Direct production measurement

The following was measured against `https://crs-candu-playtest.vercel.app` by initializing a fresh authoritative play session and committing:

```json
{
  "channelIndex": 210,
  "directionId": "toward-end-a",
  "shiftCount": 4,
  "fuelTypeId": "NAT-U-SYNTHETIC"
}
```

The command was accepted and reported an average discharged burnup of `5.99 MWd/kg HM`.

| Observable | Before | After |
|---|---:|---:|
| Fresh bundles | 128 | 124 |
| Refuelling operation count | 0 | 1 |
| Headline power | 100.0% | 100.0% |
| Headline axial tilt | 0.0% | 0.0% |
| Channel 210 relative power | 67.7617% | 72.4121% |
| Channel 210 local axial moment | approximately 0% | 5.1264% |
| All 14 zone fills | 58% | 58% |
| All 14 applied fill commands | prior state | 0 |

The largest absolute zonal shape error increased from approximately `0.0288%` of total power to `0.0888%` of total power, but the controller still applied no fill movement. The same numerical behavior was reproduced from the local production-shaped source build.

This is the clearest compact reproduction for future work.

## Graphics and interaction state

The current web client is one fixed `1600 x 900` Phaser canvas scaled with `FIT`. It has:

- a dark tactical title screen;
- a 22 by 22 stepped channel face containing 380 selectable channels;
- channel heat coloring and flow arrows;
- a selected-channel dossier;
- effective-k and reactivity text;
- a 12-position axial bundle-power bar profile;
- direct four/eight-bundle controls and a transfer animation;
- keyboard and pointer input.

It does not have project-authored image, SVG, audio, or video assets. Media found under `web/candu-playtest` belongs only to dependencies in `node_modules`. The product visuals are generated from Phaser graphics primitives and text.

The Phaser rewrite commit deleted the previous large React application, HTML diagnostic surfaces, replay tooling, and related CSS/tests, replacing them with a monolithic `OperationsScene.ts`. The rewrite improved visual coherence and made a game-like canvas, but also reduced diagnostics, accessibility surface, and information density. A future visual plan should start from a concrete art/interface brief and acceptance captures, not from a generic request to "upgrade graphics."

## How previous work became overstated

The commit history shows this sequence:

1. Backend equilibrium/RRS contracts and browser protocol diagnostics were implemented.
2. Detailed RRS dashboard, refuelling outcome deltas, and score/RRS timeline feedback were implemented in Unity-only files.
3. The repository pivoted to make Vercel/Phaser the primary product through `AGENTS.md`, README, implementation-guide, and benchmark changes.
4. The Unity player-facing features were not ported to Phaser.
5. Documentation continued describing authoritative power, tilt, RRS, score, and refuelling feedback together, allowing backend availability to be read as web feature completion.
6. Acceptance remained dominated by unit/protocol/benchmark checks instead of an owner-visible vertical-slice contract.

The key distinction for future status reporting must be:

- implemented in Core;
- projected by Game;
- serialized by Browser;
- consumed by Phaser;
- visibly usable on the deployed acceptance surface;
- behaviorally validated against an explicit physical/gameplay expectation.

No item should be called complete until the relevant final categories are satisfied.

## Test status and coverage gaps

Checks run during this audit:

| Check | Result |
|---|---|
| `npm test` in `web/candu-playtest` | 6 files, 28 tests passed |
| `npm run build` | Passed; large-bundle warning for approximately 1.28 MB minified main chunk |
| `tools/Test-DotNet.ps1 -Suite Game` | 11 tests passed |
| `tools/Test-DotNet.ps1 -Suite Browser` | 9 tests passed |
| Local authoritative WASM benchmark | Passed after WebAssembly was staged; representative refuel approximately 1.88 seconds in the non-AOT local build |
| Stable deployed bridge startup | Passed; 380-channel play mode online, no observed console errors |

What these checks establish:

- protocol parsing and compact response materialization;
- deterministic command streams and digests;
- authoritative bridge availability;
- refuelling inventory mutation and core replacement;
- invalid-command rollback;
- frontend helper behavior and scheduler mechanics;
- production compilation.

What they do not establish:

- a visible 14-zone response display;
- nonzero or physically sensible RRS action after representative refuelling;
- a signed, spatially derived whole-core axial tilt;
- a meaningful immediate global power response;
- directionally correct bundle/channel response;
- causal before/after feedback readable during normal play;
- visual quality relative to an agreed target;
- responsive usability at both required desktop and narrow-screen sizes;
- accessibility of canvas-only controls and data;
- acceptable cold-start and refuelling latency on the deployed AOT build.

The current browser test only requires the selected channel's serialized core data to change. It does not require power, tilt, RRS, or score deltas to have any expected sign or magnitude. Game tests emphasize replay equality and deliberately accept a zero-command RRS fallback.

## Important files for triage

| Area | Files |
|---|---|
| Product constraints | `AGENTS.md`, `README.md`, `docs/IMPLEMENTATION_GUIDE.md` |
| Known gap history | `docs/NEXT_SPRINT_NOTES.md` |
| Phaser presentation | `web/candu-playtest/src/scenes/OperationsScene.ts`, `drawing.ts`, `visuals.ts`, `projection.ts` |
| Browser protocol/client | `web/candu-playtest/src/protocol.ts`, `bridge.ts`, `sessionController.ts`, `wasmWorker.ts` |
| Browser bridge authority | `src/ReactorSim.Browser/PlaytestBridge.cs`, `PlaytestProtocol.cs` |
| Session/projection authority | `src/ReactorSim.Game/GameSession.cs`, `CorePresentationContracts.cs`, `PracticeGameSessionFactory.cs` |
| RRS physics | `src/ReactorSim.Core/Domain/PracticeLiquidZoneRrsContracts.cs`, `StaticAbsorptionOverlayContracts.cs` |
| Equilibrium/spatial solve | `src/ReactorSim.Core/Domain/EquilibriumCoreSolverContracts.cs`, `FullCoreDiffusionModelContracts.cs`, `SpatialEigenIterationContracts.cs` |
| Focused tests | `tests/ReactorSim.Game.Tests/LiquidZoneRrsGameSessionTests.cs`, `PracticeRefuellingCampaignTests.cs`, `tests/ReactorSim.Browser.Tests/PlaytestBridgeTests.cs`, web `*.test.ts` files |
| Deployment | `.github/workflows/deploy-candu-playtest.yml`, `web/candu-playtest/vercel.json`, `tools/Build-BrowserWasm.ps1` |

## Questions the new plan must answer

1. What exact signed definition of axial tilt should be authoritative: full-core axial first moment, zone-weighted tilt, detector-like metric, or another documented surrogate?
2. Should refuelling expose an immediate unregulated response, a sequence of deterministic regulated states, or only equilibrium deltas? What claims are physically supportable?
3. Why does the current 14-variable Jacobian proposal fail verification for a representative refuel, and what objective should the regulator minimize?
4. What zone grouping is intended for player comprehension, and how should the 14 zones map visibly onto the channel face and selected-zone detail?
5. Which quantities must visibly change after every accepted refuelling operation, and what are acceptable dead-band cases?
6. What is the intended graphics target? Obtain reference images or a written art/UI brief before estimating an upgrade.
7. Which debug/playtest controls are required to expose full state and accelerate owner acceptance without adding a second authority?
8. What browser-level acceptance tests will assert actual cause and effect, not merely successful commands and changed hashes?
9. Which remote commit is deployed as `ba1362e5...`, and how will future deployments maintain source-to-production traceability?

## Suggested triage order, not an implementation plan

1. Reconcile repository and deployment provenance.
2. Write down authoritative physical/gameplay definitions for signed tilt, post-refuel power response, and RRS response.
3. Build a small deterministic command matrix across central/peripheral channels, both directions, and four/eight-bundle shifts; record expected signs and qualitative trends.
4. Diagnose the RRS verification rollback using that matrix before changing UI.
5. Define the minimum player-visible vertical slice: before/after response, 14-zone resource display, selected-zone detail, warnings, and score consequence.
6. Define a graphics target and acceptance captures separately from the physics contract.
7. Replace shallow mutation assertions with focused Core, Game, Browser, and deployed-browser behavioral tests.
8. Implement in small web-first slices while keeping the authoritative boundaries intact.

## Bottom line

The repository is not empty or fake: refuelling mutates a real authoritative bundle inventory and recomputes a deterministic full-core equilibrium shape. The failure is that physically important effects are normalized away, disconnected from headline metrics, rolled back by the controller, or never rendered. Prior status language collapsed these distinctions and reported infrastructure as completed gameplay.

The next plan should begin from measured player-visible behavior, explicit signed physical definitions, and deployed browser acceptance—not from the current list of classes, protocol fields, or passing tests.
