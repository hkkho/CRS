# XSEC JEFF lattice deck authority v4 - source-preserving whole-cell route

**Status:** approved for the bounded offline XSEC candidate pipeline only.
This document supersedes the capture/output-selection portions of
`xsec-jeff-lattice-deck-v3.md` for candidate
`xsec-c6-37-nu-jeff31-4ev-v4-sph-preserved-wholecell-2k`. It does not admit
field values, a runtime pack, golden/reference data, a release, or Unity use.

## Preserved source basis

The authoritative procedure remains official Version5 JEFF `TCWUX11.c2m` at
commit `eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`, canonical-LF SHA-256
`9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813`.
`TCWU05Lib.c2m`, `assertS.c2m`, the 172-group JEFF/WLUP route, 4 eV
condensation, geometry, materials, source controls, and the four source
assertions remain exactly as recorded in deck v3.

Both original `EDITION := EDI` calls and both original
`EDITION := SPH: EDITION VOLMATF INTLINF ;` calls remain byte-for-byte and in
their original order. The new output never writes or replaces `EDITION`.

## Exhaustive permitted external procedure delta

The external-only capture variant declares one additional linked-list object,
`WHOLECELL0`. Immediately after the first original EDI and immediately before
the first unchanged original SPH line, it creates that separate object with a
parallel EDI operation. Its `MERG REGI` clause contains the original 31 region
slots, each assigned to output mixture `1`; it retains `COND 4.0 MICR ALL
SAVE` and `MGEO CANDU6F` exactly as used by the source EDI. The original EDI
and SPH lexemes are not modified.

For the day-300 capture variant only, immediately after the external retained
copy `PRE172 := EDITION ;` and before the unchanged second source SPH line,
the same separately named output is updated in place with the day-300 flux and
library using `WHOLECELL0 := EDI: WHOLECELL0 FLUX LIBRARY VOLMATF :: SAVE ;`.
It does not add another merge, does not alter `EDITION`, and retains the
already-proven one-mixture topology.

After the fourth source assertion only, the existing FILE-backed
`SEQ_ASCII`/`PARAMETER` interface retains exactly one requested object: either
the unchanged original `PRE0`/`PRE172` for regression evidence, or
`WHOLECELL0` for structural evidence. It does not change source control flow.
For each state, those two capture variants are otherwise byte-identical after
normalizing only that final capture-target assignment. Their normalized-delta
hashes, the preserved original EDI/SPH lexeme hash, and the preserved assertion
lexeme hash are bound in the route manifest.

No other source procedure, driver, solver, material, geometry, group,
depletion, field, tolerance, convergence, or numerical-export change is
authorized.

## Required evidence and selection boundary

`XSEC-HOM-01` performed two fresh isolated original-output captures and two
fresh isolated whole-cell captures for each state. Each original output is
byte-identical to its XSEC-03-R2 baseline capture. Each same-state whole-cell
capture is byte-identical; the two states are distinct. The external capture
audit proves each selected whole-cell direct MACROLIB has two groups, exactly
one mixture, and a one-entry volume record, with no direct `SPH`,
`SPH-EPSILON`, or `ADF` subtree. The absence applies only to the direct
selected subtree; it does not claim the source calculation history is
SPH-free.

Only these external selectors are eligible for a later semantic-decode task:

| Source elapsed day | Retained output | Exact selector |
| ---: | --- | --- |
| 0 | `WHOLECELL0` | `REF-CASE0001/MACROLIB` |
| 300 | `WHOLECELL0` | `REF-CASE0002/MACROLIB` |

The original `PRE0` and `PRE172` captures are regression controls only; they
are not field sources under this authority. No field array was decoded in
establishing this route.

## Validation and traceability

The path-free eight-run record is
`reference/manifests/xsec-hom-01-route-v1.json`. It binds the pinned DRAGON5
image and executables, source and external-delta hashes, original baseline
hashes, capture hashes, structural results, assertion/convergence/completion
fingerprints, and the non-admission boundary. It is the required source-route
input for a separately authorized fresh semantic-decode task.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`; they are methodology/applicability context
only. Official DRAGON5 IGE-335 and IGE-351 guide the EDI, persistence, and
MACROLIB structural claims. Neither source set is numerical or golden
authority here.
