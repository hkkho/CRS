# CANDU Refuelling Game

This repository is building an interactive Unity game about steady-state CANDU
on-power refuelling. The immediate goal is a playable synthetic-physics game;
DRAGON5/DONJON5-derived data should improve its realism after the complete game
loop works.

Read [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) and [`gemini_review.md`](gemini_review.md)
first. They contain the comprehensive repository review, architectural boundaries,
target player experience, and ordered implementation path.

## Current state

- `src/ReactorSim.Core` contains substantial engine-neutral simulation, refuelling,
  depletion, spatial diffusion, xenon/iodine dynamics, control systems (LZC, adjusters,
  bulk poison), and deterministic scenario contracts.
- `src/ReactorSim.Game` provides the reusable application session layer (`GameSession`),
  snapshot projections, and player command orchestration.
- `src/ReactorSim.Cli` runs synthetic scenarios headlessly for validation, baseline
  policy checks, and long-horizon soak testing.
- `unity/ReactorGame` starts a real synthetic practice session, binds it to the
  dashboard, Controls, Timeline, and Core Map views, plus the F1/backquote debug
  overlay. It advances the session continuously in fixed 100 ms wall-time requests;
  the Core Map presents 380 selectable channels, 12-bundle burnup details, preview
  and commit actions, and deterministic localized power/tilt/score feedback.
- `web/candu-playtest` is a companion browser pivot for algorithm and interaction
  playtesting. It uses the same `ReactorSim.Game` Play session through the
  versioned browser bridge when WASM is available. Development and explicit
  compatibility-debug URLs may expose a visibly labelled fixture, while
  production fails closed if the bridge is not loaded. It is not a second
  authoritative simulator or a replacement for Unity acceptance.
- `data` and `reference` contain synthetic packs, literature-derived design context,
  and offline DRAGON5/DONJON5 integration specs.

## Implementation Roadmap

As detailed in [`gemini_review.md`](gemini_review.md):
1. **Milestone 0.5 — Browser algorithm/playtest pivot:** Build and host the
   small Vite/Three.js console in [`web/candu-playtest`](web/candu-playtest/).
   Keep Play mode backed by `GameSession`, make Lab mode call the existing Core
   spatial solver through browser WASM, and capture local replay/digest/feedback
   data so UI and algorithm decisions can be iterated before Unity presentation
   work. The browser surface is public synthetic data only, contains no backend
   or authentication, and remains a companion validation tool.
2. **Complete — Slice 1 (Milestone 1):** Interactive 380-channel Core Map heat map
   with channel selection, 12-bundle axial profile inspection, and refuel preview.
3. **Complete — Slice 2:** Deterministic localized power/tilt feedback and
   fuel-utilization scoring are connected to committed refuelling.
4. **Complete — Slice 3 (Milestone 2):** In-game debug and playtesting menu
   (`F1` / backquote overlay) for time jumps, playback, restart, inventory, and
   snapshot diagnostics.
5. **Slice 4 (Milestone 3):** Refuelling candidate recommendations and operational
   trade-off guidance.
6. **Slice 5 (Milestone 4):** Full 3D two-group spatial diffusion solver (`SpatialEigenSolve`)
   integrated on a background simulation cadence.
7. **Slice 6 (Milestone 5):** Offline DRAGON5/DONJON5 runtime data pack pass.

## Quick start

Install Unity `6000.3.21f1` and a .NET `10.0.3xx` SDK, then run:

```powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
```

Open `unity/ReactorGame` in Unity and run `Assets/Scenes/Bootstrap.unity`. The
practice scenario begins automatically at 10x simulation speed; use the Controls
page to pause, resume, change playback speed, queue power and tilt targets, or
refuel a numbered channel toward either end. The synthetic practice inventory
starts with 128 fresh bundles and currently accepts `NAT-U-SYNTHETIC` fuel in
four- or eight-bundle shifts. Use the Core Map page to inspect the 380 channels,
preview a shift, and commit it; press F1 or backquote for the debug overlay.

Automated tests are intentionally limited to focused checks for code being
changed. The primary acceptance path is a playable build exercised through the
in-game debug menu. Historical task, gate, approval, and review language in
supporting research is not an active development requirement.

### Browser playtest pivot

The browser console is launched independently from the Unity project:

```powershell
cd web/candu-playtest
npm install
npm run dev
```

For a production-shaped static build, stage the authoritative bridge with
`tools/Build-BrowserWasm.ps1` and then run `npm run build`. The production
deployment workflow in `.github/workflows/deploy-candu-playtest.yml` performs
that staging, verifies the Vercel prebuilt output, and runs the browser smoke
test. Vercel hosts the directory as a static Vite project; there is no runtime
server, user account, or reactor-analysis executable involved. See the web
README for the bridge, replay, and deployment details.

## Codex agent collaboration

- When creating an agent, select GPT-5.6 Luna with reasoning effort `max` for
  all coding and implementation work.
- Select GPT-5.6 Terra with reasoning effort `high` only when clarification is
  needed.
- Select GPT-5.6 Sol with reasoning effort `medium` only when something is
  critically stuck.

