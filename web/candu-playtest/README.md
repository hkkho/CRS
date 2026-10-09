# CANDU On-Power Refuelling

The native DOM/SVG Reactor Studio is the playable browser client for the
shared C# simulation. It consumes the versioned WASM bridge through a dedicated
worker and fails closed when that authoritative bridge is unavailable.
Core Designer, its zone editor, and Phaser have been removed.

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

- Begin shift with the native launcher; select seed and objective there.
- Select channels using the map, arrow keys, or Highest burnup (`N`).
- Refuel (`R`) inserts eight bundles automatically with channel flow.
- Space pauses/resumes; power targets apply while running and time steps while paused.
- Inspect power, burnup, iodine and xenon for all twelve bundle positions.
  Fresh bundles enter with zero iodine and xenon; the four retained bundles keep
  their inventories. Advancing time builds poison in fresh fuel.
- History tabs track power, discharge, zones, tilt, reactivity, poison, fuel and score.

## Checks

```text
npm test
npm run build
npm run smoke -- http://localhost:4173
npm run benchmark -- http://localhost:4173 --warm-samples=1
```

The benchmark prints a compact `candu-playtest-reproduction-matrix-v1` to
stdout. Each fresh authoritative session covers channel 210 plus central and
peripheral fixtures, both refuelling directions, eight-bundle shifts,
paused/live advance, before/after snapshot summaries and hashes, state/replay
digests, transport timing, UTF-8 payload bytes, and console/page errors. Set
`PLAYTEST_EXPECTED_COMMIT_SHA` in CI to fail when the deployed bridge was not
built from the checked-out source SHA. The benchmark does not write a report
file or provide a browser simulation fallback.

Focused Vitest coverage protects the native views, selection, refuelling commands,
clock, history, display helpers, and transport validation. Smoke checks exercise
fresh-bundle poison, keyboard controls, narrow layouts, challenge completion,
recovery, and the production worker under the GitHub Pages `/CRS/` base path.

## Channel ripple scoring

Studio shows each channel's actual/reference thermal power, ripple ratio, core RMS
deviation and current points/hour. Game owns the fixed 2,064 MW reference profile
with 21 inserted adjusters and the xenon reference, and integrates ripple points; refuelling has no instant bonus.
See [derivation](../../docs/physics/channel-power-reference.md) and
[policy](../../docs/gameplay/score-balance.md).

Verify target changes, fixed references and paused refuelling through the Pages path:

```powershell
node scripts/ripple-smoke.mjs http://127.0.0.1:4173/CRS/
```
