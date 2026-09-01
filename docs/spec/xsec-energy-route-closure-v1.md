# XSEC fission-energy route closure v1

**Status:** closed route; no runtime admission

**Task:** `XSEC-ENERGY-06`

## Decision

Formally close the current project route for a source-proven homogenized
macro fission-only `ENERGIE F.` field under the pinned Version5 source
identity, approved deck v6, and mapping v5. The route is closed because the
completed, distinct source investigations do not prove the required macro
writer/operator and target field contract.

This is a bounded project decision. It does not claim that every DRAGON5 or
DONJON5 release, private source configuration, future artifact, or different
mapping can never provide such a field.

## Scope boundary

- Version5 source: commit
  `eee582f8594d3b7d01b65660ca1e8bef00eb6b2a`.
- Project source contract: approved JEFF lattice deck v6 and
  `xsec-source-runtime-mapping-v5.md`.
- Target: homogenized macro fission-only `(n,f)` energy field named
  `ENERGIE F.`/`EFIS` by the active mapping.
- Reference programs: DRAGON5 and DONJON5 remain offline reference tools;
  neither is a runtime dependency and no reference source is ported.

## Option decision

| Option | Evidence-based assessment | Decision |
|---|---|---|
| Investigate another macro route | The pinned source investigations already cover the SAPHYB/WCFIELD macro contract and the distinct MPO/SPHMPO lead. The remaining observed target label is not a proven macro writer, and another search without a new primary-source artifact/operator would repeat rejected evidence or invite an invented mapping. | Not selected now. Re-entry requires a genuinely new source lead. |
| Formally close the route | The predecessor findings agree on the same critical gap: no source-proven macro `ENERGIE F.` writer/operator with target output identity, inputs/weights, order, units, normalization, addressing, and reproducibility. | Selected. |

## Frozen evidence matrix

| Task | Frozen disposition | Finding preserved |
|---|---|---|
| `XSEC-ENERGY-02` | `BLOCKED` | Paired SAPHYB/WCFIELD capture reached the expected structure, but the selected macro mixture did not expose the required `RDATAX`/`EFIS` output. An input-MACROLIB seed was not authorized. |
| `XSEC-ENERGY-03` | `BLOCKED` | The pinned source route matrix found the SAPHYB macro branch reading source-defined `H-FACTOR`; the target `ENERGIE F.` operation was only established for microscopic `MEVF * NFTOT`, with no proven macro writer/operator. |
| `XSEC-ENERGY-04` | `BLOCKED` | The distinct source research attempt found no qualifying macro writer/operator. Its completion receipt is frozen evidence and is not rerun or rewritten here. |
| `XSEC-ENERGY-05` | `BLOCKED` | The distinct MPO/SPHMPO branch contains a fission-energy reaction path but constructs/consumes source-defined `H-FACTOR`, not a complete mapping-v5 macro `ENERGIE F.` record. Its completion report remains in its isolated worktree and is used only as a frozen handoff. |

The statuses above are historical evidence, not a new technical `PASS` and
not a replacement for any predecessor report.

## Runtime limitation

No direct source-proven homogenized macro `ENERGIE F.`/`EFIS` coefficient is
available for runtime consumption under mapping v5. Therefore:

- no runtime component, data pack, golden baseline, or Unity adapter may claim
  direct Version5 macro fission-energy provenance;
- no runtime implementation may decode or alias `H-FACTOR` or `PRODUCTION` as
  `ENERGIE F.`/`EFIS`;
- no runtime implementation may manufacture the field by aggregating
  microscopic `MEVF * NFTOT`, adding capture/gamma energy, applying a leakage
  or direct correction, relabeling a source record, or fabricating an input
  MACROLIB seed; and
- the existing approved synthetic/ReducedModel runtime scope is unchanged.
  This closure does not remove that scope, add external CANDU authority, or
  change any runtime equation, unit, schema, tolerance, or golden value.

The limitation is an admission boundary, not an implementation of a fallback
energy model. A future runtime use of a source-derived macro energy field is
not eligible until the re-entry criteria below are satisfied.

## Re-entry criteria

A future implementation may reopen the route only with a pinned, lawfully
usable primary-source artifact/operator that proves all of the
following without inference from labels:

1. the actual macro writer and consumer, including the output object/record;
2. the fission-only `(n,f)` input fields, isotope/material and spatial
   addressing, weights, and operation order;
3. units, conversion, normalization, and flux/volume correspondence;
4. deterministic reproduction and source/build/version identity;
5. compatibility with deck v6 and mapping v5, or an explicitly versioned
   successor mapping with focused numerical checks; and
6. legal source/data use and a rights boundary that keeps raw artifacts
   external.

Any future implementation must preserve the frozen evidence above, reproduce
its source and numerical decoding deterministically, and avoid rewriting the
historical `XSEC-ENERGY-02` through `XSEC-ENERGY-06` findings.

## Provenance, rights, and literature boundary

The source context is the official Version5 landing/source and guide set, with
the exact source commit above. The official guides
and source are provenance/semantics evidence only; they do not create a
missing output writer. `XSEC-RIGHTS-01` permits bounded internal offline use
of lawfully available derived inputs, while raw nuclear data, converted
libraries, executables, source/output records, private paths, and guide PDFs
remain external or non-redistributed.

Applicable P1-T08 rows are `S1-R04`, `S1-R05`, `S1-R11`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`. They provide methodology, provenance, or
limitation context only. They do not authorize a new equation, value, unit,
normalization, mapping, runtime behavior, or golden result.
