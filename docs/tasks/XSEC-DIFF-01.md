# XSEC-DIFF-01 - source diffusion-coefficient route decision

## Outcome

Status: COMPLETE

`XSEC-DIFF-01` resolves the semantic blocker that prevented a new XSEC-03
decode. It selects DRAGON5 EDI `GOLVER` as the only source route for the
existing no-leakage candidate's required `DIFF` field. IGE-335 defines its
Golfier-Vergain coefficient equation, IGE-351 identifies the resulting
MACROLIB `DIFF` as an isotropic diffusion coefficient in centimetres, and the
pinned EDI writer records that `GOLVER` writes that field before its leakage
branches.

Eight fresh, network-isolated source roots prove the selection without a
source-path mutation: the original PRE0/PRE172 regression captures exactly
match XSEC-03-R2, all four source assertions, all three original EDI source
lexemes, and both original SPH calls are
retained, each run completes normally with convergence evidence, and each
same-state GOLVER field capture is byte-identical. The field payloads expose
`EFIS` and `DIFF`, while the historic `REAC DIFF` scattering extra edit is
absent. No numerical array or numerical value was decoded or retained.

Effectiveness: SUCCESS

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They are methodology/applicability context
only; the field identity and equation are bound to the hash-identified
official DRAGON5 guides and source writer.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer
  `01a04bbd-63c5-79f3-bbce-daa7fb73d010`. Provider model/effort telemetry was
  not exposed.
- Attempts: two isolated pre-authority probes, then eight final fresh source
  roots, one T3 regression, and one independent review. Elapsed time
  unavailable.
- Artifact/checkpoint status: produced the owner approval, request, deck v5,
  mapping v4, path-free manifest, and this report. Raw source procedures,
  listings, captures, libraries, values, and temporary runner copies remain
  external-only.
- Review disposition: PASS.
- Review evidence verification: UNVERIFIED. The same reviewer completed its
  correction recheck, but provider model/effort telemetry was not exposed.
- Reviewer reuse/fresh-review rationale: the same reviewer completed the
  initial CONDITIONAL PASS and the final PASS recheck, as required by
  `AGENTS.md`.

## Files created or changed

- `docs/tasks/XSEC-DIFF-01-OWNER-APPROVAL.md` - bounded owner authority.
- `docs/tasks/XSEC-DIFF-01-REQUEST.md` - reviewed one-task boundary.
- `docs/spec/xsec-jeff-lattice-deck-v5.md` - source-preserving GOLVER field
  route.
- `docs/spec/xsec-source-runtime-mapping-v4.md` - exact `DIFF` identity,
  unit, formula source, and successor boundary.
- `reference/manifests/xsec-diff-01-route-v1.json` - path-free official
  source, eight-run, field-semantics, and non-admission evidence.
- `docs/tasks/XSEC-DIFF-01.md` - this report.

`docs/spec/xsec-jeff-lattice-deck-v4.md`,
`docs/spec/xsec-source-runtime-mapping-v1.md`,
`docs/spec/xsec-source-runtime-mapping-v3.md`, prior XSEC reports, source
guides, and all external source/capture artifacts were inspected but not
changed in the repository. No Core, CLI, Unity, schema, data-pack, or golden
artifact changed.

## Assumptions and design choices

- The no-leakage transport case makes the default leakage-coefficient route
  unavailable. `GOLVER` is the documented formula route for a standard `DIFF`
  record; it is selected explicitly rather than reconstructed in project code.
- `REAC 1 EFIS GOLVER` requests only fission-only energy production as an
  extra edit and selects the separate coefficient calculation. `REAC DIFF`
  remains prohibited because its collision label denotes scattering.
- Source-emitted `H-FACTOR` may remain in a selected payload but is never
  selected, decoded, transformed, or substituted for `EFIS`.
- The selected `DIFF` remains offline DONJON/reference evidence only. It
  cannot be used to derive Core conductances, topology, node volume,
  normalization, or any runtime field.

## Validation commands and results

### T0/T1/T2 - official-source, route, and manifest audit

```text
PowerShell external audit: hash IGE-335 section 3.08, IGE-351 edition and
MACROLIB sections, EDIPXS, and EDIGET; inspect the GOLVER formula/parser/writer
locators; assert the preserved source calls/assertions, exact GOLVER field
clauses, no REAC DIFF, source completion/convergence, capture-target-only
normalization, baseline equality, field labels, direct-subtree absence, and
numeric-array-decoded=false. Parse xsec-diff-01-route-v1.json and run
git diff --check.
```

Result: PASS. The source writer's `IGOVE=1` branch writes standard `DIFF`
using the documented Golfier-Vergain formula. The manifest contains only
hashes, counts, booleans, names, meanings, units, and source locators.

### T6 - fresh DRAGON5 source reproduction

```text
Eight independent roots: PRE0 original a/b, PRE0 GOLVER field a/b, PRE172
original a/b, and PRE172 GOLVER field a/b. Each used the pinned DRAGON5 image
through rdragon with network disabled, read-only root, executable temporary
/tmp filesystem, temporary /run filesystem, and OMP_NUM_THREADS=1.
```

Result: PASS. Every final run has four source assertions, six external
convergence markers, normal completion, source completion, GOLVER activation,
and no abnormal diagnostic. Original PRE0/PRE172 captures exactly match
XSEC-03-R2; GOLVER field captures are byte-identical per state and distinct
across states. The container runner emitted a nonfatal IEEE underflow/denormal
notice on standard error; all source listings ended normally and no numerical
array was decoded. Exact path-free identities are in the manifest.

### T3 - full headless Core/Golden/CLI regression

```text
$env:DOTNET_ROOT='C:\Users\infin\AppData\Local\Temp\candu-dotnet-sdk-10.0.302'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath C:\Users\infin\AppData\Local\Temp\candu-xsec-diff-01-t3
```

Result: PASS. Core 208/208, Golden 26/26, and CLI 38/38 passed with zero
failures and zero skips. The first sandboxed attempt could not read the host
Microsoft SDK path; the same command succeeded in the authorized local
environment. This was an environment access issue, not a test failure.

### Independent code review (high)

```text
Independent read-only review of the owner approval, request, deck v5, mapping
v4, manifest, report, and scope routing against AGENTS.md, XSEC-FIELD-01,
mapping v1/v3, and deck v4.
```

Result: PASS. The reviewer required explicit per-run baseline
bindings for field exports, explicit original-control pre-array/direct-
correction check status, preservation fingerprints, a named `XSEC-03-R4`
successor, and corresponding report/scope reconciliation. The same reviewer
verified all corrections, upheld the GOLVER semantics, source-preservation
boundary, non-retention rule, and T3/T6 evidence, then returned final PASS.
Actual telemetry remains UNVERIFIED.

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

Not applicable. This task proves field identity and source reproducibility but
stops before numerical-array decoding, transformation, comparison, candidate
row admission, or reference/golden evaluation.

## Deferred validation

- Semantic decoding, all inherited mapping invariants and transformations,
  candidate-row admission, converter/pack work, Core/CLI/Unity consumption,
  DONJON/full-core use, and golden/reference consideration are deferred to a
  separately authorized successor XSEC-03 semantic-decode task.
- T4/T5 do not apply because no runtime or Unity artifact changed.

## Blockers, risks, and follow-up

- Blockers: none for this bounded authority task.
- Risk trigger: source-field meaning/unit ambiguity fired in XSEC-FIELD-01.
  Required escalation occurred here through an owner-approved equation choice,
  official source binding, fresh T6 evidence, T3, and independent review.
- Risks accepted or deferred: the GOLVER model is selected only for this
  no-leakage offline candidate. It is not asserted equivalent to a leakage
  coefficient or any other coefficient-generation model. The nonfatal runner
  underflow/denormal notice remains traceability information; a successor
  decoder must fail closed on every nonfinite or invalid decoded value.
- Follow-up work: define and execute a new XSEC-03 semantic-decode task under
  deck v5/mapping v4. It must fresh-rerun the eight-root route and own all
  numeric decoding, invariant, transformation, and admission decisions.

## Next eligible task

Define a separate, owner-approved XSEC-03 semantic-decode task under deck v5
and mapping v4. `XSEC-03-R3` and `XSEC-FIELD-01` must not be rerun unchanged.

Source: `AGENTS.md`, `XSEC-DIFF-01-REQUEST.md`, mapping v1/v3/v4, deck
v4/v5, `XSEC-FIELD-01`, official IGE-335/IGE-351, and
`xsec-diff-01-route-v1.json`.
