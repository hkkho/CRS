# P8-T04 Owner Approval

## Approval

The user authorized autonomous execution of the next eligible repository task
under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`, with routine implementation choices delegated to the
agent. That authorization approves this bounded P8-T04 implementation.

## Task

Implement the Phase 8 synthetic gameplay save/load and deterministic
command-log replay slice (`P8-T04`).

## Approved scope

- Add a versioned, canonical CLI replay archive for the existing Phase 8
  scenario runtime.
- Persist only run identity and deterministic command intent: scenario ID,
  difficulty ID, seed, initial playback mode, the exact approved P8-T02
  scenario-pack SHA-256, the exact approved P8-T03 scoring-pack SHA-256, the
  ordered commands, and an expected final runtime digest.
- Support these command kinds only: `advance_wall`, `set_power_target`,
  `set_tilt_target`, `set_playback_mode`, `pause`, and `resume`.
- Add `save <path>`, `load <path>`, and `replay <path>` to the existing CLI.
  `load` must replay into a fresh candidate and replace the active session only
  after every validation and final-digest check succeeds. `replay` verifies a
  candidate without replacing the active session.
- Enforce a strict schema, canonical property order, duplicate/unknown/
  reordered/trailing/comment rejection, finite and range-checked values, a
  bounded command count, and exact approved data-pack hash checks.
- Keep the implementation deterministic, engine-neutral, and limited to the
  existing synthetic Phase 8 runtime and CLI session boundary.

## Explicit exclusions

This approval does not authorize new physics equations, constants, units,
normalization, convergence rules, tolerances, reference or golden-data
changes, production data, Unity behavior, safety/shutdown behavior, bots,
long-run soak testing, or changes to the existing P3-T04 generic state archive
contracts. P3-T04 is prerequisite context and remains the authority for its
existing state serialization/replay contracts.

## Validation and evidence

The task requires focused malformed-archive and round-trip/replay tests, a
Release build, the applicable T3 Core/CLI/Golden suites, and one bounded
independent code review (high). Physics literature applicability is
`NotApplicable`: this task consumes frozen runtime behavior and does not
select, change, or validate physics.
