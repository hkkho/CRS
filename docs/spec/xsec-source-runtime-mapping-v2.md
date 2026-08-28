# XSEC source-to-runtime mapping v2 - SPH-preserving two-knot candidate

**Status:** approved for the bounded XSEC offline candidate pipeline only.
This v2 specification supersedes the combined source-stage route in
`xsec-source-runtime-mapping-v1.md` when used with the replacement
`xsec-jeff-lattice-deck-v3.md`. It creates no runtime pack, golden/reference
authority, release, or Unity admission.

## Resolution and scope

`XSEC-03-R1` proved that removing either official source `SPH` transition is
not non-mutating instrumentation: the later unchanged JEFF source assertion
fails. `XSEC-SPH-03` then proved an official persistent capture interface that
retains both source transitions and all four source assertions. The previous
rule that simultaneously requires pre-SPH data and removal of SPH is therefore
invalidated and must not be used.

This specification retains the official JEFF `TCWUX11` source case, source
assertions, JEFF/WLUP identity route, 37-element CANDU-6-type annular-cell
geometry, 172-to-two-group condensation, 4 eV split, source controlled
materials/solver controls, required reaction fields, field meanings, units,
transformations, invariants, Core exclusions, and path-free provenance rules
from mapping v1. Those provisions are incorporated unchanged except where this
document explicitly replaces their source-stage and knot-selection clauses.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They are methodology/applicability context
only, not a numerical, runtime, or golden authority.

## Exact candidate and preserved source basis

Candidate ID: `xsec-c6-37-nu-jeff31-4ev-v3-sph-preserved-2k`.

The exact source procedures, shared assertion procedure, Version5 commit,
canonical hashes, pinned JEFF-3.1/XMAS-172 WIMSD4/WLUP route, and four source
assertion lexemes are exactly those in `xsec-jeff-lattice-deck-v2.md`. This
specification does not select a new library, source value, geometry, material
condition, group boundary, or numerical control.

Both exact source lines below remain, at their original locations and with
their original lexemes:

```text
EDITION := SPH: EDITION VOLMATF INTLINF ;
```

The project may only add the persisted capture interface proven by
`XSEC-SPH-03`: the callee receives a caller-owned `SEQ_ASCII` result through
`PARAMETER`; after the fourth source assertion it assigns exactly one retained
copy from either `PRE0` or `PRE172`; the caller provides the FILE-backed result
slot. The original procedure must otherwise keep source operations, order,
objects, and source assertions intact. The external procedure/deck identity
and capture-interface hashes must be recorded by `XSEC-03-R2`.

## Exact two-state selection

The source uses two non-mutating, external-only retained capture states:

| Candidate elapsed day | Capture object | Exact selector | Required proof |
| ---: | --- | --- | --- |
| 0 | `PRE0` | `REF-CASE0001/MACROLIB` | Direct selected payload has the documented two-group `ENERGY` identity and no direct forbidden correction subtree. |
| 300 | `PRE172` | `REF-CASE0002/MACROLIB` | Direct selected payload has the documented two-group `ENERGY` identity, its source timestamp reports elapsed day 300, and it has no direct forbidden correction subtree. |

The source's later two-group depletion loop is a source-regression continuation
only. It begins after the retained source transitions and is prohibited as a
data source for this candidate. No record other than these two selectors may
be decoded, substituted, merged, or treated as a knot.

This is a field-scoped source boundary. The selected payload may be used only
when its direct `MACROLIB` subtree contains no `SPH`, `SPH-EPSILON`, or `ADF`
record/subtree. Presence of one of those names in either selected payload is a
fail-closed rejection. SPH-related history elsewhere in the retained external
LCM object is not decoded; it is retained only as source provenance. This does
not assert that the official source calculation history has no SPH influence,
nor does it call the selected coefficients "SPH-free".

The external converter must prove one, and only one, direct selected MACROLIB
for each listed capture and reject any ambiguous path, additional source state,
missing selector, missing timestamp, wrong elapsed day, forbidden direct
subtree, wrong group count/order/boundary, NaN/Inf, nonconvergence, or failed
source assertion.

## Depletion and Core-table boundary

The candidate table has exactly two ordered source elapsed-day keys: `0` and
`300`. The inherited conversion is unchanged:

```text
B[J/kg_HM] = 31.9713 * 1000 * elapsed_days * 86400
```

Its converted burnup knots must be finite, nonnegative, and strictly
increasing. The existing `BurnupCoefficientTableV1` accepts ordered rows and
does not impose a larger minimum; its existing exact-knot/linear interpolation
and out-of-range rejection are retained. Thus this candidate's sole possible
interpolation domain is the closed interval between its two converted knots.
No extrapolation, clamping, intermediate-value synthesis, fitting, or runtime
behavior change is authorized.

The historical v1 seven-day-key list `0, 1, 5, 10, 50, 150, 300` is not an
authority for this candidate. It remains historical evidence only because its
source loop occurs after the source transitions whose removal was disproved by
`XSEC-03-R1`.

## Inherited field mapping and exclusions

For each selected record, the required source fields and their exact
transformations to `BurnupCoefficientValuesV1` are unchanged from mapping v1:
`ENERGY`, `NTOT0`, `SIGS00`, `SCAT00` with `IJJS00`/`NJJS00`/`IPOS00`,
`NFTOT`, `NUSIGF`, `CHI`, `EFIS`, `FLUX-INTG`, and offline-only `DIFF`.
The `cm^-1` to `m^-1` factor, the eV-to-joule factor, fission-energy ratio,
zero-upscatter proof, `CHI` derivation, finite/nonnegative/fission-support
invariants, and rejection rules are unchanged. The two coarse groups remain
ordered fast then thermal by the exported 4 eV `ENERGY` identity.

`DIFF`, topology, node volume, conductance, boundary conductance, power
normalization, solver tolerance, reference baseline, full-core mapping, raw
data, and all SPH/ADF quantities remain excluded from the Core candidate table.
No Core, CLI, Unity, schema, or runtime contract changes here.

## Validation and non-authorizations

`XSEC-03-R2` alone owns fresh clean source reruns, source/deck/executable and
library hashing, complete source assertion/convergence proof, semantic decode,
two-state selection validation, determinism comparison, and T6. A successful
rerun remains an offline candidate; it does not admit a runtime pack or
golden/reference data. Any source identity, capture interface, selection,
field, unit, normalization, convergence, or license discrepancy fails closed.

This specification does not select a full-core case or change the requirement
for `XSEC-CORE-01` before `XSEC-04`.

## Primary-source traceability

- DRAGON5 IGE-335: documented `EDI`, `SPH`, `SAVE`, and `TCWUX11` source
  procedure behavior.
- DRAGON5 Version5 FAQ: FILE-backed persistence and ASCII serialization
  behavior used by the XSEC-SPH-03 capture interface.
- IGE-351 data-structure guidance cited by mapping v1 for MACROLIB field and
  compressed-scattering meanings; the field mapping is inherited unchanged.
- `XSEC-03-R1`, `XSEC-RUNNER-01`, and `XSEC-SPH-03` path-free run evidence.
