# XSEC-MIX-01 owner approval - bounded source-mixture selection

## Owner direction

The project owner directed: "Create and execute the mixture-selection
specification task."

This authorizes `XSEC-MIX-01` to inspect the pinned external DRAGON5 source
procedure and the already-proven, external-only `PRE0` and `PRE172` captures;
create a narrowly bounded selection specification only where the official
source and data-structure evidence identify one exact mixture; and record the
result. The work may create task evidence, a specification, and a path-free
manifest in the repository.

## Boundaries

- The task may select only a source mixture identity and locator for the two
  mapping-v2 capture selectors. It may not select a numerical cross section,
  calculate, decode, convert, retain, or publish field values.
- The task must preserve the TCWUX11 source procedure, two source SPH calls,
  mapping-v2 two-state boundary, units, group ordering, field transformations,
  and all existing non-authorizations.
- Raw DRAGON inputs, captures, LCM serializations, listings, library bytes,
  and numerical values stay external to the repository.
- The task may not create a runtime pack, alter Core/CLI/Unity code or schema,
  admit golden/reference evidence, or select a full-core/DONJON mapping.
- If official evidence does not name a unique, source-supported mixture and
  locator, the task must remain `BLOCKED`; it must not infer an ordinal,
  volume fraction, or whole-cell composition.

## Required evidence

The task must bind the relevant official procedure and data-structure evidence
by hash/locator, prove the same identity in both fresh capture states, obtain
independent code review (high), report the decision and its limits, reconcile
`PROJECT_SCOPE.md`, and checkpoint the result.
