# XSEC source-to-runtime mapping v4 - GOLVER diffusion-coefficient identity

**Status:** approved for one later bounded offline semantic-decode task when
used with deck v5. This supersedes mapping v3 only for the required-field
selection below. It creates no runtime pack, Core/CLI/Unity integration,
full-core case, golden/reference authority, release, or Unity admission.

## Inherited mapping

All candidate identity, two-state selection, group order, units, field
transformations, invariants, interpolation, fail-closed behavior, and
exclusions in mapping v1 and mapping v3 remain unchanged. In particular,
`EFIS` remains the fission-only energy-production input to the inherited
offline energy-per-fission ratio, and `DIFF[g]` remains an offline
DONJON/reference-only isotropic diffusion coefficient in centimetres. Neither
is a runtime Core material, topology, volume, or conductance input.

## Resolved source field identity

The eligible selectors are deck v5's `WCFIELD/REF-CASE0001/MACROLIB` at day 0
and `WCFIELD/REF-CASE0002/MACROLIB` at day 300. The required coefficient is
the standard EDI `DIFF` record written because `GOLVER` selects the official
Golfier-Vergain formula:

```text
D[i,g] = alpha[g] / (3 * Sigma_tr[i,g])
```

with `alpha[g]` and `Sigma_tr[i,g]` exactly as defined by IGE-335. The source
field is the IGE-351 isotropic diffusion coefficient in centimetres. Its
identity is proven by the documented EDI writer branch, not by its name alone.
The earlier `REAC DIFF` extra edit remains a forbidden Legendre-order
scattering quantity. `H-FACTOR` remains forbidden as a selected or decoded
substitute even if the source payload carries that unrelated record; a leakage
coefficient, reconstructed transport formula, and inferred coefficient remain
forbidden.

The v5 route deliberately selects the documented formula for the existing
no-leakage source case. It does not claim equivalence to leakage-weighted
coefficients or authorize comparison, substitution, calibration, tolerance, or
golden use across coefficient-generation models.

## Required successor gate

`XSEC-03-R4`, a separately owner-approved semantic-decode task, must rerun the
exact deck-v5 route in fresh roots, repeat source regression and field-identity
checks, then decode only mapping-v1's required fields and run its complete
validation/review gate. It must retain no source or derived numeric value in
the repository and must not reuse `XSEC-03-R3` or `XSEC-FIELD-01` payloads as
candidate data.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, as methodology/applicability context only.
