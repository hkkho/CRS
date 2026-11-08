# Poison, adjusters and liquid-zone regulation

## Iodine and xenon

Iodine-135 decays into xenon-135. Xenon is removed by radioactive decay and neutron absorption, so its concentration depends on both past exposure and current flux. Core analytically evolves each bundle's isotopes over an interval at the accepted power and flux. Fresh bundles enter with zero iodine and xenon. Retained bundles carry their own isotopes into their new positions.

At full power, neutron capture accounts for about 90% of xenon removal in v11. The authored fuel curve includes a burnup-dependent reference xenon concentration. At a solve, Core replaces its absorption with the bundle's live xenon absorption. The [v11 calibration](../../physics/xenon-calibration.md) records how the game fits capture and reactivity worth.

## Fixed adjusters and liquid zones

Twenty-one fixed absorber rods add localized absorption and shape nearby power. Fourteen liquid-zone compartments vary water absorption as they fill. The controller measures each region's share of total power and compares it with the same share of the immutable [time-average reference](../../physics/channel-power-reference.md). Custom runs use the reference solved for their pack. A controller result is converged when compensated reactivity is within ±0.05 mk and all fourteen power shares are within one percentage point of their targets. The equilibrium solver separately normalizes the reported total thermal power to the run's configured power.

For a correction, the controller starts with the closest modeled reactivity it can reach, then adjusts individual fills to reduce regional power errors while preserving that modeled reactivity. The spatial solver verifies the move and supplies a measured response for one further correction if needed. Fills stay between 0% and 100% and can change by at most eight percentage points in one event.

For the matching xenon-v11 pack and tube mapping, the initial response uses full-core finite differences around 50% fill. The fit uses aged seed 1001 at nominal power with equilibrium xenon held fixed during ±2.5 percentage-point perturbations of each compartment. This is a local calibration. Other packs or mappings start from a generic estimate, and all accepted states use the full-core solver's measured response.

A zone's headroom is its remaining room to fill or drain. The core average can be within bounds while an individual zone has little room to move. Numerical convergence, controller headroom and terminal game status are separate readings.

See [operating limits and score](../manual/score.md) for game outcomes and [the solver](numerical-model.md) for the interval approximation.
