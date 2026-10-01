# Zone geometry and worth audit — 30 September 2026

This is the **pre-calibration audit**. See [the applied calibration](zone-calibration.md)
for the current 6.5 mk model and valid empty-zone states.

The pre-calibration default absorber footprint is too broad and its worth too high.
The fourteen measured power regions cover the whole core; they were also used
as the absorption footprint. Every one of 4,560 diffusion nodes receives the
same 0.020 / 0.008 m^-1 per unit fill slopes. This represents homogenized
regional absorption, not the physical volume of liquid-zone tubes.

## Physical reference

The [CANDU 6 Technical Summary, printed page 24](https://canteach.candu.org/content%20library/candu6_technicalsummary-s.pdf)
describes six vertical tubular control units containing fourteen compartments,
each unit having two or three compartments. The [manufacturer's description](https://www.camecofuel.com/business/cameco-fuel-manufacturing/fabrication-services/liquid-zone-control-assemblies)
confirms this arrangement. These localized devices control much larger power
regions; those regions are not filled with light water.

[IAEA TECDOC-1926, printed page 62](https://www-pub.iaea.org/MTCD/Publications/PDF/TE-1926web.pdf)
reports about 7 mk total worth for standard CANDU 6 liquid-zone controllers.
The [IAEA CANDU I&C teaching module, printed pages 7-2-12 and 7-3-5](https://canteach.candu.org/Content%20Library/20041607.pdf)
uses approximately 6 mk total, or +/-3 mk around 50% fill. This supports a
6–7 mk empty-to-full target, rather than 6–7 mk per percentage point.

The game's seven face regions in each axial half follow the coarse traditional
two-left, three-centre, two-right arrangement. Their exact boundaries remain
discretized approximations. Region node counts are 312, 312, 372, 288, 372,
312, 312, repeated at the other end. No tube diameters, tube coordinates,
moderator subcells or physical water volumes have been digitized into this
solver. A painted absorber mask is consequently an effective cell footprint,
not a validated reconstruction of hardware.

## Quantitative check

Six frozen aged starting inventories were solved with tighter tolerances.

| Seed | Measured 50%–100% worth (mk) | Twice that half-range (mk, extrapolated) | Positive-only exploratory full-fill rho (mk) |
|---|---:|---:|---:|
| 0 | 39.747 | 79.495 | +19.283 |
| 1001 | 39.773 | 79.546 | +18.284 |
| 1002 | 39.816 | 79.633 | +17.343 |
| 1003 | 39.682 | 79.365 | +19.697 |
| 1004 | 39.778 | 79.555 | +18.371 |
| 4294967295 | 39.764 | 79.529 | +18.929 |

All six empty-state solves failed `SpatialCoefficients.AbsorptionBelowFission`:
the current signed overlay subtracts absorption relative to 50% fill, while
some fuel knots already have thermal fission equal to total thermal absorption.
The approximately 79.5 mk full-range figure is therefore an extrapolation, not
a successful empty-to-full measurement. The measured half-range alone is
roughly twelve times the intended 3–3.5 mk.

The separate exploratory experiment scaled slopes to about 8.17% and used an
empty reference with positive-only absorption. It measured 6.457 mk full-range
worth. This changed reference convention deliberately avoids the invalid
negative absorption; it is not the runtime model or an accepted calibration.
The fuel pack then remains +17–20 mk at full fill, so the pack's reference
reactivity must be rebalanced together with the device strength and geometry.
The runtime slopes were not reduced based on this experiment.

At 6.5 mk over 100 percentage points, an approximately 1 mk burnup loss would
require about 15.4 percentage points of uniform drain. The earlier small-drain
result reflects the excessive implemented worth. Its coefficient was measured
correctly, but it should not have been taken as evidence of plausible device
strength. New geometry and pack calibration will require a new burn benchmark.

## Implemented inspection and editing

Core node bindings now separate `LogicalZoneId` (power measurement region) from
`AbsorberZoneId` (the compartment whose fill drives local absorption). Zero
slopes deactivate absorption at a cell without removing its measured region.
Both IDs and slopes participate in the mapping digest. Default assignments and
slopes retain the prior gameplay physics while exposing the problem.

Open **Core Designer → Zone geometry** (`Z`). The view displays all twelve axial
slices, fourteen colours, the selected channel's axial membership and absorber
node coverage. It provides click/drag painting, cell/half/channel scope,
independent region and mask edits, translation by lattice/axial offsets, undo,
reload and JSON export. Off-core and overlapping mask moves retain the draft.
**Apply layout & solve** sends all bindings through Core validation and the
Game transaction boundary. Invalid maps, missing/duplicate nodes, empty power
regions, invalid slopes and solver failures preserve the live physical state.
Accepted layout edits rebuild spatial references and regulate from existing
fills while preserving clock, fuel, inventory and score. A new shift restores
the default layout.

The initial response Jacobian remains a synthetic regional estimate; subsequent
accepted/rejected solver responses update it through the existing secant model.
Arbitrary footprint edits do not imply a calibrated controller or correct worth.

Full precision results, region plots and editor screenshots are under
`artifacts/zone-geometry-audit-2026-09-30`. Reproduce the native audit using the
commands in `tools/AgedCoreBenchmark/README.md`.
