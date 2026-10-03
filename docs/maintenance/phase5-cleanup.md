# Phase 5 bounded cleanup decisions

## Task 22 consumer check

Search included source, tests, tools and browser scripts (including ignored source,
excluding generated bin/obj). `drawMeter` had no caller. `toggleShiftCount` and
`adjustTarget` were used only by their own test assertions; native Studio owns
explicit size/range controls. These helpers and their self-only assertions were
removed. Refuelling draft/affordability/direction tests remain executed.

`maxBacklogMs` and `LIVE_CLOCK_MAX_BACKLOG_MS` had no caller. The compatibility
option only validated an ignored value; removal preserves the scheduler's single
pending quantum and all meaningful timing/backpressure tests. `maxDispatchMs`
still validates the minimum tick limit and is retained.

The Game `KineticsDataPackVersion` alias had two consumers: Browser metadata and
its assertion. Both now name `DiffusionDataPackVersion` with identical wire value.
The unrelated retained adjoint metadata property has its own kinetics meaning
and was not removed. `JsonSerializationCompatibilityProbe` had no code consumer;
its generic token echo is removed. Real pack parsing/serialization and browser
shared-wire/replay fixtures preserve useful coverage instead. Historical ADR
references describe past admission and are not current development requirements.

## Task 25 Golden shell

`ReactorSim.Golden.Tests` had no authored test sources; its only compile link was
the shared test data locator. The empty project and solution entries are removed.
The fixture inventory is `golden-fixture-consumers.json`, with per-file other
text consumers and existence checks. All source data/metadata remain retained:
an empty test shell is not evidence for deleting research fixtures. The executed
Core/Game/Browser suites and six shared C#/TS serialized browser fixtures are the
current validation surfaces. No new golden outcomes or tolerances are invented.

## Task 23 experiment boundary

`EnableResearchExperiments=true` explicitly includes GPU fixture types, WGSL,
bridge/host exports and JavaScript GPU modules. Default builds exclude them.
CPU threading remains independently opt-in; profiling exports require their
existing profiling option. The shared row operator and experimental numerical
budgets/reduction order are unchanged. Reproduction commands and budgets are in
[research-builds.md](research-builds.md).

Default Release AOT publish size, excluding precompressed copies, changed from
29,669,189 to 29,490,523 bytes (178,666 bytes smaller). This is the combined
cleanup difference, not a GPU-only attribution. Both GPU JavaScript modules are
absent from default publishing. Hashed framework names also changed; renamed
files in the report are not deleted assemblies. The optional research publish
used non-AOT Release and is not a comparable performance/size claim. Per-file
evidence is [the publish audit](../../benchmarks/phase5-publish-boundary.json).

Default suites passed 109 Core, 52 Game, 33 Browser and 126 frontend tests, plus
production build. Opted-in Core passed 114 tests; isolated Browser passed its
three research/boundary checks; GPU JavaScript tests passed five checks. Actual
WASM export smoke passed both configurations and read-only fixture generation.
Full default browser smoke passed direct refuelling, Designer return, zone edits,
challenge completion, modified reward exclusion and seeded retry. No hosted
deployment or new GPU speed claim is made.

## Task 27 documentation audit

README, implementation guide and roadmap now link the current architecture and
task ledger. Acceptance follows direct orders in both sizes/directions; replay
archives remain developer tooling. Requested/observed pace, terminal state,
modified rewards, score formula and current commands are documented. Historical
specs and paused proposals are indexed in
[historical-research.md](historical-research.md). All local links in these current
pages were checked, and the local production walkthrough passed the smoke above.
The wider Core-family and tool/data classification remains tasks 21 and 26.
