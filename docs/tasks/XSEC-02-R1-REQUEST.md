# XSEC-02-R1 - recovered source-mapping T3 revalidation

**Status:** AUTHORIZED TASK DEFINITION

**Authority:** `XSEC-02.md`, `TEST-INFRA-02.md`, the existing XSEC workstream
owner authorization, and the current `PROJECT_SCOPE.md` routing.

## Objective

Record a clean rerun of XSEC-02's previously blocked, mandatory portable
full-suite T3 after `TEST-INFRA-02` repaired only the CLI test-output data
declarations. Finalize the XSEC-02 candidate-mapping checkpoint without
changing its reviewed technical mapping.

## Frozen inputs and bounds

- `docs/spec/xsec-source-runtime-mapping-v1.md` and
  `reference/manifests/xsec-02-mapping-spec-v1.json` at commit `0a28ed0` are
  frozen. No source case, library identity, geometry, group ordering, field,
  unit, SPH boundary, transformation, tolerance, schema, or runtime decision
  may change.
- `TEST-INFRA-02` at commit `5341068` is the only recovery input. It changes
  test-output declarations only, not runtime lookup or data.
- Applicable P1-T08 digest rows remain `S1-R04`, `S1-R05`, `S5-R02`, `S5-R03`,
  `S5-R04`, `S5-R09`, and `S6-R03`; no new physics conclusion is selected.

The task may create only this request, `docs/tasks/XSEC-02-R1.md`, and the
corresponding `PROJECT_SCOPE.md` update. It may not change implementation,
data, manifests, mapping specification, approvals, or XSEC routing. The prior
same-reviewer high review remains the final mapping review because no candidate
changed; its actual telemetry is `UNVERIFIED`.

## Required validation and disposition

Inspect the frozen mapping identities and working tree, run
`Test-FullHeadlessSuite.ps1 -ConfirmFullSuite` against a fresh temporary
artifact path, and record exact Core/Golden/CLI counters. The task completes
only with zero failures/skips and no unrelated working-tree change. On success,
the scope may mark the candidate mapping checkpoint complete but must preserve
the historical XSEC-02 blocked report and its original artifact-layout finding.
No DRAGON/DONJON run, runtime pack, golden, full-core, release, or Unity claim
is permitted here.

## Next routing

If complete, `XSEC-03` becomes the next eligible task: reproduce the bounded
DRAGON lattice candidate externally with fresh T6 evidence. `XSEC-CORE-01`
continues to be required before XSEC-04.
