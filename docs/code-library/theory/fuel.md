# Fuel history, burnup and refuelling

The game represents 380 fuel channels, each containing twelve physical bundle
positions. A fuel bundle has a persistent identity and an exposure history. A
position is a location that different bundles may occupy over time. This
distinction lets fuel history remain attached to a bundle when it moves.

## Burnup as energy per mass

**Burnup** measures the thermal fission energy produced per kilogram of heavy
metal. Higher burnup records greater accumulated exposure; it is not elapsed time
alone. Bundles at different powers accumulate exposure at different rates.

**Equation 2. Exposure accumulated over an interval.** These relationships show
how constant interval power becomes energy and additional burnup:

```text
energy_J = power_W * duration_s
additional_burnup_J_per_kg = energy_J / heavy_metal_mass_kg
burnup_MWd_per_kg = burnup_J_per_kg / 8.64e10
```

`power_W` is thermal power in joules per second and `duration_s` is the interval
length in seconds, so their product is energy in joules. Division by heavy-metal
mass gives J/kg. One megawatt-day equals `1,000,000 × 86,400` joules, which explains
the final conversion factor. A hypothetical 20.6 kg bundle producing 1 MW for
one day gains about `1 / 20.6 = 0.0485` MWd/kg. The example explains units; it is
not a prediction for a selected channel.

The material table binds current burnup to coefficients used by the neutron
solver. Fuel evolution can therefore change later reactivity and spatial power
even if the player does not change the plan. The material curves are authored
approximations with documented calibration assumptions.

## Eight-bundle movement

Each game move inserts eight fresh bundles with a channel's flow and discharges
eight at the other end. Four bundles remain, with their identities, exposure and
poison history. Neighboring channels have opposite flow directions.

For a move toward End B, the zero-based positions change as follows:

| Bundle role | Before the move | After the move |
| --- | --- | --- |
| Fresh fuel | Outside the channel | Positions 0–7 |
| Retained fuel | Positions 0–3 | Positions 8–11 |
| Discharged fuel | Positions 4–11 | Outside the channel |

A move toward End A reverses the mapping. The interface labels positions 01–12
from End A to End B, while the simulation indexes them 0–11. The shared
`GameRefuellingPlanV1` provides both execution and preview mappings; the interface
does not infer fuel direction from the display label.

Average channel burnup describes all twelve current bundles. Average outgoing
burnup describes the eight positions that a move would discharge. They answer
different questions, so a high channel average does not by itself identify the
best discharge or predict an acceptable power response.

## Initial fuel and half-day exposure

The same seed and physics settings reconstruct the same starting conditions. The [aged-core model](../../physics/aged-core-starts.md) records the initial exposure, bundle mass and reference fuel cycle.

For time-based play, the shared model evaluates the fuel batch, then uses post-refuelling power and flux for one 12-hour interval of energy, burnup and poison. The next equilibrium solution uses the updated inventory. See the [shared step contract](../../gameplay/daily-turn-mode.md).
