# Practice limits and scoring

`practice-burnup-zone-ripple-tilt-v4` adds the points from committed fuel moves and
accepted simulated operating intervals. A run or step can have a negative score.

| Scored when | Factor | Points |
| --- | --- | --- |
| Each fuel move | Discharge burnup | Each outgoing bundle earns its burnup in MWd/kg minus 6. Below 6 costs points, exactly 6 breaks even, and higher burnup earns more. |
| Each fuel move | Liquid-zone balance | Up to 2 points for the post-fuelling levels. Average the absolute distance of all 14 fills from 50%. The bonus declines linearly from 2 at zero distance to 0 at a 50-percentage-point distance. |
| Each operating interval | Peak channel ripple | 0.50 points/hour below 1.08, 0.25 points/hour from 1.08 through 1.10, and zero above 1.10. |
| Each operating interval | Global axial tilt | 0.50 points/hour at zero tilt. The rate declines linearly to zero at 20% absolute tilt. |

Each channel's ripple ratio is its thermal power divided by its fixed time-average
reference. The nominal reference totals 2,064 MW thermal. Operating points use the
accepted shape and the interval's actual power amplitude. See the
[reference derivation](../physics/channel-power-reference.md).

The zone bonus uses the committed solution of each move, including when several
orders run together. The browser shows the operating rate and the latest fuel
batch's burnup and zone points. The wire field `freshFuelCost` carries the early
discharge penalty. Checkpoints are compared within the same scoring policy. Pre-v4
checkpoints remain stored under their original scoring identity.

## Operating limits

A run ends outside 10–90% average liquid-zone fill, beyond ±20% global axial tilt, above 7,100 kW/channel or above 900 kW/bundle. Equality is allowed. Peak channel and bundle limits are independent and use the accepted thermal powers at the applied amplitude. Ripple affects score separately.

A physical loss during a step commits the violating move or endpoint. Numerical rejection preserves the previous accepted state. See [shared step semantics](daily-turn-mode.md) and [PracticeOperatingLimits](../../src/ReactorSim.Game/PracticeOperatingLimits.cs).
