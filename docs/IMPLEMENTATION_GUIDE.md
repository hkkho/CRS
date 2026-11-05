# CANDU web game implementation guide

## Product and ownership

Develop `web/candu-playtest`, with GitHub Pages as the primary acceptance path.
The public launcher starts time-based free practice. Keep changes small,
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

## Shared daily-turn contract

Daily sessions use `pacingMode: daily-turn` and remain paused during inspection. Players
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
## Time-based play

The browser starts a worker with the existing `demo` pacing identifier.
Game owns atomic 43,200-second steps. DemoLoop interpolates endpoints over
five seconds, with one solved endpoint buffered ahead. The next unsolved step
captures the entire waiting fuel queue. Yellow outlines persist until the batch
is displayed, and a white outline marks the inspected channel. Slow solves hold
the last endpoint. Pause and hidden tabs freeze playback. Start explicitly
unfreezes playback after setup or restore.

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

Time-based update checkpoints replay accepted shared commands and verify the
final state digest, data pack, scoring policy, and integration identity. The
checkpoint uses the latest completed solver endpoint plus waiting fuel orders.
IndexedDB stores the replay and session storage points the reloading tab to it.
The game waits for other tabs to close, commits the checkpoint, then switches
builds automatically. Failed writes prevent reload. Incompatible checkpoints
remain stored. Legacy daily saves use a separate storage namespace.

Use `tools/Test-DotNet.ps1` and `tools/Test-Browser.ps1`, with `-Configuration Release`
to match CI. Add focused changed-behavior tests. Releases publish AOT WASM, build
under `/CRS/`, run time-based gameplay and update smoke and direct solver reproduction, and verify
the published build commit and live loop. Inspect the final diff. Exact commands
are in [tools](maintenance/research-tools.md) and [hosting](maintenance/hosting.md).
