# CANDU On-Power Refuelling

This browser companion is a focused play surface for the deterministic
on-power refuelling loop. The visible client is one Phaser 3 game with a fixed
1600 × 900 tactical viewport. It letterboxes through Phaser `Scale.FIT`, so the
same board remains legible on desktop and narrow screens.

The game never reimplements simulation rules in the client. It creates the
authoritative `candu-playtest-v2` bridge, loads the C# `ReactorSim.Game` WASM
module in a dedicated worker, and sends only protocol commands. If that bridge
is unavailable, the title and in-game unavailable scenes explain the locked
state instead of substituting a local simulation.

## Local run

```text
npm install
npm run dev
```

For a production-shaped build, stage the browser bridge from the repository
root and then build this directory:

```powershell
.\tools\Build-BrowserWasm.ps1
cd web/candu-playtest
npm ci
npm test
npm run build
```

`public/wasm` is generated deployment input. The runtime uses the bundled
authoritative bridge and project-authored physics pack.

## Controls

- Begin Shift: click the command or press Enter/Space.
- Select channels: pointer, arrow keys, or WASD.
- Refuelling order: `R` or Enter; choose 4/8 bundles and direction, request a
  preview, then Confirm or Cancel. `4`, `8`, and `D` are keyboard shortcuts in
  the order window.
- Playback: `1` = 1×, `2` = 10×, `3` = 60×, `4`/Space = pause.
- Reactor Control: `C`; adjust power with W/S or ↑/↓, tilt with A/D or ←/→,
  and queue targets with P/T. Time steps are available while paused.
- Escape closes an active command window.

The HUD, front-facing orthographic channel face, selected-channel dossier,
live reactor-physics readout, axial profile, projected path, transfer
animation, and result card are all rendered inside Phaser. HTML only hosts and
sizes the canvas plus a hidden live status mirror for assistive technology.

## Checks

```text
npm test
npm run build
npm run smoke -- http://localhost:4173
npm run benchmark -- http://localhost:4173 --warm-samples=1
```

The benchmark prints a compact `candu-playtest-reproduction-matrix-v1` to
stdout. Each fresh authoritative session covers channel 210 plus central and
peripheral fixtures, both refuelling directions, four/eight-bundle shifts,
paused/live advance, before/after snapshot summaries and hashes, state/replay
digests, transport timing, UTF-8 payload bytes, and console/page errors. Set
`PLAYTEST_EXPECTED_COMMIT_SHA` in CI to fail when the deployed bridge was not
built from the checked-out source SHA. The benchmark does not write a report
file or provide a browser simulation fallback.

Focused Vitest coverage protects the pure orthographic face projection, selection,
refuelling command state, scheduler, display helpers, and bridge-session
transitions. The owner acceptance path is a browser playthrough at 1600 × 900
and 390 × 844: load the title, enter a shift, select a channel, pause, preview,
cancel or confirm a transfer, and open the control window.
