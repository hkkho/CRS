# P9-T02 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P9-T02
profiling task after the completed P9-T01 desktop observation.

## Task

Profile the frozen P9-T01 CLI gameplay command streams on the current desktop
host to characterize complete-command latency, current-thread allocation, and
input/output workload descriptors. The result is observation evidence only;
it must not select a performance budget, alter simulation results, or trigger
an optimization.

## Approved scope

- Add a versioned P9-T02 profiling-parameter manifest bound to the exact
  P9-T01 benchmark manifest SHA-256
  `63114186fd9768a94e44ff0368001952cda2750610965b48c94cc36e5efb5281`.
- Extend the existing dependency-free Phase 9 benchmark with an explicit
  `--profile` mode that uses the public `CliApplication.Run` boundary and the
  three closed P9-T01 command streams.
- Record per-case timing distributions, current-thread allocations, command
  counts, and UTF-8 input/output sizes. These are host-specific observations
  and workload descriptors, not acceptance thresholds.
- Preserve the approved command streams, deterministic seed/replay behavior,
  100 ms control tick, current double-precision runtime, and P9-T01 artifact
  bindings. Wall-clock measurement must never enter simulation state or replay
  identity.
- Explicitly document that the frozen P8 synthetic CLI cases do not invoke the
  Core spatial solver. Solver hotspot and spatial solve-latency evidence remain
  a separate Core benchmark boundary; this task must not infer them from CLI
  timing.
- Record Android as `Deferred/NotAvailable` because no representative device
  or `adb` command is available. A later device task owns T5/G9 evidence.

## Explicit exclusions

This approval does not authorize a performance budget, latency/allocation
threshold, numerical tolerance, precision change, Burst/Jobs backend, native
dependency, Unity frame-loop change, Android/iOS build or signing, thermal
claim, physics equation/constant/unit/reference/golden change, runtime
optimization, production balance claim, shutdown/scram behavior, or release
decision.

## Validation and evidence

The task requires manifest/static validation, Release build of the solution,
two repeated P9-T02 profile observations with exact deterministic preflight
output, focused profile-mode validation including profile-manifest rejection,
final T3 Core/CLI/Golden regression suites, and one bounded independent code
review (high). Literature applicability is `NotApplicable`: the task consumes
frozen synthetic gameplay contracts and changes no physics or external-reference
authority.
