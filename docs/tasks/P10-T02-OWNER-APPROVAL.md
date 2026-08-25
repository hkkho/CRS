# P10-T02 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P10-T02
visual shell/navigation slice after the completed and reviewed P10-T01 seam.

## Task

Add a landscape-ready, safe-area-aware Unity shell with touch-sized
navigation between placeholder presentation pages. The shell must be usable
without a bound simulation runtime and must leave all Core/CLI state
transitions behind the P10-T01 runtime-port boundary.

## Approved scope

- Add a Unity Canvas/CanvasScaler shell using a landscape reference resolution.
- Apply `Screen.safeArea` to a dedicated content root through a testable layout
  component.
- Add touch-sized navigation buttons and deterministic page selection for the
  initial Dashboard, Core Map, Timeline, and Controls placeholders.
- Add focused EditMode and PlayMode coverage, including scene availability,
  safe-area anchor mapping, default page, and navigation transitions.
- Add only the Bootstrap scene component and Unity metadata needed to show the
  shell in the first client, including the editor-pinned built-in `com.unity.ugui`
  dependency required by the approved Canvas/Button surface.

## Explicit exclusions

This approval does not authorize simulation state, Core/CLI changes,
parameter-pack loading, snapshot mapping, clocks, frame-time advancement,
save/load/replay, physics, performance optimization, accessibility policy
selection beyond the visual touch-size baseline, Android/iOS builds or
signing, or mobile-device claims. The shell may display placeholders only;
later tasks bind approved runtime snapshots and commands.

## Validation and evidence

This is a pure visual Unity task. It requires T1 static/focused checks and T4
Unity import/compile, EditMode, PlayMode, and desktop-player smoke. T3 is not
required unless the implementation crosses the P10-T01 adapter, command,
snapshot, serialization, clock, or data-loading boundary. Literature
applicability is `NotApplicable`: no physics or external-reference authority
is selected.
