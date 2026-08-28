# XSEC-HOM-01 owner approval - source-preserving whole-cell export route

## Owner direction

Following the completed XSEC-MIX-01 blocker record, the project owner
authorized creation of a new task to complete the missing requirement.

This authorizes `XSEC-HOM-01` to define, execute externally, and prove a
bounded DRAGON5 source/deck and mapping route that creates a separately named,
whole-cell-homogenized two-group output for each approved state, while keeping
the official TCWUX11 source route intact.

## Hard boundaries

- The original `EDITION` EDI calls, both SPH calls, source order, solver
  controls, source assertions, geometry/material inputs, group boundary, and
  original source output must remain unchanged.
- A new whole-cell EDI output may be created only as a separate named output
  object from the same source inputs; it must not replace, mutate, or become an
  operand of the original `EDITION`/SPH path.
- The task may inspect/run external source inputs and captures, and create
  external procedures/drivers only. Raw procedures, captures, libraries,
  listings, and numerical values remain outside the repository.
- The task may create/revise only the bounded source/deck and mapping
  specification, path-free manifests, task evidence, and scope routing. It
  may not create a runtime pack, decode/commit cross-section values, change
  Core/CLI/Unity code or schema, admit a golden/reference case, create a
  DONJON/full-core mapping, release, or publication.
- If the separate output cannot be made one-mixture, fails source regression,
  contains a direct correction subtree, lacks source-supported semantics, or
  differs across fresh runs, the task must stop `BLOCKED` without weakening the
  invariant.
