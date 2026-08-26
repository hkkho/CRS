# XSEC-03 - DRAGON lattice candidate reproduction and semantic export

## Outcome

Status: BLOCKED

Effectiveness: BLOCKED

No valid DRAGON lattice result, semantic export, candidate pack, full-core
case, or golden evidence was produced. The first external fresh-deck attempt
established a critical source-deck conflict before a valid calculation: frozen
`TCWU11` contains WLUP-specific numerical `assertS` expectations, but the
pinned JEFF-3.1 procedure records different values. Removing or changing those
assertions is not among XSEC-02's three allowed project-deck deltas.

Applicable P1-T08 digest rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`.

## Execution and model evidence

- Role: root implementer.
- Requested model / reasoning: GPT-5.6 Luna / high.
- Actual model / reasoning: UNVERIFIED; request-level execution telemetry is
  unavailable.
- Execution receipt or telemetry source: current Codex task; no provider
  request-level receipt was exposed.
- Attempts: one bounded external reproduction attempt; no valid calculation
  artifact reached the semantic-export stage.
- Artifact/checkpoint status: `NO_ARTIFACT` for the required DRAGON semantic
  export; the task request and this diagnostic report were produced.
- Review disposition: BLOCKED. A new independent review is deferred to the
  required deck-authority task; no candidate change was made here.
- Review evidence verification: not applicable.
- Reviewer reuse/fresh-review rationale: no reviewer was dispatched because
  the task stopped on a frozen-authority conflict before a candidate exists.

## Files created or changed

- `docs/tasks/XSEC-03-REQUEST.md` - bounded offline reproduction authority and
  retention/exclusion rules.
- `docs/tasks/XSEC-03.md` - this blocked report.

All source decks, WLUP/JEFF binary inputs, converted files, output listings,
compiled CLE objects, and failed-run diagnostics remain only in external
temporary directories. No raw source, generated result, or runtime pack was
copied into the repository.

## Observed critical conflict

The frozen source procedure `Dragon/data/twlup_proc/TCWU11.c2m` asserts these
effective-multiplication values, in execution order:

```text
1.121035; 0.9414081; 1.121052; 1.075327
```

The exact pinned JEFF procedure
`Dragon/data/tjeff31gx_proc/TCWUX11.c2m` asserts instead:

```text
1.118478; 0.9420414; 1.118481; 1.073615
```

The shared `assertS` procedure aborts when relative difference is not below
`1.0E-4`. Thus a TCWU11 procedure with only the approved JEFF library procedure
substitution is not executable through its frozen test assertions. The
assertions cannot be treated as a numerical result or edited as an incidental
export change: the XSEC-02 mapping allows only library resolution, two exact
SPH omissions, and non-mutating post-EDI/pre-SPH serialization.

## Validation commands and results

### T0 - external source/library identity check

```text
Get-FileHash -Algorithm SHA256 TCWU05Lib.c2m WLUP172
```

Result: the external JEFF procedure SHA-256 was
`896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040` and the
converted WLUP binary SHA-256 was
`c7fc05d6b7cb2085d999c568854aeaacc9c79477dec4c702e6b7d3fe15ba5c5e`, matching
the XSEC-01 record. The pinned Docker image inspected as linux/amd64.

### T1 - fresh external DRAGON invocation

```text
docker run --rm --network none --read-only --tmpfs /tmp --tmpfs /run \
  --entrypoint sh -v <external-run-root>:/work -w /work \
  docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 \
  -c '/dragon/5.1/Dragon/bin/Linux_x86_64/Dragon < xsec03-tcwu11.x2m \
  > dragon.result 2> dragon.stderr'
```

Result: blocked before a valid source run. The first fresh work root also
exposed stale compiled CLE-object contamination after an invocation-wiring
probe, so it was abandoned. A second independently fresh root reached the
frozen procedure load and terminated with CLE error `108` before producing a
valid semantic export. Inspection of the pinned source procedures and `assertS`
established the conflicting numerical assertions above. No T6 repeat was
attempted because the first valid run precondition was not met.

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

Cost formula/basis: unavailable. No per-worker or review cost is invented from
aggregate service usage.

## Numerical differences

No candidate numerical comparison is admissible. The only values recorded are
the conflicting source-deck assertion lexemes above; they are diagnostic source
facts, not reproduced observables or golden values.

## Deferred validation

- T6 fresh DRAGON semantic-export repeat and manifest comparison - blocked
  pending XSEC-DECK-01 exact-deck authority.
- Mapping invariants, Core/CLI integration, Unity consumption, and numerical
  comparison - deferred; no source semantic export exists.
- DONJON static-core work - remains deferred to XSEC-CORE-01 and XSEC-04.

## Blockers, risks, and follow-up

- Blocker: unresolved conflict between frozen TCWU11 assertions and the
  admitted JEFF library route. Choosing deletion, replacement, or official
  TCWUX11 adoption would change the exact source/deck authority.
- Risk trigger: exact reference input/numerical expectation mismatch. The task
  stopped before a DRAGON result could be accepted.
- Follow-up work: `XSEC-DECK-01` must make and independently review the exact
  source-deck decision: either a precisely bounded assertion handling for
  TCWU11 or adoption/mapping of the official JEFF `TCWUX11` procedure, with
  source identities, all permitted deltas, validation intent, and consequences
  for XSEC-02. It must not infer a result from the current failure.

## Next eligible task

`XSEC-DECK-01` - resolve and review the exact JEFF lattice deck basis before
any further XSEC-03 DRAGON execution.

Source: [`AGENTS.md`](../../AGENTS.md),
[`Implementation_plan.md`](../Implementation_plan.md),
[`PROJECT_SCOPE.md`](../PROJECT_SCOPE.md),
[`XSEC-03-REQUEST.md`](XSEC-03-REQUEST.md), and the XSEC-02 mapping authority.
