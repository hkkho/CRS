# Channel power reference without adjusters

The fixed nominal reference is 2,064 MW thermal across 380 fuel channels:
5.431578947 MW/channel on average. This mean is not a uniform channel target.
The channel targets are derived by the authoritative Core diffusion solver.

[Zhang and Rouben, CANDU Fuel Management, section 3.2](https://www.nuceng.ca/canteach-rev2/library/20054411.pdf)
describes averaging lattice cross sections over each bundle position's dwell
irradiation range before solving the time-average power distribution. That
distribution provides the channel targets for fuel management. Instantaneous
channel refuelling produces ripple around the reference.
[Reactor Statics and Fuel Management, section 4.3.3.1](https://www.nuceng.ca/canteach-rev2/library/20042113.pdf)
explains that central adjusters and regional burnup differences flatten power.
A published standard CANDU-6 profile with those devices is therefore unsuitable
for directly copying into this game's core without adjusters.

## Implemented approximation

`cycle190-time-average-no-adjusters-half-zones-v1` uses the existing authored
190-full-power-day channel cycle, eight-bundle shift, opposite neighbouring
flow directions, and uniform 6.262135922 MWd/kg target discharge burnup.
For each position from inlet, compute the existing generator's beginning and
end burnup at channel ages 0 and 1. Retained positions include their earlier
dwell exposure. Average each of the nine fuel coefficients over this range:
`Cbar = integral(C(B), B_in..B_out) / (B_out - B_in)`.
The pack's coefficients are piecewise linear, so trapezoidal integration split
at every interior knot evaluates the average exactly for those authored ranges.
This avoids the incorrect shortcut `C(mean burnup)` on a nonlinear fuel curve.

Bind the averaged coefficients at each physical axial position using its actual
channel flow. Solve the same two-group eigenproblem with the existing geometry,
leakage and boundary conditions, no adjuster overlay, and all 14 liquid zones
fixed at 50% fill. Normalize fission heating to 2,064 MW thermal and sum the
12 bundle powers into each channel reference. The existing fuel pack already
contains its static poison baseline; no second dynamic equilibrium xenon
absorption is added to it. Live iodine/xenon departures still affect actual power.

This is a time-average **approximation to the current game surrogate**. It uses
prescribed exposure ranges, not an iterated self-consistent RFSP solution of
flux, channel dwell times and exposure. It retains the authored model's radial
variation. The power-limit pack flattens this distribution through its effective
coupling and leakage parameters; see [rebalance](power-limit-balance.md). No external
analysis program is used at runtime. Core calculates the immutable reference
once per process; the identity, source pack version and coefficient binding
digest accompany its presentation snapshot. Seeds, fuel moves, elapsed time,
power targets and developer geometry edits never rebase it.

## Measured reference

The `powerlimits-v2` embedded pack gives **4.567697–6.325798 MW/channel**, with a mean
of **5.431579 MW**. Initial RMS deviations for seeds 1001, 1002 and 1013 are
3.6344%, 3.0856% and 3.9361% respectively. These are measurements from the
authored surrogate, not plant data.

Reproduce the reference CSV, complete JSON profile and seed measurements:

```powershell
dotnet run --project tools/ChannelReferenceBenchmark -- tmp/channel-reference
```

See [scoring policy](../gameplay/score-balance.md) for the RMS calculation and
time-integrated points. The browser presents these authoritative readings.
