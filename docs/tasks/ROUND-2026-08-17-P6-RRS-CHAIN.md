# Round 2026-08-17 — Phase 6 RRS task-definition chain

## Authorization and boundary

Status: ACTIVATED TASK-DEFINITION / ROUTING RECORD. This file instantiates the
Phase 6 work packages from the implementation plan; it is not a completed task
report and does not authorize a gate PASS.

G5 is `PASS` for the approved `ReducedModel` / engine-neutral Core scope. The
owner-authorized one-task-at-a-time roadmap may now proceed to Phase 6/G6. The
Phase 6 implementation boundary is the frozen P2-T04
[kinetics, xenon, RRS, and feedback specification](../spec/kinetics-xenon-rrs-feedback-v1.md)
and the Phase 6 section of the
[implementation plan](../Implementation_plan.md).

The chain remains bounded:

- no shutdown, scram, trip, accident progression, safety-system response,
  thermal-hydraulic/CFD behavior, operator training, or Unity behavior;
- no production RRS gains, influence maps, controller defaults, tolerances, or
  golden values are invented by task definition;
- DRAGON5 and DONJON5 remain offline reference tools and are not runtime
  dependencies;
- missing map/data authority, unit/sign ambiguity, nondeterminism,
  nonconvergence, invalid state, or a required tolerance/golden decision stops
  the affected task and is recorded rather than approximated.

## Activated task chain

| Task ID | Bounded outcome | Depends on | Required evidence / handoff |
|---|---|---|---|
| `P6-T01` | Define and implement the engine-neutral state contract for exactly 14 independently tracked logical liquid-zone compartments grouped by exactly 6 declared physical assemblies. Bind complete mapping identity, fill state, mode, queue owner, versions, digests, and disabled-zero behavior. | G5 PASS; frozen P2-T04 zone schema; P2-T05 identity/queue/serialization contracts. | T0/T1 contract tests, deterministic canonical bytes, fail-closed mapping validation, applicable P1-T08 coverage or `NotApplicable` rationale. Next: `P6-T02`. |
| `P6-T02` | Admit and consume validated liquid-zone influence maps and implement the bounded state-dependent overlay and rate-limited zone motion contract. Do not invent a logical-to-physical map or overlay weights. | `P6-T01`; owner-approved/versioned zone map package or an explicit blocked disposition. | T1/T3 map identity, units/sign, grouping, disabled-zero, rate/delay, queue, rollback, and deterministic overlay checks. Next: `P6-T03`. |
| `P6-T03` | Define and implement the approved adjuster-bank state, grouping, insertion-fraction bounds, motion modes, delayed queue, and state-dependent influence-map binding. | `P6-T02`; owner-approved/versioned adjuster set and map package or an explicit blocked disposition. | T1/T3 exact bank identity, bounds, rate/delay, causal motion, queue ownership, sign certificate, rollback, and deterministic overlay checks. Next: `P6-T04`. |
| `P6-T04` | Implement bulk moderator poison state with separate rate-limited add and slow withdraw/cleanup actions, exact mass accounting, concentration derivation, map binding, and prescribed setup limits. | `P6-T03`; owner-approved/versioned poison map and moderator-volume data or an explicit blocked disposition. | T1/T3 mass/concentration units, nonnegative bounds, add/withdraw rate behavior, no hidden cleanup law, rollback, and deterministic overlay checks. Next: `P6-T05`. |
| `P6-T05` | Implement the limited documented regulating controller for total power and tilt, including manual commands where approved, with explicit sign ownership and no direct-reactivity duplicate path. | `P6-T01`–`P6-T04`; approved controller gains/setpoints and RRS influence maps, or an explicit blocked disposition. | T1/T3 controller sign, setpoint/error units, cadence, integral state, spatial coupling, deterministic commands, and fail-closed invalid-state checks. Next: `P6-T06`. |
| `P6-T06` | Implement actuator limits, rates, delays, saturation diagnostics, queue allocation/identity, same-time ordering, causal motion, and atomic rollback for RRS/zone/adjuster actions. | `P6-T01`–`P6-T05`; frozen P2-T04 queue and event-rank contracts. | T1/T3 command/event canonical bytes, saturation observables, overflow/reuse rejection, motion-before-consume behavior, rollback, replay determinism, and refuelling interaction handoff. Next: `P6-T07`. |
| `P6-T07` | Add the bounded scenarios: centered perturbation, regional tilt, zone saturation, adjuster insertion/withdrawal, and poison recovery. Keep scenarios as evidence fixtures; do not treat synthetic values as production authority. | `P6-T01`–`P6-T06`; approved maps/controller inputs; G5 refuelling boundary. | T1/T3 scenario/replay evidence, timestep sensitivity, saturation/stability diagnostics, refuelling interaction, and no-safety-system boundary audit. Next: `G6`. |
| `G6` | Review Phase 6 RRS implementation and approved comparisons; disposition signs, region mapping, rates, saturation, controller stability, timestep sensitivity, and refuelling interaction. | `P6-T01`–`P6-T07`; approved RRS comparison package. | Gate report, T3 suite, independent code review (high), and no unresolved critical blocker. |

## Execution discipline

Execute exactly one task ID at a time. Before each task, read `AGENTS.md`, the
complete implementation plan, this chain row, every named specification, the
current `PROJECT_SCOPE.md` lookup, and prerequisite task reports. Each task
must write `docs/tasks/<TASK-ID>.md`, record requested/actual model and review
telemetry, list applicable P1-T08 literature rows or a concrete `NotApplicable`
reason, run the proportionate validation level, and stop on a critical blocker.

No Phase 6 task has been executed by the Phase 5 goal. This chain only activates
the next bounded task IDs and preserves the Phase 6/G6 handoff for the next
goal.

Source: [`AGENTS.md`](../../AGENTS.md),
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/spec/kinetics-xenon-rrs-feedback-v1.md`](../spec/kinetics-xenon-rrs-feedback-v1.md),
and [`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md).
