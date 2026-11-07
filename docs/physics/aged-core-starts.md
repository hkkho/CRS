# Seeded aged-core starting snapshots

Core generates the starting inventory using `patterned-channel-age-eight-shift-with-flow-cycle190-v3`. The browser uses an unsigned 32-bit seed. **Starting core** allows explicit entry, **Retry seed** repeats the seed and **New core** draws a new one. Custom settings are another part of the model identity.

## Fuel cycle

At the nominal 2,064 MW thermal, 380 channels revisited every 190 full-power days require 16 fresh bundles/day for eight-bundle shifts. A bundle represents 20.6 kg of heavy metal and resides in the core for 285 full-power days on average. Energy balance gives a target mean discharge burnup of 6.262135922 MWd/kg HM.

For each of the twelve positions from inlet, use the authored axial exposure weight `w[k] = 0.55 + 0.45 sin(pi (k + 0.5) / 12)`. Dwell exposure is proportional to this weight, normalized so the eight outgoing bundles average the discharge target at the end of a cycle. Fresh slots start their first dwell at zero burnup. The four retained slots include exposure from their previous dwell. Interpolate between each slot's beginning and end burnup using the channel's cycle age.

The approach is inspired by [RFSP instantaneous-core initialization](https://www.nuceng.ca/canteach-rev2/library/20054311.pdf). The seven transverse regions use stratified age ranks. SplitMix64 assigns ranks within alternating checkerboard parities and determines which parity receives the younger ranks. Actual channel flow maps the inlet-relative positions to physical slots.

## Equilibrium before play

The aged inventory represents assumed prior exposure. Run time, score, energy and consumed fuel start at zero. The shared solver and controller settle xenon and the liquid-zone fills against the [time-average node-power targets](channel-power-reference.md). The authored fuel table ends at 30 MWd/kg HM. Core rejects out-of-range coefficients rather than extrapolating the fuel curve. See [current pack data](../../data/packs/candu6-two-group-diffusion-pack-v1.json) and [calibration provenance](calibration-provenance.md).
