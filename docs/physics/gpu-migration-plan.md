# GPU spatial solver migration

Branch: `gpu-spatial-migration`. Start: 2026-10-01.

Paused at the user's request on 2026-10-01. Work continues on
`cpu-parallel-experiment`; see [CPU parallel execution](cpu-parallel-experiment.md).
The read-only experiments and their measurements are retained for reference.

Stage 1 is implemented and measured: see [prototype results](gpu-prototype-results.md).
Kernel agreement passed on NVIDIA Ampere. The initial f32 residual floor failed
the former 1e-8 policy. On 2026-10-01 the user authorized loosening the inner
relative residual tolerance by one decade to 1e-7. The rebuilt prototype passed
that residual gate in all 32/128-step cases on this adapter; complete candidate
validation and cross-device precision checks remain stage 2 gates before live acceptance.

## Objective and boundaries

Reduce the approximately 710 ms p95 scheduled-solve stalls in the browser game.
The measured bottleneck is four spatial candidates per RRS event, with many
operator/Jacobi iterations. Keep iodine/xenon, fuel movement, inventory, clocks,
scoring, control policy, candidate validation and transaction commit in C#.
Core owns equations, compact data, ordering, provenance and numerical kernels.
BrowserHost supplies GPU execution; the web client remains transport/presentation.

The existing CPU solver stays the default and reference. No shader result may
commit live state during the prototype stages. Do not introduce runtime external
physics tools, shutdown, scram, accidents, or full-plant operations.

GPU support is optional. WGSL offers f32 (and optional f16), not native f64.
Cross-device bitwise equivalence must not be assumed. The current inner relative
tolerance is now 1e-7. The initial measured f32 floor of roughly 6-8e-8 has little
headroom, so full-solver and cross-device acceptance must still be measured.
Sources: [WGSL floating-point types](https://www.w3.org/TR/WGSL/#floating-point-types),
[WebGPU specification](https://www.w3.org/TR/webgpu/).

## Stages and gates

### 1. Portable operator and fixed-step experiment — initial implementation

- Export the accepted effective coefficients, including zone/xenon absorption,
  in canonical node/neighbor/boundary order. Retain SI units and separate groups.
- Author operator and Jacobi WGSL in Core as a versioned embedded resource.
  BrowserHost executes it with persistent buffers within each fixed-step batch.
- Generate an independent f64 reference with the existing CPU operator. Measure
  compilation, upload, submission/readback, CPU reference and fixture-export costs.
- Exercise both groups, zero/odd/even iteration counts, refuelling, changing
  poison/burnup, moderator cells and reflective boundaries. Validate malformed
  buffers, absent adapter and rejected shader/device results.
- Initial diagnostic agreement target: normalized infinity error <= 2e-6 for
  operator outputs and fixed-step flux. This is a packing/kernel test target,
  **not permission to relax the production convergence policy**.
- Record the current-policy residual gate separately. Do not call fixed steps a
  converged inner solve or infer full-game speedup from a kernel benchmark.

### 2. Complete numerical solve and precision decision

The complete coupled experiment is now implemented, including both inner solves,
fission/downscatter, hierarchical reductions, eigenvalue update, normalization
and outer convergence. It remains read-only. Its f64 verification includes both
successive states, per-group flux, node power and the current fourteen-region
mapping. See [coupled validation](gpu-coupled-validation.md) for the rollout
decision and measurements; this does not complete stages 3-5.

- Add Core-owned fission/downscatter, normalization, eigenvalue update and
  hierarchical residual/production reductions. Keep flux and reductions resident
  on the GPU; avoid CPU readback after every iteration.
- Bound iterations and check invalid values and failure reasons explicitly. GPU
  error flags alone cannot reproduce every CPU IEEE-754 failure behavior.
- Compare complete GPU candidates against f64 CPU solves under identical
  coefficients, warm starts, groups and policy. Retain the current convergence
  criteria unless an explicit, separately tested model change is agreed.
- If f32 cannot satisfy the current policy, evaluate GPU predictors followed by
  authoritative CPU correction, or mixed precision/compensated arithmetic.
  Do not silently accept a tolerance floor, clamp negative values, or label a
  stalled residual converged.
- Gate: finite nonnegative accepted flux, CPU-verified residual/power balance;
  initial comparison budgets <= 1e-4 normalized flux/power error, <= 0.05 mk
  reactivity difference, <= 0.1 percentage point zone-fill difference, and
  <= 0.0005 regional power-fraction difference. These are proposed limits;
  tighten them if tests reveal different RRS acceptance branches or thresholds.

### 3. Asynchronous candidate/commit seam

- Introduce backend-independent numerical requests/results in Core, carrying
  inventory, topology, coefficient, poison, zone-layout and kernel identities,
  request generation, warm-start binding, policy and precision identity.
- Game prepares immutable candidates; BrowserHost runs the backend asynchronously;
  C# checks all bindings and numerical acceptance before one atomic commit.
- Serialize live commands in the worker. A reset/reconfiguration cancels or
  invalidates outstanding work; stale GPU results must never replace newer state.
- On unsupported adapter, validation failure, device loss, timeout or nonconvergence,
  discard the candidate and rerun the same immutable input on CPU. Keep the live
  state and clock unchanged until acceptance. Define bounded retry/fallback policy.
- Test refuel/Designer/pause/reset races, stale results, failure rollback, resource
  cleanup and unsupported-device behavior. This seam is not implemented in stage 1.

### 4. Shadow RRS integration and replay semantics

- Run GPU solves beside CPU results without allowing them to affect gameplay.
  Cover seeded aged cores, centre/peripheral refuels, both directions/shift sizes,
  long poison history, power changes, zone layouts and nonfuel/reflection edits.
- Compare candidate/controller acceptance branches and all user-visible measures.
- Preserve exact CPU replay as the default. Before enabling a differing GPU result,
  explicitly decide how backend/precision identity and accepted numerical results
  enter replay records. A CPU restart must not pretend to reproduce GPU bits.
- Prefer GPU prediction plus deterministic CPU finalization if exact reproducible
  state remains required. Do not round live state merely to hide differences.

### 5. Opt-in rollout

- Benchmark complete worker commands, including request preparation, transfers,
  GPU kernels, verification, snapshot construction and fallback frequency.
- Gate on representative hardware: scheduled-solve p95 below 250 ms and at least
  2x improvement over the same-machine AOT CPU control, without weakening stage 2
  numerical gates. Initial target is experimental, not a promised speedup.
- Test integrated/discrete GPUs and software/no-adapter paths across supported
  browsers. Report driver, browser, adapter, fallback status and power preference.
  Software Vulkan/SwiftShader results do not establish hardware GPU speedup.
- Keep the CPU default until the gates pass. Run repository .NET/browser checks,
  local production smoke/reproduction, then the Vercel path before changing the
  deployed default. No deployment is part of this initial slice.

## First-slice deliverables

`GpuSpatialPrototypeFixtureV1` owns the CSR-like packing and f64 references.
`spatial-prototype-v1.wgsl` owns the operator/Jacobi arithmetic. The read-only
browser export supplies a versioned fixture; `gpuSpatialPrototype.mjs` only
allocates buffers, dispatches the supplied kernels, checks results and cleans up.
The executor batches all fixed iterations into one submission and performs one
final readback; pipeline compilation is cached by actual kernel source.

The benchmark records limitations and adapter availability. The game remains
CPU-driven throughout this slice. Existing uncommitted project changes are kept
on this branch and are not automatically committed as migration work.

## Running the initial prototype

Build the AOT host with `tools/Build-BrowserWasm.ps1 -RunAOTCompilation
-OmitPrecompressedAssets`, then build/preview `web/candu-playtest` normally.
From that web directory:

```powershell
npm run test:gpu
npm run benchmark:gpu -- http://127.0.0.1:4174
```

The harness uses full Chromium (`channel: chromium`). `--hardware` explicitly
enables experimental WebGPU and selects D3D11 for ANGLE on Windows; adapter
metadata still determines which WebGPU device actually runs. `--software`
requests SwiftShader Vulkan and is for correctness only. Both are isolated
browser launch flags, not machine settings or production-browser requirements.
The default run records unsupported devices explicitly.

The CPU fixture source is the existing file-based production WASM worker. A
separate GPU module worker executes the exported data. A blob worker cannot
bootstrap this .NET host reliably in this environment, so the harness does not
attempt that. Fixture export and GPU worker round-trip costs are recorded
separately from executor upload/submission/readback timings. JSON fixture
transport is diagnostic, not the planned live binary request contract.
