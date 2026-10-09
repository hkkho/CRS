# Localized LZC tubes, v7

The active `candu6-two-group-diffusion-v1-lzc-tubes-v7` practice model replaces
region-wide liquid-zone absorption with six localized vertical assemblies,
fourteen compartments, and bottom-up water filling. The fourteen power-measurement
regions are unchanged and independent of absorber ownership. Fuel channels and
bundle inventory remain intact; devices add absorption to overlapping fuel/moderator
cells without substituting a fuel bundle.

## Geometry and approximation

[St-Aubin and Marleau 2018, Figs. 1–2, printed pp. 461–462](https://publications.polymtl.ca/5143/2/2018_St-Aubin_Candu-6_reactivity_devices_optimization_advanced.pdf)
shows two axial planes, each with left/right two-compartment assemblies and a
central three-compartment assembly. The axial planes lie between bundle positions
3/4 and 9/10 (one-based). Transverse centres are at −6, 0 and +6 lattice pitches.
The paper distinguishes tube location, compartment boundaries and measured region
boundaries. They need not coincide.

The implementation reads the diagram on its pitch lattice and rounds vertical
endpoints to integer pitch boundaries. These heights are authored approximations,
not exact engineering dimensions obtained from the IAEA supplementary workbook.
With pitch 0.28575 m and bundle length 0.4953 m:

| Compartment | Centre x / pitch | Bottom y / pitch | Top y / pitch |
| --- | ---: | ---: | ---: |
| Z1 / Z8 | −6 | −7 | 1 |
| Z2 / Z9 | −6 | 1 | 9 |
| Z3 / Z10 | 0 | −10 | −4 |
| Z4 / Z11 | 0 | −4 | 4 |
| Z5 / Z12 | 0 | 4 | 11 |
| Z6 / Z13 | +6 | −7 | 1 |
| Z7 / Z14 | +6 | 1 | 9 |

Z1–Z7 are at z = 3 bundle lengths from the first bundle boundary, Z8–Z14
at z = 9 lengths. Each homogenized assembly footprint is one pitch wide and
one bundle length deep. Equal transverse and axial overlap splits it across two
fuel columns and two bundle positions; the complete footprint covers 424 cells
and conserves 106 homogenized cell volumes. This does not equate the physical
water-cylinder volume to the homogenization volume.

For a compartment extending from `y_bottom` to `y_top`, the water surface is
`y_bottom + fill * (y_top - y_bottom)`. Absorption uses exact intersection with
each cell, including partially wetted cells. It is zero outside tube footprints
and above the water surface. Total water overlap is proportional to fill;
individual-cell absorption is piecewise linear as the surface crosses rows.
At a fixed fill there is no independently simulated hydraulic transient.

[IAEA-TECDOC-1994, section 3.2.4, printed pp. 15–17](https://www-pub.iaea.org/MTCD/Publications/PDF/TE-1994web.pdf)
supports the incremental-cross-section device-volume approach. Its supplementary
`Device_specs.xlsx` contains exact coordinates and cross sections but was not
retrieved in this change. Structural/air absorption, water scattering and
device transport-generated increments are not modeled; the fast/thermal
absorption ratio of 0.1 and fitted strengths remain an authored surrogate.

## Offline retune

The [fit](../../data/calibration/lzc-tubes-v7/fit/fit.json) archives all iterations
and compartment coordinates; [source](../../data/calibration/lzc-tubes-v7/source-pack.json)
and [proposal](../../data/calibration/lzc-tubes-v7/fit/proposal.json) preserve the
before/after pack. Fitting changes only water absorption strength, adjuster strength,
and boundary leakage. Fuel coefficient knots, included xenon reference, masses,
energy per fission, interior diffusion coupling and fixed 2,064 MW total remain
unchanged. Actual xenon is re-equilibrated between fitting passes and held fixed
for each endpoint worth comparison. Independent tighter solves settle xenon again
and verify the final worth and criticality.

| Quantity | Verified value |
| --- | ---: |
| Full-footprint fast water strength | 0.005554638539404282 m⁻¹ |
| Full-footprint thermal water strength | 0.055546385394042816 m⁻¹ |
| Empty-to-full LZC worth | 6.999934 mk |
| Total inserted-adjuster worth | 16.999995 mk |
| Seed 1001 half-fill Keff | 1.000000519 |
| Fast boundary conductance | 0.0007875467350470583 m² |
| Thermal boundary conductance | half the fast value |
| Seed 1001 initial peak channel | 5.886106 MW thermal |
| Seed 1001 initial peak bundle | 547.643646 kW thermal |

These are model results, not measured plant coefficients. No all-seed guarantee
or long-run fuelling capability follows from this seed-specific fit.

The regenerated [channel reference](../../benchmarks/lzc-tubes-v7-reference/reference.json)
has 380 targets totaling 2,064 MW thermal, spanning 4.931437–5.784966 MW per
channel. The model and reference identities deliberately differ from v6.

```powershell
dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-lzc-tubes data/calibration/lzc-tubes-v7/source-pack.json tmp/lzc-tube-refit
# Review proposal and the two water strengths before staging another revision.
tools/Sync-PhysicsPacks.ps1 -Stage
```

`PracticeLiquidZoneRrsIdentityV1` supplies the fitted water strengths; the mapping
digest includes strengths and water-column/cell geometry. The initial controller
Jacobian remains a regional heuristic and is corrected by measured diffusion
secants. The active pack and channel-reference identities change deliberately;
old saved-run digests and scoring references are incompatible with this model.

## Browser presentation and validation

Core owns the moving-water overlap. Game projects immutable tube geometry,
and Browser publishes it in `core.liquidZoneTubes`. The interface draws cyan
compartment outlines and current water levels, switches between End A/End B
planes, separates the six assemblies in plan view, and shades tube-adjacent
bundle positions on the power graph. The UI calculates drawing coordinates only;
it does not calculate neutron absorption or power.

Focused tests check compartment volume conservation at six fills, axial splitting,
zero absorption outside footprints, bottom-up movement, independent compartment
control, geometry digest changes, local power depression with frozen fuel/Xe,
and full/compact bridge geometry. The existing calibrated-worth, deterministic
control, refuelling, inventory, terminal and browser smoke/reproduction checks
remain acceptance requirements. [Local absorber power](local-absorber-power.md)
records the motivating comparison and the scope of the spatial resolution.

The [verification record](../../benchmarks/lzc-tubes-v7-verification.json) records
122 Core, 74 Game, 38 Browser and 152 frontend passes, production/Pages builds,
the complete AOT `/CRS/` smoke, 320px device layout and two identical six-row
[reproduction matrices](../../benchmarks/lzc-tubes-v7-browser.json), with no browser
errors. These are working-tree measurements at the base commit, not deployment
evidence. CI uses optimized Release checks with the same assertions; the test
runners also retain their local Debug default.
