# XSEC-03-R1 - official JEFF lattice v2 reproduction

## Outcome

Status: BLOCKED

Effectiveness: BLOCKED

No valid v2 DRAGON result or semantic export was accepted. The official JEFF
TCWUX11 procedure ran through the initial stages with the pinned input, but the
approved removal of its two SPH calls altered the source's later two-group state
and caused its unchanged third source assertion to fail. Retaining SPH while
extracting only pre-SPH source data requires a separate exact-stage authority;
the current v2 deck forbids it.

Applicable P1-T08 digest rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; request-level execution telemetry is unavailable.
- Execution receipt or telemetry source: current Codex task; no provider receipt was exposed.
- Attempts: one independent fresh v2 input root reached the source assertion stage; no complete valid source run occurred.
- Artifact/checkpoint status: `NO_ARTIFACT` for the required semantic export; this report and task request were produced.
- Review disposition: BLOCKED. The source-stage change needs a new bounded authority/review task.
- Review evidence verification: not applicable.
- Reviewer reuse/fresh-review rationale: no reviewer was dispatched because no new deck decision was made.

## Files created or changed

- `docs/tasks/XSEC-03-R1-REQUEST.md` - v2 reproduction boundary.
- `docs/tasks/XSEC-03-R1.md` - this blocked report.

All JEFF/WLUP/source-deck/listing/LCM/raw output artifacts, including the failed
run, remain external. No runtime pack or raw result was retained in the repository.

## Observed critical conflict

The external run used the hash-matched TCWUX11, TCWU05Lib, assertS, and WLUP
binary source route. It retained all four source assertion lexemes but omitted
the two v2-authorized SPH calls. The first two stages proceeded; at the source
two-group `REF-CASE0001` transition, the unchanged source assertion required:

```text
REFERENCE=1.118481
CALCULATED=1.115139
```

`assertS` aborts at relative difference `>= 1.0E-4`, so the source stopped
before a valid completion. This proves the SPH calls affect the subsequent
source calculation even though the desired export stage is pre-SPH. Deleting
SPH cannot be reclassified as non-mutating instrumentation.

## Validation commands and results

### T0 - external input identity

```text
Get-FileHash -Algorithm SHA256 TCWU05Lib.c2m WLUP172
```

Result: PASS before execution. The procedure's canonical-LF hash matched v2;
the JEFF library procedure hash was
`896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040` and the
converted WLUP binary hash was
`c7fc05d6b7cb2085d999c568854aeaacc9c79477dec4c702e6b7d3fe15ba5c5e`.

### T1 - fresh offline source execution

```text
docker run --rm --network none --read-only --tmpfs /tmp --tmpfs /run \
  --entrypoint sh -v <external-run-root>:/work -w /work \
  docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 \
  -c '/dragon/5.1/Dragon/bin/Linux_x86_64/Dragon < xsec03r1-tcwux11.x2m \
  > dragon.result 2> dragon.stderr'
```

Result: FAIL by source assertion, exit 1. The source reached its third
assertion but rejected the SPH-omitted two-group state as above. No semantic
export was created; T6 repeat was correctly not attempted.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed |
| Cached input tokens | Unavailable | No task-level telemetry exposed |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed |
| Output tokens | Unavailable | No task-level telemetry exposed |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed |
| Total tokens | Unavailable | No task-level telemetry exposed |
| Estimated cost | Unavailable | No reliable task-level price allocation |
| Goal-service total | Unavailable | Reported separately; not added to this task |

Cost formula/basis: unavailable; no per-worker or review cost is invented.

## Numerical differences

The `1.118481` versus `1.115139` pair is a failed source regression assertion,
not a reproduced observable, tolerance, comparison result, or golden value.

## Deferred validation

- Fresh valid DRAGON completion, required semantic fields, mapping invariants,
  and T6 repeat - blocked pending XSEC-SPH-01.
- Full-core, converter, runtime, and Unity work - remains deferred.

## Blockers, risks, and follow-up

- Blocker: the v2 deck's SPH omission changes source behavior. Retaining SPH
  without admitting SPH-corrected source data requires an exact capture/staging
  rule that does not yet exist.
- Risk trigger: source regression failure and source-stage ambiguity. No value
  was repaired or accepted.
- Follow-up work: XSEC-SPH-01 must define and independently review whether the
  official procedure retains SPH for state evolution, exactly which pre-SPH
  object is externally captured at every knot, how subsequent state transitions
  remain source-authentic, and whether v2 requires replacement.

## Next eligible task

`XSEC-SPH-01` - resolve and review source SPH execution versus pre-SPH export
stage authority before any further v2 reproduction.

Source: [`AGENTS.md`](../../AGENTS.md), [`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`XSEC-DECK-01.md`](XSEC-DECK-01.md), and
[`XSEC-03-R1-REQUEST.md`](XSEC-03-R1-REQUEST.md).
