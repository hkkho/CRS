# Local absorber power and browser locations

The physical expectation is correct: neutron absorbers reduce nearby flux and
therefore fission power, with the final shape also depending on fuel composition,
xenon, diffusion, controller compensation and total-power normalization.
[Introduction to CANDU Processes, printed pp. 76–79](https://canteach.candu.org/Content%20Library/20042610.pdf)
describes LZC compartments near zone centres, light-water absorption, and adjusters
flattening flux by absorbing neutrons centrally.
[CNSC Reactor Physics, printed p. 161](https://canteach.candu.org/Content%20Library/20030101.pdf)
notes that withdrawing adjusters makes the overall flux more peaked. These sources
support the phenomenon, not a quantitative calibration of this authored pack.

## Pre-change v6 comparison, 2026-10-08

`PracticeAdjusterTests.InsertedRodsHaveSeventeenMkWorthAtFixedInventoryAndHalfZones`
first compared spatial powers using the previous xenon-reference-v6 pack:
seed 1001 aged fuel, identical frozen equilibrium xenon overlay, fixed half-filled
zones, unchanged leakage and fuel coefficients, and 2,064 MW thermal in both
solves. Only the pack's adjuster block is removed for the comparison; no controller
compensation or xenon re-equilibration is allowed. The retained worth assertion
also checks approximately 17 mk. All figures below are model results.

| Adjuster axial centre from End A | Mean power depression in overlapping cells |
| --- | ---: |
| 2.22885 m | 16.60195% |
| 2.97180 m | 17.90798% |
| 3.71475 m | 17.03247% |

Each percentage is `100 * (1 - sum(P_inserted) / sum(P_removed))` on the same
set of intersecting fuel cells, counted once. Equal total power means the removed
power is redistributed elsewhere; the claim is not that every fuel cell decreases.
The test checks depression separately at all three planes.

For example, channel 74's positions 4–9 (one-based), in kW thermal:

| Position | Inserted | Removed |
| --- | ---: | ---: |
| 4 | 492.710 | 503.042 |
| 5 | 492.255 | 556.608 |
| 6 | 497.769 | 570.458 |
| 7 | 494.073 | 567.302 |
| 8 | 481.094 | 547.086 |
| 9 | 495.962 | 510.638 |

The inserted curve is flattened, with shallow local dips rather than deep notches.
Comparing neighbouring positions with different fuel histories alone cannot isolate
the absorber effect. Both the power curve and the device absorption are averages
over pitch-width, bundle-length cells, not pointwise flux near rod surfaces.

## Confirmed limitation in the previous regional model

With rods and xenon held fixed, increasing Z4 alone from 50% to 100% fill reduces
the power summed over its 288 cells by **0.98948%** at fixed core thermal power.
This confirms regional absorption changes the power shape.

The previous `PracticeLiquidZoneRrsMappingV1` applied effective absorption to every
node in each region. It does not locate the individual LZC tubes, their water/air
segments or a moving water surface. It therefore cannot produce a distance-to-tube
dip or a dip tied to a particular tube's axial plane. This is a spatial-model
limitation, not evidence that the absorption term is missing. Matching total zone
worth is insufficient to validate local bundle power around the devices.

The [v7 tube model](lzc-tubes-v7.md) addresses this limitation with localized
compartment geometry, bottom-up water filling, conserved overlap, and a new
criticality/device-worth fit. The same controlled test now checks power depression
in the newly wetted tube cells. Device effects remain fuel-cell averages; a
centimetre-scale flux notch at the physical absorber surface is not resolved.

## Retuned v7 comparison

The final tube model passes the same frozen-inventory comparison. Adjuster-cell
power depression is 16.67550%, 17.93628% and 17.10172% at the three axial planes.
Increasing Z4 from 50% to 100% changes absorption in only **16 newly wetted tube
cells**, and their summed power falls **4.80722%** at the same 2,064 MW total.
Cells outside the tube footprint receive zero LZC absorption. The compartment
volume and bottom-up filling tests also pass. These results confirm the local
effect at the model's fuel-cell resolution, without claiming physical tube-surface
flux accuracy.

## Interface contract

Game derives immutable rod coordinates and affected-channel/bundle lists from
`PracticeAdjustersV1` when the active pack has a nonzero adjuster block. Browser
serializes them in `core.adjusters`, including core replacements; compact responses
that retain the core also retain this geometry. TypeScript validates the geometry
and only displays it. The face projects the three planes onto seven transverse
columns; the plan shows all 21 rods separately. Selected-channel power graphs
shade overlapping cells and mark actual rod centres, including the middle plane
between bundle positions 6 and 7. The inspector names the authoritative LZC
compartment IDs/fills, draws the localized tube geometry and current water levels,
and explicitly describes the fuel-cell averaging approximation.
