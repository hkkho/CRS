# XSEC-DECK-01 - exact JEFF lattice deck authority

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** the owner-authorized XSEC workstream and the critical frozen-deck
conflict recorded in `XSEC-03.md`.

## Objective

Resolve, document, and independently review the exact DRAGON5/JEFF lattice
deck basis that may replace the currently non-executable XSEC-03 input. The
task must determine whether the project retains TCWU11 with explicitly bounded
assertion handling or adopts the official JEFF `TCWUX11` procedure, and must
update the mapping authority only if the evidence supports one exact choice.

## Required boundaries

- Inspect and hash-bind both source procedure candidates, the shared assertion
  procedure, JEFF library procedure, tool/container identity, and all material
  geometry/tracking/depletion/export differences.
- Treat the assertion values as regression checks, not reproduced case output.
  Do not replace them with a calculated value or loosen their tolerance.
- If assertion removal is proposed, establish whether it is a non-mutating test
  harness omission or a material change to the admissible case, bind exact
  omitted lexemes, and require explicit owner/specification approval plus
  independent code review (high).
- If `TCWUX11` is proposed, compare every semantic difference to TCWU11 and
  either prove it fits the approved candidate scope or create a new candidate
  mapping identity. Do not silently substitute it.
- No DRAGON/DONJON result, source export, pack, Core/CLI/Unity integration, or
  golden claim may be made in this decision task.

## Validation and completion

Produce a path-free comparison record, updated/replacement mapping specification
only when justified, focused identity/diff checks, and one independent code
review (high) with actual telemetry verified or marked UNVERIFIED. Reconcile
scope and commit. XSEC-03 may resume only after this task records an exact,
reviewed executable deck authority.
