# Compact core deck

Follow-up on `design/control-room-game`, 2026-10-28, over base revision
`9ac573ef6d76b66fa4eda01848c985b3ed031b3d`. The working tree also contains the
preceding presentation work. This is a local development build, not a Pages deployment.

## Visible changes

- Free practice is the only launch/new-run choice in Studio and Arcade. Existing
  finite-run contracts remain readable for historical records and internal tooling.
- Removed the two decorative reactor rails and moving fuelling locator. The
  selection brackets, actual adjuster geometry, LZC overlays and input layer remain.
  Zones and adjusters use hover descriptions without text labels on the map.
- Moved selected-channel readings and Add into the right-hand LZC block. The block
  now has keyboard-accessible LZC levels, Channel, Fuel and Profiles tabs.
- Expanded the map to approximately 749 CSS pixels at the tested 1280px desktop
  width. Zoom shares its heading. The right column follows the map's height; longer
  details scroll inside their panel. Layouts at 1,050px and below stack the map,
  sheet and details, avoiding the older three-column squeeze at tablet widths.
- History controls are permanently visible above the map, alongside Keff, axial
  tilt and the active map legend. History still uses the authoritative snapshots.

## Shared simulation changes requested with this pass

The power limits are **7,100 kW per channel** and **900 kW per bundle**, including
termination, full/compact bridge snapshots, plots and warning colors. Strict
greater-than failure boundaries and reason precedence are preserved.

The fourteen zone targets now use the fixed cycle-average, half-filled reference
core's nodal power fractions. Startup settles the aged seeded core against that
reference, accepting only verified bounded controller candidates. Initial fills
are therefore nonuniform (seed 1001 is approximately 42–58%) while their average
remains near 50%. Subsequent controller events retain their existing deadband.
No independent reactor rules were added to the browser.

The changed initial model requires a new daily run. The daily model identifier
rejects earlier saved runs before restoring a candidate; saved records are retained.
The wire fixture was regenerated with the repository's BrowserWireFixtures tool.

## Verification evidence

The local Pages reproduction is `/CRS/` using the published Release browser-WASM
AOT host, with no research experiments or external analysis dependency.

- Frontend: all 204 tests passed; production and Pages builds passed.
- Browser contracts: all 45 tests passed through `tools/Test-Browser.ps1`,
  including byte-identical fixture replay and terminal 120% target behavior.
- Core: all 116 tests passed. Game: all 86 tests passed, including the four-seed
  startup checks, strict power boundaries and real-time refuelling/burnup regression.
- Browser daily acceptance passed: multi-channel and empty days, frozen planning,
  narrow layout, bundle-limit failure, and unavailable-WASM recovery.
- Arcade acceptance passed with endless practice and unlimited stock.
- Accessibility acceptance passed at 1280, 1100, 1024, 900, 640 and 320 CSS pixels,
  keyboard-only navigation, 200% zoom emulation and reduced motion, with no browser errors.
- Display-scale acceptance passed: map zoom/pan, all map palettes, axial profiles,
  history ranges, and visible off-scale 7.10 MW / 900 kW limit labels.
- Local-save acceptance passed: guest save, offline reload, WASM replay restore,
  preserved day/score/fuel/draft, and the 320px save panel.

Windows .NET 10's native test host crashed during an earlier run; the isolated
regression passed with tiered compilation disabled. Final shared checks use that
setting and serial MSBuild without shared compilation. Browser acceptance uses
the production AOT runtime independently.

Representative captures and machine-readable browser results are in
[`compact-core-deck/verification`](compact-core-deck/verification).
The source manifest identifies the uncommitted sources and Pages entry point.
No physical touchscreen or human enjoyment study was performed.
