# XSEC JEFF lattice deck authority v5 - GOLVER whole-cell field route

**Status:** approved for one later bounded offline semantic-decode task only.
This specification supersedes deck v4 only for candidate
`xsec-c6-37-nu-jeff31-4ev-v5-sph-preserved-wholecell-golver-2k`. It creates no
numeric source result, runtime pack, Core/CLI/Unity change, full-core case,
golden/reference evidence, release, or Unity admission.

## Preserved source basis

The official Version5 JEFF `TCWUX11.c2m` procedure, JEFF-3.1/WIMSD4 library
route, source geometry/materials/controls, 4 eV condensation, all three
original `EDITION := EDI` source lexemes, both original
`EDITION := SPH: EDITION VOLMATF INTLINF ;` calls, and all four source
assertions remain exactly as recorded in deck v4. `WHOLECELL0` remains the
separate all-one 31-region whole-cell object with its same day-300 in-place
update. No original source object, source operation, or source control is
replaced.

Deck v4's wording "both original EDI calls" is retained as historical record;
the hash-bound v5 audit establishes that the pinned official procedure has
three textual EDI source lexemes. This v5 clarification expands no source
operation and changes no physics: all three are preserved byte-for-byte.

## Exhaustive permitted field-output delta

One additional linked-list object, `WCFIELD`, is declared beside the deck-v4
objects. Immediately after the first original EDI and before the first
unchanged source SPH operation, it is created in parallel with the same
31-slot all-one merge, `COND 4.0`, `MICR ALL`, `SAVE`, and `MGEO CANDU6F`
inputs as `WHOLECELL0`, with this exact additional field clause:

```text
REAC 1 EFIS GOLVER
```

Immediately after `PRE172 := EDITION ;`, the day-300 `WCFIELD` update is the
existing-object EDI operation with its inherited flux/library/volume inputs,
`GOLVER`, and `SAVE`. It changes neither `WHOLECELL0` nor `EDITION`.

`EFIS` is the explicit fission-only energy-production extra edit. `GOLVER`
selects the official Golfier-Vergain coefficient route; it is not an extra
edit. The source writer produces the standard `DIFF` field from that route.
`REAC DIFF` is absent and `H-FACTOR` is never selected, decoded, or used as a
substitute. A source-emitted `H-FACTOR` record may still be present in the
MACROLIB payload and is ignored. No leakage model is enabled or inferred.

After the fourth source assertion only, the existing FILE-backed persistent
capture interface retains one selected object: `PRE0`/`PRE172` for regression
controls or `WCFIELD` for the field proof. It otherwise leaves source control
flow unchanged.

## Required selection and evidence boundary

The only later field selectors are:

| Source elapsed day | Retained output | Exact selector |
| ---: | --- | --- |
| 0 | `WCFIELD` | `REF-CASE0001/MACROLIB` |
| 300 | `WCFIELD` | `REF-CASE0002/MACROLIB` |

`PRE0` and `PRE172` remain regression controls and are never numerical field
sources. A later decoder must prove exactly two ordered groups, one mixture,
one-entry volume, no direct `SPH`/`SPH-EPSILON`/`ADF` subtree, fission-only
`EFIS`, standard `DIFF` in centimetres, and no prohibited route before reading
an array. The direct-subtree absence never describes the calculation history.

The required fresh evidence is the path-free
`reference/manifests/xsec-diff-01-route-v1.json` record. It binds the pinned
source/image identities, official guide and source-writer identities, the
eight-run source proof, semantic label/meaning outcomes, and the non-admission
boundary.

Applicable P1-T08 digest rows are `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They are methodology/applicability context
only and do not supply a numerical coefficient or golden authority.
