# XSEC-RUNNER-01 - reproduce the official TCWUX11 runner context

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** user authorization for offline DRAGON5/DONJON5/JEF/JEFF work,
the XSEC rights attestation, and the critical reproducibility blocker recorded
in `XSEC-SPH-01.md`.

## Objective

Establish the exact external-only executable launch context required to run the
hash-bound official Version5 TCWUX11 JEFF procedure unchanged through all four
source assertions. Determine the procedure-loader/environment requirements
without changing the official source procedure, assertions, nuclear data,
physics mapping, or runtime data contract.

## Constraints

- Keep all raw sources, JEF/JEFF/WLUP data, compiled tool data, temporary
  drivers, and logs outside the repository. Commit only path-free manifests
  and reports.
- Bind each attempted launch to the container/image digest, platform,
  executable hash, command, fixture/source hashes, environment variables,
  working-directory topology, exit status, and log hashes.
- Begin from the official source entry point. Any targeted driver is a
  diagnostic only and must prove its loader equivalence before it can support
  a source-result claim.
- Do not alter, bypass, remove, rebaseline, or conditionally suppress any
  TCWUX11 or shared assertion. Do not introduce a pre-SPH capture, change a
  mapping, export group constants, or call any data golden/runtime-ready.
- If the runner needs a missing external binary, image, procedure search path,
  platform, compiler setting, or inaccessible source component, record that
  exact blocker. Do not substitute another reference case or tool version.

## Required evidence and validation

- T0: hash and manifest validation for each external fixture/attempt.
- T1: a pristine TCWUX11 execution reaches all four unchanged source
  assertions, with successful completion; record its external-only run
  manifest. If this is not achieved, report `BLOCKED`.
- T6 is not claimed by this task. It belongs to a later fresh candidate rerun
  after a separately reviewed pre-SPH capture authority exists.
- Independent code review (high), actual telemetry verified or marked
  `UNVERIFIED`.

## Next routing

If and only if the pristine source run succeeds, create a separate bounded
`XSEC-SPH-02` task to prove a non-mutating pre-SPH capture while retaining the
original SPH transition and all four assertions. `XSEC-03-R2` remains
ineligible until that task is complete and reviewed.
