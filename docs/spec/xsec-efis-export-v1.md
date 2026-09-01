# XSEC EFIS export decision v1

Status: `REJECTED / BLOCKED`

Task: `XSEC-EFIS-01`

## Purpose and boundary

This decision resolves the source-export identity gap recorded by
`XSEC-03-R4-MISSING-EFIS`. It determines whether the pinned DRAGON5 EDI
whole-cell route can produce the exact `EFIS` MACROLIB field required by
[`xsec-source-runtime-mapping-v1.md`](xsec-source-runtime-mapping-v1.md) and
carried unchanged by
[`xsec-source-runtime-mapping-v4.md`](xsec-source-runtime-mapping-v4.md).

It does not decode a numerical array; alter a source deck or mapping; select a
new physics equation, normalization, or tolerance; or admit a runtime pack,
golden/reference result, DONJON case, Core/CLI/Unity integration, or release
claim.

## Required identity

Mapping v1 requires `EFIS[g]` to be the fission-only energy-production cross
section in `eV cm^-1`. Its offline energy-per-fission ratio uses that field
with the same source `NFTOT` and `FLUX-INTG` weighting. The mapping expressly
excludes `H-FACTOR` because it is not the fission-only quantity.

The pinned IGE-335 SAP documentation independently defines the SAP reaction
name `EFIS` as the energy-production cross section for the `(n,f)` reaction
only. That establishes the reaction meaning, but it does not establish an EDI
MACROLIB writer path, unit representation, normalization, or selected-output
identity.

## Static primary-source result

The proposed EDI syntax `MICR ... REAC n (HREAC...)` is not an EDI MACROLIB
field-export control:

- IGE-335 section 3.08 defines `REAC` as a selection of reactions included in
  the **output microlib**.
- `EDIGET` parses the requested names into `NOUT/HVOUT`; `EDIDRV` describes
  them as MATXS names of output cross-section types and passes them to
  `EDIMIC`.
- `EDIMIC` applies `NOUT/HVOUT` by removing unmatched `HMAKE` entries only
  while writing each isotope under the output microlib. The EDI MACROLIB writer
  has already been built by the separate EDI path.

Therefore an `EDI ... MICR ... REAC ... EFIS` request may preserve or filter a
microlib reaction entry, but it does not prove, request, or create a
`MACROLIB/GROUP/*/EFIS` record. The fresh non-mutating observation in
`XSEC-03-R4` is consistent with that source behavior: the selected WCFIELD
MACROLIB did not contain `EFIS` despite the EDI request.

The pinned EDI MACROLIB writer (`EDIPXS`) writes `PRODUCTION` from its
`RATECM(*,NW+4)` slot. The companion primary source (`EDIPRR`) defines that
slot as fixed sources / productions, while defining a different slot as
fission. `PRODUCTION` is consequently not an admissible fission-energy
substitute. `H-FACTOR` is also not admissible: it is already explicitly
forbidden by mapping v1/v4 and no source-to-mapping identity proof changes
that prohibition.

No source-preserving candidate remains under this task's constraints:

| Candidate | Decision | Reason |
| --- | --- | --- |
| Existing EDI `MICR ... REAC ... EFIS` | Rejected | Controls output microlib filtering, not EDI MACROLIB EFIS writing. |
| EDI `PRODUCTION` | Rejected | Primary writer/source identifies it as fixed sources / productions, not fission-only energy production. |
| EDI `H-FACTOR` | Rejected | Explicitly forbidden by the active mappings and lacks the required fission-only identity. |
| SAP `EFIS` reaction | Not admitted | SAP documents reaction semantics but is not a proven source-preserving EDI WCFIELD MACROLIB export route; its artifact, units, normalization, and mapping consequences are outside this task. |

## Decision and fail-closed rule

`XSEC-EFIS-01` rejects the current EDI WCFIELD MACROLIB route as an EFIS source.
No deck v6 or mapping v5 is created. No numeric source observation is permitted
from `EFIS`, `PRODUCTION`, or `H-FACTOR` on the strength of this decision.

A different source-to-runtime route needs the exact output artifact and field
identity, fission-only inclusion rule, unit, normalization, group and mixture
mapping, source-preservation impact, and the applicability consequences for
mapping v1/v4 before numeric decoding. It must not relabel, scale, reconstruct,
or infer any existing EDI field.

## Pinned evidence identities

All identities are canonical-LF SHA-256 values of externally retained,
non-redistributed primary source artifacts. This repository retains no raw
procedure, listing, capture, or numeric source value.

| Artifact role | SHA-256 |
| --- | --- |
| IGE-335 EDI grammar and microlib `REAC` documentation | `03bb6e17bee307a4a161274473513d8a197715785f6332a1cea6c0e449e7ac2e` |
| IGE-335 SAP reaction definitions, including `EFIS` | `332ce8c69ffde581feccbb480e8d869e60433eb1b41b20b74d81a9d8a8230d41` |
| EDI input parser (`EDIGET`) | `6d7365e84e1538abb19909d0354fb75e44cf7ce100bb30bd1c77af7c9ca7fcd5` |
| EDI driver (`EDIDRV`) | `b5181061753feb9d504a0dd74fc4aebc85fbe535c33cf4ef134dd668b54db55a` |
| EDI microlib writer/filter (`EDIMIC`) | `9e37585de972201fb84d4e263536435cf3897cfe43257c7de7de6f9e766ca96c` |
| EDI MACROLIB writer (`EDIPXS`) | `9bfe3de298f3d29e1c158343a09bfcfc34ab7c831a73429a5264bdf8f94cb4d1` |
| EDI rate-slot definitions (`EDIPRR`) | `5971c429d439baf3053fd5228042d4e0ba576362b3d2817e201532a3fed9a520` |
| SAP implementation (`SAP`) | `6a7438285b2a2c25aa5265689b2eed63cc8118153bca9e2576b79b1af4d72565` |

Pinned source identity: Version5 commit
`eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`; offline image identity:
`docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79`.

## Literature coverage

The literature-digest rows read for scope and limitations were `S1-R04`,
`S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03`. They provide
methodological context only and do not establish an EFIS writer identity or
replace the primary-source proof above.
