# P8-T02-DRAFT - Phase 8 scenario and difficulty parameter draft

## Outcome

Status: COMPLETE.

This draft supplies an approval-ready, project-authored synthetic scenario and
difficulty parameter pack for the next Phase 8 task. It is explicitly
`Draft/NotApproved`; it does not activate a runtime consumer, select a physics
equation, establish a physical tolerance, or change the current `P8-T01`
handoff. The draft contains three difficulty profiles, five seeded scenarios,
operating-envelope/loss-recording rules, a proposed deterministic seed policy,
an owner-approval checklist, and a bounded real-time presentation policy. The
default proposal is `10x` simulation time at a `100 ms` wall control tick,
limited to one simulation second per presentation tick; a `1x` audit mode is
also retained.

Effectiveness: SUCCESS.

The current authority stop remains intact: `P8-T02` is not activated until an
owner approves the parameter authority and a separate bounded implementation
task is selected.

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high for bounded documentation/data work
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: current Codex task; no root telemetry exposed
- Attempts: 1; elapsed time: `Unavailable`
- Artifact/checkpoint status: produced; draft JSON and manifest created
- Review disposition: not applicable for this draft-only, non-runtime artifact
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable; owner approval is intentionally pending

## Files created or changed

- `data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.json` - approval-ready synthetic parameter draft.
- `data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.manifest.json` - draft artifact byte/hash manifest.
- `docs/tasks/P8-T02-DRAFT.md` - this boundary and handoff report.

Pre-existing authorities and prerequisite reports were inspected but not
changed: `AGENTS.md`, the complete `docs/Implementation_plan.md`,
`docs/PROJECT_SCOPE.md`, `docs/tasks/P8-T01.md`, the existing P6-T07 synthetic
scenario package, and the P8-T01 CLI implementation/tests.

## Assumptions and design choices

- All values in the draft are gameplay parameters or normalized gameplay
  proxies; they are not physical reactor constants, equations, reference
  outputs, golden values, or numerical tolerances.
- The existing `synthetic-p3-t01` fixture is named as the proposed base fixture
  only to make the future consumer explicit. This draft does not authorize a
  runtime crosswalk or alter that fixture.
- The proposed seed derivation is written as a reviewable candidate and remains
  `Proposed/RequiresApproval`; no runtime PRNG or replay contract was changed.
- The proposed time model separates wall-clock presentation from explicit
  simulation time: `1x` real-time audit, `10x` default play, a `100 ms` wall
  control tick, at most `1.0 s` of simulation presentation advance per tick,
  and 100/200 ms acknowledgement/commit targets. It does not replace the
  existing solver timestep or cadence policy.
- At the default rate, the practice/standard/challenge horizons are about
  60/90/120 wall seconds and the 20/15/10 simulation-second decision intervals
  are about 2.0/1.5/1.0 wall seconds. The draft intentionally proposes no
  faster mode than 10x.
- Envelope bounds are inclusive. Values outside the bounds produce a gameplay
  loss record only. No shutdown, scram, trip, safety-system, accident, or
  hidden-reactivity behavior is introduced.
- Scoring, persistence/replay, runtime command consumption, long-run soak, and
  Unity presentation remain separate follow-on packages.
- `P1-T08` is `NotApplicable`: the draft selects no physics, nuclear data,
  reference case, normalization, or golden value.

## Validation commands and results

### JSON/schema shape check - T0/T1

```text
$path='data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.json'; $json=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json; $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(); Write-Output ('P8_T02_DRAFT_JSON_PASS status=' + $json.status + ' profiles=' + $json.difficulty_profiles.Count + ' scenarios=' + $json.scenarios.Count + ' sha256=' + $hash + ' bytes=' + (Get-Item -LiteralPath $path).Length)
```

Result: exit `0`; JSON parsed; status `Draft/NotApproved`; `3` profiles;
`5` scenarios; `3` playback modes; default factor `10`; `18187` bytes;
SHA-256
`3efc22d22b22d47ede62fe56bdb31a33dbb43636ef359924713dd978c397305d`.

### Manifest consistency check - T0

```text
$artifact=Get-Content -LiteralPath 'data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.json' -Raw | ConvertFrom-Json; $manifest=Get-Content -LiteralPath 'data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.manifest.json' -Raw | ConvertFrom-Json; $actual=(Get-FileHash -LiteralPath $manifest.artifact.path -Algorithm SHA256).Hash.ToLowerInvariant(); if($manifest.status -ne 'Draft/NotApproved' -or $manifest.owner_decision -ne 'PENDING' -or $actual -ne $manifest.artifact.sha256 -or $artifact.status -ne $manifest.status -or $artifact.difficulty_profiles.Count -ne $manifest.difficulty_profile_count -or $artifact.scenarios.Count -ne $manifest.scenario_count -or $artifact.time_model.modes.Count -ne $manifest.playback_mode_count -or $artifact.time_model.default_acceleration_factor_simulation_seconds_per_wall_second -ne $manifest.default_acceleration_factor -or $artifact.time_model.maximum_acceleration_factor_simulation_seconds_per_wall_second -ne $manifest.maximum_acceleration_factor -or $artifact.time_model.wall_control_tick_ms -ne $manifest.wall_control_tick_ms){ throw 'P8-T02 draft manifest mismatch' }; Write-Output ('P8_T02_DRAFT_MANIFEST_PASS sha256=' + $actual + ' profiles=' + $artifact.difficulty_profiles.Count + ' scenarios=' + $artifact.scenarios.Count + ' modes=' + $artifact.time_model.modes.Count + ' factor=' + $artifact.time_model.default_acceleration_factor_simulation_seconds_per_wall_second)
```

Result: exit `0`; manifest status, pending owner decision, artifact hash,
profile/mode counts, default/max acceleration factors, and wall tick matched.

### Real-time responsiveness invariants - T1

```text
$artifact=Get-Content -LiteralPath 'data/scenarios/p8-t02-scenario-difficulty-parameters-draft-v1.json' -Raw | ConvertFrom-Json; $time=$artifact.time_model; $tickSeconds=$time.wall_control_tick_ms / 1000.0; if($time.wall_clock_must_not_drive_physics -ne $true -or $time.default_acceleration_factor_simulation_seconds_per_wall_second -ne 10.0 -or $time.maximum_acceleration_factor_simulation_seconds_per_wall_second -ne 10.0 -or $time.maximum_presentation_advance_per_wall_tick_s -ne 1.0 -or $time.default_acceleration_factor_simulation_seconds_per_wall_second * $tickSeconds -gt $time.maximum_presentation_advance_per_wall_tick_s -or $time.action_acknowledgement_target_wall_ms -gt $time.wall_control_tick_ms -or $time.action_commit_target_wall_ms -gt (2 * $time.wall_control_tick_ms)){ throw 'P8-T02 time model invariant mismatch' }; foreach($profile in $artifact.difficulty_profiles){if([math]::Abs($profile.estimated_wall_duration_s_at_default_playback - ($profile.scenario_horizon_s / 10.0)) -gt 0.000001 -or [math]::Abs($profile.estimated_decision_interval_wall_s_at_default_playback - ($profile.decision_interval_s / 10.0)) -gt 0.000001){throw ('profile timing estimate mismatch ' + $profile.difficulty_id)}}; Write-Output ('P8_T02_TIME_MODEL_PASS tick_ms=' + $time.wall_control_tick_ms + ' max_sim_s_per_tick=' + $time.maximum_presentation_advance_per_wall_tick_s + ' factor=' + $time.default_acceleration_factor_simulation_seconds_per_wall_second + ' action_ack_ms=' + $time.action_acknowledgement_target_wall_ms + ' action_commit_ms=' + $time.action_commit_target_wall_ms)
```

Result: exit `0`; 100 ms wall tick, 1.0 simulation-second presentation
slice cap, 10x default/cap, and 100/200 ms action targets passed; all profile
wall-duration and decision-interval projections matched the 10x factor.

### Runtime/build validation

Not run. The draft is not copied into a test project, not consumed by the CLI,
and intentionally has no runtime implementation. A future approved P8-T02
consumer must trigger focused tests, T3 replay/determinism checks, and the
required independent code review (high).

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | `Unavailable` | No per-task telemetry exposed |
| Cached input tokens | `Unavailable` | No per-task telemetry exposed |
| Cache-write input tokens | `Unavailable` | No per-task telemetry exposed |
| Output tokens | `Unavailable` | No per-task telemetry exposed |
| Reasoning output tokens | `Unavailable` | No per-task telemetry exposed |
| Total tokens | `Unavailable` | No per-task telemetry exposed |
| Estimated cost | `Unavailable` | No allocable request-level pricing telemetry exposed |
| Goal-service total | `Unavailable` | No active goal aggregate exposed |

Cost formula/basis: unavailable; no request-level billing telemetry was
exposed, so no allocation is invented.

## Numerical differences

Not applicable; no physics or numerical state transition changed. The draft's
numbers are unapproved gameplay parameters and are not compared with external
or golden numerical results.

## Deferred validation

- Owner approval and promotion to `Approved/ApprovedParameterAuthority` -
  deferred to the owner decision and a separately bounded P8-T02 activation task.
- Canonical seed/replay behavior, envelope enforcement, loss recording, and
  event ordering - deferred until a runtime consumer exists.
- Wall-clock pacing, action acknowledgement/commit latency, pause behavior,
  acceleration changes, and backpressure under long solves - deferred until a
  runtime consumer and representative performance validation exist.
- T3, G8, save/load/replay, scoring, soak, Unity/T4, and mobile/T5 evidence -
  deferred to their authorized follow-on tasks.

## Blockers, risks, and follow-up

- Blockers: owner approval is pending; the current Phase 8 authority stop is
  intentionally preserved.
- Risk triggers: none fired for runtime behavior; this is a data/documentation
  draft only.
- Risks accepted or deferred: proposed seed derivation and all gameplay bands
  may change during approval; no consumer may depend on this draft.
- Follow-up work: owner review, then activate a separately bounded `P8-T02`
  implementation task if approved.

## Next eligible task

`P8-T02` - implement the approved scenario/difficulty consumer only after the
owner decision and parameter authority promotion are recorded. Until then,
there is no eligible Phase 8 runtime task.

Source: [`AGENTS.md`](../../AGENTS.md), complete
[`docs/Implementation_plan.md`](../Implementation_plan.md),
[`docs/PROJECT_SCOPE.md`](../PROJECT_SCOPE.md), and
[`docs/tasks/P8-T01.md`](P8-T01.md).
