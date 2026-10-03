# Implementation plan review and next actions

Reviewed 2026-10-02 against the current working tree. This is a targeted review
of the implementation guide, web roadmap, 28-task ledger, architecture,
deployment workflow, build scripts, and representative implementation/test
boundaries. It is not a new independent validation of every numerical model.

## Decision

The plan is sound enough to proceed with removal of disposable local build
artifacts. Keep the browser product and shared simulation boundaries, the
small-slice delivery approach, and the consumer checks before source retirement.
There is no reason to restart the completed refactoring or introduce another
simulation/backend for this cleanup.

Implementation completion is distinct from release acceptance. Tasks 05 and 24
remain open for human playtesting and hosted failing/green CI verification.
Do not convert those to complete based on this review or local unit tests.

## Findings, ordered by consequence

| Priority | Finding and evidence | Action |
| --- | --- | --- |
| P1 release acceptance | Task 24 correctly leaves hosted CI acceptance open. The workflow has PR validation, Core/Game/Browser tests, predeploy smoke, a dependent main-only deployment, and postdeploy commit-checked reproduction. Reading this wiring does not establish that it has executed successfully. | Retain the gate; obtain one deliberate failing validation run and one green hosted run before closing task 24. |
| P2 deployment parity | Earlier investigation in this chat loaded the stable Vercel WASM and observed 380 channels online, but the current smoke timed out waiting for `.reactor-launcher`. Thus local implementation evidence is not proof the current UI is deployed. | Verify the intended release through the existing deployment workflow and stable-alias smoke/reproduction. Do not label that selector failure a confirmed WASM bug. No deployment is made by this cleanup. |
| P2 product acceptance | Task 05 still lacks human measurements of first refuel, mistakes, completion, and restart motivation. The balance report establishes authored incentives, not player understanding. | Keep these as the next product observations; tune duration/budget from them rather than widening simulation scope. |
| P2 evidence retention | Historical notes cite ignored `tmp` reports. The 58-tool/101-artifact inventory covers authored tools/data, not all local captures and source backups. `tmp` also contains a nested Git repository and unique source snapshots. | Do not blanket-delete `tmp`. Remove only reviewed generated folders, retain raw evidence, and give durable knowledge an index. Before a later evidence purge, promote necessary raw samples as well as their summaries. |
| P3 plan readability | The task ledger intentionally retains old unchecked lists and dated statements such as “Phase 5 is left wholly unstarted” below its current completed-status table. These are historical, but easily misread as current work. | Add explicit historical/current navigation and use the status table as the authoritative progress view. Preserve the original records rather than rewriting historical results. |
| P3 reproducibility | `Build-BrowserWasm.ps1` records HEAD as `gitCommitSha` when no SHA is supplied. In this heavily modified working tree that value alone cannot reconstruct local source. Removing old binaries must not imply their exact bytes are reproducible from HEAD. | Preserve build metadata and source snapshots. Describe commands as regenerating a build from current source; exact historic reproduction also requires the original source, SDK and flags. |

## Assessment of each delivery slice

| Slice | Assessment | Evidence inspected / continuing constraint |
| --- | --- | --- |
| Trustworthy run (01–04, 08, 24, 28) | Correct first priority. Keep run state and scoring in Game, and explicit transport failure/recovery. | `PracticeScoring` clamps each discharged bundle, uses policy v2 and caps ideal operating points at one/hour. `bridge.ts` has startup/command watchdogs, `messageerror` handling and termination. CI orders validation before deployment. Hosted acceptance remains open. |
| Understandable refuelling (05–07, 10–12) | Appropriate game scope and sequence. Accepted movement/provenance should remain authoritative. | Existing Game movement/provenance contracts, focused tests and task records cover the implementation. Retain the human-playtest gap; do not treat a benchmark policy as the optimal player strategy. |
| Responsive and accessible (09, 13–16) | Measurements support the chosen improvements. Keep performance claims qualified. | `ReplayDigestChain` retains a chained digest rather than the full command history. `AppShell` dynamically loads Designer while sharing the session. The performance report distinguishes shell readiness, WASM readiness, worker traffic, synthetic chart history and machine-specific timings. |
| Separate responsibilities (17–20) | Boundaries match the repository instructions. Preserve transactions and wire semantics during future changes. | Game references Core; Browser references Game; BrowserHost references Browser. Clock, candidate, projection and bridge modules exist. Shared response fixtures and the two-seed 24-response baseline remain available; prior successful hashes are historical evidence, not newly rerun here. |
| Remove obsolete branches (21–23, 25–27) | Consumer-led retirement is safer than deletion by filename/phase number. This slice is already implemented. | Core excludes optional GPU types/resources by default; BrowserHost conditionally includes GPU modules. Research dependency tests and type/fixture inventories remain. Canonical diffusion-pack equality is enforced before Core builds. Preserve retained tools outside the solution and their fixtures. |

The remaining roadmap priorities are reasonable: human feedback and measured
worker/solver latency before further tuning. CPU/GPU experiments remain optional;
neither is a prerequisite for cleaning generated files or playing the game.

## Cleanup boundary

The reviewed allowlist and outcomes are in
[temporary-cleanup-manifest.json](temporary-cleanup-manifest.json). It records
directory counts/bytes and available `build-info.json` contents before removal.
It is an audit record, not a backup of deleted binaries.

Only ignored publish copies, built previews and isolated build/test intermediate
directories under repository `tmp` are selected. Current `public/wasm`, `dist`,
dependencies, source, tests, packs, research fixtures, logs, measurements, captures,
source snapshots, ZIP exports and nested repositories are retained. No blanket
`git clean`, source retirement or commit is part of this slice.

Knowledge and reproduction entry points are organized in the
[repository knowledge library](knowledge-library.md). Test results from this
cleanup are recorded there separately from historical phase results.
