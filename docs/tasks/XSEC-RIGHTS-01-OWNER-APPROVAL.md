# XSEC-RIGHTS-01 Owner Permission Attestation

**Recorded:** 2026-08-25

**Status:** APPROVED / BOUNDED INTERNAL-USE AUTHORITY

## Owner direction

The repository owner attests that the project has permission to download and
use JEF/JEFF nuclear data and explicitly authorizes its offline use with
DRAGON5 and DONJON5 to generate cross-section-case candidates and derived
golden-case candidates. The owner further directs that the raw nuclear-data
files are not redistributed with the game.

This attestation is the explicit project-owner rights evidence requested by
the historical XSEC-01 blocker. It applies to the JEFF-3.1/WLUP route recorded
in the XSEC-01 inventory, including the official conversion route needed to
exercise that data with the pinned offline reference tools. It records only a
project-owner internal-use direction and is not an external legal
determination.

## Bounded use and retention conditions

- Raw JEFF/JEF/WLUP data, converted library binaries, and DRAGON5/DONJON5
  installations remain external to this repository and are never Unity runtime
  dependencies.
- The game must not contain or redistribute raw JEFF/JEF/WLUP data or a
  converted source-library binary.
- This record authorizes internal offline generation and evaluation only. A
  generated runtime pack remains a candidate until XSEC-02 through XSEC-08 and
  G4-R7 establish its technical mapping, provenance, validation, reference
  reproduction, numerical comparisons, and runtime admission.
- This attestation does not authorize redistribution, publication, release, or
  store delivery of a derived runtime pack. Any such disposition requires a
  separate, artifact-specific authority after the candidate admission path.
- Source identities, hashes, provenance, applicable notices, and attribution
  must remain in path-free manifests. No claim of third-party endorsement,
  general redistribution permission, or public legal conclusion is made.
- The user-direction record is intentionally retained as an owner attestation,
  not as a copy of a private license or legal advice.

## Non-authorization

This record does not select a CANDU geometry, material state, reaction mapping,
energy-group order, collapse/homogenization method, equation, unit,
normalization, interpolation rule, convergence criterion, tolerance, pack
schema, or golden baseline. Those decisions remain reserved for the separately
reviewed XSEC-02 specification task. It also does not authorize raw-data
redistribution, publication, signing, store submission, OpenMC, a native
runtime reference dependency, Unity host-security changes, or excluded reactor
scope.

## Historical effect

XSEC-01 remains an immutable blocked technical-evaluation record. This later
attestation resolves only its project-owner internal-use/derivation condition;
it does not retroactively change XSEC-01 into a legal conclusion, a completed
reference reproduction, or golden evidence.
