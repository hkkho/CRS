# CANDU web game implementation guide

## Product and ownership

Develop `web/candu-playtest`, with GitHub Pages as the primary acceptance path.
Reactor Studio provides detailed play; Arcade presents the same shared one-day
challenge with a full interactive core map, touch zoom, plan review and a result screen. Keep changes small,
playable and testable.
Shutdown, scram, accidents, operator training, full plant operations and plant-grade
safety claims are outside scope.

- Core owns deterministic topology, inventory, refuelling, exposure, poison,
  controls and spatial solving.
- Game owns sessions, commands, daily transactions, scoring, terminal decisions
  and immutable presentation snapshots.
- Browser owns the versioned contract and instance runtimes.
- BrowserHost owns the single-threaded .NET browser-WASM executable.
- The frontend owns transport, input, history and DOM/SVG presentation.

TypeScript builds reject unused local code, imports and parameters. Keep browser
helpers limited to the active Studio; compile/test tools are development dependencies.

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
takes priority over real-time latency. The Studio/Arcade controller requires daily pacing,
rejects legacy real-time saves and commands, and refuses mismatched runtimes.
Old URL options cannot enable the live clock. Direct comparison tools retain
real-time contracts. Time based play retains the internal `demo` pacing identifier for bridge compatibility, with a separate ephemeral worker/session. Game owns atomic 43,200-second steps; completed days count full 86,400-second days. `DemoLoop` interpolates each half-day endpoint over five seconds and buffers one upcoming endpoint. Fuel uses authoritative `commit-refuel` commands at the first free worker opportunity, ahead of time advances even if a future endpoint is buffered. Duplicate pending requests are ignored and waiting input is bounded to eight channels. Channel powers and zone fills interpolate for presentation only; inventory, score and terminal outcomes use authoritative endpoints. Slow solves hold the endpoint without extrapolation or catch-up. Pause and hidden tabs freeze display time and retain input; an active solve may finish into the buffer. The launcher lists Time based first. Both mode entry paths show a modal loading gate and require an explicit Start after setup; time-based playback remains paused until that action. Setup failures keep Start hidden and offer Back. The HUD shows a ticking simulation clock, without a speed value. A native dropdown above the time-based core offers channel power, ripple, burnup and paired zone fills. Each paired cell shows End A on the left and End B on the right using published bundle logical-zone IDs and live zone levels; missing geometry is grey. Legends and hover/keyboard readings follow the chosen map. Fuel arriving during a calculation waits for that calculation to commit, then dispatches immediately before further time advances. Core already interpolates material coefficients by bundle burnup and refuelling rebuilds equilibrium with retained bundle history and fresh zero-burnup fuel; display-time interpolation never supplies cross-sections to the solver. Standard daily saves/drafts and leaderboard paths remain separate.

## Limits, score and fuel

Free practice is the only selectable run. Both Studio and Arcade use endless
play with unlimited stock; consumption remains counted. Historical challenge
contracts remain readable for compatibility, but launcher/new-run/retry controls
never create them. Seeded retries retain the seed.

The full-width core face has compact heading controls; decorative rails and
on-map device text are removed. The right column combines the fuel sheet with
an expanded details dock: LZC levels, channel readings, fuel movement and profiles.
History inspection, Keff, tilt and map colors remain visible above the workspace.

Startup uses the fixed time-average reference's node powers to derive the
fourteen RRS target fractions. It verifies a bounded startup shape correction
inside the ordinary event deadband, then settles equilibrium xenon with those
fills. Later events retain their existing deadband and candidate acceptance
rules. The nominal reference is still derived with 50% zones; the actual aged
core's initial fills are calculated rather than set to 50% for display.

The daily run-model identity is `practice-daily-one-step-reference-zones-v2`:
old daily saves are retained but rejected before candidate restore. The day
still uses one 86,400-second integration step.

A run ends outside 10-90% average LZC, beyond 20% absolute global tilt, above
7,100 kW/channel or above 900 kW/bundle. Equality is allowed. Game publishes status
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

V9 uses a circular extrapolated zero-flux boundary in X/Y and retains the v8
zero incoming partial current at axial ends with unchanged Marshak conductance.
Reflecting boundaries retain zero conductance.
Criticality, 17/7 mk device refits, references and save identity are documented in
[radial v9](physics/radial-circle-v9.md). Preserve [active equations](physics/active-two-group-solver.md)
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
pack/scoring/integration identities. Version-1 real-time saves remain stored but
cannot resume in the public game. Cloud saves
preserve ownership/revision checks; apply the [daily migration](../supabase/migrations/20261009232415_daily_turn_saves.sql)
before version-2 uploads. Guest/offline play remains available. The loaded build
and physics model are visible. A waiting update saves locally, requires other game
tabs to close, then unregisters the old worker and reloads a complete current build.
Failed saves prevent reload. See [accounts](maintenance/player-accounts.md).

Use `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1`, with `-Configuration Release`
to match CI. Add focused changed-behavior tests. Releases publish AOT WASM, build
under `/CRS/`, run daily/save/graph smoke and direct solver reproduction, and verify
the published build commit and live loop. Inspect the final diff. Exact commands
are in [tools](maintenance/research-tools.md) and [hosting](maintenance/hosting.md).
