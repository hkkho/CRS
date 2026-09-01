# CANDU Refuelling Game

This repository is building an interactive Unity game about steady-state CANDU
on-power refuelling. The immediate goal is a playable synthetic-physics game;
DRAGON5/DONJON5-derived data should improve its realism after the complete game
loop works.

Read [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) first. It
contains the repository review, target player experience, architecture, and the
ordered implementation path.

## Current state

- `src/ReactorSim.Core` contains substantial simulation, refuelling, depletion,
  spatial, xenon, control, and scenario contracts.
- `src/ReactorSim.Cli` runs the current synthetic scenario model headlessly.
- `unity/ReactorGame` now starts a real synthetic practice session, binds it to
  the dashboard/controls/timeline, and advances it continuously in fixed 100 ms
  wall-time requests. Player-controlled channel refuelling and the Core Map are
  the next Milestone 1 slice.
- `data` and `reference` contain synthetic packs, literature-derived design
  context, and incomplete DRAGON5/DONJON5 integration work.

## Quick start

Install Unity `6000.3.21f1` and a .NET `10.0.3xx` SDK, then run:

```powershell
dotnet build ReactorSim.sln
powershell -ExecutionPolicy Bypass -File tools/Prepare-UnityCore.ps1
```

Open `unity/ReactorGame` in Unity and run `Assets/Scenes/Bootstrap.unity`. The
practice scenario begins automatically at 10x simulation speed; use the Controls
page to pause, resume, change playback speed, or queue power and tilt targets.

Automated tests are intentionally limited to focused checks for code being
changed. The primary acceptance path is a playable build exercised through the
in-game debug menu. Historical task, gate, approval, and review language in
supporting research is not an active development requirement.
