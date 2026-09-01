# XSEC source mixture selection v1 - blocked whole-cell locator decision

**Status:** BLOCKED. This is a bounded negative selection decision for the
offline XSEC candidate only. It selects no mixture, numerical coefficient,
runtime data, golden/reference case, release artifact, or Unity content.

## Decision

No source mixture locator is approved for
`PRE0/REF-CASE0001/MACROLIB` or `PRE172/REF-CASE0002/MACROLIB`.

The exact hash-matched official JEFF `TCWUX11.c2m` procedure's first `EDI`
call (lines 96–102) directs `MERG REGI` to map 31 source regions into the ten
distinct mixture identifiers `1` through `10`; it does not merge the complete
annular cell into one named mixture. Both hash-bound external capture states
contain a `L_MACROLIB` with two coarse groups and ten mixtures, and its
`VOLUME` record has ten entries. Neither selected direct MACROLIB contains a
mixture-name record that could distinguish an approved whole-cell mixture.

The official DRAGON data-structure guide defines a MACROLIB as cross sections
for a *set* of mixtures, its state-vector mixture field as the number of those
mixtures, and its `VOLUME` record as the volume associated with each mixture.
The EDITION data-structure guidance identifies `REF:MATCOD` as the mixture
number associated with each original region. These definitions demonstrate
that the observed ten entries are separate homogenized mixture records, not a
source-named whole-cell record. The existing mapping-v1 requirement for one
explicitly named homogenized whole-cell mixture therefore has no compatible
source locator in the approved mapping-v2/deck-v3 capture boundary.

## Boundaries preserved

- Both original SPH calls, all source assertions, the mapping-v2 two-state
  boundary, group order, units, field transformations, and exclusions remain
  unchanged.
- No mixture is selected by index, array order, volume, material inference,
  field value, or post-processing. No numerical field has been decoded.
- This decision does not change the official source procedure or authorize a
  new `EDI` merge, whole-cell homogenization, group structure, source control,
  tolerance, schema, converter, pack, Core/CLI/Unity behavior, DONJON case, or
  golden/reference admission.

## Bound source evidence

| Evidence | Identity / locator | Decision relevance |
| --- | --- | --- |
| Official JEFF procedure | Version5 `TCWUX11.c2m`, canonical-LF SHA-256 `9d1865089741aecdd145a167cf7ea729760b4b4f1744a7b9c045480065371813`; first EDI lines 96–102 | `MERG REGI` has 31 assignments to IDs 1–10, not one whole-cell assignment. |
| Official IGE-351 EDITION guide | `SectDedition.tex`, SHA-256 `4f1738a149befcceeead88dc13207f43afbb76bf2a403c13fee3774dc6b19c08`; lines 27–33, 152–158 | Defines homogeneous-mixture count and region-to-mixture `REF:MATCOD` semantics. |
| Official IGE-351 MACROLIB guide | `SectDmacrolib.tex`, SHA-256 `7003338d47986bcbbea5a4fead19c97a13a62543cc715439e8e3a022a84723d2`; lines 3–10, 32–37, 166–167 | Defines multi-mixture MACROLIB and per-mixture `VOLUME` semantics. |
| PRE0 capture | SHA-256 `d40a67dba6a91e33179a2ecef8386723a78530669563efa750f90eb25344a572`; `REF-CASE0001/MACROLIB` | Two groups, ten mixtures, ten volumes, no direct mixture-name record. |
| PRE172 capture | SHA-256 `71dee765bf872a37865844a8fa006f62ce86bfa0a750f25ca6bbe822f35328f3`; `REF-CASE0002/MACROLIB` | Same structural condition as PRE0. |

The path-free structural checks and source-record locators are recorded in
`reference/manifests/xsec-mix-01-selection-v1.json`. Raw procedures, guides,
captures, LCM serializations, listings, and all numerical cross-section values
remain external-only.

## Follow-up boundary

`XSEC-03-R2` must not be rerun under this specification. If a whole-cell
candidate remains desired, define a new source/deck and mapping that explicitly
creates and names one whole-cell homogenized output, establishes its source
semantics, retains useful source-regression evidence, and supplies a fresh
capture/re-run plan. Do not reuse a mixture selected from these ten records or
treat this blocked decision as field admission.
