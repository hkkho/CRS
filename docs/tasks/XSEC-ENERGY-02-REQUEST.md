# XSEC-ENERGY-02 - fresh SAPHYB/WCFIELD cross-artifact semantic decode

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-ENERGY-02-OWNER-APPROVAL.md`, `XSEC-RIGHTS-01`,
`XSEC-03-R4`, `XSEC-ENERGY-01`, deck v6, mappings v1/v4/v5, and the pinned
Version5 primary documentation/source.

## Objective

Freshly reproduce deck v6 and externally decode only mapping-v5's approved
WCFIELD/SAPHYB field selections. Prove state/group/mixture/address identity,
the exact fission-energy unit transform, WCFIELD/SAPHYB flux correspondence,
all inherited source invariants, and deterministic candidate semantic outcome.
Admit or reject only an offline candidate semantic result; retain no numerical
source or derived value in the repository.

## Allowed files and retention

- `docs/tasks/XSEC-ENERGY-02*`;
- `reference/manifests/xsec-energy-02-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

External-only source variants, decoder scripts, listings, captures, arrays,
values, derived values, and diagnostic tables must not enter the repository.
No specification, deck, mapping, runtime schema/pack, Core/CLI/Unity code,
DONJON/full-core artifact, golden/reference baseline, or release artifact may
change.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current scope,
  `XSEC-RIGHTS-01`, `XSEC-03-R4`, `XSEC-ENERGY-01`, deck v6, mappings
  v1/v4/v5, and their reports/manifests; and
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03` as methodology/applicability context only.

## Required behavior and fail-closed checks

- Run fresh isolated deck-v6 roots sufficient to retain both WCFIELD and
  SAPHYB terminal captures. The source inputs may differ only in the final
  external capture target. Preserve three original EDI lexemes, both SPH
  calls, four assertions, `WHOLECELL0`, GOLVER, geometry/materials/group
  boundary/source controls, and SAPHYB initialization/recoveries.
- Decode the two mapped WCFIELD states and the matching two SAPHYB elementary
  calculations. Require exactly two ordered groups, one mixture, one volume,
  a unique `ENERGIE F.` reaction label/address, a unique WCFIELD `NFTOT`, and
  the documented source units. Reject direct correction, `H-FACTOR`,
  `PRODUCTION`, gamma/capture, leakage, missing/duplicate records, NaN/Inf,
  or any identity mismatch.
- Decode mapping-v1 fields from WCFIELD and SAPHYB `ENERGIE F.`/`FLUXS` only.
  Verify all inherited mapping-v1/v4 invariants and unit conversions
  externally. No value, difference, tolerance, candidate row, or compact data
  pack may be committed.
- To avoid inventing a numerical tolerance, prove flux correspondence only by
  elementwise identical floating-point bit patterns between the matched
  WCFIELD `FLUX-INTG` and SAPHYB `FLUXS` vectors. Any difference blocks the
  candidate.
- Use the exact mapping-v5 factor of one million electron-volts per
  mega-electron-volt for SAPHYB `ENERGIE F.` before the inherited
  energy-per-fission ratio. Require finite positive denominator/result and
  preserve all inherited rejection rules.
- Compare duplicate fresh captures/semantic fingerprints for determinism;
  retain only hashes and boolean outcomes. A host diagnostic may be classified
  as baseline-only only if it recurs unchanged in a fresh untouched control
  that reproduces its prior capture.

## Validation and routing

Run T0/T1/T2 focused decoder/manifest checks and T6 fresh source reproduction.
Per the owner’s XSEC-phase sequencing direction, defer T3 full regression and
independent code review (high) together to XSEC phase closeout. This task does
not authorize runtime, golden, DONJON, full-core, or Unity admission.
