# Curated CANDU literature digest v1

Status: candidate evidence index for P1-T08. This document is not a runtime
specification, a tolerance approval, or a golden-data approval.

Retrieved and reviewed: 2026-08-09  
Source manifest: [candu-literature-sources-v1.json](../../reference/manifests/candu-literature-sources-v1.json)

## Reading and use rules

The six records below were verified against primary PolyPublie pages and their
full-text artifacts. The committed repository contains only this digest and a
path-free manifest; the source PDFs remain external. A claim is source-reported
only when its row has a primary-source locator. `NotReported` means that the
review did not find the detail in the reviewed artifact; it is not permission to
infer a value from a nearby paper or from a private listing.

Each row has an evidence class and a permitted use class. The permitted use
classes are:

- `ContextOnly`: background or an explicitly out-of-scope model;
- `MethodologySupport`: a process or design pattern that may inform a later
  approved task;
- `CandidateCaseDesign`: a proposed shape of a future case, not an input;
- `CandidateNumericEvidence`: a source-reported number that remains blocked
  until the admission proof below is complete.

No row in this digest is promoted to an approved golden or runtime value. The
approved repository specifications remain authoritative when a source uses a
different equation, unit, normalization, tolerance, state definition, or scope.

## Source register

| ID | Verified identity | Access and redistribution | Artifact identity |
| --- | --- | --- | --- |
| S1 | Naceur & Marleau (2019), *Annals of Nuclear Energy* 124, 472–489, DOI `10.1016/j.anucene.2018.10.026`; PolyPublie 5048 | Full text; CC BY-NC-ND 4.0; external review only | 3,935,343 bytes; SHA-256 `513ffc1f9c6fe7b794c7514f46391567265fdfeff32cc27fca341096be858d59`; not committed |
| S2 | Mahjoub (2011), master's thesis, exact French title in the manifest; PolyPublie 714 | Full text; all rights reserved; no redistribution permission established | 4,660,696 bytes; SHA-256 `0c0ed163b4cf1b9b32aefc5dbebc19abd935bf0a9f6fea622733790a44b2d986`; not committed |
| S3 | Le Tennier (2021), master's thesis, exact French title in the manifest; PolyPublie 6643 | Full text; all rights reserved; no redistribution permission established | 11,293,533 bytes; SHA-256 `89be3d3eff09c3334c8e57a8068c36d47b6d9276afe6335ec5ce868b84c76bcf`; not committed |
| S4 | Holmes (2013), master's thesis, *Automated Refueling Simulations of a CANDU for the Exploitation of Thorium Fuels*; PolyPublie 1307 | Full text; all rights reserved; no redistribution permission established | 3,075,176 bytes; SHA-256 `b319a06a4b1d6a7df9cae8db5a3f547f767224fec01d7377bc2a8b20483df530`; not committed |
| S5 | Naceur & Marleau (2017 article / 2018 journal volume), *Annals of Nuclear Energy* 113, 147–161, DOI `10.1016/j.anucene.2017.11.016`; PolyPublie 5047 | Full text; CC BY 4.0 | 1,614,658 bytes; SHA-256 `ddece7b372064f294c7891f1a0dbc3c20d72ea4e6c8026b31b8399a12895b1e8`; not committed |
| S6 | St-Aubin & Marleau (2018), *Nuclear Engineering and Design* 337, 460–470, DOI `10.1016/j.nucengdes.2018.06.026`; PolyPublie 5143 | Full text; CC BY-NC-ND 4.0; external review only | 778,451 bytes; SHA-256 `1babf9ab06f29671ed6d501447ae5fe8a5be75c077cfb0c6fd6a56c88ec95823`; not committed |

Primary source pages and direct artifact URLs are recorded in the manifest. S1,
S5, and S6 identify official published versions; S2–S4 are institutional thesis
records. An accessible file is not the same as permission to redistribute it.

## Claim-level evidence index

Locators use the printed PDF page where the page differs from the repository
viewer page. Claims containing a number are source-reported and are not project
constants. A later task must re-check the artifact bytes and the locator before
using such a claim.

| Row ID | Source | Locator | Claim (paraphrased) | Evidence class | Use class | Applicability and limit |
| --- | --- | --- | --- | --- | --- | --- |
| S1-R01 | S1 | front matter, DOI and PolyPublie record | The work is a 2019 Naceur–Marleau CANDU-6 operation study in *Annals of Nuclear Energy* 124, with DOI `10.1016/j.anucene.2018.10.026`. | PrimarySourceMetadata | MethodologySupport | Identity only; not a model/version proof for this repository. |
| S1-R02 | S1 | abstract; §§2–4 | The study uses 3D DRAGON5–DONJON5 capabilities for CANDU-6 core follow-up with alternate cladding candidates. | PrimarySourceMethod | MethodologySupport | Tool family and workflow context; exact executable version/commit is not established here. |
| S1-R03 | S1 | §2.2, pp. 475–476; CANDU-6 supercell figures/tables | The lattice/supercell model represents CANDU fuel, coolant, pressure/calandria structures, moderator, and reactivity-device geometry. | PrimarySourceMethod | CandidateCaseDesign | The mapping is source-specific; it does not define the repository topology or node IDs. |
| S1-R04 | S1 | §2.1, printed p. 475 | The source reports a 172-group JEFF-3.1 WIMSD-4 library, generalized Stamm’ler self-shielding, a B1 homogeneous leakage model, and constant-power depletion at 31.9713 kW/kg. | PrimarySourceReportedNumeric | CandidateNumericEvidence | Every value is blocked pending the complete admission proof; no value becomes a runtime constant. |
| S1-R05 | S1 | §2.1, printed p. 475 | The reported workflow condenses and homogenizes data to two groups for subsequent full-core calculations. | PrimarySourceMethod | MethodologySupport | Crosswalk support only; exact reaction-rate definitions and normalization remain to be proven. |
| S1-R06 | S1 | introduction, printed p. 473 | The CANDU-6 model reports 380 channels with 12 bundles per channel, alternating coolant directions, and a channel-age follow-up model. | PrimarySourceReportedNumeric | CandidateCaseDesign | Source geometry/history only; no direct mapping to the compact repository fixtures. |
| S1-R07 | S1 | §§3–4, pp. 480–484 | The study implements an 8-bundle-shift on-power-refuelling scheme and reports a 4-channel-per-day operating history over a 300 full-power-day cycle. | PrimarySourceReportedNumeric | CandidateNumericEvidence | State/history, refuelling selection, units, and output definition must be reproduced before any use. |
| S1-R08 | S1 | §2.3 and results sections | RRS devices include six liquid-zone controllers and adjusters; the paper investigates their power-flattening effects and dynamic loss-of-regulation/recovery scenarios. | PrimarySourceMethod | ContextOnly | Dynamic and accident scenarios are context; this task does not authorize safety or accident implementation. |
| S1-R09 | S1 | depletion/results discussion | The source follows production and behavior of 135Xe during the reported depletion studies. | PrimarySourceMethod | MethodologySupport | It does not supply the approved I/Xe equations, yields, initial conditions, or repository tolerances. |
| S1-R10 | S1 | abstract; results and tables | Reported observables include channel/bundle power, reactivity, boron concentration, reactivity coefficients, fuel residence/burnup, and device response. | PrimarySourceMethod | CandidateCaseDesign | Observable identity, units, normalization, and state binding are not interchangeable with P2-T05 catalog entries. |
| S1-R11 | S1 | complete artifact review | Tool build identity, exact input deck, nuclear-data checksum, core mapping, output schema, and redistribution conditions are not a complete reproducibility package in the reviewed paper. | PrimarySourceLimitation | MethodologySupport | This is an explicit admission gap, not a reason to relax a repository gate. |
| S2-R01 | S2 | front matter, pp. 1–5 | The source is Mahjoub’s 2011 École Polytechnique de Montréal master’s thesis on generalized perturbation theory and stochastic algorithms for CANDU6 reflectors. | PrimarySourceMetadata | MethodologySupport | Institutional identity only; the thesis has no established redistribution permission. |
| S2-R02 | S2 | résumé/abstract, pp. 5–6 | The thesis studies natural-uranium CANDU6 reflector composition and flux/power flattening in the heavy-water reflector context. | PrimarySourceMethod | ContextOnly | Reflector optimization is not an approved runtime feature or equation. |
| S2-R03 | S2 | résumé, pp. 5–6; chapters on DONJON integration | Generalized perturbation theory is used to estimate zonal-power variations and a dedicated module is integrated into DONJON. | PrimarySourceMethod | MethodologySupport | Supports a possible sensitivity-analysis case; exact code version and coupling contract are NotReported. |
| S2-R04 | S2 | résumé, pp. 5–6; optimization chapters | Tabu-search and genetic algorithms are used with an objective based on flattening the power distribution. | PrimarySourceMethod | ContextOnly | Optimization algorithm choice is not approved by this task and must not alter deterministic runtime behavior. |
| S2-R05 | S2 | résumé, p. 5; liquid-zone discussion | The objective refers to the standard 14-zone liquid-controller definition for a CANDU6. | PrimarySourceReportedNumeric | CandidateCaseDesign | Source context only; exact zone-to-node map and detector/state binding are absent. |
| S2-R06 | S2 | methods and results | The source reports reflector/power observables but does not establish a repository-compatible nuclear-data identity, normalization, or reproducible golden artifact. | PrimarySourceLimitation | MethodologySupport | Missing information is NotReported, not inferred. |
| S2-R07 | S2 | complete artifact review | Exact DRAGON/DONJON versions, nuclear-data library/checksum, input geometry identity, output schema, and redistribution authority are not all established. | PrimarySourceLimitation | MethodologySupport | Candidate use remains blocked by the admission rule. |
| S3-R01 | S3 | front matter and PolyPublie 6643 record | The source is Le Tennier’s 2021 master’s thesis on CATHENA3-DONJON5 coupling for Canadian SCWR safety analysis. | PrimarySourceMetadata | ContextOnly | Canadian SCWR and safety scope is not the CANDU runtime scope. |
| S3-R02 | S3 | abstract | The thesis couples deterministic DONJON core diffusion with CATHENA thermal hydraulics using databases made with DRAGON. | PrimarySourceMethod | ContextOnly | Full thermal hydraulics and safety analysis are explicit non-goals. |
| S3-R03 | S3 | abstract, lattice-method section | The source reports an XMAS-172 mesh and a tracking choice of 9 azimuthal angles and 75 lines per centimetre as satisfactory for its study. | PrimarySourceReportedNumeric | CandidateNumericEvidence | SCWR-specific candidate settings; not a repository mesh, tolerance, or runtime constant. |
| S3-R04 | S3 | abstract and fuel-rod database method | To model a burnable absorber, each rod is split into 12 independent semicircular regions. | PrimarySourceReportedNumeric | CandidateCaseDesign | Geometry and poison representation are source-specific and not approved for CANDU core implementation. |
| S3-R05 | S3 | abstract and database-construction method | The perturbation database samples seven dimensions: up/down coolant density, up/down coolant temperature, fuel temperature, burnup, and moderator boron concentration. | PrimarySourceReportedNumeric | CandidateCaseDesign | These are SCWR interpolation dimensions; the repository feedback branch remains governed by its approved schema. |
| S3-R06 | S3 | abstract and coupling chapters | A CANDU5 temporal scheme and Python exchange of arrays/DONJON structures are used to manage the coupled calculation. | PrimarySourceMethod | ContextOnly | Coupling approach and thermal-hydraulic timing are out of scope for this task. |
| S3-R07 | S3 | abstract and conclusions | The source reports calculations lasting up to two months and evaluates maximum cladding surface temperature and other safety measures. | PrimarySourceReportedNumeric | ContextOnly | Runtime performance and MCST safety criteria do not expand project scope. |
| S3-R08 | S3 | complete artifact review | SCWR geometry, CATHENA version/build, thermal-hydraulic inputs, exact database files, and redistribution authority are not a CANDU golden-case proof. | PrimarySourceLimitation | ContextOnly | No CATHENA input or result is admitted to the repository. |
| S4-R01 | S4 | front matter, pp. 1–7; Theses Canada catalog cross-check, OCLC 1019473661 | The PolyPublie record and thesis title page identify Bradford Holmes’s 2013 master’s thesis on automated CANDU refuelling for thorium fuels. | PrimarySourceMetadata | ContextOnly | The title-page year is retained; the identified external catalog record was observed to use 2012, which remains an unresolved bibliographic discrepancy. |
| S4-R02 | S4 | abstract, pp. 6–7; lattice/full-core chapters | The work develops two-dimensional lattice and three-dimensional control-rod studies with DRAGON4 and full-core calculations with DONJON4. | PrimarySourceMethod | ContextOnly | Legacy tool versions and thorium scope are not approved runtime inputs. |
| S4-R03 | S4 | abstract; CANFUEL/refuelling chapters | CANFUEL simulates refuelling operations and the source reports a one-hundred-day simulation period. | PrimarySourceReportedNumeric | CandidateCaseDesign | Candidate history only; exact source inputs, event ordering, and output schema are not reproduced here. |
| S4-R04 | S4 | abstract and fuel-selection results | The source reports a 1% thorium choice with an 8-bundle shift as viable for its selected study. | PrimarySourceReportedNumeric | CandidateNumericEvidence | Thorium/advanced-fuel implementation is out of scope; all numerical use is blocked. |
| S4-R05 | S4 | refuelling and reactivity-insertion chapters | The work examines channel selection, zonal levels, refuelling response, and an adjuster-rod-11 withdrawal event. | PrimarySourceMethod | ContextOnly | Shutdown/accident and thorium behavior are not authorized runtime features. |
| S4-R06 | S4 | Monte Carlo validation, pp. 17–18 | The source compares SERPENT and DRAGON for a lattice case and reports its own neutron-count, cycle, and k-effective comparison settings. | PrimarySourceReportedNumeric | CandidateNumericEvidence | The independent model, library, geometry, and output normalization are not an approved repository baseline. |
| S4-R07 | S4 | complete artifact review | Exact input decks, DRAGON4/DONJON4 build identity, nuclear-data checksum, full-core mapping, and redistribution authority are not a complete admission proof. | PrimarySourceLimitation | ContextOnly | Candidate evidence remains fail-closed. |
| S5-R01 | S5 | front matter and PolyPublie 5047 record | The source is the official refereed Naceur–Marleau article, cited as a 2017 article in *Annals of Nuclear Energy* 113 (2018), 147–161, DOI `10.1016/j.anucene.2017.11.016`. | PrimarySourceMetadata | MethodologySupport | The article-year/volume-year distinction is retained rather than silently normalized. |
| S5-R02 | S5 | §2.1–§2.2, pp. 148–149 | The CANDU lattice model uses a standard 37-element bundle with explicit fuel, cladding, coolant, pressure-tube, helium-gap, calandria-tube, and moderator regions. | PrimarySourceMethod | CandidateCaseDesign | Source geometry is not a replacement for the repository topology specification. |
| S5-R03 | S5 | §2.4, pp. 149–150 | The simulation strategy uses DRAGON5, a 172-group WIMSD4-format library based on JEFF 3.1, a two-dimensional Cartesian cell with reflective boundaries, NXT tracking, generalized Stamm’ler self-shielding, and no leakage in the transport solve. | PrimarySourceMethod | MethodologySupport | These are source-method details; exact executable/library checksums and normalization are still required. |
| S5-R04 | S5 | §2.4, pp. 149–150 | The workflow produces two-group burnup-dependent reaction rates, diffusion coefficients, and condensed/homogenized macroscopic cross sections for core calculations. | PrimarySourceMethod | CandidateCaseDesign | It does not approve the project’s two-group equations, coefficient signs, or interpolation rules. |
| S5-R05 | S5 | §2.3–§2.4, pp. 149–150 | The source reports a 300-day burnup cycle at constant power of 31.9713 kW/kg and an exit burnup of 9.6 GW-day/tonne. | PrimarySourceReportedNumeric | CandidateNumericEvidence | Source-reported candidate values; output definition, units conversion, state history, and exact inputs require proof. |
| S5-R06 | S5 | §2.4, p. 150 and Table 5 | A SERPENT comparison is reported using 10,000 neutrons per cycle, 2,000 active batches, and 200 inactive batches, with differences in initial k-effective below 2 mk. | PrimarySourceReportedNumeric | CandidateNumericEvidence | Comparison evidence is not transferable without exact independent-model identity and statistical interpretation. |
| S5-R07 | S5 | §2.3, pp. 149–150, Eq. (1) | Enrichment is selected using a time-averaged k-effective criterion compared with a reference CANDU bundle; the source also discusses a homogeneous two-group alternative. | PrimarySourceMethod | MethodologySupport | The source criterion does not set a repository tolerance or golden reference. |
| S5-R08 | S5 | depletion/results, pp. 151–154 | The study observes isotope/depletion behavior including 135Xe and reports reactivity, absorption, spectral, and fission-rate comparisons across cladding cases. | PrimarySourceMethod | CandidateCaseDesign | Candidate observables require approved quantity identity, state binding, units, normalization, and gate ownership. |
| S5-R09 | S5 | complete artifact review | Exact repository-compatible input files, data checksum, output serialization, case mapping, and golden-data licensing proof are not supplied by this digest. | PrimarySourceLimitation | MethodologySupport | No numerical result is promoted. |
| S6-R01 | S6 | front matter and PolyPublie 5143 record | The source is the official refereed St-Aubin–Marleau 2018 article, *Nuclear Engineering and Design* 337, 460–470, DOI `10.1016/j.nucengdes.2018.06.026`. | PrimarySourceMetadata | MethodologySupport | Identity only; the source studies advanced cycles. |
| S6-R02 | S6 | abstract and §1, pp. 460–461 | The RRS includes adjuster rods and liquid-zone controllers; LZCs provide fine reactivity adjustment and spatial power control, while adjusters flatten flux and provide reserve. | PrimarySourceMethod | MethodologySupport | Device roles support terminology and case design; no runtime state transition is selected here. |
| S6-R03 | S6 | §2, pp. 461–462 | The reactor model solves a diffusion equation using two-group burnup-dependent cross sections and diffusion coefficients derived from lower-dimensional transport calculations. | PrimarySourceMethod | MethodologySupport | Crosswalk only; exact coefficients, units, interpolation, and boundary conditions remain governed by approved specifications. |
| S6-R04 | S6 | §2, pp. 461–462 | The source reports two identical XY LZC planes with three vertical controllers each, multi-compartment filling, detector/control-zone relationships, and source-specific bundle dimensions. | PrimarySourceReportedNumeric | CandidateCaseDesign | Device geometry and detector mapping are not a repository schema or golden fixture. |
| S6-R05 | S6 | §3, pp. 462–463 | The LZC response is modeled with iterative constrained optimization and explicit diffusion calculations coupled to quasi-static fuel evolution; perturbations include on-power refuelling and adjuster-bank withdrawal. | PrimarySourceMethod | CandidateCaseDesign | The method may inform a later bounded task but does not authorize safety limits or full RRS behavior. |
| S6-R06 | S6 | §2, pp. 461–462 | For this study, LZC response is assumed instantaneous and some response nonlinearities/local effects are neglected. | PrimarySourceMethod | MethodologySupport | This assumption must not be copied into runtime; it is an explicit applicability limitation. |
| S6-R07 | S6 | abstract, §§1–5 | The numerical examples target thorium/DUPIC/advanced cycles as well as natural uranium. | PrimarySourceLimitation | ContextOnly | Advanced-cycle implementation is explicitly out of scope. |
| S6-R08 | S6 | complete artifact review | Exact RRS input maps, device cross-section files, tool/build identity, state/history, normalization, and reproducibility package are not established by the article alone. | PrimarySourceLimitation | MethodologySupport | Candidate case design remains blocked until a dedicated admission task supplies the proof. |

## Physics-domain crosswalk

The row IDs below are evidence pointers, not approvals. A coverage gap is a
deliberate fail-closed result: it records why the literature cannot yet supply a
runtime or golden mapping.

| Domain | Applicable digest rows | Evidence/use interpretation | Coverage gap or NotApplicable reason |
| --- | --- | --- | --- |
| Topology and explicit indexing | S1-R03, S1-R06, S5-R02, S6-R04 | CANDU cell/core/device geometry can inform candidate case shapes. | Exact repository node/channel/bundle identities, flow-direction mapping, boundary labels, and topology digest are not supplied; no direct mapping. |
| Two-group neutronics | S1-R04, S1-R05, S5-R03, S5-R04, S6-R03 | Literature supports the existence of multigroup-to-two-group/homogenized workflows. | Exact equations, coefficients, group ordering, units, normalization, leakage/boundary policy, and interpolation contract must come from approved specifications and later evidence. |
| Burnup and refuelling | S1-R07, S4-R03, S4-R04, S5-R05, S6-R05 | Refuelling histories and source-specific burnup cases can shape future candidate designs. | Exact bundle history, event pre/post state, energy accounting, channel selection, and reproducible inputs are not admitted. Thorium and advanced-cycle rows are context only. |
| Kinetics and I/Xe | S1-R09, S4-R05, S4-R06, S5-R08 | Source studies mention depletion, isotope inventories, xenon, and in one case kinetics comparisons. | No approved I-135/Xe-135 equations, yields, decay/absorption data, initial conditions, delayed-neutron contract, or runtime output mapping is established; coverage gap blocks direct use. |
| Liquid-zone controllers | S1-R08, S2-R05, S6-R02, S6-R04, S6-R05 | Device roles, zone concepts, and perturbation methodology support terminology and candidate cases. | Exact device-to-node map, cross sections, actuator state machine, cadence, saturation, and ownership proof are absent. |
| Adjusters | S1-R08, S4-R05, S6-R02, S6-R05 | Adjuster effects and withdrawal/refuelling perturbations are described. | No approved adjuster geometry, overlay map, command/event contract, timing, or safety behavior is admitted. |
| Poison and feedback | S1-R04, S1-R08, S1-R10, S3-R05, S6-R03, S6-R05, S6-R06 | Boron/poison and feedback-related perturbation ideas are visible in source methods. | SCWR thermal-hydraulic dimensions, advanced-cycle poison choices, source assumptions, and exact overlay maps are not transferable; full thermal hydraulics and safety are NotApplicable. |
| Golden-case generation and validation methodology | S1-R04, S1-R11, S5-R03, S5-R06, S5-R07, S5-R09, S6-R03, S6-R08 | Versioned tools, nuclear data, homogenization, independent comparison, convergence, and limitation reporting are useful evidence patterns. | Every candidate case still lacks one or more exact tool/build identity, data checksum, geometry/state history, unit/normalization, output schema, reproducibility, and redistribution proofs. No source row is a golden oracle. |

## Candidate numeric admission record

The following source rows contain numbers or numeric comparison settings. They
are not executable inputs. The table records the metadata needed to understand
whether a number is technically comparable and lawfully reusable; it does not
create an approval or review workflow.

| Candidate rows | Required admission proof | Current disposition |
| --- | --- | --- |
| S1-R04, S1-R06, S1-R07 | Exact DRAGON5/DONJON5 program version and build; nuclear-data library/version/checksum; geometry and homogenization map; state/history and refuelling events; authoritative units/conversions; normalization; output definition and field identity; independent rerun/reproducibility; license and redistribution authority. | Blocked; source-reported candidate only. |
| S2-R05 | Exact DONJON module/version; nuclear data; 14-zone geometry and detector map; state, depletion/refuelling history, initial conditions, and event ordering; units and power normalization; output definition; repeatable input/output identity; licensing. | Blocked; source-reported candidate only. |
| S3-R03, S3-R04, S3-R05 | Exact DRAGON/DONJON/CATHENA versions; nuclear data and database checksum; SCWR geometry/mesh/tracking and interpolation state; state, depletion/refuelling history, initial conditions, and event ordering; units/normalization; output definition; independent reproduction; licensing. | Context-only for this project; cannot be admitted to runtime. |
| S4-R03, S4-R04, S4-R06 | Exact DRAGON4/DONJON4/SERPENT versions; nuclear data/DRAGLIB identity; thorium geometry; state, depletion/refuelling history, initial conditions, and event ordering; units/normalization; output definition; independent rerun; licensing. | Blocked and out of runtime scope; candidate/context only. |
| S5-R03, S5-R05, S5-R06, S5-R07 | Exact DRAGON5/SERPENT versions/builds; WIMSD4/JEFF data identity/checksum; 37-element geometry and two-group homogenization; depletion state/history; units and k-effective/burnup normalization; output definitions; independent rerun and statistical method; licensing. | Blocked; source-reported candidate only. |
| S6-R04, S6-R05 | Exact DONJON/transport versions and device-data identity; nuclear-data library/version/checksum; LZC/adjuster geometry and map; state, depletion/refuelling history, initial conditions, and perturbation/event ordering; units/normalization; output definition; repeatable full-core evidence; licensing. | Blocked; advanced-cycle assumptions are not runtime authority. |

## Agreements, conflicts, and open questions

- S1 and S5 both describe deterministic DRAGON-to-core workflows, 172-group
  WIMSD/WIMSD4-style data, two-group/homogenized quantities, burnup, and
  independent comparison ideas. The overlap supports a methodology crosswalk,
  not a merged input deck; tool revisions, case definitions, and outputs still
  differ.
- S1 and S5 report the same constant-power value of 31.9713 kW/kg in their
  respective source methods, but a repeated number is not independent
  confirmation. It remains candidate evidence until exact units, state history,
  normalization, and input identity are admitted.
- S4 uses DRAGON4/DONJON4 and thorium; S1/S5 use DRAGON5-family workflows; S3
  couples DONJON5 to CATHENA3 for an SCWR. These version and physics-scope
  differences prohibit silent substitution.
- S1/S4/S6 discuss 8-bundle or 4-bundle shifts in different fuel/cycle contexts.
  The shift size is a case parameter, not a repository-wide refuelling rule.
- The PolyPublie record and thesis title page identify S4 as 2013. The identified
  Theses Canada catalog cross-check (OCLC 1019473661) was observed to use 2012
  for related catalog metadata. The discrepancy is retained as unresolved; the
  digest does not silently rewrite the primary record.
- Exact executable versions/commits, nuclear-data checksums, complete source
  input decks, source-to-runtime topology maps, output schemas, normalization
  conventions, and lawful redistribution status remain open for every proposed
  numeric admission.
- No reviewed source supplies a complete, repository-compatible golden case for
  the CANDU runtime. Existing compact reference artifacts remain synthetic or
  limited according to their recorded provenance.

## Later use

Use the row IDs when they make a physics or data decision easier to trace.
Preserve private/raw artifact boundaries and record enough source, unit,
normalization, and tool identity to reproduce exported runtime data. Literature
may inform a case design; by itself it does not define a runtime equation,
tolerance, unit, normalization, or golden value.
