# P9-T03 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P9-T03 Core
solver profiling task after P9-T02 and the existing P4-T08 synthetic solver
baseline.

## Task

Profile the existing engine-neutral Core spatial solver on its frozen P4-T08
three-node synthetic case. Record per-solve latency and current-thread
allocation distributions, exact deterministic solver outputs, and explicit
topology/array workload descriptors. The result is observation evidence only;
it must not select a performance budget, alter equations, or optimize code.

## Approved scope

- Add a versioned P9-T03 profiling-parameter manifest bound to the exact
  P4-T08 scenario SHA-256
  `8a53f7519a3b6597d3382c9e91df356474d7d1e72e8ce5d0055e30ef635a1045`.
- Extend the existing dependency-free `ReactorSim.Benchmarks` executable with
  an explicit `--profile` mode that constructs the existing solver before the
  timed boundary and calls the public `SpatialEigenSolve.TrySolve` path once
  per sample.
- Record nearest-rank p50/p95 timing and allocation observations, converged
  output identity, solver iteration count, node/edge/boundary descriptors, and
  flux-state byte descriptors.
- Preserve the P4-T08 manifest, deterministic Jacobi policy, double precision,
  convergence policy, synthetic inputs, and default P4 benchmark mode. No
  wall-clock value may enter simulation state.
- Record internal hotspot attribution as `NotMeasured` because no
  external profiler is installed; the task must not infer a loop-level hotspot
  from an end-to-end solve measurement.
- Record Android as `Deferred/NotAvailable`; a later device task owns T5/G9
  evidence.

## Explicit exclusions

This approval does not authorize a performance budget, latency/allocation
threshold, numerical tolerance, equation or unit change, precision change,
convergence change, solver refactor, Burst/Jobs backend, native dependency,
Unity frame-loop change, Android/iOS build or signing, thermal claim,
reference/golden change, production claim, shutdown/scram behavior, or release
decision.

## Validation and evidence

The task requires strict profile-manifest/path/hash validation, Release build
of the solution, two repeated profile observations with exact solver-output
repeatability, default P4-T08 compatibility, profile-manifest rejection,
final T3 Core/CLI/Golden regression suites, and one bounded independent code
review (high). Literature applicability is `NotApplicable`: this task consumes
the frozen synthetic P4-T08 solver contract and selects no physics authority.
