# XSEC-03-R4 - fresh GOLVER whole-cell semantic decode

## Outcome

Status: **BLOCKED**

Eight fresh isolated deck-v5 GOLVER reruns passed source regression,
determinism, completion, and selected-structure checks. The selected
`WCFIELD` MACROLIBs have two groups, one mixture, one volume entry, and no
direct `SPH`, `SPH-EPSILON`, or `ADF` subtree. Yet neither state contains the
required `EFIS` record or `EFIS` edit label. They instead expose
`PRODUCTION` and source-emitted `H-FACTOR`; mapping v4 forbids `H-FACTOR` as a
substitute, and `PRODUCTION` has no approved EFIS identity.

The task stopped before numerical array decoding, compressed-scattering
decoding, transformations, or candidate admission. No source or derived
numerical value entered the repository.

Effectiveness: **BLOCKED**

## Execution and model evidence

- Role: root implementer
- Requested model / reasoning: GPT-5.6 Luna / high, per repository routing
- Actual model / reasoning: `UNVERIFIED`
- Execution receipt or telemetry source: Unavailable
- Attempts: one completed fresh-evidence attempt; two external runner-layout
  corrections before it produced no source result; elapsed time: Unavailable
- Artifact/checkpoint status: produced; checkpoint pending final audit
- Review disposition: not run; deferred to XSEC phase closeout by the owner's
  2026-08-29 validation/review sequencing direction
- Review evidence verification: not applicable
- Reviewer reuse/fresh-review rationale: not applicable; no candidate passed
  the required-field gate

## Files created or changed

- `docs/tasks/XSEC-03-R4-OWNER-APPROVAL.md` - bounded authority and sequencing.
- `docs/tasks/XSEC-03-R4-REQUEST.md` - task boundary and fail-closed checks.
- `reference/manifests/xsec-03-r4-semantic-observation-v1.json` - path-free
  reproduction, structural observation, and blocker evidence.
- `docs/tasks/XSEC-03-R4.md` - this report.
- `docs/PROJECT_SCOPE.md` - current-state handoff, updated after this report.

Deck v5, mappings v1/v3/v4, XSEC-DIFF-01 evidence, Core, CLI, Unity, and all
source artifacts were inspected but not changed. The pre-existing untracked
`docs/tasks/P10-T05-R1.md` was preserved.

## Assumptions and design choices

- Only the completed XSEC-DIFF-01 GOLVER route and pinned source/image were
  used. Capture-target-only variants preserved its original-output controls.
- A documented PyLCM reader was built from the pinned Version5 source in an
  external temporary directory and run with networking disabled. It recorded
  field names, dimensions, and booleans only.
- Missing EFIS is a critical source/mapping mismatch. The task did not infer
  `PRODUCTION` equivalence, substitute `H-FACTOR`, or attempt a workaround.
- Focused validation ran here; the owner deferred full regression and review
  together to XSEC phase closeout. This does not waive this blocker.

## Validation commands and results

### T0 - task artifact and manifest format

```text
Get-Content -Raw reference/manifests/xsec-03-r4-semantic-observation-v1.json | ConvertFrom-Json
```

Result: passed. The manifest parses as JSON; its `BLOCKED` disposition,
eight-entry ledger, no-value retention flags, required-EFIS absence, and report
blocker identifier passed focused repository checks. `git diff --check` also
passed.

### T1 - fresh source reproduction

```text
docker run --rm --entrypoint /bin/sh --network none --read-only --tmpfs /tmp:exec --tmpfs /run -e OMP_NUM_THREADS=1 -v <isolated-root>:/dragon/5.1/Dragon -w /dragon/5.1/Dragon docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 -lc './rdragon -q tcwux11sphcap.x2m'
```

Result: eight of eight roots had normal end, source completion, GOLVER
activation, convergence evidence, and no abnormal diagnostic. Original
PRE0/PRE172 captures exactly match XSEC-03-R2 baselines. Field captures are
byte-identical within state and distinct across states. The known nonfatal
IEEE underflow/denormal runner notice was observed; it was not numerical
evidence. All path-free identities are in the manifest.

### T1 - preservation identity

```text
Canonicalize each external TCWUX11.c2m procedure to LF UTF-8, SHA-256 it, compare with the matching XSEC-DIFF-01 identity, then inspect each listing for normal end, TCWU11 completion, GOLVER activation, and abnormal diagnostics.
```

Result: all eight procedure identities match XSEC-DIFF-01. The route retains
three original EDI lexemes, two SPH lexemes, four assertion lexemes, and
`WHOLECELL0`; no `REAC DIFF` extra edit or leakage selection was introduced.

### T2 - external selected-payload observation

```text
docker run --rm --entrypoint /bin/sh --network none --read-only --tmpfs /tmp:exec --tmpfs /run -v <external-decoder-root>:/decoder:ro -w /decoder docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 -lc 'PYTHONPATH=/decoder/PyGan/lib/Linux_x86_64/python/Lcm-5.0-py3.12-linux-x86_64.egg python3 lcm_observe.py'
```

Result: all four fresh field captures expose the selected structure and all
required record names except `EFIS`. EFIS is absent in both groups at both
states and absent from the additional-edit label. `PRODUCTION` and
`H-FACTOR` are present. The observer read no numerical values and began no
mapping decode; its path-free fingerprint is in the manifest.

### T2 - official field-meaning cross-check

```text
Inspect pinned IGE-351 MACROLIB record definitions and the pinned EDIPXS PRODUCTION writer; compare record identity and documented meaning with mapping v1/v4.
```

Result: official documentation supports the meanings of available records but
does not grant `PRODUCTION` an EFIS identity in mapping v1/v4. The mismatch is
a fail-closed blocker, not a conversion defect.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No per-task telemetry exposed. |
| Cached input tokens | Unavailable | No per-task telemetry exposed. |
| Cache-write input tokens | Unavailable | No per-task telemetry exposed. |
| Output tokens | Unavailable | No per-task telemetry exposed. |
| Reasoning output tokens | Unavailable | No per-task telemetry exposed. |
| Total tokens | Unavailable | No per-task telemetry exposed. |
| Estimated cost | Unavailable | No provider usage receipt exposed. |
| Goal-service total | Unavailable | No goal-service total exposed. |

Cost formula/basis: unavailable; no request-level billing telemetry was
available and no allocation is inferred.

## Numerical differences

Not applicable. Numerical arrays and derived values were not decoded or
retained after the required-field gate failed.

## Deferred validation

- Compressed-scattering decode, transforms, invariants, and candidate
  evaluation - blocked by `XSEC-03-R4-MISSING-EFIS`.
- Full Core/Golden/CLI regression and independent code review (high) - deferred
  together to the XSEC phase-closeout gate under owner direction; neither is
  represented as passed here.
- Runtime pack, Core/CLI/Unity, DONJON/full-core, golden/reference, and Unity
  validation - not authorized and not admitted.

## Blockers, risks, and follow-up

- Blocker: `XSEC-03-R4-MISSING-EFIS`. The approved mapping requires a
  fission-only EFIS record, but fresh selected payloads provide neither it nor
  its edit label.
- Risk triggers: required source-field/meaning mismatch; all dependent work
  stopped fail-closed.
- Risks accepted or deferred: `PRODUCTION` may have a relevant source-side
  relation, but no equivalence, unit, normalization, or mapping authority
  exists; it was not used.
- Follow-up work: define and authorize `XSEC-EFIS-01` to prove a
  source-preserving EFIS export identity and exact mapping, or reject this
  source route. It must not alter mapping v1/v4 or reuse this evidence as a
  numerical candidate without that authority.

## Next eligible task

None until `XSEC-EFIS-01` is separately defined and authorized to resolve the
required EFIS source-field identity.

Source: [`AGENTS.md`](../../AGENTS.md) and
[`TASK_REPORT_TEMPLATE.md`](TASK_REPORT_TEMPLATE.md).
