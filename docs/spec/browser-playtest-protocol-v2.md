# Browser playtest protocol v2

`candu-playtest-v2` reports schema version 2 and keeps the v1 command and
response envelope shape. It changes
the live snapshot to carry `axialTiltFraction` and `rrsReserveFraction` from
`GameSession`. Both are dimensionless. Axial tilt is the signed first moment
of the solved group-2 (thermal) neutron flux: End A is negative and End B is
positive. RRS reserve is normalized to 0–1 from the nearest
liquid-zone fill boundary, so a 50% fill in every zone means full reserve.

The snapshot no longer carries `absoluteTiltFraction`,
`controlMarginFraction`, `targetTiltFraction`, `staticReactivity`, or
`staticReactivityMethodId`. `physics.reactivity` remains the solved
`(k - 1) / k`; `physics.coreReactivity` is the uncompensated equilibrium value,
and `physics.compensatedNetReactivity` is the value after liquid-zone
coefficient overlays. The practice run integrates burnup and refreshes the
equilibrium shape hourly and after accepted refuelling. It does not model a
short-time kinetics or xenon transient.

The v1 `queue-tilt-target` browser command is removed because it only changed a
legacy scenario scalar, not the spatial flux solve. Other v1 commands, core
fields, compact patches, and failure semantics remain
as described in [the v1 specification](browser-playtest-protocol-v1.md).
