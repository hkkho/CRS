# Literature-guided geometry and coefficient revision

The active pack is `candu6-two-group-diffusion-v1-literature-geometry-v4`.
This revision uses the supplied geometry/cross-section PDF and checks its public
sources. It is a static curve fit and finite-core gameplay calibration, not a
transport-generated cross-section library or plant validation.

## Sources and fit

- [Rouben, AECL, CANDU Fuel Management Course (2003), section 1.1](https://www.nuceng.ca/canteach-rev2/library/20031101.pdf):
  square pitch 0.28575 m, bundle length 0.4953 m, twelve bundles per channel.
- [Naceur and Marleau, Annals of Nuclear Energy 113 (2018), 147-161](https://publications.polymtl.ca/5047/11/2018_Naceur_Neutronic_analysis_accident_tolerant_cladding.pdf):
  Tables 1-2 give the reference pin/tube geometry; Table 5 and Fig. 3 provide
  the UO2-Zr reflective lattice comparator. The reference uses DRAGON5/JEFF-3.1,
  natural uranium, and 31.9713 kW/kg. The fresh value 1.118047 is tabulated;
  subsequent points are graph-read, with an estimated +/-0.003 reading allowance.
- [IAEA-TECDOC-1319, printed p. 107](https://www-pub.iaea.org/MTCD/Publications/PDF/te_1319_web.pdf):
  independent cross-source context, integral-average k of 1.045 at 7.1 MWd/kg
  for standard 37-element fuel. This is not instantaneous k at that burnup.

The linked website required authentication. The user supplied the original
seven-page PDF; its baseline pack SHA-256 matches `source-pack.json` exactly.
The original public source pages and curve were checked on 6 October 2026.

The node volume is now `pitch^2 * length = 0.04044276185625 m3`, replacing
0.05 m3. Interior conductances use `C = D A / d`, with common effective D in
both spatial directions: 0.04 m fast and 0.02 m thermal. Those D values are
authored coupling choices, not sourced material constants. The effective
outer-face conductance is fitted, not a physical vacuum boundary condition.
The 380-channel stepped topology and twelve axial positions remain unchanged.

The fit keeps absorption and downscatter at their previous authored values.
It assigns effective nu = 2.45 to both groups, retains fast fission, and derives
thermal neutron production from the two-group infinite-medium k equation.
Thermal fission follows from `Sigma_f = nuSigma_f / nu`. These are positive
coefficients with absorption greater than fission at every knot. The constant
nu choice is an assumption; the reference k curve cannot uniquely determine
all group constants. The reference's temperature/density and group-condensation
conditions are not additional runtime capabilities.

There are 15 reference knots through 9.59139 MWd/kg, followed by six authored
tail knots through 30 MWd/kg. Tail k is sampled from
`0.954 * exp(-0.018 * (B - 9.59139))`. Runtime lookup remains linear and fails
outside the closed domain. No depletion accuracy is claimed for the tail.

| Quantity | Previous v3 | Updated v4 |
| --- | ---: | ---: |
| Fresh bare-table k infinity | 1.075166 | 1.118047 |
| Descending k=1 crossing, MWd/kg | 3.483656 | 6.586088 |
| Mean k from 0 to 7.1 MWd/kg | 1.002930 | 1.046030 |
| RMSE against 15 fit targets | 0.046625 | numerical roundoff |
| Fixed-shape burnup loss, mk/FPD | 0.498666 | 0.412585 |
| Total zone worth, mk | 7.001876 | 6.998482 |

Near-zero target residual is in-sample fit agreement, not independent validation.
The separate IAEA integral comparator differs by +0.001030 in k, but source
conditions differ. Never present it as a matched-condition bias measurement.

## Finite core and poison limitations

The offline seed-1001 fit changes boundary conductance, not lattice production,
to recover half-fill criticality. Zone slopes are adjusted to retain about 7 mk
total worth. The accepted interior coupling multiplier is one. Tight independent
solves give half-fill k = 1.0000008124. The calibration solve gives 6.639 MW peak
channel power and 684.2 kW peak bundle power at 2,064 MW thermal.

The reference curve includes depletion poisons, whereas the existing gameplay
xenon uses a fixed startup rebase and a dynamic perturbation. Their combination
does not separate poison effects: especially for fresh fuel, a loss can appear
in both the fitted burnup curve and dynamic xenon. The PDF's lattice comparison
excludes the dynamic overlay. A poison-separated dataset is required before
claiming realistic transient/refuelling response. This revision improves the
metric geometry and static burnup curve, not all physics simultaneously.

Power normalization, masses, 190-FPD channel-cycle convention, refuelling, group
ordering and browser/shared-simulation boundary remain unchanged. Changed pack
identity intentionally invalidates incompatible saved-run replay.

## Reproduction

`data/calibration/literature-geometry-v4` contains the immutable source pack,
source metadata, candidate, accepted proposal and fit measurements.

```powershell
python tools/tune_literature_pack.py
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-literature data/calibration/literature-geometry-v4/candidate.json data/calibration/literature-geometry-v4/fit
# Review proposal and matching zone slopes before staging future revisions.
tools/Sync-PhysicsPacks.ps1 -Stage
dotnet run --project tools/AgedCoreBenchmark -c Release -- --reactivity-scale benchmarks/literature-geometry-v4-reactivity.json
python tools/build_candu6_summary_pdf.py
```

The PDF builder reads the active embedded pack, archived baseline, reference
points and core audit. It checks the analytic k formula against a matrix
eigenvalue at every knot, absorption/fission inequalities, closed domain,
metric volume and target residual. Outputs are under `output/pdf`.

Verification on 7 October 2026 includes 114 passing Core tests, 74 passing Game tests, 37 passing
Browser bridge tests, 149 passing frontend tests and both normal and `/CRS/`
production builds. The shared serialized wire fixtures were regenerated for
the intentionally changed pack identity and coefficients.

The local Pages-shaped reproduction matrix is retained in
`benchmarks/literature-geometry-v4-browser.json`: six central/peripheral/flow
cases over two samples, twelve accepted refuels, identical cold/warm matrix
digests and no console or page errors. This uses a Release **non-AOT** WASM
build. The build's commit field is the base HEAD; these are working-tree
changes, not evidence that the published Pages site contains this revision.

The calibration fitter reproduces the accepted proposal byte-for-byte. Its
tool build has zero warnings/errors. The PDF is six pages, rendered and
visually inspected; all three public-source links are embedded. The complete
local `/CRS/` smoke passed: challenge completed with eight useful bundles and
a badge, empty retry missed its objective, seed retry/reset worked, and free
practice returned correctly. Studio also passed refuelling, history/session
preservation, fresh-bundle poison and three viewport checks. The result is
`benchmarks/literature-geometry-v4-smoke.json`.

Before/after numerical metrics and active/baseline hashes are retained in
`benchmarks/literature-geometry-v4-comparison.json`. The scripts used were
`tools/Test-DotNet.ps1 -Suite Core`, `-Suite Game`, `tools/Test-Browser.ps1`,
`tools/Build-BrowserWasm.ps1`, `npm run build:pages`, and the existing
`scripts/smoke.mjs` and `scripts/benchmark-wasm.mjs` against the local `/CRS/`
preview. No published deployment, AOT performance or 100-day fuelling capability
claim is implied by these checks.
