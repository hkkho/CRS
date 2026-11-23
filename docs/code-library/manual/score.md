# Operating limits and score

Equality is allowed at all four operating limits.

| Reading | Allowed |
| --- | --- |
| Average liquid-zone fill | 10–90% |
| Global axial tilt | −20 to +20% |
| Channel power | ≤ 7,100 kW thermal |
| Bundle power | ≤ 900 kW thermal |

The simulation checks limits after applying the entire fuel batch and at the end of the half-day. If the combined fuel batch crosses a limit, every order counts and the run ends at the current simulation time. If the endpoint crosses a limit, the elapsed interval counts. A numerical rejection preserves the last accepted reactor state.

## Earning points

- Each discharged bundle earns its burnup in MWd/kg minus 6. Discharges below 6 cost points.
- Each fuel order earns up to 2 points based on all fourteen zone fills after the batch. The bonus falls linearly from 2 points when every zone is half full to zero when their average absolute distance from half full is 50 percentage points.
- Peak channel ripple earns 0.50 points per simulated hour below 108%, 0.25 from 108% through 110%, and zero above 110%.
- Global axial tilt earns 0.50 points per simulated hour at zero tilt, decreasing linearly to zero at ±20%.

The fuel receipt separates the latest batch's net burnup points from its zone bonuses. The score display shows the ripple and tilt rates. When the run ends, **Retry seed** recreates the starting core and **New core** draws another.

The published code library links to the scoring reference and step contract for formulas and transaction details.
