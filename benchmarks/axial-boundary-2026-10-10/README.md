# Axial boundary sensitivity

The [review](../../docs/physics/axial-boundary-review.md) connects the public
fuel-management and reactor-physics sources to the active boundary code and
this controlled experiment.

![Axial boundary comparison](axial-boundary-comparison.png)

Eight full-core solves use the same seed-1001 aged inventory, frozen xenon and
LZC levels, radial boundaries and 2,064 MW thermal normalization. Axial leakage
alone is varied, with all adjusters in and out. Every solve converged below
2e-7 residual. The current branch reproduces the previous M11 comparison.
The stronger-boundary cases are sensitivity experiments and have not been
refitted or staged as a runtime pack.

- [Raw solve data](axial-boundary-audit.json)
- [Bundle values](axial-boundary-bundles.csv)
- [Vector figure](axial-boundary-comparison.svg)
- [Plot source](plot_axial_boundary.py)

Reproduce:

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --axial-boundary-audit benchmarks/axial-boundary-2026-10-10/axial-boundary-audit.json
python -X utf8 benchmarks/axial-boundary-2026-10-10/plot_axial_boundary.py
```
