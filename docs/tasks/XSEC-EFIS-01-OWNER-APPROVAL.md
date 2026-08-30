# XSEC-EFIS-01 owner approval - EFIS export-identity specification

**Recorded:** 2026-08-29
**Decision:** APPROVED / BOUNDED

## Owner direction

The owner directed creation and execution of a separate `XSEC-EFIS-01`
specification task after `XSEC-03-R4` stopped on the absent mandatory `EFIS`
field. This approval authorizes the task to prove and specify one
source-preserving DRAGON5 export identity for the existing required fission-only
energy-production input, or to reject the deck-v5 route if no such identity can
be established.

## Boundary

The task may inspect the pinned official Version5 documentation and source,
create external temporary input variants and read-only observers, run the
owner-authorized offline JEFF fixture, and create only the task records,
approved specification(s), path-free manifest, report, and scope update named
in its task request.

## Non-authorization

This approval does not authorize an inferred field equivalence, a substitution
of `PRODUCTION` or `H-FACTOR`, a change to geometry, materials, group boundary,
source controls, source assertions, runtime equations, Core/CLI/Unity behavior,
runtime schema, a data pack, full-core case, golden/reference baseline,
redistribution, publication, or release. Raw libraries, procedures, captures,
listings, source values, and derived values remain external-only. A later
fresh semantic-decode task remains separately required for numerical candidate
admission.
