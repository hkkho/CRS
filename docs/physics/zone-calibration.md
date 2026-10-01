> This records the earlier zones65 calibration. The active cycle190 fuel and
> zone refit is documented in [power and fuel calibration](power-and-fuel-calibration.md).

# Applied liquid-zone calibration — 30 September 2026

The active synthetic pack provides 6.5 mk total empty-to-full zone worth.
Direct tight solves across seeds 0, 1001–1004 and 4294967295 measure
6.48540784–6.50716908 mk; every empty state is valid. Seed 1001 is critical
at uniform 50% fill to the offline fit's convergence accuracy.

## Applied changes

- Fast absorption slope: 0.0016096911298162599 m^-1 per unit fill.
- Thermal absorption slope: 0.0006438764519265039 m^-1 per unit fill.
- Both slopes are 0.08048455649081299 times the prototype slopes.
- Absorption reference is empty (0), while initial fills begin at 0.5.
  The overlay is slope times fill and never subtracts material absorption.
- Both nu-fission groups are multiplied by 0.9784364707712722 at every
  burnup knot. Fission heating, absorption, mass and burnup knots are unchanged.
- Pack version: `candu6-two-group-diffusion-v1-burnup30-zones65`.

A uniform production multiplier scales the eigenvalue without changing the
flux shape at fixed absorption. The offline fit balances reference fuel at
half fill and fits rho(empty) minus rho(full), where rho = (k - 1)/k.
Reference worth is 6.500079369 mk. This is a project-authored gameplay
calibration, not a validated plant coefficient set.

The source pack, fit and hashes are in
`artifacts/zone-calibration-2026-09-30`; the reproducible fit command is in
`tools/AgedCoreBenchmark/README.md`. The coefficient-table checksum is SHA256
of sorted-key compact JSON excluding its checksum field. Repository and
embedded packs are identical.

## Geometry and controller

Power regions and absorber ownership remain independent and editable in
Core Designer's Zone geometry view. The default mask represents homogenized
absorption over all 4,560 nodes. It is not an explicit digitization of the six
physical assemblies. Moving masks or changing slopes changes actual solved
worth; the 6.5 mk target applies to the shipped default map.

The weaker control authority requires a better resolved equilibrium:
runtime outer limit is 1,600, k tolerances 2e-7 absolute / 2e-6 relative,
residual 2e-5 and source shape 1e-5. Independent benchmark probes use tighter
limits. Common-mode residual weight is 20 so reactivity participates in the
combined spatial-control objective. Every accepted correction still passes
an authoritative diffusion solve. Finite signed-zero matrix intermediates
are permitted; final zeros are canonicalized for deterministic contracts.

An aged reference retains its own regional shape, including nonzero axial
tilt. Spatial feedback tests check restoration toward that reference. A
fresh core's excess reactivity can exceed this smaller control authority.

## Benchmark and drain audit

The six-seed 72-hour results are in
`artifacts/aged-core-calibrated-benchmark-2026-09-30/report.json`, with
half-hour trajectories in `samples.csv`. Peak powers are sampled maxima at
the game's 1 GW reference scale. No refuelling or user power commands are
applied; stock startup target events remain. No xenon transient is included.

The exact compensation audit is in
`artifacts/zone-calibrated-drain-2026-09-30/zone-worth.json`. It freezes initial
fills on the final burned inventory to measure loss, then applies final fills
to measure zone gain. Individual and uniform coefficients use central
differences of +/-1 percentage point. Uniform worth is about -0.065 mk per
percentage point, so one mk of decay corresponds to about 15.4 percentage
points of uniform drain. Unequal zone importance means arithmetic mean drain
need not equal this value. Endpoint residuals expose whether the actual
solved compensation closes the balance.
