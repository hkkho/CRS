# CANDU literature used by the game model

The [source manifest](../../reference/manifests/candu-literature-sources-v1.json) records document identities, artifact checksums and access conditions. The current [calibration notes](../physics/calibration-provenance.md) explain how CRS uses these sources.

| Source | Relevant evidence |
| --- | --- |
| Rouben, CANDU Fuel Management Course, section 1.1 | The 0.28575 m lattice pitch and 0.4953 m bundle length set the authored cell volume. [Course](https://www.nuceng.ca/canteach-rev2/library/20031101.pdf) |
| Naceur and Marleau, *Annals of Nuclear Energy* 113, 147–161, Tables 1–2 and 5, Figure 3 | Lattice geometry and the static natural-uranium multiplication trend inform the fuel curve. Their constant-power depletion at 31.9713 kW/kg informs the game's included xenon proxy. The paper does not supply an independently resolved xenon history for that curve. [Paper](https://publications.polymtl.ca/5047/11/2018_Naceur_Neutronic_analysis_accident_tolerant_cladding.pdf) |
| St-Aubin and Marleau, *Nuclear Engineering and Design* 337, 460–470, section 2 and Figures 1–2 | Six vertical controllers, compartment filling and detector regions inform the separate absorber and measurement mappings. [Paper](https://publications.polymtl.ca/5143/2/2018_St-Aubin_Candu-6_reactivity_devices_optimization_advanced.pdf) |
| IAEA-TECDOC-1994, printed page 20 and Table 10 | Fixed adjuster geometry informs the modeled overlaps. The strength is fitted to the game's device worth target. [Report](https://www-pub.iaea.org/MTCD/Publications/PDF/TE-1994web.pdf) |
| Zhang and Rouben, CANDU Fuel Management, section 3.2, and the RFSP INSTANTAN chapter | Dwell-averaged lattice coefficients inform the fixed power reference. Channel age and dwell exposure inform seeded aged starts. [Course](https://www.nuceng.ca/canteach-rev2/library/20054411.pdf), [RFSP chapter](https://www.nuceng.ca/canteach-rev2/library/20054311.pdf) |

The source geometry and methods inform an authored game model. The committed
pack defines the runtime coefficients, normalizations and solver tolerances.
External analysis programs are development references.
