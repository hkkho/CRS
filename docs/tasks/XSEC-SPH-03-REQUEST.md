# XSEC-SPH-03 - official persistent pre-SPH capture interface proof

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-SPH-03-OWNER-APPROVAL.md`; the existing bounded
internal-use authority in `XSEC-RIGHTS-01-OWNER-APPROVAL.md`; and the completed
external runner contract in `XSEC-RUNNER-01.md`.

## Objective

Establish, through official DRAGON5/CLE-2000 evidence and clean execution in
the pinned `rdragon` context, one persistent external capture for each required
post-EDI/pre-SPH TCWUX11 state without changing the source SPH state
transition. The outcome is a reviewed source-interface authority that either
makes `XSEC-03-R2` eligible or records why no supported interface exists.

## Approved inputs

- `AGENTS.md`, `docs/Implementation_plan.md`, and `docs/PROJECT_SCOPE.md`.
- `XSEC-SPH-02.md`, `XSEC-RUNNER-01.md`, `XSEC-03-R1.md`,
  `XSEC-DECK-01.md`, `XSEC-02-R1.md`, and their path-free manifests.
- `docs/spec/xsec-source-runtime-mapping-v1.md` and
  `docs/spec/xsec-jeff-lattice-deck-v2.md`, read as historical inputs only:
  their two-SPH-omission rule remains invalidated and may not be reused.
- The completed P1-T08 report/digest, with rows `S1-R04`, `S1-R05`,
  `S5-R02`, `S5-R03`, `S5-R04`, `S5-R09`, and `S6-R03` recorded as method and
  applicability context only.
- Official DRAGON5/CLE-2000 primary documentation and source examples for the
  exact pinned release, plus the hash-bound external TCWUX11/TCWU05Lib/assertS/
  WLUP172 fixture and XSEC-RUNNER-01 launch contract.

## Required invariants

- Preserve the official SPH statements exactly as
  `EDITION := SPH: EDITION VOLMATF INTLINF ;`; do not substitute an alias as
  the SPH RHS or change source object identity.
- Preserve every source assertion lexeme and require all four assertions to
  pass with the exact XSEC-RUNNER-01 baseline deltas.
- Capture only after the relevant `EDI:` object has been formed and before its
  associated `SPH:` call. The capture's actual persistence mechanism may run
  after the source assertion sequence only if its retained bytes identify that
  exact pre-SPH object and it is proven non-mutating.
- Use an interface supported by the official pinned DRAGON5 release. Bind the
  syntax example/manual section, tool/version/executable/container identity,
  object/parameter direction, object lifetime, serialization target, and
  source/driver/hook/result SHA-256 values in a path-free manifest.
- Run two clean candidate roots and compare each to the two clean
  XSEC-RUNNER-01 baselines. Require normal source completion, finite values,
  no source compilation diagnostic, no NaN/Inf, and byte-identical retained
  semantic captures across the two candidates.
- Do not decode, map, convert, or admit a field; do not create a runtime pack
  or change Core/CLI/Unity. This task proves the capture interface only.

## Explicit non-goals

- Changing equations, source assertions, source physics, numerical tolerances,
  convergence controls, units, group ordering/boundaries, mappings, deck
  authority, schemas, or golden/reference data.
- Treating a crash, failed cleanup, deleted sequence, or a listing-only record
  as a persistent capture.
- Re-running XSEC-03-R2, performing T6 export admission, or claiming that
  XSEC-03-R2 will pass before it is independently executed.
- Retaining raw nuclear data, DRAGON/DONJON executables, modified source decks,
  LCM dumps, or full listings in the repository.

## Validation

- T0: verify the external fixture, pinned launcher/container/executable,
  official interface evidence, and exact candidate deltas/hashes.
- T1: two clean candidate runs; four assertion deltas must match the clean
  baselines; retained capture hashes must match across candidates; inspect the
  source listing order and object identity evidence.
- T3: required if, and only if, a deck/mapping authority revision is proposed;
  run the full Core/Golden/CLI wrapper before review.
- T6: not part of this task. It remains owned by XSEC-03-R2 after a PASS.
- Independent code review (high): required for the final capture-interface
  disposition. Actual reviewer model/reasoning telemetry must be verified or
  recorded as `UNVERIFIED`.

## Stop conditions and routing

Stop BLOCKED on an assertion mismatch, unresolved source-object identity or
serialization timing, unsupported/ambiguous CLE syntax, non-normal completion,
non-deterministic capture bytes, invalid data, or a need to change any
approved physics/mapping contract. Do not invent a task ID for the resulting
authority revision: if the capture proof succeeds, write the required task
report first, update scope, and then route separately to `XSEC-03-R2`.

## Task-definition review

Independent code review (high) disposition: PASS. The reviewer confirmed that
the owner authority, task boundary, and routing preserve original SPH/assertion
semantics and prohibit premature data, mapping, runtime, or XSEC-03-R2 pass
claims. Reviewer `01a03b98-9b86-7f22-9068-76bb1ce6b01e`; actual model and
reasoning telemetry: UNVERIFIED.
