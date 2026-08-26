# XSEC JEFF lattice deck authority v2

**Status:** approved for the candidate offline lattice pipeline only. It supersedes
the **deck basis only** of XSEC mapping v1; it creates no runtime pack, golden
case, release, or Unity admission.

## Purpose

`XSEC-03` established that the v1 project-authored TCWU11 plus JEFF library
substitution cannot retain its frozen WLUP regression assertions. This v2
authority selects the official JEFF procedure instead of changing a source
assertion. The source-to-Core fields, units, group ordering, burnup knots,
post-EDI/pre-SPH boundary, conversions, invariants, exclusions, and full-core
boundary in `xsec-source-runtime-mapping-v1.md` are incorporated unchanged.
Only the exact lattice deck identity is replaced.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`.

## Exact selected source basis

Candidate ID: `xsec-c6-37-nu-jeff31-4ev-v2`.

| Role | Version5 relative path | Git blob SHA-1 | Canonical-LF SHA-256 |
| --- | --- | --- | --- |
| Official JEFF lattice/depletion procedure | `Dragon/data/tjeff31gx_proc/TCWUX11.c2m` | `497c03361ad4ecbd29eaaaf1dd6da1f16068cae5` | `9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813` |
| Official JEFF WIMSD4 library procedure | `Dragon/data/tjeff31gx_proc/TCWU05Lib.c2m` | `f9d9ee57046d2fef808264497b7d1643ca4cee3c` | `896de7f647fc7b3050815d1006f41cd938aca276b3a85c254d34e5c7d41e0040` |
| Shared assertion procedure | `Dragon/data/assertS.c2m` | `1b9c714fa7f45a1171e1ba80a682ee89b3cc0709` | `c6291cd7a2496f48ab012c294e2d823170401549cc5df3e366c9924f0a34040c` |

All three are fixed at Version5 commit
`eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`. The procedure explicitly states
its WLUP JEFF-3.1/XMAS source, performs 172-group depletion followed by the
same documented two-group schedule and 4 eV condensation, and retains its
source geometry/depletion lexemes. It is not a named-station model.

The four source `assertS` regression values are source lexemes and must remain
exactly: `1.118478`, `0.9420414`, `1.118481`, and `1.073615`. They are neither
new golden values nor XSEC acceptance tolerances. A source assertion failure
rejects the run.

## Exhaustive allowed project deltas

1. Omit exactly the two source lines
   `EDITION := SPH: EDITION VOLMATF INTLINF ;`, one after each source `EDI`
   call. The semantic source is consequently the source-created post-`EDI`,
   pre-`SPH` `EDITION` object. No SPH/ADF-corrected value may enter the
   candidate.
2. Invoke the source procedure from a minimal external driver and decode its
   already-present `res := EDITION` ASCII serialization into the required
   path-free semantic record. The decoder is read-only with respect to source
   objects and must fail closed on a missing/ambiguous required record.

No other line may be added, removed, reordered, or changed. In particular the
official 172-group transport/depletion behavior, absence of `ALLG BATCH 100`,
EDI argument ordering, temperatures, materials, dimensions, tracking, source
assertions, power, steps, group boundary, convergence defaults, and `FLU TYPE
K` lexemes remain source-controlled. The earlier TCWU11 candidate is historical
mapping evidence only and must not be run as the JEFF v2 candidate.

## Validation and non-authorizations

XSEC-03 must hash every source artifact before execution; prove the four source
assertions pass; verify all v1 incorporated field/unit/group/invariant rules;
and run two independent fresh roots with byte-identical semantic export and
path-free manifests. Any source/delta/field/unit/group/convergence mismatch is
a fail-closed stop.

This document does not change the existing Core contract or v1 transformations,
authorize reference data, select a numerical tolerance, admit a runtime pack,
or make a full-core map. `XSEC-CORE-01` remains mandatory before XSEC-04.
