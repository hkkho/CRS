# Poison, adjusters and liquid-zone regulation

Neutron absorption depends on more than fuel burnup. The active game combines
bundle iodine/xenon history, fixed adjuster absorption and variable liquid-zone
water absorption with the material coefficients used by the spatial solver.

## Iodine and xenon

Iodine-135 decays into xenon-135. Xenon absorbs neutrons and is removed through
radioactive decay and neutron absorption. Its later concentration therefore
depends on both prior isotope inventory and neutron flux. The term **poison**
describes this neutron-absorbing contribution.

The model updates iodine/xenon analytically over an interval with the selected
power and flux held fixed. Fresh bundles enter with zero iodine and xenon;
retained bundles carry their own inventories to their new positions. Daily
planning does not age those inventories. The daily calculation evolves them
before solving the final boundary state.

The authored material curve already includes a reference poison basis. The
runtime's overlay accounts for the modeled deviation from its burnup-dependent
reference. This avoids treating an already represented reference inventory as an
additional independent absorption term. The basis remains a documented
approximation rather than a validated isotope-resolved plant model.

## Fixed adjusters

Twenty-one fixed absorber rods affect nearby power. Their absorption is localized
through shared device geometry. The map and selected-channel profile display that
geometry so a local depression can be related to the modeled absorber position.
The game does not provide rod-motion operations for these adjusters.

## Liquid-zone control

The reactor regulating-system model, abbreviated RRS, calculates bounded changes
to fourteen liquid-zone control compartments. Increasing water absorption reduces
reactivity; draining water reduces that absorption. The v7 tube geometry
localizes the contribution and models bottom-up filling, while the active v8 pack
retains the refitted device treatment.

**Headroom** is the remaining room to fill or drain a compartment. An acceptable
core-average level does not guarantee that every individual zone has useful
headroom. Spatial regulation depends on where absorption can change, not only on
the mean of the fourteen levels.

During equilibrium work, the controller evaluates the solved distribution,
constructs bounded fill trials, and uses further spatial solves to assess their
response. Published observations include its decision, residuals and limiting
zone. This is a numerical controller model with explicit movement constraints.

## Control diagnostics and game limits

Numerical convergence means the solve met its error criteria. Controller
saturation describes limited remaining control authority. A terminal game result
means the run crossed an operating or scenario condition. These outcomes must be
interpreted separately.

Game limits require average LZC from 10% through 90%, absolute global tilt at most
20%, channel power at most 7,300 kW, and bundle power at most 935 kW. Equality is
allowed. Limits are evaluated after fuel moves and at the day boundary. A
physically violating but numerically valid move is committed and ends the run;
a numerical failure rejects the attempted day and preserves the live reactor.

Detailed model notes cover [iodine/xenon evolution](../../physics/iodine-xenon-gameplay.md),
the [xenon reference basis](../../physics/xenon-reference-v6.md),
[fixed adjusters](../../physics/adjusters-v5.md) and
[liquid-zone tubes](../../physics/lzc-tubes-v7.md).
