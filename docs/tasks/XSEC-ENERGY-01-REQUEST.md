# XSEC-ENERGY-01 - SAPHYB fission-energy export route

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-ENERGY-01-OWNER-APPROVAL.md`, `XSEC-RIGHTS-01`,
`XSEC-EFIS-01`, `XSEC-DIFF-01`, deck v5, mappings v1/v4, and `XSEC-03-R4`.

## Objective

Prove and specify, or reject, a source-preserving DRAGON5 SAP/SAPHYB artifact
route which carries the exact mapping-required fission-only energy-production
quantity for the existing two-state, two-group, one-mixture whole-cell source
route. The task must prove identity, unit, normalization, group/mixture/state
mapping, source preservation, and deterministic structural export before it
may create a deck v6/mapping v5. It must not infer an equivalence between the
new SAPHYB artifact and the rejected EDI WCFIELD MACROLIB record.

## Allowed files and retention

- `docs/tasks/XSEC-ENERGY-01*`;
- `docs/spec/xsec-saphyb-efis-route-v1.md`;
- `docs/spec/xsec-jeff-lattice-deck-v6.md` and
  `docs/spec/xsec-source-runtime-mapping-v5.md` only if the exact route passes;
- `reference/manifests/xsec-energy-01-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

External-only source variants, drivers, captures, listings, raw SAPHYB/LCM
records, numeric arrays, numeric values, and derived values must not enter the
repository. No runtime, pack, Core/CLI/Unity, DONJON/full-core, golden, or
release artifact may change.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope, `XSEC-RIGHTS-01`,
  `XSEC-EFIS-01`, `XSEC-DIFF-01`, `XSEC-03-R4`, deck v5, mappings v1/v4, and
  pinned official Version5 documentation/source; and
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` as methodology/applicability context only.

## Required behavior and fail-closed checks

- Bind SAP's `EFIS` reaction definition, SAPHYB writer/reader path, and the
  exact source branch that constructs it to hash-identified primary artifacts.
  Prove that its fission contribution is `MEVF * NFTOT` and whether it has any
  capture/gamma contribution. Bind the `MEVF` unit from primary evidence; do
  not infer it from a label or value.
- Prove that SAPHYB initialization and two recovery calls can be attached as a
  separate named path while retaining the original three EDI lexemes, both SPH
  calls, all four assertions, `WHOLECELL0`, GOLVER `DIFF`, geometry, materials,
  group boundary, source controls, and existing capture route unchanged.
- The SAPHYB exporter must receive the named one-mixture WCFIELD EDI output,
  not the historical ten-mixture output, a direct-correction artifact, an
  altered deck, a reconstructed value, `PRODUCTION`, or `H-FACTOR`.
- If primary proof establishes a unit conversion, specify it exactly and prove
  its dimensional correctness. The active mappings may be superseded only by a
  successful v5 document that preserves all unrelated approved decisions and
  makes no numeric candidate admission.
- Only after static proof identifies exact syntax may a focused external probe
  run. It must show normal completion, all four original assertions, unchanged
  original-control capture, deterministic same-state SAPHYB structural output,
  distinct states, exact two-group/one-mixture selection, fission-only SAP
  field identity, no direct correction use, and no numerical-array/value read.
- On any unsupported syntax, unavailable `MEVF`, field/unit/normalization
  mismatch, source-preservation failure, nondeterminism, absent output, or
  direct correction, stop BLOCKED without changing deck/mapping. Do not decode
  numeric values or create a substitute field.

## Validation and routing

- T0/T1/T2: task document, primary-source, manifest, and focused structural
  checks; T6 only if a source probe runs.
- Per the owner's XSEC-phase sequencing direction, T3 full regression and
  independent code review (high) are deferred together to XSEC phase closeout.

No numerical semantic decode, transformation, pack, Core/CLI/Unity consumption,
DONJON/full-core case, golden/reference decision, or release claim is
authorized. A success routes only to a separately authorized fresh
cross-artifact semantic-decode/admission task; it does not reopen `XSEC-03-R4`.
