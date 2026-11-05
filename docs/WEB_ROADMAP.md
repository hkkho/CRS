# Web game roadmap

The active product is `web/candu-playtest`, backed by Core/Game/Browser and
published on GitHub Pages. Time-based free practice is the public acceptance
path. Keep rules in shared simulation and presentation in native DOM/SVG.

## Current loop

1. Select **Time based** and press **Start** when the core is ready.
2. Inspect the power map, liquid-zone levels, and live trends.
3. Queue eight-bundle orders by double-click, keyboard, or the **Queue fuel** button.
4. Watch the next solved endpoints and adjust the fuel plan while time advances.

The display interpolates half-day endpoints over five seconds. All waiting
orders join the next unsolved step. Slow solves hold the last endpoint, and
pausing or hiding the tab freezes playback. The public launcher exposes
time-based play. The shared daily-turn contract remains available for tests and
replay tooling.

The browser automatically applies available updates after saving the latest
completed endpoint and waiting orders. Other game tabs must close first.
Compatible runs reopen paused at the Start screen.

Time based play shows waiting orders, submitted orders and the latest delivered
fuel batch beside the board. Standard-mapped controllers can start from the
ready screen, inspect channels with the D-pad, queue fuel with the bottom face
button, change the map with the shoulder buttons and pause with Start.

## Next work

- Gather human feedback on channel selection, the details tabs and day results.
- Check physical phone performance and full-day calculation latency.
- Keep keyboard, reduced-motion, narrow-layout, save and Pages checks working.
- Retain deterministic replay, atomic failures and shared inventory accounting.

Shutdown, scram, accidents, operator training and full plant operations remain
outside scope. See [implementation](IMPLEMENTATION_GUIDE.md),
[architecture](architecture.md) and the [code library](code-library/index.md).
