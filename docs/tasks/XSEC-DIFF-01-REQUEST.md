# XSEC-DIFF-01 - source diffusion-coefficient route decision

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-DIFF-01-OWNER-APPROVAL.md`, `XSEC-RIGHTS-01`,
`XSEC-FIELD-01`, mapping v1/v3, deck v4, `XSEC-HOM-01`, and `XSEC-03-R3`.

## Objective

Resolve the `XSEC-FIELD-01` diffusion-coefficient blocker without reusing the
semantically incompatible `REAC DIFF` extra edit. Select and prove, or reject,
one documented DRAGON5 source-preserving route that writes the inherited
mapping-v1 `DIFF[g]` isotropic diffusion coefficient in centimetres alongside
the required fission-only `EFIS` extra edit for the existing named whole-cell
two-state selections.

The candidate route is the official EDI `GOLVER` option: it writes the
Golfier-Vergain diffusion coefficient, not a leakage coefficient and not an
extra-edit reaction. Its documented equation, field meaning, unit, and source
scope must be bound before fresh validation runs. The task may create deck v5
and mapping v4 only if the fresh proof succeeds; it does not decode any
numeric array or admit a coefficient row.

## Allowed files and retention

- `docs/tasks/XSEC-DIFF-01*`;
- `docs/spec/xsec-jeff-lattice-deck-v5.md`;
- `docs/spec/xsec-source-runtime-mapping-v4.md`;
- `reference/manifests/xsec-diff-01-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

External-only source variants, drivers, captures, listings, raw semantic
records, numeric arrays, and numeric values must not enter the repository.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope, `XSEC-RIGHTS-01`,
  mapping v1/v3, deck v4, `XSEC-HOM-01`, `XSEC-03-R3`, and
  `XSEC-FIELD-01`; and
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` as methodology/applicability context only.

## Required behavior and invariants

- Bind the exact official IGE-335 `GOLVER` equation and the IGE-351 `DIFF`
  field meaning/unit to hash-identified source files. Record the source writer
  branch that creates `DIFF`; do not infer it from the shared label.
- Preserve all three original `EDITION := EDI` source lexemes, both original
  `SPH` calls,
  all four assertions, the separate `WHOLECELL0` 31-to-one merge, and its
  day-300 in-place update byte-for-byte and in order. A new `WCFIELD` output
  must never overwrite `EDITION`.
- The sole new field route is a separate all-one `WCFIELD` EDI creation with
  `COND 4.0 MICR ALL REAC 1 EFIS GOLVER SAVE`, followed at day 300 by its
  explicit `GOLVER` in-place update. `REAC DIFF`, `H-FACTOR` as a selected or
  decoded substitute, leakage-mode diffusion, an invented formula, and every
  direct-correction source subtree are prohibited. A source-emitted
  `H-FACTOR` label may remain in a selected MACROLIB payload and is ignored;
  its presence is not a selected-field claim.
- Run eight fresh isolated external roots: original-output regression a/b and
  GOLVER-field output a/b for both day-0 and day-300 states. Every root must
  bind four source assertions, source convergence/completion, normal end,
  absent abnormal diagnostics, the XSEC-03-R2 original-capture baseline,
  capture target, and pre-array checks. Original captures must record their
  regression-control/direct-correction-check status and match their baseline;
  field captures must prove direct-correction absence, be deterministic per
  state, and be distinct across states. Stop before numeric array decoding.
- T0/T1/T2 route/manifest checks, T3 full Core/Golden/CLI, T6 fresh source
  reproduction, and independent code review (high) are required. Actual
  reviewer telemetry must be verified or marked `UNVERIFIED`.

## Explicit non-goals and routing

No source-array decoding, transformation, tolerance, data pack, runtime,
DONJON, full-core, Unity, golden/reference, or release claim is authorized.
On a final independent-review PASS, route only to the separately authorized
`XSEC-03-R4` semantic-decode task; do not reopen `XSEC-03-R3` or
`XSEC-FIELD-01`. On any semantic, unit, assertion, convergence, determinism,
or regression failure, stop BLOCKED and preserve the evidence.
