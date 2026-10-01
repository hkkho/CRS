# CANDU web game roadmap

The product is `web/candu-playtest`, backed by the shared C# simulation and
published through the existing Vercel pipeline.

## Working standard

Prioritize a playable, understandable reactor game. Project-authored and
synthetic data are acceptable when they produce plausible trends. Formal source
verification, plant calibration, historical deployment SHA reconciliation, and
research-grade validation are not prerequisites for gameplay development.
Describe approximations honestly; do not represent the model as a validated
plant simulator.

Keep deterministic rules in Core/Game, serialize them through Browser, and use
Phaser for presentation and input. Preserve finite values, units, inventory
accounting, atomic failed commands, and reproducible replay. These protect a
working game rather than certify the underlying physics data.

Work in small playable slices. A UI change does not require changes or new tests
in every architecture layer. Run focused behavior tests and the repository test
scripts appropriate to the change. Exercise the production-shaped browser smoke
path for releases. Report unavailable deployment checks without blocking useful
local work on unrelated infrastructure or historical provenance.

## Playable loop

1. Inspect the power or burnup map. Find oldest fuel is a navigation shortcut,
   not a prediction or mandatory move.
2. Choose direction and four or eight bundles. Fuel stock is the resource cost.
3. Refuel directly and compare the accepted snapshot before/after: local power,
   signed tilt, reserve, inventory, and score. Cosmetic transfer feedback does
   not impose a cooldown.
4. Run time to earn operating score; pause to inspect. Space toggles pause/resume.
5. Watch all fourteen zone fills and retain regulating headroom. Start a new
   shift from Operations when ready to try another strategy.

Refuelling points are awarded per discharged bundle using
`0.75 * clamp(burnup_MWd_per_kg, 0, 10) - 1.5`. Fresh-fuel waste therefore costs
points; reversing a shift cannot farm a fixed acceptance bonus. This is a game
balance choice, not an economic or reactor-design claim.

## Next improvements

- Tune run duration and fuel budget from actual playthroughs.
- Improve RRS controller feedback, including explicit reasons for retained fills.
- Add short optional challenges using shared Game rules and visible rewards.
- Improve keyboard accessibility and label sizes at 1280×720.
- Measure worker/solver latency before changing solver fidelity or payload shape.

Useful reference details remain in `IMPLEMENTATION_GUIDE.md` and
`physics/active-two-group-solver.md`. The latter documents what is implemented;
it is not a requirement to extend the model into a research code.

Shutdown, scram, accident progression, operator-training scenarios, and full
plant operations remain outside this game's scope.
