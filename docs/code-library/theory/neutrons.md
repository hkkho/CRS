# Neutrons, multiplication and power

Nuclear fission releases heat and additional neutrons. Some neutrons cause later
fissions; others are absorbed without fission or leave the modeled core. A
reactor's neutron balance depends on the competition between production and loss.
CRS represents that balance with a spatial two-group diffusion model.

## Fast and thermal groups

Neutrons emerge from fission with a range of energies. Interactions with the
moderating material reduce their energy. The two-group approximation represents
this spectrum using a fast group and a lower-energy thermal group. It records
one flux for each group at each spatial node instead of tracking individual
neutrons or every possible energy.

**Flux** is the neutron-field intensity used in reaction-rate calculations. The
model expresses it in neutrons per square metre per second. A macroscopic reaction
coefficient, expressed in inverse metres, multiplied by flux gives the reaction
rate per unit volume. Material coefficients therefore connect neutron intensity
to absorption, scattering and fission.

The active model includes downscatter from fast to thermal neutrons. It does not
include an upscatter term. Group order is always fast, then thermal.

## Effective multiplication and reactivity

The effective multiplication factor, `k_eff`, is the eigenvalue associated with
the solved neutron balance. A value of one describes critical balance; values
above or below one indicate excess production or excess loss for that state.
Reactivity expresses this departure on a signed scale.

**Equation 1. Reactivity from multiplication.** The following mathematical
notation relates the dimensionless multiplication factor to reactivity:

```text
rho = (k_eff - 1) / k_eff
reactivity_mk = 1000 * rho
```

`rho` is dimensionless reactivity, and `reactivity_mk` is its value in the display
unit mk, where one mk equals 0.001 reactivity. For example, `k_eff = 1` gives zero
reactivity. `k_eff = 1.001` gives approximately 0.999 mk. The relation describes
the solved balance; in the daily game it does not independently evolve a
time-dependent power amplitude between displayed decisions.

## Flux shape and thermal power

The **shape** describes the spatial distribution of neutron flux and fission
power. The solver normalizes its power distribution to a reference total of
2,064 MW thermal. The session's operating amplitude is applied to that shape
when physical power and energy are calculated. Normalization determines the
scale of the solution; it does not remove channel-to-channel variation.

Fission coefficients used for energy production differ from neutron-production
coefficients. `Sigma_f` describes the fission rate; `nuSigma_f` also includes the
number of neutrons produced per fission. Reusing one in place of the other would
confuse heat production with neutron production.

The displayed electrical output is an authored estimate based on 650 MW
electrical at 2,064 MW thermal. Bundle burnup uses thermal fission energy.
Electrical output is not the energy input to fuel exposure.

## Spatial shape and axial tilt

Channel power sums the powers of its twelve bundles. Local peaks depend on the
material and absorption around a location, so a core with an acceptable total
output can still exceed a channel or bundle limit.

Axial tilt describes the signed End A–End B imbalance derived from the solved
thermal flux. Positive tilt indicates greater weighting toward End B. The global
core value and a selected channel's local value are different observations; the
game's tilt limit applies to the global value.

See the [active equations](../../physics/active-two-group-solver.md) for the exact
balance and normalization, and the [power limits](../../gameplay/power-limits.md)
for the game-owned terminal policy.
