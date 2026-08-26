# XSEC-SPH-01 - source SPH execution and pre-SPH export authority

## Outcome

Status: BLOCKED

The official DRAGON documentation establishes that `SPH:` creates a new
SPH-corrected edition from an existing edition object.  It therefore confirms
that the `EDITION` object serialized by TCWUX11 is post-SPH at the two source
stages.  It does not, by itself, establish an executable capture procedure
that preserves the official assertions and all later source state transitions.

The source procedure shows the exact unresolved dependency:

- at lines 96--103 it creates `EDITION` with `EDI:` and immediately replaces
  it with the `SPH:` result;
- at lines 115--118 it repeats that pattern after the 172-group burnup; and
- at lines 124--125 it uses and deletes the SPH-corrected `EDITION` before the
  two-group depletion sequence.

A separate pre-SPH edition name is a source-language possibility, but no
candidate was admitted because a clean execution proving unchanged source
assertions and source-equivalent later transitions could not be completed with
the pinned runner.  No v3 deck/mapping authority, numerical result, semantic
export, runtime pack, or golden/reference claim was created.

Effectiveness: BLOCKED

## Execution and model evidence

- Role: root implementer; code review (high) reviewer requested.
- Requested model / reasoning: GPT-5.6 Luna, high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local thread; reviewer submission
  `01a03fe7-5ab0-7653-99f3-349c0f64f728`.
- Attempts: 3 source-runner probes; elapsed time: Unavailable.
- Artifact/checkpoint status: produced: this blocker report only; no source
  output or raw library artifact was retained in the repository.
- Review disposition: BLOCKED. The reviewer agrees no v3 mapping/deck
  replacement is warranted until a clean source proof exists.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: same reviewer
  `01a03b98-9b86-7f22-9068-76bb1ce6b01e`, preserving the XSEC deck/mapping
  review context as required by `AGENTS.md`.

## Files created or changed

- `docs/tasks/XSEC-SPH-01.md` - immutable evidence of the source-stage
  authority blocker.
- `reference/manifests/xsec-sph-01-runner-probes-v1.json` - path-free,
  hash-bound record of the external-only documentation and runner probes.
- `docs/tasks/XSEC-RUNNER-01-REQUEST.md` - bounded follow-up task definition
  for the runner reproducibility blocker; no execution is included here.
- `docs/PROJECT_SCOPE.md` - updated after this report to preserve the blocker
  and route the required runner-environment task.
- `docs/tasks/ROUND-2026-08-25-XSEC-DATA-CHAIN.md` - updated routing only.

Pre-existing task reports, the v2 deck authority, the v1 mapping, all source
decks, raw JEFF/WLUP data, and DRAGON results were inspected but not changed.

## Assumptions and design choices

- Applicable literature digest coverage is inherited from the completed
  `P1-T08` record and XSEC mapping/deck reports: `S1-R04`, `S1-R05`, `S5-R03`,
  `S5-R04`, and `S6-R03`.  The manual evidence is source/tool behavior, not a
  new runtime physics authority.
- IGE-335 section 3.11 was treated narrowly: its `EDINEW := SPH: EDINAM ...`
  structure states that the RHS is an existing edition and the LHS is a new
  SPH-corrected edition.  It does not authorize treating a post-SPH edition as
  the mapping's required post-EDI/pre-SPH source.
- The official TCWUX11 source, TCWU05Lib, assertS, and WLUP172 fixture stayed
  outside the repository.  Their existing v2 identity hashes remain the only
  source identities referenced here.
- No assertion, source reference, tolerance, unit, group boundary,
  normalization, equation, or runtime schema was changed.  In particular, the
  previously failed deletion of SPH calls was not repeated.

## Validation commands and results

### T0 - source/manual and identity inspection

```text
Select-String IGE335.txt -Pattern 'SPH module|EDINEW|SPH-corrected'
Get-Content TCWUX11.c2m lines 82..172
Get-FileHash -Algorithm SHA256 TCWUX11.c2m TCWU05Lib.c2m assertS.c2m WLUP172
```

Result: PASS.  The fixture matched the v2 canonical SHA-256 identities:
TCWUX11 `9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813`,
TCWU05Lib `896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040`,
assertS `c6291cd7a2496f48ab012c294e2d823170401549cc5df3e366c9924f0a34040c`,
and WLUP172 `c7fc05d6b7cb2085d999c568854aeaacc9c79477dec4c702e6b7d3fe15ba5c5e`.
The manual confirms the new-object SPH semantics described above.

### T1 - unmodified official source runner probes

```text
docker run --rm --network none --read-only --tmpfs /tmp --tmpfs /run \
  --entrypoint sh -v <external-fixture>:/work -w /work \
  docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 \
  -c '/dragon/5.1/Dragon/bin/Linux_x86_64/Dragon < tjeff31gx.x2m > xsec-sph-01-source.result 2> xsec-sph-01-source.stderr'
```

Result: BLOCKED (exit 1). The unmodified source distribution runner reached
pre-TCWUX11 regression content but stopped with
`FLU: INCONSISTENT FLUX OBJECT TRACK-TYPE AT RHS (SYBIL). EXCELL EXPECTED.`
This is a runner/environment reproduction failure, not evidence against or
for TCWUX11.

```text
docker run ... Dragon < xsec-sph-01-target.x2m > xsec-sph-01-target.result
```

Result: BLOCKED (exit 1).  A targeted official-procedure entry point failed
during CLE-2000 compilation with `late` procedure errors.  A minimal driver
failed similarly.  These probes did not execute TCWUX11 and did not create a
candidate output.  All logs and temporary runners remain external-only.

### T3 / T6

Not run.  There was no approved mapping/source-stage revision, numerical data,
or runtime change to validate; fresh T6 is impossible until the exact runner
context is reproduced.  The most recent unaffected XSEC T3 wrapper evidence
remains Core `208/208`, Golden `26/26`, and CLI `38/38`.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No task-level telemetry exposed. |
| Estimated cost | Unavailable | No verified price/usage receipt. |
| Goal-service total | Not allocable | Goal service is blocked and is not added to this task. |

Cost formula/basis: unavailable; no per-task provider billing telemetry was
exposed.

## Numerical differences

Not applicable; no numerical behavior, source result, mapping value, or
runtime data changed.

## Deferred validation

- Fresh official TCWUX11 assertions and a candidate pre-SPH capture comparison
  - deferred to `XSEC-RUNNER-01` because the current pinned container/entry
  context cannot execute the source runner through TCWUX11.
- T3 mapping review and T6 repeated source runs - deferred to `XSEC-03-R2`
  after an exact, independently reviewed runner and capture authority exists.

## Blockers, risks, and follow-up

- Blockers: the pinned runtime cannot reproduce the official source runner to
  the TCWUX11 stage, and the targeted runner cannot establish the procedure
  context.  Therefore the required proof that an external pre-SPH capture
  preserves all four source assertions and later state transitions is absent.
- Risk triggers: source-stage reproducibility failure.  It is critical because
  advancing would require inventing/assuming the capture timing and source
  equivalence.
- Risks accepted or deferred: no source/mapping revision is accepted.  The
  manual's separate-output-object semantics are recorded only as a hypothesis
  for a future isolated execution diagnostic.
- Follow-up work: `XSEC-RUNNER-01` must establish the exact executable launch
  context for the hash-bound Version5 fixture, reproduce unmodified TCWUX11,
  and independently review that result before XSEC-SPH is reopened or a new
  capture authority is defined.  It may not change source assertions or
  physics/mapping values.

Independent review: BLOCKED by reviewer
`01a03b98-9b86-7f22-9068-76bb1ce6b01e`. The reviewer confirmed the
disposition and required an exact manual identity plus path-free provenance for
the runner failures; both are now in
`reference/manifests/xsec-sph-01-runner-probes-v1.json`. The review also
confirmed that the full-run `SYBIL`/`EXCELL` failure is pre-TCWUX11 evidence,
not evidence of a TCWUX11 defect. Actual reviewer model/reasoning telemetry
remains UNVERIFIED.

## Next eligible task

`XSEC-RUNNER-01 - reproduce the official TCWUX11 runner context` after a
bounded task-definition/owner-approval record is created.  `XSEC-03-R2` is
not eligible until that task and a reopened/reviewed SPH capture authority are
complete.

Source: [`AGENTS.md`](../../AGENTS.md) and
[`CODEX_TASK_TEMPLATE.md`](../../CODEX_TASK_TEMPLATE.md).
