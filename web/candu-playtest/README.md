# CANDU On-Power Refuelling

This browser companion is a focused play surface for the deterministic
on-power refuelling loop. The visible client is one Phaser 3 game with a fixed
1600 × 900 tactical viewport. It letterboxes through Phaser `Scale.FIT`, so the
same board remains legible on desktop and narrow screens.

The game never reimplements simulation rules in the client. It creates the
authoritative `candu-playtest-v1` bridge, loads the C# `ReactorSim.Game` WASM
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

`public/wasm` is generated deployment input. The runtime never invokes
DRAGON5, DONJON5, or any analysis executable.

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

The HUD, channel dossier, axial profile, projected path, transfer animation,
and result card are all rendered inside Phaser. HTML only hosts and sizes the
canvas plus a hidden live status mirror for assistive technology.

## Checks

```text
npm test
npm run build
npm run smoke -- http://localhost:4173
```

Focused Vitest coverage protects the pure isometric projection, selection,
refuelling command state, scheduler, display helpers, and bridge-session
transitions. The owner acceptance path is a browser playthrough at 1600 × 900
and 390 × 844: load the title, enter a shift, select a channel, pause, preview,
cancel or confirm a transfer, and open the control window.
