# XSEC-FIELD-01 - required-field source export resolution

## Outcome

Status: BLOCKED

Eight fresh, network-isolated DRAGON5 reruns prove that a separate `WCFIELD`
EDI object can preserve the original `EDITION`, SPH, assertion, whole-cell,
and day-300 paths while explicitly exposing an `EFIS` extra edit.  The
unchanged original captures match the XSEC-03-R2 baselines exactly, and the
field-export captures are deterministic within each state and distinct across
states.  No numerical array was decoded or retained.

The route cannot satisfy the approved `DIFF` requirement.  IGE-335 defines
the requested `REAC DIFF` edit as a Legendre-order scattering cross section;
the mapping requires the IGE-351 `MACROLIB` `DIFF` isotropic diffusion
coefficient in centimetres.  The source writer confirms that a coefficient
record requires a leakage mode or the optional Golfier-Vergain formula.  The
candidate case has no transport leakage, and neither a leakage model nor that
formula is selected by an approved specification.  Treating the matching
label as the required coefficient, choosing a formula, or substituting
`H-FACTOR` would alter approved physics or field meaning.  This task therefore
stops fail-closed before numerical decoding.

Effectiveness: BLOCKED

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They remain methodology/applicability
context only and do not authorize a diffusion-coefficient formula, field
substitution, unit change, or golden value.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; task-definition
  reviewer `01a03b98-9b86-7f22-9068-76bb1ce6b01e`; final-review receipt pending.
  Provider model/effort telemetry was not exposed.
- Attempts: one documented source-operation probe, eight final fresh isolated
  roots, one external structural audit, one mandatory T3 regression, and one
  final review pending. A temporary day-300 linked-list omission failed before
  source execution, was corrected, and is recorded in the manifest. Elapsed
  time unavailable.
- Artifact/checkpoint status: produced a path-free route/blocker manifest and
  this report. Raw procedures, captures, listings, parser work, and numerical
  values remain external-only.
- Review disposition: PASS — blocked outcome upheld.
- Review evidence verification: UNVERIFIED. The same reviewer issued final
  PASS after the per-run ledger, T3 result, and scope routing corrections;
  provider model/effort telemetry was not exposed.
- Reviewer reuse/fresh-review rationale: the same reviewer who passed the
  XSEC-FIELD-01 definition is retained for its final disposition as required
  by `AGENTS.md`.

## Files created or changed

- `reference/manifests/xsec-field-01-route-v1.json` - path-free official
  source, eight-run, field-semantics, and fail-closed evidence.
- `docs/tasks/XSEC-FIELD-01.md` - this task report.

`docs/spec/xsec-jeff-lattice-deck-v4.md`,
`docs/spec/xsec-source-runtime-mapping-v3.md`, prior reports, and the
external-only source/capture artifacts were inspected but not changed. No deck
v5 or mapping v4 is created because the only demonstrated `DIFF` label has the
wrong approved meaning; creating either would falsely imply a valid route.

## Assumptions and design choices

- The candidate is the already approved two-state whole-cell route. `WCFIELD`
  is a separate named EDI output using the all-one 31-region merge; it neither
  overwrites `EDITION` nor changes the original source calls.
- The documented `REAC 2 EFIS DIFF` operation proves a real extra-edit export
  mechanism. The explicit field-name collision is resolved by official
  meaning and unit, never by a matching spelling.
- The standard coefficient record is not inferred from scattering data. The
  optional documented `GOLVER` operation is recognized as a distinct formula
  choice, not silently activated.
- `H-FACTOR` is retained only as a prohibited alternative; it is never read,
  transformed, or used.

## Validation commands and results

### T0/T1/T2 - official-source, manifest, and structure audit

```text
PowerShell external audit: hash IGE-335 sections 3.08/3.22/3.35 and IGE-351
MACROLIB guide; inspect EDI parser and EDIPXS writer locators; assert two
original EDI and two original SPH calls, four assertions, original-capture
baseline equality, target-only procedure normalization, direct-correction
absence, declared labels, deterministic field captures, and no numeric-array
read. Parse xsec-field-01-route-v1.json and run git diff --check.
```

Result: PASS for evidence integrity and BLOCKED as designed for field
semantics. `EFIS` is a fission-only energy-production extra edit; the `DIFF`
extra edit is not the mapped diffusion coefficient. The manifest contains only
hashes, counts, booleans, names, meanings, units, and source locators.

### T6 - fresh DRAGON5 source reproduction

```text
Eight independent roots: PRE0 original a/b, PRE0 field a/b, PRE172 original
a/b, and PRE172 field a/b. Each used the pinned DRAGON5 image through
rdragon with network disabled, read-only root, tmpfs /tmp and /run, and
OMP_NUM_THREADS=1.
```

Result: PASS. Every final run has four source assertions, normal completion,
source completion, and no abnormal diagnostic. Original PRE0/PRE172 captures
exactly match XSEC-03-R2; field captures are byte-identical per state and
distinct across states. Exact path-free identities are in the manifest.

### T3 - full headless Core/Golden/CLI regression

```text
$env:DOTNET_ROOT='C:\Users\infin\AppData\Local\Temp\candu-dotnet-sdk-10.0.302'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath C:\Users\infin\AppData\Local\Temp\candu-xsec-field-01-t3
```

Result: PASS. Core 208/208, Golden 26/26, and CLI 38/38 passed with zero
failures and zero skips.

### Independent code review (high)

```text
Same-reviewer final review of the owner approval, request, manifest, report,
and scope against mapping v1/v3, deck v4, IGE-335, and IGE-351.
```

Result: PASS. The same reviewer first required the completed T3 result, a
path-free eight-entry per-run ledger, and scope routing update; it then
verified all three corrections and upheld the BLOCKED technical conclusion.
Actual telemetry is UNVERIFIED.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No verified request-level receipt. |
| Estimated cost | Unavailable | No verified price/usage receipt. |
| Goal-service total | Not allocable | Not added to task totals. |

Cost formula/basis: unavailable; no task-level provider billing telemetry was
exposed.

## Numerical differences

Not applicable. The task stopped on source-field meaning before numerical-array
decoding, transformations, or comparisons.

## Deferred validation

- A source-diffusion-coefficient model decision, field decoding, invariants,
  transformation fingerprints, candidate-row admission, pack work,
  Core/CLI/Unity consumption, DONJON work, and golden/reference consideration
  are blocked pending a separately approved source-diffusion-coefficient task.
- T4/T5 do not apply because no runtime or Unity artifact changed.

## Blockers, risks, and follow-up

- Blocker: the only field-route `DIFF` edit is a scattering cross section, not
  the approved diffusion coefficient. The existing no-leakage case emits no
  standard coefficient record.
- Risk trigger: required field meaning/unit mismatch. Required response:
  stop before numerical decode; do not substitute or choose a new coefficient
  equation.
- Risks accepted or deferred: a future task may select a documented leakage or
  Golfier-Vergain route only after owner-approved field/equation, unit,
  validation, and admission rules exist.
- Follow-up work: define and independently review a bounded
  source-diffusion-coefficient specification task. It must choose or reject a
  coefficient-generation model before a new semantic-decode task can run.

## Next eligible task

None until a separately authorized source-diffusion-coefficient specification
task is defined and reviewed. XSEC-03-R3 and XSEC-FIELD-01 must not be rerun
unchanged.

Source: `AGENTS.md`, `XSEC-FIELD-01-REQUEST.md`, mapping v1/v3, deck v4,
XSEC-HOM-01, XSEC-03-R3, IGE-335, IGE-351, and
`xsec-field-01-route-v1.json`.
