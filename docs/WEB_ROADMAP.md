# Web game roadmap

The active product is `web/candu-playtest`, backed by Core/Game/Browser and
published on GitHub Pages. Daily turns are the acceptance path. Keep rules in
shared simulation and presentation in native DOM/SVG.

## Current loop

1. Inspect the frozen, enlarged core and the fixed-reference LZC distribution.
2. Draft eight-bundle orders by double-click, keyboard or the details-dock button.
3. Review the fuel sheet, then refuel and advance one day or advance without fuel.
4. Read the authoritative result, inspect the changed core, and plan again.

Studio and Arcade both offer endless free practice. The one-, three- and
seven-day challenges are removed from current game controls. Existing historic
contracts remain available to replay tooling.

The details dock beneath the fuel sheet uses LZC, Channel, Fuel and Profiles
tabs and fills the side column. History inspection is permanent, with Keff,
tilt and the map legend above the workspace. Compact zoom controls share the
map heading; decorative rails and device labels no longer cover the face.

LZC targets use the fixed time-average node-power reference; startup settles
the aged core's real fills and equilibrium xenon. Limits are 7,100 kW/channel
and 900 kW/bundle. The new daily model identity prevents replaying older saves
under changed rules while retaining the stored files.

## Time based play

The launcher lists Time based first, followed by Turn based. Entering either mode shows a loading pop-up, then an explicit Start button when setup is ready; the clock stays frozen until Start. Time based uses an ephemeral session with five-second interpolated half-day playback, a ticking simulation clock, double-click/keyboard/touch fuel queuing at the first free worker opportunity, pause and seeded retries. One shared solved endpoint is buffered ahead while the next atomic half-day step runs; fuel commands have priority whenever the worker is free. No calculating screen or status interrupts play; slow solves hold an endpoint without extrapolation or backlog. The vivid 5,000–6,500 kW map pulses above 6,500 and shakes above 7,000. The map dropdown offers channel power, ripple, burnup and paired zone fills (left End A / right End B). The core uses color-only cells without zoom controls; selected power and fuel input share the top strip. Live power and average-zone line plots use the space left of the core on wide screens. All fourteen zone fills and average loss-limit headroom remain visible, with reduced-motion alternatives. Daily runs remain independent.

## Next work

- Gather human feedback on channel selection, the details tabs and day results.
- Check physical phone performance and full-day calculation latency.
- Keep keyboard, reduced-motion, narrow-layout, save and Pages checks working.
- Retain deterministic replay, atomic failures and shared inventory accounting.

Shutdown, scram, accidents, operator training and full plant operations remain
outside scope. See [implementation](IMPLEMENTATION_GUIDE.md),
[architecture](architecture.md) and the [code library](code-library/index.md).
