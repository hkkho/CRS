# XSEC-03 - DRAGON lattice candidate reproduction and semantic export

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `AGENTS.md`, `docs/Implementation_plan.md`, current
`docs/PROJECT_SCOPE.md`, `ROUND-2026-08-25-XSEC-DATA-CHAIN.md`,
`XSEC-RIGHTS-01-OWNER-APPROVAL.md`, and completed XSEC-02/XSEC-02-R1 evidence.

## Objective

Perform two fresh, isolated offline DRAGON5 reproductions of
`xsec-c6-37-nu-jeff31-4ev-v1`; create deterministic, path-free external run
records and semantic exports for every approved burnup knot; verify byte-level
repeatability and every approved source-to-Core mapping invariant. Raw library,
input deck, LCM dump, executable, and listing material remains external and is
not committed.

## Frozen technical authority

- Mapping: `docs/spec/xsec-source-runtime-mapping-v1.md` and
  `reference/manifests/xsec-02-mapping-spec-v1.json` at XSEC-02-R1.
- Version5 source commit: `eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`; pinned
  DRAGON executable, container, platform, JEFF-3.1/XMAS-172 WLUP/WILLIE input
  identities, and raw-artifact boundary: XSEC-01 records.
- Applicable P1-T08 rows: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`,
  `S5-R09`, and `S6-R03`.
- Exact TCWU11 and JEFF `TCWU05Lib` source identities, only three allowed deck
  deltas, two-group 4 eV ordering, post-EDI/pre-SPH export object, required
  source fields, transformations, and fail-closed invariants: XSEC-02 mapping.

## Bounded work and retention

All DRAGON5 input/deck generation, source-library conversion, container work,
LCM serialization, listings, semantic raw records, and plots occur in a unique
external temporary directory or a read-only/offline container mount. The
repository may receive only path-free run manifests, a derived validation
summary, this task report, and `PROJECT_SCOPE.md` after successful completion.
No raw JEF/JEFF/WLUP/DRAGON/DONJON artifact, source deck, result listing,
binary, LCM dump, or generated runtime pack may be retained in or copied into
the repository.

The source procedure must remain frozen except: resolve `TCWU05Lib` to the
pinned JEFF procedure; omit exactly the two specified `SPH` lines; and add only
non-mutating external ASCII serialization of the post-EDI/pre-SPH `EDITION`
object. No solver-control, material, geometry, tracking, depletion, collapse,
or export-semantic change is permitted.

## Required evidence and validation

For each fresh run, verify source/library/tool hashes, container/image/platform,
deck identity, source lexemes, every approved knot, group boundaries/order,
required source field/unit/mixture identity, convergence/iteration evidence,
and every mapping invariant. Produce a deterministic path-free manifest carrying
only approved derived identities/observables. Run twice with independent fresh
work roots and compare semantic-export bytes and manifests exactly. Report the
actual command/result hashes, any source limitations, and all rejected/absent
states. T6 is mandatory.

Stop on a data/license mismatch, unavailable converter/image, failed DRAGON
run, missing/ambiguous field, unknown LCM serialization semantics, group/order
mismatch, SPH-stage ambiguity, nonconvergence, NaN/Inf, invalid mapped value,
or repeatability mismatch. Do not repair physics or broaden deck deltas.

## Next routing

If and only if T6 succeeds, define `XSEC-CORE-01` as the separate static-core
topology/DONJON authority before `XSEC-04`. XSEC-03 does not make XSEC-04
eligible by itself and does not authorize a runtime pack or golden claim.
