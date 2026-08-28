# XSEC-FIELD-01 - required-field source export resolution

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** XSEC-FIELD-01-OWNER-APPROVAL.md, XSEC-RIGHTS-01 internal-use
authority, mapping v1/v3, deck v4, XSEC-HOM-01, and XSEC-03-R3.

## Objective

Resolve the XSEC-03-R3 source-field blocker without changing approved physics.
Using official DRAGON5 source/data-structure guidance, define and prove either:

1. a separate, source-preserving export route that exposes required `EFIS` and
   offline-only `DIFF` for the same named whole-cell, two-state selections; or
2. a fail-closed mapping decision that establishes no approved route exists.

The task may revise only the source-deck and mapping authority needed for this
export boundary. It must not decode or retain numerical field values, create a
candidate coefficient row, or admit runtime/golden data.

## Allowed files and retention

- `docs/tasks/XSEC-FIELD-01*`;
- `docs/spec/xsec-jeff-lattice-deck-v5.md`;
- `docs/spec/xsec-source-runtime-mapping-v4.md`;
- `reference/manifests/xsec-field-01-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

External-only source variants, drivers, captures, listings, parser work files,
raw semantic records, and numerical values must not enter the repository.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope, XSEC-RIGHTS-01,
  mapping v1/v3, deck v4, XSEC-HOM-01, XSEC-03-R3, and all named prerequisite
  source/capture reports; and
- P1-T08 report/digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` as methodology/applicability context only.

## Required behavior and invariants

- Read the relevant official DRAGON5 IGE-335 and IGE-351 guidance before
  choosing an export operation, record exact guide/source locators and hashes,
  and stop if field meaning or units are unresolved.
- Preserve both original `EDITION := EDI` and both original
  `EDITION := SPH: EDITION VOLMATF INTLINF ;` calls byte-for-byte and in their
  original order, as well as all four source assertions and original-capture
  regression baselines. A separate named object/export may never overwrite
  `EDITION`.
- Preserve `WHOLECELL0`, its 31-to-one merge, the day-300 in-place update, the
  two exact selectors, group identity/order, and direct-correction-subtree
  prohibition unless a new reviewed source specification explicitly replaces a
  stated boundary.
- Prove a selected payload contains exactly the required `EFIS` and `DIFF`
  labels with the official meanings and inherited units, or record the exact
  source/guide reason it cannot. `H-FACTOR` is not an admissible substitute
  without a separate explicit mapping decision.
- Run eight fresh isolated external roots: two original-output regression
  captures and two field-export whole-cell captures for each state. The
  original captures must run through the field-export-enabled variants and
  exactly match XSEC-03-R2 baseline hashes; within each state, normalizing only
  the final capture target must make the two variant procedures identical.
  Bind every run to the XSEC-03-R2 assertion baseline, source convergence and
  completion, selector identity, field-label presence, direct-correction
  absence, and deterministic selected-payload evidence. Stop before numerical
  array decoding.
- T0/T1/T2 source/deck/semantic-structure validation, T3 full headless
  Core/Golden/CLI regression, T6 fresh DRAGON reproduction, and independent
  code review (high) are required. Record reviewer telemetry or `UNVERIFIED`.

## Explicit non-goals

- Selecting a substitute field, changing any field transformation/unit/
  normalization, decoding values, calculating differences, or admitting a
  runtime pack, Core/CLI/Unity data, full-core case, golden/reference case, or
  release.

## Definition of done and routing

The task ends only with a reviewed deck v5/mapping v4 field-export route and
fresh source proof, or a precise fail-closed blocker. A successful route makes
a new separately authorized XSEC semantic-decode task eligible; it does not
reopen XSEC-03-R3 or permit its reuse as a candidate row.
