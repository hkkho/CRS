# XSEC source-to-runtime mapping v3 - named whole-cell two-knot candidate

**Status:** approved for a later bounded offline semantic-decode task. This
specification supersedes mapping v2's source selection when used with
`xsec-jeff-lattice-deck-v4.md`. It creates no pack, Core/CLI/Unity change,
full-core mapping, golden/reference authority, release, or Unity use.

## Resolution and inherited authority

`XSEC-MIX-01` proved that the original TCWUX11 EDI output has ten mixtures and
no source-named whole-cell record. `XSEC-HOM-01` then proved a separate,
source-preserving EDI route that explicitly creates one named output mixture
without changing the original EDI/SPH path, source assertions, source controls,
geometry, materials, 4 eV split, or group structure.

All field meanings, units, transformations, invariants, exclusions,
rejection rules, two-knot conversion, interpolation contract, and provenance
requirements in mapping v1 are incorporated unchanged except for the
selection below. In particular, no interpolation, tolerance, normalization,
or numerical field rule changes here.

Candidate ID: `xsec-c6-37-nu-jeff31-4ev-v4-sph-preserved-wholecell-2k`.

## Exact source selection

| Candidate elapsed day | Named retained object | Exact selector | Structural requirement |
| ---: | --- | --- | --- |
| 0 | `WHOLECELL0` | `REF-CASE0001/MACROLIB` | Exactly two groups, exactly one MACROLIB mixture, one-entry volume record. |
| 300 | `WHOLECELL0` | `REF-CASE0002/MACROLIB` | Exactly two groups, exactly one MACROLIB mixture, one-entry volume record, and source timestamp elapsed day 300. |

The single MACROLIB mixture is the only mixture in the separately named
whole-cell source object; it is not selected by ordinal convention, region
order, volume weighting, or inferred material interpretation. The source
object identity and one-mixture proof are required before a later decoder may
read a field.

Each selected direct MACROLIB subtree must have no `SPH`, `SPH-EPSILON`, or
`ADF` record/subtree. SPH-related source history elsewhere is provenance only;
the selected coefficients must not be called physically SPH-free. `PRE0` and
`PRE172` are source-regression controls, never data selectors under v3.

## Unchanged conversion and exclusions

The only ordered source elapsed-day keys are `0` and `300`; their inherited
conversion remains:

```text
B[J/kg_HM] = 31.9713 * 1000 * elapsed_days * 86400
```

The later converter must retain mapping v1's required fields,
`cm^-1`-to-`m^-1` factor, eV-to-joule factor, fission-energy ratio,
scattering/CHI derivations, finite/nonnegative/fission-support checks, and
closed-interval interpolation/rejection behavior. No decode, transformation,
or resulting value is admitted by this specification.

`DIFF`, topology, node volume, conductance, boundary conductance, power
normalization, solver tolerance, raw data, direct correction quantities,
full-core mapping, runtime schema, Core/CLI/Unity integration, and all
golden/reference claims remain excluded.

## Required next-task gate

A new separately authorized semantic-decode task must rerun the exact v4
source route in fresh roots, repeat the source-regression and structural
checks, then and only then decode the approved required fields and execute its
full validation/review gate. It must not reuse XSEC-03-R2's blocked
ten-mixture result as a numerical candidate.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, as methodology/applicability context only.
