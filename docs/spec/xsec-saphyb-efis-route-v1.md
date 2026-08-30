# XSEC SAPHYB EFIS route v1

**Status:** approved as an offline source-artifact route only. It admits no
numerical candidate row, runtime pack, Core/CLI/Unity behavior, DONJON case,
golden/reference claim, redistribution, or release.

## Decision

`XSEC-ENERGY-01` establishes a separate DRAGON5 SAP/SAPHYB artifact route for
the fission-only energy-production field that the EDI WCFIELD MACROLIB route
could not export. The two artifacts are deliberately not treated as
interchangeable: WCFIELD remains the pre-SPH, one-mixture EDI MACROLIB source
for the existing mapping fields, while SAPHYB is the separately selected
fission-energy artifact.

The SAP input reaction `EFIS` is documented as the energy-production cross
section for the `(n,f)` reaction only. The pinned exporter source maps that
reaction to `ENERGIE F.` and constructs each group value by multiplying
`NFTOT` by `MEVF`. Its neighbouring gamma branch is distinct and is not used.
`MEVF` is documented as fission energy in MeV. Therefore the selected SAPHYB
quantity has the exact source dimension `MeV cm^-1`; it is not `H-FACTOR`,
`PRODUCTION`, capture/gamma energy, a reconstruction, or a direct-correction
quantity.

## Artifact, state, and structural contract

The SAPHYB artifact is initialized from the existing named WCFIELD source
output and recovered once at each existing mapped source state. It declares a
single user state key and has exactly two elementary calculations. Each
calculation has exactly one output-mixture directory. The field identity is
the `NOMREA` character label `ENERGIE F.`. The relevant value storage is
SAPHYB's documented `RDATAX` cross-section array, addressed through `ADRX`;
no value from that array was read in this task.

The same mixture directory carries `FLUXS`, which the SAPHYB guide defines as
the volume- and energy-integrated neutron fluxes in the output tables. Its
observed shape has exactly two entries. A later decoder must prove its
component-wise correspondence to the WCFIELD flux used by the inherited
mapping before using it in a ratio; that proof is not supplied by this route
task.

The source attaches SAPHYB after each WCFIELD EDI production and before the
corresponding unchanged original SPH call. SAPHYB documentation states that
SAPHYB cross-section information is neither transport-corrected nor
SPH-corrected. The route contains no `SPH`, `SPH-EPSILON`, or `ADF` output
subtree and makes no direct-correction claim.

## Exact mapping consequence

For the two retained state identities and the inherited group order,
SAPHYB `ENERGIE F.` replaces only mapping-v4's unavailable EDI `EFIS` source.
The required unit conversion is exact:

```text
EFIS_eV_cm-1[g] = SAPHYB_ENERGIE_F_MeV_cm-1[g] * 1,000,000 eV / MeV
```

The later numerical semantic-decode task must keep WCFIELD `NFTOT` as the
denominator cross section and use SAPHYB `FLUXS` only after proving the
required cross-artifact flux correspondence. It must reject a missing,
duplicate, non-finite, unequal, unit-ambiguous, non-two-group, or
non-one-mixture value without interpolation, reconstruction, clamping, or
substitution.

## Evidence and limitations

The route was statically bound to pinned IGE-335/IGE-351 documentation and
DRAGON5 source, then exercised in two isolated offline source runs. Both
retained all four original assertions and completed normally; an untouched
control reproduced its previous capture exactly. The duplicate SAPHYB input
reproduced its capture byte-for-byte. A host floating-point underflow/denormal
notice was also present in the untouched control and is recorded as a baseline
environment diagnostic, not numerical acceptance evidence.

This decision is only a route and field-identity decision. It supplies no
numeric comparison, tolerance, candidate admission, runtime schema, or
golden authority. `XSEC-ENERGY-02` must be separately authorized to perform a
fresh cross-artifact semantic decode and candidate-admission decision.

## Pinned primary evidence

| Artifact role | Canonical-LF SHA-256 |
| --- | --- |
| IGE-335 SAP reaction definition | `332ce8c69ffde581feccbb480e8d869e60433eb1b41b20b74d81a9d8a8230d41` |
| IGE-351 MICROLIB `MEVF` unit definition | `70b3389c226171b98bc69f846e72a97a0fea241128aded871b3fb801ad7eee34` |
| IGE-351 SAPHYB data-structure guide | `250be6ac2110a6794ca38187754d5442d0c26147c14eac895d7b6ea24329676e` |
| SAP implementation | `6a7438285b2a2c25aa5265689b2eed63cc8118153bca9e2576b79b1af4d72565` |
| SAPHYB exporter | `8072b0e2f81ca8523881135292adb21c86c35a0b74ebc8e3e4fbeb7b70f63cfa` |
| EDI microlib/input propagation | `9e37585de972201fb84d4e263536435cf3897cfe43257c7de7de6f9e766ca96c` |

Pinned source identity: Version5 commit
`eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`; offline image identity:
`docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79`.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They provide methodology and applicability
context only.
