# XSEC-03-R3 - fresh whole-cell DRAGON semantic decode

## Outcome

Status: BLOCKED

Eight fresh, network-isolated DRAGON5 roots proved the source-preserving v4 whole-cell route again: all have four source assertions, normal completion, ten source-emitted terminal convergence markers, no abnormal diagnostic, and the required capture determinism. The fresh unchanged PRE0/PRE172 captures exactly match the XSEC-03-R2 baselines; the fresh selected whole-cell payloads are byte-identical per state and distinct across states.

The semantic decoder stopped before reading a numerical array. Both exact selected MACROLIB payloads lack required `EFIS` and `DIFF` records. Mapping v1 requires `EFIS` for the fission-energy ratio and retains `DIFF` for the offline-reference boundary; it expressly prohibits substituting `H-FACTOR`. Deck v4/mapping v3 does not authorize adding source export fields or changing that mapping. The candidate cannot produce a complete semantic row.

Effectiveness: BLOCKED

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03`; they are methodology/applicability context only and do not provide a substitute field, unit, mapping, or golden value.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer `01a03b98-9b86-7f22-9068-76bb1ce6b01e`. Provider model/effort telemetry was not exposed.
- Attempts: one reviewed task-definition correction cycle; eight final fresh source roots; one read-only semantic preselection audit; one final review pending. Elapsed time unavailable.
- Artifact/checkpoint status: produced owner approval, reviewed request, path-free source/semantic manifest, report, and scope routing. Raw procedures, captures, listings, decoder work, and numerical values remain external-only.
- Review disposition: PASS — blocked outcome upheld.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: the same reviewer who reviewed XSEC-HOM-01 reviewed this definition. Its initial CONDITIONAL PASS rejected numerical repeat-difference retention; the request was corrected to retain only fingerprints and repeat-equality booleans, then received definition PASS.

## Files created or changed

- `docs/tasks/XSEC-03-R3-OWNER-APPROVAL.md` - bounded owner authority.
- `docs/tasks/XSEC-03-R3-REQUEST.md` - reviewed one-task execution boundary.
- `reference/manifests/xsec-03-r3-semantic-v1.json` - path-free fresh run, selector, deterministic-payload, and fail-closed field-presence evidence.
- `docs/tasks/XSEC-03-R3.md` - this report.
- `docs/PROJECT_SCOPE.md` - current XSEC routing after this report.

All source/mapping specifications, Core/CLI/Unity code, schemas, data packs, raw procedures, captures, listings, and numerical data were inspected but not changed in the repository.

## Assumptions and design choices

- The decoder selects MACROLIB occurrence one for `WHOLECELL0/REF-CASE0001/MACROLIB` and occurrence two for `WHOLECELL0/REF-CASE0002/MACROLIB`, as proven by the external serialized hierarchy. It hashes the selected payload but retains no value.
- A missing required label is a structural failure before numerical-array parsing. `H-FACTOR` was observed as an alternate label but was not read, transformed, or substituted because mapping v1 requires `EFIS`.
- An initial source-listing collector treated internal spacing too strictly and expected eight rather than the established ten terminal markers. It did not change a source result; the corrected whitespace-tolerant detector validated all eight roots with ten markers. A separate disposable parser probe used a helper name that conflicted with a shell alias and was discarded. It made no repository or external source modification; the final audit emits only labels, counts, booleans, and hashes.

## Validation commands and results

### T0/T1/T2 - manifest, source identity, and semantic preselection

```text
PowerShell external read-only audit: locate the exact selected MACROLIB occurrence for each approved REF-CASE path; hash the selected payload; assert MACROLIB signature, no direct correction subtree, and field-label presence/absence before any numerical array is read. Parse xsec-03-r3-semantic-v1.json; assert eight run records, exact XSEC-03-R2 original-capture matches, same-state determinism, state distinction, four assertions, ten terminal markers, normal completion, zero abnormal diagnostics, and EFIS/DIFF absence. Run git diff --check.
```

Result: BLOCKED as designed. The selector payloads are unambiguous, deterministic, direct-correction-free, and contain the required structural and most reaction/profile labels. Both are missing `EFIS` and `DIFF`; the decoder stopped before a numeric array or transformation was read. The manifest carries hashes and booleans only.

### T6 - fresh DRAGON5 source reproduction

```text
Eight independent clean external roots: PRE0 original a/b, PRE0 whole-cell a/b, PRE172 original a/b, and PRE172 whole-cell a/b. Each used the pinned rdragon command with network disabled, read-only root, /tmp and /run tmpfs, and OMP_NUM_THREADS=1.
```

Result: PASS. All eight runs passed four source assertions, normal source completion, and the ten-marker convergence detector with no abnormal/nonconverged diagnostic. Original capture pairs exactly match XSEC-03-R2; whole-cell capture and selected-payload pairs are byte-identical per state and distinct across states. Exact hashes are in the manifest.

### T3 - full headless Core/Golden/CLI regression

```text
$env:DOTNET_ROOT='C:\Users\infin\AppData\Local\Temp\candu-dotnet-sdk-10.0.302'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath C:\Users\infin\AppData\Local\Temp\candu-xsec-03-r3-t3
```

Result: PASS. Core 208/208, Golden 26/26, and CLI 38/38 passed.

### Independent code review (high)

```text
Same-reviewer final review of the owner approval, request, manifest, report, and scope against mapping v1/v3 and deck v4.
```

Result: PASS. The initial CONDITIONAL PASS required field presence to be bound separately to each selected payload and successful XSEC-03-R2 assertion-baseline comparison to every run. The corrected manifest records both selectors' `EFIS`/`DIFF` absence and `H-FACTOR` presence, a pre-array stop flag, and all eight baseline comparisons. The same reviewer returned final PASS; actual telemetry remains UNVERIFIED.

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

Cost formula/basis: unavailable; no task-level provider billing telemetry was exposed.

## Numerical differences

Not applicable. The fail-closed field-presence check ran before numerical-array decoding, transformations, or comparisons.

## Deferred validation

- Complete source-array decoding, invariant checks, transformation fingerprints, candidate semantic-row admission, pack conversion, Core/CLI/Unity consumption, full-core/DONJON work, and golden/reference consideration are blocked pending a separately approved source/deck and mapping resolution.
- T4/T5 do not apply because no runtime or Unity artifact changed.

## Blockers, risks, and follow-up

- Blocker: selected direct whole-cell MACROLIB payloads do not export required `EFIS` and `DIFF` records. Current mapping prohibits substituting `H-FACTOR` for `EFIS`; current deck/mapping authorizes no export-field modification.
- Risk trigger: a missing required source field. Required response: stop before numerical decode; do not alter fields, units, or mapping by implication.
- Risks accepted or deferred: `H-FACTOR` may have a documented role, but it is not the approved `EFIS` field and cannot be used without separate authority.
- Follow-up work: define a separately owner-approved source/deck and mapping task to prove a source-preserving required-field export route, or to make an independently reviewed mapping decision with explicit field meaning, units, normalization, and admission consequences.

## Next eligible task

None until a separately approved required-field export/mapping-resolution task is defined. XSEC-03-R3 must not be rerun unchanged.

Source: `AGENTS.md`, `XSEC-03-R3-REQUEST.md`, mapping v1/v3, deck v4, XSEC-HOM-01, and `xsec-03-r3-semantic-v1.json`.
