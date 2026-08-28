# XSEC-FIELD-01 owner approval - required-field source export resolution

**Recorded:** 2026-08-28
**Decision:** APPROVED / BOUNDED

## Owner direction

The repository owner requested creation of the successor bounded source task
after XSEC-03-R3 found that the otherwise valid whole-cell payload lacks
`EFIS` and `DIFF`. This approval authorizes a source/deck and mapping-resolution
task to determine and prove a lawful, source-preserving way to export those
already-required fields, or to produce a fail-closed decision if no such route
exists.

## Bounded authority

The task may inspect official DRAGON5 primary guides and the owner-authorized
external JEFF/WLUP fixture, create external temporary source variants and
read-only validators, run the pinned offline DRAGON5 environment, and create
the bounded deck/mapping/review/report evidence named in its request.

## Non-authorization

This approval does not authorize a silent substitution of `H-FACTOR`, a change
to field meaning, unit, normalization, equation, tolerance, runtime schema,
Core/CLI/Unity behavior, data pack, full-core/DONJON case, golden/reference
baseline, distribution, release, or Unity-ready claim. It does not authorize
retaining raw data, source procedures, captures, listings, or numerical field
values in the repository.
