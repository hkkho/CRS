# CANDU Refuelling Game - Project Scope and Delivery Register

**Purpose:** the compact, current routing map for project work. It records what
is complete, what is blocked or deferred, and the next bounded delivery slice.
It is not a substitute for an approved specification or a task authorization.

**Authority order:** [repository rules](../AGENTS.md) govern how work is done;
the [implementation plan](Implementation_plan.md) governs frozen product,
architecture, and phase/gate intent; this register reports current state;
`docs/spec/` and accepted ADRs govern approved technical decisions; task and
gate reports preserve evidence. Templates and derived guides explain or
structure work but do not authorize it. Preserve historical evidence and stop
on a conflict between current authorities.

**Reconciled:** 2026-08-24 against the implementation plan, completed reports
through `P6-T07` implementation, `P4-T06-G4J`, and `P4-T06-G4K`, the recovery
tasks `TEST-INFRA-01`, `P6-INTEGRATION-01`, `P6-INTERNALS-01`,
`CORE-NAMING-01`, and `STATUS-VISIBILITY-01`, gates through `G5`, `G6`
(conditional synthetic-only), `G4-R6`, and `G2-R4`, the
`DOC-INSTRUCTIONS-01` documentation-authority audit, and the user-authorized
P4-T06 follow-up chain plus the completed `P4-T06-G4K` task and fresh `G4-R6` disposition
in
[`ROUND-2026-08-16-P4-REFERENCE-CHAIN.md`](tasks/ROUND-2026-08-16-P4-REFERENCE-CHAIN.md).

The fresh G4 admission-resolution task set is recorded in
[`ROUND-2026-08-16-G4-ADMISSION-CHAIN.md`](tasks/ROUND-2026-08-16-G4-ADMISSION-CHAIN.md).

The non-looping G4 block-resolution follow-up is recorded in
[`ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md`](tasks/ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md).
`P4-T06-G4F` is complete as candidate-only analytical evidence, and
`P4-T06-G4G` admitted it only for bounded three-case mathematical validation.
`G4-R3` is `BLOCKED`; `P4-T06-G4H` explicitly declined production G4 approval.
The owner-authorized `P4-T06-G4I` case is now conditionally approved by
`G4-R4` for the `Synthetic` domain with an `ApprovedGolden` payload and six
explicit profiles. The separately authorized `P4-T06-G4J` representative
reduced-model sequence case is complete as candidate-only evidence. `G4-R5`
executed its reduced-model disposition and remains a historical `BLOCKED` /
`NO-ADMISSION` record because its standalone result was implementation-level
independent evidence, not an exact oracle or admissible external authority.
`P4-T06-G4K` completed the independent authority package and fresh `G4-R6`
passed G4 for the selected `ReducedModel` runtime scope. The candidate remains
`Candidate`/`Deferred`/`NoGolden`; the sibling approved payload is scoped to
`RepresentativeReducedModel`. Direct production/external CANDU authority
remains blocked/deferred, while the approved ReducedModel authority is used by
the bounded Phase 5 task chain. `P5-T11`, `P5-T12`, `P5-T13`, `P5-T14`,
`P5-T15`, and `P5-T16` are the corrective complete-state burnup/refuelling
implementations. `G5` is `PASS` for the approved `ReducedModel` /
engine-neutral Core scope, and the Phase 6/G6 task-definition chain is now
eligible under the implementation plan and frozen RRS specification.
The activated Phase 6 task IDs and handoff criteria are recorded in
[`ROUND-2026-08-17-P6-RRS-CHAIN.md`](tasks/ROUND-2026-08-17-P6-RRS-CHAIN.md).
`P6-T01` is complete for the engine-neutral Core contract. `P6-T02` is
complete for the owner-approved synthetic/test-only map, bounded overlay, and
pure delayed/rate-limited motion projection; its original blocked authority
audit remains historical in the task report. No production CANDU, external
reference, or golden-data influence-map package has been admitted, and queue
allocation/transition/rollback plus scenario packages remain reserved for
`P6-T06`/`P6-T07`. `P6-T03` is now complete for the separately owner-approved
synthetic/test-only adjuster-bank state, grouping, map, complete supplied queue
projection, delayed motion, and local overlay/rollback evidence; its original
authority stop remains historical in the task report. `P6-T04` is now complete
for the separately owner-approved synthetic/test-only bulk-poison state,
moderator-volume/concentration derivation, positive absorption overlay,
rate-limited add/withdraw accounting, six-target/two-group map, and local
overlay/rollback evidence; its original authority stop remains historical in
the task report.

`P6-T06` is complete for the shared immutable queue/command transition layer,
including typed identity, source/phase binding, canonical ordering,
saturation, causal motion-before-consume, branch owner/queue ordering, and
atomic rollback evidence. It remains synthetic/test-only. `P6-T07` is complete
for the five bounded synthetic scenario evidence fixtures and their replay,
timestep, saturation, and refuelling-order checks; its final independent high
review is `PASS` with telemetry `UNVERIFIED`. `G6` is a `CONDITIONAL PASS` for
the synthetic/test-only Core/ReducedModel contract scope. An applicable P6 RRS
comparison package is absent, so unconditional comparison, production,
external-reference, and golden authority remain deferred to a separately
authorized G6 re-entry.

The user-authorized `TEST-INFRA-01` recovery task is complete. The shared
test-output locator and explicit test-data declarations make the standard
artifact-output wrapper portable: the direct and wrapper runs both pass Core
`156/156` and Golden `19/19`, with zero failures and zero skips. The follow-on
`P6-INTEGRATION-01` task is also complete: one Core-owned RRS projection-to-
queue admission method now derives candidates and preserves the caller-
validated source binding, with focused mismatch/atomicity characterization,
Core `158/158`, Golden `19/19`, and independent review `PASS` (telemetry
`UNVERIFIED`). `P6-INTERNALS-01` is complete as an internal-only compatibility
refactor: the P6-T05 read-only RRS queue projection and P6-T06 transition queue
remain distinct, P6-T06 is documented as the admission/transition owner, and
only the genuinely identical liquid-zone/adjuster grouping-byte writer was
shared. Focused grouping characterization passed `11/11`, T3 Core passed
`158/158`, Golden `19/19`, and independent review was `PASS` (telemetry
`UNVERIFIED`). These are synthetic/test-only contract results; no runtime
physics, schema version, tolerance, or golden authority changed. The
`CORE-NAMING-01` task is now complete as a discovery-only inventory and
decision record: it classifies all 21 public P6-T06 Core types and 34
task-ID-named test fixtures, records that no CLI or Unity source currently
consumes a P6-T06 type, and defines a no-break additive migration order. No
rename, alias, public contract, persistence, or serialized-byte change was
made. `STATUS-VISIBILITY-01` is complete, and the owner-authorized
non-physics Phase 8 activation has completed `P8-T01`: the CLI now provides a
deterministic inspection shell over the existing synthetic Core fixture. It
does not add physics, time advancement, refuelling, RRS actions, save/load,
replay, or gameplay scoring. The next Phase 8 task remains unselected until
approved scenario/difficulty parameter authority exists.

## Fast routing

Search this file for the exact phase, task, or gate key before opening related
records. For example:

```powershell
rg -n "P5-T08|PHASE-5|G5" docs/PROJECT_SCOPE.md
```

Then read the named specification and prerequisite reports. This register never
authorizes a change to equations, units, signs, convergence rules, tolerances,
golden data, public contracts, or runtime architecture.

## Project boundaries that do not drift

| Area | Required boundary |
|---|---|
| Runtime | `ReactorSim.Core` is engine-neutral and owns state transitions. `ReactorSim.Cli` is the first client, test harness, and batch runner. Unity is a thin presentation adapter and never drives physics time. |
| Numerical behavior | Use deterministic fixed-step `double` arithmetic, explicit SI units where practical, canonical channel/bundle indexing, preallocated hot-loop storage, and fail-closed diagnostics. |
| Reference pipeline | DRAGON5 and DONJON5 are offline evidence tools only. Runtime consumes only documented, versioned, checksum-validated compact data; reference tools and their source are not runtime dependencies. |
| Product limit | This is an entertainment game, not plant training, safety analysis, or a model of a named station. |
| Excluded work | No shutdown/scram, safety systems, accident progression, full thermal hydraulics/CFD, runtime OpenMC, OpenMC-generated production data, proprietary station geometry, unapproved native/mobile dependencies, signing, publishing, or releases without explicit authority. |

## Execution and evidence rules

| Rule | Current application |
|---|---|
| Implementation lane | Use GPT-5.6 Luna with high reasoning by default for a bounded task; use max only with a recorded rationale. |
| Review lane | Use `code review (high)` for architecture, physics, risk triggers, gates, golden data, performance/mobile, and release work. Historical Sol-labelled evidence remains historical and is not relabelled. |
| Gate overlap | Independent work may overlap an incomplete non-critical gate only when inputs/interfaces are frozen, the unresolved finding is irrelevant, the report records the assumption and deferred evidence, and focused checks pass. |
| Critical stop | Stop dependent work if it would invent/change an equation, coefficient meaning, unit, sign, boundary, normalization, convergence rule, tolerance, golden/reference value, public contract, or excluded behavior; or if it would hide invalid data, NaN/Inf, nonconvergence, failed replay/regression, nondeterminism, or an unresolved licensing/native/signing boundary. |
| Validation | Run T0 and the focused T1 check for every applicable task. Escalate to T3 plus `code review (high)` for numerical behavior, determinism, contracts, schemas, reference data, precision, or toolchain changes. Use T4 for Unity/serialization integration, T5 for mobile, and T6 only when the reference baseline changes. |

### `G2` status

`G2` remains exactly **`FORCED CLOSED / WAIVED`** under
[G2-FC-01](gates/G2-FC-01.md). It is administrative overlap authorization,
not a technical PASS and not permission to make a new physics or tolerance
decision.

### Current-round authorization — 2026-08-16

The project owner authorized all remaining in-plan scope for bounded execution
in a new goal round and confirmed redistribution rights for the selected
DONJON5 reference data. The owner also confirmed that exact reference data will
not be used as the game's runtime model; the intended runtime is a faster
reduced model fed by offline interpolation data.

The fresh G4-R1 rerun remains technically blocked, and the follow-up admission
chain is authorized as separate task IDs.

This authorization removes the legal uncertainty from the historical P4-T06
finding, but it does not bypass the case-admission proof, frozen Phase 2
contracts, owner gates, or the one-task-at-a-time rule. The active chains are
recorded in [the prior round task set](tasks/ROUND-2026-08-16-P4-REFERENCE-CHAIN.md)
and [the G4 admission task set](tasks/ROUND-2026-08-16-G4-ADMISSION-CHAIN.md).

Historical context is intentionally short:

- The original `G2` report failed; `G2-C01` through `G2-C07` closed the named
  corrective evidence.
- `G2-R1` and `G2-R2` remain historical incomplete reruns.
- `G2-R3` recorded a technical PASS, and the receipt-verified current-lane
  `G2-R4` technical rerun also passed with no findings.
- The active administrative disposition does not change. Phase 3+ may consume
  the frozen Phase 2 specifications, but must stop on a critical condition.

## Delivered foundation and current phase

| Key | Status | Established | Still not established |
|---|---|---|---|
| `PHASE-0` / `G0` | Complete; `G0-R1` PASS | Repository layout, pinned toolchain, Core/CLI/test boundary, Unity bootstrap, focused/full test wrappers, engine-neutral JSON boundary, and the completed `TEST-INFRA-01` artifact-portable full-suite recovery. | Cross-platform automation remains a later platform concern; it does not block current Core work. |
| `PHASE-1` / `G1` | Complete; `G1` PASS | Reproducible private DRAGON5/DONJON5 smoke workflow, parsers, schemas, manifests, and `P1-T08` six-source literature digest/crosswalk. | The compact smoke exports are not yet a solver-ready golden baseline or a published dataset. |
| `PHASE-2` / `G2` | Specifications complete; active status `FORCED CLOSED / WAIVED` | Topology/indexing/units, two-group equations, refuelling/burnup, kinetics/I-Xe/RRS/feedback contracts, and validation methodology are frozen inputs. | Numeric acceptance thresholds and golden values remain deferred to their owner gates; the waiver authorizes none. |
| `PHASE-3` / `G3` | Complete; `G3` PASS | Deterministic identity/topology/inventory, lifecycle/snapshots/diagnostics, clock/queue, archive/replay, atomic application, and invalid-data rejection. The final G3 review is receipt-verified `gpt-5.6-terra` / high. | No solver, refuelling, depletion, kinetics, gameplay, or Unity behavior was introduced by this phase. |
| `PHASE-4` / `G4` | In progress; `P4-T05`, `P4-T08`, `P4-T06-R1`, `P4-T06-R2A`, `P4-T06-R2D`, `P4-T06-R3`, `P4-T06-R4`, `P4-T06-R5`, `P4-T06-G4A`, `P4-T06-G4B`, `P4-T06-G4C`, `P4-T06-G4D`, `P4-T06-G4E`, `P4-T06-G4F`, `P4-T06-G4G`, `P4-T06-G4H`, `P4-T06-G4I`, `G4-R4`, `P4-T06-G4J`, `G4-R5`, `P4-T06-G4K`, and `G4-R6` complete; `P4-T06-R2B`/`R2C` remain historical direct-admission audits; original `G4`, fresh `G4-R1`, `G4-R2`, `G4-R3`, and `G4-R5` remain historical `BLOCKED`; G4-R4 is a conditional synthetic-only pass; G4-R6 is `PASS` for `ReducedModel`; final review telemetry is receipt-verified for the bounded disposition; direct production/external authority remains blocked/deferred | `P4-T01` stencil assembly, `P4-T02` coefficient binding/operator, `P4-T03` one-step source/eigen iteration with normalization, `P4-T04` caller-policy outer convergence/invalid-state diagnostics, `P4-T05` synthetic solved-case coverage, `P4-T08` machine-specific benchmark baseline, the rights/artifact-boundary decision, the R2A applicability decision, the successful hash-bound Candu6.x2m source probe, the owner-selected separate synthetic/reduced-model boundary, the frozen R3 interpolation boundary, the deterministic R4 candidate pack/manifest, R5 candidate comparison/snapshot evidence, the G4A path-free scope/admission decision, the G4B test-only consumer evidence, the G4C authority and exact case/normalization mapping decision, the G4D independent path-free reproduction artifact/manifest, the G4E test-only consumers, the G4-R2 gate rerun, the fresh G4-R1 audit, the G4F analytical candidate, the G4G bounded mathematical-admission decision, the G4-R3 blocked disposition, the G4H explicit non-approval decision, the G4I manufactured synthetic authority candidate, the G4-R4 synthetic-only disposition, the G4J representative reduced-model candidate, the G4-R5 reduced-model no-admission disposition, the G4K independent authority package, and fresh G4-R6 approval are complete. Direct Candu6.x2m admission into P2-T02 is explicitly not admitted. | G4 is clear for the selected ReducedModel runtime domain; direct representative external authority, full production coverage, and incomplete I/Xe/production evidence remain outside this bounded approval. |
 | `PHASE-5` / `G5` | Complete; `P5-T01` through `P5-T16` implementation evidence complete; G5 `PASS` | `P5-T01` declarative schemes, `P5-T02` immutable location-layer movement, `P5-T03` named mapping/atomicity bodies with basic discharge audit events, `P5-T04` bounded explicit-time energy/derived-burnup integration, `P5-T05` bounded burnup-indexed coefficient-table validation/lookup, `P5-T06` bounded cadence-driven coefficient/spatial-state recomputation, `P5-T07` bounded identity/location/burnup/energy invariant checks, `P5-T08` test-only deterministic multistep S4/S8 history evidence, `P5-T09` complete I/Xe/lifecycle/discharge/power-history/transaction contracts, `P5-T10` structural ReducedModel sequence mapping, `P5-T11` complete-state burnup/lookup/invalidation plus strict post-shift refuelling evidence, `P5-T12` source-digest authentication plus no-table refuelling fail-closed evidence, `P5-T13` full accepted power-history authentication, `P5-T14` lifecycle-authenticated snapshot digest binding, `P5-T15` persistence-safe lifecycle digest/archive round-trip, and `P5-T16` restore-time digest reconciliation/restart reconstruction are complete under independent gate overlap. | `G5` is `PASS` for the approved ReducedModel/engine-neutral Core scope; direct production/external CANDU authority remains deferred and outside this scope. |
| `PHASE-6` / `G6` | `CONDITIONAL PASS` for the approved synthetic/test-only Core/ReducedModel scope; `P6-T01` through `P6-T07` complete. | `P6-T01` exact 14-logical/6-physical grouping and state contract, `P6-T02` owner-approved synthetic liquid-zone map/overlay/motion, `P6-T03` owner-approved synthetic adjuster-bank state/grouping/map/queue-projection/overlay/motion, `P6-T04` owner-approved synthetic bulk-poison state/volume/concentration/map/accounting/overlay evidence, `P6-T05` owner-approved synthetic RRS controller/map state, deterministic projections, local overlay, and complete read-only queue binding, `P6-T06` shared immutable queue/command transition, ordering, saturation, causal motion, source/phase binding, and rollback evidence, and `P6-T07` five bounded synthetic scenarios with replay, timestep, saturation, poison-accounting, and refuelling-order evidence are implemented in engine-neutral Core/test fixtures. The original G6 review evidence included aggregate P6 focused `44/44` and T3 Core `154/154`; the current recovery verification is P6 focused `46/46`, T3 Core `158/158`, Golden `19/19`, all with zero failures/skips. | No applicable P6 RRS comparison package is admitted; G4-R6 is non-equivalent and marks liquid-zone/adjuster runtime `NotCovered`. The P6-T02 through P6-T07 evidence remains synthetic/test-only and is not production, external-reference, or golden authority; G6 re-entry is required for an unconditional comparison disposition. |

### Historical record keys

- `PHASE-0`: `P0-T01`, `P0-T02`, `P0-T03`, `P0-T04`, `P0-T05`, `P0-T06`,
  `P0-T07`, `P0-T08`, `G0`, `G0-R1`. `P0-T01` predates the current template,
  but its outcome explicitly records completion; its report is present.
- `PHASE-1`: `P1-T01`, `P1-T02`, `P1-T03`, `P1-T04`, `P1-T05`, `P1-T06`,
  `P1-T07`, `P1-T08`, `G1`.
- `PHASE-2`: `P2-T01`, `P2-T02`, `P2-T03`, `P2-T04`, `P2-T05`, `G2`,
  `G2-C01`, `G2-C02`, `G2-C03`, `G2-C04`, `G2-C05`, `G2-C06`, `G2-C07`,
  `G2-R1`, `G2-R2`, `G2-R3`, `G2-R4`, `G2-FC-01`.
- `PHASE-3`: `P3-T01`, `P3-T02`, `P3-T03`, `P3-T04`, `P3-T05`, `G3`.
- `PHASE-4`: `P4-T01`, `P4-T02`, `P4-T03`, `P4-T04`, `P4-T05`, historical `P4-T06` (blocked), `P4-T06-R1` (complete), `P4-T06-R2` (blocked), `P4-T06-R2A` (complete), `P4-T06-R2B` (blocked partial audit), `P4-T06-R2C` (blocked authority audit), `P4-T06-R2D`, `P4-T06-R3`, `P4-T06-R4`, `P4-T06-R5`, `P4-T06-G4A` (complete), original `G4` (blocked), `P4-T06-G4B` (complete), `G4-R1` (blocked), `P4-T06-G4C`, `P4-T06-G4D`, `P4-T06-G4E`, `G4-R2` (complete; blocked), `P4-T06-G4F` (complete; candidate-only), `P4-T06-G4G` (complete; bounded mathematical admission), `G4-R3` (blocked), `P4-T06-G4H` (complete; production approval explicitly declined), `P4-T06-G4I` (complete; synthetic candidate), `G4-R4` (complete; conditional synthetic-only pass), `P4-T06-G4J` (complete; representative reduced-model candidate), `G4-R5` (complete; reduced-model no-admission / blocked), `P4-T06-G4K` (complete; technical PASS/candidate authority), `G4-R6` (complete; PASS for selected ReducedModel), `P4-T08`.
 - `PHASE-5`: `P5-T01`, `P5-T02`, `P5-T03`, `P5-T04`, `P5-T05`, `P5-T06`, `P5-T07`, `P5-T08`, `P5-T09`, `P5-T10`, `P5-T11`, `P5-T12`, `P5-T13`, `P5-T14`, `P5-T15`, `P5-T16`, `G5` (authorized chain; complete, `PASS`).
  - `PHASE-6`: `P6-T01` through `P6-T07` and `G6` are activated as task-definition routing IDs in [`ROUND-2026-08-17-P6-RRS-CHAIN.md`](tasks/ROUND-2026-08-17-P6-RRS-CHAIN.md); `P6-T01` through `P6-T07` are complete within their approved synthetic boundaries and `G6` is `CONDITIONAL PASS` for that scope, with the original authority stops preserved historically in their reports.
- Policy history: `PLAN-C01`, `PLAN-C02`, `PLAN-C03`, and `PLAN-C04` remain
  complete documentation/policy records.

## Recovery sequence — 2026-08-24

| Key | Status | Boundary and evidence | Next handoff |
|---|---|---|---|
| `TEST-INFRA-01` | `COMPLETE` | Test-only output-root resolution and explicit Core fixture declarations; direct Core `156/156`, Golden `19/19`, and fresh artifact-output wrapper Core `156/156`, Golden `19/19`, all with zero failures/skips. | Baseline is portable; no further work remains in this task. |
| `P6-INTEGRATION-01` | `COMPLETE` | Core-owned RRS projection-to-queue admission preserves v1 command/queue bytes, source binding, rank 2, delays/rates, and immutable enqueue behavior; focused `10/10`, T3 Core `158/158`, Golden `19/19`, zero failures/skips, independent review `PASS` with telemetry `UNVERIFIED`. | Follow-on `P6-INTERNALS-01` is complete; `CORE-NAMING-01` is next. |
| `P6-INTERNALS-01` | `COMPLETE` | Internal compatibility inventory preserved the distinct public RRS projection and P6-T06 transition queue; shared only the identical grouping canonical-byte body. Focused grouping `11/11`, T3 Core `158/158`, Golden `19/19`, zero failures/skips, independent review `PASS` with telemetry `UNVERIFIED`. | `CORE-NAMING-01` is next; no public rename is authorized in that discovery task. |
| `CORE-NAMING-01` | `COMPLETE` | Discovery-only inventory found 21 public P6-T06 Core types and 34 public task-ID-named test fixtures; classified serialized vs transient roles, found no CLI/Unity typed P6-T06 consumer, and defined a no-break migration order. Focused Core build passed with 0 warnings and 0 errors. | `STATUS-VISIBILITY-01` is next; any implementation rename requires a separate risk-triggered task. |
| `STATUS-VISIBILITY-01` | `COMPLETE` | Derived guide v2.8, generated 22-page PDF, private-site home/roadmap, and clean-session handoff now show direct Core `158/158`, Golden `19/19`, focused P6 `46/46`, and the artifact-wrapper recovery baseline Core `156/156` plus Golden `19/19`; the guide/site retain the synthetic/test-only G6 limitation and external/production deferrals. Local consistency and private-site route tests pass. | Owner-authorized non-physics Phase 8 activation selected `P8-T01`. |

## Phase 8 non-physics CLI sequence — 2026-08-24

| Key | Status | Boundary and evidence | Next handoff |
|---|---|---|---|
| `P8-T01` | `COMPLETE` | The first Phase 8 CLI slice provides `help`, `new run`, `inspect core`, `inspect channel`, `inspect bundle`, `pause`, and `quit` over the explicit synthetic Core fixture. Output uses canonical LF line endings, invariant numeric formatting, separate stdout/stderr, deterministic errors, and exit code `2` for malformed commands. Focused CLI tests `5/5`, full T3 Core `158/158`, Golden `19/19`, CLI `5/5`, and release build `0` warnings/errors passed; independent review final `PASS` with telemetry `UNVERIFIED`. | No next Phase 8 task is selected until an approved scenario/difficulty parameter authority is available; the remaining Phase 8 work packages and G8 evidence are not implied by this slice. |

The refactoring proposal remains a sequencing aid rather than a technical
authority. The current user authorization selects one task at a time; it does
not authorize new equations, units, tolerances, golden values, public schema
versions, or production/external RRS claims.

## `PHASE-4` - immediate delivery plan

### Completed slices

| Task | Completed boundary | Evidence status |
|---|---|---|
| `P4-T01` | Deterministic topology-driven node, neighbor, and boundary stencil assembly. | T1, T3, and receipt-verified independent review PASS. |
| `P4-T02` | Bound coefficients/conductances and matrix-free removal-plus-leakage application. | T1 and corrected T3 passed; receipt-verified final independent review PASS. |
| `P4-T03` | One immutable source/eigen iteration, deterministic inner Jacobi solve, prescribed source ordering, eigenvalue update, and power normalization. | T1 and final T3 passed: 53/53 Core tests, 0 Golden tests. Final reviewer conclusion was PASS, but actual reviewer model/receipt is `UNVERIFIED`; do not present it as receipt-verified approval. |
| `P4-T04` | Caller-supplied outer convergence policy, repeated source/eigen steps, zero-safe residual diagnostics, source-shape/power checks, deterministic failure reasons, and fail-closed invalid-state results with no usable failed state. | T1 8/8, final T3 61/61 Core tests, 0 Golden tests; final code-review (high) PASS with no findings, actual model/receipt `UNVERIFIED`. |
| `P4-T05` | Frozen synthetic one-node algebraic/no-leakage contract, symmetric and homogeneous runtime cases, deliberate nonconvergence, and canonical input-order determinism. | T1 5/5, final T3 66/66 Core tests, 0 Golden tests; final code-review (high) PASS with no findings, actual model/receipt `UNVERIFIED`. |
| `P4-T08` | Dependency-free Release benchmark for the P4-T05 synthetic three-node solve with manifest validation, exact repeat determinism, elapsed-time, and allocation observations. | Release benchmark/solution builds passed; two 200-solve runs had exact semantic repeats; final T3 66/66 Core tests, 0 Golden tests; final code-review (high) PASS with no findings, actual model/receipt `UNVERIFIED`. |

### `P4-T04` - outer convergence and invalid-state diagnostics

**State:** COMPLETE. P4-T04 added the bounded outer solve and diagnostics
wrapper around the existing P4-T03 one-step iteration. No reference baseline,
golden comparison, or G4 tolerance approval was added.

**Inputs:**

- `P4-T01` through `P4-T03` and their current Core contracts;
- [two-group solver, normalization, and convergence v1](spec/two-group-solver-normalization-convergence-v1.md);
- [observables and validation methodology v1](spec/observables-validation-methodology-v1.md);
- the applicable `P1-T08` digest rows, recorded in the eventual task report;
- the `G2` overlap rule above.

**Implemented result:** wrapped the existing one-step solver in the approved,
deterministic outer solve contract without selecting new defaults or changing
the P2-T02 equations. The task must:

1. Validate a complete caller-supplied `ConvergencePolicy`, including positive,
   finite policy values and a positive outer iteration limit.
2. Record, in canonical order, outer iteration count, eigenvalue change
   (absolute/relative), zero-safe global residuals, source-shape change,
   power-balance error, inner-solve status, policy values, and a deterministic
   convergence/failure reason.
3. Accept convergence only when every P2-T02 condition is met; the two
   eigenvalue criteria remain an OR pair and the remaining criteria remain
   required.
4. Fail closed on invalid/non-finite data, negative flux, failed inner solve,
   invalid power/production, counter overflow, or maximum-iteration exhaustion.
   A nonconverged result must expose diagnostics but no usable converged state.
5. Keep `double` arithmetic, fixed/canonical ordering, engine neutrality, and
   immutable externally observable results.

**Explicit non-goals:** no tolerance choice or relaxation, reference/golden
comparison, reference-data regeneration, refuelling/depletion/kinetics/RRS,
Unity work, parallel backend, precision change, or G4 approval.

**Evidence completed:** T0 build/format and scope inspection; focused T1
convergence, nonconvergence, invalid-policy, invalid-number, and deterministic
ordering tests; T3 plus one bounded `code review (high)` because this changes
convergence and public numerical diagnostics. T4, T5, and T6 were not implied
because no adapter, platform, or reference baseline changed. The report
preserves that G2 is
`FORCED CLOSED / WAIVED` and that numeric acceptance thresholds remain deferred.

### `P4-T05` - synthetic solved-case coverage

**State:** COMPLETE. P4-T05 added the approved synthetic coverage for the
static solver without changing runtime equations, topology invariants,
convergence policy fields, reference data, or golden values.

The one-node/no-leakage example is covered as the algebraic contract check
defined by P2-T02. The runtime topology contract from P2-T01 requires at least
two bundle positions, so runtime fixtures use the smallest valid two-position
topology; no topology minimum was weakened to manufacture a one-node runtime
case.

**Evidence:** five focused tests cover the frozen one-node algebraic example,
equal-flux reciprocal symmetry, homogeneous three-node shape preservation,
deliberate nonconvergence with no usable state, and equality of results after
reordering coefficient records. The final T3 run passed 66/66 Core tests with
zero discovered Golden tests. The final code-review (high) disposition was
PASS with no findings; actual model/receipt telemetry is `UNVERIFIED`.

**Explicit non-goals:** no DONJON5 comparison, reference/golden admission,
representative snapshot, benchmark, tolerance approval, Unity work, or later
phase behavior.

### `P4-T06` - minimal DONJON5 comparison and case admission

**State:** BLOCKED. The pinned P1-T03 descriptor and runner are internally
identifiable, but the selected prepared HDF5 group constants have no standalone
redistribution license and carry CEA/AREVA copyright/export metadata. The
repository reference policy classifies the upstream package as
`NOASSERTION / mixed notices` and forbids committing, mirroring, publishing, or
redistributing the selected inputs or derived outputs without a separate legal
and export-control determination.

The private smoke case also does not provide the solver coefficients,
repository topology/geometry mapping, units, flux/power normalization, or
output-field mapping required for a Core comparison. P4-T06 stopped before
executing or importing a result. See [the full admission audit](tasks/P4-T06.md)
for the exact metadata and runner checks.

**Overlap decision:** the unresolved finding is external reference admission,
not the Core benchmark path. An independently assigned benchmark task may
proceed under the gate-overlap policy; reference snapshots and G4 remain
dependent on P4-T06 admission evidence.

### Authorized `P4-T06-R1` through `P4-T06-R5` follow-up

The historical P4-T06 report remains `BLOCKED` evidence and is not rewritten.
The user-authorized follow-up chain now separates the resolved rights decision,
solver-ready case mapping, reduced offline-model boundary, compact
interpolation-data generation, and Core comparison/snapshot evidence. The
complete task definitions, including the R2A applicability decision, the R2B
mapping audit, the R2C capability audit, and the new R2D decision task,
dependencies, stop conditions, and handoff order are in [the
round task set](tasks/ROUND-2026-08-16-P4-REFERENCE-CHAIN.md).

`P4-T06-R1` and `P4-T06-R2A` are complete. `P4-T06-R2` remains historical
blocked evidence. R2B established a successful hash-bound Candu6.x2m source
probe with the exact production counts and candidate bundle/channel power outputs, but it
did not prove the CANDU node map, preassembled `T_g`/`B_g` conductances, SI
node volumes, per-fission energy, or P2-bound output normalization. R2C
confirmed the source capability and preserved those gaps as historical direct-
admission blockers. `P4-T06-R2D` is complete: the owner selected a separate
synthetic/reduced-model boundary and retired direct Candu6.x2m admission into
P2-T02 without changing the frozen P2 contracts. `P4-T06-R3` and `P4-T06-R4`
are complete; R4 produced the synthetic-only candidate pack and manifest with
deterministic validation and no runtime reference dependency. `P4-T06-R5` is
complete: it produced 3 exact candidate snapshots, preserved 1 nonconverged
case and 2 not-covered cases, and emitted 57 deferred typed records. R5 did
not approve tolerances or golden values. The separate original `G4` execution
is recorded as `BLOCKED` in [the historical gate report](gates/G4.md), and the
fresh `G4-R1` rerun is recorded as `BLOCKED` in
[the immutable rerun report](gates/G4-R1.md): the candidate remains synthetic
and deferred, with no independent approved solver baseline or numeric
tolerance authority. `P4-T06-G4A` is complete: it admitted the three
deterministic static synthetic cases as a bounded comparison basis and
explicitly deferred the nonconverged refuelled and not-covered RRS/poison
slots. `P4-T06-G4B` is now complete: it added four test-only candidate
consumers, bound the existing artifact/profile identities, and preserved
candidate/deferred evidence. `P4-T06-G4C` is now complete: it records
the frozen authority package and exact three-case/normalization mapping for the
independent reproduction. `P4-T06-G4D` is now complete: it produced the
standalone, hash-bound, candidate-only reproduction artifact and manifest for
the three admitted static cases, with deterministic repeat and fail-closed
input checks. `P4-T06-G4E` is now complete: its test-only consumers bind the
G4D artifact/manifest, exact case/profile identities, frozen input hashes,
units, normalization fields, typed P2-T05 scopes/state/order bindings, and the
Deferred/Candidate boundary. `G4-R2` is now complete as a technical `BLOCKED`
rerun: direct validation and T3 pass, but no approved numerical baseline or
tolerance authority exists. `P4-T06-G4F` then produced candidate-only
mathematical evidence, and `P4-T06-G4G` admitted it only for the three-case
mathematical-validation role while preserving all tolerance and missing-coverage
deferrals. `G4-R3` is now complete as a `BLOCKED` gate; no prior G4 task is restarted in
the [G4 block-resolution task set](tasks/ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md);
each task remains a separate execution. The `P4-T06-G4H` decision is complete:
no production-admissible authority was found and production G4 approval was
explicitly declined. The owner-authorized `P4-T06-G4I` case then completed as a
synthetic candidate, and `G4-R4` conditionally passed it for the bounded
`Synthetic` domain with an `ApprovedGolden` payload and six profiles. Independent
Phase 5 work remains eligible under gate overlap, and `P5-T10` is now eligible
for the separately approved ReducedModel authority from `G4-R6`.

`P4-T06-R1` is complete: the project-owner rights determination and
offline/reduced-runtime boundary are recorded in
[the task report](tasks/P4-T06-R1.md). The historical P4-T06 report remains
unchanged.

`P4-T06-R2D` is complete: the project owner selected the separate
synthetic/reduced-model boundary. Direct Candu6.x2m source fields are not
admitted as P2-T02 coefficients, node identity, volumes, per-fission energy,
normalization, or golden values. R3 froze the reduced-model,
interpolation-data, provenance, valid-domain, and runtime-separation boundary
without inventing those source mappings. R4 then produced the synthetic-only
candidate pack and manifest with deterministic validation. See [the R2D decision report](tasks/P4-T06-R2D.md),
[the R3 task/specification](tasks/P4-T06-R3.md), and [the R4 task report](tasks/P4-T06-R4.md).

### `P4-T06-R3` - reduced offline-model and interpolation boundary

**State:** COMPLETE. R3 freezes the semantic boundary selected by R2D. The
existing `BurnupCoefficientTableV1` remains the only runtime-facing material
interpolation contract. Explicit topology, node volume, conductance,
normalization, lifecycle, provenance, and gate ownership remain separate. R3
does not add runtime code, a new serialization schema, a reduced-model
equation, a unit conversion, a tolerance, or golden data.

The required T3 suite passed Core 103/103 with zero failures; the Golden
assembly still has zero discovered tests. The [task report](tasks/P4-T06-R3.md)
and [boundary specification](spec/reduced-model-interpolation-boundary-v1.md)
route the next task to R4.

### `P4-T06-R4` - offline candidate interpolation pack and validator

**State:** COMPLETE. R4 adds an offline-only converter and validator that
consumes the admitted synthetic source fixture and projects it through the
existing `BurnupCoefficientTableV1` contract. It emits a versioned candidate
pack and manifest with path-free provenance, explicit artifact/table identity,
deterministic checksums, source/pack digests, and strict finite/value/schema
validation.

The source and output require `evidence_class: synthetic`; R4 does not admit
external/reference evidence classes, exact DONJON5 data, or a runtime
reference dependency. The candidate includes no topology, volume, conductance,
direction, normalization, tolerance, or golden-data fields. It does not change
Core, runtime schemas, equations, units, tolerances, reference baselines, or
gate ownership. The focused R4 suite passed and the required T3 headless suite
passed Core 103/103 with zero failures; the Golden assembly still has zero
discoverable tests. See [the task report](tasks/P4-T06-R4.md).

**Next eligible task:** `P4-T06-R5` - compare the Core solver and reduced data
path against the approved candidate/reference boundary and prepare
representative snapshots, without selecting G4 tolerances.

### `P4-T06-R5` - Core comparison and representative candidate snapshots

**State:** COMPLETE. R5 validates the R4 source/pack/manifest bindings,
executes the existing Core spatial solve over fresh, equilibrium-like,
refuelled-perturbation, midcycle, RRS, and poison case slots, and emits
deterministic typed P2-T05 comparison records and repeat evidence. The three
homogeneous cases are exact bitwise candidate snapshots. The refuelled case
is preserved as `Nonconverged` under the inherited two-iteration policy, and
the RRS and poison cases remain `NotCovered` because their approved inputs are
absent. All 57 records are `Deferred`; no tolerance or golden value was
selected.

The tool also closes the remaining R5 admission-contract gaps: opaque IDs use
the P2 `Bytes` encoding, vector values carry the complete approved ordering
profile, the benchmark fixture values are validated rather than presence-
checked, and source/pack/manifest provenance is path-free. The focused R5
checks, T3 Core 103/103 suite, and same-context code review (high) passed; the
Golden assembly still has zero discoverable tests. See the [R5 task report](tasks/P4-T06-R5.md)
and [candidate snapshot evidence](../data/comparisons/p4-t06-r5-candidate-snapshots-v1.json).

**Historical next task:** `G4` - execute the spatial validation gate. That gate
was run separately and is recorded below as `BLOCKED`; the fresh rerun and
current follow-up are recorded in the G4 section.

### `G4` - spatial validation and tolerance gate

**State:** BLOCKED. The original gate and fresh `G4-R1` rerun found no
admissible basis for a technical PASS. The R5 output is synthetic candidate
evidence only: it contains 3 exact homogeneous `CandidateSnapshot` cases, 1
`Nonconverged` refuelled case, and 2 `NotCovered` RRS/poison cases. All 57
comparison records remain `Deferred` and `Candidate`; no record is approved.
`data/golden` contains no committed golden payload. G4B supplies four
test-only candidate consumers, and the fresh rerun passed those consumers 4/4
plus the Core suite 103/103; this execution evidence does not approve a
baseline, threshold, or golden record.

The exact R5 output hash is
`13481d139c2b2f2fd1c043725c0a2ced9f396c2813a954cc604a1266bc4c8211`. The
three exact self-consistency results are diagnostic candidate evidence, not an
external solver baseline and not tolerance approval. The original gate and the
fresh `G4-R1` rerun therefore did not relabel synthetic data, invent a
threshold, or hide the nonconvergence and coverage gaps. The historical
disposition is in the [G4 gate report](gates/G4.md), and the fresh disposition
is in the [G4-R1 rerun report](gates/G4-R1.md).

**Corrective task chain:**

1. `P4-T06-G4A` is complete. It admits only the three deterministic static
   synthetic cases as a bounded comparison basis and explicitly dispositions
   the nonconverged refuelled and not-covered RRS/poison cases. It changed no
   equation, unit, convergence policy, tolerance, golden value, or public
   contract. See the [G4A report](tasks/P4-T06-G4A.md).
2. `P4-T06-G4B` is complete. Its four test-only consumers admit only the
   G4A-bounded static cases, bind the existing artifact/profile identities,
   and leave all numeric thresholds and golden approval deferred.
3. `G4-R1` is complete as a fresh `BLOCKED` rerun. Its two retained findings
   are the absence of an independent approved baseline/tolerance authority and
   incomplete refuelled/RRS/poison coverage.
4. The authorized [G4 admission task set](tasks/ROUND-2026-08-16-G4-ADMISSION-CHAIN.md)
   ran `P4-T06-G4C`, `P4-T06-G4D`, and `P4-T06-G4E` sequentially. `G4-R2`
   then reran the gate and remains `BLOCKED`; it made no tolerance or golden
   approval.
5. The non-looping [G4 block-resolution follow-up](tasks/ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md)
   ran `P4-T06-G4F` against the frozen inputs already admitted by G4C. The
   standalone analytical artifact is candidate-only and records raw
   differences against G4D; its artifact hash is
   `93041fc517029fabdc18e02e48794d1b6e02b4a9d7e2d16b5b637129dd4ae584`.
   No prior task was restarted and no threshold, golden value, or public
   schema was added.
6. `P4-T06-G4G` is complete: it admits G4F only as a bounded mathematical-
   validation input for the three homogeneous static cases, keeps all 57 G4D
   records `Deferred`/`Candidate`, and explicitly defers refuelled/RRS/poison
   coverage. It approves no tolerance or golden value.
 7. `G4-R3` completed the fresh gate execution and remains `BLOCKED`: it
    consumed the explicit G4G decision without restarting any completed
    admission task.

The owner’s redistribution-rights and reduced-runtime confirmation authorizes
this bounded chain and the remaining in-plan work; it does not itself create a
solver baseline or bypass the gate. `P4-T06-G4H` completed the authority audit
and explicitly declined production G4 approval because no admissible authority
was available. `G4-R6` subsequently cleared the selected ReducedModel scope;
`P5-T10` may consume that approved scope, while direct production/external
comparisons remain deferred. Independent Phase 5 work may continue only where
its inputs are frozen and it does not consume unresolved production evidence.

### `P4-T08` - synthetic Core benchmark baseline

**State:** COMPLETE. P4-T08 added a package-free Release benchmark for the
current Core spatial solve. It validates the committed scenario manifest,
requires a converged usable state, and checks exact semantic repeatability
while reporting machine-specific timing and allocation observations.

The benchmark changes no Core behavior, performance target, tolerance, golden
value, reference input, native dependency, or mobile budget. Two 200-solve runs
reported approximately 506.665 and 505.7995 microseconds per solve and 856
allocated bytes per solve; these are observations only. See
[the task report](tasks/P4-T08.md) for the exact invocation and T3 evidence.

**Overlap decision:** P4-T08 is independent of the historical P4-T06
reference-admission finding. The new P4-T06-R1–R5 chain is the authorized
dependent path to reference snapshots and G4.

## `PHASE-5` - eligible overlap delivery

### `P5-T01` - declarative S4/S8 scheme metadata and position plans

**State:** COMPLETE. P5-T01 adds an engine-neutral, immutable Core contract for
the approved v1 declarative 4-bundle and 8-bundle scheme definitions. It
validates scheme identity, schema version, fresh-template identity, exact
4/8-count support, ascending inserted-slot order, catalog coverage of both
counts, and the P2-T01-bounded position-plan mapping for both flow directions.

The contract binds `EndAtoEndB` to `TowardEndB` and `EndBtoEndA` to
`TowardEndA`, and reports inserted/discharged boundary positions without
mutating `BundleInventory`. It does not resolve fuel templates, move bundle
identities, discharge state, integrate burnup, look up coefficients, create
events, or establish reference/golden evidence.

**Evidence:** focused P5-T01 tests passed 5/5; the final T3 headless run passed
71/71 Core tests with zero discovered Golden tests; format/build checks passed;
and the bounded replacement code-review (high) disposition was PASS with no
findings. The prior Feynman reviewer context was unavailable in the current
execution service, so the replacement reviewer was used and reused for the
correction/final disposition; actual receipt telemetry is `UNVERIFIED`.

**Overlap decision:** P5-T01 consumes only frozen P2-T03/P2-T01 contracts and
does not depend on the blocked P4-T06 DONJON5 admission. Atomic movement,
burnup, coefficient interpolation, reference histories, and G5 remain
separate task/gate evidence.

**Next eligible task:** `P5-T02` - atomic directional shift and persistent
bundle identity movement using these validated position plans. Keep fresh
template resolution, burnup, coefficient lookup, reference histories, and G5
evidence in separate bounded tasks.

### `P5-T02` - atomic directional shift and persistent bundle identity movement

**State:** COMPLETE. P5-T02 adds an engine-neutral immutable transition over
the current `BundleInventory` fields. It requires a fully occupied target
channel and a flow-bound P5-T01 position plan, validates explicit ordered fresh
bundle states and effective time, shifts retained bundle identities in the
approved TowardEndA/TowardEndB mappings, and returns the discharged states in
ascending old-position order. The original inventory is never mutated; a
failed precondition or postcondition returns no replacement result.

The bounded transition preserves stable identity, material key, initial burnup,
cumulative energy, heavy-metal mass, and insertion time for retained bundles.
Fresh entries require explicit identity/material/basic state, exact effective
time, and zero cumulative energy. Complete I/Xe/lifecycle envelopes,
fresh-template resolution, command/event/discharge persistence, burnup,
coefficient lookup, and history digests remain later Phase 5 work because they
are not represented by the current `BundleInventory` contract.

**Evidence:** focused P5-T02 tests passed 5/5; the pinned Release Core build
passed with zero warnings/errors; the bounded same-context code-review (high)
disposition was PASS with no findings. Actual review receipt/model telemetry is
`UNVERIFIED`.

**Overlap decision:** P5-T02 consumes the frozen P2-T01/P2-T03 position and
bundle contracts and does not depend on the blocked P4-T06 DONJON5 admission.
It creates no reference or golden evidence and leaves the complete transition
envelope for the next task.

**Next eligible task:** `P5-T03` - complete fresh insertion/discharge state and
auditable refuelling event records, subject to the approved full state/event
contracts.

### `P5-T03` - named refuelling events and basic discharge audit records

**State:** COMPLETE. P5-T03 adds the approved immutable
`RefuelMappingBodyV1` and `RefuelAtomicityBodyV1` event payloads, explicit
applicability wrappers, and `PositionBindingV1` validation. Public mapping
bodies reject duplicate, gapped, reordered, or cross-channel old/new
position domains; event creation rejects mapping-time and atomicity-status
mismatches with the outer event envelope.

`RefuelAuditTransitionV1` derives the canonical moved/inserted/discharged
mapping from a P5-T02 shift, creates paired committed refuelling events, and
appends them to an immutable `EventLogV1` only after both records validate.
It also returns deterministic `RefuelDischargeRecordV1` evidence over the
fields currently represented by `BundleState`. The record is explicitly not
the complete P2-T03 I/Xe-bearing `DischargeRecordV1`; complete templates,
nuclide/power/coefficient/history fields, transaction digests, and persistent
command-applied state remain later Phase 5 work.

**Evidence:** focused P5-T03 tests passed 3/3; the pinned Release Core build
passed with zero warnings/errors; final T3 passed 79/79 Core tests with zero
Golden tests; T4 Unity import/compile smoke passed; and the bounded same-context
code-review (high) disposition was PASS with no findings. Actual review
receipt/model telemetry is `UNVERIFIED`.

**Overlap decision:** P5-T03 consumes the frozen P2-T05 event schemas and
P5-T02 immutable shift result and does not depend on the blocked P4-T06
DONJON5 admission. It supplies no reference/golden evidence and does not
invent the missing full I/Xe transaction projection.

**Next eligible task:** `P5-T04` - integrate local bundle power over explicit
time intervals into cumulative energy and derived burnup.

### `P5-T04` - explicit-time bundle power integration and derived burnup

**State:** COMPLETE. P5-T04 adds immutable accepted bundle-power samples and
an accepted-power projection bound to snapshot identity, exact snapshot time,
core-state version, and an opaque source-state digest. The transition requires
one finite, nonnegative power for every live bundle at the matching explicit
node, evaluates bundles in canonical `(ChannelId, BundlePosition, BundleId)`
order, applies the approved left-endpoint `Delta_E = P * Delta_t` rule, and
derives burnup from initial burnup plus cumulative energy over heavy-metal
mass.

The transition computes every proposed value before constructing a replacement
inventory, validates finite/nonnegative/monotone energy and burnup, rejects
overflow and positive increments lost to floating-point rounding, advances the
proposed core-state version once, and marks the consumed power snapshot
invalid. The source inventory remains unchanged on failure. `BundleInventory`
now rejects a non-finite derived burnup at construction time.

This is deliberately bounded to the fields currently represented by
`BundleInventory`. It does not claim the complete P2-T03 `PowerSnapshot`
history/digest envelope, solver/coefficient/topology/data-pack bindings beyond
the supplied source-state digest, coefficient-table lookup/interpolation,
event-time scheduler ownership, or the single-owner lifecycle commit.

**Evidence:** focused P5-T04 tests passed 3/3; the pinned Release Core build
passed with zero warnings/errors; final T3 passed 82/82 Core tests with zero
Golden tests; pinned format and living-guide consistency checks passed; and
the bounded same-context code-review (high) disposition was PASS with no
findings. Actual review receipt/model telemetry is `UNVERIFIED`.

**Literature applicability:** the required P1-T08 burnup/refuelling rows
`S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and `S6-R05` were reviewed as
background/candidate-design evidence only. The approved P2-T03 specification
remains the authority for the equation, units, explicit-time rule, and
transaction boundary; no literature number or golden value was admitted.

**Overlap decision:** P5-T04 consumes the frozen P2-T03 burnup contract and
the current immutable inventory/P5-T03 boundary and does not depend on the
blocked P4-T06 DONJON5 admission. It supplies no reference/golden evidence;
coefficient lookup/interpolation, complete lifecycle/history binding, and G5
remain separate bounded work.

**Next eligible task:** `P5-T05` - implement the approved burnup-indexed
coefficient-table contract, interpolation, and fail-closed out-of-range
behavior without changing the frozen burnup equation.

### `P5-T05` - burnup-indexed coefficient-table validation and lookup

**State:** COMPLETE. P5-T05 adds immutable Core contracts for validated
burnup-indexed coefficient values, table rows, table metadata, and lookup
results. The values contract enforces finite SI-style material coefficients,
nonnegative forbidden terms, absorption/fission relationships, fission-support
consistency, positive energy per fission, and the approved `chi` constraints.
The table retains stable identity, schema/data/material/unit/provenance
metadata, and an opaque loader-supplied checksum; it rejects missing metadata,
private file-path provenance, missing checksums, invalid rows, and duplicate or
reordered burnup knots without sorting the caller's data.

Lookup accepts only finite, nonnegative burnup inside the table domain. Exact
knots return the exact source row with the specified endpoint fraction;
interior values use the adjacent strict bracket and the approved linear scalar
interpolation rule. `chi_1` is interpolated and `chi_2` is derived as
`1 - chi_1`, then the complete coefficient invariant set is revalidated.
Out-of-range values, invalid brackets, and invalid interpolated values fail
closed. The result retains table/material/unit/checksum identity, bracket
indices, input burnup, and interpolation fraction for auditability.

This slice deliberately does not invent a canonical table-byte serializer or
claim checksum verification beyond the supplied opaque data-pack identity. It
also does not recompute spatial solver coefficients, consume a complete
P2-T03 power/history envelope, or commit a full lifecycle transition.

**Evidence:** focused P5-T05 tests passed 4/4; the pinned Release Core build
passed with zero warnings/errors; final T3 passed 86/86 Core tests with zero
Golden tests; pinned format passed; the living-guide PDF was regenerated and
consistency-checked; and the bounded same-context code-review (high)
disposition was PASS with no findings. Actual review receipt/model telemetry
is `UNVERIFIED`.

**Literature applicability:** the required P1-T08 two-group/coefficient rows
`S1-R04`, `S1-R05`, `S5-R03`, and `S5-R04`, plus the burnup-indexed candidate
context row `S5-R05`, were reviewed as background/methodology evidence only.
The approved P2-T02 coefficient-invariant and P2-T03 refuelling/burnup
specifications remain the authorities; no literature number, coefficient,
checksum, or golden value was admitted.

**Overlap decision:** P5-T05 consumes the frozen P2-T02/P2-T03 coefficient and
burnup-indexing contracts and does not depend on the blocked P4-T06 DONJON5
admission. It supplies no reference/golden evidence; solver-side recomputation,
complete lifecycle/history binding, deterministic long histories, and G5 remain
separate bounded work.

**Next eligible task:** `P5-T06` - recompute affected coefficients and spatial
state at the approved cadence while preserving the table/solver ownership
boundary.

### `P5-T06` - affected coefficient and spatial-state recomputation

**State:** COMPLETE. P5-T06 adds immutable Core request, cadence, node-volume,
lookup-binding, recomputation, and result contracts for the approved boundary
between burnup-indexed coefficients and the existing spatial solver. The
cadence stores an explicit epoch, positive period, and integer event index; the
next event is constructed from the exact binary64 expression
`epoch + event_index * period`, and no-progress or unrepresentable times fail
closed.

At the scheduled time, the transition resolves every live bundle's current
derived burnup against the validated table for its material variant and binds
one explicit positive volume to each spatial node. It copies the validated
baseline edge and boundary conductances in canonical stencil order, runs the
existing deterministic P2-T02 solve under caller-supplied policies, and
returns lookup provenance for each bundle/node. Lifecycle acceptance occurs
only after a converged solve, a valid next cadence event, exact lifecycle time
and version/digest bindings, and exact topology-instance identity validation.
Failed lookup, stale location or identity, missing conductance, invalid state,
nonconvergence, or cadence failure produces no accepted replacement.

The slice does not add the full P2-T04 scheduler, kinetics/I-Xe/RRS behavior,
canonical data-pack checksum serialization/verification, complete P2-T03 power
history, or reference/golden evidence. It preserves the table/solver ownership
boundary and does not select new equations, tolerances, or golden values.

**Evidence:** focused P5-T06 tests passed 5/5; the pinned Release Core build
passed with zero warnings/errors; pinned format passed; final T3 passed 91/91
Core tests with zero failed/skipped and zero discovered Golden tests; the
living-guide PDF was regenerated and consistency-checked; and the bounded
same-context code-review (high) disposition was PASS with no High, Medium, or
Low findings after correcting topology-instance binding and cadence
no-progress rejection. Actual review receipt/model telemetry is
`UNVERIFIED`.

**Literature applicability:** the required P1-T08 rows `S1-R04`, `S1-R05`,
`S5-R03`, `S5-R04`, and `S5-R05` were reviewed as background/methodology or
candidate-design context only. The approved P2-T02 coefficient/solver and
P2-T03 burnup contracts, together with the P2-T04 cadence/lifecycle ordering
requirements, remain the authorities; no literature number, coefficient,
tolerance, checksum, or golden value was admitted.

**Overlap decision:** P5-T06 consumes frozen P2-T02/P2-T03/P2-T04 boundaries
and is independent of the blocked P4-T06 DONJON5 admission. It supplies no
reference/golden evidence; complete lifecycle/history state, deterministic
long histories, and G5 remain separate bounded work.

**Next eligible task:** `P5-T07` - add the approved Phase 5 identity, location,
burnup, and energy invariants.

### `P5-T07` - Phase 5 identity, location, burnup, and energy invariants

**State:** COMPLETE. P5-T07 adds an engine-neutral, fail-closed invariant
validator over the bounded Core inventory, burnup interval, and refuelling
shift contracts. The expected live bundle count is explicit. The validator
checks exact count, stable identity uniqueness, legal unique locations,
finite/nonnegative burnup and energy fields, positive heavy-metal mass, and
the exact derived energy-to-burnup relation. Burnup results additionally bind
canonical record order, accepted power samples, exact `P * Delta_t` energy,
exact cumulative-energy addition, monotonic burnup, and no-representable-
increase rejection. Refuelling results additionally bind flow direction,
retained identity/location movement, fresh insertion state, and discharged
identity partition.

The slice remains bounded to fields represented by the current Core contracts.
It does not claim complete I/Xe/lifecycle/discharge/history envelopes,
residence-time or power-history observables, deterministic long histories,
reference comparisons, golden consumers, or G5 approval. It does not select a
new equation, unit, tolerance, coefficient, or golden value.

**Evidence:** focused P5-T07 tests passed 4/4; the Release Core build passed
with zero warnings/errors; final T3 passed 95/95 Core tests with zero failed
or skipped tests and zero discovered Golden tests; the living-guide PDF was
regenerated and consistency-checked; and the bounded same-context
code-review (high) disposition was PASS with no High, Medium, or Low findings.
Actual review receipt/model telemetry is `UNVERIFIED`. Validation used the
pinned 10.0.302 SDK without changing `global.json`.

**Literature applicability:** the required P1-T08 rows `S1-R07`, `S4-R03`,
`S4-R04`, `S5-R05`, and `S6-R05` were reviewed as background/methodology or
candidate-design context only. `S4-R04` is advanced-fuel context outside this
runtime slice. The approved P2-T01/P2-T03/P2-T05 identity, refuelling/burnup,
and observable/invariant specifications remain the authorities; no literature
number, coefficient, tolerance, checksum, or golden value was admitted.

**Overlap decision:** P5-T07 consumes frozen P2-T01/P2-T03/P2-T05 boundaries
and is independent of the blocked P4-T06 DONJON5 licensing/data and solver-
mapping admission. It supplies no reference/golden evidence; deterministic
multistep histories remain the next bounded Phase 5 work.

**Next eligible task:** `P5-T08` - deterministic multistep refuelling
histories and selected reference-history comparison preparation, subject to
the separate P4-T06/G4 reference-admission blocker for any DONJON5 comparison.

### `P5-T08` - deterministic multistep refuelling histories

**State:** COMPLETE for the eligible synthetic-history boundary. P5-T08 adds
test-only repeated explicit-time burnup and equal-time S4/S8 refuelling
histories. It advances the synthetic core version once per accepted interval
and once per accepted refuelling batch, validates every transition with the
P5-T07 invariants, checks nonnegative monotone burnup and identity non-reuse,
and compares complete replay fingerprints when accepted-power inputs arrive
in reverse order. The fingerprint covers every canonical burnup record,
ordered discharged state, and resulting live inventory state.

No runtime history schema, production digest serializer, I/Xe/lifecycle
envelope, DONJON5 comparison, reference value, tolerance, or golden baseline
was invented. The synthetic harness is evidence only; it does not make G5 a
technical PASS.

**Evidence:** focused P5-T08 tests passed 3/3; the pinned Release Core build
passed with zero warnings/errors; pinned format passed; final T3 passed 98/98
Core tests with zero failed or skipped tests and zero discovered Golden tests;
the living-guide PDF was regenerated and consistency-checked; and the bounded
same-context code-review (high) disposition was PASS with no High, Medium, or
Low findings. Actual review receipt/model telemetry is `UNVERIFIED`.

**Literature applicability:** the required P1-T08 rows `S1-R07`, `S4-R03`,
`S4-R04`, `S5-R05`, and `S6-R05` were reviewed as background/methodology or
candidate-case-design context only. `S4-R04` is advanced-fuel context outside
this runtime slice. No literature number, coefficient, tolerance, checksum,
or golden value was admitted.

**Overlap decision:** P5-T08 consumes frozen P2-T03/P2-T05 transition and
invariant boundaries and is independent of the blocked P4-T06 DONJON5
licensing/data and solver-mapping admission. Reference sequence comparison and
G5 remain deferred until `P5-T10` consumes the R2D-selected reduced-model path
and the G4-R6-approved authority; direct production/external reference work
remains separately deferred.

**Next dependent task:** `P5-T10` is now eligible for the G4-R6-approved
ReducedModel authority; then run the G5 refuelling/depletion review with the
approved comparisons. Frozen independent work may overlap only when it does not
consume unresolved production/external evidence.

### `P5-T09` - complete I/Xe, lifecycle, power-history, and refuelling state

**State:** COMPLETE for the eligible engine-neutral Core boundary. P5-T09 adds
complete I-135/Xe-135 data and envelope ownership, explicit left-endpoint
Euler integration with continuity/equation checks, atomic batch application,
full accepted-power snapshot/history binding, complete discharge records,
complete before/proposed transaction digests, immutable density rebinding,
fresh insertion validation, and prepared refuelling requests with exact source
core-version, lifecycle-digest, and complete-inventory-digest preconditions.
Changed energy, power history, lifecycle state, or source identity is rejected
before a proposed result is built; retained power history remains available
after the current power binding is invalidated.

The slice is additive and engine-neutral. It does not admit DONJON5 data,
create a runtime reference-data dependency, choose a new equation or
tolerance, or close G5. The user's redistribution rights and reduced-runtime
confirmation are recorded at P4-T06-R1 and the R2D decision. R3 froze the
reduced-model boundary; G4-R6 now supplies approved evidence for the selected
ReducedModel scope, while P5-T10 remains the required sequence-comparison task.

**Evidence:** focused P5-T09 tests passed 5/5; the pinned Release Core build
passed with zero warnings/errors; the full Core suite passed 103/103; the
guarded T3 headless suite passed Core 103/103 with zero failures; pinned format
and whitespace checks passed; and the same-context high-effort code review
returned PASS with no remaining High, Medium, or Low findings. Actual review
receipt/model telemetry is `UNVERIFIED`; the Golden test assembly still has
zero discoverable tests.

**Overlap decision:** P5-T09 consumes the frozen P2-T03/P2-T04/P2-T05 state,
I/Xe, refuelling, and observable contracts and is independent of the blocked
P4-T06 direct Candu6 admission. It supplies no reference/golden evidence. The
separately bounded R4/R5/G4 reduced-model path is now resolved for the selected
ReducedModel scope by G4-R6; P5-T10 is the next Phase 5 task.

**Next dependent task:** none for the synthetic domain; `G4-R4` is complete with
one explicitly scoped synthetic `ApprovedGolden`. `P5-T10` is eligible for the
G4-R6-approved ReducedModel scope and the completed P5-T09 prerequisites.

### `P5-T10` - approved ReducedModel sequence comparison

**State:** COMPLETE for the bounded `RepresentativeReducedModel` /
`ReducedModel` scope approved by `G4-R6`. The test-only Golden consumer binds the
existing G4J sequence definition to the corrected G4K approved artifact, checks
both explicit S4 directions, verifies the two refuelling event records, and
replays the two shifts through the frozen Core contracts. It asserts exact
structural mapping of the existing project-authored burnup projection, stable
identity, cumulative-energy fields, insertion time, discharge preservation, and
final inventory invariants. It does not independently integrate power into
burnup or establish accumulated numerical drift. It introduces no runtime
equation, schema, tolerance, or new golden authority and makes no direct
DONJON5/CANDU production claim.

**Evidence:** focused P5-T10 tests passed 2/2; the full T3 headless suite passed
Core 103/103 and Golden 19/19 with zero failures or skips. The final independent
same-context code review (high) returned `PASS` with no actionable findings.
Receipt parsing verifies the reviewer ran as `gpt-5.6-sol` at `high` effort in
reviewer context `01a01217-337b-79e1-9b86-88232ccf91d1`, final turn
`01a0125d-0b07-7731-a67b-fe6f0a461372`. The current G4-R6 approved artifact and
manifest hashes remain bound by its correction addendum; P5-T10 did not modify
either file. The pinned formatter check could not run because SDK `10.0.302`
is absent while `global.json` pins it; the pinned file was not changed.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` were reviewed as context/methodology and candidate-design evidence
only. The frozen Phase 2 specifications and approved reduced-model artifact
remain the authorities; no literature number was admitted.

**Overlap decision:** P5-T10 consumes only frozen Phase 5 contracts and the
G4-R6-approved ReducedModel authority. It is independent of the unresolved
direct production/external CANDU authority and does not expand the runtime or
Unity boundary.

**Next dependent task:** `P5-T11` - correct the complete-state burnup commit,
lookup invalidation, and composed recompute/refuelling path before the `G5`
rerun. Direct production/external authority remains a separate deferred path.

### `P5-T11` - complete-state burnup commit and stale-binding correction

**State:** Implementation and focused evidence COMPLETE; `G5` correction rerun
pending. P5-T11 closes the review finding that the accepted complete-state
burnup path could carry stale current power and coefficient bindings through an
energy update. The new atomic transition binds the complete accepted snapshot
and lifecycle, applies the frozen left-endpoint energy rule, performs and
stores every next-state coefficient lookup, advances explicit time and core
version, invalidates current power/snapshot bindings, retains append-only power
history, and leaves the source state untouched on failed lookup. The legacy
energy helper also invalidates stale bindings. A strict complete-refuelling
overload performs post-shift lookups before lifecycle commit.

The composed Core tests cover complete snapshot -> burnup -> lookup/spatial
recompute -> accepted snapshot -> strict S4 refuelling, including discharge,
stale-snapshot rejection, rollback, post-shift bindings, and repeatable history.
P5-T11 changes no equation, unit, sign, tolerance, golden artifact, direct
external authority, Unity adapter, or mobile behavior.

**Evidence:** focused P5-T11 tests passed 2/2; the full Core suite passed
105/105; the full T3 headless suite passed Core 105/105 and Golden 19/19.
The pinned formatter could not run because SDK `10.0.302` is absent while
`global.json` pins it; the pinned file was preserved. The independent
same-context high-effort G5 correction review remains the next administrative
step.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` are context/methodology or candidate-design evidence only. The frozen
refuelling/burnup specification, complete-state contracts, and approved
ReducedModel boundary remain the authorities.

**Overlap decision:** P5-T11 consumes frozen Phase 5 contracts and the
G4-R6-approved ReducedModel boundary. It is independent of unresolved direct
production/external authority and does not expand the runtime or Unity boundary.

**Next dependent task:** `P5-T13` - authenticate the full accepted
power-history inventory at the burnup boundary before the `G5` rerun.

### `P5-T12` - source-digest authentication and strict refuelling boundary

**State:** Implementation and focused evidence COMPLETE; `G5` third
correction rerun pending after P5-T13. P5-T12 adds explicit canonical inventory and
burnup/energy digests to complete accepted power snapshots, serializes them,
and verifies them both when a snapshot is accepted and when a positive-duration
burnup interval commits. A reconstructed inventory retaining accepted
power/history bindings but changing energy or mass is rejected before any
replacement state is built. The public no-table complete-refuelling overload
now fails closed; every successful complete-state refuelling path requires a
version-matched coefficient table and stores post-shift lookup bindings.

**Evidence:** focused P5-T11/P5-T12 coverage passed 3/3 at that task
disposition; the later P5-T13 correction passes 4/4 and Phase 5 focused Core
coverage passes 41/41; the full T3 headless suite passes Core 107/107 and
Golden 19/19 with zero failures or skips. No equation, unit, sign, tolerance,
golden artifact, direct external authority, Unity adapter, or mobile behavior
changed. The pinned formatter remains unavailable because SDK `10.0.302` is
absent while `global.json` pins it; the pinned file was preserved.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` remain context/methodology or candidate-design evidence only. Frozen
complete-state/refuelling specifications and the approved ReducedModel
boundary remain the authorities.

**Overlap decision:** P5-T12 consumes frozen Phase 5 contracts and the
G4-R6-approved ReducedModel boundary. It is independent of unresolved direct
production/external authority and does not expand the runtime or Unity boundary.

**Next dependent task:** `P5-T14` - bind complete snapshot inventory and
burnup/energy digests into the accepted lifecycle before the G5 rerun.

### `P5-T13` - full accepted power-history authentication correction

**State:** Implementation and focused evidence COMPLETE; the third same-context
G5 correction rerun is pending. P5-T13 adds separate full pre-accept and
post-accept inventory digests to the complete power snapshot. The burnup
boundary authenticates the full post-accept digest, including every retained
power-history record and current power/coefficient binding. A focused test
removes an earlier history record while retaining the current accepted record
and verifies fail-closed rejection with unchanged source lifecycle state.

**Evidence:** focused P5-T11/P5-T13 tests pass 4/4; Phase 5 focused Core tests
pass 41/41; the full T3 headless suite passes Core 107/107 and Golden 19/19.
The pinned formatter remains unavailable because SDK `10.0.302` is absent;
`global.json` was preserved. No equation, unit, sign, tolerance, golden
artifact, direct external authority, Unity adapter, or mobile behavior changed.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` remain context/methodology or candidate-design evidence only.

**Next dependent task:** `P5-T14` - bind complete snapshot inventory and
burnup/energy digests into the accepted lifecycle before the G5 rerun.

### `P5-T14` - lifecycle-authenticated complete snapshot digest correction

**State:** Implementation and focused evidence COMPLETE; the fourth same-context
G5 correction rerun is pending. P5-T14 stores the full pre-accept inventory,
full post-accept inventory, and burnup/energy digests on the accepted lifecycle
and includes them in the canonical lifecycle projection. Burnup requires the
supplied snapshot to match those lifecycle-authenticated values, then checks
the live inventory against the lifecycle-stored post-accept digest. A focused
test reconstructs a history-truncated inventory and matching forged snapshot
while reusing lifecycle IDs and verifies fail-closed rejection.

**Evidence:** focused P5-T11/P5-T14 tests pass 5/5; Phase 5 focused Core tests
pass 42/42; the full T3 headless suite passes Core 108/108 and Golden 19/19.
G4-R6 consumer 3/3, P5-T10 consumer 2/2, and the six-profile digest audit
pass. The pinned formatter remains unavailable because SDK `10.0.302` is
absent; `global.json` was preserved. No equation, unit, sign, tolerance,
golden artifact, direct external authority, Unity adapter, or mobile behavior
changed.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` remain context/methodology or candidate-design evidence only.

**Next dependent task:** `P5-T15` - preserve the lifecycle-authenticated
snapshot digest tuple and complete inventory through the state archive before
the G5 rerun.

### `P5-T15` - persistence-safe complete snapshot lifecycle binding

**State:** Implementation and focused evidence COMPLETE; the fifth same-context
G5 correction rerun is pending. P5-T15 advances the lifecycle, state-snapshot,
and state-archive schema boundaries to version 2, serializes the three
lifecycle-authenticated snapshot digests, and persists the complete inventory
projection needed to reproduce the accepted post-snapshot digest. A focused
save/load regression preserves all three digests and the full inventory digest,
then completes a subsequent burnup transition from the restored state.

**Evidence:** P3-T04 serialization tests pass 5/5; focused P5-T11/P5-T15 tests
pass 6/6; Phase 5 focused Core tests pass 43/43; the full T3 headless suite
passes Core 109/109 and Golden 19/19. G4-R6 consumer 3/3, P5-T10 consumer
2/2, and the six-profile digest audit pass. The pinned formatter passes with
the existing `10.0.302` SDK cache; `global.json` was preserved. No equation,
unit, sign, tolerance, golden artifact, direct external authority, Unity
adapter, or mobile behavior changed.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` remain context/methodology or candidate-design evidence only.

**Next dependent task:** `P5-T16` - reconcile restored complete-state digests
and reconstruct the accepted snapshot from restored state before the G5 rerun.

### `P5-T16` - restore authentication and restart reconstruction correction

**State:** Implementation and focused evidence COMPLETE; the final same-context
G5 correction review is `PASS`. P5-T16 addresses and closes the findings from the
P5-T15 review: restore now reconciles the restored complete inventory and
burnup/energy state against the lifecycle-authenticated digests and validates
per-bundle power, snapshot, coefficient, nuclide, and retained-history
coherence. The post-load path reconstructs the complete accepted snapshot from
restored state, compares canonical bytes, and uses that reconstructed object
for the subsequent burnup instead of reusing the pre-save object.

**Evidence:** P3-T04 serialization tests pass 5/5; focused P5-T11/P5-T16 tests
pass 7/7; Phase 5 focused Core tests pass 44/44; the full T3 headless suite
passes Core 110/110 and Golden 19/19. G4-R6 consumer 3/3, P5-T10 consumer
2/2, and the six-profile digest audit pass. The pinned formatter passes with
the existing `10.0.302` SDK cache; `global.json` was preserved. Artifact and
manifest hashes remain unchanged. No equation, unit, sign, tolerance, golden
artifact, direct external authority, Unity adapter, or mobile behavior changed.

**Literature applicability:** `S1-R07`, `S4-R03`, `S4-R04`, `S5-R05`, and
`S6-R05` remain context/methodology or candidate-design evidence only.

**Next dependent task:** Phase 6/G6 task-definition chain - instantiate the
seven Phase 6 work packages from the implementation plan and frozen RRS
specification before execution.

### `P6-T01` - liquid-zone state contract

**State:** COMPLETE. `P6-T01` implemented the engine-neutral contract for
exactly 14 logical liquid-zone compartments grouped by exactly 6 declared
physical assemblies. It binds explicit grouping identity/version/digest,
fill state, mode, queue identity and typed branch owner, data/version
identities, canonical state digests, and a zone-level disabled-zero assertion.
It does not implement influence-map weights, overlay motion, controllers,
poison, adjusters, or queue transitions.

**Evidence:** the focused P6-T01 suite passes 5/5; final headless Core passes
115/115; the existing golden-consumer suite passes 19/19; pinned Unity
EditMode, PlayMode, and StandaloneWindows64 external-copy smoke each pass
1/1. The same independent code-review context returned final `PASS`, but
actual reviewer model/effort telemetry is unavailable and therefore remains
`UNVERIFIED`.

**Literature applicability:** `S1-R08`, `S2-R05`, `S6-R02`, `S6-R04`,
`S6-R05`, and `S6-R08` are context/methodology/candidate-design rows only;
none is a runtime map or golden authority for this contract.

**Next eligible task:** `P6-T02` - complete. The owner-approved synthetic
package and bounded overlay/motion evidence are recorded in the P6-T02 report;
the next eligible task is `P6-T03`.

### `P6-T02` - liquid-zone influence-map admission and bounded motion

**State:** COMPLETE / SYNTHETIC TEST-ONLY. The initial authority audit was
`BLOCKED` and is preserved in the task report. After the owner supplied `Kevin
Ho` and approved `P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1`, the reopened task
admitted the exact versioned synthetic package and implemented the bounded
engine-neutral overlay and pure delayed/rate-limited motion projection.
No production CANDU, external-reference, golden-data, queue-transition, or
scenario authority was invented or admitted.

**Evidence:** the owner approval record, synthetic package/manifest, focused
P6-T02 suite `6/6`, full Core `121/121`, existing Golden consumers `19/19`,
and pinned Unity EditMode/PlayMode/StandaloneWindows64 smoke `1/1` each pass.
The same independent review context first returned `FAIL` with four findings;
the corrected candidate received final `PASS` with no remaining blocking or
medium findings. Actual reviewer model/effort telemetry is unavailable, so the
review remains `UNVERIFIED`.

**Literature applicability:** `S1-R08`, `S2-R05`, `S6-R02`, `S6-R04`,
`S6-R05`, and `S6-R08` remain context/methodology/candidate-design rows only;
none is a runtime map or golden authority for the synthetic package.

**Next eligible task:** `P6-T03` - continue the Phase 6 chain with its own
approved input authority and preserve the P6-T02 synthetic/non-production
boundary.

### `P6-T03` - adjuster-bank state and influence-map binding

**State:** `COMPLETE / SYNTHETIC TEST-ONLY`. The initial authority audit was
`BLOCKED` and is preserved historically in [`P6-T03.md`](tasks/P6-T03.md).
After Kevin Ho supplied the exact approval for
`P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1`, the task admitted the versioned
synthetic grouping/map/package and implemented the engine-neutral Core state,
complete supplied queue projection, causal delayed/rate-limited motion, typed
owner binding, and deterministic local overlay/rollback evidence. No
production CANDU, external-reference, or golden-data claim was made.

**Evidence:** the owner approval record, synthetic package/manifest, focused
P6-T03 suite `5/5`, full Core `126/126`, existing Golden consumers `19/19`,
and pinned Unity import/compile smoke pass. The independent code review is
recorded in the task report; actual reviewer model/effort telemetry remains
`UNVERIFIED`.

**Literature applicability:** digest `candu-literature-digest-v1`, rows
`S1-R08`, `S4-R05`, `S6-R02`, `S6-R05`, and `S6-R08` are device-role,
candidate-design, or limitation context only; none is a runtime or golden
adjuster authority.

**Next eligible task:** `P6-T04` - continue with its own approved poison-map
authority; queue allocation/transition/rollback and scenario packages remain
reserved for `P6-T06`/`P6-T07`.

### `P6-T04` - bulk moderator poison state and influence-map admission

**State:** `COMPLETE / SYNTHETIC TEST-ONLY`. Kevin Ho approved the exact
versioned P6-T04 synthetic fixture on 2026-08-20. The engine-neutral Core
implementation validates poison-owned state, 5.0 m^3 moderator volume, derived
concentration, separate 0.1 kg/s add and 0.01 kg/s withdraw projections,
Prescribed setup maximum 1.0 kg, positive absorption overlay, six explicit
targets, two groups, exact map/state/overlay/action digests, and local scratch
rollback without shared queue mutation.

**Evidence:** [`P6-T04.md`](tasks/P6-T04.md) records the reopened execution,
focused 6/6 tests, full T3 Core 132/132 plus Golden 19/19 regression, pinned
T4 Unity import/compile smoke, and independent high review disposition. The
owner approval is preserved in
[`P6-T04-OWNER-APPROVAL.md`](tasks/P6-T04-OWNER-APPROVAL.md); the package and
manifest are `data/packs/p6-t04-synthetic-bulk-poison-map-v1.json` and
`data/packs/p6-t04-synthetic-bulk-poison-map-v1.manifest.json`. The package is
synthetic/test-only and makes no production CANDU, external-reference, or
golden-data claim.

**Literature applicability:** digest rows `S1-R04`, `S1-R08`, `S1-R10`,
`S3-R05`, `S6-R03`, `S6-R05`, and `S6-R06` provide context or candidate-design
limitations only; none is a P6-T04 runtime, map, or golden authority.

**Next eligible task:** `P6-T05` - continue the Phase 6 chain with its own
approved authority. Queue allocation/transition/rollback remains reserved for
`P6-T06`, and scenario packages remain reserved for `P6-T07`.

### `P6-T05` - limited total-power and regional-tilt controller

**State:** `COMPLETE / SYNTHETIC TEST-ONLY`. Kevin Ho approved the exact
versioned fixture on 2026-08-20. The engine-neutral Core implementation
validates the immutable controller state, complete read-only supplied queue
projection, two disjoint complete regions, signed gains and units,
automatic/manual/held projections, local overlay signs, deterministic
canonical digests, fail-closed bindings, and local scratch rollback. It makes
no production CANDU, external-reference, or golden-data claim.

**Evidence:** [`P6-T05.md`](tasks/P6-T05.md) records the reopened execution,
focused 7/7 tests, full T3 Core 139/139 plus Golden 19/19 regression, pinned
T4 Unity import/compile smoke, package/manifest hash binding, generated
digests, and independent high-review findings/corrections/final `PASS` with no
remaining findings. Actual reviewer receipt telemetry is `UNVERIFIED`.
The owner approval is preserved in
[`P6-T05-OWNER-APPROVAL.md`](tasks/P6-T05-OWNER-APPROVAL.md); the package and
manifest are `data/packs/p6-t05-synthetic-rrs-controller-map-v1.json` and
`data/packs/p6-t05-synthetic-rrs-controller-map-v1.manifest.json`.
Shared queue allocation, command identity allocation, enqueue/consume,
same-time ordering, transition, actuator motion, and atomic queue rollback are
now recorded in `P6-T06`; scenarios remain reserved for `P6-T07`.

**Literature applicability:** digest rows `S1-R08`, `S4-R05`, `S6-R02`,
`S6-R05`, and `S6-R08` provide device-role, regulating-system, adjuster, and
candidate-design context or limitations only; none is a P6-T05 controller,
gain, map, or golden authority.

**Next eligible task:** `P6-T07` - continue with the bounded scenario
packages within their own synthetic/test-only boundary. Final gate review
remains `G6`.

### `P6-T06` - shared queue transitions and actuator motion

**State:** `COMPLETE / SYNTHETIC TEST-ONLY`. The engine-neutral Core queue
transition layer implements typed owner/target identity, deterministic command
IDs and canonical queue/command bytes, source-binding and event-phase tokens,
exact due-time construction, saturation diagnostics, allocator overflow and
reuse rejection, same-time ordering, causal motion-before-consume behavior,
branch owner/queue ordering, and immutable atomic rollback for RRS,
liquid-zone, and adjuster queues. No production CANDU, external-reference, or
golden-data claim was made; P6-T07 owns scenarios.

**Evidence:** [`P6-T06.md`](tasks/P6-T06.md) records the focused 7/7 suite,
final T3 Core 146/146 plus Golden 19/19 regression, pinned T4 Unity
import/compile smoke, final independent high-review `PASS` with no remaining
findings, and the model/receipt telemetry limitation (`UNVERIFIED`).

**Literature applicability:** digest rows `S1-R08`, `S4-R05`, `S6-R02`,
`S6-R05`, and `S6-R08` provide context or candidate-design limitations only;
none is a runtime queue, actuator, reference, or golden authority.

**Next eligible task:** `P6-T07` - bounded centered-perturbation, regional-
tilt, zone-saturation, adjuster-motion, and poison-recovery scenarios. `G6`
remains the final Phase 6 gate.

### `P6-T07` - bounded RRS scenario evidence fixtures

**State:** `COMPLETE / SYNTHETIC TEST-ONLY`. P6-T07 adds the five bounded
scenario fixtures required by the activated Phase 6 chain: centered
perturbation, regional tilt, zone saturation, adjuster insertion/withdrawal,
and poison recovery. The package binds the approved P6-T02 through P6-T05
synthetic inputs and records deterministic replay, exact one-step/two-step
motion partition equality, saturation, poison mass/concentration accounting,
and the frozen same-time refuelling event order. It does not execute or mutate
refuelling state and adds no production, external-reference, golden-data,
safety-system, shutdown, scram, trip, hidden-chemistry, or hidden-reactivity
behavior.

**Evidence:** [`P6-T07.md`](tasks/P6-T07.md) records the scenario package and
manifest, focused P6-T07 `8/8`, final T3 Core `154/154` plus Golden `19/19`,
and the required independent high review `PASS` with telemetry `UNVERIFIED`.
The package is synthetic/test-only; it is not an approved RRS comparison or
production/golden authority.

**Literature applicability:** digest rows `S1-R08`, `S4-R05`, `S6-R02`,
`S6-R05`, and `S6-R08` provide device-role, regulating-system, adjuster,
candidate-design, or limitation context only; none is a scenario, runtime,
reference, or golden authority.

**Next eligible task:** [`G6`](gates/G6.md) - `CONDITIONAL PASS` for the
synthetic/test-only Phase 6 scope. An owner-authorized RRS comparison package
and G6 re-entry are required for an unconditional comparison disposition.

### `G6` - Phase 6 regulating-system review

**State:** `CONDITIONAL PASS` for the approved synthetic/test-only
engine-neutral Core/ReducedModel scope. The gate report records aggregate P6
focused `44/44`, T3 Core `154/154`, Golden `19/19`, the package/boundary audit,
and the independent high review `CONDITIONAL PASS` with actual telemetry
`UNVERIFIED`. The gate confirms signs, region mapping, rates, saturation,
controller stability, timestep partitioning, queue/refuelling ordering, and
the no-safety boundary only for the versioned synthetic fixtures.

The gate does not admit a production, external-reference, or golden RRS
comparison. The earlier G4-R6 approved ReducedModel payload is non-equivalent
for this P6 package and declares `liquid_zone_adjuster_runtime=NotCovered`.
An owner-authorized, fully manifested P6 RRS comparison package and a G6
re-entry are required before an unconditional comparison disposition.

**Evidence:** [`G6.md`](gates/G6.md).

**Next eligible work:** separately authorized RRS comparison-package admission
and G6 re-entry; no production or external RRS claim is eligible from this
conditional result.

### G4 resolution and remaining boundary

1. `P4-T06-G4A` is complete: its report binds the exact model/tool/data,
   topology/geometry, units, normalization, observables, reproducibility, and
   licensing for the three admitted static synthetic cases, and explicitly
   dispositions the missing cases. It did not silently change a reference
   baseline or reinterpret candidate evidence.
2. `P4-T06-G4B` is complete: its four test-only consumers bind the compact
   case, manifest, artifact hashes, and P2-T05 profile/quantity identities.
   The consumers remain candidate/deferred checks; they are not approved
   golden evidence and do not select a tolerance.
3. R5's refuelled nonconvergence and RRS/poison not-covered findings remain
   visible until approved inputs or an explicit owner-approved scope
   disposition exists. Do not synthesize maps, overlays, or tolerances.
4. `G4-R1` reran the gate after G4A and G4B and remains `BLOCKED`: the
   candidate evidence is internally exact but not independently validated.
5. The authorized G4 admission chain in
   [the task set](tasks/ROUND-2026-08-16-G4-ADMISSION-CHAIN.md) is complete:
   `P4-T06-G4C` recorded the authority and mapping, `P4-T06-G4D` produced the
   candidate reproduction, `P4-T06-G4E` bound test-only consumers, and
   `G4-R2` reran the gate. G4-R2 remains `BLOCKED` because no approved
   baseline/tolerance authority is present.
6. The non-looping follow-up chain in
   [the G4 block-resolution task set](tasks/ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md)
   has completed `P4-T06-G4F` as candidate-only analytical evidence. It does
   not change the G4-R2 disposition or approve a threshold.
7. `P4-T06-G4G` supplied the bounded mathematical-validation basis and
   explicit missing-coverage disposition. G4 owns solver tolerance approval;
   no preceding task invented a numeric acceptance threshold.
8. `G4-R3` completed the fresh gate execution and remains `BLOCKED` because
   numeric tolerance/golden authority and representative coverage are absent.
9. `P4-T06-G4H` is complete: its authority audit found no admissible numeric
   baseline/tolerance authority and explicitly declined production G4 approval.
   It preserved the G4-R3 blocker, all Candidate/Deferred/NoGolden statuses,
   and the refuelled/RRS/poison coverage deferrals without deriving a threshold
   from raw candidate differences.
10. `P4-T06-G4I` is complete for a new owner-authorized synthetic-only case:
    the project-authored manufactured definition binds a 2x2 topology,
    heterogeneous coefficients, reciprocal axial/transverse edges, vacuum end
    leakage, units, normalization, output order, and rights. A standalone
    deterministic solver reproduces the exact manufactured state and a Core
    consumer converges against it. The artifact remains
    `Candidate`/`Deferred`/`NoGolden`; it does not change production G4 or the
    existing refuelled/RRS/poison coverage dispositions.
11. `G4-R4` is complete as a conditional synthetic-only pass: it approved the
    new `ApprovedGolden` payload and six explicit `1e-10` profiles for the
    `Synthetic` domain after the authority audit, T3/T6-style evidence, and
    high-effort review lane. At that point it did not relabel the case as a
    direct CANDU physics baseline or unblock `P5-T10`; G4-R6 later cleared the
    selected ReducedModel scope.
12. G4 is clear for the selected ReducedModel runtime domain under `G4-R6`.
    Direct production/external authority, full-core coverage, refuelled
    production history, RRS, and poison evidence remain separate and require
    their own owner-authorized task; do not restart the completed chain.
13. `P4-T06-G4J` is complete as a separate candidate-only representative
    reduced-model sequence case. It binds a 4x12 topology, alternating flow,
    burnup projection, auditable S4 shifts in both directions, prescribed RRS
    and bulk-poison overlays, explicit authority fields, standalone repeat,
    and a Core consumer. It does not establish external CANDU authority or
    production tolerances. The separately executed `G4-R5` disposition is
    `BLOCKED` / `NO-ADMISSION`: its standalone result is implementation-level
    independent evidence, not an exact oracle or admissible external authority.
14. `P4-T06-G4K` is complete as the independent authority package for the
    selected runtime `ReducedModel` scope. It covers all five G4J scenarios,
    uses a materially independent dense expected-result path, binds every
    authority field, preserves `Candidate`/`Deferred`/`NoGolden` status, and
    passes T0/T1/T3 evidence. Review telemetry remains `UNVERIFIED`; the
    technical review disposition is `PASS` with no unresolved findings.
15. Fresh `G4-R6` is complete with `PASS` for the selected `ReducedModel`
    scope. It audited G4K, created the sibling `ApprovedGolden` payload with
    six profiles, activated its approved consumer, and passed focused 3/3 plus
    T3 Core 103/103 and Golden 17/17. It does not establish direct CANDU or
    production authority.

### `P4-T06-G4H` - owner-authorized numeric baseline and tolerance decision

**State:** COMPLETE. The authority audit found no admissible numeric/reference
authority with the required model/tool/version, data/material identity,
geometry/topology, state/history, units, normalization, observable mapping,
reproducibility, and rights. Production G4 approval was explicitly declined;
`G4-R3` and `G4` remain `BLOCKED`. No threshold, tolerance, golden value, or
Candidate/Deferred/NoGolden status changed. The refuelled case remains
`Nonconverged`, RRS and poison remain `NotCovered`; at that historical
disposition `P5-T10`/`G5` remained blocked and no next task was eligible until
a new owner-authorized task supplied an admissible authority or changed the
scope decision. `G4-R6` later cleared the selected ReducedModel scope.

### `P4-T06-G4I` - manufactured synthetic spatial authority candidate

**State:** COMPLETE for the bounded synthetic-validation task. The new case is
a project-authored manufactured solution, not an external CANDU/DRAGON5/
DONJON5 reference. It binds exact source/generator identity, the source
definition hash, explicit 2x2 channel/position topology, reciprocal axial and
transverse edges, reflective cardinal faces, nonzero vacuum end leakage,
heterogeneous volumes and coefficients, SI units, target-power normalization,
state/ordering rules, observable identities, convergence policy, standalone
repeat hashes, and redistribution rights. The candidate artifact remains
`Candidate`/`Deferred`/`NoGolden` until G4 acts.

Evidence: the standalone generator produced and revalidated byte-identical
candidate/manifest output; exact manufactured equation residual relative
infinity is approximately `3.744e-17`; the independent solver converged in 54
outer iterations with maximum flux absolute error approximately `3.548e-12`,
eigenvalue absolute error approximately `4.473e-13`, and power error zero; the
Core consumer converged and passed the diagnostic candidate bounds; the full
headless suite passed Core 103/103 and Golden 10/10. Actual model/reviewer
receipt telemetry remains `UNVERIFIED`.

The case supports a golden/tolerance decision for the synthetic spatial
operator and normalization domain. It cannot establish external CANDU physics,
burnup/refuelling, RRS, poison, or production representative coverage. The
fresh `G4-R4` gate conditionally approved the sibling payload under
`data/golden` with six explicit profiles; the original candidate remains
unchanged.

### `G4-R4` - synthetic-only G4 disposition

**State:** COMPLETE — CONDITIONAL PASS for `Synthetic`. The gate audited the
nine authority fields, preserved the G4I candidate, regenerated the approved
payload and manifest byte-identically, and passed the focused approved consumer
and full T3 suite. It approved `ApprovedGolden`/`Approved` status only for
`g4i_2x2_vacuum_nonuniform`, with explicit `1e-10` profiles for k, flux, node
power, total power, and equation residual plus exact discrete convergence
metadata. Production G4 remains `BLOCKED` because the case is not an external
CANDU authority and representative refuelled/RRS/poison coverage is incomplete.

See the [fresh gate report](gates/G4-R4.md) and [gate task report](tasks/G4-R4.md)
for the hashes, commands, findings, and deferred production work.

### `P4-T06-G4J` - representative reduced-model sequence candidate

**State:** COMPLETE — candidate-only `RepresentativeReducedModel`. The case
binds an explicit 4-channel by 12-position 2x2 topology, alternating
`EndAtoEndB`/`EndBtoEndA` flow, 92 reciprocal edges, 104 explicit boundaries,
burnup-indexed projection from the existing candidate pack, two auditable S4
shifts with insertion/movement/discharge records, a prescribed RRS overlay,
and a prescribed bulk-poison overlay. It covers five deterministic scenario
states: fresh, equilibrium-like, refuelled, RRS-tilt, and bulk poison.

The standalone generator repeats each scenario byte-identically and the Core
consumer converges all five scenarios. The candidate remains `Candidate` /
`Deferred` / `NoGolden` under the `ReducedModel` domain. It explicitly leaves
external CANDU numeric authority, full 380-channel coverage, I/Xe, kinetics,
liquid-zone/adjuster runtime sequences, thermal behavior, and production G4
tolerance approval deferred. See the [G4J task report](tasks/P4-T06-G4J.md)
and the [candidate artifact](../data/comparisons/p4-t06-g4j-representative-authority-v1.json).

### `G4-R5` - reduced-model disposition

**State:** `BLOCKED` / `NO-ADMISSION`. The gate separately reviewed the G4J
candidate for reduced-model tolerance or golden admission. The candidate
authority fields are complete for the bounded project-authored case; its five
scenarios regenerate byte-identically, converge through the Core consumer, and
pass the focused and full headless checks. Those results are implementation-
level consistency evidence only. The standalone solver and Core consumer both
consume the same project-authored candidate definition/material pack, so the
comparison is not an exact manufactured oracle or an admissible external
authority.

The candidate remains `Candidate` / `Deferred` / `NoGolden` with its `1e-8`
diagnostic bound. No G4J golden payload or approved tolerance profile was
retained. At the time of this historical disposition, production G4, `P5-T10`,
and `G5` remained blocked. G4-R6 later approved a sibling authority for the
selected ReducedModel scope; a future owner-authorized task would still need an
exact oracle or complete independent authority
with model/tool/build, nuclear/material identity, topology/history, units,
normalization, observable mapping, reproduction, and rights before any
reduced-model admission could be reconsidered. See the [G4-R5 gate report](gates/G4-R5.md)
and [task report](tasks/G4-R5.md).

### `P4-T06-G4K` - independent reduced-model G4 authority package

**State:** `COMPLETE` technical PASS; candidate remains
`Candidate`/`Deferred`/`NoGolden`, and review receipt telemetry is
`UNVERIFIED`. The task replaced G4J's implementation-level self-consistency
evidence with an independent dense expected-result authority for all five
reduced-model scenarios. It binds the frozen P2 contracts, complete
provenance/rights, P2-T05 profile identities and digests, hash-bound expected
outputs, canonical flux ordering, deterministic regeneration, altered-hash
rejection, and Core consumers. It did not create an approved golden payload or
select a tolerance. See the [P4-T06-G4K execution report](tasks/P4-T06-G4K.md).

### `G4-R6` - fresh reduced-model G4 disposition

**State:** `COMPLETE — PASS` for the selected `ReducedModel` runtime domain.
The gate independently audited G4K, approved the sibling `ApprovedGolden`
payload with six quantity-specific profiles, activated its approved consumer,
and passed focused 3/3 plus T3 Core 103/103 and Golden 17/17. Direct
CANDU/production authority, external/release identity, and full-core coverage
remain separate and deferred. Review receipt telemetry is explicitly
`UNVERIFIED`; the technical disposition contains no unresolved review finding.
See the [G4-R6 gate report](gates/G4-R6.md) and [task report](tasks/G4-R6.md).

**G4-R6 correction addendum (2026-08-17):** the approved artifact was
regenerated after the final same-context review found two bounded defects in
the handoff evidence. The exact `spatial.convergence` profile now carries
explicit `null` numeric thresholds, and the approved `spatial.power` scalar
profile is enforced independently at every node. The current approved artifact
SHA-256 is
`897c0b4dc055f59b21b110f4674903c12e795e9cfaaf2131cb3ab46e042ede6a`; the
current manifest SHA-256 is
`e927a014c15f13341bd4739c59bc8f9c7d8bd4e36a74a0c37dc669ab83098f4b`.
`ProfileDigestAudit.py` and the focused consumer pass 3/3; the superseded
hashes remain historical evidence. The same reviewer context is completing a
final disposition over the corrected records before P5-T10 starts.

The final disposition is `PASS` with no remaining findings. Receipt telemetry
for that same context verifies `gpt-5.6-sol` at `high` effort (final turn
`01a01254-3951-7d32-8438-16ace632a525`; reviewer context
`01a01217-337b-79e1-9b86-88232ccf91d1`). Cumulative reviewer-session usage was
input `9,999,733`, cached input `9,741,824`, cache-write input `0`, output
`31,441`, reasoning output `14,631`, total `10,031,174`; no per-task cost or
goal allocation was available.

## Remaining delivery roadmap

| Key | Work still required | Gate evidence |
|---|---|---|
| `PHASE-5` / `G5` | Complete: P5-T01 through P5-T16 implementation evidence and final G5 `PASS` for the approved ReducedModel/engine-neutral Core scope. | T3 plus approved selected reference or reduced-model histories; review exact bundle movement, power-to-burnup units, interpolation edges, cadence/lifecycle binding, complete-state invalidation, and accumulated drift. Direct external authority remains deferred. |
| `PHASE-6` / `G6` | Conditional PASS: `P6-T01` through `P6-T07` are complete within their approved synthetic Core/test-fixture boundaries; the remaining work is an owner-authorized comparison package and G6 re-entry for an unconditional disposition. | T3 plus an applicable approved RRS comparison package; current conditional evidence validates queue transitions, signs, region mapping, rates, saturation, stability, timestep sensitivity, and refuelling interaction only within the synthetic fixtures. |
| `PHASE-7A` / `G7A` | Approved point-kinetics/quasi-static amplitude, I-135/Xe-135 history, stable timestep integration, controlled spatial coupling, and history scenarios. | T3, long histories, timestep convergence, nonnegative inventories, and approved reference cases. |
| `PHASE-7B` / `G7B` | Optional temperature/purity feedback branches and coupled-stability work, only if enabled by approved data. | Required only when enabled. A failed `G7B` does not block release with feedback disabled. |
| `PHASE-8` / `G8` | Complete CLI game loop: commands, scenarios, operating envelope/loss, scoring, explanations, versioned replay, balance bots, and soak monitoring. | T3, replay/seed checks, malformed-command coverage, scripted strategies, and long soaks. |
| `PHASE-9` / `G9` | Versioned benchmarks, profiling, plain C# optimization, optional Burst decision, off-frame solves, Android/iOS spikes, and sustained device evidence. | T3/T4/T5; prove numerical equivalence, no replay drift, no hot-loop allocations, and device/thermal behavior. |
| `PHASE-10` / `G10` | Touch-first Unity interface, core map/detail/refuelling/RRS panels, trends/events, safe-area/accessibility/tutorials, and save/replay UI. | Pure visuals use T1/T4. Any adapter/command/serialization/clock/data-loading change also triggers T3; mobile smoke uses T5. |
| `PHASE-11` / `G11` | Lock data/save schemas and migrations, licensing/privacy/offline policy, store assets/signing, device matrix, release notes, and limitation archive. | Full T3-T5 release audit; T6 only if the reference baseline changed. Release requires PASS and explicit external authority. |

## Evidence gaps that remain visible

- No approved production/full-reference golden baseline or production numeric
  tolerance threshold exists. `G4-R4` approved one synthetic-only golden case
  and six profiles, while `G4-R6` approved the bounded ReducedModel payload;
  `G4`, `G5`, `G7A`, and `G7B` still own their production thresholds; G6's
  conditional synthetic-only result does not approve production RRS thresholds.
- G4H explicitly declined production G4 approval because no complete numeric
  authority was available. `P4-T06-G4I` and `G4-R4` now provide a bounded
  synthetic-only approval, but the candidate/production boundary and external
  authority gap remain visible.
- Literature from `P1-T08` is indexed context/methodology or candidate evidence;
  it is not an executable oracle or approved golden data without complete case
  admission proof.
- DRAGON5/DONJON5 source artifacts and private inputs remain external. Stop if
  a required license, nuclear-data source, unit, normalization, or reproducible
  case cannot be established.
- The reference test project now contains candidate and approved consumers for
  `P4-T06-G4I`, a candidate consumer for `P4-T06-G4J`, and an approved consumer
  for `P4-T06-G4K`; the approved payloads are scoped to Synthetic and the
  selected ReducedModel domains, while G4J remains candidate evidence and none
  is direct production/external CANDU evidence.
- `P4-T06-G4J` supplies a separate representative reduced-model case with
  explicit burnup/refuelling/RRS/poison state and event records, but it does
  not replace the missing external CANDU numeric authority or approve a
  tolerance. `G4-R5` separately reviewed that case and recorded
  `BLOCKED` / `NO-ADMISSION`; no reduced-model golden or tolerance profile was
  admitted.
- `P4-T06-G4K` and `G4-R6` have resolved and admitted the reduced-model
  authority gap for the five bounded scenarios with independently generated
  expected values, complete provenance, P2-T05 profile/digest records, six
  approved profiles, and zero-failure T3 evidence. The original candidate
  remains preserved as `Candidate`/`Deferred`/`NoGolden`; review receipt
  telemetry is explicitly `UNVERIFIED`.
- No Unity game feature, mobile device evidence, signing identity, or release
  artifact is implied by the completed Core work.

## Maintenance contract

- Update this register and the private roadmap whenever a phase, gate
  disposition, approved specification, runtime model, or current task frontier
  changes. Keep the history here short; detailed commands and findings belong
  in the linked task/gate records.
- Regenerate the student-facing physics guide and run its consistency check
  when an approved physics specification, runtime model, gate disposition, or
  game-loop contract changes, as required by the implementation plan.
- Keep the private site's bundled physics-guide PDF byte-identical to the
  generated output; hosting or publication remains separately authorized.
- Every bounded task ends with `docs/tasks/<TASK-ID>.md`, including actual
  model/review evidence, validation commands/results, risk/gate disposition,
  available token/cost fields, and the next eligible task.

## Canonical records

- [Repository rules](../AGENTS.md)
- [Implementation plan](Implementation_plan.md)
- [Phase 2 specifications](spec/)
- [P1-T08 literature digest](reference/candu-literature-digest-v1.md)
- [G2 forced-closure addendum](gates/G2-FC-01.md)
- [G2-R4 technical rerun](gates/G2-R4.md)
- [G3 state-contract review](gates/G3.md)
- [P4-T03 handoff](tasks/P4-T03.md)
- [P4-T04 outer convergence and diagnostics](tasks/P4-T04.md)
- [P4-T05 synthetic solved-case coverage](tasks/P4-T05.md)
- [P4-T06 DONJON5 admission audit](tasks/P4-T06.md)
- [P4-T06-R1 rights and reduced-runtime boundary](tasks/P4-T06-R1.md)
- [P4-T06-R2 path-free DONJON5 admission audit](tasks/P4-T06-R2.md)
- [P4-T06-R2A CANDU source-profile applicability decision](tasks/P4-T06-R2A.md)
- [P4-T06-R2B CANDU source-to-Core mapping and authority audit](tasks/P4-T06-R2B.md)
- [P4-T06-R2C final Candu6 source-to-P2-T02 mapping authority audit](tasks/P4-T06-R2C.md)
- [P4-T06-R2D reduced/synthetic model admission disposition](tasks/P4-T06-R2D.md)
- [P4-T06-R3 reduced-model/interpolation boundary](tasks/P4-T06-R3.md)
- [P4-T06-R4 offline candidate interpolation pack and validator](tasks/P4-T06-R4.md)
- [Reduced-model/interpolation boundary specification](spec/reduced-model-interpolation-boundary-v1.md)
- [Round 2026-08-16 P4 reference/data follow-up chain](tasks/ROUND-2026-08-16-P4-REFERENCE-CHAIN.md)
- [Round 2026-08-16 G4 block-resolution follow-up chain](tasks/ROUND-2026-08-16-G4-BLOCK-RESOLUTION-CHAIN.md)
- [P4-T06-G4F candidate algebraic spatial reference](tasks/P4-T06-G4F.md)
- [P4-T06-G4G bounded baseline and coverage admission](tasks/P4-T06-G4G.md)
- [P4-T06-G4H numeric baseline/tolerance decision](tasks/P4-T06-G4H.md)
- [P4-T06-G4I manufactured synthetic spatial authority candidate](tasks/P4-T06-G4I.md)
- [G4-R4 synthetic-only spatial authority gate](gates/G4-R4.md)
- [G4-R4 gate task report](tasks/G4-R4.md)
- [P4-T06-G4J representative reduced-model sequence candidate](tasks/P4-T06-G4J.md)
- [G4-R5 reduced-model disposition gate](gates/G4-R5.md)
- [G4-R5 reduced-model disposition task report](tasks/G4-R5.md)
- [P4-T06-G4K independent reduced-model G4 authority task](tasks/P4-T06-G4K.md)
- [G4-R6 fresh reduced-model G4 disposition](gates/G4-R6.md)
- [G4-R6 gate task report](tasks/G4-R6.md)
- [P4-T08 synthetic Core benchmark baseline](tasks/P4-T08.md)
- [P5-T01 declarative S4/S8 scheme metadata](tasks/P5-T01.md)
- [P5-T02 atomic directional shift and identity movement](tasks/P5-T02.md)
- [P5-T03 named refuelling events and discharge audit](tasks/P5-T03.md)
- [P5-T04 explicit-time burnup integration](tasks/P5-T04.md)
- [P5-T05 burnup-indexed coefficient-table validation and lookup](tasks/P5-T05.md)
- [P5-T06 affected coefficient and spatial-state recomputation](tasks/P5-T06.md)
- [P5-T07 Phase 5 identity, location, burnup, and energy invariants](tasks/P5-T07.md)
- [P5-T08 deterministic multistep refuelling histories](tasks/P5-T08.md)
- [TEST-INFRA-01 artifact-portable full-suite recovery](tasks/TEST-INFRA-01.md)
- [Documentation-authority audit](tasks/DOC-INSTRUCTIONS-01.md)
