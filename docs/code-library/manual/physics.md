# Maps and reactor readings

Use the map tabs above the core. Hover, focus or select a channel to inspect its reading.

| Map | What it measures |
| --- | --- |
| Power | Thermal power of the twelve bundles, in kW. High-power channels pulse above 6,500 kW and shake above 7,000 kW. Reduced motion uses static outlines. |
| Max bundle power | The highest thermal power among the bundles in each channel, in kW. |
| Ripple | Channel power as a percentage of that channel's fixed time-average reference. At 100%, it matches its reference. |
| Burnup | Average accumulated fission energy per kilogram of heavy metal for the channel's bundles, in MWd/kg. |
| Inlet zones | Each cell shows the zone at the end where fresh fuel enters. Adjacent channels alternate flow direction. Six of the eight fresh bundles land in the inlet half. |

**Fuelling history** lists each channel's latest fuelling time and net points from that move in the current run.

Use the **Power** and **Zone fills** tabs below the liquid-zone gauges to switch between trends. The zone chart shows the core average and both ends of the selected channel. The fourteen individual zone readings can reveal limited control margin even when the average looks comfortable. The map and trend labels follow the A-to-W pressurizer-end face view; see [CANDU 6 Zone Locations, Figure 5.14](https://canteach.candu.org/Content%20Library/20043404.pdf).

Open **Zone response to last fuel batch** in the fuel receipt to see each affected channel's inlet and outlet zones before refuelling, after the fuel solve and at the end of the step. A rise during refuelling can be followed by a drop as fuel burns and poison evolves over the interval.

## What changes the readings

Fuel loses reactivity as it accumulates burnup. Refuelling replaces part of the inventory, changing the local power distribution. Iodine and xenon evolve with exposure, and xenon absorbs neutrons. The automatic regulator adjusts liquid-zone water absorption to control the reactor. Fixed adjusters also shape the power distribution.

The game uses an authored approximation of these effects. The reactor theory pages in the published code library explain the model and its limitations.
