# P9-T01 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P9-T01
performance-observation task after the `G8` PASS.

## Task

Establish a versioned, deterministic Phase 9 benchmark scenario set and record
machine-specific desktop observations for the completed Phase 8 CLI gameplay
loop. The benchmark is an observation harness only; it must not select a
performance target, alter simulation results, optimize code, or imply mobile,
thermal, release, physical, or production readiness.

## Approved scope

- Add a project-owned manifest containing a closed set of P8 gameplay command
  streams: steady survival, controlled response, and operating-envelope loss.
- Bind the manifest to the exact approved P8-T02 scenario SHA-256
  `80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2`, P8-T03
  scoring SHA-256
  `4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c`, and
  P8-T05 policy SHA-256
  `d0e6dec7199751ca0f5d5418892fe0210831e46153ff445ec13e4e84ddf49d39`.
- Execute the streams in-process through the existing public CLI boundary,
  verify exact repeated output/exit determinism, and observe elapsed time and
  current-thread allocation counts on this desktop host.
- Preserve the explicit command stream, deterministic seed/replay behavior,
  approved 100 ms control tick, and current double-precision runtime. No
  wall-clock value may enter simulation state or replay identity.
- Record Android baseline status as `Deferred/NotAvailable` because this
  environment has no `adb` command or attached representative Android device.
  A later device task must provide platform/toolchain/device evidence; this
  task must not synthesize it.

## Explicit exclusions

This approval does not authorize a performance budget, latency/allocation
threshold, numerical tolerance, precision change, Burst/Jobs backend, native
dependency, Unity frame-loop change, Android/iOS build or signing, thermal
claim, physics equation/constant/unit/tolerance/reference/golden change,
production balance claim, shutdown/scram behavior, or release decision.

## Validation and evidence

The task requires manifest/static validation, Release build of the benchmark
and solution, repeated desktop benchmark observations with exact semantic
output equality, focused P9-T01 validation, final T3 Core/CLI/Golden suites,
and one bounded independent code review (high). Literature applicability is
`NotApplicable`: this task consumes frozen synthetic gameplay contracts and
selects no physics or external-reference authority.
