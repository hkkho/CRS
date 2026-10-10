# Single-step daily campaign — 100 days

**Passed:** seed 1001, two highest-burnup eligible channels refuelled per day,
normal endless browser practice. Each turn uses one 86,400-second exposure/poison
interval and one final equilibrium/RRS solve (`practice-daily-one-step-v1`). No
physics, scoring, inventory or power overrides were applied.

| Result | Value |
| --- | --- |
| Completed days | 100 |
| Score | 2,320.912464 |
| Fuel operations / bundles | 200 / 1,600 |
| Mean discharged-bundle burnup | 6.182297 MWd/kg HM |
| Discharged-bundle burnup range | 4.372579–7.908013 MWd/kg HM |
| Final average LZC | 46.976244% |
| Final axial tilt | +0.581427% |
| Day-boundary average LZC range | 46.959235–51.130873% |
| Maximum observed channel / bundle power | 6,078.732 / 567.338 kW |
| Thermal energy | 4,953,600 MWh |
| Operating-limit or numerical ending | None |

The harness drives the native .NET versioned Browser bridge using authoritative
Core/Game physics. It validates exact day increments, frozen daily pacing,
energy/fuel/score accounting and finite measurements. Observations cover day
boundaries; the single-step model does not calculate intermediate intraday states.
Browser UI, animation and offline save/replay are checked separately using the
production AOT WASM `/CRS/` smoke path.

Artifacts:

- [Full report](single-step-oldest-2-seed1001.json): initial/final states, 101
  boundary observations, every fuel movement, integration identity, assembly hashes.
- [Telemetry](single-step-oldest-2-seed1001.jsonl): one initial row and 100 daily rows.
- [Commands](single-step-oldest-2-seed1001-commands.jsonl): exact initialization
  and 100 accepted daily commands with state/replay digests.
- [Browser acceptance](browser-acceptance.json): corrected AOT WASM daily loop,
  survival/loss dialogs, 320px layout and offline daily save/replay all passed.
- [Display-range acceptance](display-scales-browser-acceptance.json): benchmark
  color floors, zoomed trends, outlier expansion, limit annotations and 320px
  layout passed against production WASM.
- [V7 fuel-only decay audit](v7-fuel-decay-audit.json): a separate seed-1001
  one-full-power-day exposure with no refuelling, fixed 50% zone fills and frozen
  actual xenon measured a 0.347430 mk/FPD reactivity loss. This isolates fuel
  depletion and is not the net regulated reactivity trend of the 100-day campaign.

Reproduce from the repository root:

```powershell
dotnet run --project tools/LongRunPlaytest -c Release -- --pacing=daily-turn --policy=oldest --channels-per-day=2 --days=100 --seed=1001 --output=benchmarks/daily-turns-100-days-2026-10-09/single-step-oldest-2-seed1001
```

Recorded elapsed time was 337.8 seconds with concurrent tests/builds; it is not
an isolated latency benchmark. The report timestamp is UTC (October 10); the
campaign ran October 9 in the user's America/New_York timezone.

The `oldest-2-seed1001.*` artifacts describe the interrupted two-day campaign
using the earlier subdivided model. They are superseded and are not evidence for
the corrected 100-day result.
