# XSEC-03-R1 - official JEFF lattice v2 reproduction

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `AGENTS.md`, current `PROJECT_SCOPE.md`,
`XSEC-DECK-01.md`, `xsec-jeff-lattice-deck-v2.md`,
`xsec-deck-01-source-admission-v2.json`, and XSEC-RIGHTS-01.

## Objective

Perform two independent fresh external DRAGON5 runs of
`xsec-c6-37-nu-jeff31-4ev-v2`, using only official TCWUX11/TCWU05Lib/assertS
source identities, then determine whether the source-created `res` ASCII
serialization contains every required v1 semantic field unambiguously. Create
only path-free derived evidence if and only if all source assertions, exports,
mapping invariants, and repeatability checks pass.

## Frozen boundaries

- Retain every TCWUX11 source lexeme and its four source assertions.
- Omit only the two approved SPH lines; use only a minimal external driver and
  read-only decoder of `res`.
- Use the pinned external JEFF binary and Dragon container/image/platform.
- Preserve the v1 field/unit/group/invariant mapping and reject a missing,
  ambiguous, nonfinite, nonconvergent, or incompatible source record.
- Keep all source/library/deck/listing/LCM/raw semantic artifacts external;
  do not create a runtime pack or alter repository code/data.

## Required evidence

For both fresh roots, verify exact hashes, container identity, source assertions,
all approved knots, 4 eV fast/thermal order, post-EDI/pre-SPH stage, required
fields and units, mapping invariants, and byte-identical semantic exports. T6 is
mandatory. Stop on any failure; do not edit source assertions or broaden deltas.

## Next routing

If valid, XSEC-03-R1 must report the reproduction outcome and route the separate
XSEC-CORE-01 static-core authority. If invalid, report the exact field/export/
run blocker without treating values as golden evidence.
