# P7-T06 source report - Phase 7 kinetics/I-Xe authority search

## Decision summary

Research date: 2026-08-24
Task: `P7-T06`
Applicable literature-digest rows: `S1-R09`, `S4-R05`, `S4-R06`, `S5-R08`

The external-reference route is **not admitted**. The bounded search found
useful CANDU/PHWR kinetics and I/Xe model descriptions, but no public package
that simultaneously exposes the exact input deck, output history, model/tool
version, nuclear-data identity, geometry/topology, units, normalization,
convergence settings, and redistribution rights needed to bind the frozen
P2-T04/P7 contracts. Several CANDU sources explicitly describe their results as
illustrative or omit important plant controls. The available official NEA
benchmark packages are well-defined but cover PBMR or BWR cases rather than a
Phase 7 CANDU/PHWR case.

The task therefore selects the alternate route authorized by the P7-T06
routing record: a project-authored manufactured/synthetic package derived only
from the already approved runtime contracts and existing synthetic fixture
conventions. It is not a CANDU physics baseline, a published numerical
authority, or a production tolerance source.

## Frozen case requirements checked

The case must bind the existing Phase 7 observables without changing runtime
code: delayed-neutron amplitude and precursor histories; explicit I-135/Xe-135
inventories and number densities; fission-rate-density and actual group-flux
forcing; node volume; xenon absorption overlay; explicit time-step and event
ordering; state/data identity; deterministic replay; nonnegative inventories;
balance diagnostics; and manifest byte/hash identity.

The governing runtime and unit authority remains the retained
`docs/spec/kinetics-xenon-rrs-feedback-v1.md` specification. This report does not select
new equations, nuclear constants, units, tolerances, normalization rules, or
production values. The synthetic generator will reproduce the frozen
left-endpoint Euler contracts independently and will not call `ReactorSim.Core`
to manufacture expected values.

## Claim-to-source ledger

Access dates are 2026-08-24. URLs are preserved exactly as accessed. “Not
admitted” means the source can inform context or case-search triage but cannot
serve as the runtime or golden-data authority for this task.

| ID | Source and authority class | What it establishes | Exact-case / rights audit | Disposition |
|---|---|---|---|---|
| `P7-R01` | [CNS, “Xenon Transients Simulation Using the Reactor Code DONJON”](https://proceedings.cns-snc.ca/index.php/pcns/article/view/2978), 1998 conference record | CANDU spatial-kinetic xenon work; local bundle I/Xe concentrations and an improved quasistatic method are described in the abstract. | The public record exposes an abstract and a PDF link, not a complete input/output package, nuclear-data identity, topology, or reproducible manifest. DONJON remains an offline reference tool; no runtime source is imported. | Context/candidate only; not admitted. |
| `P7-R02` | [CNS, Javidnia and Jiang, “MATLAB/SIMULINK Model of CANDU Reactor for Control Studies”](https://proceedings.cns-snc.ca/index.php/pcns/article/download/5796/5795), 2006 university conference PDF | Gives a PHWR/CANDU-like nodal I/Xe equation structure, tabled illustrative parameters, and example power histories. | The paper states that the data are from an Indian PHWR, that the simulations do not exactly represent CANDU-core transient behavior, and that liquid-zone controllers are not modeled. It does not expose a license for redistribution of a complete case package or exact machine-readable outputs. Its parameters cannot become project golden values. | Background/method cross-check only; not admitted. |
| `P7-R03` | [CNS, “Enhancements in FMDP Spatial-Control Algorithm and Xenon Time Search”](https://proceedings.cns-snc.ca/index.php/pcns/article/download/1372/1372/1408), 1990 conference PDF | Confirms the historical FMDP/XEMAX CANDU xenon-transient and liquid-zone-control context. | The accessible paper describes code modules but does not provide a complete public case, output manifest, nuclear-data identity, or redistribution terms for the underlying tool/data. | Context only; not admitted. |
| `P7-R04` | [OECD/NEA PBMR coupled neutronics/thermal-hydraulics transient benchmark](https://harbor.oecd-nea.org/jcms/pl_20496/pebble-bed-modular-reactor-pbmr-coupled-neutronics/thermal-hydraulics-transients-benchmark-pbmr-400-core-design) | Official benchmark page describes common cross sections, multidimensional transient exercises, and an archive containing xenon-dependent libraries and interpolation material. | This is a PBMR/HTGR benchmark, not CANDU/PHWR. The package is distributed through NEA Databank request, so it is not an accessible in-repository exact case and has no approved mapping to the frozen CANDU topology/data contracts. | Negative comparator; not admitted. |
| `P7-R05` | [OECD/NEA BWR turbine-trip benchmark volume](https://harbor.oecd-nea.org/upload/docs/application/pdf/2019-12/nsc-doc2006-23.pdf) | Shows an official benchmark pattern in which supplied xenon number densities and microscopic cross sections are overlaid on a xenon-free base. | The case is BWR, not CANDU; its supplied distributions and model package are not a Phase 7 CANDU authority. The absorption-overlay pattern is contextual and does not authorize a new runtime value or tolerance. | Context/negative comparator only; not admitted. |
| `P7-R06` | [IAEA WIMS-D technical-document draft](https://nds.iaea.org/wimsd/download/tecdocdraft_old.pdf) | Provides background I-135/Xe-135 data references in a reactor-physics documentation context. | No complete Phase 7 transient history, topology, exact normalization, or project-compatible manifest is supplied. The document cannot be used to select runtime constants or golden values. | Background only; not admitted. |
| `P7-R07` | [IAEA TECDOC-1348](https://www-pub.iaea.org/MTCD/Publications/PDF/te_1348_web.pdf) | Provides general reactor-physics and fission-product context relevant to I/Xe behavior. | It is not an exact CANDU Phase 7 input/output case and does not provide a reproducible package mapped to the frozen state/data contracts. | Background only; not admitted. |
| `P7-R08` | [IAEA technical report TE-2024](https://www-pub.iaea.org/MTCD/Publications/PDF/TE-2024web.pdf) | Describes a high-power/low-power xenon observation procedure and a post-change observation window. | The public report does not expose the raw CANDU-compatible numerical history, exact model/data identity, geometry, or a redistributable case package. A protocol is not a golden trace. | Context only; not admitted. |
| `P7-R09` | [OSTI CEND-3932](https://www.osti.gov/servlets/purl/4628095), historical CEXE documentation | Documents time-dependent iodine/xenon input and output capabilities in a historical reactor code. | The artifact is a historical code manual, not a current reproducible Phase 7 case with exact inputs/outputs and clear project redistribution rights. No private code or input is imported. | Historical context only; not admitted. |
| `P7-R10` | [Public CANDU-6 simulator repository](https://github.com/wcharczuk/reactor) | Claims a CANDU-6 simulator with six-group kinetics and xenon transients. | The public repository page does not expose a license in the visible repository files, and its scope includes SCRAM, diesel, thermal-hydraulic, and other excluded behavior. No exact validation case or data manifest is supplied. | Excluded; not admitted. |
| `P7-R11` | [Nexus phase-0 simulator repository](https://github.com/gregory82gr/Nexus-1-phase-0) | MIT-licensed educational simulator with a documented kinetics layer. | The repository explicitly labels its constants representative and not tuned/validated to a specific reactor; its live examples are PWR/SMR-oriented. It is not an exact CANDU case and is not used as data. | Excluded; not admitted. |
| `P7-R12` | [Open Nuclear Engineering Teaching Suite](https://github.com/open-nuclear-suite/OpenNuclearSuite) | MIT-licensed teaching simulators with classroom kinetics and CSV export. | The project identifies simplified teaching/LWR scope and says outputs are qualitative, not engineering predictions. It does not supply the required CANDU Phase 7 case identity or mapping. | Excluded; not admitted. |

### Digest-row relationship

The required `P1-T08` rows `S1-R09`, `S4-R05`, `S4-R06`, and `S5-R08` were
used as literature-search anchors and applicability context. None is promoted
to an executable oracle. The sources above remain a separate source ledger so
that background evidence, candidate evidence, and approved runtime/golden
authority are not conflated.

## Case-mapping and reproducibility gap matrix

| Required field | Public CANDU/PHWR evidence found | Admission result |
|---|---|---|
| Exact model/tool/version | DONJON, FMDP/XEMAX, and MATLAB/Simulink are named in individual records | No single public, versioned case package binds all model components. |
| Nuclear-data identity | Some papers mention source tools or table illustrative parameters | No exact data-library/version/processing identity is exposed for a redistributable case. |
| Geometry/topology and boundary conditions | Nodal descriptions and illustrative zone counts occur in papers | No complete machine-readable topology and boundary package mapped to the project state identity. |
| Units and normalization | Equations/tables expose partial units and normalizations | Not sufficient to bind every stored value and observable without inference. |
| Input histories | Published power/load histories and plots exist | Raw numerical histories, step schedule, and state/event identity are not all available. |
| Output histories | Plots/scalars and qualitative behavior are available | No exact machine-readable I/Xe/kinetic/overlay history with byte identity. |
| License/redistribution | CNS/IAEA/NEA publication pages are publicly readable | Publication access does not grant redistribution of omitted code/data or private station inputs. |
| Runtime mapping | No source provides the project’s P2-T04/P7 state/data/volume/digest contract | Any complete mapping would require invented or privately obtained data. |

Because the missing fields are critical to a golden/reference admission, the
external route is closed as `NOT ADMITTED / DEFERRED`, not silently converted
into an inferred baseline.

## Search log and stopping rule

Primary queries used:

- `site:iaea.org CANDU xenon transient kinetics benchmark I-135 Xe-135 data`
- `site:publications.gc.ca CANDU xenon transient I-135 Xe-135 reactor kinetics`
- `PHWR CANDU xenon oscillation benchmark numerical results precursor iodine xenon original paper`
- `DRAGON5 xenon equilibrium depletion benchmark public input output IAEA`
- `site:oecd-nea.org xenon transient benchmark iodine xenon input files reactor kinetics`
- `site:nea.fr xenon benchmark reactor transient iodine xenon benchmark data`
- `site:iaea.org reactor kinetics benchmark xenon iodine transient input deck`
- `site:osti.gov CANDU xenon iodine transient benchmark input data`
- `site:proceedings.cns-snc.ca xenon CANDU pdf iodine xenon oscillation numerical`
- `site:proceedings.cns-snc.ca CANDU xenon transient model PDF`
- `site:iaea.org "CANDU" "I-135" "Xe-135" pdf`
- `site:publications.gc.ca "Xe-135" CANDU pdf`
- `public GitHub iodine xenon reactor kinetics benchmark input output dataset license`
- `site:github.com CANDU xenon transient reactor kinetics open source`
- `site:github.com iodine xenon depletion benchmark reactor kinetics`
- `site:iaea.org CANDU xenon transient reproducible benchmark input`

Search stopped after the same gap recurred across CNS/IAEA/NEA/OSTI and public
source-repository lanes: CANDU-specific material lacked the complete
redistributable case, while complete official benchmark packages were for
other reactor classes or required separate package access. Further search would
not repair the missing exact mapping without a new external artifact or owner
decision. The manufactured route is therefore the bounded, reproducible option
inside this task.

## Synthetic-route boundary

The follow-on package is project-authored and uses only:

- approved P2-T04/P7 left-endpoint Euler equations and state ownership;
- existing synthetic fixture-scale values already used by P7 contract tests;
- explicit node volumes, state/data identities, fission-rate and flux forcing,
  and a fixed timestep schedule authored in the task definition; and
- independent arithmetic and manifest generation outside `src/ReactorSim.Core`.

The package must be labeled `Synthetic` in every artifact and consumer.
Exact deterministic consumption is a regression/contract check, not a physical
validation tolerance. It must not be presented as a CANDU reference case or
used to activate temperature/purity behavior.

Source: the retained kinetics specification, the CANDU literature digest,
and the external sources listed in this research note.
