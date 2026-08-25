# P8-T06 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization approves this bounded P8-T06
implementation.

## Task

Implement long-run soak cases and invariant monitoring for the Phase 8
synthetic CLI runtime (`P8-T06`). The soak is a regression harness and gate
evidence producer only; it must not become player-facing automation or a
physical/control authority.

## Approved scope

- Consume only the exact approved P8-T05 policy artifact SHA-256
  `d0e6dec7199751ca0f5d5418892fe0210831e46153ff445ec13e4e84ddf49d39`, which
  is already bound to the approved P8-T02 scenario SHA-256
  `80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2` and
  P8-T03 scoring SHA-256
  `4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c`.
- Run each of the four approved P8-T05 policies for 16 fresh deterministic
  cycles. Preserve the P8-T05 policy command granularity and exact scheduled
  wall-time offsets; monitor the existing runtime's explicit 100 ms control-
  tick/state-segment decomposition inside each advance call so scoring,
  turn-summary count, and replay-digest semantics are unchanged.
- Monitor only existing public synthetic runtime observables: finite values,
  nonnegative/nondecreasing clocks, horizon bounds, control-step progress,
  action/event/loss ordering, pending-action capacity, operating-envelope
  state, score bounds/quality, turn-summary consistency, terminal outcome,
  and exact P8-T05 replay/expected-observable identity.
- Run the complete soak plan twice and require byte-for-byte equality of the
  deterministic aggregate digest and all per-cycle observables. Report the
  first invariant path and cycle on any failure; do not continue after an
  invalid state.
- Keep the runner internal to the CLI/test boundary. Do not add a player-facing
  command, Unity adapter, save schema, network path, random source, or runtime
  dependency.

## Explicit exclusions

This approval does not authorize new physics equations, constants, units,
normalization, tolerances, reference/golden-data changes, external or
production balance claims, Unity behavior, player-facing bot automation,
autonomous safety actions, shutdown/scram behavior, full thermal hydraulics,
or G8 closure by administrative assertion. P8-T06 supplies soak evidence for
the separate G8 review; it does not itself promote the synthetic runtime to a
production or physical authority.

## Validation and evidence

The task requires the focused soak/invariant suite, exact two-pass aggregate
repeatability, the final T3 Core/CLI/Golden suites, and one bounded independent
code review (high). Literature applicability is `NotApplicable`: the task
consumes frozen synthetic gameplay contracts and selects no physics or
external-reference authority.
