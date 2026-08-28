# XSEC-MIX-01 request - source mixture selection specification

Task ID: XSEC-MIX-01  
Requested lane / reasoning: GPT-5.6 Luna / high; independent code review (high)  
Objective: Define and prove, or fail closed on, the exact source mixture
identity and locator required to decode one homogenized whole-cell MACROLIB
record from each mapping-v2 capture state.  
Allowed files/subsystems: `docs/tasks/XSEC-MIX-01*`,
`docs/spec/xsec-source-mixture-selection-v1.md`,
`reference/manifests/xsec-mix-01-selection-v1.json`, and
`docs/PROJECT_SCOPE.md` only. External-only inspection may use the pinned
DRAGON5 container and the proved capture interface.  
Approved inputs: `AGENTS.md`, `docs/Implementation_plan.md`,
`docs/PROJECT_SCOPE.md`, `XSEC-RIGHTS-01`, mapping v1/v2, deck v3,
`XSEC-SPH-03`, `XSEC-MAP-R2`, `XSEC-03-R2`, the pinned official TCWUX11
procedure/data-structure guidance, and this owner approval.  
Current scope lookup: `XSEC-03-R2`  
Literature digest applicability: `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
`S5-R04`, `S5-R09`, and `S6-R03`, used only as methodology/applicability
context; the mixture locator must come from the official source/data-structure
evidence, not literature.  

## Required behavior and invariants

- Inspect the exact source procedure and its prepared `PRE0`/`PRE172` captured
  `REF-CASE.../MACROLIB` structure without decoding or retaining numerical
  cross-section values.
- Establish a single exact locator that is valid in both selected capture
  states, bind it to the official source semantics, and state its
  whole-cell/homogenization meaning and units boundary.
- Prove the locator is unique and has not been selected by array order,
  positional guess, volume weighting, or an unapproved derived mixture.
- Preserve all mapping v2/deck v3 source and exclusion boundaries. A successful
  selector specification does not itself admit a candidate field, runtime pack,
  reference/golden case, full-core case, Core/CLI/Unity behavior, or new schema.
- Fail closed if evidence names multiple possible mixtures, if the two captures
  differ in the proposed locator/meaning, or if the source semantics or units
  cannot be proven.

## Explicit non-goals

- Decode, display, transform, compare, or commit coefficient values.
- Modify DRAGON5 source/decks, source SPH behavior, mapping field rules, units,
  group structure, burnup rule, convergence rule, or tolerances.
- Produce converter/pack/Core/CLI/Unity/DONJON work or golden/reference claims.

## Validation

- T0: schema/path-free/identity static checks for the new selection manifest and
  specification, including source/procedure hashes and a complete locator.
- T1: external, read-only proof against fresh or hash-bound `PRE0` and `PRE172`
  capture structures that the selected locator is unique, structurally present,
  and source-supported without exposing field values.
- Risk escalation: selection of a source numerical row triggers independent
  code review (high). T3/T4/T5/T6 are not run unless this task changes a
  runtime contract, source baseline, numerical artifact, or another named gate
  requires them.

## Definition of done

- A bounded specification either names a source-supported exact mixture locator
  for both captures or records a fail-closed blocker.
- The decision is independently reviewed at high level, all evidence remains
  path-free/non-numerical, the report is complete, and current scope routing is
  reconciled.
- The task ends after its checkpoint; `XSEC-03-R2` is rerouted only when this
  selector decision is a reviewed PASS.
