# XSEC-SPH-02 - prove non-mutating pre-SPH capture for TCWUX11

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** user authorization for offline DRAGON5/DONJON5/JEF/JEFF work,
the XSEC rights attestation, and the completed runner context in
`XSEC-RUNNER-01.md`.

## Objective

Use the reproduced `rdragon` launch context to prove an exact source-supported
method for retaining only the post-EDI/pre-SPH TCWUX11 objects required by the
approved mapping, while preserving every later SPH-driven source transition
and all four unchanged source assertions.

## Constraints

- Start from the hash-bound official TCWUX11, TCWU05Lib, assertS, and WLUP172
  fixture. Raw files, modified candidate drivers, and outputs remain
  external-only.
- Do not remove, change, bypass, rebaseline, or conditionally suppress any
  source assertion. Do not change source physics, data, equations, units,
  tolerances, group structure, normalization, runtime schema, or mapping.
- A candidate may only add separately named source objects/serializations when
  IGE-335 semantics prove that the original SPH-corrected `EDITION` still
  drives the subsequent state transition exactly as before.
- Bind every candidate object name, source location, capture timing,
  serialization name, input/output hash, and assertion comparison in a
  path-free manifest. SPH/ADF-corrected fields remain excluded from Core.
- No group constants, pack, Core/CLI/Unity integration, golden/reference, or
  full-core claim is authorized by this task.

## Required validation

- T0/T1: two clean candidate runs and two clean unmodified baseline runs using
  the runner contract in `xsec-runner-01-provenance-v1.json`; all four source
  assertions must pass and candidate/baseline assertion deltas must match.
- T1: prove the capture artifact is pre-SPH and that subsequent source
  transitions use the original SPH-corrected state.
- T3: if and only if a mapping/deck authority revision is proposed, run the
  required Core/Golden/CLI wrapper and independent code review (high).
- Do not call T6, data admission, or a fresh XSEC-03 export complete here.

## Stop conditions and routing

Stop `BLOCKED` on any assertion difference, nonconvergence, NaN/Inf, source
object/serialization ambiguity, or inability to prove pre-SPH state. On a
successful independently reviewed authority revision, route separately to
`XSEC-03-R2` for fresh T6 source runs; otherwise preserve the blocker.
