# Seeded aged-core starting snapshots

The browser starts from `patterned-channel-age-eight-shift-with-flow-cycle190-v3`, generated in
Core and solved by the existing full-core diffusion solver and bounded RRS.
The browser draws a cryptographically random 32-bit seed on loading and when
**New aged core**, **New shift**, or **Try new seed** is selected. Explicit seed
entry and **Retry same seed** preserve reproducibility. Offline fixtures retain
fixed seeds, including 1001, so numerical regressions can be compared.
Browser `initialize` and `reset` accept optional `seed` integers in
0..4294967295; reset without a seed recreates the current seed. The request is
recorded in replay history. Game/Core APIs accept unsigned 64-bit seeds.

## Theory and alternatives

RFSP's instantaneous-core approach uses a channel cycle age `f` in [0,1] and
interpolates each bundle between beginning- and end-of-cycle irradiation:
`B[k] = B_in[k] + f * (B_out[k] - B_in[k])`. This starting-point approximation
is described in the [CANDU fuel-management course, equilibrium-core design](https://www.nuceng.ca/canteach-rev2/library/20031116.pdf)
and the [RFSP INSTANTAN manual chapter](https://www.nuceng.ca/canteach-rev2/library/20054311.pdf).
The latter is a reference for the module; this implementation does not execute
RFSP or reproduce its input format. A patterned age distribution and the
beginning/end-of-cycle construction are also described in
[Song's fuelling study, section 5.5.3](https://espace.rmc-cmr.ca/jspui/bitstream/11264/561/1/Song_Fuelling_Study_Thesis_Finalalized%20%28Electronic%20Version%29.pdf).

A full time-average calculation would iterate lattice properties, flux, dwell
time and target discharge irradiation to convergence. A history-based approach
would run many months of burnup and scheduled refuelling before saving a state.
Both could improve axial exposure and power consistency but require more
physics inputs and startup/offline work. Independent random bundle burnups
would lose the shared channel cycle and retained-fuel history. The current
generator is a cheap, reproducible intermediate approximation.

## Implemented surrogate

Use a prescribed symmetric axial exposure weight
`w[k] = 0.55 + 0.45*sin(pi*(k+0.5)/12)` rather than claiming a converged
time-average solution. The eight-bundle shift has `B_in[k]=0` for k=0..7 and
`B_in[k]=B_out[k-8]` for k=8..11. Set each dwell increment to
`delta[k] = 8 * 6.2621359223300965 MWd/kg_HM * w[k]/sum(w)`.
Thus the eight departing positions k=4..11 average 6.262136 MWd/kg_HM at cycle end.
Their individual burnups differ; older retained fuel has two dwell exposures.
Opposite checkerboard parities feed from opposite ends. The discharge target is
energy-balanced for a 190-FPD channel refuelling interval at 2064 MW thermal:
16 fresh bundles/day at 20.6 kg HM each. Mean bundle residence is 285 FPD.
See [power and fuel calibration](power-and-fuel-calibration.md) for the applied
fuel curve and preserved 6.5-mk zone worth. Up to eight bounded
RRS passes settle the assumed pre-run state at time zero; playtime does not
advance. The controller minimizes the combined spatial and criticality residual,
so it can retain fills before meeting its criticality tolerance. Representative
seed tests require residual reactivity below 2 milli-k and remaining headroom.

Within each of seven transverse RRS regions, stratified ages `(rank+0.5)/N`
have mean exactly 0.5. Checkerboard groups split younger and older ranks;
SplitMix64 shuffles ranks within parity and chooses which parity is younger.
This suppresses local fresh-fuel clustering while preserving seed variation.
It does not model an actual refuelling schedule or guarantee identical power
or reserve for every seed. The authoritative solver determines those outcomes.

Bundle identities, ordering, 20.6 kg heavy-metal mass and finite fresh stock
are preserved. Initial exposure is stored in SI J/kg_HM (1 MWd/kg = 8.64e10
J/kg). Run time, accumulated energy, score and refuelling count start at zero;
the aged inventory is an assumed prior history. Xenon remains the existing
static/unavailable state. `CreatePractice()` retains the old smooth synthetic
fixture for isolated solver tests; live sessions use `CreateAgedPractice(seed)`.

## Burnup coverage

The active pack has fourteen knots: ten remapped low-burnup knots resolving the
early peak and decline, then explicit 15, 20, 25 and 30 MWd/kg tail knots.
The remapping and authored positive tail are documented in
[power and fuel calibration](power-and-fuel-calibration.md). Both embedded and
repository copies carry the same version, provenance and coefficient checksum.
The pack ends at 30 MWd/kg; lookups outside its domain still fail. Tests check
knots, interpolation, positive coefficients, yield relationships, matching
pack copies, and generated inventory plus 30 days at 2 MW per bundle.

Gameplay uses eight-bundle refuelling with channel flow. The aged inventory’s
position-from-inlet index follows the same published topology flow direction
(`EndAtoEndB` or `EndBtoEndA`), including the alternating direction of adjacent
channels. This orientation correction uses model identity
`patterned-channel-age-eight-shift-with-flow-cycle190-v3`; previous seeded
physical digests are superseded.
