# P10-T03 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P10-T03
snapshot-presentation slice after the completed and reviewed P10-T02 shell.

## Task

Bind the first Dashboard presentation page to the immutable
`Phase8UnityPresentationSnapshotV1` published by the approved P10-T01
`Phase8UnityRuntimeAdapter`. The view is read-only: it formats already-owned
runtime observables into a stable dashboard and never advances, mutates, or
duplicates simulation state.

## Approved scope

- Add a Unity dashboard view component with explicit `Bind`/`Unbind` methods
  for a `Phase8UnityRuntimeAdapter`.
- Render the existing snapshot's scenario, playback, acceleration, explicit
  wall-tick, simulation/wall time, power, tilt, control margin, device
  availability, refuelling count, pending actions, score, and outcome fields
  as presentation text.
- Subscribe/unsubscribe to `SnapshotChanged` and render the adapter's current
  immutable snapshot immediately on bind.
- Require the host lifecycle to call Dashboard `Unbind()` before the P10-T01
  adapter `Unbind()`; this preserves the existing adapter contract without
  adding a new lifecycle event in this presentation task.
- Add a testable Dashboard page lookup to the existing visual shell, a
  Bootstrap scene adapter/view component, and focused EditMode/PlayMode tests.
- Preserve the current built-in UGUI package and all P10-T02 safe-area,
  navigation, and placeholder-page behavior.

## Explicit exclusions

This approval does not authorize Core/CLI changes, new snapshot fields or
schemas, parameter-pack loading, runtime-port implementation, command
dispatch, save/load/replay, clocks, frame-time sampling, physics, equations,
units, tolerances, golden/reference data, performance optimization,
accessibility policy selection, Android/iOS builds or signing, or mobile and
production claims. The adapter remains the sole owner of runtime state and
command sequencing; this task only renders its immutable projection.

## Validation and evidence

Because this task consumes the approved public snapshot/adapter boundary, it
requires T1 static checks, T3 Core/CLI/Golden Release regression, T4 Unity
import/compile, EditMode, PlayMode, and StandaloneWindows64 desktop-player
smoke, plus one bounded same-context independent code review (high). No new
physics or external reference authority is selected; literature applicability
is `NotApplicable`.
