# Repository knowledge library

Start here for decisions and measurements worth keeping after local build output
is discarded. This is an index of maintained Markdown notes, not another copy of
the simulation specification. Raw numerical fixtures stay in their original
machine-readable formats and retain their existing consumers.

## Current product and plan

- [Product contract](../IMPLEMENTATION_GUIDE.md): authoritative ownership,
  gameplay scope, browser contracts and contributor commands.
- [Web roadmap](../WEB_ROADMAP.md): next player-facing improvements.
- [Current architecture](../architecture.md): session, worker and build boundaries.
- [Task ledger](../REFACTORING_TASK_GUIDE.md): completed implementation and the two
  outstanding human/hosted-CI acceptance tasks; later sections are history.
- [Plan review](implementation-plan-review.md): detailed assessment and cleanup scope.

## Findings to retain

| Knowledge | Durable source | Practical consequence |
| --- | --- | --- |
| Scoring and seeded policy comparisons | [Score balance](../gameplay/score-balance.md), [raw report](../../benchmarks/gameplay-balance-v2.json) | Per-bundle rewards and finite fuel make refuelling meaningful; operating points do not overwhelm them. Human balance still needs observation. |
| Startup, rendering and achieved pace | [Browser measurements](../performance/browser-phase3.md), [latency budgets](../../benchmarks/browser-latency-budgets.json) | Lazy Designer loading improves shell startup; WASM still dominates readiness. Display reduction preserves original history. Compare like-for-like timings. |
| Deterministic refactoring | [Equilibrium boundary](../physics/equilibrium-presentation-boundary.md), [response baseline](../../benchmarks/phase4-contract-baseline.json) | Preserve response semantics, units and candidate/commit behavior when reorganizing code. |
| Research isolation and type preservation | [Core/research boundary](core-research-boundary.md), [phase 5 decisions](phase5-cleanup.md) | Phase-era names are not evidence that types or fixtures are unused. |
| Supported tool invocations and pack staging | [Tool library](research-tools.md), [inventory](research-inventory.json) | Runtime uses embedded authored packs; canonical-copy equality is separate from external source validation. |
| CPU thread experiments | [CPU parallelism](../physics/cpu-parallel-experiment.md), [alternate host](../physics/cpu-main-thread-hosting.md) | Keep experiments isolated from the default worker runtime; browser hosting and end-to-end latency matter in addition to row-kernel timings. |
| GPU experiments | [GPU results](../physics/gpu-prototype-results.md), [coupled validation](../physics/gpu-coupled-validation.md), [current build setup](research-builds.md) | Read-only experiments do not replace or commit gameplay state; use current opt-in commands with the recorded comparison budgets. |
| Profiling and solver experiments | [Runtime profile](../physics/runtime-profile.md), [single-snapshot proposal](../physics/single-snapshot-step-proposal.md) | Instrumented and production AOT timings are not interchangeable; proposals are not shipped behavior. |
| Historical research | [Historical index](historical-research.md) | Preserve provenance, original artifact paths and retained test/tool inputs. |

## Temporary-file policy

Generated publish trees and isolated `bin`/`obj` trees can be regenerated with
the maintained build tools. Historical bytes may require an older source tree,
SDK and flags; a dirty-tree HEAD label alone is insufficient. The cleanup
[manifest](temporary-cleanup-manifest.json) preserves available build metadata.

Keep raw reports and captures while documents still cite them. A summary alone
does not replace per-sample benchmark data or a regression fixture. Before a
future purge, promote the useful raw records into a named `benchmarks` or
documentation evidence location and update consumers, then remove redundant
copies. Local logs, reports and screenshots retained under ignored `tmp` are
still local-only, not durable versioned evidence.

Source backups, ZIP exports and nested repositories require individual content
comparison before removal. The first cleanup deliberately retains them.
Current generated `web/candu-playtest/public/wasm` and frontend `dist` also remain
available so the browser product stays runnable.

## Cleanup validation — 2026-10-02

- Removed 47 reviewed directories containing 9,568 generated files and
  1,743,519,201 bytes (about 1.62 GiB of logical file content). Filesystem space
  recovered may differ because of allocation/compression.
- Rechecked every target against the repository `tmp` boundary, Git ignore and
  tracked-file status, file counts/sizes, nested repositories and reparse points
  before deletion. All manifest targets are now absent.
- All 50 staged browser-runtime files retained identical SHA-256 hashes across
  cleanup. Canonical and embedded diffusion packs still match.
- `tools/Test-Browser.ps1` passed: 33 Browser .NET tests, 126 frontend tests,
  and the production build. The existing deferred Designer chunk-size warning
  remains. Local log: `tmp/cleanup-browser-check.log`.
- New documentation links, cleanup manifest and edited-file whitespace checks
  passed. No numerical or gameplay implementation changed; Core/Game suites
  were not separately rerun for this documentation/generated-output cleanup.
- Local production gameplay smoke passed against port 4186: 380 channels,
  refuelling, retained Designer session, zone edits, history reset, challenge
  completion, sandbox reward exclusion, seeded retries and recovery. Browser
  console/page error arrays were empty. Logs/captures: `tmp/cleanup-smoke.log`
  and `tmp/cleanup-smoke`.
- No fresh WASM compilation, deployment, hosted-CI acceptance or human playtest
  is claimed. The local smoke used the preserved staged runtime.
