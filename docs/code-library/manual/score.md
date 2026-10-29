# Keep the run productive

Run limits — equality is allowed.

| Reading | Allowed |
| --- | --- |
| Average LZC | 10–90% |
| Global axial tilt | −20 to +20% |
| Channel power | ≤ 7,100 kW |
| Bundle power | ≤ 900 kW |

Limits are checked after each fuel move and at the day boundary. A violating move ends the run and stops remaining orders. A day-end loss retains the completed day. An invalid plan or numerical failure consumes no fuel or time; an interrupted calculation may have an unknown outcome, which is reported separately.

## Ripple points

Each channel has a fixed time-average power reference. The score uses RMS relative deviation from those references: points/hour = 1 / (1 + (RMS / 0.10)²). Smaller ripple earns more, up to one point per simulated hour. Fuel moves have no direct bonus or cost; their later power response changes points earned.

The day report records executed and unexecuted orders, fuel, useful discharges, energy, earned points and any ending reason. After a loss, Retry same seed repeats the starting core and objective; Try new seed draws another core.
