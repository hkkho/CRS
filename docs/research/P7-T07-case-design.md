# P7-T07 literature-informed candidate comparison cases

## Decision

`P7-T07` creates three literature-informed **candidate case designs** from the
P1-T08 CANDU literature digest. They are comparison-ready case cards, not
executable reference decks and not approved golden data. The committed package
contains source metadata, source-reported candidate parameters, intended
observables, and an explicit admission-gap ledger; it does not contain the
external paper PDFs, DRAGON5/DONJON5 inputs, nuclear-data files, or generated
source outputs.

The cases are deliberately split into lattice and full-core stages:

| Case | Source | Intended use | Current disposition |
|---|---|---|---|
| `p7-t07-s5-lattice-37-bundle-crosscheck-v1` | S5, rows `S5-R02`, `S5-R03`, `S5-R04`, `S5-R06`, `S5-R09` | 37-element bundle lattice method plus DRAGON5/SERPENT cross-check | Candidate / deferred; exact source run is not reproducible from the paper alone |
| `p7-t07-s5-lattice-depletion-300d-v1` | S5, rows `S5-R03`, `S5-R04`, `S5-R05`, `S5-R07`, `S5-R08`, `S5-R09` | Constant-power depletion and two-group handoff to core calculations | Candidate / deferred; source histories and data assets are incomplete |
| `p7-t07-s1-fullcore-380-channel-300fpd-v1` | S1, rows `S1-R02` through `S1-R07`, `S1-R09`, `S1-R11` | CANDU-6 channel/bundle power, refuelling, burnup, reactivity, and xenon history | Candidate / deferred; exact DRAGON5/DONJON5 case and mapping are not supplied |

The machine-readable cards are in
[`data/comparisons/p7-t07-literature-cases-v1.json`](../../data/comparisons/p7-t07-literature-cases-v1.json).
Their status is `Candidate/Deferred/NoGolden`, their coverage class is
`LiteratureCandidate`, and their runtime use is prohibited until a separate
authority-admission task succeeds.

## What was taken from the papers

The case cards preserve only concise, source-attributed facts already indexed by
the committed P1-T08 digest:

- S5 supplies a 37-element CANDU bundle description, explicit material regions,
  a two-dimensional DRAGON5 lattice workflow, WIMSD4/JEFF-3.1-era
  multigroup-to-two-group methodology, depletion observables, and an
  independently described SERPENT comparison setup.
- S5 also supplies candidate depletion-history values: a 300-day constant-power
  cycle, 31.9713 kW/kg, and 9.6 GWd/t reported exit burnup. These are
  source-reported candidate evidence, not runtime constants or tolerances.
- S1 supplies a CANDU-6 full-core case shape: 380 channels, 12 bundles per
  channel, alternating coolant directions, a reported 8-bundle shift,
  4 channels/day, and a 300 full-power-day history. The exact refuelling
  direction remains `NotReported`. It also describes a
  DRAGON5-to-DONJON5 two-group workflow and 135Xe behavior.

The package does not infer missing values. `NotReported`, `NotProven`,
`NotRun`, and `NotAuthorized` are retained as explicit states where the paper
does not establish the necessary admission field.

## Comparison mapping

The cards identify possible future project quantities without claiming that a
mapping exists today:

| Candidate observable | Possible project quantity | Required proof before comparison |
|---|---|---|
| Lattice k-effective | `spatial.k` | Exact geometry, state, tool/data identity, normalization, and output definition |
| Lattice two-group coefficients | `spatial.coefficient_identity` | Complete condensation/homogenization map and coefficient ordering |
| Lattice fission rate | `spatial.fission_source` | Source reaction-rate definition and normalization |
| Depletion burnup | `burnup.current` | Energy/mass convention, step history, and bundle-state mapping |
| Depletion Xe-135 | `xenon.Xe135_inventory` | Initial inventory, yields/decay/absorption data, volume, and time history |
| Full-core channel/bundle power | `spatial.power` | 380-channel topology, bundle ordering, boundary conditions, and normalization |
| Full-core reactivity | `spatial.k` | Exact solver state and consecutive comparison definition |

Every mapping is marked `NotProven` in the artifact. The cards do not create a
new `QuantityId`, alter P2-T05 comparison rules, or claim `Direct` coverage.

## Authority and licensing boundary

P1-T08 remains the authority for the source identities, digest row IDs,
applicability classes, and admission gaps. P2-T04 remains the runtime authority
for equations, units, signs, state ownership, cadence, and I/Xe behavior.
P2-T05 remains the authority for observable identity, comparison records, and
coverage labels. P7-T06's approved synthetic artifact is unchanged and remains
the only bounded Phase 7 synthetic golden consumer.

The S5 article is identified in the manifest as CC BY 4.0, but its paper is not
copied into the repository. S1 is identified as CC BY-NC-ND 4.0; its source
artifact and any derived source run remain external. S2-S4 and S6 are retained
only as digest references in the exclusion notes for this task, with no source
artifact or derived output redistributed.

S2 is not emitted as a case because its reflector/optimization material lacks a
complete repository mapping and redistribution authority. S3 is excluded for
SCWR/CATHENA, safety, and full-thermal-hydraulics scope. S4 is excluded for
legacy DRAGON4/DONJON4 and thorium/advanced-cycle scope plus unresolved rights.
S6 is context for a separate RRS/device task, not a lattice/full-core case in
this package.

## Admission stop condition

No case may become an executable reference or golden comparison until a later
owner-authorized task proves all of the following for the selected source run:

1. exact source program/version/build and coupling identity;
2. exact nuclear-data file, processing, format, and checksum;
3. complete geometry/topology/mesh and source-to-project mapping;
4. initial state, depletion/refuelling history, event order, units, and
   normalization;
5. output quantity identity, schema, ordering, convergence/statistical evidence,
   and independent reproduction; and
6. lawful redistribution or retained-private-artifact authority for every
   required input and output.

Until then, source-reported numbers remain candidate evidence. The package must
not be used to tune the C# solver, select a tolerance, replace the P7-T06
synthetic artifact, or claim a CANDU production baseline.

[`docs/reference/candu-literature-digest-v1.md`](../reference/candu-literature-digest-v1.md),
[`reference/manifests/candu-literature-sources-v1.json`](../../reference/manifests/candu-literature-sources-v1.json),
the retained technical specifications, and the external sources listed in
this research note.
