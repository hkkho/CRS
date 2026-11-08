# Keep the run productive

Equality is allowed at all four operating limits.

| Reading | Allowed |
| --- | --- |
| Average liquid-zone fill | 10–90% |
| Global axial tilt | −20 to +20% |
| Channel power | ≤ 7,100 kW thermal |
| Bundle power | ≤ 900 kW thermal |

The shared simulation checks limits after applying the entire fuel batch and at the end of the half-day. If the combined fuel batch crosses a limit, every order counts and the run ends at the current simulation time. An endpoint loss retains the elapsed interval. A numerical rejection preserves the previously solved reactor.

## Earning points

- Each discharged bundle earns its burnup in MWd/kg minus 6. Discharges below 6 cost points.
- Each move earns up to 2 points for keeping all fourteen zones close to half full after the batch.
- Peak channel ripple earns 0.50 points per simulated hour below 108%, 0.25 from 108% through 110%, and zero above 110%.
- Global axial tilt earns 0.50 points per simulated hour at zero tilt, decreasing linearly to zero at ±20%.

The time-based fuel receipt reports the latest batch. The score display shows the ripple and tilt rates. When the run ends, **Retry seed** recreates the starting core and **New core** draws another.
