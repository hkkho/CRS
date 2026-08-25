# P10-T01 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P10-T01
Unity presentation/input adapter slice after P9-T04.

## Task

Add the first thin Unity presentation seam over the completed engine-neutral
Phase 8 CLI/Core contracts. The seam must forward player input and explicit
wall-time requests to an injected runtime port, publish immutable state for UI
binding, and remain free of simulation transitions and Unity frame-clock
advancement.

## Approved scope

- Add a versioned Unity command vocabulary for wall-time advance, power/tilt
  targets, playback mode, pause, and resume.
- Add an immutable presentation snapshot containing the already-approved Phase
  8 gameplay/time observables needed by later UI panels.
- Add a Unity `MonoBehaviour` bridge with explicit bind, snapshot refresh,
  command dispatch, duplicate-sequence rejection, and command-result events.
- Add focused EditMode and PlayMode tests using a test-only runtime-port stub;
  retain the established G2 test assemblies and bootstrap smoke.
- Document that the approved 100 ms control tick, 10x default acceleration,
  and one-simulation-second presentation cap remain owned by the Phase 8
  parameter/runtime port.

## Explicit exclusions

This approval does not authorize new physics, equations, units, tolerances,
golden/reference data, parameter-pack loading, a second simulation runtime,
save/load/replay implementation, Unity `Update`-driven simulation, frame-time
sampling, performance optimization, native dependencies, Android/iOS builds or
signing, or production/mobile claims. The runtime port remains the sole owner
of Core/CLI state transitions.

## Validation and evidence

The adapter/command/snapshot contract triggers T3 and T4. Validation requires
the Core/CLI/Golden Release regression, Unity import/compile, EditMode,
PlayMode, and StandaloneWindows64 desktop-player smoke, plus one bounded
independent code review (high). Literature applicability is `NotApplicable`:
this task selects no physics equation, constant, unit, reference case,
tolerance, or golden authority.
