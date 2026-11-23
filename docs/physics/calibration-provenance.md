# Calibration and model provenance

The game loads an embedded copy of the project-authored [two-group physics pack](../../data/packs/candu6-two-group-diffusion-pack-v1.json). The [xenon calibration](xenon-calibration.md) describes the capture and worth fit, and the [geometry calibration](../../data/calibration/diffusion-v10/README.md) documents the geometry fit and 100-day comparisons. These calibrations describe a surrogate gameplay model.

## Model inputs

| Input | Basis and use |
| --- | --- |
| Fuel lattice | Rouben's CANDU fuel-management course gives 0.28575 m lattice pitch and 0.4953 m bundle length. The authored lattice-cell volume is 0.04044276185625 m³. The v4 fuel curve fits the static Naceur and Marleau natural-uranium lattice multiplication trend with an authored positive tail through 30 MWd/kg HM. The group constants are a surrogate, and the source does not supply a separately resolved xenon history. |
| Fixed adjusters | Twenty-one fully inserted rods use the IAEA-TECDOC-1994 geometry. Overlap-weighted axial and lateral spans add fast and thermal absorption to nearby nodes. The nominal v10 fit targets 17 mk of total worth. |
| Included xenon | The pack's fuel coefficients include a xenon contribution, and its separate burnup-indexed reference table records the corresponding xenon density. The solver replaces that reference contribution with live bundle xenon. The fresh knot is zero; nonzero knots use local equilibrium at 31.9713 kW/kg HM. See the [xenon calibration](xenon-calibration.md). |
| Liquid zones | Six vertical assemblies contain fourteen compartments based on St-Aubin and Marleau. The absorber calculation follows bottom-up water filling. The fourteen power-measurement regions are a separate mapping. The nominal v10 water fit targets 7 mk of total worth. |
| Radial boundary | The calibrated circle has a physical envelope radius of 3.35 m plus a 2.2 m effective reflector distance. Exposed radial face conductance uses the distance to this zero-flux boundary. See the [radial comparison](../../benchmarks/radial-circle-v9-2026-10-30/README.md). |
| Axial boundary | The [geometry fit](../../data/calibration/diffusion-v10/README.md) uses zero-flux extrapolation 0.22 m beyond the axial faces and effective diffusion coefficients of 1.5 cm fast and 0.75 cm thermal. |

The [literature register](../reference/candu-literature-digest-v1.md) and [source manifest](../../reference/manifests/candu-literature-sources-v1.json) identify underlying references. See [solver equations](active-two-group-solver.md), [regulation and poison](../code-library/theory/regulation.md), and [aged-core starts](aged-core-starts.md) for current runtime behavior.

## Reproduction records

- [Axial sensitivity before v8](../../benchmarks/axial-boundary-2026-10-10/README.md) and [v8 acceptance](../../benchmarks/axial-marshak-v8-2026-10-10/README.md)
- [Local absorber audit](../../benchmarks/bundle-absorption-2026-10-10/README.md) and [v9 radial comparison](../../benchmarks/radial-circle-v9-2026-10-30/README.md)
- [V4 fuelling trials](../../benchmarks/fuelling-capability-2026-10-07/README.md) and [v10 fit and campaigns](../../data/calibration/diffusion-v10/README.md)

Each report retains its own pack, cadence, seed and scoring context. Use the [release guide](../maintenance/hosting.md) for current acceptance checks.
