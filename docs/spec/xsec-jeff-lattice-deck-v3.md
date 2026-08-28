# XSEC JEFF lattice deck authority v3 - SPH-preserving capture route

**Status:** approved for the bounded offline XSEC candidate pipeline only.
This document replaces the project-delta section of
`xsec-jeff-lattice-deck-v2.md` for candidate
`xsec-c6-37-nu-jeff31-4ev-v3-sph-preserved-2k`. It admits no source result,
runtime pack, golden/reference case, release, or Unity use.

## Exact preserved source basis

The authoritative source procedure is official Version5 JEFF `TCWUX11.c2m`,
the library procedure is `TCWU05Lib.c2m`, and the shared assertion procedure
is `assertS.c2m`, all at Version5 commit
`eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`. Their exact paths, git blob
identities, canonical-LF hashes, library identity, group structure, source
geometry/materials, 4 eV condensation, solver lexemes, and four source
assertion lexemes remain exactly as recorded in deck v2.

## Exhaustive allowed project deltas

1. Retain both original source `EDITION := SPH: EDITION VOLMATF INTLINF ;`
   lines unchanged and in place. They are required source state transitions,
   not removable instrumentation.
2. Use only the official FILE-backed caller/callee `SEQ_ASCII`/`PARAMETER`
   capture interface proved by `XSEC-SPH-03`. It makes an external retained
   copy of `PRE0` after the first `EDI` and before the first `SPH`, or
   `PRE172` after the second `EDI` and before the second `SPH`, after the
   fourth source assertion. It does not replace, reorder, edit, or delete a
   source object.
3. Invoke the source procedure from the existing minimal external driver and
   decode only the two selectors authorized by
   `xsec-source-runtime-mapping-v2.md`: `PRE0/REF-CASE0001/MACROLIB` and
   `PRE172/REF-CASE0002/MACROLIB`.

No other source line or source behavior may be added, removed, reordered, or
changed. In particular, no project-selected iteration/tolerance, transport,
depletion, material, geometry, condensation, field, or semantic-export rule
is authorized.

## Required validation boundary

The later `XSEC-03-R2` must hash all source and project-delta artifacts, prove
the four unchanged source assertions and normal source completion, prove the
two captures deterministic across fresh roots, and fail closed unless the two
exact selected MACROLIB paths meet mapping v2. It must not decode the later
source two-group loop or claim it is a pre-SPH state.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, as methodology context only.

