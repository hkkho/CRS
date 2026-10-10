# Web game roadmap

The only developed product is `web/candu-playtest`, backed by Core/Game/Browser
and published on GitHub Pages. Daily turns are the default acceptance path.
Keep rules in the shared simulation and presentation in native DOM/SVG. Preserve
deterministic replay, atomic numerical failures, units and inventory accounting.

## Delivered loop

1. Inspect a frozen core, bundle profiles, burnup and fourteen LZC levels.
2. Choose today's eight-bundle fuel plan and inspect discharge previews.
3. Review or clear the plan without consuming fuel/time.
4. Execute one large 24-hour exposure/poison update and final equilibrium solve.
   The waiting dialog ends with survival/loss and score.
5. Inspect the authoritative report and next decision, or retry after a loss.

See [daily turns](gameplay/daily-turn-mode.md) and [v8 axial correction](physics/axial-marshak-v8.md),
including its successful 100-day LZC-guided policy. Benchmark-based maps/charts
reveal daily changes while retaining outliers. Endless practice, the optional
one-day challenge, seeded retries, movements, ripple scoring, history, keyboard
input, narrow layouts, guest saves and private cloud saves are implemented.
Legacy pacing remains a compatibility/developer path inside the same client.

## Next improvements

- Gather human feedback on channel selection and understanding the day report.
- Refine fuelling demand and axial/radial shape against retained physics studies.
  Record calibration changes, device worths, save identity and campaign effects.
- Explain adjusters, LZC headroom and discharge burnup through existing observations.
- Measure full-day latency on multiple devices while retaining solver fidelity.
  Improve feedback/rendering before considering numerical changes.
- Expand optional scenario configuration and challenges after playtest feedback.
- Retain keyboard, layout, replay, cloud revision and published-worker checks.

[Implementation](IMPLEMENTATION_GUIDE.md), [architecture](architecture.md) and the
[knowledge library](maintenance/knowledge-library.md) describe current behavior.
Retired research engines, GPU experiments and alternate hosts are no longer
development paths. Shutdown, scram, accidents, operator training and full plant
operations remain outside scope.
