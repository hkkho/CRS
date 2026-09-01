# XSEC-01 Legal and Technical Admission Inventory

**Task:** XSEC-01
**Recorded:** 2026-08-25
**Status:** TECHNICAL EVALUATION CANDIDATE / RIGHTS UNRESOLVED
**Machine record:** [xsec-01-source-admission-v1.json](../../reference/manifests/xsec-01-source-admission-v1.json)
**Execution record:** [xsec-01-execution-provenance-v1.json](../../reference/manifests/xsec-01-execution-provenance-v1.json)

## Purpose and boundary

This record establishes a bounded, traceable external technical-evaluation
candidate. It does not establish a legally reusable derivative-data route and
does not select an equation,
cross-section field, reaction mapping, group boundary/order, collapse,
homogenization method, normalization, convergence rule, tolerance, geometry, or
runtime schema. It also does not admit a runtime pack, golden data, or an
authoritative CANDU model.

Applicable P1-T08 digest rows: S1-R02, S1-R03, S1-R04, S1-R05, S1-R06,
S1-R07, S1-R11, S5-R02, S5-R03, S5-R04, S5-R05, S5-R09, S6-R03, and
S6-R08.

The rows provide source and workflow context. They do not override the approved
specifications or turn a published or source-tool value into a runtime or golden
authority.

## Official-source findings

| Item | Official evidence | Admission conclusion |
|---|---|---|
| DRAGON5/DONJON5 toolchain | [Version5](https://merlin.polymtl.ca/version5.htm) identifies Version5.1 production and its OECD NEA GitLab source, says DRAGON5 is compatible with WLUP WIMS-D4 libraries, and states the Version5 distribution is LGPL. The [FAQ](https://merlin.polymtl.ca/faq5.htm) separates user procedures from the Version5 LGPL statement and warns that most available libraries are proprietary. | Admitted only as an external, offline reference tool. This repository does not distribute the tool source, executable, image, or any tool-produced raw object. |
| IAEA WLUP data and converter | The IAEA [WLUP downloads](https://www-nds.iaea.org/wimsd/downloads.htm) page publishes WIMSD libraries, benchmark materials, processing inputs, and for.src; the [WLUP home page](https://www-nds.iaea.org/wimsd/) describes its 172-group library and auxiliary tools as freely available. The exact [JEFF-3.1 catalogue](https://www-nds.iaea.org/wimsd/downloads2.htm) identifies jeff31gx.lib as a 172-group WIMSD-formatted library based on JEFF-3.1. Version5 explicitly directs users to convert the ASCII WLUP files with WILLIE FOBI. | Admitted only as an external technical-evaluation candidate. The catalogue establishes source and format, not artifact-specific ownership, derivative, or redistribution rights. |
| IAEA use permission | The IAEA [Terms of Use](https://nucleus.iaea.org/Pages/Others/Terms-Of-Use.aspx) permit reuse of IAEA content with attribution and no implied endorsement, but separately require rights from third-party holders. | The project records a hash-bound terms snapshot as evidence, but it does not treat the terms as a derivative-data license for JEFF-3.1/WLUP or a future runtime pack. |
| NEA dragon/libraries assets | Pinned project/API metadata and the checked tree contain no root or asset-level LICENSE, COPYING, or NOTICE. The official FAQ itself distinguishes open WIMS-D4/Draglib sources from the many proprietary libraries. | Not admitted for raw or derived runtime data. Public accessibility or a generic open-source description is not an asset-specific redistribution grant. |

## Exact external identities

### Version5 offline reference environment

| Property | Value |
|---|---|
| Source repository / tag | https://git.oecd-nea.org/dragon/5.1.git / v5.1.0 |
| Source commit / tree | eee582f8594d3b7d01b65660ca1e8bef00eb6b2a / 0d3e8f721c84b3c3b2d03e9898a8a0a9c29287d8 |
| Container | docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79 |
| Platform / image size | linux/amd64 / 394,464,605 bytes |
| Image created / OCI label | 2025-09-15T09:41:14.646815353Z / 24.04 |
| Compiler | GNU Fortran 13.3.0 |
| DRAGON executable SHA-256 | 3121023a186cab6d0a56fd363bb979794fcd904f860f49439567bff4b1510cd0 |
| DONJON executable SHA-256 | e125c8bc095d9a1a7e088847b1c21b1bc37043681b6104713fa29ee1507f1a76 |
| Execution controls | digest pin, network disabled, read-only root, explicit input staging, OMP_NUM_THREADS=1, external-only raw inputs/outputs |

### IAEA JEFF-3.1 / XMAS 172-group source

| Artifact | URL / identity | Size | SHA-256 |
|---|---|---:|---|
| Archive | https://www-nds.iaea.org/wimsd/download/jeff31gx.zip | 11,361,507 bytes | 7972ecffed02984aa4208ccd20c631d6d1272af51fb6db85b351daaf5c25962a |
| Formatted WIMSD library | jeff31gx.lib; declares 173 materials and 172 energy groups | 31,291,162 bytes | 9509b50f2df5329df10ae3c52c5d2ba2ce3df2447d7bbe4f9df06466ecf499c5 |
| IAEA auxiliary-program source | https://www-nds.iaea.org/wimsd/download/for.src | 792,505 bytes | 5b613a5e84ffc367ed1dd85a0e14e06b4b1fa3c1810fdcf2ac59eb21ad45a7e3 |
| IAEA deck splitter source | https://www-nds.iaea.org/wimsd/download/dckspl.for | 4,028 bytes | 971ab56d5091037b08aa9b8a6ddb21c993d8f4e8ded1ce3da4e333fe1b3aa393 |
| Generated binary | jeff31gx.bin; WIMSD4 sequential-unformatted binary for the pinned Linux/GNU Fortran environment | 8,157,872 bytes | c7fc05d6b7cb2085d999c568854aeaacc9c79477dec4c702e6b7d3fe15ba5c5e |

The unformatted binary is an external, platform/toolchain-bound derivative. The
formatted source and binary are not added to this repository.

## Rights finding

The direct technical path is reproducible, but the required artifact-specific
permission is not. The IAEA terms distinguish IAEA content from third-party
content. The WLUP catalogue says that jeff31gx.lib is based on JEFF-3.1, while
the official NEA [JEFF-3.1 page](https://databank.io.oecd-nea.org/data/jeff/31/)
describes a multi-institutional evaluated library but supplies no explicit
derivative/redistribution license for this artifact. The official-source check
found no permission that covers deriving and redistributing a Unity runtime pack
from this chain.

Accordingly, this record permits only external technical evaluation. It blocks
runtime-pack derivation, redistribution, licensing, production, Unity-ready,
authoritative, and golden claims. Explicit rights evidence for the exact data
and converter sources is required before a new task can define a pack or advance
to XSEC-02.

## Converter and reader evidence

The first direct test correctly failed when the IAEA ASCII file was supplied to
DRAGON5's WIMSD4 reader. Inspection of the pinned reader source shows it opens
that input as FORM='UNFORMATTED'. This is a format mismatch, not an accepted
implicit conversion.

The official route then succeeded:

1. Built the dckspl and WILLIE decks extracted from for.src with GNU Fortran
   13.3.0 in the pinned container. The extracted dckspl deck hash was
   0356057ed6b06ec27eb4fd1982a614113b1ac9503c54e662096e4a494062fdec;
   the separately downloaded dckspl.for remains a hash-bound IAEA locator.
2. Used the exact source lexeme retained in the machine manifest to convert the
   formatted source to the hash-bound binary above.
3. Ran the unmodified upstream tjeff31gx / TCWUX01 procedure against that
   binary, offline.

DRAGON5 loaded the 172-group library and listed the expected seven requested
records. The first two upstream assertions completed without assertion failure:

| Observable | Upstream expected | Observed | Absolute difference | Relative difference |
|---|---:|---:|---:|---:|
| K-EFFECTIVE / annular SYBIL case | 0.8227208 | 0.8227200 | 8.0e-7 | 9.723830062671246e-7 |
| K-EFFECTIVE / Cartesian SYBIL case | 0.8228317 | 0.8228318 | 1.0e-7 | 1.215318726307789e-7 |

The unmodified upstream procedure later terminated at its EXCELL transition with
FLU: INCONSISTENT FLUX OBJECT TRACK-TYPE AT RHS (SYBIL). EXCELL EXPECTED.
The cause is unresolved. The evidence demonstrates format conversion, reader
loading, and two calculations only; it is not a full regression PASS, numerical
baseline, convergence claim, or golden evidence.

## Candidate case routing

| Candidate | Use in this workstream |
|---|---|
| Project-authored CANDU 37-element lattice/depletion deck using the admitted external IAEA source | Eligible only after XSEC-02 freezes the case/model/mapping and fields. XSEC-03 owns execution and T6 repetition. |
| Project-authored DONJON full-core/static deck consuming the approved DRAGON product | Eligible only after XSEC-03; XSEC-04 owns execution and T6 repetition. |
| Upstream RegtestLZC_mccg | External provenance/control-poison smoke context only; not a depletion-data-pack case. |
| Upstream CFC-CELL | Not admitted for production: it relies on an E6MLIB WIMS-AECL source without established reuse/redistribution terms. |
| Upstream DONJON Candu6 prepared CPOs | Topology/context or external smoke only; no exact admitted DRAGON progenitor is established. |
| P1-T02 lumpSS and P1-T03 AFA | External-private provenance smoke only, under their existing reports. They are neither licensed production CANDU data nor Unity packs. |

## Permission and provenance controls

Any later candidate pack derived under this path must retain, in a path-free
manifest, the exact source URL/access date, archive and expanded-file hashes and
sizes, converted-binary hash, converter source/build hashes, tool image digest,
executable hashes, source deck lexemes/hashes, IAEA attribution/no-endorsement
notice, group structure, geometry, materials, units, normalization,
homogenization/collapse, convergence settings, and numerical comparison records.

No raw input, generated binary, upstream deck, or reference output may be
vendored or called at Unity runtime. The future runtime pack is a separate
admission decision requiring XSEC-02 through XSEC-08 and G4-R7.

## Remaining authority boundary

Before a redistributable runtime pack is produced, confirm artifact-specific
permission for use, derivation, retention, and redistribution of the exact
JEFF-3.1/WLUP and WILLIE inputs. The implementation also needs an exact CANDU
case, reaction/source-label mapping, energy boundaries and order,
group-collapse/homogenization method, exported fields, units, normalization,
convergence, interpolation, and runtime schema. These are technical and legal
inputs to the offline data pipeline, not project approval or review gates.
