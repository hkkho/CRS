# P10-T04 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P10-T04
controls/command-presentation slice after the completed and reviewed P10-T03
Dashboard projection.

## Task

Add the first touch-oriented Controls presentation over the existing P10-T01
`Phase8UnityRuntimeAdapter`. The view may issue only commands already defined
by that adapter, reports the returned result, and never owns simulation state,
command sequencing, pacing, or runtime validation.

## Approved scope

- Add a Unity Controls view with explicit `Bind`/`Unbind` lifecycle over a
  bound `Phase8UnityRuntimeAdapter`.
- Present explicit wall-duration buttons for the approved 100 ms control tick
  and a 1,000 ms interactive advance, plus Pause and Resume buttons. These are
  UI requests only; the Phase 8 runtime remains the pacing authority.
- Present only the already-approved `audit-real-time-1x` and
  `play-accelerated-10x` playback mode commands.
- Present invariant-culture numeric entry and Apply buttons for the existing
  normalized power target and nonnegative absolute-tilt target commands. The
  runtime port remains the authority for domain acceptance/rejection.
- Subscribe to the existing snapshot and command-result events, show current
  observed state/pacing, and show accepted or rejected command diagnostics.
- Ensure repeated binding does not duplicate button listeners or event
  subscriptions. Each UI activation calls one adapter convenience method;
  adapter-generated monotonic sequences remain the duplicate-sequence
  authority.
- Add the Bootstrap scene component and focused EditMode/PlayMode tests.
- Instantiate the graphical Controls surface in the Bootstrap scene even when
  the host has not bound a runtime yet. Show the unavailable/waiting state and
  keep its inputs and actions disabled until `Bind` succeeds.
- Add a normal offline Windows demo-build path for visual smoke inspection.
  It must not pass Unity Test Runner arguments (`-runTests`, `-testPlatform`, or
  `-testResults`); Test Runner players remain headless validation artifacts.
- Add a launcher that removes only confirmed Candu Test Runner players from
  temporary `candu-*` artifact roots before starting the plain demo. It may not
  edit Windows Firewall or other host security settings.

There are no irreversible/refuelling controls in the P10-T01 vocabulary, so
an irreversible-action confirmation/cancel flow is explicitly not applicable
to this task. A later task that adds such a command must introduce its own
confirmation authority before exposing it.

## Explicit exclusions

This approval does not authorize Core/CLI changes, new adapter commands,
runtime-port implementation, new snapshot fields or schemas, parameter-pack
loading, refuelling, save/load/replay, clocks, frame-time sampling, physics,
equations, units, tolerances, golden/reference data, altered acceleration or
control-tick policy, performance optimization, accessibility-policy selection,
Android/iOS builds or signing, or mobile/production claims. The view must not
use `Update`, Unity time APIs, or manually construct command sequences.

## Validation and evidence

Because this task consumes the approved public adapter command/snapshot
boundary, it requires T1 static checks, T3 Core/CLI/Golden Release regression,
T4 Unity import/compile, EditMode, PlayMode, and StandaloneWindows64
desktop-player smoke, plus one visible offline-demo graphical smoke and one
bounded same-context independent code review (high). Literature applicability
is `NotApplicable`.
