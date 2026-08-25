# P7-T08 literature-calibrated synthetic case package

## Decision

`P7-T08` creates four reproducible, project-authored synthetic cases calibrated
against small published targets or an independent unit identity. They are
approved only for Golden-data audit and report-regression use. They are not
executable DRAGON/DONJON reproductions, a CANDU physics baseline, a production
tolerance source, or a Core runtime mapping.

The official source records and the accessible thesis expose useful model
descriptions, scripts, tables, and headline values. They do not expose the
complete external inputs needed for an authoritative rerun: exact builds,
nuclear-data files/checksums, full geometry/composition, initial state and
event history, output schema/order, and an independently reproduced result.
For example, the Holmes refuelling script names external `Candu6_*.x2m`,
`.result`, CPO, data, result, and archive directories; the paper does not
package those databases with the thesis. The existing P7-T07 admission gap is
therefore preserved rather than silently reclassified.

## Evidence boundary

| Evidence class | Meaning in this package |
| --- | --- |
| `ReportedTarget` | A concise value or range printed by the source, retained with a paper locator. It is not a tolerance or runtime constant. |
| `ComputedTheory` | A deterministic unit/energy identity independently calculated from reported inputs. It is not a source output. |
| `SyntheticCalibrated` | A project-authored deterministic output generated from the frozen definition by the standalone P7-T08 tool. |
| `ReproducedReference` | Not present. No external solver rerun is claimed. |

Applicable digest rows are listed in the definition and generated artifact:
`S1-R02`, `S1-R04`, `S1-R06`, `S1-R07`, `S1-R09`, `S1-R10`, `S1-R11`,
`S4-R02`, `S4-R03`, `S4-R04`, `S4-R06`, `S4-R07`, and `S5-R03` through
`S5-R09` as applicable per case. P2-T04 remains the authority for runtime
kinetics/Xe equations; this task does not change them.

The source records remain external: [S1 PolyPublie record](https://publications.polymtl.ca/5048/),
[S5 PolyPublie record](https://publications.polymtl.ca/5047/), and the
[Holmes thesis](https://publications.polymtl.ca/1307/1/2013_BradfordHolmes.pdf).
The S1 license is recorded as CC BY-NC-ND, S5 as CC BY, and the Holmes thesis
retains author copyright. No source PDF, nuclear-data file, input deck, or
full source output is copied into the repository.

The explicit licensing decision is facts-only attribution: the approved
artifact contains project-authored synthetic values and concise attributed
facts, while source bytes, figures, full tables, input decks, nuclear data, and
source-result payloads remain external. No derived source run is claimed and no
source artifact/output is redistributed. Digitization, full-table
redistribution, or a raw input/output bundle remains permission-gated.

## Case matrix and comparison results

### S4 lattice k-effective cross-check

The printed Holmes Table 3.1 reports `k_eff = 1.73509 +/- 0.00042` for
SERPENT and `1.72972` for DRAGON. The synthetic result is the minimax midpoint
`1.732405`, which gives `+2.685 mk` relative to DRAGON and `-2.685 mk` relative
to SERPENT; the absolute relative difference to either printed value is about
`0.1552%`. The midpoint minimizes the maximum absolute distance to the two
reported methods; it does not reproduce either method and is not a physical
acceptance criterion. The paper's printed `0.2%` error label is retained as
source text, while the difference recomputed from the printed k values is not
used to select a tolerance.

### S5 lattice depletion and burnup handoff

The source reports 300 days at `31.9713 kW/kg` and a rounded exit value of
`9.6 GWd/t`. The independent identity used by the generator is:

```text
burnup [GWd/t] = power [kW/kg] * days * 24 h/day / 24000 kWh/kg per GWd/t
```

This produces `9.59139 GWd/t`, an absolute difference of `0.00861 GWd/t` and
relative difference of about `0.0897%` from the rounded published value. The
full synthetic history contains every day from 0 through 300 at constant
reported power. The source's reported initial k-difference upper bound and
six printed comparison values are retained as targets only; no k tolerance is
invented.

### S1 CANDU-6 schedule and alternate-cladding summary

The synthetic history uses the reported 380 channels, 12 bundles/channel,
8-bundle shift, 4 channels/day, and 300 full-power days. It therefore contains
1200 scheduled channel events and 9600 shifted bundles under the explicit
synthetic convention of one eight-bundle shift per refuel event.

The reported summary targets are represented as a small calibrated comparison:
`+3.4 mk` SiC reactivity change, a midpoint `+1.25 ppm` inside the reported
`+1.1` to `+1.4 ppm` enriched-boron range, and `-246 kW` enriched channel-power
change. These are reported-summary replays, not extracted source curves or a
mapping to the compact repository Core.

### S4 full-core refuelling aggregate fixture

The Holmes Table 5.2 aggregate simulation values are replayed exactly as a
synthetic data-contract fixture. The corresponding time-average reference
values remain alongside them, so the report exposes the source's own
reference-to-simulation differences rather than hiding them. The deterministic
100-day schedule has 408 events, exactly `4.08 channels/day`, and eight bundles
per event. This is a reproducible fixture for provenance/table handling, not a
DONJON4/CANFUEL result.

## Iteration and improvement record

1. The initial candidate design was checked against the P7-T07 cards and the
   P1-T08 digest. It was rejected as a direct external golden because the
   executable case packages were absent.
2. The lattice case was improved from a one-sided target copy to the explicit
   minimax midpoint, so its maximum distance to the two printed k values is
   minimized and visible.
3. The depletion case was improved by replacing a copied rounded burnup with
   an independent constant-power energy calculation and a complete daily
   history.
4. The full-core cases were improved by making channel/refuelling schedules
   fully deterministic and complete, with exact event totals and source
   locators.
5. Review hardening added recursive finite-number checks, exact sequential
   `0..N` history bounds, actual source-manifest hash verification, and
   deterministic PDF metadata. Two consecutive PDF renders were byte-identical.
6. No further tuning is source-backed. Changing an equation, runtime constant,
   tolerance, or published target to reduce a remaining difference would cross
   the authority boundary and is intentionally not performed.

## Generated artifacts

- Definition: `data/comparisons/p7-t08-literature-calibrated-definition-v1.json`.
- Candidate: `data/comparisons/p7-t08-literature-calibrated-synthetic-v1.json`
  with status `Candidate/Deferred/NoGolden`.
- Approved synthetic-only package: `data/golden/p7-t08-literature-calibrated-synthetic-v1.json`
  with status `Approved/ApprovedGolden`.
- Both manifests bind the definition bytes, artifact bytes, case count, and an
  exact independent regeneration hash.
- The standalone generator has no `ReactorSim.Core` reference, verifies the
  actual literature source-manifest bytes, and rejects non-finite or
  non-sequential history data. Golden tests audit the package and invariants
  only; no production/runtime mapping is introduced.
- The final report PDF is a deterministic seven-page artifact with SHA-256
  `14b210408cfcfa081ae4273a8557de61bce3b721ab054c720fd9289e7977aee8`.

## Admission stop condition

An external or production claim remains blocked until a separately authorized
task supplies exact source build identity, nuclear-data identity/checksums,
complete geometry/state/history, units and normalization, output schema/order,
independent rerun evidence, and lawful handling of every required artifact.
The current approved status means only that the synthetic package is stable,
traceable, and useful for deterministic test/report regression.
