# P9-T04 Owner Approval

## Approval

The user authorized autonomous selection and execution of the next eligible
repository task under `AGENTS.md`, `docs/Implementation_plan.md`, and
`docs/PROJECT_SCOPE.md`. That authorization selects this bounded P9-T04
performance-target and optimization decision record after P9-T02 and P9-T03.

## Task

Review the existing P9-T01 desktop gameplay observations, P9-T02 CLI profile,
and P9-T03 Core solver profile, then record whether the evidence authorizes a
performance target or runtime optimization. The decision must preserve the
observations as machine-specific evidence and must not promote them into a
mobile, thermal, release, physics, golden, or production authority.

## Approved scope

- Add a versioned, report-bound decision artifact that binds the exact P9-T01,
  P9-T02, and P9-T03 manifests by SHA-256.
- Record the reviewed observation ranges, their measurement boundaries, and
  the evidence limitations.
- Decide whether a numeric performance target, plain-C# optimization, precision
  change, Burst/Jobs backend, or mobile claim is authorized. The default
  decision is `HOLD` unless the frozen evidence and authorities support a
  change; no target may be invented from host timing.
- Preserve all simulation equations, units, signs, convergence/tolerance
  policies, double precision, command ordering, replay identity, and runtime
  architecture.
- Carry the Android/device and external-profiler gaps to the next named task
  rather than fabricating completion.

## Explicit exclusions

This approval does not authorize code optimization, a numeric performance or
allocation budget, a precision change, a Burst/Jobs/native backend, a Unity
frame-loop change, an Android/iOS build or signing operation, thermal or release
claims, physics/reference/golden changes, shutdown/scram behavior, or a target
derived from a single desktop host.

## Validation and evidence

This is a documentation/data decision task. It requires manifest/static
validation, exact hash checks against the repository artifacts, consistency
checks against P9-T02/P9-T03 reports, and one bounded independent code review
(high). Existing P9-T02/P9-T03 T3 and profile evidence is cited; no runtime
code change is made, so no new T3 run is required by this task itself.
Literature applicability is `NotApplicable`: no physics or external-reference
authority is selected.
