# Calibration and model provenance

The game loads the project-authored [v10 diffusion pack](../../data/packs/candu6-two-group-diffusion-pack-v1.json) from an embedded copy. The [active calibration report](../../data/calibration/diffusion-v10/README.md) contains the fit, measured device worths and 100-day comparisons. These are surrogate gameplay measurements.

## Inputs carried into v10

| Input | Basis and use |
| --- | --- |
| Fuel lattice | Rouben's CANDU fuel-management course gives 0.28575 m lattice pitch and 0.4953 m bundle length. The authored lattice-cell volume is 0.04044276185625 m³. The v4 fuel curve fits the static Naceur and Marleau natural-uranium lattice multiplication trend with an authored positive tail through 30 MWd/kg HM. The group constants are a surrogate, and the source does not supply a separately resolved xenon history. |
| Fixed adjusters | Twenty-one fully inserted rods use the IAEA-TECDOC-1994 geometry. Overlap-weighted axial and lateral spans add fast and thermal absorption to nearby nodes. The nominal v10 fit targets 17 mk of total worth. |
| Included xenon | The v6 accounting assigns an analytic burnup-indexed reference to the included-poison fuel table. The solver replaces that reference with live bundle xenon. Its proxy history starts at zero and was generated at 31.9713 kW/kg HM. |
| Liquid zones | Six vertical assemblies contain fourteen compartments based on St-Aubin and Marleau. The absorber calculation follows bottom-up water filling. The fourteen power-measurement regions are a separate mapping. The nominal v10 water fit targets 7 mk of total worth. |
| Radial boundary | V9 introduced a circle at the physical envelope radius of 3.35 m plus 2.2 m effective reflector distance. Exposed radial face conductance uses the distance to that zero-flux circle. |
| Axial boundary | V8 used a Marshak zero-incoming-current condition. V10 uses zero-flux extrapolation 0.22 m beyond the axial faces and effective diffusion of 1.5 cm fast and 0.75 cm thermal. |

The [literature register](../reference/candu-literature-digest-v1.md) and [source manifest](../../reference/manifests/candu-literature-sources-v1.json) identify underlying references. See [solver equations](active-two-group-solver.md), [regulation and poison](../code-library/theory/regulation.md), and [aged-core starts](aged-core-starts.md) for current runtime behavior.

## Reproduction records

- [Axial sensitivity before v8](../../benchmarks/axial-boundary-2026-10-10/README.md) and [v8 acceptance](../../benchmarks/axial-marshak-v8-2026-10-10/README.md)
- [Local absorber audit](../../benchmarks/bundle-absorption-2026-10-10/README.md) and [v9 radial comparison](../../benchmarks/radial-circle-v9-2026-10-30/README.md)
- [V4 fuelling trials](../../benchmarks/fuelling-capability-2026-10-07/README.md) and [current v10 fit and campaigns](../../data/calibration/diffusion-v10/README.md)

Each report retains its own pack, cadence, seed and scoring context. Use the [release guide](../maintenance/hosting.md) for current acceptance checks.
