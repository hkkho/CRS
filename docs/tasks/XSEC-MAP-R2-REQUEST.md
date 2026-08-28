# XSEC-MAP-R2 - SPH-preserving source/runtime mapping reconciliation

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-MAP-R2-OWNER-APPROVAL.md`, the bounded internal-use
authority in `XSEC-RIGHTS-01-OWNER-APPROVAL.md`, and the completed
`XSEC-SPH-03` capture-interface proof.

## Objective

Replace the internally inconsistent `SPH`-omission source/deck route with one
bounded, evidence-backed authority for `XSEC-03-R2`: preserve the official
source state transitions and capture only the two exact pre-transition source
records needed for a two-knot candidate table.

## Allowed files/subsystems

- `docs/spec/xsec-source-runtime-mapping-v2.md`;
- `docs/spec/xsec-jeff-lattice-deck-v3.md`;
- `reference/manifests/xsec-map-r2-spec-v1.json`;
- `docs/tasks/XSEC-MAP-R2-OWNER-APPROVAL.md`;
- `docs/tasks/XSEC-MAP-R2-REQUEST.md`;
- `docs/tasks/XSEC-MAP-R2.md`; and
- `docs/PROJECT_SCOPE.md` after the report is complete.

No source deck, raw data, result, runtime code, Unity asset, or package may be
changed.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, and the `XSEC-03-R2` scope
  lookup in `docs/PROJECT_SCOPE.md`;
- `docs/spec/xsec-source-runtime-mapping-v1.md`,
  `docs/spec/xsec-jeff-lattice-deck-v2.md`, and
  `docs/spec/reduced-model-interpolation-boundary-v1.md`;
- `XSEC-02.md`, `XSEC-DECK-01.md`, `XSEC-03-R1.md`,
  `XSEC-RUNNER-01.md`, `XSEC-SPH-02.md`, and `XSEC-SPH-03.md`, with their
  path-free manifests;
- the complete P1-T08 report/digest. Applicable context-only rows are
  `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03`;
  none supplies a numeric, golden, or runtime authority; and
- the official DRAGON5 Version5 FAQ/IGE-335 source-stage evidence recorded by
  `XSEC-SPH-03`, plus its two external capture identities.

## Required behavior and invariants

- Retain both exact official `EDITION := SPH: EDITION VOLMATF INTLINF ;`
  source calls, their placement, every source assertion lexeme, and every
  source-controlled numerical/physics lexeme.
- Use only `XSEC-SPH-03`'s official FILE-backed `SEQ_ASCII`/`PARAMETER`
  capture interface. It must capture the first post-`EDI` state before the
  first source `SPH`, and the second post-`EDI` state before the second source
  `SPH`, without changing either source transition.
- Define source selection, not a new calculation: day 0 is exactly
  `PRE0/REF-CASE0001/MACROLIB`; day 300 is exactly
  `PRE172/REF-CASE0002/MACROLIB`. The selected 300-day timestamp must report
  elapsed day 300. Every other record, including the later two-group loop, is
  rejected for this candidate.
- The selection is field-scoped, not a claim that a source history lacks SPH.
  A selected `MACROLIB` must have no direct `SPH`, `SPH-EPSILON`, or `ADF`
  subtree; source-history metadata elsewhere in the captured object is
  provenance only and is not decoded into runtime fields.
- Preserve v1's reaction-field meanings, units, transformations, group order,
  4 eV boundary, fail-closed validation, Core exclusions, and no-extrapolation
  runtime-table behavior unchanged. A two-row table is valid under the
  existing Core contract; its only candidate interpolation domain is between
  its two converted knots.
- Preserve the seven-knot route only as historical, invalidated evidence. Do
  not synthesize intermediate knots, fit values, or call the two selected
  states equilibrium/refuelled/control/poison/golden/reference data.

## Explicit non-goals

- Running `XSEC-03-R2`, semantic export/decode, T6, data admission, or pack
  creation.
- Choosing or changing equations, units, conversions, source numerical
  controls, convergence criteria, tolerances, geometry, materials, group
  boundary/order, Core contract, public schema, or runtime behavior.
- Retaining raw source artifacts or making any licensing, redistribution,
  production, golden, or Unity-readiness claim.

## Validation

- T0: inspect inherited/overridden clauses; validate the manifest as JSON;
  confirm source hashes, selectors, forbidden subtrees, and two ordered
  elapsed-day keys are explicit and path-free.
- T1: inspect the two `XSEC-SPH-03` captures externally: each selector exists;
  selected MACROLIB subtrees have the exact two-group ENERGY identity; the
  day-300 selector's timestamp is 300; and no forbidden subtree occurs within
  either selected payload.
- T3: required because a source/mapping authority changes. Run the full
  Core/Golden/CLI headless wrapper before review. T6 remains owned by
  `XSEC-03-R2`.
- Independent code review (high): required. Reuse reviewer
  `01a03b98-9b86-7f22-9068-76bb1ce6b01e`; verify actual telemetry or record
  it as `UNVERIFIED`.

## Definition of done and routing

The replacement specifications must plainly supersede only the conflicting
combined v1/v2 route, preserve historical documents, and make exactly one
subsequent task eligible: `XSEC-03-R2`, which owns fresh T6 reruns, semantic
decoding, mapping validation, and any admission decision. Write the report,
update scope after it, checkpoint the task, and stop.
