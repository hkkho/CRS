# XSEC-HOM-01 - source-preserving whole-cell export route

## Outcome

Status: COMPLETE

The bounded source route is proven. A separate `WHOLECELL0` EDI object creates one explicitly named whole-cell mixture from all 31 original region slots at day 0 and is updated in place at day 300. Neither original `EDITION` EDI/SPH path nor its four source assertions was changed. In two fresh roots per state, the unchanged original outputs are byte-identical to the XSEC-03-R2 capture baselines. The separate whole-cell captures are byte-identical per state, distinct across states, and structurally contain two groups, one mixture, and one volume entry with no direct correction subtree.

No field array or numerical cross-section value was decoded, retained, mapped, or admitted. This is a source-route and structural authority only.

Effectiveness: SUCCESS

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03`; they are methodology/applicability context only. Official DRAGON5 EDI/data-structure guides and the hash-bound procedure are the source-route evidence.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer `01a03b98-9b86-7f22-9068-76bb1ce6b01e`. Provider model/effort telemetry was not exposed.
- Attempts: one reviewed task-definition correction cycle; one bounded external route investigation; eight final fresh isolated reference runs; one final same-reviewer review. Elapsed time unavailable.
- Artifact/checkpoint status: produced owner approval, reviewed request, deck v4, mapping v3, path-free manifest, report, and scope routing. Raw procedures, captures, listings, and field values remain external-only.
- Review disposition: PASS.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: the same reviewer who examined the task definition reviews the final candidate, retaining its source/capture context. Its task-definition CONDITIONAL PASS required explicit byte-identical original-output proof and explicit P1-T08/digest inputs; both were added before final evidence execution.

## Files created or changed

- `docs/tasks/XSEC-HOM-01-OWNER-APPROVAL.md` - bounded owner authorization.
- `docs/tasks/XSEC-HOM-01-REQUEST.md` - reviewed one-task execution boundary.
- `docs/spec/xsec-jeff-lattice-deck-v4.md` - exact source-preserving whole-cell route.
- `docs/spec/xsec-source-runtime-mapping-v3.md` - exact named one-mixture selection, without numerical admission.
- `reference/manifests/xsec-hom-01-route-v1.json` - path-free eight-run source-route, structural, and determinism evidence.
- `docs/tasks/XSEC-HOM-01.md` - this report.
- `docs/PROJECT_SCOPE.md` - current XSEC status and successor routing.

All Core/CLI/Unity code, schemas, data packs, raw source files, capture procedures, raw captures, listings, and numerical fields were inspected but not changed in the repository.

## Assumptions and design choices

- The one-mixture output is created by a parallel separately named EDI object; it never replaces or rewrites the authoritative `EDITION` object.
- The day-300 route updates the same named object after the unchanged retained `PRE172` copy and before the unchanged second SPH line. This preserves the one-mixture topology without adding a second region merge.
- Structural success is limited to two groups, one mixture, one volume entry, selector identity, and direct correction-subtree absence. It does not infer physical SPH freedom or use a structural check as field-value authority.
- The historical XSEC-03-R2 report remains immutable. A new task identifier is required for any fresh semantic decode under v4/v3 rather than reopening its blocked ten-mixture execution.

## Validation commands and results

### T0 - static authority and manifest validation

```text
PowerShell parse xsec-hom-01-route-v1.json and assert eight runs, four original baseline matches, same-state whole-cell equality, cross-state distinction, four assertions, ten convergence markers, normal/source completion, zero abnormal diagnostics, two groups, one mixture, one volume, absent direct correction subtree, no decoded values, and no absolute source paths.
git diff --check
```

Result: PASS. The complete source/data-route evidence is path-free and carries only identities, hashes, counts, booleans, and structural metadata. The final static check also proves each state’s original and whole-cell-enabled variants normalize to the same procedure identity when only the final capture target is replaced, and binds every run to the XSEC-03-R2 canonical assertion baseline alongside the additional HOM raw-line projection.

### T1/T6 - fresh DRAGON5 source reproduction and structural capture audit

```text
Eight independent clean external roots: PRE0 original a/b, PRE0 whole-cell a/b, PRE172 original a/b, and PRE172 whole-cell a/b. For each invoke the pinned rdragon procedure with network disabled, read-only root, /tmp and /run tmpfs, and OMP_NUM_THREADS=1.
```

Result: PASS. All eight source runs have four source assertions, normal/source completion, ten source convergence markers, and no abnormal diagnostic. The four unchanged original captures exactly match the respective XSEC-03-R2 baseline hashes. Same-state whole-cell capture pairs are byte-identical and cross-state captures differ. The direct selected MACROLIB structures each have two groups, one mixture, a one-entry volume record, and no direct `SPH`, `SPH-EPSILON`, or `ADF` subtree. Exact artifact/capture/result hashes are in the manifest.

Preliminary disposable external probes that attempted a new day-300 output after the second source EDI were rejected by DRAGON reference-tracking diagnostics. The admitted route instead updates the separately created day-0 object in place; only the final eight roots are evidence.

### T3 - full headless Core/Golden/CLI regression

```text
$env:DOTNET_ROOT='C:\Users\infin\AppData\Local\Temp\candu-dotnet-sdk-10.0.302'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
.\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath C:\Users\infin\AppData\Local\Temp\candu-xsec-hom-01-t3
```

Result: PASS. Core 208/208, Golden 26/26, and CLI 38/38 passed. The first sandboxed invocation could not read the local Windows SDK-discovery folder; the identical offline command then passed with the required local read access.

### Independent code review (high)

```text
Same-reviewer final review of deck v4, mapping v3, manifest, report, and scope against the XSEC-HOM-01 request.
```

Result: PASS. The initial CONDITIONAL PASS required proof that the regression captures use the whole-cell-enabled variants with only their final capture target differing, machine-verifiable one-mixture/day-300 deltas, candidate-pending-review document status, and an explicit per-run XSEC-03-R2 assertion-baseline binding. The same reviewer confirmed all corrections and returned final PASS. Actual model/reasoning telemetry remains UNVERIFIED.

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

Not applicable. This task decoded no field value and changed no equation, unit, normalization, tolerance, runtime behavior, reference baseline, or golden value.

## Deferred validation

- Field decode, units/invariant validation, source-to-Core transformations, pack conversion, Core/CLI/Unity consumption, full-core/DONJON work, plots, PDF, and golden/reference consideration are deferred to later separately authorized tasks.
- T4/T5 do not apply because no runtime or Unity artifact changed.

## Blockers, risks, and follow-up

- Blockers: none within this bounded source-route task.
- Risk triggers: source/output mapping changes triggered T3, T6, and independent high review; all required evidence is recorded above.
- Risks accepted or deferred: a one-mixture structural source object does not prove field admissibility, physical fidelity, license redistribution rights, runtime readiness, or golden authority.
- Follow-up work: define a bounded owner-approved fresh semantic-decode task under deck v4/mapping v3. It must repeat the source route in fresh roots and fail closed before admission on any source, structure, unit, convergence, or field mismatch.

## Next eligible task

None until a separately authorized `XSEC-03-R3` fresh semantic-decode task is defined under deck v4 and mapping v3. `XSEC-03-R2` remains historical blocked evidence and must not be overwritten.

Source: `AGENTS.md`, `XSEC-HOM-01-REQUEST.md`, `XSEC-MIX-01.md`, `XSEC-03-R2.md`, deck v3, mapping v2, and `xsec-hom-01-route-v1.json`.
