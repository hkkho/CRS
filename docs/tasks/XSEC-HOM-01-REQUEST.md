# XSEC-HOM-01 request - source-preserving whole-cell export route

Task ID: XSEC-HOM-01
Requested lane / reasoning: GPT-5.6 Luna / high; independent code review (high)
Objective: Define and prove a separately named, one-mixture whole-cell EDI
output for the existing day-0 and day-300 source states without changing the
official TCWUX11 EDI/SPH calculation path or decoding fields.
Allowed files/subsystems: `docs/tasks/XSEC-HOM-01*`,
`docs/spec/xsec-jeff-lattice-deck-v4.md`,
`docs/spec/xsec-source-runtime-mapping-v3.md`,
`reference/manifests/xsec-hom-01-*.json`, and `docs/PROJECT_SCOPE.md` only.
External-only source procedure/driver/capture variants may be created in a
temporary directory under the pinned DRAGON5 environment.
Approved inputs: `AGENTS.md`, `docs/Implementation_plan.md`,
`docs/PROJECT_SCOPE.md`, `XSEC-RIGHTS-01`, mapping v1/v2,
`xsec-source-mixture-selection-v1.md`, deck v3, `XSEC-SPH-03`, `XSEC-MAP-R2`,
`XSEC-03-R2`, `XSEC-MIX-01`, the completed `P1-T08` report, the approved
`docs/reference/candu-literature-digest-v1.md`, official TCWUX11 and
IGE-335/IGE-351 guides, and this owner approval.
Current scope lookup: `XSEC-MIX-01`
Literature digest applicability: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, used only as methodology/applicability
context. Exact EDI/homogenization semantics must be proven from official
DRAGON guides and the source procedure.

## Required behavior and invariants

- Preserve each original TCWUX11 `EDITION := EDI` call and each following
  `EDITION := SPH` call byte-for-byte and in original order; retain all four
  original source assertions and normal completion.
- Prove no hidden source-path drift: for each state, capture the unchanged
  original `PRE0`/`PRE172` output in two fresh roots from the new external
  variant and require byte identity with the XSEC-03-R2 approved baseline
  capture hash. Bind the four source-assertion fingerprint/delta evidence to
  the established XSEC-03-R2 source-regression baseline. Assertion success
  alone is insufficient.
- Create a separate, explicitly named whole-cell EDI output for each state
  (`WHOLECELL0` and `WHOLECELL172` are candidate object names), using the same
  source inputs and source two-group/4 eV condensation configuration. The new
  EDI must have its own output LHS and never write or replace `EDITION`.
- The candidate whole-cell EDI merge must map exactly the original 31 region
  slots to one explicitly named output mixture; prove from the official source
  and data-structure guides that its resulting MACROLIB is one mixture with a
  one-entry volume record.
- Capture only the named whole-cell output through the previously approved
  FILE-backed interface after the fourth assertion. Run eight fresh isolated
  roots total: two original-output regression captures and two whole-cell
  captures for each state. Hash all source/project-delta/driver/hooks/captures
  and prove same-state byte identity plus cross-state distinction.
- Reject direct `SPH`, `SPH-EPSILON`, or `ADF` record/subtree in each selected
  whole-cell MACROLIB. Do not call it physically SPH-free; preserve source
  history distinction.
- Do not decode, display, calculate, transform, compare, or retain field
  arrays or numerical cross-section values. A successful route only permits a
  future fresh XSEC-03-R2 semantic-decode task.

## Explicit non-goals

- Alter any original source EDI/SPH call, source assertion, material/geometry,
  depletion control, group boundary/order, unit, mapping field, tolerance, or
  convergence criterion.
- Admit a field, runtime data pack, Core/CLI/Unity behavior/schema, full-core
  mapping, DONJON case, reference/golden evidence, release, or publication.

## Validation

- T0: static specification/manifest/procedure-delta identity checks; assert
  distinct whole-cell LHS, unchanged original EDI/SPH statements, 31-to-one
  merge, complete path-free hashes, and no field payload.
- T1: external structural capture audit for eight fresh roots; assert original
  baseline-capture byte identity, four source assertions, source-regression
  fingerprint/delta identity, normal completion, source convergence evidence,
  one MACROLIB mixture/two groups/one volume, absent direct correction subtree,
  and deterministic same-state whole-cell captures.
- T3: full headless Core/Golden/CLI regression because source/deck and mapping
  authority changes.
- T6: fresh DRAGON5 source reruns and manifest comparison.
- Independent code review (high), with actual telemetry verified or `UNVERIFIED`.

## Definition of done

- Reviewed deck v4/mapping v3 either prove a one-mixture named whole-cell
  source output for both states or record an exact fail-closed blocker.
- The source regression/capture proof and required T3/T6 are complete when the
  route succeeds; no field is decoded or admitted.
- The report, scope routing, and checkpoint are complete. A successful route
  names fresh XSEC-03-R2 as its only next task; a blocked route names the exact
  external/source authority conflict.
