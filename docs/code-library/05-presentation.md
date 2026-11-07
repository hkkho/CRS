# 5. From accepted state to the screen

Game projects immutable readings from accepted inventory and flux. Browser maps these readings into full snapshots and compact patches. `DemoView` consumes the displayed endpoint or its interpolated view through `DemoLoop`.

```annotated-source
path: src/ReactorSim.Game/GamePresentationProjector.cs
title: "Scale solved power for presentation"
language: csharp
start: "            double amplitude = Clamp(powerAmplitude, 0.0, 1.5);"
end: "                    projection.ShapeNodePowerWatts[index] * physicalShapeScale;"
steps:
- through: "            double actualPowerFraction = totalPowerWatts / referencePowerWatts;"
  explanation: "Apply the accepted power amplitude and express the result relative to the run's nominal thermal power."
- through: "                    projection.ShapeNodePowerWatts[index] * physicalShapeScale;"
  explanation: "Accumulate each channel's bundle powers in channel-major order."
```

```annotated-source
path: web/candu-playtest/src/demoPresentation.ts
title: "Blend readings between accepted endpoints"
language: typescript
start: "export function interpolateDemo(from: CanduSnapshot, to: CanduSnapshot, fraction: number): CanduSnapshot {"
end: "      zones: from.rrs.zones.map(z => ({ ...z, fillFraction: mix(z.fillFraction, to.rrs.zones.find(t => t.logicalZoneId === z.logicalZoneId)?.fillFraction ?? z.fillFraction) })),"
steps:
- through: "  const powers = new Map(channels.map(c => [c.channelIndex, c.powerWatts]));"
  explanation: "Return exact endpoints at the interval boundaries. Inside the interval, blend channel power by stable channel index."
- through: "      zones: from.rrs.zones.map(z => ({ ...z, fillFraction: mix(z.fillFraction, to.rrs.zones.find(t => t.logicalZoneId === z.logicalZoneId)?.fillFraction ?? z.fillFraction) })),"
  explanation: "Derive display ripple from blended watts and blend matching zone fills. Fuel movements and terminal status become visible when the endpoint is reached."
```

[ArcadeMap](../../web/candu-playtest/src/ArcadeMap.ts) renders the current map and keyboard focus. [DemoTrends](../../web/candu-playtest/src/DemoTrends.ts) keeps display observations for the plots. Neither provides input to the shared solver.
