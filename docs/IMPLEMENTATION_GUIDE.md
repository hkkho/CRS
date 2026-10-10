# CANDU web game implementation guide

## Product and ownership

Develop `web/candu-playtest`, with GitHub Pages as the primary acceptance path.
Reactor Studio is the sole gameplay view. Keep changes small, playable and testable.
Shutdown, scram, accidents, operator training, full plant operations and plant-grade
safety claims are outside scope.

- Core owns deterministic topology, inventory, refuelling, exposure, poison,
  controls and spatial solving.
- Game owns sessions, commands, daily transactions, scoring, terminal decisions
  and immutable presentation snapshots.
- Browser owns the versioned contract and instance runtimes.
- BrowserHost owns the single-threaded .NET browser-WASM executable.
- The frontend owns transport, input, history and DOM/SVG presentation.

The client consumes authoritative snapshots and fails closed without WASM. Do not
duplicate reactor rules in TypeScript. Preserve units, group ordering, channel-major
indexing, deterministic digests and failure semantics. See [architecture](architecture.md)
and [protocol v2](spec/browser-playtest-protocol-v2.md).

## Daily decisions

New runs use `pacingMode: daily-turn` and remain paused during inspection. Players
draft today's channels, review outgoing bundles, then refuel and advance a day or
advance without fuel. Planning consumes no fuel or time. Game validates unique
indices and `expectedCompletedDays`, sorts the plan and executes eight-bundle moves
with channel flow. Adjacent channels have opposite flow. Core's `GameRefuellingPlanV1`
supplies execution and preview position maps; previews predict movement only.

One large 86,400-second interval updates burnup, energy, score and analytic poison
once using post-refuelling power/flux. One final equilibrium/LZC solve determines
the next decision. Intermediate intraday states are not calculated. Integration
identity `practice-daily-one-step-v1` distinguishes saves from subdivided integration.

A detached session/clock/solver makes numerical failure atomic across the day.
Operating limits are checked after each fuel move and at day end. A violating move
commits that move and stops remaining orders; a day-end loss commits the completed
update. Reports retain accepted/unexecuted orders, bundle identities, discharge
burnup, fuel, energy, points and ending reason. Full and compact snapshots publish
pacing, completed days and `lastDayResult`. See [daily semantics](gameplay/daily-turn-mode.md).

The scheduler never ticks daily sessions. The waiting dialog supports reduced
motion and flashes survival/loss with day points and total score. Rejection and
interrupted unknown outcomes are distinct from physical losses. Solver accuracy
takes priority over real-time latency. Explicit `?pacing=real-time` and version-1
replay retain the old clock for compatibility/comparison. New development targets
daily turns.

## Limits, score and fuel

Free practice is endless with unlimited stock; consumption remains counted. The
optional one-day challenge has finite stock and rewards discharging at least eight
bundles at 6 MWd/kg HM or above while finishing within limits. Retry preserves seed
and objective; new shift draws a new aged-core seed.

A run ends outside 10-90% average LZC, beyond 20% absolute global tilt, above
7,300 kW/channel or above 935 kW/bundle. Equality is allowed. Game publishes status
and reason separately from numerical diagnostics/RRS exhaustion. Terminal runs
freeze until reset. See [power limits](gameplay/power-limits.md).

Policy `practice-channel-ripple-v3` compares channel powers to fixed time-average
references summing to 2,064 MW thermal. Equal-channel RMS relative deviation sets
`points/hour = 1 / (1 + (RMS / 0.10)^2)`. Accepted intervals earn points; fuel moves
have no direct bonus/cost. See [score balance](gameplay/score-balance.md).

Use `FreshBundlesAvailable` and `UnlimitedFreshFuel` for stock, not request counters.
Movement reports retain nullable old/new positions and confirmed discharge values;
null means unavailable. Rejection preserves readings. Engineering edits mark modified
sandbox provenance and exclude standard challenge rewards until reset.

## Physics and presentation

V8 uses zero incoming partial current at axial ends with Marshak conductance,
separately from fitted radial leakage. Reflecting boundaries retain zero conductance.
Criticality, 17/7 mk device refits, references and save identity are documented in
[axial v8](physics/axial-marshak-v8.md). Preserve [active equations](physics/active-two-group-solver.md)
and useful [physics knowledge](maintenance/knowledge-library.md). Packs are authored
approximations for plausible gameplay, not validated plant data.

Poison follows bundle identity. Fresh fuel enters with zero iodine/xenon; retained
fuel carries its inventory. Burnup uses thermal fission energy/heavy-metal mass.
Reference output is 2,064 MW thermal; 650 MW electrical is a presentation estimate.
Controller explanations and limiting-zone headroom come from shared data.

Studio provides power/ripple/burnup maps, axial profiles, fuel shortlist, device
geometry and history tabs. History keeps up to 4,096 complete observations and
clears on new initialization; it is presentation data. Historical inspection disables
operations. Benchmark-based chart bounds expand for readings and label off-scale
limits. Preserve keyboard input, 320px layouts, stable focus, reduced motion and
drafts across navigation. Remove listeners/subscriptions when views close.

## Saves and release checks

Version-2 saves include pacing/drafts, replay shared commands and reject incompatible
pack/scoring/integration identities. Version-1 uses real-time replay. Cloud saves
preserve ownership/revision checks; apply the [daily migration](../supabase/migrations/20261009232415_daily_turn_saves.sql)
before version-2 uploads. Guest/offline play remains available. See [accounts](maintenance/player-accounts.md).

Use `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1`, with `-Configuration Release`
to match CI. Add focused changed-behavior tests. Releases publish AOT WASM, build
under `/CRS/`, run daily/save/graph smoke and direct solver reproduction, and verify
the published build commit and live loop. Inspect the final diff. Exact commands
are in [tools](maintenance/research-tools.md) and [hosting](maintenance/hosting.md).
