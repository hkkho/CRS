# P10-T05 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P10-T05
Timeline/event-log presentation slice after the completed and reviewed
P10-T04 Controls view.

## Task

Add a graphical Timeline page over the existing P10-T01
`Phase8UnityRuntimeAdapter`. The view records only immutable snapshots and
command-result notifications that it observes while bound, renders compact
power/tilt/control-margin trends and a bounded event log, and remains a
presentation-only consumer. It must not create a second runtime, infer
missing channel data, or advance simulation time.

## Approved scope

- Add a Unity Timeline view with explicit `Bind`/`Unbind` lifecycle over a
  bound `Phase8UnityRuntimeAdapter`.
- Subscribe to the existing `SnapshotChanged` and `CommandCompleted` events;
  render the adapter snapshot immediately on bind and record accepted or
  rejected command diagnostics in a bounded event log.
- Render an actual UGUI trend surface for the observed normalized power,
  absolute tilt, and control-margin fractions, with invariant presentation
  formatting and a visible sample-cap/status summary.
- Keep a deterministic, bounded in-memory presentation history. Eviction is
  a UI history policy only and never mutates or serializes runtime state.
- Instantiate the graphical Timeline surface in Bootstrap even when no host
  has bound a runtime. Show an explicit waiting state and an empty-history
  diagnostic until binding succeeds.
- Add focused EditMode/PlayMode coverage for binding, event subscription,
  bounded history, command-result rendering, and Bootstrap availability.
- Extend the existing test-only `BootstrapAdapter` with an opt-in normal
  StandaloneWindows64 player marker smoke. When explicit marker arguments are
  supplied, it may verify the built shell/Timeline surface, write a marker,
  and quit; this is test evidence only and is not a player-facing command,
  network behavior, runtime contract, or simulation state transition.
- Preserve the approved UGUI package, safe-area shell, navigation, Controls,
  Dashboard, explicit wall-time pacing, and adapter sequencing behavior.

## Explicit exclusions

This approval does not authorize Core/CLI changes, new adapter commands,
snapshot fields or schemas, parameter-pack loading, runtime-port
implementation, channel/bundle data projection, map or RRS behavior,
refuelling confirmation, save/load/replay, clocks, frame-time sampling,
physics, equations, units, tolerances, golden/reference data, performance
optimization, accessibility-policy selection, Android/iOS builds or signing,
or mobile/production claims. The view must not use `Update`, Unity time APIs,
or manually construct command sequences. Trend values are already-owned
snapshot observables; display clamping is presentation-only and must be
documented as such.

## Validation and evidence

Because this task consumes the approved adapter snapshot/command-result
boundary, it requires T1 static checks, T3 Core/CLI/Golden Release
regression, T4 Unity import/compile, EditMode, PlayMode, and
StandaloneWindows64 desktop-player smoke, plus one visible offline-demo
graphical smoke. The normal-player marker path may supplement the desktop
smoke without Test Runner arguments; it does not waive the final clean visual
click-through. Also require one bounded same-context independent code review
(high).
Literature applicability is `NotApplicable`.
