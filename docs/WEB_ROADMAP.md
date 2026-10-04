# CANDU web game roadmap

The product is `web/candu-playtest`, backed by the shared C# simulation and
published through the GitHub Pages pipeline.

## Working standard

Prioritize a playable, understandable reactor game. Project-authored and
synthetic data are acceptable when they produce plausible trends. Formal source
verification, plant calibration, historical deployment SHA reconciliation, and
research-grade validation are not prerequisites for gameplay development.
Describe approximations honestly; do not represent the model as a validated
plant simulator.

Keep deterministic rules in Core/Game, serialize them through Browser, and use
native DOM controls and Phaser for presentation and input. Preserve finite values, units, inventory
accounting, atomic failed commands, and reproducible replay. These protect a
working game rather than certify the underlying physics data.

Work in small playable slices. A UI change does not require changes or new tests
in every architecture layer. Run focused behavior tests and the repository test
scripts appropriate to the change. Exercise the production-shaped browser smoke
path for releases. Report unavailable deployment checks without blocking useful
local work on unrelated infrastructure or historical provenance.

## Playable loop

1. Inspect the power or burnup map. Highest burnup is a navigation shortcut,
   not a prediction or mandatory move.
2. Refuel eight bundles automatically with channel flow. Inspect the marked outgoing positions
   and burnup; Core supplies the movement map. Fuel stock is the resource cost.
3. Refuel directly and compare the accepted snapshot before/after: local power,
   signed tilt, average LZC level, inventory, and score. Cosmetic transfer feedback does
   not impose a cooldown.
4. Run time to earn operating score; pause to inspect. Space toggles pause/resume.
5. Watch all fourteen zone fills. Average LZC level below 10% or above 90%, or
   absolute global tilt above 20%, ends the run. Start a new
   shift from Reactor Studio when ready to try another strategy.

Reactor Studio is the sole gameplay view. The simulation-time history tabs
cover power peaks, confirmed discharge burnup, fourteen zone fills and their
mean, iodine/xenon inventories, regional xenon, tilt, Keff/reactivity, average LZC level,
fuel and score. History stays with the
browser session across Core Designer visits and resets with a new shift. Accepted
physical Designer edits mark a run as modified sandbox; inspection and rejected
edits preserve standard eligibility. Sandbox play remains available, with standard
challenge rewards excluded until reset. Controller reason text and limiting-zone
headroom come from the shared snapshot rather than a fill-delta guess.


Refuelling points are awarded per discharged bundle using
`0.75 * clamp(burnup_MWd_per_kg, 0, 10) - 1.5`. Fresh-fuel waste therefore costs
points; reversing a shift cannot farm a fixed acceptance bonus. Operating points add at most one point per simulated hour:
`hours * (0.7 * powerQuality + 0.3 * tiltQuality)`. The authored quality measures
still use power near nominal and low core tilt. See the [same-seed balance
comparison](gameplay/score-balance.md) for the rationale and limits. The current
policy is `practice-fuel-and-operation-v2`; previous totals are not comparable
across policy versions. This is a game balance choice, not an economic or
reactor-design claim.

## Next improvements

- Tune run duration and fuel budget from actual playthroughs.
- RRS branch explanations and limiting-zone headroom are delivered (task 11).
- Expand the existing one-day challenge after human playtest feedback.
- Native launcher/Designer actions, keyboard play at 1280×720, 200% zoom emulation,
  a 320px minimum layout and quiet operation announcements are delivered (task 13).
  Phase 3 also delivers an on-demand Designer shell, cached/patchable rendering,
  full-history display reduction and observed pace (tasks 14–16). See
  [measured results and budgets](performance/browser-phase3.md).
  Retain keyboard, narrow-layout and production-worker checks during the next
  maintenance phase. Responsibility boundaries (tasks 17–20) are delivered.
- Measure worker/solver latency before changing solver fidelity or payload shape.
- Evaluate [CPU parallel execution](physics/cpu-parallel-experiment.md) through
  exact numerical/replay comparisons and complete-command browser benchmarks.
  The [alternate background host](physics/cpu-main-thread-hosting.md) now works
  in developer tests; benchmark the [single-snapshot controller proposal](physics/single-snapshot-step-proposal.md)
  before changing gameplay integration.
  GPU migration is paused; gameplay remains on the shared CPU solver.

Current task status is [REFACTORING_TASK_GUIDE.md](REFACTORING_TASK_GUIDE.md);
current boundaries are [architecture.md](architecture.md). Historical specs and
paused proposals remain reference material, not active product gates.

Useful reference details remain in `IMPLEMENTATION_GUIDE.md` and
`physics/active-two-group-solver.md`. The latter documents what is implemented;
it is not a requirement to extend the model into a research code.

Shutdown, scram, accident progression, operator-training scenarios, and full
plant operations remain outside this game's scope.

## Phase 4 refactor acceptance

The Game clock/projection, instance bridge and frontend view boundaries preserve
the same playable loop and protocol v2. The browser still consumes authoritative
C# snapshots. Characterization compares complete serialized response bytes,
shared fixtures cover transport variants, and repeated Designer navigation checks
selection/draft/history/response retention and subscription cleanup. Research IQS
is separated from product dependencies; wider legacy retirement and experiment
isolation remain Phase 5 tasks. See `REFACTORING_TASK_GUIDE.md` for final checks
and the outstanding human/hosted-CI follow-ups.
