# XSEC-SPH-02 - prove non-mutating pre-SPH capture for TCWUX11

## Outcome

Status: BLOCKED

The task reproduced the exact source SPH state transitions and found a
non-mutating *in-process* copy: `PRE0 := EDITION` or `PRE172 := EDITION`
immediately after each `EDI:` call, followed by the original
`EDITION := SPH: EDITION ...` statement.  A clean candidate using that form
passed all four unchanged source assertions and completed normally.  It did
not, however, retain a capture artifact: DRAGON's normal `END` removes its
sequential files before `rdragon` invokes its post-run save hook.

The documented persistent-file alternatives were then bounded and tested.
`FILE` declarations in the called procedure and caller-side `PARAMETER`
bindings each failed during CLE-2000 compilation before any altered physics
run.  The only earlier candidate that retained files did so by aborting in
cleanup, which is not a valid source completion.  Therefore this task cannot
prove a persistent post-EDI/pre-SPH capture with the approved source/runner
contract.  No v3 deck/mapping authority, export, pack, Core/CLI/Unity change,
golden case, or reference admission was made.

## Files created or changed

- `docs/tasks/XSEC-SPH-02.md` - this immutable blocked-task evidence.
- `reference/manifests/xsec-sph-02-capture-probes-v1.json` - path-free,
  hash-bound external-probe record.

All DRAGON inputs, altered candidate procedures, WLUP data, compiler outputs,
and generated objects remain external-only.

## Applicable literature evidence

Applicable `P1-T08` digest rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`.  They are method/background evidence only;
they do not supply a golden value or authorize source-stage changes.  The
tool-language evidence was the external IGE-335 copy, SHA-256
`1b89fa2d8c30ba3d1ece03c96ccf6ae384ee4a11a384c3ca395ddfb616acc5d`:
section 3.11 defines SPH as producing a new corrected edition from an
existing RHS edition, and pages 356-357 describe FILE-backed sequential
objects as persistent after normal run completion.

## Assumptions and design choices

- The hash-bound external fixture and supported `rdragon` context from
  `XSEC-RUNNER-01` were used unchanged: pinned image
  `docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79`,
  no network, read-only root, executable `/tmp`, and one thread.
- Source identities remained TCWUX11
  `9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813`,
  TCWU05Lib `896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040`,
  assertS `c6291cd7a2496f48ab012c294e2d823170401549cc5df3e366c9924f0a34040c`,
  and WLUP172 `c7fc05d6b7cb2085d999c568854aeaacc9c79477dec4c702e6b7d3fe15ba5c5e`.
- No assertion, value, tolerance, source equation, unit, group structure,
  normalization, or approved mapping field was changed.  The candidate that
  passed assertions kept `EDITION` as both the original SPH RHS and LHS.

## Validation commands and results

### T0/T1 - source semantics and candidate probes

```text
docker run --rm --network none --read-only --tmpfs /tmp:exec --tmpfs /run \
  --entrypoint sh -v <external-data>:/dragon/5.1/Dragon/data:ro \
  -v <external-results>:/dragon/5.1/Dragon/Linux_x86_64 \
  -w /dragon/5.1/Dragon <pinned-image> \
  -c './rdragon -q tcwux11sphcap.x2m'
```

Result: candidate C was rejected after the second source assertion because
using `PRE0` as the SPH RHS yielded `EDI: NO REFERENCE TRACKING AVAILABLE`.
This establishes that changing the source SPH operand identity is not an
admissible capture method.

Result: candidate F, which copied the post-EDI object but retained the
original `EDITION := SPH: EDITION ...` calls, completed normally.  Its
external-only source SHA-256 was
`c57cb865f2a9f7238db482359d10711795c0eef20f63ee3c7829159005404bac` and
listing SHA-256 was
`ec9ae990b0961ea01a244980f056951aa07ae952092e3b2d42957f8df71381b8`.
All source deltas exactly matched both clean XSEC-RUNNER-01 baselines:

| Assertion reference | Candidate/baseline delta |
|---:|---:|
| 1.118478 | 2.131634e-06 |
| 0.9420414 | 9.490768e-07 |
| 1.118481 | 9.059420e-06 |
| 1.073615 | 7.328338e-06 |

The listing contains `test TCWU11 completed` and `normal end of execution`.
Its sequential artifacts were unavailable to the launcher save hook after
normal `END`; the task therefore has no retained pre-SPH object to admit.

Result: candidates H/I placed explicit `FILE` declarations in the called
procedure.  They failed at CLE compilation.  Candidates J/K/L/M moved the
FILE declaration to the caller and attempted the documented `PARAMETER`
output form, including the one-output pattern used by the official `twlup`
driver.  They also failed at CLE compilation before DRAGON physics execution.
The last source/driver identities are recorded in the manifest.

### T3/T6

Not run.  No source-stage authority revision, numerical-data change, runtime
change, or approved capture exists.  T3 and independent code review (high)
would be mandatory only for a proposed authority revision; T6 remains
deferred to an eligible fresh XSEC-03 rerun.

## Execution and model evidence

- Requested implementation model/reasoning: GPT-5.6 Luna, high.
- Actual implementation model/reasoning: UNVERIFIED; task-level execution
  telemetry is not exposed.
- Attempts: source-preserving probes C through M; only candidate F reached
  valid normal source completion.
- Artifact status: external-only probe artifacts produced; repository retains
  the report and path-free manifest only.
- Independent code review (high): PASS. The reused reviewer verified the
  per-probe object/timing/target and source/driver/hook/listing identities,
  agreed with the critical blocker and no-admission boundary, and found no
  authority revision warranted. Actual reviewer model/reasoning telemetry is
  UNVERIFIED.
- Reviewer: `01a03b98-9b86-7f22-9068-76bb1ce6b01e`; one bounded initial
  review and one final disposition after the provenance correction. Reuse
  preserved the XSEC runner/deck review context.

## Token and cost accounting

| Field | Value |
|---|---|
| Input, cached, cache-write, output, reasoning, and total tokens | Unavailable |
| Cost basis / estimate | Unavailable; no verified task-level billing receipt |
| Goal-service total | Not allocable and not added to this task |

## Blocker, risk, and follow-up

The inability to retain and prove the pre-SPH object is a critical source
object/serialization ambiguity under the task definition and `AGENTS.md`.
Proceeding would require inventing a CLE procedure interface or treating an
abnormally terminated capture as valid.  Neither is authorized.

The required follow-up is a separately defined source-interface task that
obtains an official, executable TCWUX11-compatible persistent capture pattern
or an approved replacement case with an explicit capture contract.  It must
then reproduce two clean candidates, compare all four assertions with the
existing two clean baselines, and obtain independent high review.  It may not
alter assertions, SPH state transitions, mappings, or physics values.

## Next eligible task

No dependent XSEC-03 rerun is eligible.  The next work must be a new bounded
task-definition/owner-approval record for the source-interface blocker above;
its ID and authority are not invented here.
