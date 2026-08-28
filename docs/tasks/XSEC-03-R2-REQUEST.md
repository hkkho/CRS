# XSEC-03-R2 - SPH-preserving DRAGON lattice semantic export

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** XSEC-03-R2-OWNER-APPROVAL.md, XSEC-RIGHTS-01 owner
internal-use authority, XSEC-MAP-R2 mapping v2/deck v3, and completed
XSEC-SPH-03 capture-interface evidence.

## Objective

Perform two independent fresh external DRAGON5 runs per approved selector (four
fresh runs total) of
xsec-c6-37-nu-jeff31-4ev-v3-sph-preserved-2k. Preserve both official source SPH
calls and all four source assertions; retain PRE0 and PRE172 through the proved
FILE-backed capture interface; semantically validate only the approved direct
MACROLIB selectors; and record path-free T6 provenance/determinism evidence.

## Allowed files/subsystems

- docs/tasks/XSEC-03-R2-OWNER-APPROVAL.md;
- docs/tasks/XSEC-03-R2-REQUEST.md;
- docs/tasks/XSEC-03-R2.md;
- reference/manifests/xsec-03-r2-run-semantic-v1.json; and
- docs/PROJECT_SCOPE.md after the report is complete.

All modified source procedure copies, callers, access hooks, raw captures,
listings, validator work files, and decoded values remain external-only.

## Approved inputs

- AGENTS.md, docs/Implementation_plan.md, current XSEC scope records, mapping
  v2/deck v3, and the mapping manifest;
- XSEC-03-R1, XSEC-RUNNER-01, XSEC-SPH-02, XSEC-SPH-03, and XSEC-MAP-R2 reports
  plus their path-free manifests;
- existing XSEC-02 mapping v1 only for inherited source-field meanings and
  transformations; and
- P1-T08 report/digest rows S1-R04, S1-R05, S5-R02, S5-R03, S5-R04, S5-R09,
  and S6-R03 as methodology context only.

## Required behavior and invariants

- Hash and prove the exact source procedures, shared assertion procedure,
  pinned JEFF/WLUP library, Dragon and rdragon executables, image/platform,
  driver, capture procedure, and access hook before each run.
- Execute two clean roots per selector with network disabled, read-only
  container root, executable tmpfs /tmp, tmpfs /run, and the runner's
  one-thread environment. All four runs must pass the four unchanged source
  assertions and normal source completion.
- For every run, extract and hash-bind the source-emitted convergence and
  iteration evidence using an exact recorded listing-detection rule. The
  record must identify every source solver completion marker, relevant
  iteration/status line, and any abnormal/nonconverged diagnostic. A missing
  or ambiguous status fails closed; no project tolerance may be inferred.
- The FILE-backed interface retains exactly one selector per run. The two PRE0
  captures must match each other, the two PRE172 captures must match each
  other, and the two selected states must differ.
- Decode only PRE0/REF-CASE0001/MACROLIB at elapsed day 0 and
  PRE172/REF-CASE0002/MACROLIB at elapsed day 300. Reject every other source
  state, including the later two-group loop.
- Fail closed unless each direct selected payload is unique, two-group fast then
  thermal at the documented 4 eV boundary, has no direct SPH, SPH-EPSILON, or
  ADF subtree, has finite required source fields with explicit inherited units,
  and passes every inherited mapping invariant.
- Record source-to-Core transformations and per-field finite/invariant results
  only as path-free checks and hashes. Do not retain decoded cross-section
  values or a data pack.
- T6 is mandatory. Run T3 only if a repository runtime/numerical interface or
  data-pack artifact changes; none is authorized here. Independent code review
  (high) is mandatory for final disposition, reusing reviewer
  01a03b98-9b86-7f22-9068-76bb1ce6b01e with telemetry verified or UNVERIFIED.

## Explicit non-goals

- Changing any approved source, mapping, equation, unit, normalization,
  tolerance, runtime contract, or data schema.
- Claiming reference/golden/runtime-pack/full-core/Unity admission.
- Retaining or redistributing raw source data, tools, decks, captures, listings,
  or numerical cross sections.

## Definition of done and routing

A PASS requires two byte-identical captures per selected state, distinct states,
hash-bound source assertion/convergence/iteration success, selector/field/unit/invariant validation,
deterministic semantic fingerprints, and final independent high-review PASS.
A PASS remains candidate-only. Report BLOCKED on any failed condition. Update
scope only after the report and stop. The next task must be selected from the
current scope; no later work is included in this task.
