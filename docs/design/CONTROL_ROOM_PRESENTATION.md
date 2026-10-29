# CRS / Control Room presentation

Implemented on `design/control-room-game`, 2026-10-26. Starting revision:
`3e95bc570dbd9ce121b7950f6c79f1a818af2ac8`, with the existing uncommitted
interface-simplification work preserved. This is an uncommitted development
presentation pass, not a published Pages release.

## Player experience

The existing choice remains: inspect the frozen core, queue eight-bundle channel
orders, then commit the next day and respond to the shared result. No reactor
rules, fuel budgets, scoring, scenarios, commands, save formats or integration
cadence changed in this pass.

The visual direction is an industrial strategy board: amber enamel controls,
dark steel panels, pale instrument readings and a warm paper fuel manifest.
The cover presents run choices and a prominent Start run button. Studio and
Arcade share the materials, selection language and result styling. Charts,
fuel profiles, zone gauges, menus, manual, saves and recovery use the same palette.
Existing detailed observations remain available.

The live reactor has a machined ring, bolt heads, rails, a fuelling-machine
locator and a bracketed selected channel. Planned channels have dashed gold
outlines; selected channels have solid pale outlines and corner brackets.
Published power/burnup/ripple colors and thresholds remain unchanged.
Motion respects reduced-motion settings. No decorative effect advances time,
consumes fuel or awards points.

## Artwork and implementation

- `web/candu-playtest/src/controlRoom.css` is the final presentation layer over
  the existing layouts. Its palette uses the existing shared CSS tokens.
- `ReactorIllustration.ts` contains original decorative SVG cover art: a layered
  steel vessel, fuel ports, piping, supports and a fuelling machine. It is not a
  live topology or power forecast.
- `ReactorFrame.ts` draws the live housing and locators around the existing
  authoritative channel input layer. Decorative elements ignore pointer input.
- `public/favicon.svg` is an original five-port reactor mark. Vite rewrites the
  icon URL for `/CRS/`; the existing offline asset collector includes it.

All new art and texture is project-authored SVG/CSS. There are no downloaded
asset packs, external fonts, raster generators or new runtime dependencies.
Textures are static patterns and gradients, without SVG noise filters or
per-frame rendering loops. System display fonts have local fallbacks.

## Verification

Commands run from the repository root or browser package as appropriate:

```powershell
./tools/Test-Browser.ps1 -Configuration Release
npm run build:pages
npm run smoke:daily -- http://127.0.0.1:4175/CRS/
npm run smoke:accessibility -- http://127.0.0.1:4175/CRS/
npm run smoke:arcade -- http://127.0.0.1:4175/CRS/
git diff --check
```

Results:

- Browser bridge: 45 passed. Frontend: 204 tests in 35 files passed.
- TypeScript, normal production build and `/CRS/` production build passed.
- Daily acceptance: seed 1001; two fuel orders, removal and re-addition,
  row inspection, a successful fuelled day, a successful empty day, frozen
  planning, exact 16-bundle accounting, physical loss and WASM recovery passed.
- Accessibility acceptance: keyboard planning/day/result/history, manual focus
  trapping and return, reduced motion, 1280/640/320px layouts and 200% zoom
  emulation passed. No duplicate announcements or browser errors were reported.
- Arcade acceptance: 380 targets; touch selection and zoom; power/burnup;
  320/390/768/1280px layouts; review, authoritative day result and retry passed.
- Manual browser inspection used the local production build, including a
  two-channel plan that consumed no fuel while drafting and a committed day.
- Screenshots were visually inspected for the cover, core/plan, narrow Studio,
  power charts, result and Arcade surfaces.

The first daily smoke exposed a layout regression: hiding an empty selection
status line moved the core between clicks. The final style reserves its height,
and daily smoke now explicitly checks target position across inspection before
double-clicking. The rerun passed. The final narrow-toolbar adjustment was
followed by another production build and accessibility/Arcade acceptance run.

The staged authoritative runtime still reports the starting revision above.
The frontend is the development working tree, identified by
`control-room/verification/source-digests.json`; this is not a claim that these
presentation edits are already present on GitHub Pages.

## Evidence and limits

Representative captures and machine-readable results are in
[`control-room/verification`](control-room/verification/).

- [Cover](control-room/verification/launcher-1280.png)
- [Draft fuel plan](control-room/verification/studio-fuel-plan.png)
- [Day result](control-room/verification/day-1-success.png)
- [Narrow Studio](control-room/verification/studio-320.png)
- [Power charts](control-room/verification/power-1280.png)
- [Arcade](control-room/verification/arcade-result.png-desktop.png)

Controls are unchanged: click/tap to inspect, double-click or R/Enter on the
core to queue/remove, arrows to select, N for highest burnup, F1 for the manual;
zoom buttons and scrolling support larger targets and panning. Arcade also
offers explicit Add to plan, review and Run the day actions.

Chromium desktop and emulated touch/narrow layouts were checked. Physical
phones, Safari, Firefox and independent human enjoyment/retention testing were
not performed. The screenshots and passing checks establish presentation and
interaction evidence, not that the redesign is more fun for every player.
