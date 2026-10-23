# Power, fuel and regulation

## Reactivity & Keff

Keff describes neutron multiplication. Keff = 1 is critical equilibrium. Reactivity is ρ = (Keff − 1) / Keff; 1 mk = 0.001 in reactivity. The shared solver balances the core using two neutron energy groups and spatial fuel/device data.

## Liquid-zone control (LZC)

Water in fourteen compartments absorbs neutrons. Filling reduces reactivity; draining adds it. The regulating system adjusts levels and shape. High average level leaves less fill room; low average level leaves less drain room. Individual zones can have little headroom even when the average looks comfortable. The highlighted zone has the least headroom.

## Adjusters

Twenty-one fixed absorber rods depress power near their positions. Amber marks show their geometry; shaded bands show overlap with the selected channel. They are fixed in this game.

## Iodine & xenon

Iodine-135 decays to xenon-135. Xenon absorbs neutrons and is removed by decay and neutron absorption. Inventories follow bundle identity. Fresh bundles start with zero iodine and xenon; retained bundles carry their inventories. Advancing a day builds poison in fresh fuel and changes the solved power shape.

## Axial tilt

Signed tilt measures the End A / End B power imbalance. Positive means higher power toward End B. Global tilt and local channel tilt are separate readings; the run limit applies to global tilt.

## Power units

Channel and bundle kW are thermal fission power. The reference core is 2,064 MW thermal, corresponding to an authored estimate of 650 MW electrical. Burnup uses thermal energy, not electrical output.
