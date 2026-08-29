# XSEC-03-R4 owner approval - GOLVER semantic decode

**Recorded:** 2026-08-29
**Decision:** APPROVED / BOUNDED

## Owner direction

The owner directed direct continuation from completed `XSEC-DIFF-01`. This
approval authorizes `XSEC-03-R4` to fresh-rerun the exact deck-v5/mapping-v4
GOLVER route and decode only its approved two source selections to decide
whether an offline candidate semantic row is admissible.

## Boundary

The task may create external temporary source variants and read-only decoders,
run the pinned offline DRAGON5 fixture, and retain only path-free provenance,
semantic/transformation fingerprints, field/unit identities, invariant
outcomes, and repeat-equality results in the repository.

It does not authorize changing deck v5, mapping v4, equations, units,
tolerances, source inputs, schemas, Core/CLI/Unity behavior, a runtime pack,
DONJON/full-core case, golden/reference baseline, redistribution, publication,
or release. Raw source procedures, captures, listings, numerical arrays,
source values, and derived values remain external-only.

## Validation and review sequencing

Per the owner's 2026-08-29 direction, this continuing XSEC task requires
focused source, semantic-decoder, and determinism validation only. The full
cross-project regression and independent code review are deferred to the XSEC
phase-closeout gate, where both are required together. This is a sequencing
decision only; it does not waive a failed focused check, source-reproduction
failure, or any fail-closed stop condition in the task request.
