# XSEC-03-R3 - fresh whole-cell DRAGON semantic decode

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** XSEC-03-R3-OWNER-APPROVAL.md, XSEC-RIGHTS-01 internal-use
authority, `xsec-jeff-lattice-deck-v4.md`,
`xsec-source-runtime-mapping-v3.md`, and XSEC-HOM-01.

## Objective

Freshly reproduce the approved v4/v3 separately named whole-cell source route,
then decode and validate the two approved direct MACROLIB selections at elapsed
days 0 and 300. Record only path-free provenance, semantic fingerprints,
field/unit/invariant outcomes, and deterministic transformation outcomes. This
task may establish an offline candidate semantic result only; it cannot admit a
runtime pack, reference/golden data, full-core case, or Unity data.

## Allowed files and retention

- `docs/tasks/XSEC-03-R3*`;
- `reference/manifests/xsec-03-r3-*.json`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

The decoder/validator, source variants, input decks, captures, listings, raw
semantic records, numerical field values, and derived values remain external
only. No source/mapping specification, runtime schema, Core/CLI/Unity code, or
data pack may change.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, current `PROJECT_SCOPE.md`,
  XSEC-RIGHTS-01, mapping v1/v3, deck v4, and XSEC-HOM-01 evidence;
- XSEC-RUNNER-01, XSEC-SPH-03, XSEC-MAP-R2, XSEC-03-R2, XSEC-MIX-01, and
  P1-T08 reports plus their relevant path-free manifests; and
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03`, as methodology/applicability context only.

## Required behavior and fail-closed checks

- Run eight fresh isolated roots: original-output a/b and whole-cell a/b for
  each state. The original captures must exactly match XSEC-03-R2 baseline
  hashes; each same-state whole-cell pair must be byte-identical and the two
  states distinct. Preserve both original SPH transitions and four source
  assertions.
- Hash all source, library, driver, capture-interface, variant, tool, image,
  and run identities. Record source completion, convergence/iteration evidence,
  abnormal-diagnostic detection, and the XSEC-03-R2 canonical assertion-baseline
  binding for every run.
- Decode only `WHOLECELL0/REF-CASE0001/MACROLIB` at day 0 and
  `WHOLECELL0/REF-CASE0002/MACROLIB` at day 300. Reject every other source
  state, capture, direct correction subtree, group identity/order mismatch,
  field-name/unit/dimension ambiguity, and `Nf != 1`.
- Apply only mapping v1’s inherited required fields and transformations:
  `ENERGY`, `NTOT0`, `SIGS00`, compressed `SCAT00` with `IJJS00`/`NJJS00`/
  `IPOS00`, `NFTOT`, `NUSIGF`, `CHI`, `EFIS`, `FLUX-INTG`, and offline-only
  `DIFF`. Prove all stated finite, support, conservation, zero-upscatter,
  nonnegative, absorption/fission, energy-ratio, and strictly increasing knot
  invariants. Do not invent a tolerance, repair a value, or infer a missing
  field.
- Retain no numerical value. Record deterministic source-semantic and
  transformation fingerprints, counts, units, provenance, boolean invariant
  outcomes, and per-observable repeat-equality pass/fail only.
- T0/T1/T2 semantic-parser and determinism checks, T3 full headless
  Core/Golden/CLI regression, T6 fresh DRAGON reproductions, and independent
  code review (high) are required. Reviewer telemetry must be verified or
  marked `UNVERIFIED`.

## Explicit non-goals

- Changing any equation, unit, group, normalization, convergence rule,
  tolerance, source behavior, schema, runtime code, or game behavior.
- Keeping numerical field values in the repository or claiming licensed
  redistribution, runtime, golden/reference, full-core, DONJON, or Unity
  admission.

## Definition of done

Success requires the fresh eight-root source proof, deterministic semantic
decode/transform fingerprints, all approved mapping invariants, T3/T6 passes,
and final independent high-review PASS. A successful result is still only an
offline candidate. Update scope after the report and stop; a later task must
own any candidate pack, Core/CLI/Unity, full-core, or golden decision.
