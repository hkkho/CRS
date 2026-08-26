# XSEC source-to-runtime mapping v1

**Status:** approved for the XSEC candidate offline pipeline only. This is an
XSEC-02 technical specification under the owner authority recorded in
`XSEC-RIGHTS-01-OWNER-APPROVAL.md`. It creates neither an approved runtime
pack nor golden/reference approval.

## Purpose and boundary

This specification fixes the single DRAGON5 lattice source case, two-group
ordering, field meanings, unit transformations, burnup knots, and fail-closed
projection to the existing `BurnupCoefficientTableV1` contract. It implements
no equations or runtime code and changes no existing Core, topology,
normalization, tolerance, or public serialization contract.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They establish CANDU/DRAGON methodology
context only. The exact field names and units below are bound to the official
Version5 user and data-structure guides, not inferred from paper figures.

The project-owner attestation authorizes offline internal use of the JEFF/JEF
source route. Raw library files, converted WIMSD binaries, reference-tool
executables, input listings, and LCM dumps remain external. This specification
does not authorize their repository retention, game distribution, publication,
release, or store delivery. A derived compact pack remains an internal
candidate until the later XSEC tasks and G4-R7 admit it.

## Exact candidate lattice case

`xsec-c6-37-nu-jeff31-4ev-v1` is a project-authored rerun case based on the
official DRAGON5 `TCWU11` CANDU-6-type annular-cell procedure, not a named
station model and not a claim about a real unit's operating state. Its exact
source basis is Version5 commit `eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`:

| Role | Relative source path | Canonical-LF SHA-256 |
| --- | --- | --- |
| Lattice/depletion procedure | `Dragon/data/twlup_proc/TCWU11.c2m` | `c9b6e04867eaf2d3ba17e6b6af37b39a7d14042a914f89127bca749e3c22190d` |
| JEFF-3.1 WIMSD4 library procedure | `Dragon/data/tjeff31gx_proc/TCWU05Lib.c2m` | `896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040` |

XSEC-03 must preserve every adopted source lexeme in its external input/deck
record and record the complete project-deck SHA-256. The only allowed deck
deltas are: (1) resolve the source procedure's `TCWU05Lib` import to the
pinned JEFF-3.1 procedure above instead of the WLUP `iaea` procedure; (2) omit
exactly the two source lines `EDITION := SPH: EDITION VOLMATF INTLINF ;`, one
after each source `EDI` call; and (3) add a non-mutating external ASCII
serialization/export stage that reads the post-`EDI`, pre-`SPH` `EDITION`
object and emits only the required semantic records. No source procedure line
may otherwise be edited, reordered, or supplemented. In particular, this
specification authorizes no explicit `SHI` iteration/tolerance controls and no
`FLU` convergence controls; the TCWU11 `SHI ... NOLJ` and `FLU ... TYPE K`
lexemes remain unchanged. Any material, geometry, tracking, depletion,
collapse, solver-control, or export-semantics change requires a new mapping
specification.

The adopted geometry is the source procedure's 37-element natural-uranium
bundle arrangement (rings `1 + 6 + 12 + 18`) in the CANDU-6-type annular
cell. It retains its explicit fuel, cladding, coolant, pressure-tube,
calandria-tube, and moderator regions; reflective radial boundary; source
temperatures/densities/isotopics; and source dimensions. Its self-shielding
geometry uses 13 regions and its transport geometry 31 regions. The source
procedure's documented nominal fuel facts are: natural enrichment 0.71140,
UO2 real density 10.59300 g/cm3, effective density 10.43750 g/cm3, fuel
temperature 941.28998 K, coolant D2 atom percentage 99.222, moderator D2 atom
percentage 99.911, uranium mass 19.23600 kg, and 615.00000 kW bundle power.
These are source-deck facts for reproduction, not game parameters.

The source library is the exact JEFF-3.1/XMAS-172 WIMSD4 binary produced by
the official WLUP/WILLIE route already identity-recorded by XSEC-01. XSEC-03
must repeat the archive/library/converter/binary SHA-256 checks and retain
their identities in a path-free run manifest. A substituted JEFF/JEF release,
group structure, library byte sequence, isotope label, or source route is a
different case and must fail closed.

The source procedure uses no transport leakage for this candidate. It uses
EXCELT tracking, generalized Stamm'ler self-shielding, and exactly the source
`SHI: ... NOLJ` and `FLU: ... TYPE K` controls. XSEC-03 must record the
actual source-emitted convergence/iteration evidence and stop on a
nonconverged or abnormal run; source default controls are not reinterpreted as
project acceptance tolerances. The exported object is explicitly the
post-`EDI`, pre-`SPH` `EDITION` object at each knot. The two `SPH` calls are
omitted by the tightly bounded deck delta above because the existing Core
contract has no SPH/ADF field or mapping authority. SPH, ADF, and
SPH-corrected macrolib data are consequently prohibited from this candidate.

The exported energy structure must contain exactly two coarse groups created
by the documented `EDI: ... COND 4.0` eV split. The source `ENERGY` record
must be strictly decreasing, identify a high-energy group followed by a
low-energy group, and contain the 4.0 eV split as rendered by the source
export. The target mapping is therefore explicit:

| Runtime group | Source group | Meaning |
| --- | --- | --- |
| `1` | `GROUP[1]` above the 4.0 eV split | fast |
| `2` | `GROUP[2]` below the 4.0 eV split | thermal |

The converter must reject anything other than exactly two ordered groups; it
must not use a microgroup ordinal, array position, or a nearest-boundary guess
as a substitute for the exported `ENERGY` identity.

## Depletion states and scope

The exact candidate knots are source elapsed days `0, 1, 5, 10, 50, 150, 300`
at the documented constant specific power `31.9713 kW/kg_HM`. They are ordered
by increasing elapsed days and become the table knots through:

```text
B[J/kg_HM] = 31.9713 * 1000 * elapsed_days * 86400
```

The expression is a unit conversion from the source's kW/kg and days to SI;
it is not an interpolation, fit, or new burnup model. Source-emitted burnup may
also be retained as an audit value only when it agrees with this expression to
the exact source/deck calculation semantics recorded by XSEC-03.

`0` is the fresh candidate state. The later knots are depletion-history
candidates only. This specification does not call any knot equilibrium,
refuelled, control, poisoned, production, authoritative, or golden. The source
case contains no on-power refuelling transition, device state, poison
perturbation, or full-core state; XSEC-03 must not fabricate those variants.

## Required source semantic export

For every knot, XSEC-03 must export an ASCII LCM-derived semantic record for
the one explicitly named homogenized whole-cell mixture. The record must carry
the source lexeme, mixture identity, group `ENERGY` boundaries, physical units,
and the following source quantities. An ambiguous, missing, duplicate, or
dimensionally inconsistent record fails closed.

| Source quantity | Official meaning | Required use |
| --- | --- | --- |
| `NTOT0[g]` | flux-weighted total macroscopic cross section, cm^-1 | absorption derivation |
| `SIGS00[g]` | total P0 scattering out of group `g`, cm^-1 | absorption derivation |
| decoded `SCAT00[1->2]` | P0 transfer from primary group 1 to secondary group 2, cm^-1 | downscatter |
| decoded `SCAT00[2->1]` | P0 transfer from primary group 2 to secondary group 1, cm^-1 | zero-upscatter proof |
| `NFTOT[g]` | macroscopic fission cross section, cm^-1 | fission |
| `NUSIGF[g,1]` | steady-state nu-fission cross section, cm^-1 | nu-fission |
| `CHI[g,1]` | steady-state fission spectrum | chi |
| `EFIS[g]` | fission-only energy-production cross section, eV cm^-1 | energy per fission |
| `FLUX-INTG[g]` | volume-integrated flux, cm s^-1 | energy-ratio weighting only |
| `DIFF[g]` | isotropic diffusion coefficient, cm | offline DONJON/reference evidence only |

`NFTOT` and `EFIS` must be requested explicitly as fission and fission-energy
reactions in the source export. `NUSIGF` and `CHI` are admissible only when the
homogenized output reports exactly one selected fission spectrum (`Nf = 1`).
Multiple fission-spectrum components require an approved aggregation rule and
are rejected here. `SCAT00` must be decoded exclusively with its accompanying
`IJJS00`, `NJJS00`, and `IPOS00` profile records as specified by IGE-351;
direct indexing into compressed scattering values is prohibited.

## Projection to the existing Core contract

For each source knot and group `g`, define `C = 100.0 m^-1 per cm^-1` and use
only the following deterministic transformations:

| `BurnupCoefficientValuesV1` field | Required source expression | Target unit |
| --- | --- | --- |
| `AbsorptionGroup{g}PerM` | `(NTOT0[g] - SIGS00[g]) * C` | m^-1 |
| `FissionGroup{g}PerM` | `NFTOT[g] * C` | m^-1 |
| `NuFissionGroup{g}PerM` | `NUSIGF[g,1] * C` | m^-1 |
| `DownscatterGroup1To2PerM` | `SCAT00[1->2] * C` | m^-1 |
| `ChiGroup1` | `CHI[1,1]` | 1 |
| `ChiGroup2` | `1 - ChiGroup1`; never serialized/interpolated separately | 1 |
| `EnergyPerFissionJ` | `(Σ_g EFIS[g] * FLUX-INTG[g]) / (Σ_g NFTOT[g] * FLUX-INTG[g]) * 1.602176634E-19` | J |

The exact elementary charge factor is the SI definition of one electron-volt in
joules. The energy ratio uses the same source flux in numerator and denominator;
its arbitrary source normalization cancels. It uses `EFIS`, not `H-FACTOR`, so
only fission energy production is included. The denominator must be finite and
strictly positive. `EFIS`/`FLUX-INTG` remain offline provenance inputs and are
not runtime pack fields.

The converted `DIFF[g] * 0.01` has unit m and is retained only for the exact
offline DONJON/reference case. It must never be used to derive a Core node
volume, edge conductance, boundary conductance, topology, normalisation, or
runtime field; P2-T02 and the reduced-model boundary expressly prohibit that
shortcut.

Before a row is accepted, the converter must prove finite values, zero
`SCAT00[2->1]`, nonnegative target fields, absorption greater than or equal to
fission, matching fission/nu-fission zero support, finite positive implied
yield, `CHI[1,1]` in `[0,1]`, source `CHI[1,1] + CHI[2,1] = 1` under the
export's exact numeric semantics, positive energy per fission, and strictly
increasing converted burnup knots. It must not clamp, normalize, smooth,
interpolate, infer, repair, or drop a failing value.

The table identity and path-free provenance will be defined by XSEC-05. Until
then, no JSON runtime pack or new public schema exists. The existing
`BurnupCoefficientTableV1` schema/version, `SI-v1` units profile, exact-knot
lookup, and linear interpolation remain unchanged.

## Full-core and DONJON boundary

This lattice mapping does not supply a 380-channel coordinate map, node
volumes, conductances, boundary conductances, or a legally/technically admitted
DONJON CANDU full-core input. The old P1-T03 HDF5/deck route and direct
`Candu6.x2m` route remain external provenance smoke evidence only; they are not
admitted source-to-runtime or full-core authorities.

Consequently XSEC-04 is not eligible after XSEC-03 alone. `XSEC-CORE-01` must
first define and review a separate, bounded DONJON static-core input and its
explicit topology/mapping authority. It must use only the XSEC-03 exported
candidate constants, preserve the Core separation rules, and name whether it
is an external reference candidate or a project-authored CANDU-style case. It
may not claim a named-station configuration or derive Core conductances from
DRAGON diffusion coefficients.

## Validation and non-authorizations

XSEC-03 must first validate the exact source-deck lexemes, external artifact
hashes, group count/order/boundary, source convergence, all required field
names/units, and every mapping invariant. It then performs two fresh offline
reruns and compares their path-free manifests and semantic-export bytes before
any candidate pack work. T6 source reproduction is owned by XSEC-03.

This specification does not select a runtime conductance rule, topology,
volume, power normalisation, solver tolerance, golden threshold, reference
baseline, control/poison/refuelling state, runtime serialization schema, or
game/UI behavior. It is invalidated by any source-library, geometry, material,
group-boundary, source-field, unit, or meaning change.

## Primary-source traceability

- DRAGON5 user guide IGE-335: `TCWU11` CANDU-6-type two-group depletion case,
  `EDI` condensation/homogenization, `APX` reaction names, and documented
  solver/self-shielding defaults.
- DRAGON/TRIVAC data-structure guide IGE-351: `MACROLIB` `ENERGY`, `NTOT0`,
  `NUSIGF`, `CHI`, `DIFF`, `NFTOT`, and compressed `SCAT00` semantics/units.
- P1-T08 digest rows `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03`.
- XSEC-01 technical inventory and XSEC-RIGHTS-01 owner internal-use
  attestation.
