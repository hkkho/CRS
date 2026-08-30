# XSEC-EFIS-01 - source-preserving EFIS export-identity specification

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-EFIS-01-OWNER-APPROVAL.md`, `XSEC-RIGHTS-01`,
`XSEC-03-R4`, deck v5, mappings v1/v3/v4, and `XSEC-DIFF-01`.

## Objective

Resolve the `XSEC-03-R4-MISSING-EFIS` blocker by proving and specifying, or
rejecting, an official DRAGON5 source-preserving route that makes the exact
mapping-v1 fission-only energy-production quantity available in the selected
two-state `WCFIELD` MACROLIB payload. The task must distinguish the documented
`EFIS` reaction identity from output-selection syntax, `PRODUCTION`, and
`H-FACTOR`; it may not infer equivalence from a label or a numerical comparison.

## Allowed files and retention

- `docs/tasks/XSEC-EFIS-01*`;
- `docs/spec/xsec-efis-export-v1.md`;
- `docs/spec/xsec-jeff-lattice-deck-v6.md` and
  `docs/spec/xsec-source-runtime-mapping-v5.md` only if the exact route and
  field identity are proven;
- `reference/manifests/xsec-efis-01-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

External-only source variants, drivers, captures, listings, raw semantic
records, numeric arrays, numeric values, and derived values must not enter the
repository. No runtime, pack, Core/CLI/Unity, DONJON/full-core, golden, or
release artifact may change.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope,
  `XSEC-RIGHTS-01`, `XSEC-03-R3`, `XSEC-FIELD-01`, `XSEC-DIFF-01`,
  `XSEC-03-R4`, deck v5, mappings v1/v3/v4, and the pinned official Version5
  documentation/source; and
- P1-T08 rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and
  `S6-R03` as methodology/applicability context only.

## Required behavior and fail-closed checks

- Bind the official `EFIS` reaction definition, EDI parser/selection behavior,
  and the writer path that creates the selected payload record to
  hash-identified primary artifacts. State precisely whether `REAC n ...`
  controls MICROLIB output selection, MACROLIB record creation, or both.
- Investigate only source-preserving candidate routes that leave the three
  original EDI lexemes, both original SPH calls, all four assertions,
  `WHOLECELL0`, GOLVER `DIFF` route, geometry, materials, group structure,
  source controls, and capture interface unchanged. A candidate may add a
  separate named output object only when the official grammar/source writer
  proves its semantics.
- Reject `PRODUCTION` unless official primary evidence proves it is exactly the
  required fission-only energy-production cross section with unit
  `eV cm^-1`, flux-weighting/normalization, and groupwise MACROLIB identity.
  Reject `H-FACTOR`, `REAC DIFF`, leakage coefficients, reconstructed formulas,
  and numeric-equivalence arguments as substitutes.
- Run focused external source probes only after the static proof identifies an
  exact candidate syntax. For any successful candidate, prove record/edit
  label presence, field unit/meaning, exact two-group/one-mixture/one-volume
  selection, no direct correction subtree, source completion/convergence,
  determinism, and unchanged original regression capture. Do not read or
  retain numerical arrays or values.
- On proof, create deck v6 and mapping v5 that make no numerical admission;
  retain only path-free hashes, source locators, syntax fingerprints, names,
  units, dimensions, and boolean observations. On any unresolved identity,
  unit, normalization, syntax, source-preservation, or determinism issue,
  stop BLOCKED without changing deck/mapping.
- Run focused T0/T1/T2 and T6 only if a source probe runs. Per owner
  sequencing direction, full cross-project regression and independent review
  are deferred together to XSEC phase closeout.

## Non-goals and routing

No numerical semantic decode, mapping transformation, interpolation,
tolerance, data pack, Core/CLI/Unity consumption, DONJON/full-core case,
golden/reference decision, or release claim is authorized. A successful
specification routes only to a separately authorized fresh semantic-decode task;
it does not reopen `XSEC-03-R4` or admit its historical payload.
