# Burnup, zone worth and per-step regulation

This records the historical `candu6-two-group-diffusion-v1-cycle190-650mwe-reactivity-v3` pack.
The [literature-guided v4 revision](literature-geometry-v4.md) supersedes its active
geometry, coefficient curve, zone slopes and burnup-loss measurement.
At the seed-1001 reference aged inventory and 2,064 MW thermal, independent
tight diffusion solves measure:

| Measurement | Previous powerlimits-v2 pack | Applied v3 pack |
| --- | ---: | ---: |
| Burnup-only loss over one full-power day | 0.264761 mk | 0.498666 mk |
| All fourteen zones, empty to full | 6.511838 mk | 7.001876 mk |

Reactivity is `rho = (k - 1) / k`, with `mk = 1000 * rho`. One FPD is
86,400 seconds at the reference thermal power. The burnup audit deposits that
energy using the initial half-fill power shape, then compares the start/end
inventories with zones fixed at 50% and no evolving xenon or refuelling. Zone
worth compares empty and full fills on the identical initial inventory. Live
net reactivity also includes zone response and xenon; its change is not a
burnup-only decay measurement. These local model coefficients vary with the
inventory and power distribution; they are not validated plant constants.

The fitter applies a positive exponent of 1.8288995621240054 to the prior
burnup-dependent fission contrast. A common neutron-production normalization
restores the half-filled reference to criticality. Existing knots, 30 MWd/kg
domain, group ordering, geometry, thermal reference, masses and fuel accounting
remain unchanged. Tail coefficients stay positive and interpolate at the same
knots. Fast and thermal zone absorption slopes are 0.0017230342616652234 and
0.0006892137046660893 per metre per unit fill. Empty zones add no absorption.
The canonical coefficient-table checksum and embedded pack match.

Each 100 ms browser control tick at 1x advances three simulated minutes and
runs the shared equilibrium/LZC solve with the updated burnup and xenon.
Faster modes split their larger advances into the same 180-second substeps;
they do not postpone zone calculation until a half-hour boundary. Wall-time
partitions retain identical substep order and warm starts. Partial wall ticks
that advance no simulation time do not perform another solve. Refuelling still
runs an immediate bounded RRS event. Requested playback speed can exceed
observed speed when the solver takes longer; no skipped solves or catch-up
approximation are introduced.

The pack version changed intentionally. Earlier saved runs are rejected by
the pack compatibility check rather than replayed using different physics.
Start a new run after updating.

The reviewed source, proposal and fit trials are in
`data/calibration/reactivity-scale-2026-10-06`; measured reports are
`benchmarks/reactivity-scale-current-v1.json` and `benchmarks/reactivity-scale-v3.json`.

```powershell
dotnet run --project tools/AgedCoreBenchmark -- --fit-reactivity-scale data/calibration/reactivity-scale-2026-10-06/source-pack.json artifacts/reactivity-fit
dotnet run --project tools/AgedCoreBenchmark -- --reactivity-scale artifacts/reactivity-scale.json
```
