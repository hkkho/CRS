# XSEC-MAP-R2 - SPH-preserving source/runtime mapping reconciliation

## Outcome

Status: COMPLETE

The conflicting SPH-omission route has been replaced by a bounded,
SPH-preserving two-knot candidate authority. The new mapping requires the
official source transitions and uses only the verified direct capture selections
PRE0/REF-CASE0001/MACROLIB at elapsed day 0 and
PRE172/REF-CASE0002/MACROLIB at elapsed day 300. The later SPH-dependent
seven-knot source loop is excluded. No source output, decoded cross section,
runtime pack, Core/CLI/Unity change, golden/reference baseline, or release
claim was created.

Effectiveness: SUCCESS

Applicable P1-T08 digest rows: S1-R04, S1-R05, S5-R02, S5-R03, S5-R04, S5-R09,
and S6-R03. They were used only as documented methodology/applicability
context; no row is numerical or golden authority.

## Execution and model evidence

- Role: root implementer; independent code review (high) requested.
- Requested model / reasoning: GPT-5.6 Luna / high; code review (high).
- Actual model / reasoning: UNVERIFIED.
- Execution receipt or telemetry source: local Codex thread; reviewer
  01a03b98-9b86-7f22-9068-76bb1ce6b01e. No provider receipt exposes the
  actual model/effort.
- Attempts: one bounded specification/reconciliation attempt; one reviewer
  candidate pass plus same-reviewer correction recheck; elapsed time:
  unavailable.
- Artifact/checkpoint status: produced: bounded owner approval, task request,
  mapping v2, deck v3, path-free specification manifest, report, and scope
  routing. Raw source artifacts and capture data remain external-only.
- Review disposition: PASS.
- Review evidence verification: UNVERIFIED.
- Reviewer reuse/fresh-review rationale: reused reviewer
  01a03b98-9b86-7f22-9068-76bb1ce6b01e, preserving the XSEC source/capture
  context. The initial disposition was CONDITIONAL PASS with one Medium
  documentation finding; the same reviewer rechecked the correction and issued
  final PASS. No fresh-review swarm was used.

## Files created or changed

- docs/tasks/XSEC-MAP-R2-OWNER-APPROVAL.md - bounded owner direction.
- docs/tasks/XSEC-MAP-R2-REQUEST.md - one-task request, invariant, validation,
  and routing boundary.
- docs/spec/xsec-source-runtime-mapping-v2.md - replacement mapping:
  exact SPH-preserving capture/selector boundary and two-knot profile.
- docs/spec/xsec-jeff-lattice-deck-v3.md - replacement deck-delta authority
  retaining both source SPH calls.
- reference/manifests/xsec-map-r2-spec-v1.json - path-free specification,
  source identity, selector, and non-admission manifest.
- docs/tasks/XSEC-MAP-R2.md - this evidence.
- docs/PROJECT_SCOPE.md - current XSEC routing after this report.

Pre-existing mapping v1, deck v2, task reports, Core contracts, source
procedures, captures, raw data, runtime code, tests, and Unity content were
inspected but not changed.

## Assumptions and design choices

- The owner explicitly authorized this bounded replacement specification. It
  supersedes only the combined v1/v2 source-stage route that conflicts with
  XSEC-03-R1; historical documents remain unmodified.
- XSEC-SPH-03 proves persistent pre-transition capture while retaining both
  source SPH calls and all assertions. The selected paths were independently
  inspected in external captures, not inferred from a listing name.
- The selection is field-scoped. Direct correction subtrees are rejected from
  selected MACROLIB payloads, but the specification makes no unsupported claim
  that the complete official source history is physically SPH-free.
- The existing BurnupCoefficientTableV1 requires ordered nonempty rows, strict
  increasing knots, exact/linear lookup, and out-of-range rejection; it imposes
  no larger row-count minimum. The two-knot profile changes neither its
  contract nor its interpolation equation.
- Source transformations, units, group order/boundary, field meanings,
  normalization boundary, numerical controls, and Core exclusions are inherited
  unchanged. No numerical coefficient was selected or changed.

## Validation commands and results

### T0 - specification/manifest static validation

~~~text
PowerShell inline assertion:
- Parse reference/manifests/xsec-map-r2-spec-v1.json.
- Assert task ID, preserved-SPH policy, selectors, ordered day keys 0,300,
  forbidden direct subtrees, path-free files, and no stale SPH-omission rule.
- git diff --check
~~~

Result: PASS, exit 0. The final manifest SHA-256 is
44ba1e3903315aea3e43d50191945d2a409f585165dae1fbc7e4058562bb8668.
The final rerun also confirmed mapping v2 names deck v3, not the retired v2
delta.

### T1 - external capture selector audit

~~~text
PowerShell inline inspection of external XSEC-SPH-03 capture serializations:
- locate the direct REF-CASE/MACROLIB hierarchy rather than a metadata name;
- verify PRE0/REF-CASE0001/MACROLIB timestamp day 0;
- verify PRE172/REF-CASE0002/MACROLIB timestamp day 300;
- verify each selected payload has the documented two-group ENERGY identity
  and no direct SPH, SPH-EPSILON, or ADF subtree.
~~~

Result: PASS, exit 0. Both selectors were unique at the required hierarchy,
each selected payload reported three ENERGY boundaries (two groups) with the
4 eV boundary, and neither selected payload contained a forbidden direct
correction subtree. Raw records and numeric values remain external-only.

### T3 - full headless Core/Golden/CLI regression

~~~text
$env:DOTNET_ROOT=<external user cache for exact SDK 10.0.302>
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
& .\tools\Test-FullHeadlessSuite.ps1 -ConfirmFullSuite -ArtifactsPath <external artifact root>
~~~

Result: PASS, exit 0 on the repository-pinned SDK 10.0.302. TRX counters:
Core 208/208, Golden 26/26, and CLI 38/38, with zero failures and zero skips.

The first required-wrapper attempt was BLOCKED before test execution because
global.json pins SDK 10.0.302 with roll-forward disabled, while only 10.0.303
was initially installed. The exact 10.0.302 SDK was downloaded from Microsoft's
build feed into an external user cache; neither global.json nor repository
toolchain configuration changed. The rerun used that exact pinned SDK.

### Independent code review (high)

~~~text
Reviewer: 01a03b98-9b86-7f22-9068-76bb1ce6b01e
Candidate: owner approval, request, mapping v2, deck v3, and manifest.
~~~

Initial result: CONDITIONAL PASS. The sole Medium finding was that mapping v2
named deck v2 instead of replacement deck v3. The mapping was corrected and T0
rerun. Final result from the same reviewer: PASS, no remaining findings.
Actual reviewer model/reasoning telemetry: UNVERIFIED.

## Token and cost accounting

| Field | Value | Source/notes |
|---|---:|---|
| Input tokens | Unavailable | No task-level telemetry exposed. |
| Cached input tokens | Unavailable | No task-level telemetry exposed. |
| Cache-write input tokens | Unavailable | No task-level telemetry exposed. |
| Output tokens | Unavailable | No task-level telemetry exposed. |
| Reasoning output tokens | Unavailable | No task-level telemetry exposed. |
| Total tokens | Unavailable | No verified request-level receipt. |
| Estimated cost | Unavailable | No verified price/usage receipt. |
| Goal-service total | Not allocable | Not added to task totals. |

Cost formula/basis: unavailable; no task-level provider billing telemetry was
exposed.

## Numerical differences

Not applicable; no equation, numerical coefficient, runtime numerical
behavior, source result, or golden/reference baseline changed. The two elapsed
day keys are source-selection identities, not generated cross-section values.

## Deferred validation

- Fresh DRAGON5 source reruns, all source assertion/convergence records,
  semantic decode, deterministic export comparison, and T6 are deferred to
  XSEC-03-R2, which owns source execution and any candidate admission.
- Converter/pack, Core/CLI consumer, Unity consumer, full-core/DONJON case,
  plots/PDF, and golden/reference admission remain owned by later named XSEC
  tasks and applicable gates.
- No T4/T5 applies because no Core/Unity interface or serialization contract
  changed.

## Blockers, risks, and follow-up

- Blockers: none for this bounded specification task.
- Risk triggers: mapping/deck authority change triggered T3 and independent
  high review. The first T3 attempt exposed a pinned-SDK environment mismatch;
  it was resolved using the exact pinned SDK without changing repository
  configuration.
- Risks accepted or deferred: the two-state candidate has not yet been rerun,
  decoded, or admitted. The source history/provenance distinction is explicit;
  any direct correction-field, selector, source identity, unit, convergence,
  or license discrepancy must fail closed in XSEC-03-R2.
- Follow-up work: execute XSEC-03-R2 as the next task. Do not use the
  historical seven-knot route or retire current candidate boundaries without a
  separate reviewed authority.

## Next eligible task

XSEC-03-R2 - execute the fresh SPH-preserving two-knot source semantic-export
candidate under mapping v2/deck v3; it owns T6, decoding, mapping validation,
and any later candidate-admission decision.

Source: AGENTS.md, XSEC-MAP-R2-REQUEST.md, and XSEC-SPH-03.md.

