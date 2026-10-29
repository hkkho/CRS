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

## Live demo

The launcher also offers an ephemeral power-only demo: five-second day deadlines, immediate double-click/keyboard/touch refuelling, background shared solves, pause and seeded retries. The map retains latest solved power while requests run; a bounded queue and coalesced deadlines prevent solver backlog. Slow hardware may reduce the achieved pace. Daily runs remain independent.

## Next work

- Gather human feedback on channel selection, the details tabs and day results.
- Check physical phone performance and full-day calculation latency.
- Keep keyboard, reduced-motion, narrow-layout, save and Pages checks working.
- Retain deterministic replay, atomic failures and shared inventory accounting.

Shutdown, scram, accidents, operator training and full plant operations remain
outside scope. See [implementation](IMPLEMENTATION_GUIDE.md),
[architecture](architecture.md) and the [code library](code-library/index.md).
