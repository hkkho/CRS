# XSEC source-to-runtime mapping v5 - SAPHYB fission-energy companion

**Status:** approved for one later bounded offline cross-artifact semantic
decode task when used with deck v6. This supersedes mapping v4 only for the
fission-only energy source. It creates no runtime pack, Core/CLI/Unity
integration, full-core case, golden/reference authority, release, or Unity
admission.

## Inherited mapping

All field identities, group order, units, transformations, invariants,
interpolation, exclusions, and fail-closed behavior in mappings v1/v4 remain
unchanged except for the source artifact of `EFIS`. WCFIELD remains the source
of `NFTOT` and every unchanged mapping-v4 field, including the offline-only
GOLVER `DIFF` coefficient. `H-FACTOR`, `PRODUCTION`, a leakage coefficient,
and reconstructed or inferred quantities remain forbidden.

## Resolved cross-artifact EFIS source

At each inherited source state, select the matching deck-v6 SAPHYB elementary
calculation and its sole mixture. The only accepted reaction label is
`ENERGIE F.` in `contenu/NOMREA`; use its documented `ADRX` location in the
mixture `RDATAX` store. It is SAP's fission-only `EFIS` export, constructed as
`MEVF * NFTOT`, with unit `MeV cm^-1`.

Convert each selected group exactly to the mapping-v1 required unit:

```text
EFIS[g] = SAPHYB_ENERGIE_F[g] * 1,000,000 eV / MeV
```

Use the matching SAPHYB `FLUXS[g]` only after proving it is the required
volume- and energy-integrated flux correspondence for the matching WCFIELD
state, mixture, and group. The inherited energy-per-fission ratio then uses
the converted SAPHYB EFIS and matched SAPHYB flux in its numerator, and the
unchanged WCFIELD `NFTOT` with that same proven flux in its denominator.

The successor must reject a missing/duplicate selector, missing address,
non-two-group/non-one-mixture structure, missing flux, non-finite value,
unit/normalization ambiguity, cross-artifact state/group/mixture mismatch, or
failed flux correspondence. It must not transform, interpolate, smooth,
repair, normalize, substitute, or retain a failed candidate value.

## Required successor gate

`XSEC-ENERGY-02` must be separately owner-approved. It owns a fresh deck-v6
rerun, a read-only cross-artifact semantic decoder, exact source and companion
structure checks, value-level correspondence/invariants, and the candidate
admission or rejection decision. It must retain no source or derived numerical
value in the repository and must not claim runtime, golden, DONJON, or Unity
admission.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, as methodology/applicability context only.
