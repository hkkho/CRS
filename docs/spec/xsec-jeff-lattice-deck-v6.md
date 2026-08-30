# XSEC JEFF lattice deck authority v6 - SAPHYB energy companion route

**Status:** approved for one later bounded offline cross-artifact semantic
decode task only. This specification supersedes deck v5 solely for the
additional SAPHYB fission-energy companion artifact. It creates no numerical
source result in the repository, runtime pack, Core/CLI/Unity change,
full-core case, golden/reference authority, release, or Unity admission.

## Preserved source basis

All deck-v5 source basis and restrictions remain in force: the official
Version5 JEFF procedure, its geometry/materials/group boundary/controls,
three original EDI source lexemes, both original SPH calls, all four source
assertions, `WHOLECELL0`, WCFIELD, and the GOLVER `DIFF` route are retained.
No original source operation is replaced, reordered, or reinterpreted.

## Exhaustive SAPHYB companion delta

The external procedure declares one additional persistent object and enables
the documented SAP module. Immediately after WCFIELD's existing first-state
creation and before the first unchanged SPH call, it initializes SAPHYB from
the existing library, declares one user state key, selects the existing
one-mixture macro output, and selects only SAP `EFIS`.

Immediately after the existing WCFIELD update at the later mapped state and
before the second unchanged SPH call, it performs the second SAPHYB recovery
from that same named WCFIELD object. Both recoveries therefore consume the
pre-SPH WCFIELD source and preserve the original SPH path. The final external
capture may select the companion SAPHYB object only after the fourth original
assertion; unchanged WCFIELD controls are separately captured through the
existing terminal interface.

No source geometry, material, library, group condensation, merge, correction,
transport/depletion control, assertion, WCFIELD EDI command, `WHOLECELL0`, or
GOLVER clause changes. `H-FACTOR`, `PRODUCTION`, capture/gamma energy, direct
SPH data, ADF data, and a new EDI MACROLIB field remain excluded.

## Required successor boundary

The next task must fresh-rerun the exact deck-v6 route, prove source-control
preservation and source/companion determinism, and decode no more than the
separately approved mapping-v5 cross-artifact fields. It must not use a SAPHYB
value until it proves the documented address, reaction label, state ordering,
two-group/one-mixture structure, units, and WCFIELD/SAPHYB flux correspondence.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, as methodology/applicability context only.
