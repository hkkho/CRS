# Research scope and evidence

Research date: **24 October 2026**. The sample starts with itch.io's
[top-rated browser simulation listing](https://itch.io/games/top-rated/genre-simulation/platform-web),
then deliberately adds different kinds of simulation: administration, logistics,
infrastructure, incremental production, care, artificial life and creative building.
Ratings identify established examples, not an objective ranking of fun. Listing
order, rating counts and builds can change. This is a purposive design study,
not a representative survey or an experiment proving what causes retention.

The ten primary simulations are Sort the Court!, Peggy's Post, Waterworks!,
1,000,000 shrimp, Peckin' Pixels, The MachinEGG, plant daddy, Evolution,
Orb of Creation and Gourdlets. Planet Life and Tiny Islands are adjacent
incremental/adventure and spatial/card references; they are not counted as
additional simulation-tagged games merely to reach ten.

## What evidence means

- **Creator report:** facts stated on a game's official page or developer log.
- **Observed:** a screen, input or state change actually inspected in this session.
- **Interpretation:** a proposed explanation of why the design may engage players.
- **Original recipe:** our implementation specification, including suggested timings,
  numerical tuning and graphics. These are not measurements or source code from
  the reference game.
- **Untested:** later progression, ending, device compatibility or audio quality
  that this session did not establish.

Every case study names its evidence and limitations. A screenshot of a title
screen does not count as a completed gameplay loop. A store-page claim of mobile
support does not count as testing on a phone. A development pipeline's benchmark
claims describe its authors' evaluation; we have not independently reproduced it.

## Inspection protocol

For each game, record the first clear player verb, the information needed to make
a decision, the scarce resource or creative constraint, the immediate feedback,
the next goal and one likely source of friction. Inspect the actual browser build
where it runs. Distinguish a test-environment loading problem from a game defect.
Keep short-session observations separate from the creator's longer-term features.

Inspect animation as a sequence: anticipation, action, visible state change,
acknowledgement, recovery. Inspect art as production constraints: silhouette,
palette, projection, material, scale and interface hierarchy. Do not infer an
engine or a neural-network implementation from appearance; cite the creator or
repository for those claims.

## Reproduction scope

The accompanying Pattern Lab implements original, small demonstrations of ten
transferable mechanics and their feedback. It does not reproduce every feature,
balance curve, animation frame or visual asset of ten complete games. Its art,
names, layouts and source code are newly authored. Reference links credit the
games that motivated each study. No original game files, music or character art
are bundled.

The useful unit to reproduce is a relationship: a choice spends a resource,
the world visibly changes, and the change makes the next choice more interesting.
The skill must then test whether that relationship works for the user's idea.
