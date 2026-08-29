# XSEC-03-R4 - fresh GOLVER whole-cell semantic decode

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-03-R4-OWNER-APPROVAL.md`, `XSEC-RIGHTS-01`,
`XSEC-DIFF-01`, deck v5, mapping v1/v3/v4, and `XSEC-HOM-01`.

## Objective

Freshly reproduce the approved deck-v5/mapping-v4 `WCFIELD` route, then decode
only `WCFIELD/REF-CASE0001/MACROLIB` at day 0 and
`WCFIELD/REF-CASE0002/MACROLIB` at day 300. Prove the inherited mapping-v1
field identities, units, dimensions, source invariants, deterministic
transformations, and two-knot ordering without retaining a numerical source or
derived value in the repository.

## Allowed files and retention

- `docs/tasks/XSEC-03-R4*`;
- `reference/manifests/xsec-03-r4-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

The decoder/validator, source variants, input decks, raw semantic records,
captures, listings, numerical arrays, values, and derived rows remain external
only. No specification, source deck, runtime schema, Core/CLI/Unity code, or
data pack may change.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope, `XSEC-RIGHTS-01`,
  `XSEC-DIFF-01`, mapping v1/v3/v4, deck v5, `XSEC-HOM-01`, and all named
  predecessor XSEC reports/manifests; and
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` as methodology/applicability context only.

## Required behavior and fail-closed checks

- Run eight fresh isolated roots: PRE0/PRE172 original-output regression a/b
  and GOLVER-field output a/b. Preserve all three original EDI source
  lexemes, both original SPH calls, all four source assertions, `WHOLECELL0`,
  and capture-target-only variation. Original captures must exactly match
  XSEC-03-R2; field captures must repeat byte-identically per state and differ
  across states.
- Bind every root to the pinned source/image/library identities, source
  assertion baseline, convergence/completion, normal end, abnormal-diagnostic
  check, GOLVER activation, and pre-array direct-subtree check. Reject any
  `REAC DIFF`/`H-FACTOR` substitution, leakage route, direct `SPH`,
  `SPH-EPSILON`, or `ADF` field payload subtree.
- Decode only mapping-v1 required records: `ENERGY`, `NTOT0`, `SIGS00`,
  compressed `SCAT00` plus `IJJS00`/`NJJS00`/`IPOS00`, `NFTOT`, `NUSIGF`,
  `CHI`, `EFIS`, `FLUX-INTG`, and offline-only GOLVER `DIFF`. Prove exactly
  two ordered groups, one mixture, one volume, `Nf=1`, field meanings/units,
  finite values, zero upscatter, nonnegative fields, absorption at least
  fission, matching fission/nu-fission support, CHI conservation/range,
  positive energy-per-fission denominator/result, and strictly increasing
  converted burnup knots. Do not infer, clamp, repair, smooth, normalize, or
  retain a failed value.
- Keep only path-free hashes/fingerprints, record names/units/dimensions,
  boolean invariant outcomes, and per-observable repeat-equality results. No
  numerical value, difference, tolerance, or candidate row may enter the
  repository.
- Run focused T0/T1/T2 parser/semantic/determinism checks and T6 fresh source
  reproduction. Per the 2026-08-29 owner clarification, defer the full
  Core/Golden/CLI regression and independent code review (high) to the XSEC
  phase-closeout gate, where both are required together. Mark unavailable
  model/effort telemetry `UNVERIFIED`.

## Non-goals and routing

No runtime pack, Core/CLI/Unity consumption, DONJON/full-core case,
golden/reference decision, or release claim is authorized. On final PASS,
`XSEC-03-R4` may establish only an offline candidate semantic result; a later
separately authorized task owns pack/admission work. Stop BLOCKED on a missing
field, source/mapping mismatch, unit/dimension ambiguity, nonconvergence,
NaN/Inf, nondeterminism, failed invariant, or regression failure.
