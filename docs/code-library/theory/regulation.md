# Poison, adjusters and liquid-zone regulation

## Iodine and xenon

Iodine-135 decays into xenon-135. Xenon is removed by radioactive decay and neutron absorption, so its concentration depends on both past exposure and current flux. Core analytically evolves each bundle's isotopes over an interval at the accepted power and flux. Fresh bundles enter with zero iodine and xenon. Retained bundles carry their own isotopes into their new positions.

The authored fuel curve includes a burnup-dependent reference xenon concentration. At a solve, Core replaces its absorption with the bundle's live xenon absorption. This accounts for the reference once. The [calibration notes](../../physics/calibration-provenance.md) record the poison-basis assumptions.

## Fixed adjusters and liquid zones

Twenty-one fixed absorber rods add localized absorption and shape nearby power. Fourteen liquid-zone compartments vary water absorption as they fill. The controller assesses neutron balance and region power relative to the [time-average reference](../../physics/channel-power-reference.md). It tries bounded changes to the fills and checks the response with the spatial solver.

A zone's headroom is its remaining room to fill or drain. The core average can be within bounds while an individual zone has little room to move. Numerical convergence, controller headroom and terminal game status are separate readings.

See [operating limits and score](../manual/score.md) for game outcomes and [the solver](numerical-model.md) for the interval approximation.
