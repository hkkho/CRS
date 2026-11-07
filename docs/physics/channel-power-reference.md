# Channel power reference

The nominal reference is a fixed distribution across 380 channels that sums to 2,064 MW thermal. Each channel has its own reference power. Ripple is the live channel power divided by its reference. The default model ID is `cycle190-time-average-21-adjusters-diffusion-half-zones-v7`.

## Derivation

The [fuel-management method](https://www.nuceng.ca/canteach-rev2/library/20054411.pdf) averages lattice cross sections over each bundle position's dwell irradiation range before solving the time-average shape. CRS uses the authored [190-full-power-day cycle and exposure ranges](aged-core-starts.md).

For each inlet-relative position, average the nine fuel coefficients over its beginning and end burnup. Core integrates piecewise-linear coefficients by splitting the trapezoidal integral at each interior knot. Bind the resulting coefficients by the channel's actual flow direction. The reference uses the active [two-group solver](active-two-group-solver.md), all 21 fixed adjusters and fourteen liquid-zone compartments at 50% fill. It averages the included xenon reference across the same dwell ranges and replaces it with equilibrium xenon for the solved shape.

Normalize node heating to the nominal output and sum the twelve bundle powers per channel. The node powers also supply the startup regulator targets. The actual startup fills are calculated separately. A custom pack computes its own immutable reference at the chosen nominal power, which its scoring and ripple displays share.

This method prescribes exposure ranges. An iterated time-average fuel management solution would solve for dwell times and exposure as well as flux. Current calibration inputs and validation are recorded with the [v10 pack](../../data/calibration/diffusion-v10/README.md).

To produce current reference CSV, JSON and seed comparisons, run from the repository root.

```powershell
dotnet run --project tools/ChannelReferenceBenchmark -c Release -- tmp/channel-reference
```

See [limits and scoring](../gameplay/score-balance.md) for the peak ripple score.
