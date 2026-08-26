# XSEC-PLAN-01 Owner Approval

**Recorded:** 2026-08-25

**Status:** APPROVED

**Scope:** bounded definition and execution of the licensed cross-section-data
workstream recorded by `ROUND-2026-08-25-XSEC-DATA-CHAIN.md`

## Owner direction

The repository owner authorizes up to six hours of autonomous, sequential work
to research, obtain, execute, validate, package, integrate, document, and
checkpoint-commit a lawfully usable DRAGON5/DONJON5-derived CANDU cross-section
data pack. The authorization includes official-source online research, use of
external temporary directories, strictly necessary dependency installation,
offline DRAGON5/DONJON5 runs, repository changes within each active task,
Core/CLI/Unity integration, proportionate validation, independent review, and
PDF evidence generation.

`P1-T09` is not registered by the implementation plan or current delivery
register. This approval therefore uses the separate `XSEC-*` namespace and
starts with the routing-only task `XSEC-PLAN-01`.

## Boundaries and admission conditions

- Execute one task ID at a time and commit a checkpoint after each completed
  task. Preserve unrelated work and historical evidence.
- DRAGON5 and DONJON5 remain offline reference tools and may never become
  runtime dependencies.
- The P1-T02/P1-T03 cases may be rerun only as external-private provenance
  smoke cases unless a later legal and technical admission establishes the
  required rights and exact mapping.
- `XSEC-01` must establish verifiable permissions for acquisition, execution,
  derivation, retention, and any proposed redistribution before candidate data
  can enter the repository.
- Missing source-to-runtime field definitions, energy-group boundaries/order,
  collapse or homogenization rules, units, normalization, convergence,
  interpolation, tolerances, or geometry mappings require the separate
  specification task `XSEC-02`; they may not be inferred from owner approval.
- Any candidate runtime pack must be versioned, path-free, deterministic,
  checksum-bound, schema-validated, finite, converged, unit-explicit, and
  fail-closed. It may contain only fields admitted by an approved specification.
- `ReactorSim.Core` remains engine-neutral, `ReactorSim.Cli` remains the
  deterministic harness, and Unity remains a thin graphical adapter consuming
  only the validated runtime pack.
- No output may be called licensed, real, authoritative, golden, production,
  or Unity-ready until the workstream's legal, provenance, reference,
  comparison, Core/CLI, Unity, and gate evidence all support that claim.

## Explicit non-authorization

This record is not legal advice or third-party license proof. It does not select
a nuclear-data library, source case, numerical value, energy boundary,
collapse/homogenization method, equation, unit, schema, tolerance, convergence
rule, or golden baseline. It does not authorize OpenMC; runtime native reference
dependencies; publication, signing, or store submission; host-security or
firewall changes; or excluded shutdown, scram, accident, full
thermal-hydraulic, CFD, or operator-training behavior.

If an authority conflict or critical blocker defined by `AGENTS.md` is reached,
only the dependent work stops; independent eligible work may continue.
